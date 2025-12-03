"""API authentication dependencies."""

from typing import Union

from fastapi import HTTPException, Security
from fastapi.security import APIKeyHeader

from app.config import settings
from app.storage.project_cache import ProjectInfo, project_cache

# Define the API key header
api_key_header = APIKeyHeader(name="X-Api-Key", auto_error=False)


async def require_admin_key(api_key: str = Security(api_key_header)) -> str:
    """
    Dependency that validates the admin API key.
    Use for admin-only endpoints (project management, analytics).
    Raises 401 if missing or invalid.
    """
    if not api_key or api_key != settings.ADMIN_API_KEY:
        raise HTTPException(
            status_code=401,
            detail="Unauthorized",
        )
    return api_key


async def require_project_key(api_key: str = Security(api_key_header)) -> ProjectInfo:
    """
    Dependency that validates a project API key.
    Use for project-specific endpoints (chat completions).
    Raises 401 if missing or invalid.
    Returns ProjectInfo with project details.
    """
    if not api_key:
        raise HTTPException(
            status_code=401,
            detail="Unauthorized",
        )

    project = project_cache.validate(api_key)
    if not project:
        raise HTTPException(
            status_code=401,
            detail="Unauthorized",
        )

    return project


async def require_any_key(api_key: str = Security(api_key_header)) -> Union[str, ProjectInfo]:
    """
    Dependency that accepts either admin or project API key.
    Use for endpoints accessible by both (model info, health).
    Raises 401 if missing or invalid.
    Returns either "admin" string or ProjectInfo.
    """
    if not api_key:
        raise HTTPException(
            status_code=401,
            detail="Unauthorized",
        )

    # Check admin key first (faster, no hashing)
    if api_key == settings.ADMIN_API_KEY:
        return "admin"

    # Check project keys
    project = project_cache.validate(api_key)
    if project:
        return project

    raise HTTPException(
        status_code=401,
        detail="Unauthorized",
    )


# Alias for backwards compatibility
require_api_key = require_any_key
