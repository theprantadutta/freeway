"""Thread-safe in-memory storage for health check results."""

from collections import deque
from datetime import datetime, timezone
from threading import RLock
from typing import Dict, List, Optional

from app.config import settings
from app.models.health_check import HealthCheckResult, HealthStatus, ModelHealthStats
from app.models.openrouter import OpenRouterModel


class MemoryStore:
    """
    Thread-safe in-memory storage for model data and health results.
    Uses deque for bounded history per model.
    """

    def __init__(self, history_size: Optional[int] = None):
        self._lock = RLock()
        self._history_size = history_size or settings.HISTORY_SIZE

        # Store free models: model_id -> OpenRouterModel
        self._models: Dict[str, OpenRouterModel] = {}

        # Store health check history: model_id -> deque of HealthCheckResult
        self._health_history: Dict[str, deque] = {}

        # Metadata
        self._last_models_fetch: Optional[datetime] = None
        self._last_health_check: Optional[datetime] = None

    def update_models(self, models: List[OpenRouterModel]) -> None:
        """Update the list of monitored free models."""
        with self._lock:
            self._models = {m.id: m for m in models}
            self._last_models_fetch = datetime.now(timezone.utc)

            # Initialize health history for new models
            for model_id in self._models:
                if model_id not in self._health_history:
                    self._health_history[model_id] = deque(maxlen=self._history_size)

            # Remove history for models no longer in list
            obsolete = set(self._health_history.keys()) - set(self._models.keys())
            for model_id in obsolete:
                del self._health_history[model_id]

    def add_health_result(self, model_id: str, result: HealthCheckResult) -> None:
        """Add a health check result for a model."""
        with self._lock:
            if model_id in self._health_history:
                self._health_history[model_id].append(result)
                self._last_health_check = datetime.now(timezone.utc)

    def get_model_stats(self, model_id: str) -> Optional[ModelHealthStats]:
        """Calculate health statistics for a model."""
        with self._lock:
            if model_id not in self._models:
                return None

            model = self._models[model_id]
            history = list(self._health_history.get(model_id, []))

            if not history:
                return ModelHealthStats(
                    model_id=model_id,
                    model_name=model.name,
                    context_length=model.context_length,
                    total_checks=0,
                    successful_checks=0,
                    availability_score=0.0,
                    avg_response_time_ms=None,
                    last_check=None,
                    last_status=None,
                )

            successful = [r for r in history if r.status == HealthStatus.SUCCESS]
            response_times = [
                r.response_time_ms for r in successful if r.response_time_ms is not None
            ]

            return ModelHealthStats(
                model_id=model_id,
                model_name=model.name,
                context_length=model.context_length,
                total_checks=len(history),
                successful_checks=len(successful),
                availability_score=len(successful) / len(history),
                avg_response_time_ms=(
                    sum(response_times) / len(response_times) if response_times else None
                ),
                last_check=history[-1].timestamp,
                last_status=history[-1].status,
            )

    def get_all_models(self) -> List[OpenRouterModel]:
        """Get all monitored free models."""
        with self._lock:
            return list(self._models.values())

    def get_all_model_ids(self) -> List[str]:
        """Get all monitored model IDs."""
        with self._lock:
            return list(self._models.keys())

    @property
    def last_health_check(self) -> Optional[datetime]:
        """Get timestamp of last health check."""
        with self._lock:
            return self._last_health_check

    @property
    def last_models_fetch(self) -> Optional[datetime]:
        """Get timestamp of last models fetch."""
        with self._lock:
            return self._last_models_fetch

    @property
    def model_count(self) -> int:
        """Get count of monitored models."""
        with self._lock:
            return len(self._models)

    def remove_model(self, model_id: str) -> bool:
        """Remove a model from tracking. Returns True if removed."""
        with self._lock:
            if model_id in self._models:
                del self._models[model_id]
                if model_id in self._health_history:
                    del self._health_history[model_id]
                return True
            return False

    def has_model(self, model_id: str) -> bool:
        """Check if a model is being tracked."""
        with self._lock:
            return model_id in self._models


# Global singleton instance
memory_store = MemoryStore()
