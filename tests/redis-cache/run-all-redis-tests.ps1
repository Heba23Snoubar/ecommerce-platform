$ErrorActionPreference = "Stop"

$envFile = "C:\Web Apps project\ecommerce-platform\.env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -ne "" -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $parts = $line.Split("=", 2)
            $k = $parts[0].Trim()
            $v = $parts[1].Trim()
            [System.Environment]::SetEnvironmentVariable($k, $v, "Process")
        }
    }
}

# Override database connection strings to target the running Docker container on port 5434
$env:ConnectionStrings__CatalogDb = "Host=localhost;Port=5434;Database=appdb;Username=products_role;Password=products_pw"
$env:ConnectionStrings__Users = "Host=localhost;Port=5434;Database=appdb;Username=users_role;Password=users_pw"
$env:ConnectionStrings__Orders = "Host=localhost;Port=5434;Database=appdb;Username=orders_role;Password=orders_pw"
$env:ConnectionStrings__Notifications = "Host=localhost;Port=5434;Database=appdb;Username=notifications_role;Password=notifications_pw"
$env:ConnectionStrings__CartDb = "Host=localhost;Port=5434;Database=appdb;Username=cart_role;Password=cart_pw"
$env:ConnectionStrings__Redis = "localhost:6379,abortConnect=false,connectTimeout=3000"

$workDir = "C:\Web Apps project\ecommerce-platform\src\AppService\AppService.Host"
$binPath = Join-Path $workDir "bin\Release\net8.0\AppService.Host.exe"

Write-Host "Starting AppService.Host from $workDir..." -ForegroundColor Cyan

$proc = Start-Process -FilePath $binPath -ArgumentList "--urls http://localhost:5000" -WorkingDirectory $workDir -PassThru -NoNewWindow

try {
    Write-Host "Waiting for backend on http://localhost:5000/api/v1/products to become ready..." -ForegroundColor Yellow
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            $resp = Invoke-WebRequest -Uri "http://localhost:5000/api/v1/products" -Method GET -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($resp.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {}
    }

    if (-not $ready) {
        Write-Host "Backend failed to start within 30 seconds." -ForegroundColor Red
        exit 1
    }

    Write-Host "Backend is READY! Running Redis Cache Test Suite..." -ForegroundColor Green
    & "C:\Web Apps project\ecommerce-platform\tests\redis-cache\redis-cache-check.ps1" -BaseUrl "http://localhost:5000"
}
finally {
    Write-Host "Stopping AppService.Host (PID: $($proc.Id))..." -ForegroundColor Cyan
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}
