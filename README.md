# Freeway

An AI Gateway that proxies requests to OpenRouter with project management, usage tracking, and analytics.

## Overview

Freeway is a full-featured AI Gateway built with FastAPI that:
- Proxies chat completion requests to OpenRouter
- Manages projects with individual API keys
- Tracks usage, costs, and analytics per project
- Automatically selects best free and cheapest paid models
- Provides a complete admin API for management

## Features

- **OpenAI-Compatible Chat Endpoint**: `POST /chat/completions` with model selection (`free`, `paid`, or specific model ID)
- **Project Management**: Create projects with individual API keys, rate limits, and metadata
- **Usage Tracking**: Logs all requests with tokens, costs, and response times
- **Admin Analytics**: Usage summaries, per-project stats, and detailed logs
- **Model Selection**: Auto-selects best free model (by context) and cheapest paid model (by price)
- **Daily Refresh**: Models updated at midnight UTC
- **PostgreSQL Storage**: Persistent storage for projects and usage data
- **Docker Ready**: Includes docker-compose with PostgreSQL

## Authentication

Freeway uses two types of API keys:

| Key Type | Header | Used For |
|----------|--------|----------|
| Admin Key | `X-Api-Key` | Admin endpoints, model info, control panel |
| Project Key | `X-Api-Key` | Chat completions endpoint |

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
- `"free"` - Use best free model (auto-selected)
- `"paid"` - Use cheapest paid model (auto-selected)
- `"<model_id>"` - Use specific model by ID

**Response:**
```json
{
  "id": "chatcmpl-abc123",
  "created": 1234567890,
  "model": "x-ai/grok-4.1-fast:free",
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
GET /model/free      # Best free model
GET /model/paid      # Cheapest paid model
GET /models/free     # All free models (ranked by context)
GET /models/paid     # All paid models (ranked by price)
GET /health          # Service health check
```

### Admin Endpoints (Admin Key Only)

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

#### Analytics

```bash
GET /admin/analytics/summary        # Global summary
GET /admin/analytics/usage?project_id={id}  # Project usage stats
GET /admin/analytics/logs?project_id={id}   # Detailed usage logs
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
| `ADMIN_API_KEY` | Yes | Admin API key for control panel |
| `OPENROUTER_API_KEY` | Yes | OpenRouter API key for chat completions |
| `DATABASE_URL` | No | PostgreSQL connection string |
| `DB_HOST` | No | Database host (default: localhost) |
| `DB_PORT` | No | Database port (default: 5432) |
| `DB_USER` | No | Database user (default: freeway) |
| `DB_PASSWORD` | No | Database password |
| `DB_NAME` | No | Database name (default: freeway) |

## Quick Start

### Local Development

```bash
# Clone and enter directory
cd freeway

# Create virtual environment
python -m venv venv
source venv/bin/activate  # or `venv\Scripts\activate` on Windows

# Install dependencies
pip install -r requirements.txt

# Configure environment
cp .env.example .env
# Edit .env and set ADMIN_API_KEY and OPENROUTER_API_KEY

# Start PostgreSQL (or use Docker)
# ...

# Run
uvicorn app.main:app --reload
```

### Docker

```bash
# Start all services (API + PostgreSQL)
docker compose up -d --build

# View logs
docker compose logs -f freeway

# Stop services
docker compose down
```

## Project Structure

```
freeway/
├── app/
│   ├── api/
│   │   ├── auth.py             # Dual authentication (admin/project)
│   │   ├── routes.py           # Model endpoints
│   │   ├── chat.py             # Chat completions endpoint
│   │   └── admin.py            # Admin CRUD & analytics
│   ├── db/
│   │   ├── connection.py       # Async database pool
│   │   └── repositories/
│   │       ├── project_repo.py
│   │       └── usage_repo.py
│   ├── models/
│   │   ├── openrouter.py       # OpenRouter API models
│   │   └── database.py         # SQLAlchemy ORM models
│   ├── schemas/
│   │   ├── responses.py        # Model response schemas
│   │   ├── chat.py             # Chat completion schemas
│   │   ├── projects.py         # Project schemas
│   │   └── analytics.py        # Analytics schemas
│   ├── services/
│   │   ├── openrouter_service.py  # OpenRouter API client
│   │   ├── ranking_service.py     # Model ranking/selection
│   │   ├── project_service.py     # Project management
│   │   └── usage_service.py       # Usage tracking
│   ├── storage/
│   │   ├── memory_store.py     # In-memory model cache
│   │   └── project_cache.py    # In-memory project cache
│   ├── config.py               # Configuration
│   ├── main.py                 # FastAPI application
│   └── scheduler.py            # Daily refresh scheduler
├── Dockerfile
├── compose.yml
├── requirements.txt
└── .env.example
```

## Usage Examples

### Create a Project and Make a Request

```python
import httpx

FREEWAY_URL = "http://localhost:8000"
ADMIN_KEY = "your-admin-key"

# 1. Create a project
response = httpx.post(
    f"{FREEWAY_URL}/admin/projects",
    headers={"X-Api-Key": ADMIN_KEY},
    json={"name": "My App", "rate_limit_per_minute": 100}
)
project = response.json()
project_key = project["api_key"]  # Save this!

# 2. Use the project key for chat completions
response = httpx.post(
    f"{FREEWAY_URL}/chat/completions",
    headers={"X-Api-Key": project_key},
    json={
        "model": "free",
        "messages": [{"role": "user", "content": "Hello!"}]
    }
)
completion = response.json()
print(completion["choices"][0]["message"]["content"])
```

### Check Usage Analytics

```python
# Get project usage
response = httpx.get(
    f"{FREEWAY_URL}/admin/analytics/usage",
    headers={"X-Api-Key": ADMIN_KEY},
    params={"project_id": project["id"]}
)
usage = response.json()
print(f"Total requests: {usage['summary']['total_requests']}")
print(f"Total cost: ${usage['summary']['total_cost_usd']:.4f}")
```

## Flutter Control Panel

A Flutter control panel is available at `../control_plane` for managing Freeway:

- Dashboard with stats and selected models
- Browse and search all available models
- Create, edit, and delete projects
- View API keys and rotate them
- Light/dark theme support
- Responsive design (mobile, tablet, desktop)

See `../control_plane/README.md` for setup instructions.

## Deployment

The included `compose.yml` is configured for Traefik reverse proxy:

- Domain: `freeway.pranta.dev` (configure in compose.yml)
- TLS: Automatic via Let's Encrypt
- Network: External `proxy` network

## How It Works

1. **On startup**:
   - Initializes PostgreSQL connection
   - Loads project cache from database
   - Fetches models from OpenRouter API

2. **Model categorization**:
   - Free models: Have `:free` suffix or zero pricing
   - Paid models: Everything else

3. **Model selection**:
   - Free: Best = largest context length
   - Paid: Best = lowest combined price

4. **Daily refresh**:
   - Scheduler runs at midnight UTC
   - Updates model list
   - Refreshes project cache

5. **Request flow**:
   - Project key validated against cache
   - Model resolved (free/paid/specific)
   - Request proxied to OpenRouter
   - Usage logged asynchronously
