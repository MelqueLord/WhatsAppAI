@echo off
REM Quick start script - run after initial setup

echo.
echo ========================================
echo   WhatsApp AI Manager - Quick Start
echo ========================================
echo.

echo Starting MySQL...
docker compose up -d mysql

echo Waiting for MySQL...
timeout /t 5 /nobreak > nul

echo Starting Backend...
start "WhatsApp AI Backend" dotnet run --project src/WhatsAppAI.WebApi

echo Starting Frontend...
start "WhatsApp AI Frontend" cmd /c "cd apps\web && npm run dev"

echo.
echo ========================================
echo   Application is starting!
echo.
echo   Backend:  http://localhost:5179
echo   Frontend: http://localhost:5173
echo.
echo   Press any key to stop all services
echo ========================================
pause > nul

echo Stopping services...
taskkill /FI "WindowTitle eq WhatsApp AI Backend*" /F 2>nul
taskkill /FI "WindowTitle eq WhatsApp AI Frontend*" /F 2>nul
docker compose down
