# Supabase Complete Setup with Admin User
# Usage: .\setup-supabase-with-admin.ps1 -ProjectUrl "https://xxx.supabase.co" -ApiKey "xxx" -DbPassword "xxx"

param(
    [string]$ProjectUrl = $(Read-Host "Supabase Project URL (https://xxx.supabase.co)"),
    [string]$ApiKey = $(Read-Host "Supabase API Key"),
    [string]$DbPassword = $(Read-Host "Supabase DB Password")
)

$ErrorActionPreference = 'Stop'

# Extract project ref from URL
$projectRef = $ProjectUrl -replace 'https://(.+)\.supabase\.co', '$1'
if (-not $projectRef) { throw "Invalid Supabase URL format" }

Write-Host "=== Supabase Complete Setup with Admin ===" -ForegroundColor Cyan
Write-Host "Project Ref: $projectRef"

# Run base setup
Write-Host "`n1. Running base Supabase setup..." -ForegroundColor Yellow
& ".\setup-supabase.ps1" -ProjectUrl $ProjectUrl -ApiKey $ApiKey -DbPassword $DbPassword -SkipMigration

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Base setup failed" -ForegroundColor Red
    exit 1
}

# Build connection string
$connectionString = "postgresql://postgres:$DbPassword@db.$projectRef.supabase.co:5432/postgres?sslmode=require"

# Run migrations
Write-Host "`n2. Running EF Core migrations..." -ForegroundColor Yellow

$env:ConnectionStrings__DefaultConnection = $connectionString

Push-Location src/WhatsAppAI.WebApi
dotnet ef database update -p ../WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj 2>&1 | Select-String -Pattern "Done|error|Error" | Select-Object -Last 3
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Migrations failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Migrations applied" -ForegroundColor Green

# Seed admin user via SQL
Write-Host "`n3. Seeding admin user..." -ForegroundColor Yellow

$env:PGPASSWORD = $DbPassword

# Read SQL seed file
$seedSql = Get-Content "setup-supabase-admin.sql" -Raw

# Execute via psql
psql -h "db.$projectRef.supabase.co" -U postgres -d postgres -c $seedSql 2>&1 | Select-String -Pattern "INSERT|already|error" | Select-Object -Last 5

Remove-Item env:PGPASSWORD

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Admin user seeded" -ForegroundColor Green
} else {
    Write-Host "⚠ Admin seed may have skipped (user exists?)" -ForegroundColor Yellow
}

dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString --project src/WhatsAppAI.WebApi

Write-Host "`n=== Supabase Ready ===" -ForegroundColor Green
Write-Host "Start: dotnet run --project src/WhatsAppAI.WebApi" -ForegroundColor Cyan
Write-Host "Login: admin@platform.com / Admin@123" -ForegroundColor Cyan
