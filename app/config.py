"""Application configuration using pydantic-settings."""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    # Freeway API Authentication
    API_KEY: str  # Required - API key for accessing Freeway endpoints

    # OpenRouter API
    OPENROUTER_API_KEY: str = ""  # For future use
    OPENROUTER_BASE_URL: str = "https://openrouter.ai/api/v1"

    # Request Settings
    REQUEST_TIMEOUT_SECONDS: int = 30  # Timeout for API requests

    # Application
    PROJECT_NAME: str = "Freeway"
    HOST: str = "0.0.0.0"
    PORT: int = 8000

    class Config:
        env_file = ".env"
        case_sensitive = True


settings = Settings()
