@echo off
title Magic Touch AR - Hand Tracker with Preview
cd /d "%~dp0\.."
echo ===================================================
echo  Starting MediaPipe AR Hand Tracker (with Preview)
echo ===================================================
python Tools\mediapipe_streamer.py --stream-video --preview %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Streamer stopped with error code %ERRORLEVEL%.
    pause
)
