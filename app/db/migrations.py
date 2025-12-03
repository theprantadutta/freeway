"""Database migrations utility."""

import logging
import sys
from alembic import command
from alembic.config import Config

logger = logging.getLogger(__name__)


def run_migrations():
    """Run alembic migrations to head."""
    logger.info("Running database migrations...")

    try:
        alembic_cfg = Config("alembic.ini")
        command.upgrade(alembic_cfg, "head")
        logger.info("Database migrations completed successfully")
    except Exception as e:
        logger.error(f"Migration failed: {type(e).__name__}: {e}")
        # Re-raise with more context
        raise RuntimeError(f"Database migration failed: {e}") from e
