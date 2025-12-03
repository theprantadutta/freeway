"""Usage repository for database operations."""

import uuid
from datetime import datetime
from decimal import Decimal
from typing import List, Optional

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.database import UsageLog


class UsageRepository:
    """Repository for UsageLog database operations."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def create(
        self,
        project_id: uuid.UUID,
        model_id: str,
        model_type: str,
        input_tokens: int,
        output_tokens: int,
        response_time_ms: int,
        cost_usd: Decimal,
        prompt_cost_per_token: Optional[Decimal] = None,
        completion_cost_per_token: Optional[Decimal] = None,
        success: bool = True,
        error_message: Optional[str] = None,
        request_id: Optional[str] = None,
    ) -> UsageLog:
        """Create a new usage log entry."""
        log = UsageLog(
            project_id=project_id,
            model_id=model_id,
            model_type=model_type,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            response_time_ms=response_time_ms,
            cost_usd=cost_usd,
            prompt_cost_per_token=prompt_cost_per_token,
            completion_cost_per_token=completion_cost_per_token,
            success=success,
            error_message=error_message,
            request_id=request_id,
        )
        self.session.add(log)
        await self.session.commit()
        return log

    async def get_by_project(
        self,
        project_id: uuid.UUID,
        start_date: Optional[datetime] = None,
        end_date: Optional[datetime] = None,
        limit: int = 100,
        offset: int = 0,
    ) -> List[UsageLog]:
        """Get usage logs for a project with optional date filtering."""
        query = select(UsageLog).where(UsageLog.project_id == project_id)

        if start_date:
            query = query.where(UsageLog.created_at >= start_date)
        if end_date:
            query = query.where(UsageLog.created_at <= end_date)

        query = query.order_by(UsageLog.created_at.desc()).limit(limit).offset(offset)

        result = await self.session.execute(query)
        return list(result.scalars().all())

    async def get_project_stats(
        self,
        project_id: uuid.UUID,
        start_date: Optional[datetime] = None,
        end_date: Optional[datetime] = None,
    ) -> dict:
        """Get aggregated statistics for a project."""
        query = select(
            func.count(UsageLog.id).label("total_requests"),
            func.sum(func.cast(UsageLog.success, int)).label("successful_requests"),
            func.sum(UsageLog.input_tokens).label("total_input_tokens"),
            func.sum(UsageLog.output_tokens).label("total_output_tokens"),
            func.sum(UsageLog.cost_usd).label("total_cost_usd"),
            func.avg(UsageLog.response_time_ms).label("avg_response_time_ms"),
        ).where(UsageLog.project_id == project_id)

        if start_date:
            query = query.where(UsageLog.created_at >= start_date)
        if end_date:
            query = query.where(UsageLog.created_at <= end_date)

        result = await self.session.execute(query)
        row = result.one()

        return {
            "total_requests": row.total_requests or 0,
            "successful_requests": int(row.successful_requests or 0),
            "failed_requests": (row.total_requests or 0) - int(row.successful_requests or 0),
            "total_input_tokens": int(row.total_input_tokens or 0),
            "total_output_tokens": int(row.total_output_tokens or 0),
            "total_cost_usd": float(row.total_cost_usd or 0),
            "avg_response_time_ms": int(row.avg_response_time_ms or 0),
        }

    async def get_stats_by_model(
        self,
        project_id: uuid.UUID,
        start_date: Optional[datetime] = None,
        end_date: Optional[datetime] = None,
    ) -> List[dict]:
        """Get statistics grouped by model for a project."""
        query = select(
            UsageLog.model_id,
            UsageLog.model_type,
            func.count(UsageLog.id).label("requests"),
            func.sum(UsageLog.input_tokens + UsageLog.output_tokens).label("tokens"),
            func.sum(UsageLog.cost_usd).label("cost_usd"),
        ).where(UsageLog.project_id == project_id)

        if start_date:
            query = query.where(UsageLog.created_at >= start_date)
        if end_date:
            query = query.where(UsageLog.created_at <= end_date)

        query = query.group_by(UsageLog.model_id, UsageLog.model_type)

        result = await self.session.execute(query)
        rows = result.all()

        return [
            {
                "model_id": row.model_id,
                "model_type": row.model_type,
                "requests": row.requests,
                "tokens": int(row.tokens or 0),
                "cost_usd": float(row.cost_usd or 0),
            }
            for row in rows
        ]

    async def get_global_stats(
        self,
        start_date: Optional[datetime] = None,
        end_date: Optional[datetime] = None,
    ) -> dict:
        """Get global statistics across all projects."""
        query = select(
            func.count(UsageLog.id).label("total_requests"),
            func.sum(UsageLog.cost_usd).label("total_cost_usd"),
        )

        if start_date:
            query = query.where(UsageLog.created_at >= start_date)
        if end_date:
            query = query.where(UsageLog.created_at <= end_date)

        result = await self.session.execute(query)
        row = result.one()

        return {
            "total_requests": row.total_requests or 0,
            "total_cost_usd": float(row.total_cost_usd or 0),
        }
