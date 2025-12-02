"""Model ranking service with scoring algorithm."""

import logging
from typing import List, Optional, Tuple

from app.models.health_check import ModelHealthStats
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

# Scoring weights (higher = more important)
WEIGHT_AVAILABILITY = 50  # Availability is most important
WEIGHT_SPEED = 30  # Response time matters
WEIGHT_CONTEXT = 20  # Context length is a bonus

# Thresholds
MAX_LATENCY_MS = 30000  # 30 seconds - anything slower scores 0 for speed
MIN_CONTEXT_LENGTH = 4096  # Minimum useful context
MAX_CONTEXT_LENGTH = 200000  # Maximum for scoring (after this, no extra points)


class RankingService:
    """
    Service for ranking models using a weighted scoring algorithm.

    Scoring formula:
    - Availability (50%): Direct percentage (0-100%)
    - Speed (30%): Inverse of latency, normalized
    - Context (20%): Normalized context length bonus

    Total score = weighted sum, higher is better.
    """

    def calculate_score(self, stats: ModelHealthStats) -> Tuple[float, dict]:
        """
        Calculate a composite score for a model.
        Returns (score, breakdown_dict).
        """
        breakdown = {}

        # Availability score (0-50 points)
        availability_score = stats.availability_score * WEIGHT_AVAILABILITY
        breakdown["availability"] = round(availability_score, 2)

        # Speed score (0-30 points) - lower latency = higher score
        if stats.avg_response_time_ms and stats.avg_response_time_ms > 0:
            # Normalize: fast (500ms) = 30 points, slow (30s) = 0 points
            latency_normalized = max(0, 1 - (stats.avg_response_time_ms / MAX_LATENCY_MS))
            speed_score = latency_normalized * WEIGHT_SPEED
        else:
            speed_score = 0  # No data = no speed score
        breakdown["speed"] = round(speed_score, 2)

        # Context length score (0-20 points)
        context = stats.context_length or MIN_CONTEXT_LENGTH
        if context >= MIN_CONTEXT_LENGTH:
            # Normalize between min and max
            context_normalized = min(1, (context - MIN_CONTEXT_LENGTH) / (MAX_CONTEXT_LENGTH - MIN_CONTEXT_LENGTH))
            context_score = context_normalized * WEIGHT_CONTEXT
        else:
            context_score = 0
        breakdown["context"] = round(context_score, 2)

        total_score = availability_score + speed_score + context_score
        breakdown["total"] = round(total_score, 2)

        return total_score, breakdown

    def get_ranked_models(self) -> List[ModelHealthStats]:
        """
        Get all models ranked by composite score (highest first).

        Ranking criteria (weighted):
        1. Availability (50%) - Must be reliable
        2. Speed (30%) - Lower latency is better
        3. Context length (20%) - Larger context is a bonus
        """
        model_ids = memory_store.get_all_model_ids()
        scored_models = []

        for model_id in model_ids:
            stats = memory_store.get_model_stats(model_id)
            if stats:
                score, _ = self.calculate_score(stats)
                scored_models.append((score, stats))

        # Sort by score descending (highest first)
        scored_models.sort(key=lambda x: x[0], reverse=True)

        return [stats for _, stats in scored_models]

    def get_best_model(self) -> Optional[ModelHealthStats]:
        """Get the best model based on ranking score."""
        ranked = self.get_ranked_models()

        # Prefer models with at least some successful checks
        available = [m for m in ranked if m.availability_score > 0]

        if available:
            return available[0]

        # Fallback to any model if none have health data yet
        return ranked[0] if ranked else None

    def get_model_with_score(self, model_id: str) -> Optional[Tuple[ModelHealthStats, float, dict]]:
        """Get a specific model's stats and score breakdown."""
        stats = memory_store.get_model_stats(model_id)
        if stats:
            score, breakdown = self.calculate_score(stats)
            return stats, score, breakdown
        return None


# Singleton instance
ranking_service = RankingService()
