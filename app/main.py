"""Freeway - OpenRouter Free Models Health Monitor."""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.routes import router
from app.config import settings
from app.scheduler import start_scheduler, stop_scheduler
from app.services.health_check_service import health_check_service
from app.services.openrouter_service import openrouter_service
from app.storage.memory_store import memory_store

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan: startup and shutdown events."""
    # Startup
    logger.info("=" * 60)
    logger.info(f"Starting {settings.PROJECT_NAME}")
    logger.info("=" * 60)

    # Always fetch the free models list on startup
    logger.info("Fetching free models list...")
    try:
        count = await health_check_service.refresh_free_models()
        logger.info(f"Loaded {count} free models")
    except Exception as e:
        logger.error(f"Failed to fetch models: {e}")

    # Only run health checks if enabled and API key is set
    if settings.HEALTH_CHECK_ENABLED and settings.OPENROUTER_API_KEY:
        logger.info("Health checks ENABLED")

        # Show account credits
        try:
            account_info = await openrouter_service.get_account_info()
            data = account_info.get("data", {})
            credits = data.get("limit_remaining")
            if credits is not None:
                logger.info(f"OpenRouter credits remaining: ${credits:.4f}")
            else:
                logger.info("OpenRouter account: unlimited credits")
        except Exception as e:
            logger.warning(f"Could not fetch account info: {e}")

        hours = settings.CHECK_INTERVAL_SECONDS / 3600
        model_count = len(memory_store.get_all_model_ids())
        cycle_mins = model_count * settings.CHECK_DELAY_SECONDS / 60
        logger.info(f"Check interval: {hours:.1f} hours | Delay: {settings.CHECK_DELAY_SECONDS}s | Cycle: ~{cycle_mins:.0f} min for {model_count} models")

        # Start background scheduler (no initial burst - let it run gradually)
        start_scheduler()
        logger.info("Health checks will start with first scheduled run")
    else:
        if not settings.OPENROUTER_API_KEY:
            logger.info("Health checks DISABLED (no API key set)")
        else:
            logger.info("Health checks DISABLED (HEALTH_CHECK_ENABLED=false)")

    logger.info("=" * 60)

    yield  # Application runs here

    # Shutdown
    logger.info("Shutting down...")
    if settings.HEALTH_CHECK_ENABLED and settings.OPENROUTER_API_KEY:
        stop_scheduler()
    logger.info("Shutdown complete")


app = FastAPI(
    title=settings.PROJECT_NAME,
    description="Monitor and rank OpenRouter free models by availability and speed",
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
)

# Include API routes
app.include_router(router)


@app.get("/")
async def root():
    """Root endpoint with service information."""
    return {
        "service": "freeway",
        "description": "OpenRouter Free Models Health Monitor",
        "version": "1.0.0",
        "endpoints": {
            "best_model": "/model",
            "all_models": "/models",
            "health": "/health",
            "docs": "/docs",
        },
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=True,
    )
