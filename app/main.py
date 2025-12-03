"""Freeway - OpenRouter AI Gateway."""

import logging
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI

from app.api.auth import require_api_key
from app.api.admin import router as admin_router
from app.api.routes import router
from app.api.chat import router as chat_router
from app.config import settings
from app.db.connection import init_db, close_db
from app.scheduler import start_scheduler, stop_scheduler
from app.services.openrouter_service import openrouter_service
from app.services.ranking_service import ranking_service
from app.storage.memory_store import memory_store
from app.storage.project_cache import project_cache

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

    # Initialize database
    db_available = False
    try:
        await init_db()
        db_available = True
    except Exception as e:
        logger.error(f"Database initialization failed: {e}")
        logger.warning("Continuing without database - some features will be unavailable")

    # Load project cache from database
    if db_available:
        try:
            count = await project_cache.load_from_db()
            logger.info(f"Loaded {count} projects into cache")
        except Exception as e:
            logger.error(f"Failed to load project cache: {e}")

    # Fetch and categorize models
    logger.info("Fetching models from OpenRouter...")
    try:
        free_models, paid_models = await openrouter_service.fetch_and_categorize_models()
        memory_store.update_models(free_models, paid_models)
        logger.info(f"Loaded {len(free_models)} free models, {len(paid_models)} paid models")

        # Select best free model (auto-updated daily)
        ranking_service.select_best_free_model()

        # Select cheapest paid model (fixed, not auto-updated)
        ranking_service.select_cheapest_paid_model()

    except Exception as e:
        logger.error(f"Failed to fetch models: {e}")

    # Start the scheduler for daily model refresh
    start_scheduler()

    logger.info("=" * 60)

    yield  # Application runs here

    # Shutdown
    logger.info("Shutting down...")
    stop_scheduler()
    await close_db()
    logger.info("Shutdown complete")


app = FastAPI(
    title=settings.PROJECT_NAME,
    description="OpenRouter AI Gateway with project management and analytics",
    version="2.0.0",
    lifespan=lifespan,
    docs_url=None,  # Disable public docs
    redoc_url=None,  # Disable public redoc
    openapi_url=None,  # Disable public OpenAPI schema
)

# Include API routes
app.include_router(router)
app.include_router(chat_router)
app.include_router(admin_router)


@app.get("/")
async def root(_: str = Depends(require_api_key)):
    """Root endpoint with service information."""
    return {
        "service": "freeway",
        "description": "OpenRouter AI Gateway",
        "version": "2.0.0",
        "endpoints": {
            "chat_completions": "/chat/completions",
            "free_model": "/model/free",
            "paid_model": "/model/paid",
            "all_free_models": "/models/free",
            "all_paid_models": "/models/paid",
            "health": "/health",
            "admin": "/admin/*",
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
