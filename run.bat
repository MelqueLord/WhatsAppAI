@echo off
REM Quick start script - run after initial setup

echo.
echo ========================================
echo   WhatsApp AI Manager - Quick Start
echo ========================================
echo.

echo Starting Backend...
start "WhatsApp AI Backend" cmd /c "set DatabaseProvider=SQLite&& dotnet run --project src\WhatsAppAI.WebApi --urls http://localhost:5000"

echo Starting Frontend...
start "WhatsApp AI Frontend" cmd /c "cd apps\web && npm run dev"

if exist "services\whatsapp-web\node_modules" (
	echo Starting WhatsApp Web bridge...
	start "WhatsApp AI WhatsApp Bridge" cmd /c "cd services\whatsapp-web && npm run dev"
) else (
	echo WhatsApp Web bridge skipped: run npm install in services\whatsapp-web to enable QR Code.
)

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
taskkill /FI "WindowTitle eq WhatsApp AI WhatsApp Bridge*" /F 2>nul
