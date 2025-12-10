import logging
from logging.config import fileConfig

from sqlalchemy import create_engine, pool, text

from alembic import context

from app.config import settings
from app.db.base import Base

# Import models to register them with Base.metadata (for autogenerate)
# This import is safe because database.py only imports Base from base.py
from app.models import database  # noqa: F401

logger = logging.getLogger(__name__)

config = context.config

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

target_metadata = Base.metadata


def get_url():
    """Get sync database URL (convert asyncpg to psycopg2)."""
    url = settings.database_url
    # Convert async URL to sync URL for alembic
    if url.startswith("postgresql+asyncpg://"):
        url = url.replace("postgresql+asyncpg://", "postgresql://")
    return url


def run_migrations_offline() -> None:
    """Run migrations in 'offline' mode."""
    url = get_url()
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations in 'online' mode."""
    connectable = create_engine(
        get_url(),
        poolclass=pool.NullPool,
        connect_args={
            "connect_timeout": 30,
        },
    )

    try:
        with connectable.connect() as connection:
            context.configure(
                connection=connection,
                target_metadata=target_metadata,
                transaction_per_migration=True,
            )
            with context.begin_transaction():
                context.run_migrations()
    finally:
        connectable.dispose()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
