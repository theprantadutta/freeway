"""Usage tracking service."""

import logging
import uuid
from decimal import Decimal
from typing import Optional

from app.db.connection import async_session_maker
from app.db.repositories.usage_repo import UsageRepository
from app.models.openrouter import OpenRouterModel

logger = logging.getLogger(__name__)


class UsageService:
    """Service for tracking API usage."""

    @staticmethod
    def calculate_cost(
        model: OpenRouterModel,
        input_tokens: int,
        output_tokens: int,
    ) -> Decimal:
        """
        Calculate the cost of a completion based on model pricing.

        Args:
            model: The OpenRouter model with pricing info
            input_tokens: Number of input/prompt tokens
            output_tokens: Number of output/completion tokens

        Returns:
            Total cost in USD as Decimal
        """
        try:
            prompt_price = Decimal(model.pricing.prompt)
            completion_price = Decimal(model.pricing.completion)

            prompt_cost = prompt_price * input_tokens
            completion_cost = completion_price * output_tokens

            return prompt_cost + completion_cost
        except (ValueError, TypeError, AttributeError):
            return Decimal("0")

    @staticmethod
    async def log_usage(
        project_id: str,
        model_id: str,
        model_type: str,
        input_tokens: int,
        output_tokens: int,
        response_time_ms: int,
        cost_usd: Decimal,
        prompt_cost_per_token: Optional[Decimal] = None,
        completion_cost_per_token: Optional[Decimal] = None,
        success: bool = True,
        error_message: Optional[str] = None,
        request_id: Optional[str] = None,
        provider: Optional[str] = None,
        request_messages: Optional[list] = None,
        response_content: Optional[str] = None,
        finish_reason: Optional[str] = None,
        request_params: Optional[dict] = None,
    ) -> None:
        """
        Log a usage entry to the database.

        This is typically called as a background task to avoid blocking the response.
        """
        try:
            async with async_session_maker() as session:
                repo = UsageRepository(session)
                await repo.create(
                    project_id=uuid.UUID(project_id),
                    model_id=model_id,
                    model_type=model_type,
                    input_tokens=input_tokens,
                    output_tokens=output_tokens,
                    response_time_ms=response_time_ms,
                    cost_usd=cost_usd,
                    prompt_cost_per_token=prompt_cost_per_token,
                    completion_cost_per_token=completion_cost_per_token,
                    success=success,
                    error_message=error_message,
                    request_id=request_id,
                    provider=provider,
                    request_messages=request_messages,
                    response_content=response_content,
                    finish_reason=finish_reason,
                    request_params=request_params,
                )
                logger.debug(f"Logged usage for project {project_id}: {model_id}, {input_tokens + output_tokens} tokens")
        except Exception as e:
            logger.error(f"Failed to log usage: {e}")


# Singleton instance
usage_service = UsageService()
