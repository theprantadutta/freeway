"""Health check orchestration service."""

import asyncio
import logging
from typing import Dict

from app.config import settings
from app.models.health_check import HealthCheckResult, HealthStatus
from app.services.openrouter_service import openrouter_service
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

# Rate limit retry settings
RATE_LIMIT_WAIT_SECONDS = 180  # 3 minutes wait on 429
MAX_RETRIES = 3  # Retry up to 3 times before giving up


class HealthCheckService:
    """Service for orchestrating model health checks."""

    async def refresh_free_models(self) -> int:
        """Fetch and update the list of free models."""
        try:
            free_models = await openrouter_service.fetch_free_models()
            memory_store.update_models(free_models)
            logger.info(f"Updated free models list: {len(free_models)} models")
            return len(free_models)
        except Exception as e:
            logger.error(f"Failed to refresh free models: {e}")
            raise

    async def check_single_model(self, model_id: str) -> HealthCheckResult:
        """Run health check for a single model with retry on rate limit."""
        result = await openrouter_service.test_model(model_id)

        # If rate limited (429), retry with exponential backoff
        retry_count = 0
        while self._is_rate_limited(result) and retry_count < MAX_RETRIES:
            retry_count += 1
            wait_time = RATE_LIMIT_WAIT_SECONDS * retry_count  # 3min, 6min, 9min
            logger.warning(f"Rate limited on {model_id}, attempt {retry_count}/{MAX_RETRIES}, waiting {wait_time}s...")
            try:
                await asyncio.sleep(wait_time)
                result = await openrouter_service.test_model(model_id)
            except asyncio.CancelledError:
                logger.info("Retry interrupted (shutdown)")
                raise

        memory_store.add_health_result(model_id, result)
        return result

    def _is_rate_limited(self, result: HealthCheckResult) -> bool:
        """Check if result indicates a 429 rate limit error."""
        if result.status == HealthStatus.FAILURE and result.error_message:
            return "429" in result.error_message
        return False

    async def check_all_models(self) -> Dict[str, int]:
        """
        Run health checks for all monitored models sequentially.
        Handles rate limits with automatic retry after wait.
        """
        model_ids = memory_store.get_all_model_ids()

        if not model_ids:
            logger.warning("No models to check - refreshing model list first")
            await self.refresh_free_models()
            model_ids = memory_store.get_all_model_ids()

        logger.info(f"Starting health checks for {len(model_ids)} models")

        results = []
        for i, model_id in enumerate(model_ids):
            try:
                result = await self.check_single_model(model_id)
                results.append(result)
                status_str = result.status.value
                if result.status == HealthStatus.SUCCESS:
                    logger.info(f"[{i + 1}/{len(model_ids)}] ✓ {model_id} ({result.response_time_ms:.0f}ms)")
                else:
                    logger.info(f"[{i + 1}/{len(model_ids)}] ✗ {model_id} - {status_str}")
            except Exception as e:
                logger.error(f"[{i + 1}/{len(model_ids)}] Error checking {model_id}: {e}")
                results.append(e)

            # Add delay between requests (only if not last model)
            if i < len(model_ids) - 1:
                try:
                    await asyncio.sleep(settings.CHECK_DELAY_SECONDS)
                except asyncio.CancelledError:
                    logger.info("Health check interrupted (shutdown)")
                    break

        success_count = sum(
            1
            for r in results
            if isinstance(r, HealthCheckResult) and r.status == HealthStatus.SUCCESS
        )
        failure_count = len(results) - success_count

        logger.info(f"Health check complete: {success_count}/{len(results)} succeeded")

        return {
            "total": len(results),
            "success": success_count,
            "failure": failure_count,
        }


# Singleton instance
health_check_service = HealthCheckService()
