"""Database migrations utility."""

import logging
import time
from alembic import command
from alembic.config import Config
from alembic.script import ScriptDirectory
from alembic.runtime.migration import MigrationContext
from sqlalchemy import create_engine, inspect, text

from app.config import settings

logger = logging.getLogger(__name__)

MAX_RETRIES = 30
RETRY_DELAY = 2  # seconds


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


def tables_exist():
    """Check if our application tables already exist."""
    engine = create_engine(
        get_sync_url(),
        connect_args={"connect_timeout": 10},
    )
    try:
        with engine.connect() as conn:
            inspector = inspect(conn)
            existing = inspector.get_table_names()
            # Check for our main tables
            return 'projects' in existing and 'usage_logs' in existing
    finally:
        engine.dispose()


def wait_for_db():
    """Wait for database to be fully ready."""
    logger.info("Waiting for database to be ready...")
    engine = create_engine(
        get_sync_url(),
        connect_args={"connect_timeout": 5},
    )

    for attempt in range(1, MAX_RETRIES + 1):
        try:
            with engine.connect() as conn:
                # Simple query to verify DB is fully operational
                conn.execute(text("SELECT 1"))
                conn.commit()
            logger.info(f"Database is ready (attempt {attempt})")
            engine.dispose()
            return True
        except Exception as e:
            logger.warning(f"Database not ready (attempt {attempt}/{MAX_RETRIES}): {e}")
            if attempt < MAX_RETRIES:
                time.sleep(RETRY_DELAY)
        finally:
            engine.dispose()

    raise RuntimeError(f"Database not ready after {MAX_RETRIES} attempts")


def run_migrations():
    """Run alembic migrations to head."""
    # Wait for database to be fully ready first
    wait_for_db()

    logger.info("Checking database migrations...")

    try:
        current = get_current_revision()
        head = get_head_revision()

        if current == head:
            logger.info(f"Database is up to date (revision: {current})")
            return

        # If no revision but tables exist, stamp instead of migrate
        if current is None and tables_exist():
            logger.info("Tables exist but no alembic version - stamping current state")
            alembic_cfg = Config("alembic.ini")
            command.stamp(alembic_cfg, head)
            logger.info(f"Database stamped at revision: {head}")
            return

        logger.info(f"Current revision: {current}, target: {head}")
        logger.info("Running database migrations...")

        alembic_cfg = Config("alembic.ini")
        command.upgrade(alembic_cfg, "head")
        logger.info("Database migrations completed successfully")

    except Exception as e:
        logger.error(f"Migration failed: {type(e).__name__}: {e}")
        raise RuntimeError(f"Database migration failed: {e}") from e
