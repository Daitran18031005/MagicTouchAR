@echo off
title Magic Touch AR - MediaPipe Hand Tracker
cd /d "%~dp0\.."
echo ===================================================
echo  Starting MediaPipe AR Hand Tracker for Magic Touch
echo ===================================================
set ARGS=%*
if "%ARGS%"=="" set ARGS=--stream-video
python Tools\mediapipe_streamer.py %ARGS%
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Streamer stopped with error code %ERRORLEVEL%.
    pause
)
