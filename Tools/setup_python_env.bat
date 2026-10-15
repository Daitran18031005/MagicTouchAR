@echo off
setlocal EnableExtensions
title Magic Touch AR - Setup Python environment
rem Creates .venv in the project root with Python 3.11 and installs Tools\requirements.txt.
rem Usage:
rem   Tools\setup_python_env.bat                      auto: "py -3.11", then "python" on PATH if it is 3.11
rem   Tools\setup_python_env.bat C:\path\python.exe   use this Python 3.11 interpreter (e.g. a conda env)
rem Set MT_NO_PAUSE=1 to skip the final "pause" (automation).
cd /d "%~dp0\.."
set "PY_CMD="

if not "%~1"=="" (
    call :try_python "%~1"
    if not defined PY_CMD (
        echo [ERROR] "%~1" is not a Python 3.11 interpreter.
        goto :no_python
    )
)
if not defined PY_CMD call :try_python py -3.11
if not defined PY_CMD call :try_python python
if not defined PY_CMD goto :no_python
echo Using Python 3.11: %PY_CMD%

if exist ".venv\Scripts\python.exe" (
    ".venv\Scripts\python.exe" -c "import sys; sys.exit(0 if sys.version_info[:2] == (3, 11) else 1)" >nul 2>&1
    if errorlevel 1 (
        echo [ERROR] .venv already exists but is not a working Python 3.11 environment.
        echo         Delete the .venv folder in the project root, then run this script again.
        goto :fail
    )
    echo Reusing existing .venv ^(Python 3.11^).
) else (
    echo Creating virtual environment in .venv ...
    %PY_CMD% -m venv .venv
    if errorlevel 1 (
        echo [ERROR] Could not create .venv with %PY_CMD%.
        goto :fail
    )
)

".venv\Scripts\python.exe" -m pip install --upgrade pip
".venv\Scripts\python.exe" -m pip install -r Tools\requirements.txt
if errorlevel 1 (
    echo [ERROR] Failed to install Tools\requirements.txt.
    goto :fail
)

".venv\Scripts\python.exe" -c "import cv2, mediapipe, numpy; print('OK: cv2', cv2.__version__, '| mediapipe', mediapipe.__version__, '| numpy', numpy.__version__)"
if errorlevel 1 (
    echo [ERROR] .venv was created but "import cv2, mediapipe, numpy" failed.
    goto :fail
)

echo.
echo Done: .venv is ready ^(Python 3.11^).
if not defined MT_NO_PAUSE pause
exit /b 0

:try_python
%* -c "import sys; sys.exit(0 if sys.version_info[:2] == (3, 11) else 1)" >nul 2>&1
if not errorlevel 1 set "PY_CMD=%*"
exit /b 0

:no_python
echo [ERROR] Python 3.11 was not found.
echo         Install Python 3.11 from https://www.python.org/downloads/ ^(tick "py launcher"^),
echo         or activate a Python 3.11 conda env first ^(conda activate magictouch^),
echo         or pass the interpreter path: Tools\setup_python_env.bat C:\path\to\python.exe
goto :fail

:fail
echo.
if not defined MT_NO_PAUSE pause
exit /b 1
