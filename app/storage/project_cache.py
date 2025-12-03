"""Thread-safe in-memory cache for project API keys."""

import logging
from dataclasses import dataclass
from threading import RLock
from typing import Dict, Optional

import bcrypt

logger = logging.getLogger(__name__)


@dataclass
class ProjectInfo:
    """Cached project information."""

    id: str
    name: str
    rate_limit_per_minute: int
    is_active: bool


class ProjectCache:
    """
    Thread-safe in-memory cache for validating project API keys.

    API keys are stored as bcrypt hashes in the database.
    This cache stores the hashes for fast validation without DB queries.
    """

    def __init__(self):
        self._lock = RLock()
        # Map of api_key_hash -> ProjectInfo
        self._projects: Dict[str, ProjectInfo] = {}
        self._loaded = False

    async def load_from_db(self) -> int:
        """
        Load all active projects from database into cache.
        Returns the number of projects loaded.
        """
        from app.db.connection import async_session_maker
        from app.db.repositories.project_repo import ProjectRepository

        try:
            async with async_session_maker() as session:
                repo = ProjectRepository(session)
                projects = await repo.get_all_active()

            with self._lock:
                self._projects.clear()
                for p in projects:
                    self._projects[p.api_key_hash] = ProjectInfo(
                        id=str(p.id),
                        name=p.name,
                        rate_limit_per_minute=p.rate_limit_per_minute,
                        is_active=p.is_active,
                    )
                self._loaded = True

            count = len(self._projects)
            logger.info(f"Loaded {count} projects into cache")
            return count

        except Exception as e:
            logger.error(f"Failed to load projects from database: {e}")
            raise

    def validate(self, api_key: str) -> Optional[ProjectInfo]:
        """
        Validate an API key and return project info if valid.

        This method checks the key against all cached hashes using bcrypt.
        Returns None if the key is invalid.
        """
        if not api_key:
            return None

        with self._lock:
            for key_hash, project in self._projects.items():
                if not project.is_active:
                    continue
                try:
                    if bcrypt.checkpw(api_key.encode(), key_hash.encode()):
                        return project
                except Exception:
                    # Invalid hash format, skip
                    continue

        return None

    def get_project_by_id(self, project_id: str) -> Optional[ProjectInfo]:
        """Get a project from cache by its ID."""
        with self._lock:
            for project in self._projects.values():
                if project.id == project_id:
                    return project
        return None

    def invalidate(self) -> None:
        """Clear the cache."""
        with self._lock:
            self._projects.clear()
            self._loaded = False
        logger.info("Project cache invalidated")

    @property
    def is_loaded(self) -> bool:
        """Check if cache has been loaded."""
        with self._lock:
            return self._loaded

    @property
    def project_count(self) -> int:
        """Get the number of cached projects."""
        with self._lock:
            return len(self._projects)


# Global singleton instance
project_cache = ProjectCache()
