"""Admin API endpoints for project management and analytics."""

import logging
import uuid
from datetime import datetime, timedelta, timezone
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query

from app.api.auth import require_admin_key
from app.db.connection import async_session_maker
from app.db.repositories.project_repo import ProjectRepository
from app.db.repositories.usage_repo import UsageRepository
from app.schemas.analytics import (
    GlobalSummaryResponse,
    ModelUsage,
    ProjectUsageResponse,
    UsageLogEntry,
    UsageLogsResponse,
    UsageSummary,
)
from app.schemas.projects import (
    CreateProjectRequest,
    ProjectListResponse,
    ProjectResponse,
    ProjectWithKeyResponse,
    RotateKeyResponse,
    UpdateProjectRequest,
)
from app.schemas.responses import SetModelRequest, SetModelResponse
from app.services.project_service import ProjectService
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/admin", tags=["admin"])


# ============== Project Management ==============


@router.get("/projects", response_model=ProjectListResponse)
async def list_projects(_: str = Depends(require_admin_key)):
    """List all projects."""
    async with async_session_maker() as session:
        repo = ProjectRepository(session)
        projects = await repo.get_all()

    return ProjectListResponse(
        projects=[ProjectResponse.model_validate(p) for p in projects],
        total_count=len(projects),
    )


@router.post("/projects", response_model=ProjectWithKeyResponse, status_code=201)
async def create_project(
    request: CreateProjectRequest,
    _: str = Depends(require_admin_key),
):
    """
    Create a new project.

    **Important:** The `api_key` is only returned once. Store it securely!
    """
    async with async_session_maker() as session:
        service = ProjectService(session)
        project, api_key = await service.create_project(
            name=request.name,
            rate_limit_per_minute=request.rate_limit_per_minute,
            metadata=request.metadata,
        )

    return ProjectWithKeyResponse(
        id=project.id,
        name=project.name,
        api_key_prefix=project.api_key_prefix,
        created_at=project.created_at,
        updated_at=project.updated_at,
        is_active=project.is_active,
        rate_limit_per_minute=project.rate_limit_per_minute,
        api_key=api_key,
    )


@router.get("/projects/{project_id}", response_model=ProjectResponse)
async def get_project(
    project_id: uuid.UUID,
    _: str = Depends(require_admin_key),
):
    """Get a project by ID."""
    async with async_session_maker() as session:
        repo = ProjectRepository(session)
        project = await repo.get_by_id(project_id)

    if not project:
        raise HTTPException(status_code=404, detail="Project not found")

    return ProjectResponse.model_validate(project)


@router.patch("/projects/{project_id}", response_model=ProjectResponse)
async def update_project(
    project_id: uuid.UUID,
    request: UpdateProjectRequest,
    _: str = Depends(require_admin_key),
):
    """Update a project."""
    async with async_session_maker() as session:
        service = ProjectService(session)
        project = await service.update_project(
            project_id=project_id,
            name=request.name,
            is_active=request.is_active,
            rate_limit_per_minute=request.rate_limit_per_minute,
            metadata=request.metadata,
        )

    if not project:
        raise HTTPException(status_code=404, detail="Project not found")

    return ProjectResponse.model_validate(project)


@router.delete("/projects/{project_id}", status_code=204)
async def delete_project(
    project_id: uuid.UUID,
    _: str = Depends(require_admin_key),
):
    """Delete a project."""
    async with async_session_maker() as session:
        service = ProjectService(session)
        deleted = await service.delete_project(project_id)

    if not deleted:
        raise HTTPException(status_code=404, detail="Project not found")


@router.post("/projects/{project_id}/rotate-key", response_model=RotateKeyResponse)
async def rotate_api_key(
    project_id: uuid.UUID,
    _: str = Depends(require_admin_key),
):
    """
    Rotate (regenerate) a project's API key.

    **Important:** The old key is immediately invalidated.
    The new `api_key` is only returned once. Store it securely!
    """
    async with async_session_maker() as session:
        service = ProjectService(session)
        project, api_key = await service.rotate_api_key(project_id)

    if not project or not api_key:
        raise HTTPException(status_code=404, detail="Project not found")

    return RotateKeyResponse(
        id=project.id,
        api_key=api_key,
        api_key_prefix=project.api_key_prefix,
    )


# ============== Analytics ==============


@router.get("/analytics/usage", response_model=ProjectUsageResponse)
async def get_project_usage(
    project_id: uuid.UUID = Query(..., description="Project ID"),
    start_date: Optional[datetime] = Query(None, description="Start date (UTC)"),
    end_date: Optional[datetime] = Query(None, description="End date (UTC)"),
    _: str = Depends(require_admin_key),
):
    """Get usage statistics for a specific project."""
    async with async_session_maker() as session:
        project_repo = ProjectRepository(session)
        project = await project_repo.get_by_id(project_id)

        if not project:
            raise HTTPException(status_code=404, detail="Project not found")

        usage_repo = UsageRepository(session)

        # Get summary stats
        stats = await usage_repo.get_project_stats(project_id, start_date, end_date)

        # Get per-model breakdown
        model_stats = await usage_repo.get_stats_by_model(project_id, start_date, end_date)

    return ProjectUsageResponse(
        project_id=project.id,
        project_name=project.name,
        period={
            "start": start_date.isoformat() if start_date else None,
            "end": end_date.isoformat() if end_date else None,
        },
        summary=UsageSummary(**stats),
        by_model=[ModelUsage(**m) for m in model_stats],
    )


