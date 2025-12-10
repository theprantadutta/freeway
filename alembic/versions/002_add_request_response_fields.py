"""Add request/response fields to usage_logs

Revision ID: 002
Revises: 001
Create Date: 2024-12-10

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB

# revision identifiers, used by Alembic.
revision: str = '002'
down_revision: Union[str, None] = '001'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Add provider, request/response content, and params fields."""
    op.add_column('usage_logs', sa.Column('provider', sa.String(50), nullable=True))
    op.add_column('usage_logs', sa.Column('request_messages', JSONB, nullable=True))
    op.add_column('usage_logs', sa.Column('response_content', sa.Text(), nullable=True))
    op.add_column('usage_logs', sa.Column('finish_reason', sa.String(50), nullable=True))
    op.add_column('usage_logs', sa.Column('request_params', JSONB, nullable=True))


def downgrade() -> None:
    """Remove added columns."""
    op.drop_column('usage_logs', 'request_params')
    op.drop_column('usage_logs', 'finish_reason')
    op.drop_column('usage_logs', 'response_content')
    op.drop_column('usage_logs', 'request_messages')
    op.drop_column('usage_logs', 'provider')
