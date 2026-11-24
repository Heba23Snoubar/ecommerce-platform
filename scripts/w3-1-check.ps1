$rabbitUser = if ($env:RABBIT_USER) { $env:RABBIT_USER } else { "app" }
$rabbitPass = if ($env:RABBIT_PASS) { $env:RABBIT_PASS } else { "change_me_rabbit" }
$pair = "${rabbitUser}:${rabbitPass}"
$base64 = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
$headers = @{ Authorization = "Basic $base64" }

Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/orders.email" -Method Get -Headers $headers |
    Select-Object name, messages, messages_ready, messages_unacknowledged

$redisKeys = docker exec ecommerce-platform-redis-1 redis-cli --scan --pattern "sent:44444444-4444-4444-8444-*"
Write-Host "Redis keys count (expected 480): $($redisKeys.Count)"

$logs = docker compose logs order-worker
Write-Host "Total log lines for order-worker: $($logs.Count)"
($logs | Select-String -Pattern "duplicate|already|sent|order\.created" -CaseSensitive:$false) |
    Select-Object -Last 25