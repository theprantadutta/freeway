"""OpenRouter API client service."""

import logging
from datetime import datetime, timezone
from typing import List

import httpx

from app.config import settings
from app.models.health_check import HealthCheckResult, HealthStatus
from app.models.openrouter import ModelsResponse, OpenRouterModel

logger = logging.getLogger(__name__)


class OpenRouterService:
    """Service for interacting with OpenRouter API."""

    def __init__(self):
        self.base_url = settings.OPENROUTER_BASE_URL
        self.timeout = settings.REQUEST_TIMEOUT_SECONDS

    def _get_auth_headers(self) -> dict:
        """Get headers for authenticated API requests (chat completions)."""
        return {
            "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
            "HTTP-Referer": "https://github.com/freeway",
            "X-Title": settings.PROJECT_NAME,
        }

    async def fetch_models(self) -> List[OpenRouterModel]:
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

    async def fetch_free_models(self) -> List[OpenRouterModel]:
        """Fetch only free models from OpenRouter API (models with :free suffix)."""
        all_models = await self.fetch_models()
        # Free models have ":free" suffix in their ID
        free_models = [m for m in all_models if m.id.endswith(":free")]
        logger.info(f"Found {len(free_models)} free models (with :free suffix) out of {len(all_models)} total")
        return free_models

    async def get_account_info(self) -> dict:
        """Get account info including credits remaining."""
        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.get(
                f"{self.base_url}/key",
                headers=self._get_auth_headers(),
            )
            response.raise_for_status()
            return response.json()

    async def test_model(self, model_id: str) -> HealthCheckResult:
        """
        Send a test prompt to a model and measure response.
        Returns HealthCheckResult with timing and status.
        """
        start_time = datetime.now(timezone.utc)

        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.post(
                    f"{self.base_url}/chat/completions",
                    headers=self._get_auth_headers(),
                    json={
                        "model": model_id,
                        "messages": [{"role": "user", "content": settings.TEST_PROMPT}],
                        "max_tokens": 10,  # Minimal tokens to reduce cost
                    },
                )

                end_time = datetime.now(timezone.utc)
                response_time_ms = (end_time - start_time).total_seconds() * 1000

                if response.status_code == 200:
                    return HealthCheckResult(
                        timestamp=start_time,
                        status=HealthStatus.SUCCESS,
                        response_time_ms=response_time_ms,
                    )
                else:
                    return HealthCheckResult(
                        timestamp=start_time,
                        status=HealthStatus.FAILURE,
                        error_message=f"HTTP {response.status_code}: {response.text[:200]}",
                    )

        except httpx.TimeoutException:
            return HealthCheckResult(
                timestamp=start_time,
                status=HealthStatus.TIMEOUT,
                error_message="Request timed out",
            )
        except Exception as e:
            return HealthCheckResult(
                timestamp=start_time,
                status=HealthStatus.FAILURE,
                error_message=str(e)[:200],
            )


# Singleton instance
openrouter_service = OpenRouterService()
