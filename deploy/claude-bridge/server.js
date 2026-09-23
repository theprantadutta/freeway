#!/usr/bin/env node
"use strict";

/**
 * claude-bridge — exposes the host's Claude Code CLI over a small HTTP API so the
 * Freeway container can use it for the premium lane.
 *
 * It lives on the host rather than in the container so the credentials in ~/.claude
 * never enter an image. It is deliberately dependency-free: node's stdlib only, so
 * deploying it is copying one file.
 *
 * This is a best-effort backend. Every failure path returns a plain reason string
 * rather than throwing, because the caller is expected to shrug and use a paid model
 * instead.
 */

const http = require("node:http");
const { spawn } = require("node:child_process");

const PORT = Number(process.env.BRIDGE_PORT || 8787);
const HOST = process.env.BRIDGE_HOST || "0.0.0.0";
const TOKEN = process.env.BRIDGE_TOKEN || "";
const CLAUDE_BIN = process.env.CLAUDE_BIN || "claude";
const TIMEOUT_MS = Number(process.env.BRIDGE_TIMEOUT_MS || 120000);
const HEALTH_TTL_MS = Number(process.env.BRIDGE_HEALTH_TTL_MS || 10 * 60 * 1000);
const MAX_CONCURRENT = Number(process.env.BRIDGE_MAX_CONCURRENT || 1);
const WORKDIR = process.env.BRIDGE_WORKDIR || "/tmp";

// A token is required whenever the bridge is reachable from outside its own
// network. On a private compose network with no published port there is nothing to
// defend against, so it is optional there — but say so out loud, because an open
// inference endpoint on a routable address would be a bad surprise.
if (!TOKEN) {
  console.warn(
    "BRIDGE_TOKEN is not set: running without authentication. " +
    "Only do this when the service publishes no ports and sits on an internal network."
  );
}

// Claude Code is an interactive developer tool, not a server. Running several at
// once is a good way to get rate limited and to thrash the host, so requests queue.
let inFlight = 0;
const waiting = [];

function acquire() {
  if (inFlight < MAX_CONCURRENT) {
    inFlight++;
    return Promise.resolve();
  }
  return new Promise((resolve) => waiting.push(resolve));
}

function release() {
  const next = waiting.shift();
  if (next) next();
  else inFlight--;
}

/**
 * Strips the agent scaffolding down as far as the CLI allows. The default
 * invocation carries ~27k tokens of system prompt and tool definitions before the
 * caller's own prompt; this gets it to roughly a third of that.
 */
function buildArgs(systemPrompt) {
  return [
    "-p",
    "--output-format", "json",
    "--max-turns", "1",
    "--system-prompt", systemPrompt,
    "--exclude-dynamic-system-prompt-sections",
    "--strict-mcp-config",
    "--setting-sources", "",
    "--permission-mode", "default",
    "--disallowed-tools",
    "Bash", "Read", "Write", "Edit", "Glob", "Grep",
    "WebFetch", "WebSearch", "Task", "NotebookEdit", "TodoWrite",
  ];
}

function runClaude(prompt, systemPrompt) {
  return new Promise((resolve) => {
    const started = Date.now();
    let child;
    try {
      child = spawn(CLAUDE_BIN, buildArgs(systemPrompt), {
        cwd: WORKDIR,
        env: process.env,
        stdio: ["pipe", "pipe", "pipe"],
      });
    } catch (err) {
      return resolve({ ok: false, reason: `spawn failed: ${err.message}` });
    }

    let stdout = "";
    let stderr = "";
    let settled = false;

    const done = (value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve(value);
    };

    const timer = setTimeout(() => {
      child.kill("SIGKILL");
      done({ ok: false, reason: `timed out after ${TIMEOUT_MS}ms` });
    }, TIMEOUT_MS);

    child.stdout.on("data", (d) => (stdout += d));
    child.stderr.on("data", (d) => (stderr += d));
    child.on("error", (err) => done({ ok: false, reason: `spawn failed: ${err.message}` }));

    child.on("close", (code) => {
      const durationMs = Date.now() - started;

      if (code !== 0) {
        const detail = (stderr || stdout || "").trim().slice(0, 400);
        return done({ ok: false, reason: `exit ${code}: ${detail || "no output"}`, durationMs });
      }

      let parsed;
      try {
        parsed = JSON.parse(stdout);
      } catch {
        return done({
          ok: false,
          reason: `unparseable output: ${stdout.trim().slice(0, 200)}`,
          durationMs,
        });
      }

      if (parsed.is_error || parsed.subtype !== "success") {
        return done({
          ok: false,
          reason: parsed.api_error_status
            ? `api error ${parsed.api_error_status}`
            : `claude reported ${parsed.subtype || "an error"}`,
          durationMs,
        });
      }

      const usage = parsed.usage || {};
      const model = Object.keys(parsed.modelUsage || {})[0] || null;

      done({
        ok: true,
        text: parsed.result ?? "",
        model,
        // Reported by the CLI at list price. On a subscription nothing is billed,
        // so this is the amount avoided rather than the amount spent.
        listCostUsd: parsed.total_cost_usd ?? 0,
        promptTokens:
          (usage.input_tokens || 0) +
          (usage.cache_creation_input_tokens || 0) +
          (usage.cache_read_input_tokens || 0),
        completionTokens: usage.output_tokens || 0,
        stopReason: parsed.stop_reason || null,
        durationMs,
      });
    });

    // The prompt goes over stdin: it can be long, and argv quoting is a trap.
    child.stdin.end(prompt);
  });
}

