<#
  w3-clean.ps1: resets the test state: purges orders.email and orders.email.dlq and deletes the
  sent:*, lock:* and emailsent:* keys from Redis. Use it only on a development environment.
#>
param(
    [switch]$Force,
    [string]$RabbitUser,
    [string]$RabbitPass,
    [string]$Api = "http://localhost:15672/api"
)

. "$PSScriptRoot\w3-common.ps1"
Set-Location $RepoRoot
$h = Get-RabbitHeaders $RabbitUser $RabbitPass

if (-not $Force) {
    $answer = Read-Host "This purges orders.email and orders.email.dlq and deletes sent:/lock:/emailsent: keys in Redis. Type yes to continue"
    if ($answer -ne "yes") { Write-Host "cancelled"; return }
}

foreach ($queue in "orders.email", "orders.email.dlq") {
    Invoke-RestMethod -Method Delete -Uri "$Api/queues/%2F/$queue/contents" -Headers $h | Out-Null
    Write-Host "purged $queue"
}

foreach ($pattern in "sent:*", "lock:*", "emailsent:*") {
    $keys = Get-RedisKeys $pattern
    if ($keys.Count -gt 0) {
        Invoke-Redis ($keys | ForEach-Object { "DEL $_" }) | Out-Null
    }
    Write-Host ("deleted {0} keys matching {1}" -f $keys.Count, $pattern)
}