# Freeway

A multi-provider AI Gateway with project management, usage tracking, analytics, and a web control panel.

## Overview

Freeway is a full-featured AI Gateway built with .NET 10 that:
- Proxies chat completion requests to multiple AI providers (OpenRouter, OpenAI, Gemini, Groq, Mistral, Cohere, HuggingFace)
- Manages projects with individual API keys
- Tracks usage, costs, and analytics per project
- Automatically selects best free, cheapest paid, and cheapest image generation models
- Provides a complete admin API for management
- Includes a modern Next.js web control panel with JWT authentication

## Features

- **OpenAI-Compatible Chat Endpoint**: `POST /chat/completions` with model selection (`free`, `paid`, `paid:low`, `paid:moderate`, `paid:premium`, `image`, or specific model ID)
- **Multi-Provider Support**: Fallback across 8 AI providers for reliability
- **Project Management**: Create projects with individual API keys, rate limits, and metadata
- **Usage Tracking**: Logs all requests with tokens, costs, and response times
- **Admin Analytics**: Usage summaries, per-project stats, and detailed logs
- **Model Selection**: Auto-selects best free model (by context), cheapest paid model, and cheapest image generation model (by price)
- **Paid Tiers**: `paid:low`, `paid:moderate` and `paid:premium` route to curated models per cost/capability tier, each with its own fallback chain
- **Image Generation**: Supports image generation models via `model: "image"` with auto-selection of cheapest option
- **Daily Refresh**: Models updated via Hangfire background jobs
- **Weekly Email Report**: Usage, cost and per-project breakdown emailed to the admin
- **Spend Alerts**: Email when a project, model or the gateway overspends, or OpenRouter credit runs low
- **Local Claude Code** (optional): Serve `paid:premium` from a host Claude Code subscription at no charge, falling back to a paid model whenever it is unavailable
- **PostgreSQL Storage**: Persistent storage for projects, users, and usage data
- **Web Control Panel**: Next.js 16 dashboard with JWT authentication
- **Docker Ready**: Includes Dockerfile and compose.yml with Traefik support

## Architecture

Built with Clean Architecture pattern:

```
freeway/
├── src/
│   ├── Freeway.Domain/           # Entities, Interfaces
│   ├── Freeway.Application/      # CQRS handlers, DTOs, Validators
│   ├── Freeway.Infrastructure/   # EF Core, Provider clients, Caching
│   ├── Freeway.Api/              # Controllers, Middleware
│   └── Freeway.Web/              # Next.js 16 Control Panel
├── Dockerfile                    # API container
├── compose.yml
└── .env.example
```

## Authentication

Freeway supports multiple authentication methods:

| Method | Header/Cookie | Used For |
|--------|---------------|----------|
| Admin API Key | `X-Api-Key` | Admin endpoints, model info, CLI tools |
| Project API Key | `X-Api-Key` | Chat completions endpoint |
| JWT Bearer Token | `Authorization: Bearer <token>` | Web control panel |

### JWT Authentication (Web Panel)

The web control panel uses JWT tokens for authentication:

```bash
# Login
POST /auth/login
{
  "username": "admin",
  "password": "your-password"
}

# Response
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "user": { "id": "...", "username": "admin" },
  "expiresAt": "2025-01-02T00:00:00Z"
}
```

## API Endpoints

### Chat Completions (Project Key)

```bash
POST /chat/completions
```

OpenAI-compatible chat completion endpoint.

**Request:**
```json
{
  "model": "free",
  "messages": [
    {"role": "user", "content": "Hello!"}
  ],
  "temperature": 0.7,
  "max_tokens": 1000
}
```

**Model options:**
- `"free"` - Route across free providers. **Never incurs a charge**: if every free
  provider fails, the request returns `503` rather than falling back to a paid model
- `"paid"` - Alias for `"paid:low"` (unchanged behaviour)
- `"paid:low"` - Cheapest paid models
- `"paid:moderate"` - Balanced cost/capability models
- `"paid:premium"` - Highest-capability models
- `"image"` - Cheapest image generation model, ranked by what it costs to generate
- `"<model_id>"` - Use specific model by ID

