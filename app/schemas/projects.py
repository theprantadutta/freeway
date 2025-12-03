"""Project management schemas."""

from datetime import datetime
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, Field


class ProjectBase(BaseModel):
    """Base project fields."""

    name: str = Field(..., min_length=1, max_length=255, description="Project name")
    rate_limit_per_minute: int = Field(default=60, ge=1, description="Rate limit per minute")


class CreateProjectRequest(ProjectBase):
    """Request to create a new project."""

    metadata: Optional[dict] = Field(default=None, description="Optional metadata")


class UpdateProjectRequest(BaseModel):
    """Request to update a project."""

    name: Optional[str] = Field(None, min_length=1, max_length=255)
    is_active: Optional[bool] = None
    rate_limit_per_minute: Optional[int] = Field(None, ge=1)
    metadata: Optional[dict] = None


class ProjectResponse(BaseModel):
    """Project response (without API key)."""

    id: UUID
    name: str
    api_key_prefix: str
    created_at: datetime
    updated_at: datetime
    is_active: bool
    rate_limit_per_minute: int

    class Config:
        from_attributes = True


class ProjectWithKeyResponse(ProjectResponse):
    """Project response with API key (only returned on create/rotate)."""

    api_key: str = Field(..., description="Full API key - only shown once!")


class ProjectListResponse(BaseModel):
    """List of projects response."""

    projects: List[ProjectResponse]
    total_count: int


class RotateKeyResponse(BaseModel):
    """Response for API key rotation."""

    id: UUID
    api_key: str = Field(..., description="New API key - only shown once!")
    api_key_prefix: str
