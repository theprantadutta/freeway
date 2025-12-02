"""Background scheduler for daily health checks using APScheduler."""

import logging

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.triggers.cron import CronTrigger

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
    """Start the background scheduler with daily cron job."""
    try:
        # Add the health check job - runs daily at configured hour (UTC)
        scheduler.add_job(
            run_health_checks,
            trigger=CronTrigger(hour=settings.HEALTH_CHECK_HOUR, timezone="UTC"),
            id="health_check_job",
            name="Daily Model Health Checks",
            replace_existing=True,
        )

        scheduler.start()
        logger.info(f"Scheduler started - health checks daily at {settings.HEALTH_CHECK_HOUR:02d}:00 UTC")

    except Exception as e:
        logger.error(f"Failed to start scheduler: {e}")


def stop_scheduler():
    """Stop the background scheduler."""
    try:
        scheduler.shutdown(wait=False)
        logger.info("Scheduler stopped")
    except Exception as e:
        logger.error(f"Failed to stop scheduler: {e}")
