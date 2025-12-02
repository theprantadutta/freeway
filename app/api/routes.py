"""API routes for the Freeway service."""

import logging

from fastapi import APIRouter, Depends, HTTPException

from app.api.auth import require_api_key
from app.config import settings
from app.models.health_check import HealthStatus
from app.schemas.responses import (
    HealthResponse,
    ModelListResponse,
    ModelResponse,
    ReportRequest,
    ReportResponse,
)
from app.services.health_check_service import health_check_service
from app.services.ranking_service import ranking_service
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

router = APIRouter()


@router.get("/model", response_model=ModelResponse)
async def get_best_model(_: str = Depends(require_api_key)):
    """
    Get the best available free model based on weighted scoring.

    Scoring (0-100):
    - Availability (50%): Reliability based on health checks
    - Speed (30%): Response time (lower is better)
    - Context (20%): Context length bonus
    """
    best = ranking_service.get_best_model()

    if not best:
        raise HTTPException(
            status_code=503,
            detail="No models available. Service may be initializing.",
        )

    # Get score for the best model
    score_data = ranking_service.get_model_with_score(best.model_id)
    score = score_data[1] if score_data else None

    return ModelResponse(
        model_id=best.model_id,
        model_name=best.model_name,
        context_length=best.context_length,
        availability_score=best.availability_score,
        avg_response_time_ms=best.avg_response_time_ms,
        last_check=best.last_check,
        last_status=best.last_status,
        rank=1,
        score=round(score, 2) if score else None,
    )


@router.get("/models", response_model=ModelListResponse)
async def get_all_models(_: str = Depends(require_api_key)):
    """
    Get all monitored free models ranked by composite score.

    Ranking criteria (weighted):
    - Availability (50%): Reliability based on health checks
    - Speed (30%): Response time (lower is better)
    - Context (20%): Context length bonus
    """
    ranked = ranking_service.get_ranked_models()

    models = []
    for idx, stats in enumerate(ranked):
        score_data = ranking_service.get_model_with_score(stats.model_id)
        score = score_data[1] if score_data else None

        models.append(
            ModelResponse(
                model_id=stats.model_id,
                model_name=stats.model_name,
                context_length=stats.context_length,
                availability_score=stats.availability_score,
                avg_response_time_ms=stats.avg_response_time_ms,
                last_check=stats.last_check,
                last_status=stats.last_status,
                rank=idx + 1,
                score=round(score, 2) if score else None,
            )
        )

    return ModelListResponse(
        models=models,
        total_count=len(models),
        last_updated=memory_store.last_health_check,
    )


@router.get("/health", response_model=HealthResponse)
async def health_check(_: str = Depends(require_api_key)):
    """
    Service health check endpoint.
    Returns service status and monitoring statistics.
    """
    return HealthResponse(
        status="healthy",
        service="freeway",
        version="1.0.0",
        models_monitored=memory_store.model_count,
        health_checks_enabled=settings.HEALTH_CHECK_ENABLED and bool(settings.OPENROUTER_API_KEY),
        last_check_run=memory_store.last_health_check,
    )


@router.post("/report", response_model=ReportResponse)
async def report_failing_model(request: ReportRequest, _: str = Depends(require_api_key)):
    """
    Report a failing model from external projects.

    This endpoint:
    1. Checks if the model exists in our tracking
    2. Runs a health check against the model
    3. If it fails, removes it from the list
    4. If it passes, keeps it in the list
    """
    model_id = request.model_id
    logger.info(f"Received failure report for model: {model_id}")

    # Check if model exists
    if not memory_store.has_model(model_id):
        return ReportResponse(
            model_id=model_id,
            action="not_found",
            message=f"Model {model_id} is not being tracked",
            health_check_passed=None,
        )

    # Run a health check
    try:
        result = await health_check_service.check_single_model(model_id)
        health_passed = result.status == HealthStatus.SUCCESS

        if health_passed:
            logger.info(f"Model {model_id} passed health check, keeping in list")
            return ReportResponse(
                model_id=model_id,
                action="kept",
                message=f"Model {model_id} passed health check, kept in list",
                health_check_passed=True,
            )
        else:
            # Remove the failing model
            memory_store.remove_model(model_id)
            logger.warning(f"Model {model_id} failed health check, removed from list")
            return ReportResponse(
                model_id=model_id,
                action="removed",
                message=f"Model {model_id} failed health check and was removed",
                health_check_passed=False,
            )

    except Exception as e:
        logger.error(f"Error checking model {model_id}: {e}")
        # On error, remove the model to be safe
        memory_store.remove_model(model_id)
        return ReportResponse(
            model_id=model_id,
            action="removed",
            message=f"Error checking model, removed for safety: {str(e)}",
            health_check_passed=False,
        )
