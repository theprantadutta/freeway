# Freeway

A lightweight FastAPI service that selects the best free and cheapest paid OpenRouter models for your projects.

## Overview

Freeway fetches all models from OpenRouter, categorizes them as free or paid, and automatically selects:
- **Best Free Model**: Ranked by context length (larger is better)
- **Cheapest Paid Model**: Ranked by price (lower prompt + completion cost)

Models are refreshed daily at midnight UTC. Free model selection auto-updates, paid model stays fixed.

## Features

- **API Key Authentication**: All endpoints require `X-Api-Key` header
- **Automatic Model Discovery**: Fetches and categorizes all OpenRouter models
- **Separate Endpoints**: Dedicated endpoints for free and paid models
- **Daily Refresh**: Scheduler updates model list at midnight UTC
- **Full Model Details**: Returns pricing, context length, and description
- **Docker Ready**: Includes Dockerfile and docker-compose with Traefik integration

## Authentication

All API endpoints require authentication via the `X-Api-Key` header.

```bash
curl -H "X-Api-Key: your-api-key" http://localhost:8000/model/free
```

Without a valid API key, requests return `401 Unauthorized`.

**Note**: Swagger/OpenAPI docs are disabled in production for security.

## API Endpoints

### GET /model/free

Returns the best free model (highest context length).

```json
{
  "model_id": "x-ai/grok-4.1-fast:free",
  "model_name": "xAI: Grok 4.1 Fast (free)",
  "description": "Grok 4.1 Fast is xAI's best agentic tool calling model...",
  "context_length": 2000000,
  "pricing": {
    "prompt": "0",
    "completion": "0"
  }
}
```

### GET /model/paid

Returns the cheapest paid model (lowest prompt + completion cost).

```json
{
  "model_id": "meta-llama/llama-3.2-3b-instruct",
  "model_name": "Meta: Llama 3.2 3B Instruct",
  "description": "Llama 3.2 3B is a 3-billion-parameter multilingual model...",
  "context_length": 131072,
  "pricing": {
    "prompt": "0.00000002",
    "completion": "0.00000002"
  }
}
```

### GET /models/free

Returns all free models ranked by context length (largest first).

```json
{
  "models": [
    {
      "model_id": "x-ai/grok-4.1-fast:free",
      "model_name": "xAI: Grok 4.1 Fast (free)",
      "context_length": 2000000,
      "pricing": {"prompt": "0", "completion": "0"},
      "rank": 1
    },
    ...
  ],
  "total_count": 30,
  "last_updated": "2025-12-03T00:00:00Z"
}
```

### GET /models/paid

Returns all paid models ranked by price (cheapest first).

```json
{
  "models": [
    {
      "model_id": "meta-llama/llama-3.2-3b-instruct",
      "model_name": "Meta: Llama 3.2 3B Instruct",
      "context_length": 131072,
      "pricing": {"prompt": "0.00000002", "completion": "0.00000002"},
      "rank": 1
    },
    ...
  ],
  "total_count": 305,
  "last_updated": "2025-12-03T00:00:00Z"
}
```

### GET /health

Service health check endpoint.

```json
{
  "status": "healthy",
  "service": "freeway",
  "version": "1.0.0",
  "free_models_count": 30,
  "paid_models_count": 305,
  "selected_free_model": "x-ai/grok-4.1-fast:free",
  "selected_paid_model": "meta-llama/llama-3.2-3b-instruct",
  "last_refresh": "2025-12-03T00:00:00Z"
}
```

## Configuration

Environment variables (see `.env.example`):

| Variable | Default | Description |
|----------|---------|-------------|
| `API_KEY` | (required) | API key for accessing Freeway endpoints |
| `OPENROUTER_API_KEY` | - | OpenRouter API key (for future use) |
| `REQUEST_TIMEOUT_SECONDS` | `30` | Timeout for API requests |

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
# Edit .env and set API_KEY

# Run
uvicorn app.main:app --reload
```

### Docker

```bash
# Build and run
docker compose up -d --build

# View logs
docker compose logs -f freeway
```

## Project Structure

```
freeway/
├── app/
│   ├── api/
│   │   ├── auth.py             # API key authentication
│   │   └── routes.py           # API endpoints
│   ├── models/
│   │   └── openrouter.py       # OpenRouter API models
│   ├── schemas/
│   │   └── responses.py        # API response schemas
│   ├── services/
│   │   ├── openrouter_service.py    # OpenRouter API client
│   │   └── ranking_service.py       # Model ranking/selection
│   ├── storage/
│   │   └── memory_store.py     # In-memory data store
│   ├── config.py               # Configuration
│   ├── main.py                 # FastAPI application
│   └── scheduler.py            # Daily model refresh scheduler
├── Dockerfile
├── compose.yml
├── requirements.txt
└── .env.example
```

## Usage Example

From another project, get the best free model:

```python
import httpx

FREEWAY_URL = "http://localhost:8000"
FREEWAY_API_KEY = "your-freeway-api-key"

headers = {"X-Api-Key": FREEWAY_API_KEY}

# Get best free model
response = httpx.get(f"{FREEWAY_URL}/model/free", headers=headers)
free_model = response.json()
print(f"Best free: {free_model['model_id']}")

# Get cheapest paid model
response = httpx.get(f"{FREEWAY_URL}/model/paid", headers=headers)
paid_model = response.json()
print(f"Cheapest paid: {paid_model['model_id']}")
```

## Deployment

The included `compose.yml` is configured for Traefik reverse proxy:

- Domain: `freeway.pranta.dev` (configure in compose.yml)
- TLS: Automatic via Let's Encrypt
- Network: External `proxy` network (must exist)

## How It Works

1. **On startup**: Fetches all models from OpenRouter API
2. **Categorization**: Splits models into free (`:free` suffix) and paid
3. **Selection**:
   - Free: Best = largest context length
   - Paid: Best = lowest (prompt + completion) cost
4. **Daily refresh**: Scheduler runs at midnight UTC to update models
5. **Auto-update**: Free model selection updates if better model available
6. **Fixed paid**: Paid model selection stays fixed (not auto-updated)
