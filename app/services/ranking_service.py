"""Model ranking service for selecting best free and cheapest paid models."""

import logging
from typing import List, Optional

from app.models.openrouter import OpenRouterModel
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)


class RankingService:
    """
    Service for ranking and selecting models.

    - Free models: Ranked by context length (larger is better)
    - Paid models: Ranked by price (cheaper is better)
    """

    def get_best_free_model(self) -> Optional[OpenRouterModel]:
        """
        Get the best free model based on context length.
        Larger context length = better model.
        """
        free_models = memory_store.get_all_free_models()

        if not free_models:
            return None

        # Sort by context length descending (largest first)
        sorted_models = sorted(
            free_models,
            key=lambda m: m.context_length or 0,
            reverse=True,
        )

        return sorted_models[0]

    def get_cheapest_paid_model(self) -> Optional[OpenRouterModel]:
        """
        Get the cheapest paid model based on pricing.
        Lower total cost (prompt + completion) = better.
        """
        paid_models = memory_store.get_all_paid_models()

        if not paid_models:
            return None

        def get_total_cost(model: OpenRouterModel) -> float:
            """Calculate total cost per token (prompt + completion)."""
            try:
                prompt_cost = float(model.pricing.prompt)
                completion_cost = float(model.pricing.completion)
                return prompt_cost + completion_cost
            except (ValueError, TypeError):
                # If pricing can't be parsed, treat as very expensive
                return float("inf")

        # Sort by total cost ascending (cheapest first)
        sorted_models = sorted(paid_models, key=get_total_cost)

        # Filter out models with infinite cost (unparseable pricing)
        valid_models = [m for m in sorted_models if get_total_cost(m) != float("inf")]

        return valid_models[0] if valid_models else sorted_models[0]

    def get_ranked_free_models(self) -> List[OpenRouterModel]:
        """Get all free models ranked by context length (largest first)."""
        free_models = memory_store.get_all_free_models()
        return sorted(
            free_models,
            key=lambda m: m.context_length or 0,
            reverse=True,
        )

    def get_ranked_paid_models(self) -> List[OpenRouterModel]:
        """Get all paid models ranked by price (cheapest first)."""
        paid_models = memory_store.get_all_paid_models()

        def get_total_cost(model: OpenRouterModel) -> float:
            try:
                prompt_cost = float(model.pricing.prompt)
                completion_cost = float(model.pricing.completion)
                return prompt_cost + completion_cost
            except (ValueError, TypeError):
                return float("inf")

        return sorted(paid_models, key=get_total_cost)

    def select_best_free_model(self) -> Optional[str]:
        """
        Select and store the best free model.
        Called on startup and by scheduler.
        Returns the model ID if selected.
        """
        best = self.get_best_free_model()
        if best:
            memory_store.set_selected_free_model(best.id)
            logger.info(f"Selected best free model: {best.id} (context: {best.context_length})")
            return best.id
        return None

    def select_cheapest_paid_model(self) -> Optional[str]:
        """
        Select and store the cheapest paid model.
        Only called on startup (not auto-updated).
        Returns the model ID if selected.
        """
        cheapest = self.get_cheapest_paid_model()
        if cheapest:
            memory_store.set_selected_paid_model(cheapest.id)
            prompt_cost = cheapest.pricing.prompt
            completion_cost = cheapest.pricing.completion
            logger.info(f"Selected cheapest paid model: {cheapest.id} (prompt: {prompt_cost}, completion: {completion_cost})")
            return cheapest.id
        return None


# Singleton instance
ranking_service = RankingService()
