<#
.SYNOPSIS
  Builds and starts the whole backend locally (PostgreSQL + API + Worker) with Docker,
  and optionally the Studio and Showcase dev servers.

.EXAMPLE
  ./scripts/dev-up.ps1                 # asks for the Studio password the first time
  ./scripts/dev-up.ps1 -Frontend       # also starts Studio (5174) and Showcase (5175) in new windows
  ./scripts/dev-up.ps1 -Down           # stops the containers (data is kept)
  ./scripts/dev-up.ps1 -Reset          # stops and deletes all local data, then starts fresh

  Needs: Docker Desktop (running). For -Frontend also Node 22 + pnpm (corepack enable).
  Works in Windows PowerShell 5.1, PowerShell 7 and pwsh on Linux/macOS.
#>
param(
  [string]$Password,
  [switch]$Frontend,
  [switch]$Down,
  [switch]$Reset
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$envFile = Join-Path $root 'deploy/.env'
$compose = @('compose', '-f', 'deploy/docker-compose.dev.yml', '--env-file', 'deploy/.env')

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# Docker must be installed and running.
docker version --format '{{.Server.Version}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running. Start Docker Desktop and try again.' }

if ($Down -or $Reset) {
  if (Test-Path $envFile) {
    Step ($(if ($Reset) { 'Stopping and deleting local data' } else { 'Stopping containers' }))
    $args2 = $compose + @('down') + $(if ($Reset) { @('-v') } else { @() })
    docker @args2
  }
  if ($Down) { return }
}

# First run: deploy/.env with the password hash and a random database password.
if (-not (Test-Path $envFile)) {
  Step 'First run: building the API image to hash your Studio password'
  if (-not $Password) {
    $secure = Read-Host 'Choose the Studio password (Mật khẩu đăng nhập Studio)' -AsSecureString
    $Password = [Net.NetworkCredential]::new('', $secure).Password
  }
  if ($Password.Length -lt 8) { throw 'Use a password of at least 8 characters.' }
  docker build --target api -t showcase-api:dev backend
  if ($LASTEXITCODE -ne 0) { throw 'Building the API image failed.' }
  $hash = (docker run --rm showcase-api:dev hash-password $Password | Select-Object -Last 1).Trim()
  if (-not $hash) { throw 'Could not hash the password.' }
  $chars = [char[]]'abcdefghijkmnpqrstuvwxyz23456789'
  $dbPassword = -join (1..24 | ForEach-Object { $chars | Get-Random })
  $lines = Get-Content 'deploy/.env.example' | ForEach-Object {
    if ($_ -match '^ADMIN_PASSWORD_HASH=') { "ADMIN_PASSWORD_HASH=$hash" }
    elseif ($_ -match '^POSTGRES_PASSWORD=') { "POSTGRES_PASSWORD=$dbPassword" }
    else { $_ }
  }
  # LF endings, no BOM: docker compose reads it the same on every OS.
  [IO.File]::WriteAllText($envFile, (($lines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
  Write-Host "Wrote deploy/.env (keep it private; delete it to choose a new password)."
}

Step 'Building and starting PostgreSQL + API + Worker (first build downloads Blender, ~5–10 min)'
$up = $compose + @('up', '-d', '--build')
docker @up
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed. See the messages above.' }

Step 'Waiting for the API'
$ok = $false
for ($i = 0; $i -lt 90; $i++) {
  try {
    $r = Invoke-WebRequest -Uri 'http://localhost:5080/health' -UseBasicParsing -TimeoutSec 2
    if ($r.StatusCode -eq 200) { $ok = $true; break }
  } catch { Start-Sleep -Seconds 2 }
}
if (-not $ok) {
  $logs = $compose + @('logs', '--tail', '50', 'api')
  docker @logs
  throw 'The API did not become healthy in 3 minutes (logs above).'
}
Write-Host 'API is up.' -ForegroundColor Green

if ($Frontend) {
  Step 'Installing frontend packages and starting Studio + Showcase'
  if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) { throw 'pnpm not found. Install Node 22, then run: corepack enable' }
  Push-Location frontend
  pnpm install
  Pop-Location
  $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }
  Start-Process $shell -ArgumentList '-NoExit', '-Command', "Set-Location '$root/frontend'; pnpm --filter @showcase/studio dev"
  Start-Process $shell -ArgumentList '-NoExit', '-Command', "Set-Location '$root/frontend'; `$env:API_URL='http://localhost:5080'; pnpm --filter @showcase/showcase build; pnpm --filter @showcase/showcase preview"
}

Write-Host @"

Running:
  API + OpenAPI   http://localhost:5080/health · http://localhost:5080/openapi/v1.json
  Studio          http://localhost:5174   (cd frontend; pnpm --filter @showcase/studio dev)
  Showcase        http://localhost:5175   (rebuild after publishing: `$env:API_URL='http://localhost:5080'; pnpm --filter @showcase/showcase build; pnpm --filter @showcase/showcase preview)
  PostgreSQL      localhost:5432 (user/db: showcase, password in deploy/.env)

Logs: docker compose -f deploy/docker-compose.dev.yml --env-file deploy/.env logs -f worker
Stop: ./scripts/dev-up.ps1 -Down
"@ -ForegroundColor Green
