"""Analytics schemas."""

from datetime import date, datetime
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel


class UsageSummary(BaseModel):
    """Summary of usage statistics."""

    total_requests: int
    successful_requests: int
    failed_requests: int
    total_input_tokens: int
    total_output_tokens: int
    total_cost_usd: float
    avg_response_time_ms: int


class ModelUsage(BaseModel):
    """Usage statistics per model."""

    model_id: str
    model_type: str
    requests: int
    tokens: int
    cost_usd: float


class ProjectUsageResponse(BaseModel):
    """Usage statistics for a project."""

    project_id: UUID
    project_name: str
    period: dict  # {"start": date, "end": date}
    summary: UsageSummary
    by_model: List[ModelUsage]


class GlobalSummaryResponse(BaseModel):
    """Global summary across all projects."""

    total_projects: int
    active_projects: int
    total_requests_today: int
    total_requests_this_month: int
    total_cost_this_month_usd: float


class UsageLogEntry(BaseModel):
    """Single usage log entry."""

    id: UUID
    model_id: str
    model_type: str
    input_tokens: int
    output_tokens: int
    total_tokens: int
    response_time_ms: int
    cost_usd: float
    success: bool
    error_message: Optional[str]
    created_at: datetime


class UsageLogsResponse(BaseModel):
    """Response with usage logs."""

    logs: List[UsageLogEntry]
    total_count: int
