"""OpenAI-compatible chat completion schemas."""

import time
import uuid
from typing import List, Literal, Optional

from pydantic import BaseModel, Field


class ChatMessage(BaseModel):
    """A single chat message."""

    role: Literal["system", "user", "assistant"] = Field(
        ..., description="The role of the message author"
    )
    content: str = Field(..., description="The content of the message")


class ChatCompletionRequest(BaseModel):
    """OpenAI-compatible chat completion request."""

    model: str = Field(
        default="free",
        description="Model to use: 'free', 'paid', or a specific model ID",
    )
    messages: List[ChatMessage] = Field(
        ..., description="List of messages in the conversation"
    )
    temperature: Optional[float] = Field(
        default=1.0,
        ge=0.0,
        le=2.0,
        description="Sampling temperature",
    )
    max_tokens: Optional[int] = Field(
        default=None,
        gt=0,
        description="Maximum tokens to generate",
    )
    top_p: Optional[float] = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
        description="Nucleus sampling parameter",
    )
    frequency_penalty: Optional[float] = Field(
        default=0.0,
        ge=-2.0,
        le=2.0,
        description="Frequency penalty",
    )
    presence_penalty: Optional[float] = Field(
        default=0.0,
        ge=-2.0,
        le=2.0,
        description="Presence penalty",
    )
    stop: Optional[List[str]] = Field(
        default=None,
        description="Stop sequences",
    )
    stream: bool = Field(
        default=False,
        description="Whether to stream the response",
    )


class ChatChoice(BaseModel):
    """A single completion choice."""

    index: int
    message: ChatMessage
    finish_reason: Optional[str] = None


class UsageInfo(BaseModel):
    """Token usage information."""

    prompt_tokens: int
    completion_tokens: int
    total_tokens: int


class ChatCompletionResponse(BaseModel):
    """OpenAI-compatible chat completion response."""

    id: str = Field(default_factory=lambda: f"chatcmpl-{uuid.uuid4().hex[:12]}")
    object: Literal["chat.completion"] = "chat.completion"
    created: int = Field(default_factory=lambda: int(time.time()))
    model: str
    choices: List[ChatChoice]
    usage: UsageInfo


class ChatCompletionError(BaseModel):
    """Error response for chat completion."""

    error: str
    message: str
    model: Optional[str] = None
