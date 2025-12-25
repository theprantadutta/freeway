# Freeway AI Gateway - Complete Request Flow Analysis

## Project Overview

**Freeway** is a sophisticated **multi-provider AI gateway** built with .NET 10 using Clean Architecture (CQRS pattern with MediatR). It acts as an OpenAI-compatible proxy that intelligently routes requests across 8 AI providers with smart fallback, benchmarking, and usage tracking.

---

## Complete Request Flow for `POST /chat/completions`

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              REQUEST LIFECYCLE                              │
└─────────────────────────────────────────────────────────────────────────────┘

    Client Request (X-Api-Key header + JSON body)
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 1: MIDDLEWARE PIPELINE                                              │
│  ─────────────────────────────────────────────────────────────────────────│
│  GlobalExceptionHandlerMiddleware.cs:16-27                                 │
│  └─► Wraps entire pipeline in try/catch                                    │
│  └─► Catches unhandled exceptions → JSON error response                    │
│                                                                            │
│  ApiKeyAuthenticationMiddleware.cs:31-101                                  │
│  └─► Extracts X-Api-Key header (line 58)                                   │
│  └─► Check 1: Is it ADMIN_API_KEY? → Set admin claims                     │
│  └─► Check 2: Call projectCacheService.ValidateApiKey(apiKey)             │
│        └─► ProjectCacheService.cs:75-98                                   │
│        └─► Iterates cached projects, BCrypt verifies hash                 │
│        └─► Returns ProjectInfo (id, name, rate_limit, is_active)          │
│  └─► Sets ClaimsPrincipal with project_id, project_name, role claims      │
│  └─► 401 if key missing/invalid                                           │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 2: CONTROLLER LAYER                                                 │
│  ─────────────────────────────────────────────────────────────────────────│
│  ChatController.cs:10-31                                                   │
│                                                                            │
│  [HttpPost("/chat/completions")]                                           │
│  [RequireProject]  ← Authorization attribute (requires project key)        │
│                                                                            │
│  1. Extract projectId from User claims (line 14)                           │
│  2. Create CreateChatCompletionCommand with all params (lines 16-27)       │
│  3. Send command via MediatR: await Mediator.Send(command)                 │
│  4. HandleResult() → returns appropriate HTTP status                       │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 3: VALIDATION PIPELINE (MediatR Behavior)                           │
│  ─────────────────────────────────────────────────────────────────────────│
│  ValidationBehavior.cs:16-39                                               │
│                                                                            │
│  CreateChatCompletionCommandValidator.cs validates:                        │
│  └─► ProjectId: NotEmpty                                                   │
│  └─► Model: NotEmpty                                                       │
│  └─► Messages: NotEmpty, Count > 0                                         │
│  └─► Each message: Role + Content required                                 │
│  └─► Temperature: 0-2 range (if provided)                                  │
│  └─► MaxTokens: > 0 (if provided)                                          │
│                                                                            │
│  Throws ValidationException if failures → 400 Bad Request                  │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 4: COMMAND HANDLER (Core Business Logic)                            │
│  ─────────────────────────────────────────────────────────────────────────│
│  CreateChatCompletionCommandHandler.cs:39-128                              │
│                                                                            │
│  STEP 1: Convert DTOs to Domain Entities (lines 42-58)                     │
│  └─► ChatMessageDto[] → ChatMessage[]                                      │
│  └─► Create ChatCompletionOptions with all params                          │
│                                                                            │
│  STEP 2: Route Based on Model Type (line 66)                               │
│  ┌────────────────────────────────────────────────────────────────────┐   │
│  │  IF model == "free":                                               │   │
│  │     → Use ProviderOrchestrator.ExecuteWithFallbackAsync()          │   │
│  │     → Smart multi-provider routing with benchmarks                 │   │
│  │                                                                    │   │
│  │  IF model == "paid" or specific model ID:                          │   │
│  │     → ResolveModel() to find model in caches                       │   │
│  │     → Call OpenRouterService directly                              │   │
│  └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│  STEP 3: Fire-and-Forget Usage Logging (line 98)                           │
│  └─► Task.Run(() => LogUsageInBackgroundAsync(...))                        │
│  └─► Creates new DI scope, saves UsageLog to PostgreSQL                    │
│                                                                            │
│  STEP 4: Return Result (lines 100-127)                                     │
│  └─► Success: 200 OK with ChatCompletionResponseDto                        │
│  └─► Failure: 502 Bad Gateway with error message                           │
└───────────────────────────────────────────────────────────────────────────┘
                    │
         ┌─────────┴──────────┐
         │                    │
    model="free"         model="paid"/specific
         │                    │
         ▼                    ▼
