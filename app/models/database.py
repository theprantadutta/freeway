"""SQLAlchemy ORM models for database tables."""

import uuid
from datetime import datetime, timezone
from decimal import Decimal
from typing import Optional

from sqlalchemy import Boolean, DateTime, Index, Integer, Numeric, String, Text, ForeignKey
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base


def utc_now() -> datetime:
    """Return current UTC datetime."""
    return datetime.now(timezone.utc)


class Project(Base):
    """Project model - represents registered API projects."""

    __tablename__ = "projects"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        primary_key=True,
        default=uuid.uuid4,
    )
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    api_key_hash: Mapped[str] = mapped_column(String(255), nullable=False, unique=True)
    api_key_prefix: Mapped[str] = mapped_column(String(8), nullable=False)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=utc_now,
        nullable=False,
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=utc_now,
        onupdate=utc_now,
        nullable=False,
    )
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)
    rate_limit_per_minute: Mapped[int] = mapped_column(Integer, default=60, nullable=False)
    metadata_: Mapped[Optional[dict]] = mapped_column(
        "metadata",
        JSONB,
        default=dict,
        nullable=True,
    )

    # Relationships
    usage_logs: Mapped[list["UsageLog"]] = relationship(
        "UsageLog",
        back_populates="project",
        cascade="all, delete-orphan",
    )

    __table_args__ = (
        Index("idx_projects_api_key_hash", "api_key_hash"),
        Index("idx_projects_is_active", "is_active"),
    )

    def __repr__(self) -> str:
        return f"<Project(id={self.id}, name='{self.name}', prefix='{self.api_key_prefix}')>"


class UsageLog(Base):
    """Usage log model - tracks every completion request."""

    __tablename__ = "usage_logs"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        primary_key=True,
        default=uuid.uuid4,
    )
    project_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("projects.id", ondelete="CASCADE"),
        nullable=False,
    )
    model_id: Mapped[str] = mapped_column(String(255), nullable=False)
    model_type: Mapped[str] = mapped_column(String(10), nullable=False)  # 'free' or 'paid'
    input_tokens: Mapped[int] = mapped_column(Integer, default=0, nullable=False)
    output_tokens: Mapped[int] = mapped_column(Integer, default=0, nullable=False)
    response_time_ms: Mapped[int] = mapped_column(Integer, nullable=False)
    cost_usd: Mapped[Decimal] = mapped_column(
        Numeric(20, 10),
        default=Decimal("0"),
        nullable=False,
    )
    prompt_cost_per_token: Mapped[Optional[Decimal]] = mapped_column(
        Numeric(20, 15),
        nullable=True,
    )
    completion_cost_per_token: Mapped[Optional[Decimal]] = mapped_column(
        Numeric(20, 15),
        nullable=True,
    )
    success: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)
    error_message: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    request_id: Mapped[Optional[str]] = mapped_column(String(255), nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=utc_now,
        nullable=False,
    )

    # Extended fields for full request/response logging
    provider: Mapped[Optional[str]] = mapped_column(String(50), nullable=True)
    request_messages: Mapped[Optional[list]] = mapped_column(JSONB, nullable=True)
    response_content: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    finish_reason: Mapped[Optional[str]] = mapped_column(String(50), nullable=True)
    request_params: Mapped[Optional[dict]] = mapped_column(JSONB, nullable=True)

    # Relationships
    project: Mapped["Project"] = relationship("Project", back_populates="usage_logs")

    __table_args__ = (
        Index("idx_usage_logs_project_id", "project_id"),
        Index("idx_usage_logs_created_at", "created_at"),
        Index("idx_usage_logs_project_created", "project_id", "created_at"),
    )

    @property
    def total_tokens(self) -> int:
        """Calculate total tokens."""
        return self.input_tokens + self.output_tokens

    def __repr__(self) -> str:
        return f"<UsageLog(id={self.id}, project_id={self.project_id}, model='{self.model_id}')>"


class ModelsCache(Base):
    """Models cache - persistent storage for OpenRouter models."""

    __tablename__ = "models_cache"

    id: Mapped[str] = mapped_column(String(255), primary_key=True)  # OpenRouter model ID
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    description: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    context_length: Mapped[Optional[int]] = mapped_column(Integer, nullable=True)
    prompt_price: Mapped[str] = mapped_column(String(50), nullable=False)
    completion_price: Mapped[str] = mapped_column(String(50), nullable=False)
    request_price: Mapped[Optional[str]] = mapped_column(String(50), nullable=True)
    image_price: Mapped[Optional[str]] = mapped_column(String(50), nullable=True)
    is_free: Mapped[bool] = mapped_column(Boolean, nullable=False)
    architecture: Mapped[Optional[dict]] = mapped_column(JSONB, nullable=True)
    openrouter_created: Mapped[Optional[datetime]] = mapped_column(
        DateTime(timezone=True),
        nullable=True,
    )
    fetched_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        default=utc_now,
        nullable=False,
    )
    raw_data: Mapped[Optional[dict]] = mapped_column(JSONB, nullable=True)

    __table_args__ = (Index("idx_models_cache_is_free", "is_free"),)

    def __repr__(self) -> str:
        return f"<ModelsCache(id='{self.id}', name='{self.name}', is_free={self.is_free})>"
