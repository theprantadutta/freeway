"""Chat completion API endpoint."""

import logging
import uuid
from decimal import Decimal
from typing import Optional

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException

from app.api.auth import require_project_key
from app.models.openrouter import OpenRouterModel
from app.schemas.chat import (
    ChatChoice,
    ChatCompletionRequest,
    ChatCompletionResponse,
    ChatMessage,
    UsageInfo,
)
from app.services.openrouter_service import openrouter_service
from app.services.usage_service import usage_service
from app.storage.memory_store import memory_store
from app.storage.project_cache import ProjectInfo

logger = logging.getLogger(__name__)

router = APIRouter(tags=["chat"])


def resolve_model(model_param: str) -> tuple[OpenRouterModel, str]:
    """
    Resolve the model parameter to an actual OpenRouter model.

    Args:
        model_param: "free", "paid", or a specific model ID

    Returns:
        Tuple of (OpenRouterModel, model_type)
        model_type is "free" or "paid"

    Raises:
        HTTPException if model not found
    """
    if model_param.lower() == "free":
        model = memory_store.get_selected_free_model()
        if not model:
            raise HTTPException(
                status_code=503,
                detail="No free model available. Service may be initializing.",
            )
        return model, "free"

    elif model_param.lower() == "paid":
        model = memory_store.get_selected_paid_model()
        if not model:
            raise HTTPException(
                status_code=503,
                detail="No paid model available. Service may be initializing.",
            )
        return model, "paid"

    else:
        # Specific model ID - check both free and paid
        model = memory_store.get_free_model(model_param)
        if model:
            return model, "free"

        model = memory_store.get_paid_model(model_param)
        if model:
            return model, "paid"

        raise HTTPException(
            status_code=404,
            detail=f"Model '{model_param}' not found",
        )


@router.post("/chat/completions", response_model=ChatCompletionResponse)
async def create_chat_completion(
    request: ChatCompletionRequest,
    background_tasks: BackgroundTasks,
    project: ProjectInfo = Depends(require_project_key),
):
    """
    Create a chat completion using OpenRouter.

    This endpoint proxies requests to OpenRouter and tracks usage per project.

    **Model Selection:**
    - `model: "free"` - Use the best free model (auto-selected)
    - `model: "paid"` - Use the cheapest paid model (auto-selected)
    - `model: "<model_id>"` - Use a specific model by ID

    **Authentication:**
    Requires a valid project API key in the `X-Api-Key` header.
    """
    # Resolve model
    model, model_type = resolve_model(request.model)
    logger.info(f"Chat completion request from project {project.name}: model={model.id}")

    # Convert messages to dict format
    messages = [{"role": m.role, "content": m.content} for m in request.messages]

    # Generate request ID for tracking
    request_id = str(uuid.uuid4())

    try:
        # Call OpenRouter API
        response_data, response_time_ms = await openrouter_service.create_chat_completion(
            model_id=model.id,
            messages=messages,
            temperature=request.temperature,
            max_tokens=request.max_tokens,
            top_p=request.top_p,
            frequency_penalty=request.frequency_penalty,
            presence_penalty=request.presence_penalty,
            stop=request.stop,
        )

        # Extract usage info
        usage_data = response_data.get("usage", {})
        input_tokens = usage_data.get("prompt_tokens", 0)
        output_tokens = usage_data.get("completion_tokens", 0)

        # Calculate cost
        cost_usd = usage_service.calculate_cost(model, input_tokens, output_tokens)

        # Get pricing for logging
        prompt_cost: Optional[Decimal] = None
        completion_cost: Optional[Decimal] = None
        try:
            prompt_cost = Decimal(model.pricing.prompt)
            completion_cost = Decimal(model.pricing.completion)
        except (ValueError, TypeError):
            pass

        # Log usage in background
        background_tasks.add_task(
            usage_service.log_usage,
            project_id=project.id,
            model_id=model.id,
            model_type=model_type,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            response_time_ms=response_time_ms,
            cost_usd=cost_usd,
            prompt_cost_per_token=prompt_cost,
            completion_cost_per_token=completion_cost,
            success=True,
            request_id=request_id,
        )

        # Build response
        choices = []
        for i, choice in enumerate(response_data.get("choices", [])):
            message_data = choice.get("message", {})
            choices.append(
                ChatChoice(
                    index=i,
                    message=ChatMessage(
                        role=message_data.get("role", "assistant"),
                        content=message_data.get("content", ""),
                    ),
                    finish_reason=choice.get("finish_reason"),
                )
            )

        return ChatCompletionResponse(
            id=response_data.get("id", f"chatcmpl-{uuid.uuid4().hex[:12]}"),
            created=response_data.get("created", 0),
            model=model.id,
            choices=choices,
            usage=UsageInfo(
                prompt_tokens=input_tokens,
                completion_tokens=output_tokens,
                total_tokens=input_tokens + output_tokens,
            ),
        )

    except HTTPException:
        raise
    except Exception as e:
        error_message = str(e)[:500]
        logger.error(f"Chat completion failed: {error_message}")

        # Log failed request in background
        background_tasks.add_task(
            usage_service.log_usage,
            project_id=project.id,
            model_id=model.id,
            model_type=model_type,
            input_tokens=0,
            output_tokens=0,
            response_time_ms=0,
            cost_usd=Decimal("0"),
            success=False,
            error_message=error_message,
            request_id=request_id,
        )

        raise HTTPException(
            status_code=502,
            detail=f"Failed to complete chat request: {error_message}",
        )
