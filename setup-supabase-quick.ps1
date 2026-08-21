# Initializes an empty Supabase database without persisting credentials.
param(
    [string]$ProjectRef = "ysgzfmircmyrghuhweze",
    [string]$PoolerHost = "aws-0-sa-east-1.pooler.supabase.com",
    [int]$Port = 5432,
    [System.Security.SecureString]$DbPassword = (Read-Host "Supabase database password" -AsSecureString)
)

$ErrorActionPreference = "Stop"
$password = [System.Net.NetworkCredential]::new("", $DbPassword).Password
$connectionString = "Host=$PoolerHost;Port=$Port;Database=postgres;Username=postgres.$ProjectRef;Password=$password;SSL Mode=Require"

try {
    $env:DatabaseProvider = "SUPABASE"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:DatabaseInitialization__EnsureCreated = "true"
    $env:DatabaseInitialization__Only = "true"
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $env:Encryption__Key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

    dotnet run --project src/WhatsAppAI.WebApi --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Supabase initialization failed." }
}
finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Remove-Item Env:Encryption__Key -ErrorAction SilentlyContinue
    $password = $null
    $connectionString = $null
}

Write-Host "Supabase schema initialized." -ForegroundColor Green
