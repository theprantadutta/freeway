"""Health check result models."""

from datetime import datetime
from enum import Enum
from typing import Optional

from pydantic import BaseModel


class HealthStatus(str, Enum):
    """Health check status."""

    SUCCESS = "success"
    FAILURE = "failure"
    TIMEOUT = "timeout"


class HealthCheckResult(BaseModel):
    """Single health check result."""

    timestamp: datetime
    status: HealthStatus
    response_time_ms: Optional[float] = None  # None if failed/timeout
    error_message: Optional[str] = None


class ModelHealthStats(BaseModel):
    """Aggregated health statistics for a model."""

    model_id: str
    model_name: str
    context_length: Optional[int]
    total_checks: int
    successful_checks: int
    availability_score: float  # 0.0 to 1.0
    avg_response_time_ms: Optional[float]  # None if no successful checks
    last_check: Optional[datetime]
    last_status: Optional[HealthStatus]