@router.get("/analytics/summary", response_model=GlobalSummaryResponse)
async def get_global_summary(_: str = Depends(require_admin_key)):
    """Get global summary statistics across all projects."""
    now = datetime.now(timezone.utc)
    today_start = now.replace(hour=0, minute=0, second=0, microsecond=0)
    month_start = now.replace(day=1, hour=0, minute=0, second=0, microsecond=0)

    async with async_session_maker() as session:
        project_repo = ProjectRepository(session)
        usage_repo = UsageRepository(session)

        # Get project counts
        total_projects = await project_repo.count()
        active_projects = await project_repo.count_active()

        # Get today's usage
        today_stats = await usage_repo.get_global_stats(start_date=today_start)

        # Get this month's usage
        month_stats = await usage_repo.get_global_stats(start_date=month_start)

    return GlobalSummaryResponse(
        total_projects=total_projects,
        active_projects=active_projects,
        total_requests_today=today_stats["total_requests"],
        total_requests_this_month=month_stats["total_requests"],
        total_cost_this_month_usd=month_stats["total_cost_usd"],
    )


@router.get("/analytics/logs", response_model=UsageLogsResponse)
async def get_usage_logs(
    project_id: uuid.UUID = Query(..., description="Project ID"),
    start_date: Optional[datetime] = Query(None, description="Start date (UTC)"),
    end_date: Optional[datetime] = Query(None, description="End date (UTC)"),
    limit: int = Query(100, ge=1, le=1000, description="Max records to return"),
    offset: int = Query(0, ge=0, description="Offset for pagination"),
    _: str = Depends(require_admin_key),
):
    """Get usage logs for a specific project."""
    async with async_session_maker() as session:
        project_repo = ProjectRepository(session)
        project = await project_repo.get_by_id(project_id)

        if not project:
            raise HTTPException(status_code=404, detail="Project not found")

        usage_repo = UsageRepository(session)
        logs = await usage_repo.get_by_project(
            project_id, start_date, end_date, limit, offset
        )

    return UsageLogsResponse(
        logs=[
            UsageLogEntry(
                id=log.id,
                model_id=log.model_id,
                model_type=log.model_type,
                input_tokens=log.input_tokens,
                output_tokens=log.output_tokens,
                total_tokens=log.total_tokens,
                response_time_ms=log.response_time_ms,
                cost_usd=float(log.cost_usd),
                success=log.success,
                error_message=log.error_message,
                created_at=log.created_at,
            )
            for log in logs
        ],
        total_count=len(logs),
    )


# ============== Model Selection ==============


@router.put("/model/free", response_model=SetModelResponse)
async def set_selected_free_model(
    request: SetModelRequest,
    _: str = Depends(require_admin_key),
):
    """
    Set the selected free model.

    The model must exist in the available free models list.
    This overrides the automatic selection based on context length.
    """
    model = memory_store.get_free_model(request.model_id)

    if not model:
        raise HTTPException(
            status_code=404,
            detail=f"Free model '{request.model_id}' not found. Check /models/free for available models.",
        )

    success = memory_store.set_selected_free_model(request.model_id)

    if not success:
        raise HTTPException(
            status_code=500,
            detail="Failed to set selected free model",
        )

    logger.info(f"Admin set selected free model to: {request.model_id}")

    return SetModelResponse(
        success=True,
        model_id=model.id,
        model_name=model.name,
        message=f"Selected free model set to '{model.name}'",
    )


@router.put("/model/paid", response_model=SetModelResponse)
async def set_selected_paid_model(
    request: SetModelRequest,
    _: str = Depends(require_admin_key),
):
    """
    Set the selected paid model.

    The model must exist in the available paid models list.
    This overrides the automatic selection based on price.
    """
    model = memory_store.get_paid_model(request.model_id)

    if not model:
        raise HTTPException(
            status_code=404,
            detail=f"Paid model '{request.model_id}' not found. Check /models/paid for available models.",
        )

    success = memory_store.set_selected_paid_model(request.model_id)

    if not success:
        raise HTTPException(
            status_code=500,
            detail="Failed to set selected paid model",
        )

    logger.info(f"Admin set selected paid model to: {request.model_id}")

    return SetModelResponse(
        success=True,
        model_id=model.id,
        model_name=model.name,
        message=f"Selected paid model set to '{model.name}'",
    )
