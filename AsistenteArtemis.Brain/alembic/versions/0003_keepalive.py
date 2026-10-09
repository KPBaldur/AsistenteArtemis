"""tabla keepalive + rol keepalive_bot (ping diario para que Supabase no pause el proyecto)

Revision ID: 0003
Revises: 0002
Create Date: 2026-10-09

"""
from alembic import op
import sqlalchemy as sa

revision = "0003"
down_revision = "0002"
branch_labels = None
depends_on = None


def upgrade():
    # El plan gratuito de Supabase pausa el proyecto tras ~7 dias sin actividad,
    # y abrir una conexion no cuenta: hace falta una escritura. Un workflow
    # diario de GitHub Actions hace UPDATE sobre esta fila unica.
    op.create_table(
        "keepalive",
        sa.Column("id", sa.Integer, primary_key=True),
        sa.Column("last_ping", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.CheckConstraint("id = 1", name="ck_keepalive_single_row"),
    )
    op.execute("INSERT INTO keepalive (id) VALUES (1)")

    # Rol de permisos minimos: solo leer/actualizar esta tabla. Se crea sin
    # LOGIN; la contrasena se le asigna a mano desde el SQL Editor de Supabase
    # (ALTER ROLE keepalive_bot WITH LOGIN PASSWORD '...') para que nunca quede
    # en el repo.
    op.execute("""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'keepalive_bot') THEN
                CREATE ROLE keepalive_bot NOLOGIN;
            END IF;
        END
        $$;
    """)
    op.execute("GRANT USAGE ON SCHEMA public TO keepalive_bot")
    op.execute("GRANT SELECT, UPDATE (last_ping) ON keepalive TO keepalive_bot")

    # Supabase expone el esquema public por su API REST y da privilegios por
    # defecto a anon/authenticated en tablas nuevas: se los quitamos y se
    # habilita RLS con una unica politica para keepalive_bot (el UPDATE ... WHERE
    # tambien necesita pasar la politica de lectura).
    op.execute("REVOKE ALL ON keepalive FROM anon, authenticated")
    op.execute("ALTER TABLE keepalive ENABLE ROW LEVEL SECURITY")
    op.execute(
        "CREATE POLICY keepalive_bot_ping ON keepalive FOR ALL TO keepalive_bot "
        "USING (true) WITH CHECK (true)"
    )


def downgrade():
    op.drop_table("keepalive")
    op.execute("REVOKE USAGE ON SCHEMA public FROM keepalive_bot")
    op.execute("DROP ROLE IF EXISTS keepalive_bot")
