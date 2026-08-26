"""tabla note_embeddings + indice hnsw para busqueda por similitud coseno

Revision ID: 0001
Revises:
Create Date: 2026-08-26

"""
from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import UUID
from pgvector.sqlalchemy import Vector

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None

EMBEDDING_DIM = 768


def upgrade():
    # La extension "vector" ya esta habilitada en Supabase (migracion EF Core
    # EnablePgVector del lado TodoAppRoll.Api) -- no hace falta volver a crearla.
    op.create_table(
        "note_embeddings",
        sa.Column("id", UUID(as_uuid=True), primary_key=True),
        sa.Column("note_id", UUID(as_uuid=True), nullable=False),
        sa.Column("user_id", UUID(as_uuid=True), nullable=False),
        sa.Column("chunk_index", sa.Integer, nullable=False),
        sa.Column("chunk_text", sa.Text, nullable=False),
        sa.Column("embedding", Vector(EMBEDDING_DIM), nullable=False),
        sa.Column("note_updated_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
    )
    op.create_index("ix_note_embeddings_note_id", "note_embeddings", ["note_id"])
    op.create_index("ix_note_embeddings_user_id", "note_embeddings", ["user_id"])
    op.execute(
        "CREATE INDEX ix_note_embeddings_embedding_hnsw ON note_embeddings "
        "USING hnsw (embedding vector_cosine_ops)"
    )


def downgrade():
    op.drop_table("note_embeddings")
