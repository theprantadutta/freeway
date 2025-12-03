"""Application configuration using pydantic-settings."""

from typing import Optional

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    # Admin API Authentication (for Flutter control panel)
    ADMIN_API_KEY: str  # Required - Admin API key for control panel

    # OpenRouter API
    OPENROUTER_API_KEY: str = ""  # Required for chat completions
    OPENROUTER_BASE_URL: str = "https://openrouter.ai/api/v1"

    # Database Configuration
    DATABASE_URL: Optional[str] = None  # Full connection string
    # Or individual params (used if DATABASE_URL is not set)
    DB_HOST: str = "localhost"
    DB_PORT: int = 5432
    DB_USER: str = "freeway"
    DB_PASSWORD: str = ""
    DB_NAME: str = "freeway"

    # Request Settings
    REQUEST_TIMEOUT_SECONDS: int = 30  # Timeout for model API requests
    COMPLETION_TIMEOUT_SECONDS: int = 120  # Longer timeout for chat completions

    # API Key Settings
    API_KEY_PREFIX: str = "fw_"  # Prefix for generated project API keys

    # Application
    PROJECT_NAME: str = "Freeway"
    HOST: str = "0.0.0.0"
    PORT: int = 8000

    @property
    def database_url(self) -> str:
        """Get the database URL, constructing from parts if not provided directly."""
        if self.DATABASE_URL:
            return self.DATABASE_URL
        return f"postgresql+asyncpg://{self.DB_USER}:{self.DB_PASSWORD}@{self.DB_HOST}:{self.DB_PORT}/{self.DB_NAME}"

    class Config:
        env_file = ".env"
        case_sensitive = True


settings = Settings()
