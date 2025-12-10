"""SQLAlchemy Base class - separate module to avoid circular imports."""

from sqlalchemy.orm import DeclarativeBase


class Base(DeclarativeBase):
    """Base class for SQLAlchemy models."""

    pass