### Paid Tiers

Each tier is defined by two things, in priority order:

1. **A curated preference list** - an ordered set of model IDs. The first one the upstream
   catalog still offers wins. This is what makes `premium` mean *a capable model* rather than
   merely *an expensive one*.
2. **A price band** - used to classify every other model, and to supply the fallback chain
   when none of the curated models are available.

| Tier | Price band (combined USD/Mtok) | Default head | Curated? |
|------|-------------------------------|--------------|----------|
| `paid:low` | `< 1.00` | cheapest available | no - pure cheapest-first |
| `paid:moderate` | `1.00 - 10.00` | `openai/gpt-5-mini` | yes |
| `paid:premium` | `> 10.00` | `openai/gpt-5.6-sol` | yes |

A model named in a curated list belongs to that tier regardless of its price, so a tier head
never drifts because upstream changed a number. Every tier keeps the same fallback behaviour
as before: the selected model first, then `PAID_FALLBACK_COUNT` backups from the rest of the
tier, with rate-limited models pushed to the back of the queue.

Tiers are configurable via `PAID_TIER_LOW_MAX`, `PAID_TIER_MODERATE_MAX`,
`PAID_TIER_MODERATE_MODELS` and `PAID_TIER_PREMIUM_MODELS` (see `.env.example`).

> **Note:** `:batch` model variants are excluded from every tier. They are priced for
> asynchronous batch submission and are not valid targets for a synchronous chat endpoint.

**Response:**
```json
{
  "id": "chatcmpl-abc123",
  "created": 1234567890,
  "model": "google/gemini-2.0-flash-exp:free",
  "choices": [
    {
      "index": 0,
      "message": {"role": "assistant", "content": "Hello! How can I help?"},
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 10,
    "completion_tokens": 20,
    "total_tokens": 30
  }
}
```

### Model Endpoints (Admin or Project Key)

```bash
GET /model/free              # Best free model
GET /model/paid              # Selected paid model (low tier)
GET /model/paid/{tier}       # Selected model for low | moderate | premium
GET /model/image             # Cheapest image generation model
GET /models/free             # All free models (ranked by context)
GET /models/paid             # All paid models (ranked by price)
GET /models/paid/{tier}      # Models in one tier, in fallback-chain order
GET /models/image            # All image generation models (ranked by price)
GET /health                  # Service health check
```

### Authentication Endpoints (Public/JWT)

```bash
POST /auth/login              # Login with username/password
POST /auth/register           # Register new user (first user becomes admin)
GET  /auth/me                 # Get current user info (requires JWT)
POST /auth/change-password    # Change password (requires JWT)
POST /auth/logout             # Logout (optional, client-side)
```

### Admin Endpoints (Admin Key or JWT)

#### Project Management

```bash
GET    /admin/projects              # List all projects
POST   /admin/projects              # Create project (returns API key ONCE)
GET    /admin/projects/{id}         # Get project
PATCH  /admin/projects/{id}         # Update project
DELETE /admin/projects/{id}         # Delete project
POST   /admin/projects/{id}/rotate-key  # Rotate API key
```

**Create Project Request:**
```json
{
  "name": "My Project",
  "rate_limit_per_minute": 60,
  "metadata": {"team": "backend"}
}
```

**Create Project Response:**
```json
{
  "id": "uuid",
  "name": "My Project",
  "api_key": "fw_abc123...",
  "api_key_prefix": "fw_abc12",
  "rate_limit_per_minute": 60,
  "is_active": true,
  "created_at": "2025-01-01T00:00:00Z"
}
```

> **Important:** The `api_key` is only shown once on create or rotate. Store it securely!

#### Model Selection

```bash
PUT /admin/model/free          # Set selected free model
PUT /admin/model/paid          # Set selected paid model (targets the model's own tier)
PUT /admin/model/paid/{tier}   # Set selected model for low | moderate | premium
PUT /admin/model/image         # Set selected image generation model
```

The model must belong to the tier being set; a mismatch returns `400`.

**Request:**
```json
{
  "model_id": "google/gemma-3-27b-it:free"
}
```

#### Notifications