// A probe is not free, so its verdict is cached and shared by every health check.
let cachedHealth = null;

async function probe() {
  if (cachedHealth && Date.now() - cachedHealth.checkedAt < HEALTH_TTL_MS) {
    return { ...cachedHealth, cached: true };
  }

  await acquire();
  let result;
  try {
    result = await runClaude("Reply with exactly: ok", "You are a health probe. Answer in one word.");
  } finally {
    release();
  }

  const state = result.ok
    ? "available"
    : /rate.?limit|usage limit|quota|too many requests|429/i.test(result.reason || "")
      ? "rate_limited"
      : "unavailable";

  cachedHealth = {
    state,
    ok: state === "available",
    detail: result.ok ? null : result.reason,
    latencyMs: result.durationMs ?? null,
    model: result.model ?? null,
    checkedAt: Date.now(),
  };

  return { ...cachedHealth, cached: false };
}

function send(res, status, body) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "content-type": "application/json",
    "content-length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

function authorised(req) {
  if (!TOKEN) return true;
  const header = req.headers.authorization || "";
  return header === `Bearer ${TOKEN}`;
}

function readBody(req, limitBytes = 1_000_000) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on("data", (c) => {
      size += c.length;
      if (size > limitBytes) {
        reject(new Error("body too large"));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

/**
 * Renders a chat transcript into the single prompt the CLI takes. System messages
 * are lifted out into --system-prompt; the rest become a labelled transcript.
 */
function render(messages) {
  const system = messages
    .filter((m) => m.role === "system")
    .map((m) => m.content)
    .join("\n\n");

  const turns = messages.filter((m) => m.role !== "system");

  const transcript =
    turns.length === 1 && turns[0].role === "user"
      ? turns[0].content
      : turns
          .map((m) => `${m.role === "assistant" ? "Assistant" : "User"}: ${m.content}`)
          .join("\n\n") + "\n\nAssistant:";

  return {
    system:
      system ||
      "You are a helpful assistant. Answer the user directly and concisely. Do not use tools.",
    prompt: transcript,
  };
}

const server = http.createServer(async (req, res) => {
  if (!authorised(req)) return send(res, 401, { error: "unauthorised" });

  if (req.method === "GET" && req.url.startsWith("/health")) {
    try {
      return send(res, 200, await probe());
    } catch (err) {
      return send(res, 200, { ok: false, state: "unavailable", detail: err.message });
    }
  }

  if (req.method === "POST" && req.url.startsWith("/complete")) {
    let body;
    try {
      body = JSON.parse(await readBody(req));
    } catch (err) {
      return send(res, 400, { ok: false, reason: `bad request: ${err.message}` });
    }

    const messages = Array.isArray(body.messages) ? body.messages : [];
    if (messages.length === 0) {
      return send(res, 400, { ok: false, reason: "messages is required" });
    }

    const { system, prompt } = render(messages);

    await acquire();
    try {
      const result = await runClaude(prompt, system);
      // A refusal to serve invalidates the cached "available" verdict straight away,
      // so the next request does not keep trying a route that just failed.
      if (!result.ok) cachedHealth = null;
      return send(res, 200, result);
    } finally {
      release();
    }
  }

  send(res, 404, { error: "not found" });
});

server.listen(PORT, HOST, () => {
  console.log(`claude-bridge listening on ${HOST}:${PORT} (max ${MAX_CONCURRENT} concurrent)`);
});
