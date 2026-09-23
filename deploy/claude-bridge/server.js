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

/**
 * How much of the Claude subscription this bridge is allowed to spend per hour.
 *
 * It has to be configured, because it cannot be discovered: the CLI reports what a
 * call used but nothing about the plan's ceiling or what is left of it -- there is no
 * rate, limit, remaining or reset field anywhere in its output. So this is a budget
 * you set, not a limit the bridge reads.
 *
 * The default is deliberately conservative. Measure real consumption in the log lines
 * this prints and raise it if there is headroom; the reactive guard is separate and
 * still there, since a genuine rate-limit reply marks the bridge unavailable.
 */
const HOURLY_TOKEN_BUDGET = Number(process.env.BRIDGE_HOURLY_TOKEN_BUDGET || 200000);

/**
 * The share of that budget spillover traffic may use, leaving the rest for premium.
 * Premium is where the subscription earns its keep -- it displaces frontier-model
 * pricing, where spillover only displaces cheap models -- so it keeps a reserve that
 * spillover cannot touch however busy the cheaper lane gets.
 */
const SPILLOVER_SHARE = Number(process.env.BRIDGE_SPILLOVER_SHARE || 0.6);

/** Assumed tokens per call before one has been measured. */
const DEFAULT_CALL_TOKENS = 9500;

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

/**
 * A rolling hour of token spend, so the budget is a moving window rather than a
 * counter that resets on the hour and lets an hour's worth of traffic through twice
 * either side of the boundary.
 */
const ledger = [];

function budget() {
  const cutoff = Date.now() - 60 * 60 * 1000;
  while (ledger.length && ledger[0].at < cutoff) ledger.shift();

  const used = ledger.reduce((sum, e) => sum + e.tokens, 0);
  // What a call actually costs, learned from recent ones: the estimate only has to
  // be good enough to stop the budget being overshot by a whole request.
  const recent = ledger.slice(-10);
  const estimate = recent.length
    ? Math.round(recent.reduce((sum, e) => sum + e.tokens, 0) / recent.length)
    : DEFAULT_CALL_TOKENS;

  return {
    limit: HOURLY_TOKEN_BUDGET,
    used,
    remaining: Math.max(0, HOURLY_TOKEN_BUDGET - used),
    spilloverLimit: Math.round(HOURLY_TOKEN_BUDGET * SPILLOVER_SHARE),
    estimate,
  };
}

function record(tokens) {
  ledger.push({ at: Date.now(), tokens });
}

/**
 * Whether a call of this priority may run right now.
 *
 * Spillover is the cheap lane riding along on spare capacity, so it is refused the
 * moment it would have to wait or would eat into premium's reserve. Refusing is
 * instantaneous and the caller falls straight through to a paid model, which is the
 * whole point: spillover must never turn into latency.
 */
