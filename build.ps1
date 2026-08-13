#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== Restoring .NET dependencies ===" -ForegroundColor Cyan
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "=== Building .NET ===" -ForegroundColor Cyan
dotnet build --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host "=== Running .NET tests ===" -ForegroundColor Cyan
dotnet test --no-build --configuration Release --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }

Write-Host "=== Restoring frontend dependencies ===" -ForegroundColor Cyan
Push-Location apps/web
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }

    Write-Host "=== Linting frontend ===" -ForegroundColor Cyan
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw "npm run lint failed" }

    Write-Host "=== Building frontend ===" -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }

    Write-Host "=== Running frontend tests ===" -ForegroundColor Cyan
    npm test
    if ($LASTEXITCODE -ne 0) { throw "npm test failed" }
} finally {
    Pop-Location
}

Write-Host "=== All checks passed ===" -ForegroundColor Green
