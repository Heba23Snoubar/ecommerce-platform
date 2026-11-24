# Shared helpers for the W3 worker tests (dot-source this file; do not run it directly)
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Get-DotEnv {
    $map = @{}
    $path = Join-Path $RepoRoot ".env"
    if (Test-Path $path) {
        Get-Content $path | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object {
            $k, $v = $_ -split '=', 2
            $map[$k.Trim()] = $v.Trim()
        }
    }
    return $map
}

function Get-RabbitHeaders([string]$User, [string]$Pass) {
    $dotenv = Get-DotEnv
    if (-not $User) { $User = $dotenv["RABBITMQ_USER"] }
    if (-not $Pass) { $Pass = $dotenv["RABBITMQ_PASSWORD"] }
    if (-not $User -or -not $Pass) {
        throw "RabbitMQ credentials not found: set RABBITMQ_USER and RABBITMQ_PASSWORD in .env or pass -RabbitUser and -RabbitPass"
    }
    $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${User}:${Pass}"))
    return @{ Authorization = "Basic $b64"; "Content-Type" = "application/json" }
}

function Get-RedisContainer {
    $id = (docker compose ps -q redis | Select-Object -First 1)
    if (-not $id) { throw "redis container not found (check: docker compose ps redis)" }
    return $id
}

function Invoke-Redis([string[]]$Commands) {
    $id = Get-RedisContainer
    return ($Commands -join "`n") | docker exec -i $id redis-cli
}

function Get-RedisKeys([string]$Pattern) {
    $id = Get-RedisContainer
    $keys = docker exec $id redis-cli --scan --pattern $Pattern
    return @($keys | Where-Object { $_ })
}