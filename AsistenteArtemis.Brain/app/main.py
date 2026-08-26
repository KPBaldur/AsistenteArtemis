import asyncio
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

# En Windows, correr uvicorn sin forzar UTF-8 hace que las respuestas JSON con
# tildes/eñes queden mal codificadas (doble UTF-8) -- correr con
# PYTHONUTF8=1 y PYTHONIOENCODING=utf-8 en el entorno antes de levantar
# uvicorn. En Linux/Render no hace falta (locale ya es UTF-8 por defecto).

from app.chat import router as chat_router
from app.sync import sync_loop
from app.suggestions import suggestions_loop

logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(app: FastAPI):
    sync_task = asyncio.create_task(sync_loop())
    suggestions_task = asyncio.create_task(suggestions_loop())
    yield
    sync_task.cancel()
    suggestions_task.cancel()


app = FastAPI(title="AsistenteArtemis Brain", lifespan=lifespan)

app.include_router(chat_router)


@app.get("/health")
def health():
    return {"status": "ok"}
