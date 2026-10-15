@echo off
title Magic Touch AR - Setup Python environment
cd /d "%~dp0\.."
echo Creating virtual environment in .venv ...
python -m venv .venv
if errorlevel 1 (
    echo Failed to create .venv. Install Python 3.9 - 3.12 and make sure "python" is on PATH.
    pause
    exit /b 1
)
call .venv\Scripts\python.exe -m pip install --upgrade pip
call .venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 (
    echo Failed to install requirements.
    pause
    exit /b 1
)
echo.
echo Done. Press Play in Unity - the game will use .venv automatically.
pause
