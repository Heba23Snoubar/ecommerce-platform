<#
  w3-verify.ps1: waits for the queue to drain, then checks the evidence.
  Emails are counted from the fake sender (Redis key emailsent:{orderId}), so run the workers with EMAIL_SENDER=fake.
#>
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][int]$ExpectedEmails,
    [int]$ExpectedDlq = 0,
    [int]$WaitSeconds = 150,
    [string]$RabbitUser,
    [string]$RabbitPass,
    [string]$Api = "http://localhost:15672/api"
)

. "$PSScriptRoot\w3-common.ps1"
Set-Location $RepoRoot
$h = Get-RabbitHeaders $RabbitUser $RabbitPass

function Get-Queue([string]$Name) { return Invoke-RestMethod -Uri "$Api/queues/%2F/$Name" -Headers $h }

Write-Host "Waiting for orders.email to drain (max $WaitSeconds s)..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($WaitSeconds)
do {
    $q = Get-Queue "orders.email"
    Write-Host ("  ready={0} unacked={1}" -f $q.messages_ready, $q.messages_unacknowledged)
    if (($q.messages_ready + $q.messages_unacknowledged) -eq 0) { break }
    Start-Sleep -Seconds 3
} while ((Get-Date) -lt $deadline)
Start-Sleep -Seconds 6
$q = Get-Queue "orders.email"
$dlq = Get-Queue "orders.email.dlq"

$emailKeys = Get-RedisKeys "emailsent:$RunId-*"
$sentKeys = Get-RedisKeys "sent:$RunId-*"
$lockKeys = Get-RedisKeys "lock:$RunId-*"

$dups = 0
if ($emailKeys.Count -gt 0) {
    $vals = Invoke-Redis ($emailKeys | ForEach-Object { "GET $_" })
    $dups = @($vals | Where-Object { [int](($_ -replace '"', '').Trim()) -gt 1 }).Count
}

Write-Host "`n== Emails per worker (from the logs) ==" -ForegroundColor Cyan
$lines = docker compose logs --no-color order-worker 2>&1 | Select-String "FAKE-EMAIL-SENT order=$RunId"
if ($lines) {
    $lines | ForEach-Object { ($_.Line -split '\|')[0].Trim() } | Group-Object |
        Select-Object @{ n = "Worker"; e = { $_.Name } }, Count | Format-Table -AutoSize
} else {
    Write-Host "(no FAKE-EMAIL-SENT lines: are the workers running with EMAIL_SENDER=fake?)" -ForegroundColor Yellow
}

$failed = 0
function Check([string]$Name, [bool]$Ok, [string]$Detail) {
    if ($Ok) { $tag = "PASS"; $color = "Green" } else { $tag = "FAIL"; $color = "Red"; $script:failed++ }
    Write-Host ("[{0}] {1}: {2}" -f $tag, $Name, $Detail) -ForegroundColor $color
}

Write-Host "== Results ==" -ForegroundColor Cyan
Check "Queue drained" (($q.messages_ready + $q.messages_unacknowledged) -eq 0) ("ready={0} unacked={1}" -f $q.messages_ready, $q.messages_unacknowledged)
Check "One email per order (count)" ($emailKeys.Count -eq $ExpectedEmails) "emails=$($emailKeys.Count) expected=$ExpectedEmails"
Check "No duplicate emails" ($dups -eq 0) "orders emailed more than once=$dups"
Check "No lost emails" ($sentKeys.Count -eq $emailKeys.Count) "sent markers=$($sentKeys.Count) emails actually sent=$($emailKeys.Count)"
Check "Dead letter queue" ($dlq.messages -eq $ExpectedDlq) "dlq=$($dlq.messages) expected=$ExpectedDlq"
Write-Host ("(leftover locks for this run: {0})" -f $lockKeys.Count)

if ($failed -gt 0) { Write-Host "`n$failed check(s) FAILED" -ForegroundColor Red; exit 1 }
Write-Host "`nAll checks passed" -ForegroundColor Green