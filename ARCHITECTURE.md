# Freeway AI Gateway - Complete Request Flow Analysis

## Project Overview

**Freeway** is a sophisticated **multi-provider AI gateway** built with .NET 10 using Clean Architecture (CQRS pattern with MediatR). It acts as an OpenAI-compatible proxy that intelligently routes requests across 8 AI providers with smart fallback, benchmarking, and usage tracking. Replies come back as one JSON body or, with `"stream": true`, as OpenAI-format server-sent events; both go through the same routing (see [Streaming Request Flow](#streaming-request-flow)).

---

## Complete Request Flow for `POST /chat/completions`

Served at `/chat/completions` and `/v1/chat/completions`. This is the non-streaming
path; a streaming request shares phases 1-5 and differs from there, as described in
[Streaming Request Flow](#streaming-request-flow).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              REQUEST LIFECYCLE                              │
└─────────────────────────────────────────────────────────────────────────────┘

    Client Request (X-Api-Key or Authorization: Bearer <key> + JSON body)
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 1: MIDDLEWARE PIPELINE                                              │
│  ─────────────────────────────────────────────────────────────────────────│
│  GlobalExceptionHandlerMiddleware.cs:16-27                                 │
│  └─► Wraps entire pipeline in try/catch                                    │
│  └─► Catches unhandled exceptions → JSON error response                    │
│                                                                            │
│  ApiKeyAuthenticationMiddleware.cs                                         │
│  └─► Bearer token: valid JWT → user claims; otherwise treated as a key     │
│      (how OpenAI SDKs send theirs)                                         │
│  └─► Otherwise extracts X-Api-Key header (wins if both are sent)           │
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
│  ChatController.cs                                                         │
│                                                                            │
│  [HttpPost("/chat/completions")] + [HttpPost("/v1/chat/completions")]      │
│  [RequireProject]  ← Authorization attribute (requires project key)        │
│                                                                            │
│  1. Extract projectId from User claims                                     │
│  2. Create CreateChatCompletionCommand with all params                     │
│     (max_completion_tokens fills in for an absent max_tokens)              │
│  3. stream=false: await Mediator.Send(command), HandleResult()             │
│     stream=true:  Send(StreamChatCompletionCommand) → SSE writer           │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 3: VALIDATION PIPELINE (MediatR Behavior)                           │
│  ─────────────────────────────────────────────────────────────────────────│
│  ValidationBehavior.cs                                                     │
│                                                                            │
│  CreateChatCompletionCommandValidator.cs validates:                        │
│  └─► ProjectId: NotEmpty                                                   │
│  └─► Model: NotEmpty                                                       │
│  └─► Messages: NotEmpty, Count > 0                                         │
│  └─► Each message: Role + Content required                                 │
│  └─► Temperature: 0-2 range (if provided)                                  │
│  └─► MaxTokens: > 0 (if provided)                                          │
│  └─► N: must be 1 (if provided)                                            │
│                                                                            │
│  Throws ValidationException if failures → 400 Bad Request                  │
│  (GlobalExceptionHandlerMiddleware maps it; it used to surface as a 500)   │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 4: COMMAND HANDLER (Core Business Logic)                            │
│  ─────────────────────────────────────────────────────────────────────────│
│  CreateChatCompletionCommandHandler.cs → ChatRouter.RouteAsync<T>()        │
│                                                                            │
│  The routing lives in ChatRouter and is generic over what an attempt       │
│  returns: a ChatCompletionResult here, a ChatCompletionStream for the      │
│  streaming handler. Each handler only supplies the calls to make.          │
│                                                                            │
│  STEP 1: Convert DTOs to Domain Entities (ChatRouter.ToMessages/ToOptions) │
│  └─► ChatMessageDto[] → ChatMessage[]                                      │
│  └─► Create ChatCompletionOptions with all params                          │
│                                                                            │
│  STEP 2: Route Based on Model Type                                         │
│  ┌────────────────────────────────────────────────────────────────────┐   │
│  │  IF model == "free":                                               │   │
│  │     → Use ProviderOrchestrator.ExecuteWithFallbackAsync()          │   │
│  │     → Smart multi-provider routing with benchmarks                 │   │
│  │                                                                    │   │
│  │  IF model == "paid[:tier]" / "image" / specific model ID:          │   │
│  │     → ResolveModel() to find model + tier in caches                │   │
│  │     → Call OpenRouterService directly                              │   │
│  └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│  STEP 3: Fire-and-Forget Usage Logging                                     │
│  └─► ChatUsageLogger.LogInBackground(...)                                  │
│  └─► Creates new DI scope, saves UsageLog to PostgreSQL                    │
│                                                                            │
│  STEP 4: Return Result                                                     │
│  └─► Success: 200 OK with ChatCompletionResponseDto                        │
│  └─► Failure: 502 Bad Gateway with error message                           │
└───────────────────────────────────────────────────────────────────────────┘
                    │
         ┌─────────┴──────────┐
         │                    │
    model="free"      model="paid[:tier]"/specific
         │                    │
         ▼                    ▼
┌────────────────────┐ ┌────────────────────────────────────────────────────┐
│ PHASE 5A:          │ │ PHASE 5B: MODEL RESOLUTION                          │
│ ORCHESTRATOR       │ │ ──────────────────────────────────────────────────│
│                    │ │ ChatRouter.ResolveModel()                          │
│ ProviderOrchestra- │ │                                                     │
│ tor.cs             │ │ Priority order:                                    │
│                    │ │ 1. "free" → GetSelectedFreeModel()                 │
│ 1. Get ranked      │ │ 2. "paid[:tier]" → GetSelectedPaidModel(tier)      │
│    providers from  │ │    (bare "paid" == "paid:low"; bad tier → 400)     │
│    benchmark cache │ │ 3. "image" → GetSelectedImageModel()               │
│                    │ │ 4. Check ModelCacheService (OpenRouter models)     │
│ 2. Filter to FREE  │ │ 5. Check ProviderModelCache (all providers)        │
│    providers only  │ │ 6. OpenRouter format check (contains "/")          │
│                    │ │ 7. Return error if not found                       │
│    providers only  │ │                                                     │
│                    │ │ Then → OpenRouterService.CreateChatCompletionAsync │
│ 3. Try each in     │ └────────────────────────────────────────────────────┘
│    ranked order    │
│    with retries    │
│                    │
│ 4. Then OpenRouter │
│    zero-cost models│
│    then 503; never │
│    a paid model    │
└────────────────────┘
         │
         ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 6: PROVIDER EXECUTION (For "free" model via Orchestrator)           │
│  ─────────────────────────────────────────────────────────────────────────│
│  ProviderOrchestrator.cs (RunAsync<T>, shared by both forms of call)       │
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
│  │  3. TryProviderWithRetryAsync()                                     │  │
│  │     └─► Max 2 retries with delays [500ms, 1000ms]                   │  │
│  │     └─► 429 rate limit → skip to next provider immediately          │  │
│  │     └─► 4xx client error → skip to next provider immediately        │  │
│  │     └─► 5xx server error → retry then next provider                 │  │
│  │  4. On success: update benchmark, return result                     │  │
│  │  5. On failure: add to errors list, try next provider               │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  IF all direct free providers fail:                                        │
│  └─► Try OpenRouter's zero-cost models (FREE_LANE_OPENROUTER_COUNT)        │
│  └─► Each re-checked for a zero price immediately before calling           │
│  IF those fail too:                                                        │
│  └─► 503. Never a paid model                                               │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 7: INDIVIDUAL PROVIDER API CALL                                     │
│  ─────────────────────────────────────────────────────────────────────────│
│  Example: GeminiProvider.cs                                                │
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
│  ChatUsageLogger.cs                                                        │
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
│  │     └─► Streamed, TimeToFirstTokenMs (streams only)                 │  │
│  │  4. Save to PostgreSQL usage_logs table                             │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────┐
│  PHASE 9: RESPONSE TRANSFORMATION                                          │
│  ─────────────────────────────────────────────────────────────────────────│
│  CreateChatCompletionCommandHandler.ToDto()                                │
│  BaseApiController.HandleResult()                                          │
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
│                                                                            │
│  Error bodies: {"detail": "...", "error": {message, type, param, code}}    │
│  ("error" is the object OpenAI SDKs read their message from)               │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## Streaming Request Flow

`"stream": true` takes the same route through the gateway — auth, validation, lane
resolution, local Claude, every fallback chain — and differs only in what an attempt
returns and in how the reply leaves.

```
ChatController (stream=true)
   │
   ▼
StreamChatCompletionCommand ──► validated by the same CreateChatCompletionCommandValidator
   │
   ▼
StreamChatCompletionCommandHandler
   └─► ChatRouter.RouteAsync<ChatCompletionStream>(
          Free:  orchestrator.StreamWithFallbackAsync
          Local: localClaude.StreamAsync
          Paid:  openRouter.StreamChatCompletionAsync)
   │
   │   Every attempt is a ChatCompletionStream, which only counts as a
   │   success once its FIRST DELTA has arrived (ChatCompletionStream.OpenAsync).
   │   A failure before that is an ordinary failed Result with a status code,
   │   so retries, 429 cooldowns, skipping providers, local Claude falling
   │   through to paid, and the final 400/502/503 all behave exactly as for
   │   a non-streaming request.
   │
   ├─► nothing opened ──► JSON error, same status as non-streaming; request logged
   │
   ▼
ChatCompletionStreamSession (open stream + deferred usage logging)
   │
   ▼
ChatCompletionSseWriter (API layer)
   ├─► 200, text/event-stream, response buffering off, flush per event
   ├─► chunk: delta {"role":"assistant","content":""}
   ├─► chunk per content delta  (reasoning deltas keep it alive but are not sent)
   ├─► chunk: delta {}, finish_reason
   ├─► chunk: choices [], usage          (only with stream_options.include_usage)
   └─► data: [DONE]
       or, if the upstream fails mid-stream: data: {"error": {...}} and no [DONE]
   │
   ▼
session disposed ──► upstream connection released ──► ChatUsageLogger
                      (streamed = true, time_to_first_token_ms, billed cost from
                       the final chunk; tokens estimated if the stream was cut short)
```

### The commit point

The gateway's value is falling back, and a stream can only fall back until the caller
has been sent something. `ChatCompletionStream.OpenAsync` therefore reads the source
until its first delta before handing the stream out; anything thrown before then
becomes the stream's failed `Result` (status code included, via
`ChatCompletionStreamException`). Every piece of fallback logic — the orchestrator's
`RunAsync<T>`, `ChatRouter`'s model chain — reads `stream.Result` exactly as it reads
a `ChatCompletionResult`, which is what lets one copy of the routing serve both.

After the commit, failures cannot be routed around. `ReadAllAsync` records them on
`Result` instead of throwing, and the writer sends a final `error` event.

### How each backend streams

| Backend | Implementation |
|---|---|
| OpenRouter (paid tiers, specific models) | `OpenRouterService.StreamChatCompletionAsync` → `OpenAiChunkStream` |
| OpenRouter (free lane's zero-cost rung), Groq, Mistral, OpenAI, HuggingFace | `BaseAiProvider.StreamOpenAiCompatibleAsync` → `OpenAiChunkStream` |
| Gemini | `GeminiProvider` — `streamGenerateContent?alt=sse` |
| Cohere | `CohereProvider` — v1 chat NDJSON events (`stream-start`, `text-generation`, `stream-end`) |
| Local Claude Code | `LocalClaudeService.StreamAsync` ← bridge NDJSON ← `claude --output-format stream-json` |
| Anything else | `BaseAiProvider` default: the finished result replayed as one delta |

`OpenAiChunkStream` folds each chunk's metadata into the live `Result` as it goes —
the upstream's id and model, OpenRouter's `provider`, `usage` (including OpenRouter's
billed `cost` and Groq's `x_groq.usage`) — and raises a mid-stream `error` object as a
failure. `UpstreamStream` sends with `ResponseHeadersRead` and applies
`COMPLETION_TIMEOUT_SECONDS` as an idle timeout reset on every line, so long answers
are never cut off at a fixed total.

### Cancellation

The streaming handler is sent `HttpContext.RequestAborted`, and every upstream call
captures it. A caller hanging up cancels the upstream read, so generation stops; the
writer marks the stream abandoned, and the request is logged as successful with the
note `caller disconnected before the reply finished`, its tokens estimated from the
text produced so far.

---

## Key Components Deep Dive

### 1. Authentication Flow (`ApiKeyAuthenticationMiddleware.cs`)

```
Authorization: Bearer <token>
       │
       ├─► Valid JWT? → user claims (web panel)
       └─► Otherwise the token is used as the API key below
           (OpenAI SDKs send their key this way)

X-Api-Key header (takes precedence over a Bearer key)
       │
       ├─► Match ADMIN_API_KEY? → admin claims
       │
       └─► ProjectCacheService.ValidateApiKey()
              │
              ├─► SHA-256(key) already verified, and its project still in the
              │   current cache under the same hash? → project claims
              ├─► SHA-256(key) already rejected against this cache snapshot? → 401
              └─► Otherwise BCrypt.Verify, projects whose stored prefix matches
                  the key first, then the rest → remember the outcome
                     └─► Match found → project claims (id, name, rate_limit)

  Rotate / deactivate / delete reload the cache, which revokes a remembered
  key at the same moment it always was: the memo only names a project, and
  the project is looked up in the current snapshot on every request.
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
  │ Try #3: Mistral / Cohere / HuggingFace                    │
  │   └─► SUCCESS → Return result, update benchmark           │
  │        cost 0, cost_source "free_tier"                    │
  │                                                           │
  │ ALL DIRECT PROVIDERS FAILED:                              │
  │   └─► OpenRouter zero-cost models, selected first then    │
  │       by rank, up to FREE_LANE_OPENROUTER_COUNT           │
  │       each re-checked for a zero price before calling     │
  │                                                           │
  │ STILL NOTHING:                                            │
  │   └─► 503. Never a paid model: a lane called "free"       │
  │       must not produce a bill                             │
  └───────────────────────────────────────────────────────────┘

  OpenAI is absent from this list on purpose: it bills per token, so it must
  never sit in the free lane's rotation.
```

```
model="paid[:tier]" / "image" request (CreateChatCompletionCommandHandler):

  ┌───────────────────────────────────────────────────────────┐
  │ BuildPaidCandidates(): ordered candidate list             │
  │   = [selected model for tier]                             │
  │     + next PAID_FALLBACK_COUNT from that tier's chain     │
  │   (models on cooldown moved to the back as last resort)   │
  │                                                           │
  │ ExecuteWithModelFallbackAsync(): try each in order        │
  │   Try #1: selected paid/image model                       │
  │     └─► 429 → mark in ModelCooldownCache → try next       │
  │     └─► other error → try next                            │
  │   Try #2..N: next models in the tier chain                │
  │     └─► SUCCESS → return (usage logged vs the model used) │
  │                                                           │
  │ ALL CANDIDATES FAILED → 502 Bad Gateway                   │
  └───────────────────────────────────────────────────────────┘

  A specific model ID (e.g. "anthropic/claude-...") is a single
  attempt with no substitution — only the "paid[:tier]"/"image"
  virtual models fan out to backups.
```

### Paid tier chains (ModelCacheService.BuildTierChains)

Each tier is ordered curated-first, then price-band cheapest-first:

```
  paid:low       no curation → cheapest-first over models < $1/Mtok
                 (identical to what bare "paid" always did)

  paid:moderate  openai/gpt-5-mini → gpt-4.1-mini → gpt-5.4-mini
                 → o4-mini → rest of the $1–$10/Mtok band

  paid:premium   openai/gpt-5.6-sol → sol-pro → gpt-5.6-terra
                 → gpt-5.2 → claude-opus-4.1 → rest of >$10/Mtok
```

Curation wins over the price band, so a curated model stays in its tier
even if upstream changes its price. `:batch` variants are excluded from
every tier — they are priced for async batch submission and are not valid
on a synchronous chat endpoint.

The tier is recorded on each `UsageLog` as `model_tier` (nullable);
`model_type` keeps its original `free`/`paid`/`image` values so existing
analytics grouping and pre-tier rows stay comparable.

Every OpenRouter request also carries provider-routing preferences
(`OpenRouterService.BuildProviderPreferences`): `sort` (default
`throughput`), `allow_fallbacks` (default true) and an optional `ignore`
blocklist. This makes OpenRouter pick the healthiest endpoint and route
around a throttled provider to another one serving the *same* model,
cutting 429s before the client-side model fallback chain is even needed.
Configured via `OPENROUTER_PROVIDER_SORT`, `OPENROUTER_ALLOW_FALLBACKS`,
`OPENROUTER_IGNORE_PROVIDERS`.

### 4. Supported Providers

| Provider | In the free lane? | API Endpoint | Billing |
|----------|-------------------|--------------|---------|
| Gemini | yes | `generativelanguage.googleapis.com/v1beta` | Google's free tier |
| Groq | yes | `api.groq.com/openai/v1` | Groq's free tier |
| Mistral | yes | `api.mistral.ai/v1` | Mistral's free tier |
| Cohere | yes | `api.cohere.com` | Cohere's trial tier |
| HuggingFace | yes | `api-inference.huggingface.co` | HuggingFace's free tier |
| OpenAI | **no** | `api.openai.com/v1` | Billed per token |
| OpenRouter | zero-cost models only | `openrouter.ai/api/v1` | Billed per token |

`IsFreeProvider` decides whether a provider may serve a `model:"free"` request.
OpenAI was previously flagged `true`, which meant a free request could land on a
billed OpenAI call. It is now `false`.

OpenRouter is not a free provider either, but the free lane may use its zero-cost
models as a last rung — each one re-checked for a zero price immediately before it
is called.

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
│  stream=true: provider chunks → ChatCompletionDelta → ChatCompletion-   │
│  Stream (Result filled in as it is read) → ChatCompletionChunkDto       │
│  → text/event-stream                                                    │
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
8. **Commit on first token** - A stream is a success only once its first delta is in, so streaming reuses every fallback rule unchanged
