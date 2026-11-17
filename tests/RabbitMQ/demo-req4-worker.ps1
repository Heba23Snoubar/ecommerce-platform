param(
    [string]$RecipientEmail = "",
    [int]$OrdersCount = 3
)

$ErrorActionPreference = "Stop"

# Read environment variables from .env file
$envMap = @{}
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $parts = $line.Split("=", 2)
            $envMap[$parts[0].Trim()] = $parts[1].Trim()
        }
    }
}

$rabbitUser = if ($envMap["RABBITMQ_USER"]) { $envMap["RABBITMQ_USER"] } else { "app" }
$rabbitPass = if ($envMap["RABBITMQ_PASSWORD"]) { $envMap["RABBITMQ_PASSWORD"] } else { "change_me_rabbit" }
if ([string]::IsNullOrWhiteSpace($RecipientEmail)) {
    $RecipientEmail = if ($envMap["SMTP_USER"]) { $envMap["SMTP_USER"] } else { "customer@example.com" }
}

$pair = "${rabbitUser}:${rabbitPass}"
$encodedCreds = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
$headers = @{
    Authorization  = "Basic $encodedCreds"
    "Content-Type" = "application/json"
}

Write-Host "=== 1. Stopping order-worker container to simulate consumer downtime ===" -ForegroundColor Yellow
docker compose stop order-worker
Start-Sleep -Seconds 2

Write-Host "`n=== 2. Publishing $OrdersCount new orders while worker is stopped ===" -ForegroundColor Cyan
for ($i = 1; $i -le $OrdersCount; $i++) {
    $orderPayload = @{
        messageId     = [Guid]::NewGuid().ToString()
        orderId       = [Guid]::NewGuid().ToString()
        userId        = [Guid]::NewGuid().ToString()
        customerName  = "Demo Customer #$i"
        customerEmail = $RecipientEmail
        total         = (50.00 * $i)
        items         = @(
            @{
                productId   = [Guid]::NewGuid().ToString()
                productName = "Demo Product #$i"
                unitPrice   = 50.00
                quantity    = $i
            }
        )
        createdAtUtc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    } | ConvertTo-Json -Depth 5 -Compress

    $publishRequest = @{
        properties       = @{ delivery_mode = 2 }
        routing_key      = "order.created"
        payload          = $orderPayload
        payload_encoding = "string"
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod `
        -Uri "http://localhost:15672/api/exchanges/%2F/orders.events/publish" `
        -Method Post `
        -Headers $headers `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($publishRequest))

    Write-Host "  -> Published Order #$i (Routed: $($response.routed))" -ForegroundColor Green
}

# Wait 6 seconds for RabbitMQ management stats to refresh (5s default interval)
Start-Sleep -Seconds 6
$queueInfo = Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/orders.email" -Headers $headers
Write-Host "`nQueue 'orders.email' status while worker is stopped: Ready = $($queueInfo.messages_ready)" -ForegroundColor Magenta

Write-Host "`n=== 3. Starting order-worker to process all queued messages ===" -ForegroundColor Yellow
docker compose start order-worker

Write-Host "Waiting for all messages to be processed and emails sent..." -ForegroundColor Cyan
do {
    Start-Sleep -Seconds 3
    $queueInfoAfter = Invoke-RestMethod -Uri "http://localhost:15672/api/queues/%2F/orders.email" -Headers $headers
    Write-Host "  ... Remaining in queue: Ready = $($queueInfoAfter.messages_ready), Unacked = $($queueInfoAfter.messages_unacknowledged)" -ForegroundColor DarkGray
} while (($queueInfoAfter.messages_ready -gt 0) -or ($queueInfoAfter.messages_unacknowledged -gt 0))

Write-Host "All messages processed! Final queue status: Ready = $($queueInfoAfter.messages_ready)" -ForegroundColor Green

Write-Host "`n=== 4. Latest processing logs from order-worker ===" -ForegroundColor Cyan
docker compose logs --tail=25 order-worker