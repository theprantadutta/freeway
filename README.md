# Freeway

A lightweight FastAPI service that monitors and recommends the best free OpenRouter models for your projects.

## Overview

Freeway fetches free models from OpenRouter, performs periodic health checks, and ranks them using a weighted scoring algorithm. Other projects can query Freeway to get the best available free model at any time.

## Features

- **Automatic Model Discovery**: Fetches all free models (`:free` suffix) from OpenRouter API
- **Health Monitoring**: Periodic health checks with configurable intervals
- **Smart Rate Limiting**: Handles 429 errors with automatic retry after 60 seconds
- **Weighted Scoring**: Ranks models by availability, speed, and context length
- **External Reporting**: Other projects can report failing models via API
- **Docker Ready**: Includes Dockerfile and docker-compose with Traefik integration

## Scoring Algorithm

Models are ranked using a weighted scoring system (0-100 points):

| Factor | Weight | Description |
|--------|--------|-------------|
| Availability | 50% | Health check success rate |
| Speed | 30% | Response time (lower is better) |
| Context Length | 20% | Larger context = bonus points |

## API Endpoints

### GET /model

Returns the best available model based on ranking score.

```json
{
  "model_id": "google/gemini-2.0-flash-exp:free",
  "model_name": "Gemini 2.0 Flash Exp",
  "context_length": 1048576,
  "availability_score": 1.0,
  "avg_response_time_ms": 2345.67,
  "last_check": "2025-12-02T18:30:00Z",
  "last_status": "success",
  "rank": 1,
  "score": 85.5
}
```

### GET /models

Returns all tracked models ranked by score.

```json
{
  "models": [...],
  "total_count": 28,
  "last_updated": "2025-12-02T18:30:00Z"
}
```

### GET /health

Service health check endpoint.

```json
{
  "status": "healthy",
  "service": "freeway",
  "version": "1.0.0",
  "models_monitored": 28,
  "health_checks_enabled": true,
  "last_check_run": "2025-12-02T18:30:00Z"
}
```

### POST /report

Report a failing model from external projects. Freeway will verify and remove it if confirmed failing.

**Request:**
```json
{
  "model_id": "some-model/name:free"
}
```

**Response:**
```json
{
  "model_id": "some-model/name:free",
  "action": "removed",
  "message": "Model failed health check and was removed",
  "health_check_passed": false
}
```

Possible actions: `removed`, `kept`, `not_found`

## Configuration

Environment variables (see `.env.example`):

| Variable | Default | Description |
|----------|---------|-------------|
| `OPENROUTER_API_KEY` | (required for health checks) | Your OpenRouter API key |
| `HEALTH_CHECK_ENABLED` | `true` | Enable/disable health checks |
| `CHECK_INTERVAL_SECONDS` | `86400` | Interval between health check cycles (24h) |
| `CHECK_DELAY_SECONDS` | `30` | Delay between each model check |
| `HISTORY_SIZE` | `20` | Number of health results to keep per model |
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
# Edit .env and add your OPENROUTER_API_KEY

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
│   │   └── routes.py           # API endpoints
│   ├── models/
│   │   ├── health_check.py     # Health check data models
│   │   └── openrouter.py       # OpenRouter API models
│   ├── schemas/
│   │   └── responses.py        # API response schemas
│   ├── services/
│   │   ├── health_check_service.py  # Health check orchestration
│   │   ├── openrouter_service.py    # OpenRouter API client
│   │   └── ranking_service.py       # Model ranking algorithm
│   ├── storage/
│   │   └── memory_store.py     # In-memory data store
│   ├── config.py               # Configuration
│   ├── main.py                 # FastAPI application
│   └── scheduler.py            # Background task scheduler
├── Dockerfile
├── compose.yml
├── requirements.txt
└── .env.example
```

## Usage Example

From another project, get the best free model:

```python
import httpx

response = httpx.get("http://localhost:8000/model")
best_model = response.json()

# Use the model ID with OpenRouter
model_id = best_model["model_id"]  # e.g., "google/gemini-2.0-flash-exp:free"
```

Report a failing model:

```python
import httpx

httpx.post("http://localhost:8000/report", json={
    "model_id": "failing-model/name:free"
})
```

## Deployment

The included `compose.yml` is configured for Traefik reverse proxy:

- Domain: `freeway.pranta.dev` (configure in compose.yml)
- TLS: Automatic via Let's Encrypt
- Network: External `proxy` network (must exist)

## Notes

- Models are identified by `:free` suffix in their ID
- Health checks require an OpenRouter API key with credits (for higher rate limits)
- Without API key, the service still serves the model list but without health data
- All timestamps are in UTC
