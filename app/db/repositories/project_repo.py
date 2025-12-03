"""Project repository for database operations."""

import uuid
from datetime import datetime, timezone
from typing import List, Optional

from sqlalchemy import select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.database import Project


class ProjectRepository:
    """Repository for Project database operations."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_all(self) -> List[Project]:
        """Get all projects."""
        result = await self.session.execute(select(Project).order_by(Project.created_at.desc()))
        return list(result.scalars().all())

    async def get_all_active(self) -> List[Project]:
        """Get all active projects."""
        result = await self.session.execute(
            select(Project).where(Project.is_active == True).order_by(Project.created_at.desc())
        )
        return list(result.scalars().all())

    async def get_by_id(self, project_id: uuid.UUID) -> Optional[Project]:
        """Get a project by ID."""
        result = await self.session.execute(select(Project).where(Project.id == project_id))
        return result.scalar_one_or_none()

    async def get_by_api_key_hash(self, api_key_hash: str) -> Optional[Project]:
        """Get a project by API key hash."""
        result = await self.session.execute(
            select(Project).where(Project.api_key_hash == api_key_hash)
        )
        return result.scalar_one_or_none()

    async def create(
        self,
        name: str,
        api_key_hash: str,
        api_key_prefix: str,
        rate_limit_per_minute: int = 60,
        metadata: Optional[dict] = None,
    ) -> Project:
        """Create a new project."""
        project = Project(
            name=name,
            api_key_hash=api_key_hash,
            api_key_prefix=api_key_prefix,
            rate_limit_per_minute=rate_limit_per_minute,
            metadata_=metadata or {},
        )
        self.session.add(project)
        await self.session.commit()
        await self.session.refresh(project)
        return project

    async def update(
        self,
        project_id: uuid.UUID,
        name: Optional[str] = None,
        is_active: Optional[bool] = None,
        rate_limit_per_minute: Optional[int] = None,
        metadata: Optional[dict] = None,
    ) -> Optional[Project]:
        """Update a project."""
        project = await self.get_by_id(project_id)
        if not project:
            return None

        if name is not None:
            project.name = name
        if is_active is not None:
            project.is_active = is_active
        if rate_limit_per_minute is not None:
            project.rate_limit_per_minute = rate_limit_per_minute
        if metadata is not None:
            project.metadata_ = metadata

        project.updated_at = datetime.now(timezone.utc)
        await self.session.commit()
        await self.session.refresh(project)
        return project

    async def update_api_key(
        self,
        project_id: uuid.UUID,
        new_api_key_hash: str,
        new_api_key_prefix: str,
    ) -> Optional[Project]:
        """Update a project's API key (for rotation)."""
        project = await self.get_by_id(project_id)
        if not project:
            return None

        project.api_key_hash = new_api_key_hash
        project.api_key_prefix = new_api_key_prefix
        project.updated_at = datetime.now(timezone.utc)
        await self.session.commit()
        await self.session.refresh(project)
        return project

    async def delete(self, project_id: uuid.UUID) -> bool:
        """Delete a project."""
        project = await self.get_by_id(project_id)
        if not project:
            return False

        await self.session.delete(project)
        await self.session.commit()
        return True

    async def count(self) -> int:
        """Count total projects."""
        result = await self.session.execute(select(Project))
        return len(result.scalars().all())

    async def count_active(self) -> int:
        """Count active projects."""
        result = await self.session.execute(select(Project).where(Project.is_active == True))
        return len(result.scalars().all())
