"""API routes for the Freeway service."""

import logging

from fastapi import APIRouter, Depends, HTTPException

from app.api.auth import require_api_key
from app.models.openrouter import OpenRouterModel
from app.schemas.responses import (
    HealthResponse,
    ModelListResponse,
    ModelResponse,
    PricingInfo,
    SelectedModelResponse,
)
from app.services.ranking_service import ranking_service
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

router = APIRouter()


def model_to_response(model: OpenRouterModel, rank: int = None) -> ModelResponse:
    """Convert OpenRouterModel to ModelResponse."""
    return ModelResponse(
        model_id=model.id,
        model_name=model.name,
        description=model.description,
        context_length=model.context_length,
        pricing=PricingInfo(
            prompt=model.pricing.prompt,
            completion=model.pricing.completion,
        ),
        rank=rank,
    )


def model_to_selected_response(model: OpenRouterModel) -> SelectedModelResponse:
    """Convert OpenRouterModel to SelectedModelResponse."""
    return SelectedModelResponse(
        model_id=model.id,
        model_name=model.name,
        description=model.description,
        context_length=model.context_length,
        pricing=PricingInfo(
            prompt=model.pricing.prompt,
            completion=model.pricing.completion,
        ),
    )


# ============== Free Model Endpoints ==============


@router.get("/model/free", response_model=SelectedModelResponse)
async def get_selected_free_model(_: str = Depends(require_api_key)):
    """
    Get the currently selected free model.

    The best free model is automatically selected based on context length
    and updated daily when better models become available.
    """
    model = memory_store.get_selected_free_model()

    if not model:
        raise HTTPException(
            status_code=503,
            detail="No free model selected. Service may be initializing.",
        )

    return model_to_selected_response(model)


@router.get("/models/free", response_model=ModelListResponse)
async def get_all_free_models(_: str = Depends(require_api_key)):
    """
    Get all free models ranked by context length (largest first).
    """
    ranked = ranking_service.get_ranked_free_models()

    models = [model_to_response(m, rank=idx + 1) for idx, m in enumerate(ranked)]

    return ModelListResponse(
        models=models,
        total_count=len(models),
        last_updated=memory_store.last_models_fetch,
    )


# ============== Paid Model Endpoints ==============


@router.get("/model/paid", response_model=SelectedModelResponse)
async def get_selected_paid_model(_: str = Depends(require_api_key)):
    """
    Get the currently selected paid model.

    The cheapest paid model is selected on startup and remains fixed
    (not automatically updated).
    """
    model = memory_store.get_selected_paid_model()

    if not model:
        raise HTTPException(
            status_code=503,
            detail="No paid model selected. Service may be initializing.",
        )

    return model_to_selected_response(model)


@router.get("/models/paid", response_model=ModelListResponse)
async def get_all_paid_models(_: str = Depends(require_api_key)):
    """
    Get all paid models ranked by price (cheapest first).
    """
    ranked = ranking_service.get_ranked_paid_models()

    models = [model_to_response(m, rank=idx + 1) for idx, m in enumerate(ranked)]

    return ModelListResponse(
        models=models,
        total_count=len(models),
        last_updated=memory_store.last_models_fetch,
    )


# ============== Health Endpoint ==============


@router.get("/health", response_model=HealthResponse)
async def health_check(_: str = Depends(require_api_key)):
    """
    Service health check endpoint.
    Returns service status and model statistics.
    """
    return HealthResponse(
        status="healthy",
        service="freeway",
        version="1.0.0",
        free_models_count=memory_store.free_model_count,
        paid_models_count=memory_store.paid_model_count,
        selected_free_model=memory_store.selected_free_model_id,
        selected_paid_model=memory_store.selected_paid_model_id,
        last_refresh=memory_store.last_models_fetch,
    )