```bash
GET  /admin/notifications/weekly-report/preview  # Build the report as JSON, send nothing
POST /admin/notifications/weekly-report/send     # Send the weekly report now
GET  /admin/notifications/alerts                 # Alerts currently firing, ignoring cooldown
POST /admin/notifications/alerts/check           # Run the checks and email anything not in cooldown
```

#### Analytics

```bash
GET /admin/analytics/summary                    # Global summary
GET /admin/analytics/usage?project_id={id}      # Project usage stats
GET /admin/analytics/logs?project_id={id}       # Detailed usage logs
```

**Global Summary Response:**
```json
{
  "total_projects": 5,
  "active_projects": 4,
  "total_requests_today": 1250,
  "total_requests_this_month": 45000,
  "total_cost_this_month_usd": 12.50
}
```

## Configuration

Environment variables (see `.env.example`):

| Variable | Required | Description |
|----------|----------|-------------|
| `PORT` | No | API port (default: 8080) |
| `ADMIN_API_KEY` | Yes | Admin API key for CLI/scripts |
| `OPENROUTER_API_KEY` | Yes | OpenRouter API key |
| `OPENAI_API_KEY` | No | OpenAI API key (for fallback) |
| `GEMINI_API_KEY` | No | Google Gemini API key (for fallback) |
| `GROQ_API_KEY` | No | Groq API key (for fallback) |
| `MISTRAL_API_KEY` | No | Mistral API key (for fallback) |
| `COHERE_API_KEY` | No | Cohere API key (for fallback) |
| `HUGGINGFACE_API_KEY` | No | HuggingFace API key (for fallback) |
| `PAID_FALLBACK_COUNT` | No | Backup models to try after the primary for `paid`/`image` requests (default: 3) |
| `PAID_TIER_LOW_MAX` | No | Upper bound of the `paid:low` price band, combined USD/Mtok (default: 1.0) |
| `PAID_TIER_MODERATE_MAX` | No | Upper bound of the `paid:moderate` price band, combined USD/Mtok (default: 10.0) |
| `PAID_TIER_MODERATE_MODELS` | No | Comma-separated curated model IDs for `paid:moderate`, best-first |
| `PAID_TIER_PREMIUM_MODELS` | No | Comma-separated curated model IDs for `paid:premium`, best-first |
| `MODEL_COOLDOWN_SECONDS` | No | How long a rate-limited (429) model is skipped before being retried (default: 60) |
| `OPENROUTER_PROVIDER_SORT` | No | OpenRouter endpoint sort: `throughput`, `price`, or `latency` (default: `throughput`). Empty disables sorting |
| `OPENROUTER_ALLOW_FALLBACKS` | No | Let OpenRouter route around a failed/throttled provider to another serving the same model (default: true) |
| `OPENROUTER_IGNORE_PROVIDERS` | No | Comma-separated provider slugs to exclude, e.g. `novita,deepinfra` (default: none) |
| `REQUEST_TIMEOUT_SECONDS` | No | Timeout for provider list/model fetches (default: 30) |
| `COMPLETION_TIMEOUT_SECONDS` | No | Timeout for chat completion calls (default: 120) |
| `DB_HOST` | No | Database host (default: localhost) |
| `DB_PORT` | No | Database port (default: 5432) |
| `DB_USER` | No | Database user (default: postgres) |
| `DB_PASSWORD` | No | Database password |
| `DB_NAME` | No | Database name (default: freeway) |
| `JWT_SECRET` | Yes | Secret key for JWT tokens (min 32 chars) |
| `JWT_EXPIRY_HOURS` | No | JWT token expiry (default: 24) |
| `HANGFIRE_USERNAME` | No | Hangfire dashboard username |
| `HANGFIRE_PASSWORD` | No | Hangfire dashboard password |
| `ALLOWED_ORIGINS` | No | CORS origins (default: *) |
| `SMTP_HOST` | No | SMTP server (default: `smtp.gmail.com`) |
| `SMTP_PORT` | No | SMTP port; 587 uses STARTTLS, 465 implicit TLS (default: 587) |
| `SMTP_USERNAME` | For email | SMTP user. For Gmail this is the full address |
| `SMTP_PASSWORD` | For email | Gmail **App Password**, not the account password |
| `SMTP_FROM` | No | From address (default: `SMTP_USERNAME`) |
| `SMTP_FROM_NAME` | No | From display name (default: `Freeway`) |
| `ADMIN_NOTIFICATION_EMAIL` | For email | Where reports and alerts are sent |
| `WEEKLY_REPORT_ENABLED` | No | Turn the weekly report on/off (default: true) |
| `WEEKLY_REPORT_CRON` | No | Cron in UTC (default: `0 3 * * 1`, Monday 03:00 UTC) |
| `SPEND_ALERTS_ENABLED` | No | Turn alerts on/off (default: true) |
| `SPEND_ALERT_CRON` | No | Cron in UTC (default: hourly) |
| `ALERT_COOLDOWN_HOURS` | No | How long one alert stays quiet after firing (default: 12) |
| `ALERT_GLOBAL_DAILY_USD` | No | Gateway-wide 24h spend threshold (default: 5) |
| `ALERT_PROJECT_DAILY_USD` | No | Per-project 24h spend threshold (default: 2) |
| `ALERT_PROJECT_DAILY_REQUESTS` | No | Per-project 24h request threshold (default: 5000) |
| `ALERT_MODEL_DAILY_USD` | No | Single-model 24h spend threshold (default: 3) |
| `ALERT_MONTHLY_USD` | No | Month-to-date budget (default: 25) |
| `ALERT_OPENROUTER_CREDIT_USD` | No | Warn below this remaining credit (default: 2) |
| `ALERT_OPENROUTER_KEY_REMAINING_USD` | No | Warn below this remaining key limit (default: 1) |
| `ALERT_OPENROUTER_KEY_EXPIRY_DAYS` | No | Warn this many days before key expiry (default: 14) |
| `CREDENTIAL_ALERTS_ENABLED` | No | Alert when any provider rejects its API key (default: true) |
| `FREE_LANE_OPENROUTER_COUNT` | No | Zero-cost OpenRouter models the free lane may try (default: 3, 0 disables) |
| `LOCAL_CLAUDE_ENABLED` | No | Serve `paid:premium` from a local Claude Code subscription (default: false) |
| `CLAUDE_HOME` | No | Home directory holding `.claude` and `.claude.json` to mount (default: `/home/ubuntu`) |
| `CLAUDE_UID` / `CLAUDE_GID` | No | uid/gid owning those files (default: 1000) |
| `LOCAL_CLAUDE_URL` | No | Bridge address (default: `http://claude-bridge:8787`) |
| `LOCAL_CLAUDE_TOKEN` | No | Only needed if the bridge is exposed outside the compose network |
| `LOCAL_CLAUDE_TIMEOUT_SECONDS` | No | Timeout for a bridge call (default: 150) |
| `LOCAL_CLAUDE_HEALTH_CRON` | No | How often to probe the bridge (default: hourly) |

