# Freeway

A multi-provider AI Gateway with project management, usage tracking, analytics, and a web control panel.

## Overview

Freeway is a full-featured AI Gateway built with .NET 10 that:
- Proxies chat completion requests to multiple AI providers (OpenRouter, OpenAI, Gemini, Groq, Mistral, Cohere, HuggingFace)
- Manages projects with individual API keys
- Tracks usage, costs, and analytics per project
- Automatically selects best free and cheapest paid models
- Provides a complete admin API for management
- Includes a modern Next.js web control panel with JWT authentication

## Features

- **OpenAI-Compatible Chat Endpoint**: `POST /chat/completions` with model selection (`free`, `paid`, or specific model ID)
- **Multi-Provider Support**: Fallback across 8 AI providers for reliability
- **Project Management**: Create projects with individual API keys, rate limits, and metadata
- **Usage Tracking**: Logs all requests with tokens, costs, and response times
- **Admin Analytics**: Usage summaries, per-project stats, and detailed logs
- **Model Selection**: Auto-selects best free model (by context) and cheapest paid model (by price)
- **Daily Refresh**: Models updated via Hangfire background jobs
- **PostgreSQL Storage**: Persistent storage for projects, users, and usage data
- **Web Control Panel**: Next.js 15 dashboard with JWT authentication
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
│   └── Freeway.Web/              # Next.js 15 Control Panel
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
- `"free"` - Use best free model (auto-selected)
- `"paid"` - Use cheapest paid model (auto-selected)
- `"<model_id>"` - Use specific model by ID

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
GET /model/free      # Best free model
GET /model/paid      # Cheapest paid model
GET /models/free     # All free models (ranked by context)
GET /models/paid     # All paid models (ranked by price)
GET /health          # Service health check
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
│   └── Freeway.Web/               # Next.js 15 Control Panel
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
- Selected models display (free and paid)
- Quick navigation to all features

### Models
- Browse all available models (free and paid tabs)
- Search models by name or ID
- View model details (context length, pricing, capabilities)
- Select active free/paid models

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

## Hangfire Dashboard

Background job monitoring is available at `/hangfire` (requires basic auth with `HANGFIRE_USERNAME` and `HANGFIRE_PASSWORD`).

Recurring jobs:
- **refresh-models**: Daily at midnight UTC - fetches models from all providers
- **refresh-project-cache**: Daily at 1 AM UTC - reloads project cache from database

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
   - Paid models: Everything else (filtered for valid pricing)

3. **Model selection**:
   - Free: Best = largest context length
   - Paid: Best = lowest combined price

4. **Background jobs**:
   - Hangfire runs daily refresh at midnight UTC
   - Updates model list from all providers
   - Refreshes project cache from database

5. **Request flow**:
   - Authentication validated (API key or JWT)
   - Model resolved (free/paid/specific)
   - Request proxied to appropriate provider
   - Usage logged to database

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
- **Next.js 15** - React framework with App Router
- **React 19** - UI library
- **Tailwind CSS** - Styling
- **TanStack Query** - Data fetching & caching
- **Zustand** - State management
- **Recharts** - Usage charts
- **Lucide React** - Icons
- **TypeScript** - Type safety

## License

MIT
