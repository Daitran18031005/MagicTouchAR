"""Magic Touch leaderboard server.

Đăng ký / đăng nhập (JWT), gửi điểm (tài khoản hoặc khách nhập tên) và bảng xếp hạng.
Chạy: uvicorn app.main:app --host 0.0.0.0 --port 8000
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import os
import re
import secrets
from datetime import datetime, timedelta, timezone
from pathlib import Path

import jwt
from fastapi import Depends, FastAPI, Header, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field
from sqlalchemy import (DateTime, ForeignKey, Integer, String, create_engine, func,
                        select)
from sqlalchemy.orm import (DeclarativeBase, Mapped, Session, mapped_column,
                            sessionmaker)

# ---------------------------------------------------------------- cấu hình
DATABASE_URL = os.getenv("DATABASE_URL", "sqlite:///./data/magictouch.db")
# Một số cloud (Render, Heroku) cấp URL dạng postgres:// -> đổi sang driver psycopg 3.
if DATABASE_URL.startswith("postgres://"):
    DATABASE_URL = "postgresql+psycopg://" + DATABASE_URL[len("postgres://"):]
elif DATABASE_URL.startswith("postgresql://"):
    DATABASE_URL = "postgresql+psycopg://" + DATABASE_URL[len("postgresql://"):]

JWT_SECRET = os.getenv("JWT_SECRET") or secrets.token_urlsafe(32)  # đặt cố định trên cloud!
JWT_DAYS = int(os.getenv("JWT_DAYS", "30"))
GAME_API_KEY = os.getenv("GAME_API_KEY", "")  # rỗng = không kiểm tra
MAX_SCORE = int(os.getenv("MAX_SCORE", "1000000"))
ALLOWED_MODES = {"solo", "versus"}
NAME_RE = re.compile(r"^[\w .\-]{2,20}$", re.UNICODE)
USER_RE = re.compile(r"^[A-Za-z0-9_.]{3,20}$")

if DATABASE_URL.startswith("sqlite"):
    Path("data").mkdir(exist_ok=True)
    engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
else:
    engine = create_engine(DATABASE_URL, pool_pre_ping=True)
SessionLocal = sessionmaker(engine, expire_on_commit=False)


# ---------------------------------------------------------------- bảng
class Base(DeclarativeBase):
    pass


class User(Base):
    __tablename__ = "users"
    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    username: Mapped[str] = mapped_column(String(20), unique=True, index=True)
    display_name: Mapped[str] = mapped_column(String(20), unique=True, index=True)
    password_hash: Mapped[str] = mapped_column(String(200))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))


class Score(Base):
    __tablename__ = "scores"
    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id"), nullable=True, index=True)
    player_name: Mapped[str] = mapped_column(String(20), index=True)
    score: Mapped[int] = mapped_column(Integer, index=True)
    mode: Mapped[str] = mapped_column(String(10), default="solo", index=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))


Base.metadata.create_all(engine)


# ---------------------------------------------------------------- mật khẩu + token
def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 200_000)
    return "pbkdf2$" + base64.b64encode(salt).decode() + "$" + base64.b64encode(digest).decode()


def verify_password(password: str, stored: str) -> bool:
    try:
        _, salt_b64, digest_b64 = stored.split("$")
        digest = hashlib.pbkdf2_hmac("sha256", password.encode(), base64.b64decode(salt_b64), 200_000)
        return hmac.compare_digest(digest, base64.b64decode(digest_b64))
    except ValueError:
        return False


def make_token(user: User) -> str:
    payload = {"sub": str(user.id), "exp": datetime.now(timezone.utc) + timedelta(days=JWT_DAYS)}
    return jwt.encode(payload, JWT_SECRET, algorithm="HS256")


def get_db():
    with SessionLocal() as db:
        yield db


def optional_user(authorization: str | None = Header(default=None), db: Session = Depends(get_db)) -> User | None:
    if not authorization or not authorization.lower().startswith("bearer "):
        return None
    try:
        payload = jwt.decode(authorization[7:], JWT_SECRET, algorithms=["HS256"])
    except jwt.PyJWTError:
        raise HTTPException(401, "Phiên đăng nhập hết hạn, hãy đăng nhập lại")
    user = db.get(User, int(payload["sub"]))
    if user is None:
        raise HTTPException(401, "Tài khoản không tồn tại")
    return user


def require_user(user: User | None = Depends(optional_user)) -> User:
    if user is None:
        raise HTTPException(401, "Cần đăng nhập")
    return user


def check_game_key(x_game_key: str | None = Header(default=None)):
    if GAME_API_KEY and not hmac.compare_digest(x_game_key or "", GAME_API_KEY):
        raise HTTPException(403, "Sai game key")


# ---------------------------------------------------------------- schema
class RegisterIn(BaseModel):
    username: str = Field(min_length=3, max_length=20)
    password: str = Field(min_length=6, max_length=72)
    display_name: str | None = Field(default=None, max_length=20)


class LoginIn(BaseModel):
    username: str
    password: str


class AuthOut(BaseModel):
    token: str
    username: str
    display_name: str


class ScoreIn(BaseModel):
    score: int = Field(ge=0)
    mode: str = "solo"
    player_name: str | None = None  # bắt buộc khi chơi khách


class ScoreOut(BaseModel):
    id: int
    player_name: str
    score: int
    mode: str
    rank: int
    best: int


class BoardRow(BaseModel):
    rank: int
    player_name: str
    best_score: int
    games: int
    registered: bool
    last_played: datetime


# ---------------------------------------------------------------- app
app = FastAPI(title="Magic Touch Leaderboard", version="1.0.0")
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])
STATIC_DIR = Path(__file__).parent / "static"


@app.get("/", include_in_schema=False)
def index():
    return FileResponse(STATIC_DIR / "index.html")


@app.get("/health")
def health():
    return {"status": "ok"}


def _name_taken(db: Session, name: str, exclude_user: int | None = None) -> bool:
    q = select(User.id).where(func.lower(User.display_name) == name.lower())
    uid = db.scalar(q)
    return uid is not None and uid != exclude_user


@app.post("/api/auth/register", response_model=AuthOut)
def register(body: RegisterIn, db: Session = Depends(get_db)):
    username = body.username.strip()
    display = (body.display_name or username).strip()
    if not USER_RE.match(username):
        raise HTTPException(400, "Tên đăng nhập 3-20 ký tự, chỉ gồm chữ không dấu, số, _ hoặc .")
    if not NAME_RE.match(display):
        raise HTTPException(400, "Tên hiển thị 2-20 ký tự")
    if db.scalar(select(User.id).where(func.lower(User.username) == username.lower())):
        raise HTTPException(409, "Tên đăng nhập đã tồn tại")
    if _name_taken(db, display):
        raise HTTPException(409, "Tên hiển thị đã có người dùng")
    user = User(username=username, display_name=display, password_hash=hash_password(body.password))
    db.add(user)
    db.commit()
    return AuthOut(token=make_token(user), username=user.username, display_name=user.display_name)


@app.post("/api/auth/login", response_model=AuthOut)
def login(body: LoginIn, db: Session = Depends(get_db)):
    user = db.scalar(select(User).where(func.lower(User.username) == body.username.strip().lower()))
    if user is None or not verify_password(body.password, user.password_hash):
        raise HTTPException(401, "Sai tên đăng nhập hoặc mật khẩu")
    return AuthOut(token=make_token(user), username=user.username, display_name=user.display_name)


@app.get("/api/me")
def me(user: User = Depends(require_user), db: Session = Depends(get_db)):
    best = db.scalar(select(func.max(Score.score)).where(Score.user_id == user.id)) or 0
    games = db.scalar(select(func.count(Score.id)).where(Score.user_id == user.id)) or 0
    return {"username": user.username, "display_name": user.display_name, "best_score": best, "games": games}


@app.get("/api/me/scores")
def my_scores(limit: int = Query(20, ge=1, le=100), user: User = Depends(require_user), db: Session = Depends(get_db)):
    rows = db.scalars(select(Score).where(Score.user_id == user.id).order_by(Score.created_at.desc()).limit(limit))
    return [{"score": s.score, "mode": s.mode, "created_at": s.created_at} for s in rows]


@app.post("/api/scores", response_model=ScoreOut, dependencies=[Depends(check_game_key)])
def submit_score(body: ScoreIn, user: User | None = Depends(optional_user), db: Session = Depends(get_db)):
    mode = body.mode.lower()
    if mode not in ALLOWED_MODES:
        raise HTTPException(400, "mode phải là solo hoặc versus")
    if body.score > MAX_SCORE:
        raise HTTPException(400, "Điểm không hợp lệ")
    if user is not None:
        name = user.display_name
    else:
        name = (body.player_name or "").strip()
        if not NAME_RE.match(name):
            raise HTTPException(400, "Tên người chơi 2-20 ký tự")
        if _name_taken(db, name):
            raise HTTPException(409, "Tên này thuộc một tài khoản đã đăng ký, hãy đăng nhập hoặc chọn tên khác")
    row = Score(user_id=user.id if user else None, player_name=name, score=body.score, mode=mode)
    db.add(row)
    db.commit()
    best = db.scalar(select(func.max(Score.score)).where(Score.player_name == name, Score.mode == mode)) or 0
    better = _best_per_player(mode).subquery()
    rank = (db.scalar(select(func.count()).select_from(better).where(better.c.best_score > best)) or 0) + 1
    return ScoreOut(id=row.id, player_name=name, score=row.score, mode=mode, rank=rank, best=best)


def _best_per_player(mode: str):
    return (select(Score.player_name,
                   func.max(Score.score).label("best_score"),
                   func.count(Score.id).label("games"),
                   func.max(Score.user_id).label("uid"),
                   func.max(Score.created_at).label("last_played"))
            .where(Score.mode == mode)
            .group_by(Score.player_name))


@app.get("/api/leaderboard", response_model=list[BoardRow])
def leaderboard(mode: str = "solo", limit: int = Query(20, ge=1, le=100), db: Session = Depends(get_db)):
    sub = _best_per_player(mode.lower()).subquery()
    rows = db.execute(select(sub).order_by(sub.c.best_score.desc(), sub.c.last_played.asc()).limit(limit)).all()
    return [BoardRow(rank=i + 1, player_name=r.player_name, best_score=r.best_score, games=r.games,
                     registered=r.uid is not None, last_played=r.last_played) for i, r in enumerate(rows)]
