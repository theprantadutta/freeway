"""Initial schema

Revision ID: 001
Revises:
Create Date: 2024-12-03

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

# revision identifiers, used by Alembic.
revision: str = '001'
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Create initial tables (idempotent - skips if tables exist)."""
    conn = op.get_bind()
    inspector = sa.inspect(conn)
    existing_tables = inspector.get_table_names()

    # Create projects table
    if 'projects' not in existing_tables:
        op.create_table(
            'projects',
            sa.Column('id', sa.UUID(), nullable=False),
            sa.Column('name', sa.String(255), nullable=False),
            sa.Column('api_key_hash', sa.String(255), nullable=False),
            sa.Column('api_key_prefix', sa.String(8), nullable=False),
            sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
            sa.Column('updated_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
            sa.Column('is_active', sa.Boolean(), server_default='true', nullable=False),
            sa.Column('rate_limit_per_minute', sa.Integer(), server_default='60', nullable=False),
            sa.Column('metadata', sa.JSON(), server_default='{}', nullable=True),
            sa.PrimaryKeyConstraint('id'),
            sa.UniqueConstraint('api_key_hash')
        )
        op.create_index('ix_projects_api_key_hash', 'projects', ['api_key_hash'])
        op.create_index('ix_projects_is_active', 'projects', ['is_active'])

    # Create usage_logs table
    if 'usage_logs' not in existing_tables:
        op.create_table(
            'usage_logs',
            sa.Column('id', sa.UUID(), nullable=False),
            sa.Column('project_id', sa.UUID(), nullable=False),
            sa.Column('model_id', sa.String(255), nullable=False),
            sa.Column('model_type', sa.String(10), nullable=False),
            sa.Column('input_tokens', sa.Integer(), server_default='0', nullable=False),
            sa.Column('output_tokens', sa.Integer(), server_default='0', nullable=False),
            sa.Column('response_time_ms', sa.Integer(), nullable=False),
            sa.Column('cost_usd', sa.Numeric(20, 10), server_default='0', nullable=True),
            sa.Column('prompt_cost_per_token', sa.Numeric(20, 15), nullable=True),
            sa.Column('completion_cost_per_token', sa.Numeric(20, 15), nullable=True),
            sa.Column('success', sa.Boolean(), server_default='true', nullable=False),
            sa.Column('error_message', sa.Text(), nullable=True),
            sa.Column('request_id', sa.String(255), nullable=True),
            sa.Column('created_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
            sa.PrimaryKeyConstraint('id'),
            sa.ForeignKeyConstraint(['project_id'], ['projects.id'], ondelete='CASCADE')
        )
        op.create_index('ix_usage_logs_project_id', 'usage_logs', ['project_id'])
        op.create_index('ix_usage_logs_created_at', 'usage_logs', ['created_at'])
        op.create_index('ix_usage_logs_model_id', 'usage_logs', ['model_id'])

    # Create models_cache table
    if 'models_cache' not in existing_tables:
        op.create_table(
            'models_cache',
            sa.Column('id', sa.String(255), nullable=False),
            sa.Column('name', sa.String(255), nullable=False),
            sa.Column('description', sa.Text(), nullable=True),
            sa.Column('context_length', sa.Integer(), nullable=True),
            sa.Column('prompt_price', sa.String(50), nullable=False),
            sa.Column('completion_price', sa.String(50), nullable=False),
            sa.Column('is_free', sa.Boolean(), nullable=False),
            sa.Column('fetched_at', sa.DateTime(timezone=True), server_default=sa.text('now()'), nullable=False),
            sa.PrimaryKeyConstraint('id')
        )


def downgrade() -> None:
    op.drop_table('models_cache')
    op.drop_table('usage_logs')
    op.drop_table('projects')