## Quick Start

### Prerequisites

- .NET 10 SDK
- Node.js 22+ (for web panel)
- PostgreSQL database

### Local Development

#### 1. Backend API

```bash
# Clone and enter directory
cd freeway

# Configure environment
cp .env.example .env
# Edit .env and set required values:
#   - ADMIN_API_KEY
#   - OPENROUTER_API_KEY
#   - JWT_SECRET (minimum 32 characters)
#   - DB_* values

# Restore and build
dotnet restore
dotnet build

# Run migrations
dotnet ef database update -p src/Freeway.Infrastructure -s src/Freeway.Api

# Run the API
dotnet run --project src/Freeway.Api
```

The API will be available at `http://localhost:8080`.

#### 2. Web Control Panel

```bash
# Enter web directory
cd src/Freeway.Web

# Install dependencies
npm install

# Configure environment
cp .env.example .env.local
# Edit .env.local:
#   NEXT_PUBLIC_API_URL=http://localhost:8080

# Run development server
npm run dev
```

The web panel will be available at `http://localhost:3000`.

#### 3. First User Setup

Navigate to `http://localhost:3000` and register the first user. The first registered user will be created as an admin.

### Docker Deployment

```bash
# Build and start all services
docker compose up -d --build

# View logs
docker compose logs -f freeway
docker compose logs -f freeway-web

# Stop services
docker compose down
```

