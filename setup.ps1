<#
.SYNOPSIS
    Prepares WhatsApp AI Manager for local development without administrator rights.
.DESCRIPTION
    Uses SQLite, .NET SDK and Node.js already installed for the current user.
    Docker/MySQL remain optional for integration or production-like runs.
#>

[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Require-Command([string]$Name, [string]$InstallUrl) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Host "$Name was not found. Install it for the current user: $InstallUrl" -ForegroundColor Red
        exit 1
    }
}

Write-Host 'WhatsApp AI Manager - local setup (no administrator rights)' -ForegroundColor Cyan
Require-Command 'dotnet' 'https://dotnet.microsoft.com/download/dotnet/10.0'
Require-Command 'node' 'https://nodejs.org/'
Require-Command 'npm' 'https://nodejs.org/'

Write-Host "Using .NET $(& dotnet --version) and Node $(& node --version)" -ForegroundColor Green

& dotnet user-secrets init --project src/WhatsAppAI.WebApi 2>$null
& dotnet user-secrets set 'DatabaseProvider' 'SQLite' --project src/WhatsAppAI.WebApi
& dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Data Source=whatsappai.db' --project src/WhatsAppAI.WebApi
& dotnet user-secrets set 'Encryption:Key' 'AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHiA=' --project src/WhatsAppAI.WebApi
& dotnet user-secrets set 'Meta:VerifyToken' 'dev-verify-token' --project src/WhatsAppAI.WebApi
& dotnet user-secrets set 'Meta:AppSecret' 'dev-app-secret' --project src/WhatsAppAI.WebApi

Write-Host 'Restoring backend dependencies...' -ForegroundColor Yellow
& dotnet restore

Write-Host 'Installing frontend dependencies...' -ForegroundColor Yellow
Push-Location apps/web
try {
    if (Test-Path package-lock.json) { & npm ci } else { & npm install }
}
finally { Pop-Location }

if (Test-Path services/whatsapp-web/package.json) {
    Write-Host 'Installing optional WhatsApp Web bridge dependencies...' -ForegroundColor Yellow
    Push-Location services/whatsapp-web
    try { & npm install }
    finally { Pop-Location }
}

Write-Host 'Building backend...' -ForegroundColor Yellow
& dotnet build src/WhatsAppAI.WebApi/WhatsAppAI.WebApi.csproj --no-restore

Write-Host 'Building frontend...' -ForegroundColor Yellow
Push-Location apps/web
try { & npm run build }
finally { Pop-Location }

if (-not $SkipTests) {
    Write-Host 'Running frontend tests...' -ForegroundColor Yellow
    Push-Location apps/web
    try { & npm test }
    finally { Pop-Location }
}

Write-Host 'Setup completed. SQLite will be created by the API on first run.' -ForegroundColor Green
Write-Host 'Run run.bat or use: dotnet run --project src/WhatsAppAI.WebApi --urls http://localhost:5000' -ForegroundColor Green

if ($Run) {
    & .\run.bat
}
