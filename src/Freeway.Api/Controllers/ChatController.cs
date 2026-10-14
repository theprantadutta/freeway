using Freeway.Api.Attributes;
using Freeway.Api.Streaming;
using Freeway.Application.DTOs;
using Freeway.Application.Features.Chat.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

public class ChatController : BaseApiController
{
    /// <summary>
    /// OpenAI-compatible chat completions. Served at both paths so a client can use
    /// either the gateway root or ".../v1" as its base URL; OpenAI SDKs are usually
    /// configured with the latter.
    /// </summary>
    [HttpPost("/chat/completions")]
    [HttpPost("/v1/chat/completions")]
    [RequireProject]
    public async Task<ActionResult> CreateChatCompletion([FromBody] ChatCompletionRequestDto request)
    {
        var projectId = Guid.Parse(User.FindFirst("project_id")!.Value);

        var command = new CreateChatCompletionCommand(
            ProjectId: projectId,
            Model: request.Model,
            Messages: request.Messages,
            Temperature: request.Temperature,
            MaxTokens: request.MaxTokens ?? request.MaxCompletionTokens,
            TopP: request.TopP,
            FrequencyPenalty: request.FrequencyPenalty,
            PresencePenalty: request.PresencePenalty,
            Stop: request.Stop,
            Stream: request.Stream,
            UseLocalOnly: request.UseLocalOnly,
            Reasoning: request.Reasoning,
            N: request.N
        );

        if (!request.Stream)
        {
            var result = await Mediator.Send(command);
            return HandleResult(result);
        }

        // Until the first token, a streaming request fails exactly like any other:
        // same fallback, same status codes, an ordinary JSON error body.
        var opened = await Mediator.Send(new StreamChatCompletionCommand(command), HttpContext.RequestAborted);
        if (!opened.IsSuccess)
        {
            return HandleResult(opened);
        }

        await using var session = opened.Value!;
        await ChatCompletionSseWriter.WriteAsync(
            HttpContext, session, includeUsage: request.StreamOptions?.IncludeUsage == true);

        return new EmptyResult();
    }
}
