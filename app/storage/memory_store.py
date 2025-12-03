"""Thread-safe in-memory storage for models."""

from datetime import datetime, timezone
from threading import RLock
from typing import Dict, List, Optional

from app.models.openrouter import OpenRouterModel


class MemoryStore:
    """
    Thread-safe in-memory storage for model data.
    Stores free and paid models separately with selected model tracking.
    """

    def __init__(self):
        self._lock = RLock()

        # Store models by category: model_id -> OpenRouterModel
        self._free_models: Dict[str, OpenRouterModel] = {}
        self._paid_models: Dict[str, OpenRouterModel] = {}

        # Selected models (best free, cheapest paid)
        self._selected_free_model_id: Optional[str] = None
        self._selected_paid_model_id: Optional[str] = None

        # Metadata
        self._last_models_fetch: Optional[datetime] = None

    def update_models(
        self,
        free_models: List[OpenRouterModel],
        paid_models: List[OpenRouterModel],
    ) -> None:
        """Update the list of models."""
        with self._lock:
            self._free_models = {m.id: m for m in free_models}
            self._paid_models = {m.id: m for m in paid_models}
            self._last_models_fetch = datetime.now(timezone.utc)

    def get_all_free_models(self) -> List[OpenRouterModel]:
        """Get all free models."""
        with self._lock:
            return list(self._free_models.values())

    def get_all_paid_models(self) -> List[OpenRouterModel]:
        """Get all paid models."""
        with self._lock:
            return list(self._paid_models.values())

    def get_free_model(self, model_id: str) -> Optional[OpenRouterModel]:
        """Get a specific free model by ID."""
        with self._lock:
            return self._free_models.get(model_id)

    def get_paid_model(self, model_id: str) -> Optional[OpenRouterModel]:
        """Get a specific paid model by ID."""
        with self._lock:
            return self._paid_models.get(model_id)

    # Selected model management
    def set_selected_free_model(self, model_id: str) -> bool:
        """Set the selected free model. Returns True if model exists."""
        with self._lock:
            if model_id in self._free_models:
                self._selected_free_model_id = model_id
                return True
            return False

    def set_selected_paid_model(self, model_id: str) -> bool:
        """Set the selected paid model. Returns True if model exists."""
        with self._lock:
            if model_id in self._paid_models:
                self._selected_paid_model_id = model_id
                return True
            return False

    def get_selected_free_model(self) -> Optional[OpenRouterModel]:
        """Get the currently selected free model."""
        with self._lock:
            if self._selected_free_model_id:
                return self._free_models.get(self._selected_free_model_id)
            return None

    def get_selected_paid_model(self) -> Optional[OpenRouterModel]:
        """Get the currently selected paid model."""
        with self._lock:
            if self._selected_paid_model_id:
                return self._paid_models.get(self._selected_paid_model_id)
            return None

    @property
    def selected_free_model_id(self) -> Optional[str]:
        """Get the selected free model ID."""
        with self._lock:
            return self._selected_free_model_id

    @property
    def selected_paid_model_id(self) -> Optional[str]:
        """Get the selected paid model ID."""
        with self._lock:
            return self._selected_paid_model_id

    @property
    def last_models_fetch(self) -> Optional[datetime]:
        """Get timestamp of last models fetch."""
        with self._lock:
            return self._last_models_fetch

    @property
    def free_model_count(self) -> int:
        """Get count of free models."""
        with self._lock:
            return len(self._free_models)

    @property
    def paid_model_count(self) -> int:
        """Get count of paid models."""
        with self._lock:
            return len(self._paid_models)


# Global singleton instance
memory_store = MemoryStore()
