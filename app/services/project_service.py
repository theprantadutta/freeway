"""Project service for managing projects and API keys."""

import logging
import secrets
import uuid
from typing import List, Optional, Tuple

import bcrypt
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.db.repositories.project_repo import ProjectRepository
from app.models.database import Project
from app.storage.project_cache import project_cache

logger = logging.getLogger(__name__)


class ProjectService:
    """Service for managing projects and API keys."""

    def __init__(self, session: AsyncSession):
        self.session = session
        self.repo = ProjectRepository(session)

    @staticmethod
    def generate_api_key() -> Tuple[str, str, str]:
        """
        Generate a new API key.

        Returns:
            Tuple of (raw_key, key_hash, key_prefix)
            - raw_key: The full API key to give to the user (only shown once)
            - key_hash: bcrypt hash to store in database
            - key_prefix: First 8 characters for identification
        """
        # Generate random token
        raw_token = secrets.token_urlsafe(32)
        full_key = f"{settings.API_KEY_PREFIX}{raw_token}"

        # Hash with bcrypt
        key_hash = bcrypt.hashpw(full_key.encode(), bcrypt.gensalt()).decode()

        # Get prefix for display
        key_prefix = full_key[:8]

        return full_key, key_hash, key_prefix

    async def create_project(
        self,
        name: str,
        rate_limit_per_minute: int = 60,
        metadata: Optional[dict] = None,
    ) -> Tuple[Project, str]:
        """
        Create a new project with a generated API key.

        Returns:
            Tuple of (project, raw_api_key)
            Note: raw_api_key is only returned once and should be shown to user
        """
        # Generate API key
        raw_key, key_hash, key_prefix = self.generate_api_key()

        # Create project in database
        project = await self.repo.create(
            name=name,
            api_key_hash=key_hash,
            api_key_prefix=key_prefix,
            rate_limit_per_minute=rate_limit_per_minute,
            metadata=metadata,
        )

        logger.info(f"Created project: {project.name} (id={project.id}, prefix={key_prefix})")

        # Reload cache to include new project
        await project_cache.load_from_db()

        return project, raw_key

    async def get_project(self, project_id: uuid.UUID) -> Optional[Project]:
        """Get a project by ID."""
        return await self.repo.get_by_id(project_id)

    async def get_all_projects(self) -> List[Project]:
        """Get all projects."""
        return await self.repo.get_all()

    async def get_active_projects(self) -> List[Project]:
        """Get all active projects."""
        return await self.repo.get_all_active()

    async def update_project(
        self,
        project_id: uuid.UUID,
        name: Optional[str] = None,
        is_active: Optional[bool] = None,
        rate_limit_per_minute: Optional[int] = None,
        metadata: Optional[dict] = None,
    ) -> Optional[Project]:
        """Update a project."""
        project = await self.repo.update(
            project_id=project_id,
            name=name,
            is_active=is_active,
            rate_limit_per_minute=rate_limit_per_minute,
            metadata=metadata,
        )

        if project:
            logger.info(f"Updated project: {project.name} (id={project.id})")
            # Reload cache
            await project_cache.load_from_db()

        return project

    async def delete_project(self, project_id: uuid.UUID) -> bool:
        """Delete a project."""
        project = await self.repo.get_by_id(project_id)
        if not project:
            return False

        result = await self.repo.delete(project_id)

        if result:
            logger.info(f"Deleted project: {project.name} (id={project_id})")
            # Reload cache
            await project_cache.load_from_db()

        return result

    async def rotate_api_key(self, project_id: uuid.UUID) -> Tuple[Optional[Project], Optional[str]]:
        """
        Rotate (regenerate) a project's API key.

        Returns:
            Tuple of (project, new_raw_api_key)
            Returns (None, None) if project not found
        """
        project = await self.repo.get_by_id(project_id)
        if not project:
            return None, None

        # Generate new API key
        raw_key, key_hash, key_prefix = self.generate_api_key()

        # Update in database
        updated_project = await self.repo.update_api_key(
            project_id=project_id,
            new_api_key_hash=key_hash,
            new_api_key_prefix=key_prefix,
        )

        if updated_project:
            logger.info(f"Rotated API key for project: {project.name} (id={project_id}, new_prefix={key_prefix})")
            # Reload cache
            await project_cache.load_from_db()

        return updated_project, raw_key
