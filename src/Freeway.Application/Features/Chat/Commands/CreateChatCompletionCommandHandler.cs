using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Freeway.Application.Features.Chat.Commands;

public class CreateChatCompletionCommandHandler : IRequestHandler<CreateChatCompletionCommand, Result<ChatCompletionResponseDto>>
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IModelCacheService _modelCacheService;
    private readonly IAppDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<CreateChatCompletionCommandHandler> _logger;

    public CreateChatCompletionCommandHandler(
        IOpenRouterService openRouterService,
        IModelCacheService modelCacheService,
        IAppDbContext context,
        IDateTimeService dateTimeService,
        ILogger<CreateChatCompletionCommandHandler> logger)
    {
        _openRouterService = openRouterService;
        _modelCacheService = modelCacheService;
        _context = context;
        _dateTimeService = dateTimeService;
        _logger = logger;
    }

    public async Task<Result<ChatCompletionResponseDto>> Handle(CreateChatCompletionCommand request, CancellationToken cancellationToken)
    {
        // Resolve model
        var (modelId, modelType, model) = ResolveModel(request.Model);

        if (modelId == null)
        {
            return Result<ChatCompletionResponseDto>.ServiceUnavailable($"Model '{request.Model}' not available");
        }

        // Convert messages
        var messages = request.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        // Create options
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            Stop = request.Stop,
            Stream = request.Stream
        };

        // Call OpenRouter
        var result = await _openRouterService.CreateChatCompletionAsync(modelId, messages, options, cancellationToken);

        // Log usage asynchronously (fire and forget)
        _ = LogUsageAsync(request, modelId, modelType, model, result);

        if (!result.Success)
        {
            return Result<ChatCompletionResponseDto>.BadGateway(result.ErrorMessage ?? "OpenRouter API error");
        }

        return Result<ChatCompletionResponseDto>.Success(new ChatCompletionResponseDto
        {
            Id = result.Id,
            Object = "chat.completion",
            Created = result.Created,
            Model = result.Model,
            Choices = result.Choices.Select(c => new ChatChoiceDto
            {
                Index = c.Index,
                Message = new ChatMessageDto
                {
                    Role = c.Message.Role,
                    Content = c.Message.Content
                },
                FinishReason = c.FinishReason
            }).ToList(),
            Usage = new UsageDto
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            }
        });
    }

    private (string? modelId, string modelType, CachedModel? model) ResolveModel(string requestedModel)
    {
        // Handle "free" and "paid" keywords
        if (requestedModel.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedFreeModel();
            return (model?.Id, "free", model);
        }

        if (requestedModel.Equals("paid", StringComparison.OrdinalIgnoreCase))
        {
            var model = _modelCacheService.GetSelectedPaidModel();
            return (model?.Id, "paid", model);
        }

        // Look up specific model
        var cachedModel = _modelCacheService.GetModelById(requestedModel);
        if (cachedModel != null)
        {
            return (cachedModel.Id, cachedModel.IsFree ? "free" : "paid", cachedModel);
        }

        // If not in cache, assume it's a valid model ID (pass-through)
        return (requestedModel, "unknown", null);
    }

    private async Task LogUsageAsync(
        CreateChatCompletionCommand request,
        string modelId,
        string modelType,
        CachedModel? model,
        ChatCompletionResult result)
    {
        try
        {
            decimal promptCost = 0;
            decimal completionCost = 0;
            decimal totalCost = 0;

            if (model != null)
            {
                decimal.TryParse(model.PromptPrice, out promptCost);
                decimal.TryParse(model.CompletionPrice, out completionCost);
                totalCost = (result.Usage.PromptTokens * promptCost) + (result.Usage.CompletionTokens * completionCost);
            }

            var usageLog = new UsageLog
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                ModelId = modelId,
                ModelType = modelType,
                InputTokens = result.Usage.PromptTokens,
                OutputTokens = result.Usage.CompletionTokens,
                ResponseTimeMs = result.ResponseTimeMs,
                CostUsd = totalCost,
                PromptCostPerToken = promptCost,
                CompletionCostPerToken = completionCost,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                RequestId = result.Id,
                CreatedAt = _dateTimeService.UtcNow,
                Provider = "openrouter",
                RequestMessages = request.Messages.Select(m => new ChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList(),
                ResponseContent = result.Choices.FirstOrDefault()?.Message.Content,
                FinishReason = result.FinishReason,
                RequestParams = new Dictionary<string, object>
                {
                    ["temperature"] = request.Temperature ?? 0.7,
                    ["max_tokens"] = request.MaxTokens ?? 0
                }
            };

            _context.UsageLogs.Add(usageLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log usage for project {ProjectId}", request.ProjectId);
        }
    }
}
