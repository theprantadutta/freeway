"""OpenRouter API response models."""

from typing import List, Optional

from pydantic import BaseModel


class Pricing(BaseModel):
    """Model pricing information."""

    prompt: str
    completion: str
    request: Optional[str] = None
    image: Optional[str] = None

    def is_free(self) -> bool:
        """Check if model is completely free."""
        return self.prompt == "0" and self.completion == "0"


class Architecture(BaseModel):
    """Model architecture information."""

    modality: Optional[str] = None
    input_modalities: Optional[List[str]] = None
    output_modalities: Optional[List[str]] = None
    tokenizer: Optional[str] = None


class OpenRouterModel(BaseModel):
    """Single model from OpenRouter API."""

    id: str
    name: str
    description: Optional[str] = None
    context_length: Optional[int] = None
    pricing: Pricing
    architecture: Optional[Architecture] = None
    created: Optional[int] = None


class ModelsResponse(BaseModel):
    """Response from OpenRouter /models endpoint."""

    data: List[OpenRouterModel]
