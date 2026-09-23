# claude-bridge

Exposes the host's Claude Code CLI over a tiny HTTP API so the Freeway container can
use it to serve the `paid:premium` lane at no charge.

It runs on the **host**, not in a container, so the credentials in `~/.claude` never
enter an image. It has no dependencies beyond Node, which Claude Code already needs.

## Why it is best-effort

Every failure returns a reason string rather than an error. Freeway treats this route
as optional: if the bridge is down, rate limited, slow, or returns anything odd, the
request falls through to the normal paid model and the reason is written to the log.
Nothing about the gateway depends on this working.

## What it costs

Claude Code carries its agent scaffolding into every invocation. Measured on a
trivial prompt:

| invocation | input tokens | latency |
|---|---|---|
| default | 26,935 | 3.1s |
| as invoked here | 9,428 | 1.9s |

So roughly **9.4k tokens of overhead per request** before the caller's own prompt, and
about two seconds of latency. That is fine at low volume and will exhaust a
subscription quickly under real traffic.

## Install

```bash
sudo mkdir -p /opt/claude-bridge
sudo cp server.js /opt/claude-bridge/
sudo cp claude-bridge.service /etc/systemd/system/

# Set User=, BRIDGE_TOKEN= and (if needed) CLAUDE_BIN= first.
sudoedit /etc/systemd/system/claude-bridge.service

sudo systemctl daemon-reload
sudo systemctl enable --now claude-bridge
sudo systemctl status claude-bridge
```

Check the docker bridge address matches `BRIDGE_HOST`:

```bash
ip addr show docker0 | grep 'inet '
```

## Verify

```bash
TOKEN=your-bridge-token

curl -s -H "Authorization: Bearer $TOKEN" http://172.17.0.1:8787/health

curl -s -H "Authorization: Bearer $TOKEN" -H 'content-type: application/json' \
  -d '{"messages":[{"role":"user","content":"Reply with exactly: ok"}]}' \
  http://172.17.0.1:8787/complete
```

Then point Freeway at it in `.env`:

```bash
LOCAL_CLAUDE_ENABLED=true
LOCAL_CLAUDE_URL=http://172.17.0.1:8787
LOCAL_CLAUDE_TOKEN=your-bridge-token
```

## Endpoints

| method | path | returns |
|---|---|---|
| `GET` | `/health` | `{ ok, state, detail, latencyMs, model, checkedAt, cached }` |
| `POST` | `/complete` | `{ ok, text, model, listCostUsd, promptTokens, completionTokens, durationMs }` |

`state` is one of `available`, `rate_limited`, `unavailable`. `listCostUsd` is what the
same work would have cost at API list price — on a subscription it is the amount
avoided, not the amount spent.

Both endpoints require `Authorization: Bearer $BRIDGE_TOKEN`. Bind to the docker
bridge address, never `0.0.0.0`, unless you have a firewall in front of it.