Services will be available at:
- API: `https://freeway.pranta.dev` (configure in compose.yml)
- Web Panel: `https://freewayapp.pranta.dev` (configure in compose.yml)

## Project Structure

```
freeway/
├── src/
│   ├── Freeway.Domain/
│   │   ├── Entities/              # Project, UsageLog, User, ChatMessage
│   │   ├── Common/                # BaseEntity
│   │   └── Interfaces/            # Service interfaces
│   ├── Freeway.Application/
│   │   ├── Common/                # Result pattern, ValidationBehavior
│   │   ├── DTOs/                  # Data transfer objects
│   │   └── Features/              # CQRS commands, queries, handlers
│   │       ├── Projects/
│   │       ├── Models/
│   │       ├── Analytics/
│   │       ├── Chat/
│   │       └── Health/
│   ├── Freeway.Infrastructure/
│   │   ├── Persistence/           # EF Core DbContext, Configurations
│   │   ├── Services/              # Provider clients, Auth, Caching
│   │   ├── Jobs/                  # Hangfire background jobs
│   │   └── DependencyInjection.cs
│   ├── Freeway.Api/
│   │   ├── Controllers/           # API controllers (including AuthController)
│   │   ├── Middleware/            # Auth, Exception handling
│   │   ├── Attributes/            # RequireAdmin, RequireProject
│   │   └── Program.cs             # Application startup
│   └── Freeway.Web/               # Next.js 16 Control Panel
│       ├── src/
│       │   ├── app/               # App Router pages
│       │   │   ├── (auth)/        # Login page
│       │   │   └── (dashboard)/   # Protected pages
│       │   ├── components/        # React components
│       │   │   ├── ui/            # Reusable UI components
│       │   │   └── layout/        # Sidebar, Header, Mobile Nav
│       │   └── lib/               # Utilities, API client, stores
│       ├── Dockerfile
│       └── package.json
├── Dockerfile                     # API container
├── compose.yml
├── Freeway.sln
└── .env.example
```

## Web Control Panel Features

The Next.js web panel provides:

### Dashboard
- Stats overview: Total projects, active projects, requests today, monthly cost
- Selected models display (free, all three paid tiers, and image)
- Quick navigation to all features

### Design system

Each model type owns a hue and keeps it everywhere it appears, so colour carries
information rather than decorating: free is green, then the paid tiers run low blue,
moderate amber, premium rose as a cost ladder, and image sits apart in violet. The
mapping lives once in `src/lib/theme/accents.ts`.

Chart marks are separate tokens from text tokens: marks want mid tones, text wants
contrast against its surface. The mark set is validated for lightness band, chroma
floor, colour-blind separation and surface contrast, and passes in both light and
dark, so the same values are used in either mode.

> Accent classes are referenced through `accents.ts` rather than written literally in
> markup, so `tailwind.config.ts` must scan all of `./src/**`. Narrowing that glob
> silently purges every lane colour.

### Models
- Browse all available models (free, paid, and image tabs)
- Paid tab has Low / Moderate / Premium sub-tabs, listed in fallback-chain order
- Curated models are badged so you can see which are tier-preferred
- Search models by name or ID
- View model details (context length, pricing, capabilities)
- Select active free/paid/image models, per tier for paid

### Projects
- Create, edit, and delete projects
- View and copy API keys
- Rotate API keys with confirmation
- Set rate limits and metadata
- View project status (active/inactive)

### Project Details
- Detailed usage statistics
- Usage by model breakdown (pie chart)
- Request logs with pagination
- Filter logs by date range

### Settings
- User profile information
- Theme toggle (Light/Dark/System)
- API connection status
- Logout

## Usage Examples

### Create a Project and Make a Request

```bash
# Using curl

# 1. Create a project (with Admin API key)
curl -X POST http://localhost:8080/admin/projects \
  -H "X-Api-Key: your-admin-key" \
  -H "Content-Type: application/json" \
  -d '{"name": "My App", "rate_limit_per_minute": 100}'

# Response includes api_key - save it!

# 2. Make a chat completion (with Project API key)
curl -X POST http://localhost:8080/chat/completions \
  -H "X-Api-Key: fw_your-project-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "free",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

### Using JWT Authentication (Web Panel)

```bash
# 1. Login
curl -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "your-password"}'

