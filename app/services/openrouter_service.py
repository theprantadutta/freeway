"""OpenRouter API client service."""

import logging
from typing import List, Tuple

import httpx

from app.config import settings
from app.models.openrouter import ModelsResponse, OpenRouterModel

logger = logging.getLogger(__name__)


class OpenRouterService:
    """Service for interacting with OpenRouter API."""

    def __init__(self):
        self.base_url = settings.OPENROUTER_BASE_URL
        self.timeout = settings.REQUEST_TIMEOUT_SECONDS

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


# Singleton instance
openrouter_service = OpenRouterService()
