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

### Why it is not faster or cheaper than this

All of the obvious levers were tried and measured, and every one of them was worse.
Recorded here so nobody spends the money finding out twice.

**The flags are load-bearing.** Straying from Claude Code's usual shape loses the
large prefix the upstream cache already holds, and the bill goes up:

| invocation | cost per request |
|---|---|
| as invoked here | **$0.0247** |
| default system prompt + `--exclude-dynamic-system-prompt-sections` | $0.0309 |
| `--restricted` | $0.0589 |
| `--disable-slash-commands` | $0.1021 |

`--exclude-dynamic-system-prompt-sections` was removed because the CLI ignores it
whenever `--system-prompt` is passed, which here is always.

**Keeping the prompt cache warm does not work.** The CLI puts its cache breakpoint
after the last message, so a request only reads the prefix from cache when the whole
prompt is byte-identical to a recent one. Repeating one prompt costs $0.002 instead
of $0.025 — but real traffic never repeats a prompt, so every real request pays the
cold price. Nothing the bridge can do from outside the CLI changes that.

**A persistent session is the wrong shape.** `--resume` and `--continue` replay the
earlier conversation, so one caller's messages would land in the next caller's
context. This is a shared gateway; each request gets its own invocation.

**Pre-spawning does not help.** A process started 2.5s ahead of the prompt saved
roughly 300ms, which is inside the run-to-run noise (startup measured 1.2–2.4s
either way). Not worth a standing pool of processes.

Of the ~3.5s a request takes, about 2s is the API call itself and the rest is CLI
startup. Both are the CLI's, not the bridge's.

## Who may use it, and how much

Two lanes reach the bridge, deliberately on different terms.

| | `paid:premium` | `paid:moderate` |
|---|---|---|
| queues when busy | yes | **never** |
| deadline | 150s | 5s |
| share of the hourly budget | all of it | 60% (`BRIDGE_SPILLOVER_SHARE`) |

The asymmetry is the whole design. Premium displaces frontier-model pricing, so a
request served here avoids around $0.025; moderate displaces models costing a
fraction of a cent. Both consume the same subscription quota, so premium is worth
roughly 25x more per unit of the scarce resource and is never made to wait behind
the cheaper lane. Moderate rides along on capacity that would otherwise sit idle,
and the moment that capacity is in question it is refused and the request goes to
the model it would have used anyway.

Refusals are instantaneous and happen before the queue, so spillover can never turn
into latency. They come back with `reasonCode` of `busy` or `budget`, which the
caller treats as the bridge working rather than failing -- a refused spillover must
not mark the bridge unhealthy, or cheap traffic would lock premium out of the
subscription entirely.

### The budget is set, not discovered

`BRIDGE_HOURLY_TOKEN_BUDGET` caps spend over a rolling hour. It has to be
configured, because the ceiling cannot be read: the CLI reports what a call used but
carries no rate, limit, remaining or reset field anywhere in its output. There is
nothing to query.

At roughly 9.4k tokens a call, the 200,000 default is about 21 calls an hour, of
which moderate may have 12. Probes count against it too -- they are real calls on
the same subscription. `GET /admin/local-claude` reports where the hour stands, and
every invocation logs its own consumption, so size it from observed numbers rather
than from this paragraph.

This is the proactive half. The reactive half is unchanged: a genuine rate-limit
reply still marks the bridge unavailable and takes both lanes off it.

## Install

### As a compose service (recommended)

It is already declared in `compose.yml`. Point it at your credentials and turn the
feature on in `.env`:

```bash
LOCAL_CLAUDE_ENABLED=true

CLAUDE_HOME=/home/ubuntu      # whose ~/.claude and ~/.claude.json to mount
CLAUDE_UID=1000               # id -u
CLAUDE_GID=1000               # id -g
```

**The uid must own those files.** This is the single most common way to get a bridge
that starts cleanly and then fails every request: the container reads the credentials
as `CLAUDE_UID`, and if that uid cannot read them, Claude Code dies locally before it
ever calls the API.

```bash
stat -c '%u %g' $CLAUDE_HOME/.claude $CLAUDE_HOME/.claude.json
```

If Claude Code was set up under `root`, that is `CLAUDE_HOME=/root` with
`CLAUDE_UID=0` and `CLAUDE_GID=0` — `/root` is mode 700, so uid 1000 cannot even
traverse into it.

The bridge checks this at boot and says so plainly:

```
credentials: /home/claude/.claude/.credentials.json NOT readable by uid 1000
  (owned by 0:0, mode 600). Set CLAUDE_UID/CLAUDE_GID to the owner of those files.
```

Then:

```bash
docker compose up -d --build
docker compose logs -f claude-bridge
```

No token, no URL and no systemd unit: the service publishes no ports and sits on a
private network that only the API can reach, so there is nothing for a shared secret
to protect against.

**Both mounts are read-write on purpose.** Claude Code refreshes its OAuth token in
`.credentials.json` and writes session state, so a read-only mount works right up
until the token expires and then stops. `~/.claude.json` lives beside the directory
rather than inside it, which is why there are two mounts and not one.

**The host and the container share that state.** History, locks and daemon files are
all in there. If you use Claude Code interactively on the same box, they are writing
to the same files.

### As a host service instead

If you would rather the credentials never entered a container, run it on the host
with the included systemd unit and set a `BRIDGE_TOKEN`:

```bash
sudo mkdir -p /opt/claude-bridge
sudo cp server.js /opt/claude-bridge/
sudo cp claude-bridge.service /etc/systemd/system/
sudoedit /etc/systemd/system/claude-bridge.service   # User=, BRIDGE_TOKEN=
sudo systemctl daemon-reload && sudo systemctl enable --now claude-bridge
```

Then in Freeway's `.env`, point at the docker bridge address and pass the token:

```bash
LOCAL_CLAUDE_URL=http://172.17.0.1:8787   # check: ip addr show docker0
LOCAL_CLAUDE_TOKEN=the-same-BRIDGE_TOKEN
```

## Verify

```bash
# From the API container, on the compose network:
docker compose exec freeway curl -s http://claude-bridge:8787/health

# Or against a host install, with its token:
curl -s -H "Authorization: Bearer $BRIDGE_TOKEN" http://172.17.0.1:8787/health
```

## Endpoints

| method | path | returns |
|---|---|---|
| `GET` | `/health` | `{ ok, state, detail, latencyMs, model, checkedAt, cached }` |
| `POST` | `/complete` | `{ ok, text, model, listCostUsd, promptTokens, completionTokens, durationMs }` |

`/complete` takes an optional `priority` of `premium` (the default, so a caller that
sends nothing keeps the old behaviour) or `spillover`. `/health` additionally reports
`busy` and a `budget` of `{ limit, used, remaining, spilloverLimit, estimate }`.

`state` is one of `available`, `rate_limited`, `unavailable`. `listCostUsd` is what the
same work would have cost at API list price — on a subscription it is the amount
avoided, not the amount spent.

`BRIDGE_TOKEN` is optional. When it is set both endpoints require
`Authorization: Bearer $BRIDGE_TOKEN`; when it is unset the bridge runs open and says
so at startup, which is only acceptable behind a private network with no published
ports. If you expose it on a routable address, set a token.
