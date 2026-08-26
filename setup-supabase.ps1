# Supabase Setup Script
# Usage: .\setup-supabase.ps1 -ProjectUrl "https://xxx.supabase.co" -ApiKey "xxx" -DbPassword "xxx"

param(
    [string]$ProjectUrl = $(Read-Host "Supabase Project URL (https://xxx.supabase.co)"),
    [string]$ApiKey = $(Read-Host "Supabase API Key"),
    [string]$DbPassword = $(Read-Host "Supabase DB Password"),
    [switch]$SkipMigration
)

$ErrorActionPreference = 'Stop'

# Extract project ref from URL
$projectRef = $ProjectUrl -replace 'https://(.+)\.supabase\.co', '$1'
if (-not $projectRef) { throw "Invalid Supabase URL format" }

# Build connection string
$connectionString = "postgresql://postgres:$DbPassword@db.$projectRef.supabase.co:5432/postgres?sslmode=require"

Write-Host "=== Supabase Setup ===" -ForegroundColor Cyan
Write-Host "Project Ref: $projectRef"
Write-Host "Connection String: postgresql://postgres:***@db.$projectRef.supabase.co:5432/postgres?sslmode=require"

# Test connection
Write-Host "`n1. Testing connection..." -ForegroundColor Yellow
$testCmd = "SELECT version();"
$env:PGPASSWORD = $DbPassword
$result = psql -h "db.$projectRef.supabase.co" -U postgres -d postgres -c $testCmd 2>&1
Remove-Item env:PGPASSWORD

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Connection failed: $result" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Connection successful" -ForegroundColor Green

# Run migrations
if (-not $SkipMigration) {
    Write-Host "`n2. Running EF Core migrations..." -ForegroundColor Yellow
    
    $env:ConnectionStrings__DefaultConnection = $connectionString
    
    Set-Location src/WhatsAppAI.WebApi
    dotnet ef database update -p ../WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Migrations failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Migrations applied" -ForegroundColor Green
    
    Set-Location ../../
}

# Keep the credential outside versioned files.
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString --project src/WhatsAppAI.WebApi

Write-Host "`n=== Ready for Supabase ===" -ForegroundColor Green
Write-Host "Start with: dotnet run --project src/WhatsAppAI.WebApi" -ForegroundColor Cyan