# Response: {"token": "eyJ...", "user": {...}, "expiresAt": "..."}

# 2. Use token for admin endpoints
curl http://localhost:8080/admin/projects \
  -H "Authorization: Bearer eyJ..."
```

### Check Usage Analytics

```bash
curl "http://localhost:8080/admin/analytics/usage?project_id=YOUR_PROJECT_ID" \
  -H "X-Api-Key: your-admin-key"
```

## Local Claude Code for the premium lane

`paid:premium` can be served from a Claude Code subscription running on the host
instead of a paid model, through the small sidecar in
[`deploy/claude-bridge`](deploy/claude-bridge). Nothing is billed for those requests.

**It is an optimisation, never a dependency.** If the bridge is missing, throttled,
slow or returns anything unexpected, the request continues to the normal paid chain
and the reason is written to the log:

```
Premium request could not use local Claude (exit 1: usage limit reached); using a paid model instead
Premium request skipped local Claude (rate limited: ...); using a paid model instead
```

Only `paid:premium` is eligible. `free`, `paid:low`, `paid:moderate` and `image` are
untouched.

### What it actually costs

Claude Code carries its agent scaffolding into every invocation. Measured on a
trivial prompt:

| invocation | input tokens | latency |
|---|---|---|
| default | 26,935 | 3.1s |
| as the bridge invokes it | 9,428 | 1.9s |

So roughly **9.4k tokens of overhead per request** before your own prompt, and several
seconds of latency. It saves money and spends quota, several times faster than the
equivalent API call would. Fine at low volume; it will exhaust a subscription quickly
under real traffic.

> Using a Claude Code subscription as the backend for an API gateway is likely outside
> what that subscription permits. Anthropic sells API credits for serving applications.
> This is off by default for that reason.

### How it is logged

A request served this way records `cost_source = "subscription"`, `cost_usd = 0`,
`upstream_provider` set to the Claude model, and `avoided_cost_usd` holding what the
same work would have cost at list price. The weekly email reports the count and the
total avoided, and says why the route was idle when it was.

### Health

A probe runs hourly (`LOCAL_CLAUDE_HEALTH_CRON`) and records `available`,
`rate_limited` or `unavailable`. The request path only ever reads that cached verdict,
so it never blocks on a probe. A real request failing downgrades the verdict
immediately, which is a better signal than the probe anyway.

### Setup

The bridge is a compose service. Point it at your credentials and switch it on:

```bash
LOCAL_CLAUDE_ENABLED=true
CLAUDE_HOME=/home/ubuntu    # whose ~/.claude and ~/.claude.json to mount
CLAUDE_UID=1000             # id -u  — must own those files
CLAUDE_GID=1000             # id -g
```

Then `docker compose up -d --build`. No token and no URL: the bridge publishes no
ports and sits on a private network only the API can reach.

Both mounts are read-write because Claude Code refreshes its OAuth token and writes
session state; a read-only mount works until the token expires. `~/.claude.json` sits
beside the directory rather than inside it, hence two mounts. The host and the
container share that state, so interactive use on the same box writes to the same
files.

If you would rather keep credentials off a container entirely,
[`deploy/claude-bridge/README.md`](deploy/claude-bridge/README.md) covers running it
on the host with systemd and a shared token instead.

## Email Notifications

Freeway emails the admin address in two situations.

### Weekly usage report

Sent Monday 03:00 UTC by default, covering the previous Monday-Sunday week:

- Spend this week, last week and the change between them, month to date, and all time
- Requests, success rate, tokens in and out, average response time
- Per-project breakdown with requests, tokens and cost
- Per-lane breakdown (free / low / moderate / premium / image)
- Per-model breakdown with cost per request, so an expensive model is visible even
  when its total is small
- Most used model, the model that cost the most, the cheapest per request, and the
  project that spent the most
- Remaining OpenRouter credit, the key's own limit, and its expiry date

A week with no traffic still sends a short email. Silence would otherwise be
ambiguous: a quiet week and a broken job look identical from the inbox.

### Spend and credit alerts

Checked hourly. Each alert stays quiet for `ALERT_COOLDOWN_HOURS` after firing, so a
runaway key produces one email rather than one per check.

| Alert | Fires when |
|-------|------------|
| Gateway spend | 24h spend across all projects exceeds `ALERT_GLOBAL_DAILY_USD` |
| Project spend | One project's 24h spend exceeds `ALERT_PROJECT_DAILY_USD` |
| Project requests | One project's 24h request count exceeds `ALERT_PROJECT_DAILY_REQUESTS` |
| Model spend | One model's 24h spend exceeds `ALERT_MODEL_DAILY_USD` |
| Monthly budget | Month-to-date spend exceeds `ALERT_MONTHLY_USD` |
| Credit low | OpenRouter remaining credit falls below `ALERT_OPENROUTER_CREDIT_USD` |
| Key limit low | The key's remaining limit falls below `ALERT_OPENROUTER_KEY_REMAINING_USD` |
| Key expiring | The OpenRouter key expires within `ALERT_OPENROUTER_KEY_EXPIRY_DAYS` days |
| Credential rejected | Any provider refuses its configured API key |

Only OpenRouter publishes an expiry date. For every other provider, an expired key is
indistinguishable from one that was revoked, deleted or restricted to the wrong
origin — what the gateway can observe is that it stopped being accepted, which is the
thing worth an email either way. A transient outage is deliberately not reported, so a
provider having a bad afternoon does not look like a dead key.

The request-count alert exists because `rate_limit_per_minute` is stored but not
enforced. A leaked project key is most visible as a spend or request-rate spike, and
on free models the spend rules never fire, so the count rule is the one that catches it.

Alert state is held in memory, matching the other caches in this project. A restart can
repeat an alert once, which is the right way round: a duplicate warning is cheap, a
missed one is not.

### Gmail setup

Gmail rejects account passwords over SMTP. Enable 2-Step Verification, create an
**App Password** at <https://myaccount.google.com/apppasswords>, and use the 16
characters as `SMTP_PASSWORD`. Port 587 uses STARTTLS; 465 uses implicit TLS.

Verify the setup without waiting for Monday:

```bash
curl -X POST https://freeway.pranta.dev/admin/notifications/weekly-report/send   -H "X-Api-Key: your-admin-key"
```

## Hangfire Dashboard

Background job monitoring is available at `/hangfire` (requires basic auth with `HANGFIRE_USERNAME` and `HANGFIRE_PASSWORD`).

Recurring jobs:
- **refresh-models**: Daily at midnight UTC - fetches models from all providers
- **refresh-project-cache**: Daily at 1 AM UTC - reloads project cache from database
- **weekly-usage-report**: Monday 03:00 UTC - emails the previous week's usage
- **spend-alerts**: Hourly - checks spend thresholds and OpenRouter credit

## Deployment

The included `compose.yml` is configured for Traefik reverse proxy:

| Service | Domain | Port |
|---------|--------|------|
| API | `freeway.pranta.dev` | 8080 |
| Web Panel | `freewayapp.pranta.dev` | 3243 |

Features:
- TLS: Automatic via Let's Encrypt
- Network: External `proxy` network
- Health checks: `/health` (API), `/` (Web)
- Automatic restart on failure

To customize domains, edit the Traefik labels in `compose.yml`.

## How It Works

1. **On startup**:
   - Loads environment from `.env` file
   - Initializes PostgreSQL connection via EF Core
   - Loads project cache from database
   - Fetches models from all configured providers
   - Starts Hangfire background job server

2. **Model categorization**:
   - Free models: Have `:free` suffix or zero pricing
   - Paid models: Everything else (valid pricing, context >= 8000, excluding `/auto`,
     `router` and `:batch` variants), then split into low/moderate/premium tiers
   - Image models: Fetched from OpenRouter with `?output_modalities=image`

3. **Model selection**:
   - Free: Best = largest context length
   - Paid: Per tier, the first available curated model, else the cheapest in the tier's band
   - Image: Cheapest by `image_output`, the price to generate. Ranking on prompt +
     completion would be meaningless, since nearly every image model reports 0 for
     both. Auto-routers are excluded: OpenRouter reports variable pricing as `-1`,
     which would otherwise sort them to the front of a cheapest-first list

4. **Background jobs**:
   - Hangfire runs daily refresh at midnight UTC
   - Updates model list from all providers
   - Refreshes project cache from database

5. **Request flow**:
   - Authentication validated (API key or JWT)
   - Model resolved (free/paid/image/specific)
   - Request proxied to appropriate provider
   - Usage logged to database, with the cost the provider actually billed

## Cost Accounting

Every request records what it cost and **where that figure came from**, in
`usage_logs.cost_source`:

| `cost_source` | Meaning |
|---------------|---------|
| `provider` | The amount the upstream actually billed. Authoritative |
| `subscription` | Served by local Claude Code. Nothing billed; `avoided_cost_usd` holds the list price that was not paid |
| `free_tier` | Served by a provider's own free tier (Groq, Gemini, Mistral, Cohere, HuggingFace). Genuinely zero |
| `estimated` | Tokens multiplied by a cached catalog price. Approximate |
| `backfilled` | Re-costed after the fact at catalog prices. Approximate |
| `legacy` | Written before cost accounting was fixed. Under-reported |
| `unknown` | Neither a price nor a billed amount was available |

OpenRouter requests carry `{"usage":{"include":true}}`, so the response returns
`usage.cost` — the real charge — along with the endpoint that served it
(recorded as `upstream_provider`).

This matters because of provider routing: with `OPENROUTER_PROVIDER_SORT=throughput`,
the endpoint serving a request may not charge the model's headline rate. A measured
request billed `$0.00000036` where the cached-price estimate gave `$0.00000025` — a
44% under-estimate on a single call.

### What the free lane tries

In order, stopping at the first success:

1. Direct free-tier providers (Groq, Gemini, Mistral, Cohere, HuggingFace), in
   benchmark-ranked order
2. OpenRouter's own zero-cost models, selected model first then by rank, up to
   `FREE_LANE_OPENROUTER_COUNT`
3. `503` — never a paid model

Step 2 exists because those models were being fetched, ranked and shown on the
dashboard while being unreachable: the orchestrator only considered providers flagged
`IsFreeProvider`, and OpenRouter is not one. The "selected free model" was decorative.
Each candidate is re-checked for having a zero price before it is called, so this rung
cannot produce a charge whatever the catalog says.

### Why `free` never falls back to paid

The free lane used to fall through to a paid OpenRouter model when every free
provider failed. The request succeeded, but it was logged as `free` with a cost of
zero, so real spend became invisible. On one gateway this hid 181 requests and
283,047 tokens on a paid model.

A lane called `free` must never produce a bill. Callers that want a paid model on
failure ask for one explicitly with `paid` or `paid:<tier>`.

`OpenAiProvider` is likewise **not** treated as a free provider — OpenAI bills per
token, so it must never sit in the free lane's rotation.

## Tech Stack

### Backend
- **.NET 10** - Runtime
- **ASP.NET Core** - Web framework
- **Entity Framework Core** - ORM
- **PostgreSQL** - Database
- **MediatR** - CQRS pattern
- **FluentValidation** - Request validation
- **Hangfire** - Background jobs
- **Serilog** - Logging
- **BCrypt.Net** - Password & API key hashing
- **JWT** - Web authentication
- **Scalar** - API documentation (available at `/scalar/v1` in development)

### Frontend (Web Panel)
- **Next.js 16** - React framework with App Router
- **React 19** - UI library
- **Tailwind CSS** - Styling, driven by the semantic tokens in `globals.css`
- **IBM Plex Sans / Mono** - Typography, with tabular figures on compared numbers
- **TanStack Query** - Data fetching & caching
- **Zustand** - State management
- **Lucide React** - Icons
- **TypeScript** - Type safety

Charts are plain HTML and CSS rather than a charting library. The one chart in the
panel is a ranked bar list, which a `div` with a width does better than a dependency.

## License

MIT