┌────────────────────┐ ┌────────────────────────────────────────────────────┐
│ PHASE 5A:          │ │ PHASE 5B: MODEL RESOLUTION                          │
│ ORCHESTRATOR       │ │ ──────────────────────────────────────────────────│
│                    │ │ ResolveModel() at line 130-183                     │
│ ProviderOrchestra- │ │                                                     │
│ tor.cs:30-110      │ │ Priority order:                                    │
│                    │ │ 1. "free" → GetSelectedFreeModel()                 │
│ 1. Get ranked      │ │ 2. "paid" → GetSelectedPaidModel()                 │
│    providers from  │ │ 3. Check ModelCacheService (OpenRouter models)     │
│    benchmark cache │ │ 4. Check ProviderModelCache (all providers)        │
│                    │ │ 5. OpenRouter format check (contains "/")          │
│ 2. Filter to FREE  │ │ 6. Return error if not found                       │
│    providers only  │ │                                                     │
│                    │ │ Then → OpenRouterService.CreateChatCompletionAsync │
│ 3. Try each in     │ └────────────────────────────────────────────────────┘
│    ranked order    │
│    with retries    │
│                    │
│ 4. On failure,     │
│    fallback to     │
│    OpenRouter paid │
└────────────────────┘
         │
         ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 6: PROVIDER EXECUTION (For "free" model via Orchestrator)           │
