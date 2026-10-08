"""tabla vault_chunks (cerebro de proyectos BrainObsidian) + indice hnsw

Revision ID: 0002
Revises: 0001
Create Date: 2026-10-08

"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import UUID
from pgvector.sqlalchemy import Vector

revision = "0002"
down_revision = "0001"
branch_labels = None
depends_on = None

EMBEDDING_DIM = 768


def upgrade():
    op.create_table(
        "vault_chunks",
        sa.Column("id", UUID(as_uuid=True), primary_key=True),
        sa.Column("path", sa.Text, nullable=False),
        sa.Column("doc_type", sa.Text, nullable=True),
        sa.Column("project", sa.Text, nullable=True),
        sa.Column("heading", sa.Text, nullable=True),
        sa.Column("chunk_index", sa.Integer, nullable=False),
        sa.Column("chunk_text", sa.Text, nullable=False),
        sa.Column("embedding", Vector(EMBEDDING_DIM), nullable=False),
        sa.Column("content_hash", sa.Text, nullable=False),
        sa.Column("indexed_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
    )
    op.create_index("ix_vault_chunks_path", "vault_chunks", ["path"])
    op.create_index("ix_vault_chunks_project", "vault_chunks", ["project"])
    op.execute(
        "CREATE INDEX ix_vault_chunks_embedding_hnsw ON vault_chunks "
        "USING hnsw (embedding vector_cosine_ops)"
    )


def downgrade():
    op.drop_table("vault_chunks")
