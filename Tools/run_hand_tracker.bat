@echo off
title Magic Touch AR - MediaPipe Hand Tracker
cd /d "%~dp0\.."
echo ===================================================
echo  Starting MediaPipe AR Hand Tracker for Magic Touch
echo ===================================================
python Tools\mediapipe_streamer.py %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Streamer stopped with error code %ERRORLEVEL%.
    pause
)