│  ─────────────────────────────────────────────────────────────────────────│
│  ProviderOrchestrator.cs:30-166                                            │
│                                                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  PROVIDER RANKING (ProviderBenchmarkCache.cs)                       │  │
│  │  ─────────────────────────────────────────────────────────────────  │  │
│  │  Default order: gemini → groq → openai → mistral → cohere → hugging │  │
│  │                                                                      │  │
│  │  Score formula: (SuccessRate × 100) - (AvgResponseTimeMs / 100)     │  │
│  │  └─► Updated after each request (line 77)                           │  │
│  │  └─► Providers with <30% success get deprioritized                  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  FOR EACH ranked free provider:                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  1. Check if provider.IsEnabled (has API key)                       │  │
│  │  2. Get best model from ProviderModelCache                          │  │
│  │  3. TryProviderWithRetryAsync() (lines 112-166)                     │  │
│  │     └─► Max 2 retries with delays [500ms, 1000ms]                   │  │
│  │     └─► 429 rate limit → skip to next provider immediately          │  │
│  │     └─► 4xx client error → skip to next provider immediately        │  │
│  │     └─► 5xx server error → retry then next provider                 │  │
│  │  4. On success: update benchmark, return result                     │  │
│  │  5. On failure: add to errors list, try next provider               │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  IF all free providers fail:                                               │
│  └─► Fallback to OpenRouter (paid) as last resort                          │
│  └─► If that fails: return 502 with all error messages                     │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 7: INDIVIDUAL PROVIDER API CALL                                     │
│  ─────────────────────────────────────────────────────────────────────────│
│  Example: GeminiProvider.cs:34-146                                         │
│                                                                            │
│  1. Start stopwatch for timing                                             │
│  2. Create cancellation token with COMPLETION_TIMEOUT (default 120s)       │
│  3. Convert messages to provider format:                                   │
│     └─► Gemini: system → systemInstruction, assistant → "model"           │
│     └─► OpenAI: standard roles                                             │
│  4. Build request with generation config (temp, max_tokens, etc.)          │
│  5. HTTP POST to provider API:                                             │
│     └─► Gemini: generativelanguage.googleapis.com/v1beta/models/{m}       │
│     └─► OpenAI: api.openai.com/v1/chat/completions                        │
│     └─► Groq: api.groq.com/openai/v1/chat/completions                     │
│  6. Parse response, map to unified ChatCompletionResult                    │
│  7. Handle errors: timeout, API errors, parse failures                     │
│  8. Return result with response time, tokens, finish reason                │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 8: BACKGROUND USAGE LOGGING                                         │
│  ─────────────────────────────────────────────────────────────────────────│
│  CreateChatCompletionCommandHandler.cs:185-250                             │
│                                                                            │
│  Fire-and-forget (doesn't block response):                                 │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  1. Create new DI scope (line 195)                                  │  │
│  │  2. Calculate costs from model pricing:                             │  │
│  │     └─► promptCost × inputTokens + completionCost × outputTokens    │  │
│  │  3. Build UsageLog entity with:                                     │  │
│  │     └─► ProjectId, ModelId, ModelType                               │  │
│  │     └─► InputTokens, OutputTokens, ResponseTimeMs                   │  │
│  │     └─► CostUsd, Provider, RequestId                                │  │
│  │     └─► RequestMessages (full conversation)                         │  │
│  │     └─► ResponseContent (assistant's reply)                         │  │
│  │     └─► FinishReason, RequestParams                                 │  │
│  │  4. Save to PostgreSQL usage_logs table                             │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 9: RESPONSE TRANSFORMATION                                          │
│  ─────────────────────────────────────────────────────────────────────────│
│  CreateChatCompletionCommandHandler.cs:105-127                             │
│  BaseApiController.cs:14-35                                                │
│                                                                            │
│  ChatCompletionResult → ChatCompletionResponseDto:                         │
│  {                                                                         │
│    "id": "chatcmpl-xxx",                                                   │
│    "object": "chat.completion",                                            │
│    "created": 1735123456,                                                  │
│    "model": "gemini-2.0-flash-exp",                                        │
│    "choices": [{                                                           │
│      "index": 0,                                                           │
│      "message": { "role": "assistant", "content": "..." },                 │
│      "finish_reason": "stop"                                               │
│    }],                                                                     │
│    "usage": {                                                              │
│      "prompt_tokens": 150,                                                 │
│      "completion_tokens": 200,                                             │
│      "total_tokens": 350                                                   │
│    }                                                                       │
│  }                                                                         │
│                                                                            │
│  HandleResult() maps Result<T> → HTTP status:                              │
│  └─► Success → 200 OK                                                      │
│  └─► 502 → Bad Gateway (provider errors)                                   │
│  └─► 503 → Service Unavailable (model not found)                           │
│  └─► 400 → Bad Request (validation errors)                                 │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## Key Components Deep Dive

### 1. Authentication Flow (`ApiKeyAuthenticationMiddleware.cs`)

```
X-Api-Key header
       │
       ├─► Match ADMIN_API_KEY? → admin claims
       │
       └─► ProjectCacheService.ValidateApiKey()
              │
              └─► For each cached project:
                     BCrypt.Verify(apiKey, project.ApiKeyHash)
                     └─► Match found → project claims (id, name, rate_limit)
```

### 2. Provider Ranking Algorithm (`ProviderBenchmarkCache.cs`)

```
Score = (SuccessRate × 100) - (AvgResponseTimeMs / 100)

Example:
  Gemini:  95% success, 500ms avg → 95 - 5 = 90
  Groq:    90% success, 200ms avg → 90 - 2 = 88
  OpenAI:  99% success, 800ms avg → 99 - 8 = 91  ← Winner

Providers with <30% success → Score = MIN_VALUE (deprioritized)
```

### 3. Fallback Strategy (`ProviderOrchestrator.cs`)

```
model="free" request:

  ┌───────────────────────────────────────────────────────────┐
  │ Try #1: Gemini (highest ranked)                           │
  │   └─► 429 Rate Limited → SKIP immediately                 │
  │                                                           │
  │ Try #2: Groq (next in rank)                               │
  │   └─► 500 Server Error → RETRY (500ms, 1000ms delays)     │
  │   └─► Still failing → move to next                        │
  │                                                           │
  │ Try #3: OpenAI                                            │
  │   └─► SUCCESS → Return result, update benchmark           │
  │                                                           │
  │ ALL FAILED:                                               │
  │   └─► Fallback to OpenRouter (paid) as last resort        │
  └───────────────────────────────────────────────────────────┘
```

### 4. Supported Providers

| Provider | Type | API Endpoint | Default Model |
|----------|------|--------------|---------------|
| Gemini | Free | `generativelanguage.googleapis.com/v1beta` | `gemini-2.0-flash-exp` |
| Groq | Free | `api.groq.com/openai/v1` | Provider default |
| OpenAI | Free* | `api.openai.com/v1` | `gpt-4o-mini` |
| Mistral | Free | `api.mistral.ai/v1` | Provider default |
| Cohere | Free | `api.cohere.com` | Provider default |
| HuggingFace | Free | `api-inference.huggingface.co` | Provider default |
| OpenRouter | Paid | `openrouter.ai/api/v1` | Selected cheapest |

*OpenAI marked as "free" in this context means it's tried before paid OpenRouter fallback

---

## Data Flow Summary

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         COMPLETE DATA FLOW                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  HTTP Request                                                           │
│       │                                                                 │
│       ▼                                                                 │
│  ChatCompletionRequestDto (JSON body)                                   │
│       │                                                                 │
│       ▼                                                                 │
│  CreateChatCompletionCommand (MediatR command)                          │
│       │                                                                 │
│       ▼                                                                 │
│  ChatMessage[] + ChatCompletionOptions (domain entities)                │
│       │                                                                 │
│       ├──────────────────────────────────────┐                          │
│       │                                      │                          │
│       ▼                                      ▼                          │
│  GeminiRequest / OpenAiRequest        OpenRouterChatRequest             │
│  (provider-specific DTOs)             (for paid/specific)               │
│       │                                      │                          │
│       ▼                                      ▼                          │
│  GeminiResponse / OpenAiResponse      OpenRouterChatResponse            │
│       │                                      │                          │
│       └──────────────────┬───────────────────┘                          │
│                          ▼                                              │
│                 ChatCompletionResult (unified)                          │
│                          │                                              │
│                          ▼                                              │
│                 ChatCompletionResponseDto                               │
│                          │                                              │
│                          ▼                                              │
│                    HTTP Response (JSON)                                 │
│                                                                         │
│  ─────────────────── PARALLEL ──────────────────                        │
│                                                                         │
│                 UsageLog → PostgreSQL                                   │
│                          │                                              │
│                          ▼                                              │
│              usage_logs table (full audit)                              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Key Architectural Patterns

1. **CQRS** - Commands (mutations) and Queries (reads) are separate
2. **MediatR Pipeline** - Validation → Handler → Response
3. **Strategy Pattern** - Multiple providers implement `IAiProvider`
4. **Circuit Breaker-like** - Benchmark scores deprioritize failing providers
5. **Fire-and-Forget** - Usage logging doesn't block response
6. **Claims-based Auth** - Project context flows through claims
7. **Result Pattern** - `Result<T>` for explicit success/failure handling
