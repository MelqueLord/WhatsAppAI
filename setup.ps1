#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Setup completo do WhatsApp AI Manager
.DESCRIPTION
    Instala pré-requisitos, configura banco de dados e inicia a aplicação
.EXAMPLE
    .\setup.ps1
    .\setup.ps1 -SkipInstall
    .\setup.ps1 -RunOnly
#>

param(
    [switch]$SkipInstall,
    [switch]$RunOnly,
    [switch]$TestOnly
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$DOTNET_VERSION = "10.0"
$NODE_MIN_VERSION = "20"
$POSTGRES_PASSWORD = "postgres"
$POSTGRES_DB = "whatsappai_dev"
$ENCRYPTION_KEY = [Convert]::ToBase64String([byte[]](1..32))

function Write-Step {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Test-Command {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

function Install-DotNet {
    Write-Step "Verificando .NET SDK"

    if (Test-Command "dotnet") {
        $version = dotnet --version
        Write-Host "✅ .NET SDK encontrado: $version" -ForegroundColor Green
        return
    }

    Write-Host "❌ .NET SDK não encontrado" -ForegroundColor Red

    if ($SkipInstall) {
        Write-Host "Instale manualmente: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📦 Instalando .NET 10 SDK..." -ForegroundColor Yellow

    if (Test-Command "winget") {
        winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements
    } else {
        Write-Host "Winget não disponível. Baixe manualmente:" -ForegroundColor Yellow
        Write-Host "https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Cyan
        exit 1
    }

    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")

    if (-not (Test-Command "dotnet")) {
        Write-Host "❌ Instalação falhou. Reinicie o terminal e tente novamente." -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ .NET SDK instalado com sucesso" -ForegroundColor Green
}

function Install-Docker {
    Write-Step "Verificando Docker"

    if (Test-Command "docker") {
        $version = docker --version
        Write-Host "✅ Docker encontrado: $version" -ForegroundColor Green
        return
    }

    Write-Host "❌ Docker não encontrado" -ForegroundColor Red

    if ($SkipInstall) {
        Write-Host "Instale manualmente: https://www.docker.com/products/docker-desktop/" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📦 Instalando Docker Desktop..." -ForegroundColor Yellow

    if (Test-Command "winget") {
        winget install Docker.DockerDesktop --accept-source-agreements --accept-package-agreements
    } else {
        Write-Host "Winget não disponível. Baixe manualmente:" -ForegroundColor Yellow
        Write-Host "https://www.docker.com/products/docker-desktop/" -ForegroundColor Cyan
        exit 1
    }

    Write-Host "⚠️  Reinicie o computador após instalar o Docker Desktop" -ForegroundColor Yellow
    Write-Host "   Depois execute este script novamente" -ForegroundColor Yellow
    exit 0
}

function Install-Node {
    Write-Step "Verificando Node.js"

    if (Test-Command "node") {
        $version = node --version
        Write-Host "✅ Node.js encontrado: $version" -ForegroundColor Green
        return
    }

    Write-Host "❌ Node.js não encontrado" -ForegroundColor Red

    if ($SkipInstall) {
        Write-Host "Instale manualmente: https://nodejs.org/" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📦 Instalando Node.js..." -ForegroundColor Yellow

    if (Test-Command "winget") {
        winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
    } else {
        Write-Host "Winget não disponível. Baixe manualmente:" -ForegroundColor Yellow
        Write-Host "https://nodejs.org/" -ForegroundColor Cyan
        exit 1
    }

    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")

    Write-Host "✅ Node.js instalado com sucesso" -ForegroundColor Green
}

function Start-Postgres {
    Write-Step "Iniciando PostgreSQL via Docker"

    # Check if compose file exists
    if (-not (Test-Path "compose.yaml")) {
        Write-Host "❌ compose.yaml não encontrado" -ForegroundColor Red
        exit 1
    }

    # Start PostgreSQL
    Write-Host "🐘 Iniciando PostgreSQL 18..." -ForegroundColor Yellow
    docker compose up -d postgres

    # Wait for PostgreSQL to be ready
    Write-Host "⏳ Aguardando PostgreSQL ficar pronto..." -ForegroundColor Yellow
    $maxAttempts = 30
    $attempt = 0

    while ($attempt -lt $maxAttempts) {
        $attempt++
        $result = docker compose exec -T postgres pg_isready -U postgres 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ PostgreSQL pronto!" -ForegroundColor Green
            return
        }

        Write-Host "." -NoNewline
        Start-Sleep -Seconds 1
    }

    Write-Host "`n❌ PostgreSQL não ficou pronto a tempo" -ForegroundColor Red
    exit 1
}

function Set-UserSecrets {
    Write-Step "Configurando User Secrets"

    $projectPath = "src/WhatsAppAI.WebApi"

    # Initialize user secrets
    dotnet user-secrets init --project $projectPath 2>$null

    # Set connection string
    $connString = "Host=localhost;Database=$POSTGRES_DB;Username=postgres;Password=$POSTGRES_PASSWORD"
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connString --project $projectPath

    # Set encryption key
    dotnet user-secrets set "Encryption:Key" $ENCRYPTION_KEY --project $projectPath

    # Set Meta secrets (placeholder for development)
    dotnet user-secrets set "Meta:VerifyToken" "dev-verify-token" --project $projectPath
    dotnet user-secrets set "Meta:AppSecret" "dev-app-secret" --project $projectPath

    Write-Host "✅ User Secrets configurado" -ForegroundColor Green
}

function Install-Dependencies {
    Write-Step "Instalando dependências"

    # .NET restore
    Write-Host "📦 Restaurando pacotes .NET..." -ForegroundColor Yellow
    dotnet restore

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha ao restaurar pacotes .NET" -ForegroundColor Red
        exit 1
    }

    # Frontend dependencies
    Write-Host "📦 Instalando dependências do frontend..." -ForegroundColor Yellow
    Push-Location apps/web
    npm install
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha ao instalar dependências do frontend" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Dependências instaladas" -ForegroundColor Green
}

function Initialize-Database {
    Write-Step "Inicializando banco de dados"

    # Create database if not exists
    Write-Host "🐘 Criando banco de dados..." -ForegroundColor Yellow
    docker compose exec -T postgres psql -U postgres -c "CREATE DATABASE $POSTGRES_DB;" 2>$null

    # Run migrations
    Write-Host "🔄 Executando migrations..." -ForegroundColor Yellow
    dotnet ef database update --project src/WhatsAppAI.Infrastructure --startup-project src/WhatsAppAI.WebApi

    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️  Migrations não executadas (pode ser normal na primeira vez)" -ForegroundColor Yellow
        Write-Host "   O banco será criado quando a aplicação iniciar" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Banco de dados inicializado" -ForegroundColor Green
    }
}

function Build-Solution {
    Write-Step "Build da solução"

    Write-Host "🔨 Compilando backend..." -ForegroundColor Yellow
    dotnet build --configuration Release

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha no build do backend" -ForegroundColor Red
        exit 1
    }

    Write-Host "🔨 Compilando frontend..." -ForegroundColor Yellow
    Push-Location apps/web
    npm run build
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha no build do frontend" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Build concluído com sucesso" -ForegroundColor Green
}

function Invoke-Tests {
    Write-Step "Executando testes"

    # Backend tests
    Write-Host "🧪 Executando testes .NET..." -ForegroundColor Yellow
    dotnet test --configuration Release --verbosity normal

    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️  Alguns testes falharam" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Todos os testes passaram" -ForegroundColor Green
    }

    # Frontend tests
    Write-Host "🧪 Executando testes frontend..." -ForegroundColor Yellow
    Push-Location apps/web
    npm test
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️  Alguns testes frontend falharam" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Testes frontend passaram" -ForegroundColor Green
    }
}

function Start-Application {
    Write-Step "Iniciando aplicação"

    Write-Host @"

🚀 WhatsApp AI Manager

   Backend:  http://localhost:5000
   Frontend: http://localhost:5173
   Health:   http://localhost:5000/health/live

   Pressione Ctrl+C para parar

"@ -ForegroundColor Green

    # Start backend in background
    Write-Host "🖥️  Iniciando backend..." -ForegroundColor Yellow
    $backendJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project src/WhatsAppAI.WebApi --configuration Release
    }

    # Start frontend
    Write-Host "🖥️  Iniciando frontend..." -ForegroundColor Yellow
    Push-Location apps/web

    try {
        npm run dev
    } finally {
        Pop-Location
        Stop-Job -Job $backendJob -ErrorAction SilentlyContinue
        Remove-Job -Job $backendJob -Force -ErrorAction SilentlyContinue
    }
}

# ============================================
# MAIN
# ============================================

Write-Host @"

╔═══════════════════════════════════════════════╗
║     WhatsApp AI Manager - Setup Script        ║
╚═══════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Change to script directory
Set-Location $PSScriptRoot

if ($TestOnly) {
    Install-DotNet
    Install-Dependencies
    Invoke-Tests
    exit 0
}

if (-not $RunOnly) {
    Install-DotNet
    Install-Docker
    Install-Node
    Start-Postgres
    Set-UserSecrets
    Install-Dependencies
    Initialize-Database
    Build-Solution
}

if (-not $SkipInstall) {
    Invoke-Tests
}

Start-Application
