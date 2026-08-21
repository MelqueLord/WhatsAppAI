# Quick Supabase Setup
param([string]$ProjectUrl = "https://ysgzfmircmyrghuhweze.supabase.co", [string]$DbPassword = "MelK050587!!")
$ErrorActionPreference = 'Stop'
$projectRef = $ProjectUrl -replace 'https://(.+)\.supabase\.co', '$1'
$connectionString = "postgresql://postgres:$DbPassword@db.$projectRef.supabase.co:5432/postgres?sslmode=require"

Write-Host "Setup Supabase" -ForegroundColor Cyan

Write-Host "`n1 Build..."
dotnet restore src/WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj 2>&1 > $null
dotnet build src/WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj -c Release 2>&1 > $null
Write-Host "OK" -ForegroundColor Green

Write-Host "`n2 Migrate..."
$env:ConnectionStrings__DefaultConnection = $connectionString
$env:DatabaseProvider = "SUPABASE"
Push-Location src/WhatsAppAI.WebApi
dotnet restore 2>&1 > $null
dotnet ef database update -p ../WhatsAppAI.Infrastructure/WhatsAppAI.Infrastructure.csproj 2>&1 | Select-String "done|error|Failed" | Select-Object -Last 3
$migCode = $LASTEXITCODE
Pop-Location
if ($migCode -ne 0) { Write-Host "Migration error - check connection" -ForegroundColor Red; exit 1 }
Write-Host "OK" -ForegroundColor Green

Write-Host "`n3 Config..."
$cfg = @{ Logging=@{LogLevel=@{Default="Information"}}; DatabaseProvider="SUPABASE"; ConnectionStrings=@{DefaultConnection=$connectionString}; Authentication=@{JwtSecret=([guid]::NewGuid()).ToString()} } | ConvertTo-Json -Depth 10
Set-Content -Path "src/WhatsAppAI.WebApi/appsettings.Supabase.json" -Value $cfg
Write-Host "OK" -ForegroundColor Green

Write-Host "`nDone!" -ForegroundColor Green
Write-Host 'Run: $env:ASPNETCORE_ENVIRONMENT="Supabase"; dotnet run --project src/WhatsAppAI.WebApi' -ForegroundColor Cyan
