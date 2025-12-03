"""OpenRouter API client service."""

import logging
import time
from typing import Any, Dict, List, Optional, Tuple

import httpx

from app.config import settings
from app.models.openrouter import ModelsResponse, OpenRouterModel

logger = logging.getLogger(__name__)


class OpenRouterService:
    """Service for interacting with OpenRouter API."""

    def __init__(self):
        self.base_url = settings.OPENROUTER_BASE_URL
        self.timeout = settings.REQUEST_TIMEOUT_SECONDS
        self.completion_timeout = settings.COMPLETION_TIMEOUT_SECONDS

    def _get_auth_headers(self) -> Dict[str, str]:
        """Get headers for authenticated API requests."""
        return {
            "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
            "HTTP-Referer": "https://github.com/freeway",
            "X-Title": settings.PROJECT_NAME,
        }

    async def fetch_all_models(self) -> List[OpenRouterModel]:
        """Fetch all models from OpenRouter API (public endpoint, no auth needed)."""
        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.get(
                f"{self.base_url}/models",
                headers={"Content-Type": "application/json"},
            )
            response.raise_for_status()
            data = response.json()
            models_response = ModelsResponse(**data)
            return models_response.data

    async def fetch_and_categorize_models(self) -> Tuple[List[OpenRouterModel], List[OpenRouterModel]]:
        """
        Fetch all models and categorize into free and paid.

        Returns:
            Tuple of (free_models, paid_models)
        """
        all_models = await self.fetch_all_models()

        free_models = []
        paid_models = []

        for model in all_models:
            if model.id.endswith(":free"):
                free_models.append(model)
            else:
                paid_models.append(model)

        logger.info(f"Fetched {len(all_models)} models: {len(free_models)} free, {len(paid_models)} paid")
        return free_models, paid_models

    async def create_chat_completion(
        self,
        model_id: str,
        messages: List[Dict[str, str]],
        temperature: Optional[float] = None,
        max_tokens: Optional[int] = None,
        top_p: Optional[float] = None,
        frequency_penalty: Optional[float] = None,
        presence_penalty: Optional[float] = None,
        stop: Optional[List[str]] = None,
    ) -> Tuple[Dict[str, Any], int]:
        """
        Create a chat completion via OpenRouter API.

        Args:
            model_id: The OpenRouter model ID to use
            messages: List of message dicts with 'role' and 'content'
            temperature: Sampling temperature (0-2)
            max_tokens: Maximum tokens to generate
            top_p: Nucleus sampling parameter
            frequency_penalty: Frequency penalty (-2 to 2)
            presence_penalty: Presence penalty (-2 to 2)
            stop: Stop sequences

        Returns:
            Tuple of (response_dict, response_time_ms)
        """
        if not settings.OPENROUTER_API_KEY:
            raise ValueError("OPENROUTER_API_KEY is not configured")

        # Build request payload
        payload: Dict[str, Any] = {
            "model": model_id,
            "messages": messages,
        }

        # Add optional parameters if provided
        if temperature is not None:
            payload["temperature"] = temperature
        if max_tokens is not None:
            payload["max_tokens"] = max_tokens
        if top_p is not None:
            payload["top_p"] = top_p
        if frequency_penalty is not None:
            payload["frequency_penalty"] = frequency_penalty
        if presence_penalty is not None:
            payload["presence_penalty"] = presence_penalty
        if stop is not None:
            payload["stop"] = stop

        # Make request and measure time
        start_time = time.perf_counter()

        async with httpx.AsyncClient(timeout=self.completion_timeout) as client:
            response = await client.post(
                f"{self.base_url}/chat/completions",
                headers=self._get_auth_headers(),
                json=payload,
            )

            elapsed_ms = int((time.perf_counter() - start_time) * 1000)

            if response.status_code != 200:
                error_detail = response.text[:500]
                logger.error(f"OpenRouter API error: {response.status_code} - {error_detail}")
                response.raise_for_status()

            response_data = response.json()
            return response_data, elapsed_ms


# Singleton instance
openrouter_service = OpenRouterService()
