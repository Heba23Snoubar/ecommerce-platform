param(
    [string]$Base = "http://localhost:8088",
    [Parameter(Mandatory = $true)][string]$Email,
    [string]$StopService = "app2",
    [int]$Requests = 30
)

$secure = Read-Host "Password" -AsSecureString
$plain = [System.Net.NetworkCredential]::new("", $secure).Password
$login = Invoke-RestMethod -Method Post "$Base/api/v1/users/login" -ContentType "application/json" `
    -Body (@{ email = $Email; password = $plain } | ConvertTo-Json)
if (-not $login.accessToken) { throw "login failed" }
$h = @{ Authorization = "Bearer $($login.accessToken)" }
$url = "$Base/api/v1/orders?page=1&pageSize=1"

function Hit([int]$n) {
    $fail = 0
    1..$n | ForEach-Object {
        try { Invoke-RestMethod $url -Headers $h | Out-Null } catch { $fail++ }
    }
    return $fail
}

function Dist([string]$since) {
    docker compose logs --since $since nginx | Select-String "upstream=" | ForEach-Object {
        (($_.Line -split 'upstream=')[1].Split(' ')[0]).TrimEnd(',')
    } | Group-Object | Select-Object Name, Count
}

Write-Host "`n== 0) Forwarded headers (I2-3) ==" -ForegroundColor Cyan
Invoke-RestMethod "$Base/api/v1/products" -Headers @{ "X-Forwarded-For" = "1.2.3.4" } | Out-Null
docker compose logs --tail 2 nginx | Select-String "xff_sent"

Write-Host "`n== 1) Baseline: all instances up ==" -ForegroundColor Cyan
$f = Hit $Requests
"requests=$Requests failures=$f"
Dist "1m"

Write-Host "`n== 2) Stop $StopService ==" -ForegroundColor Cyan
docker compose stop $StopService | Out-Null
$f = Hit $Requests
"requests=$Requests failures=$f"
Write-Host "-- retried requests (two upstream addresses on one line):"
docker compose logs --since 3m nginx | Select-String "upstream=\S+, " | Select-Object -First 5
Write-Host "-- connection errors seen by nginx:"
docker compose logs --since 3m nginx | Select-String "Connection refused|timed out|No route to host" | Select-Object -First 3

Write-Host "`n== 3) Start $StopService again ==" -ForegroundColor Cyan
docker compose start $StopService | Out-Null
Start-Sleep -Seconds 20
$f = Hit $Requests
"requests=$Requests failures=$f"
Dist "40s"
Write-Host "`nIf $StopService is missing above: run 'docker compose restart nginx' (nginx resolves names only at startup) and re-run section 3." -ForegroundColor Yellow