"""Database migrations utility."""

import logging
from alembic import command
from alembic.config import Config

logger = logging.getLogger(__name__)


def run_migrations():
    """Run alembic migrations to head."""
    try:
        logger.info("Running database migrations...")
        alembic_cfg = Config("alembic.ini")
        command.upgrade(alembic_cfg, "head")
        logger.info("Database migrations completed successfully")
    except Exception as e:
        logger.error(f"Migration failed: {e}")
        raise
