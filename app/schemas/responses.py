"""API response schemas."""

from datetime import datetime
from typing import List, Optional

from pydantic import BaseModel

from app.models.health_check import HealthStatus


class ModelResponse(BaseModel):
    """Response for a single model with stats."""

    model_id: str
    model_name: str
    context_length: Optional[int]
    availability_score: float
    avg_response_time_ms: Optional[float]
    last_check: Optional[datetime]
    last_status: Optional[HealthStatus]
    rank: int
    score: Optional[float] = None  # Composite ranking score (0-100)


class ModelListResponse(BaseModel):
    """Response for GET /models endpoint."""

    models: List[ModelResponse]
    total_count: int
    last_updated: Optional[datetime]


class HealthResponse(BaseModel):
    """Response for GET /health endpoint."""

    status: str
    service: str
    version: str
    models_monitored: int
    health_checks_enabled: bool
    last_check_run: Optional[datetime]


class ReportRequest(BaseModel):
    """Request body for POST /report endpoint."""

    model_id: str


class ReportResponse(BaseModel):
    """Response for POST /report endpoint."""

    model_id: str
    action: str  # "removed", "kept", "not_found"
    message: str
    health_check_passed: Optional[bool] = None