function admit(priority) {
  const b = budget();

  if (priority === "spillover") {
    if (inFlight > 0 || waiting.length > 0) {
      return { ok: false, reasonCode: "busy", reason: "bridge busy; spillover does not queue" };
    }
    if (b.used + b.estimate > b.spilloverLimit) {
      return {
        ok: false,
        reasonCode: "budget",
        reason: `spillover budget spent (${b.used}/${b.spilloverLimit} tokens this hour)`,
      };
    }
    return { ok: true };
  }

  if (b.used + b.estimate > b.limit) {
    return {
      ok: false,
      reasonCode: "budget",
      reason: `hourly token budget spent (${b.used}/${b.limit})`,
    };
  }
  return { ok: true };
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

// Used when the caller sends no system message of its own, and by the health probe
// so that the probe's timing reflects a real request.
const SYSTEM_PROMPT =
  process.env.BRIDGE_SYSTEM_PROMPT ||
  "You are a helpful assistant. Answer the user directly and concisely. Do not use tools.";

/**
 * Strips the agent scaffolding down as far as the CLI allows. The default
 * invocation carries ~27k tokens of system prompt and tool definitions before the
 * caller's own prompt; this gets it to roughly a third of that.
 *
 * These exact flags were measured against the alternatives; every variant tried cost
 * more per request, because deviating from Claude Code's usual shape loses the large
 * shared prefix the upstream cache already holds:
 *
 *   this set                                       $0.0247
 *   default system prompt + --exclude-dynamic...   $0.0309
 *   --restricted                                   $0.0589
 *   --disable-slash-commands                       $0.1021
 *
 * So treat it as load-bearing rather than arbitrary, and re-measure before changing it.
 *
 * `--exclude-dynamic-system-prompt-sections` is deliberately absent: the CLI ignores
 * it whenever --system-prompt is given, which is always, here.
 */
function buildArgs(systemPrompt) {
  return [
    "-p",
    "--output-format", "json",
    "--max-turns", "1",
    "--system-prompt", systemPrompt,
    "--strict-mcp-config",
    "--setting-sources", "",
    "--permission-mode", "default",
    "--disallowed-tools",
    "Bash", "Read", "Write", "Edit", "Glob", "Grep",
    "WebFetch", "WebSearch", "Task", "NotebookEdit", "TodoWrite",
  ];
}

/**
 * Turns a failed invocation into one useful line.
 *
 * Claude Code prints a full result envelope even when it fails, so blindly slicing
 * the first few hundred characters yields token counters and nothing about the
 * cause. This digs out the fields that actually say what went wrong, and names the
 * two failures that are almost always the real answer.
 */
function explain(code, stdout, stderr) {
  const raw = `${stdout}
${stderr}`;

  if (/permission denied|EACCES/i.test(raw)) {
    return (
      `exit ${code}: cannot read the mounted credentials (permission denied). ` +
      `The container runs as uid ${process.getuid?.() ?? "?"}; that uid must own ` +
      `~/.claude and ~/.claude.json. Check CLAUDE_UID/CLAUDE_GID against ` +
      `\`stat -c '%u %g' $CLAUDE_HOME/.claude\`.`
    );
  }

  if (/not logged in|please run .?claude login|authentication_error|invalid api key|oauth/i.test(raw)) {
    return `exit ${code}: Claude Code is not authenticated in the container. The mounted ~/.claude may be the wrong user's.`;
  }

  let parsed = null;
  try {
    parsed = JSON.parse(stdout);
  } catch {
    /* not JSON; fall through to the raw text */
  }

  if (parsed) {
    const bits = [
      parsed.subtype && parsed.subtype !== "success" ? parsed.subtype : null,
      parsed.api_error_status ? `api ${parsed.api_error_status}` : null,
      typeof parsed.result === "string" && parsed.result.trim() ? parsed.result.trim() : null,
      parsed.error ? JSON.stringify(parsed.error) : null,
      parsed.terminal_reason && parsed.terminal_reason !== "completed" ? parsed.terminal_reason : null,
    ].filter(Boolean);

    if (bits.length) return `exit ${code}: ${bits.join(" | ").slice(0, 300)}`;
    return `exit ${code}: claude exited with a result envelope but no error detail (stop_reason ${parsed.stop_reason ?? "?"}, ${parsed.usage?.input_tokens ?? 0} input tokens)`;
  }

  const text = (stderr || stdout || "").trim().replace(/\s+/g, " ");
  return `exit ${code}: ${text.slice(0, 300) || "no output"}`;
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
        return done({ ok: false, reason: explain(code, stdout, stderr), durationMs });
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

      // Worth a line of its own. A request whose prompt is byte-identical to a recent
      // one reads the whole prefix from cache and costs about $0.002 instead of
      // $0.025; distinct prompts never do, since the CLI puts its cache breakpoint
      // after the last message. So `created` on every line is expected rather than a
      // fault -- but a jump in it means the prefix changed, and that is worth chasing.
      const created = usage.cache_creation_input_tokens || 0;
      const read = usage.cache_read_input_tokens || 0;

      // Charged against the hourly budget. Probes count too: they are real calls on
      // the same subscription, and a budget that ignored them would drift.
      const spent =
        (usage.input_tokens || 0) + created + read + (usage.output_tokens || 0);
      record(spent);
      console.log(
        `claude ok in ${durationMs}ms, cache created=${created} read=${read}, $${(parsed.total_cost_usd ?? 0).toFixed(6)} list`
      );

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
    // The same system prompt a default completion uses, so the latency this reports
    // is the latency a real request would see.
    result = await runClaude("Reply with exactly: ok", SYSTEM_PROMPT);
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

  // The caller's system message goes through as the system prompt. Folding it into
  // the transcript to keep one constant prefix was tried and measured: it changes
  // nothing about what the upstream cache holds, so the straightforward thing wins.
  return {
    system: system || SYSTEM_PROMPT,
    prompt: transcript,
  };
}

const server = http.createServer(async (req, res) => {
  if (!authorised(req)) return send(res, 401, { error: "unauthorised" });

  if (req.method === "GET" && req.url.startsWith("/health")) {
    try {
      const health = await probe();
      // busy and budget are read after the probe, so they describe the bridge as it
      // is now rather than as it was when the cached verdict was taken.
      return send(res, 200, {
        ...health,
        busy: inFlight > 0 || waiting.length > 0,
        budget: budget(),
      });
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

    // "premium" is the default so an older caller that sends no priority keeps its
    // current behaviour exactly.
    const priority = body.priority === "spillover" ? "spillover" : "premium";

    // Checked before the queue, never inside it: a refused spillover call has to come
    // back immediately so the caller can use a paid model, rather than waiting.
    const verdict = admit(priority);
    if (!verdict.ok) {
      console.log(`${priority} refused: ${verdict.reason}`);
      return send(res, 200, { ok: false, reason: verdict.reason, reasonCode: verdict.reasonCode });
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

/**
 * Says at boot whether the mounted credentials are actually readable by this
 * process. Without it the first symptom is a failed probe several minutes later
 * with an opaque exit code.
 */
function reportCredentials() {
  const fs = require("node:fs");
  const home = process.env.HOME || "/home/claude";
  const uid = process.getuid?.() ?? "?";
  const checks = [`${home}/.claude/.credentials.json`, `${home}/.claude.json`];

  for (const path of checks) {
    try {
      fs.accessSync(path, fs.constants.R_OK);

      // A directory passes a readability test. Docker silently creates one when the
      // host path in a bind mount does not exist, which is a common way to end up
      // with a "present" credentials file that Claude Code cannot use.
      if (fs.statSync(path).isDirectory()) {
        console.error(
          `credentials: ${path} is a DIRECTORY, not a file. The host path it is ` +
          `mounted from does not exist, so docker created an empty directory. ` +
          `Check CLAUDE_HOME points at the home directory that actually holds these.`
        );
        continue;
      }

      console.log(`credentials: ${path} readable`);
    } catch (err) {
      const stat = (() => {
        try {
          const s = fs.statSync(path);
          return s.isDirectory()
            ? "it is a DIRECTORY — the host path probably does not exist, so docker created one"
            : `owned by ${s.uid}:${s.gid}, mode ${(s.mode & 0o777).toString(8)}`;
        } catch {
          return "missing";
        }
      })();
      console.error(
        `credentials: ${path} NOT readable by uid ${uid} (${stat}). ` +
        `Set CLAUDE_UID/CLAUDE_GID to the owner of those files.`
      );
    }
  }
}

server.listen(PORT, HOST, () => {
  console.log(`claude-bridge listening on ${HOST}:${PORT} (max ${MAX_CONCURRENT} concurrent, uid ${process.getuid?.() ?? "?"})`);
  reportCredentials();
});
