"""Background scheduler for daily model refresh using APScheduler."""

import logging

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.cron import CronTrigger

from app.services.openrouter_service import openrouter_service
from app.services.ranking_service import ranking_service
from app.storage.memory_store import memory_store

logger = logging.getLogger(__name__)

scheduler = AsyncIOScheduler()


async def refresh_models():
    """
    Scheduled task: refresh models from OpenRouter API.

    - Updates the model lists (free and paid)
    - Auto-updates the best free model selection
    - Does NOT update the paid model selection (kept fixed)
    """
    try:
        logger.info("Starting scheduled model refresh...")

        # Fetch and categorize models
        free_models, paid_models = await openrouter_service.fetch_and_categorize_models()

        # Update storage
        memory_store.update_models(free_models, paid_models)

        # Auto-update best free model (this can change)
        old_free_model = memory_store.selected_free_model_id
        ranking_service.select_best_free_model()
        new_free_model = memory_store.selected_free_model_id

        if old_free_model != new_free_model:
            logger.info(f"Free model updated: {old_free_model} -> {new_free_model}")
        else:
            logger.info(f"Free model unchanged: {new_free_model}")

        # Note: Paid model is NOT auto-updated
        logger.info(f"Paid model kept: {memory_store.selected_paid_model_id}")

        logger.info(f"Model refresh complete: {len(free_models)} free, {len(paid_models)} paid")

    except Exception as e:
        logger.error(f"Error in scheduled model refresh: {e}")


def start_scheduler():
    """Start the background scheduler with daily cron job at midnight UTC."""
    try:
        # Add the refresh job - runs daily at midnight (00:00 UTC)
        scheduler.add_job(
            refresh_models,
            trigger=CronTrigger(hour=0, minute=0, timezone="UTC"),
            id="model_refresh_job",
            name="Daily Model Refresh",
            replace_existing=True,
        )

        scheduler.start()
        logger.info("Scheduler started - model refresh daily at 00:00 UTC")

    except Exception as e:
        logger.error(f"Failed to start scheduler: {e}")


def stop_scheduler():
    """Stop the background scheduler."""
    try:
        scheduler.shutdown(wait=False)
        logger.info("Scheduler stopped")
    except Exception as e:
        logger.error(f"Failed to stop scheduler: {e}")
