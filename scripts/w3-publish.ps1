<#
  w3-publish.ps1: publishes test messages straight into the orders.email queue through the
  RabbitMQ management API (no real orders, no real emails when the workers run with EMAIL_SENDER=fake).

  Examples:
    .\scripts\w3-publish.ps1 -Valid 480 -Duplicates 20                       # W3-1
    .\scripts\w3-publish.ps1 -Valid 12                                       # W3-2 (kill a worker meanwhile)
    .\scripts\w3-publish.ps1 -Valid 20 -BadJson 5 -MissingOrderId 5 -FailingSend 5   # W3-3
#>
param(
    [string]$RunId = ([guid]::NewGuid().ToString("N").Substring(0, 8)),
    [int]$Valid = 0,
    [int]$Duplicates = 0,
    [int]$BadJson = 0,
    [int]$MissingOrderId = 0,
    [int]$FailingSend = 0,
    [switch]$AllowRealEmails,
    [string]$RabbitUser,
    [string]$RabbitPass,
    [string]$Api = "http://localhost:15672/api"
)

. "$PSScriptRoot\w3-common.ps1"
Set-Location $RepoRoot
$h = Get-RabbitHeaders $RabbitUser $RabbitPass

# Safety: never flood the real SMTP account. The workers must run with EMAIL_SENDER=fake.
$sender = ""
try { $sender = (docker compose exec -T --index 1 order-worker printenv EMAIL_SENDER | Out-String).Trim() } catch { $sender = "" }
if ($sender -ne "fake" -and -not $AllowRealEmails) {
    throw "order-worker is not running with EMAIL_SENDER=fake (found '$sender'). Start it with: `$env:EMAIL_SENDER='fake'; docker compose up -d --no-build --force-recreate --scale order-worker=3 order-worker   (or pass -AllowRealEmails)"
}

function Publish-Raw([string]$Payload) {
    $body = @{
        properties       = @{ delivery_mode = 2; content_type = "application/json" }
        routing_key      = "orders.email"
        payload          = $Payload
        payload_encoding = "string"
    } | ConvertTo-Json -Depth 4
    $r = Invoke-RestMethod -Uri "$Api/exchanges/%2F/amq.default/publish" -Method Post -Headers $h -Body $body
    if (-not $r.routed) { throw "message was not routed to orders.email" }
}

function New-Payload([string]$OrderId, [string]$Email, [string]$Name) {
    $msg = [ordered]@{
        messageId     = [guid]::NewGuid().ToString()
        orderId       = $OrderId
        userId        = [guid]::NewGuid().ToString()
        customerName  = $Name
        customerEmail = $Email
        total         = 25.0
        items         = @(@{ productId = [guid]::NewGuid().ToString(); productName = "Test item"; unitPrice = 25.0; quantity = 1 })
        createdAtUtc  = (Get-Date).ToUniversalTime().ToString("o")
    }
    return ($msg | ConvertTo-Json -Depth 5 -Compress)
}

function New-OrderId([int]$Index) { return ("{0}-4444-4444-8444-{1:D12}" -f $RunId, $Index) }

for ($i = 1; $i -le $Valid; $i++) {
    Publish-Raw (New-Payload (New-OrderId $i) "ok$i@example.com" "Test $i")
}

$dupCount = [Math]::Min($Duplicates, $Valid)
for ($i = 1; $i -le $dupCount; $i++) {
    Publish-Raw (New-Payload (New-OrderId $i) "ok$i@example.com" "Duplicate of $i")
}

for ($i = 1; $i -le $FailingSend; $i++) {
    Publish-Raw (New-Payload (New-OrderId (5000 + $i)) "fail-$i@example.com" "Failing $i")
}

for ($i = 1; $i -le $MissingOrderId; $i++) {
    Publish-Raw ('{"messageId":"' + [guid]::NewGuid() + '","customerEmail":"noorder' + $i + '@example.com"}')
}

for ($i = 1; $i -le $BadJson; $i++) {
    Publish-Raw "this is not valid json {"
}

$total = $Valid + $dupCount + $FailingSend + $MissingOrderId + $BadJson
Write-Host ""
Write-Host "RunId = $RunId" -ForegroundColor Yellow
Write-Host "Published $total messages: valid=$Valid duplicates=$dupCount failingSend=$FailingSend missingOrderId=$MissingOrderId badJson=$BadJson"
Write-Host "Verify with:  .\scripts\w3-verify.ps1 -RunId $RunId -ExpectedEmails $Valid -ExpectedDlq $($FailingSend + $MissingOrderId + $BadJson)"