@echo off
setlocal enabledelayedexpansion

:: Replace these variables as needed
set NGROK_PATH=ngrok.exe
set NGROK_PORT=5249

:: Start ngrok without hiding the console
start "" /B "!NGROK_PATH!" http !NGROK_PORT!

echo Ngrok started for port !NGROK_PORT!
pause  :: This keeps the batch file window open
