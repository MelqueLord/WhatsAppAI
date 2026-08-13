#!/bin/bash
set -euo pipefail

echo "=== Restoring .NET dependencies ==="
dotnet restore

echo "=== Building .NET ==="
dotnet build --no-restore --configuration Release

echo "=== Running .NET tests ==="
dotnet test --no-build --configuration Release --verbosity normal

echo "=== Restoring frontend dependencies ==="
cd apps/web
npm ci

echo "=== Linting frontend ==="
npm run lint

echo "=== Building frontend ==="
npm run build

echo "=== Running frontend tests ==="
npm test

echo "=== All checks passed ==="
