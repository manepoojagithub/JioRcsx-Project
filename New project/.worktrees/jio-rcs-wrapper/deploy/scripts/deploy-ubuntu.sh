#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/advait-services}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"

cd "$APP_DIR"

if [ ! -f ".env.production" ]; then
  echo "Missing $APP_DIR/.env.production. Copy .env.production.example and set real secrets first." >&2
  exit 1
fi

docker compose --env-file .env.production -f "$COMPOSE_FILE" build web
docker compose --env-file .env.production -f "$COMPOSE_FILE" up -d sqlserver

echo "Waiting for SQL Server container..."
docker compose --env-file .env.production -f "$COMPOSE_FILE" up -d --wait sqlserver

set -a
. ./.env.production
set +a
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=JioCxRcsWrapper;User Id=sa;Password=${SQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project src/JioCxRcsWrapper.Infrastructure \
  --startup-project src/JioCxRcsWrapper.Web

docker compose --env-file .env.production -f "$COMPOSE_FILE" up -d web
docker compose --env-file .env.production -f "$COMPOSE_FILE" ps
