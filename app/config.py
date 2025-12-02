"""Application configuration using pydantic-settings."""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""

    # Freeway API Authentication
    API_KEY: str  # Required - API key for accessing Freeway endpoints

    # OpenRouter API
    OPENROUTER_API_KEY: str = ""  # Optional - only needed for health checks
    OPENROUTER_BASE_URL: str = "https://openrouter.ai/api/v1"

    # Health Check Settings
    HEALTH_CHECK_ENABLED: bool = True  # Set to False to disable health checks
    HEALTH_CHECK_HOUR: int = 0  # Hour to run daily health check (0-23, default midnight UTC)
    HISTORY_SIZE: int = 20  # Last N results per model
    REQUEST_TIMEOUT_SECONDS: int = 30  # Timeout for API requests
    CHECK_DELAY_SECONDS: float = 60.0  # Delay between checks (60s for free models)

    # Test prompt for health checks
    TEST_PROMPT: str = "Say hi"

    # Application
    PROJECT_NAME: str = "Freeway"
    HOST: str = "0.0.0.0"
    PORT: int = 8000

    class Config:
        env_file = ".env"
        case_sensitive = True


settings = Settings()
