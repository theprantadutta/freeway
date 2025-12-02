"""API authentication dependency."""

from fastapi import HTTPException, Security
from fastapi.security import APIKeyHeader

from app.config import settings

# Define the API key header
api_key_header = APIKeyHeader(name="X-Api-Key", auto_error=False)


async def require_api_key(api_key: str = Security(api_key_header)) -> str:
    """
    Dependency that validates the API key from X-Api-Key header.
    Raises 401 if missing or invalid.
    """
    if not api_key:
        raise HTTPException(
            status_code=401,
            detail="Missing API key. Provide X-Api-Key header.",
        )

    if api_key != settings.API_KEY:
        raise HTTPException(
            status_code=401,
            detail="Invalid API key.",
        )

    return api_key
