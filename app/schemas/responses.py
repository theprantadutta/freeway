"""API response schemas."""

from datetime import datetime
from typing import List, Optional

from pydantic import BaseModel


class PricingInfo(BaseModel):
    """Pricing information for a model."""

    prompt: str
    completion: str


class ModelResponse(BaseModel):
    """Response for a single model."""

    model_id: str
    model_name: str
    description: Optional[str] = None
    context_length: Optional[int] = None
    pricing: PricingInfo
    rank: Optional[int] = None


class ModelListResponse(BaseModel):
    """Response for listing models."""

    models: List[ModelResponse]
    total_count: int
    last_updated: Optional[datetime] = None


class SelectedModelResponse(BaseModel):
    """Response for the selected model endpoint."""

    model_id: str
    model_name: str
    description: Optional[str] = None
    context_length: Optional[int] = None
    pricing: PricingInfo


class HealthResponse(BaseModel):
    """Response for GET /health endpoint."""

    status: str
    service: str
    version: str
    free_models_count: int
    paid_models_count: int
    selected_free_model: Optional[str] = None
    selected_paid_model: Optional[str] = None
    last_refresh: Optional[datetime] = None
