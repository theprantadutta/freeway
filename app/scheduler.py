"""Background scheduler for periodic health checks using APScheduler."""

import logging

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.interval import IntervalTrigger

from app.config import settings
from app.services.health_check_service import health_check_service

logger = logging.getLogger(__name__)

scheduler = AsyncIOScheduler()


async def run_health_checks():
    """Scheduled task: refresh models and run health checks."""
    try:
        # First, refresh the model list (in case new free models were added)
        await health_check_service.refresh_free_models()

        # Then run health checks on all models
        result = await health_check_service.check_all_models()
        logger.info(f"Scheduled health check completed: {result}")

    except Exception as e:
        logger.error(f"Error in scheduled health check: {e}")


def start_scheduler():
    """Start the background scheduler."""
    from datetime import datetime, timedelta, timezone

    try:
        # Add the health check job - run immediately, then every CHECK_INTERVAL_SECONDS
        scheduler.add_job(
            run_health_checks,
            trigger=IntervalTrigger(seconds=settings.CHECK_INTERVAL_SECONDS),
            id="health_check_job",
            name="Model Health Checks",
            replace_existing=True,
            next_run_time=datetime.now(timezone.utc) + timedelta(seconds=5),  # Start in 5 seconds
        )

        scheduler.start()
        hours = settings.CHECK_INTERVAL_SECONDS / 3600
        logger.info(f"Scheduler started - first run in 5s, then every {hours:.1f} hours")

    except Exception as e:
        logger.error(f"Failed to start scheduler: {e}")


def stop_scheduler():
    """Stop the background scheduler."""
    try:
        scheduler.shutdown(wait=False)
        logger.info("Scheduler stopped")
    except Exception as e:
        logger.error(f"Failed to stop scheduler: {e}")
