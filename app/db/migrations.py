"""Database migrations utility."""

import logging
from alembic import command
from alembic.config import Config
from alembic.script import ScriptDirectory
from alembic.runtime.migration import MigrationContext
from sqlalchemy import create_engine

from app.config import settings

logger = logging.getLogger(__name__)


def get_sync_url():
    """Get sync database URL for migrations."""
    url = settings.database_url
    if url.startswith("postgresql+asyncpg://"):
        url = url.replace("postgresql+asyncpg://", "postgresql://")
    return url


def get_current_revision():
    """Get the current database revision."""
    engine = create_engine(
        get_sync_url(),
        connect_args={"connect_timeout": 10},
    )
    try:
        with engine.connect() as conn:
            context = MigrationContext.configure(conn)
            return context.get_current_revision()
    finally:
        engine.dispose()


def get_head_revision():
    """Get the head revision from alembic scripts."""
    alembic_cfg = Config("alembic.ini")
    script = ScriptDirectory.from_config(alembic_cfg)
    return script.get_current_head()


def run_migrations():
    """Run alembic migrations to head."""
    logger.info("Checking database migrations...")

    try:
        current = get_current_revision()
        head = get_head_revision()

        if current == head:
            logger.info(f"Database is up to date (revision: {current})")
            return

        logger.info(f"Current revision: {current}, target: {head}")
        logger.info("Running database migrations...")

        alembic_cfg = Config("alembic.ini")
        command.upgrade(alembic_cfg, "head")
        logger.info("Database migrations completed successfully")

    except Exception as e:
        logger.error(f"Migration failed: {type(e).__name__}: {e}")
        raise RuntimeError(f"Database migration failed: {e}") from e
