@echo off
REM Quick start script - run after initial setup

echo.
echo ========================================
echo   WhatsApp AI Manager - Quick Start
echo ========================================
echo.

echo Starting PostgreSQL...
docker compose up -d postgres

echo Waiting for PostgreSQL...
timeout /t 5 /nobreak > nul

echo Starting Backend...
start "WhatsApp AI Backend" dotnet run --project src/WhatsAppAI.WebApi

echo Starting Frontend...
cd apps/web
start "WhatsApp AI Frontend" npm run dev
cd ..

echo.
echo ========================================
echo   Application is starting!
echo.
echo   Backend:  http://localhost:5000
echo   Frontend: http://localhost:5173
echo.
echo   Press any key to stop all services
echo ========================================
pause > nul

echo Stopping services...
taskkill /FI "WindowTitle eq WhatsApp AI Backend*" /F 2>nul
taskkill /FI "WindowTitle eq WhatsApp AI Frontend*" /F 2>nul
docker compose down
