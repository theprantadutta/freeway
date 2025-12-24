# Freeway

An AI Gateway that proxies requests to OpenRouter with project management, usage tracking, and analytics.

## Overview

Freeway is a full-featured AI Gateway built with .NET 10 that:
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
- **Daily Refresh**: Models updated via Hangfire background jobs
- **PostgreSQL Storage**: Persistent storage for projects and usage data
- **Docker Ready**: Includes Dockerfile and compose.yaml with Traefik support

## Architecture

Built with Clean Architecture pattern:

```
freeway/
├── src/
│   ├── Freeway.Domain/           # Entities, Interfaces
│   ├── Freeway.Application/      # CQRS handlers, DTOs, Validators
│   ├── Freeway.Infrastructure/   # EF Core, OpenRouter client, Caching
│   └── Freeway.Api/              # Controllers, Middleware
├── Dockerfile
├── compose.yaml
└── .env.example
```

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

#### Model Selection

```bash
PUT /admin/model/free   # Set selected free model
PUT /admin/model/paid   # Set selected paid model
```

**Request:**
```json
{
  "model_id": "google/gemma-3-27b-it:free"
}
```

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
| `DB_HOST` | No | Database host (default: localhost) |
| `DB_PORT` | No | Database port (default: 5432) |
| `DB_USER` | No | Database user (default: postgres) |
| `DB_PASSWORD` | No | Database password |
| `DB_NAME` | No | Database name (default: freeway) |
| `HANGFIRE_USERNAME` | No | Hangfire dashboard username (default: admin) |
| `HANGFIRE_PASSWORD` | No | Hangfire dashboard password (default: admin) |
| `ALLOWED_ORIGINS` | No | CORS origins (default: *) |

## Quick Start

### Prerequisites

- .NET 10 SDK
- PostgreSQL database

### Local Development

```bash
# Clone and enter directory
cd freeway

# Configure environment
cp .env.example .env
# Edit .env and set ADMIN_API_KEY, OPENROUTER_API_KEY, and DB_* values

# Restore and build
dotnet restore
dotnet build

# Run migrations
dotnet ef database update -p src/Freeway.Infrastructure -s src/Freeway.Api

# Run the application
dotnet run --project src/Freeway.Api
```

The API will be available at `http://localhost:5000` (or as configured in launchSettings.json).

### Docker

```bash
# Build and start
docker compose up -d --build

# View logs
docker compose logs -f freeway

# Stop services
docker compose down
```

## Project Structure

```
freeway/
├── src/
│   ├── Freeway.Domain/
│   │   ├── Entities/              # Project, UsageLog, ChatMessage
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
│   │   ├── Services/              # OpenRouter, Caching, ApiKey services
│   │   ├── Jobs/                  # Hangfire background jobs
│   │   └── DependencyInjection.cs
│   └── Freeway.Api/
│       ├── Controllers/           # API controllers
│       ├── Middleware/            # Auth, Exception handling
│       ├── Attributes/            # RequireAdmin, RequireProject
│       └── Program.cs             # Application startup
├── Dockerfile
├── compose.yaml
├── Freeway.sln
└── .env.example
```

## Usage Examples

### Create a Project and Make a Request

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
var adminKey = "your-admin-key";

// 1. Create a project
client.DefaultRequestHeaders.Add("X-Api-Key", adminKey);
var createResponse = await client.PostAsJsonAsync("/admin/projects", new
{
    name = "My App",
    rate_limit_per_minute = 100
});
var project = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
var projectKey = project.GetProperty("api_key").GetString(); // Save this!

// 2. Use the project key for chat completions
client.DefaultRequestHeaders.Remove("X-Api-Key");
client.DefaultRequestHeaders.Add("X-Api-Key", projectKey);
var chatResponse = await client.PostAsJsonAsync("/chat/completions", new
{
    model = "free",
    messages = new[] { new { role = "user", content = "Hello!" } }
});
var completion = await chatResponse.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(completion.GetProperty("choices")[0]
    .GetProperty("message")
    .GetProperty("content")
    .GetString());
```

### Check Usage Analytics

```csharp
client.DefaultRequestHeaders.Clear();
client.DefaultRequestHeaders.Add("X-Api-Key", adminKey);

var usageResponse = await client.GetAsync($"/admin/analytics/usage?project_id={projectId}");
var usage = await usageResponse.Content.ReadFromJsonAsync<JsonElement>();
var summary = usage.GetProperty("summary");
Console.WriteLine($"Total requests: {summary.GetProperty("total_requests")}");
Console.WriteLine($"Total cost: ${summary.GetProperty("total_cost_usd"):F4}");
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

## Hangfire Dashboard

Background job monitoring is available at `/hangfire` (requires basic auth with `HANGFIRE_USERNAME` and `HANGFIRE_PASSWORD`).

Recurring jobs:
- **refresh-models**: Daily at midnight UTC - fetches models from OpenRouter
- **refresh-project-cache**: Daily at 1 AM UTC - reloads project cache from database

## Deployment

The included `compose.yaml` is configured for Traefik reverse proxy:

- Domain: `freeway.pranta.dev` (configure in compose.yaml)
- TLS: Automatic via Let's Encrypt
- Network: External `proxy` network
- Health check: `/health` endpoint

## How It Works

1. **On startup**:
   - Loads environment from `.env` file
   - Initializes PostgreSQL connection via EF Core
   - Loads project cache from database
   - Fetches models from OpenRouter API
   - Starts Hangfire background job server

2. **Model categorization**:
   - Free models: Have `:free` suffix or zero pricing
   - Paid models: Everything else (filtered for valid pricing)

3. **Model selection**:
   - Free: Best = largest context length
   - Paid: Best = lowest combined price

4. **Background jobs**:
   - Hangfire runs daily refresh at midnight UTC
   - Updates model list from OpenRouter
   - Refreshes project cache from database

5. **Request flow**:
   - Project key validated against cached hashes (BCrypt)
   - Model resolved (free/paid/specific)
   - Request proxied to OpenRouter
   - Usage logged to database

## Tech Stack

- **.NET 10** - Runtime
- **ASP.NET Core** - Web framework
- **Entity Framework Core** - ORM
- **PostgreSQL** - Database
- **MediatR** - CQRS pattern
- **FluentValidation** - Request validation
- **Hangfire** - Background jobs
- **Serilog** - Logging
- **BCrypt.Net** - API key hashing
- **Scalar** - API documentation (available at `/scalar/v1` in development)
