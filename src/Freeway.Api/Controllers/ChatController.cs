using Freeway.Api.Attributes;
using Freeway.Application.DTOs;
using Freeway.Application.Features.Chat.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

public class ChatController : BaseApiController
{
    [HttpPost("/chat/completions")]
    [RequireProject]
    public async Task<ActionResult> CreateChatCompletion([FromBody] ChatCompletionRequestDto request)
    {
        var projectId = Guid.Parse(User.FindFirst("project_id")!.Value);

        var command = new CreateChatCompletionCommand(
            ProjectId: projectId,
            Model: request.Model,
            Messages: request.Messages,
            Temperature: request.Temperature,
            MaxTokens: request.MaxTokens,
            TopP: request.TopP,
            FrequencyPenalty: request.FrequencyPenalty,
            PresencePenalty: request.PresencePenalty,
            Stop: request.Stop,
            Stream: request.Stream
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
