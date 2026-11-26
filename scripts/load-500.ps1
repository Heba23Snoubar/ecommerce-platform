<#
  load-500.ps1 (E-1): creates N orders through the load balancer with several parallel threads and prints
  the success rate, latency and the distribution across the instances.
  Requires the workers to run with EMAIL_SENDER=fake (otherwise N real emails would be sent).

  Examples:
    .\scripts\load-500.ps1 -Email customer@example.com
    .\scripts\load-500.ps1 -Email customer@example.com -StopService app2 -StopAfterSeconds 5   # failover under load (T3)
#>
param(
    [string]$Base = "http://localhost:8088",
    [Parameter(Mandatory = $true)][string]$Email,
    [int]$Orders = 500,
    [int]$Threads = 20,
    [string]$ProductId,
    [string]$StopService,
    [int]$StopAfterSeconds = 5
)

$ErrorActionPreference = "Stop"
[System.Net.ServicePointManager]::DefaultConnectionLimit = 200
[System.Net.ServicePointManager]::Expect100Continue = $false
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# Safety: never send hundreds of real emails
$sender = ""
try { $sender = (docker compose exec -T --index 1 order-worker printenv EMAIL_SENDER | Out-String).Trim() } catch { $sender = "" }
if ($sender -ne "fake") {
    throw "order-worker is not running with EMAIL_SENDER=fake (found '$sender'). Run: `$env:EMAIL_SENDER='fake'; docker compose up -d --no-build --force-recreate --scale order-worker=3 order-worker"
}

$secure = Read-Host "Password" -AsSecureString
$plain = [System.Net.NetworkCredential]::new("", $secure).Password
$login = Invoke-RestMethod -Method Post "$Base/api/v1/users/login" -ContentType "application/json" `
    -Body (@{ email = $Email; password = $plain } | ConvertTo-Json)
$token = $login.accessToken
if (-not $token) { throw "login failed" }
$h = @{ Authorization = "Bearer $token" }

if (-not $ProductId) {
    $p = Invoke-RestMethod "$Base/api/v1/products" -Headers $h
    $items = if ($p.items) { $p.items } else { $p }
    $ProductId = $items[0].id
}
$bodyJson = @{ items = @(@{ productId = $ProductId; quantity = 1 }) } | ConvertTo-Json -Depth 5 -Compress
$listUrl = "$Base/api/v1/orders?page=1&pageSize=1"
$totalBefore = (Invoke-RestMethod $listUrl -Headers $h).total

$work = {
    param($Base, $Token, $Body, $Count)
    $res = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $Count; $i++) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $status = 0; $server = ""
        try {
            $r = Invoke-WebRequest -Uri "$Base/api/v1/orders" -Method Post -Body $Body -ContentType "application/json" `
                -Headers @{ Authorization = "Bearer $Token" } -UseBasicParsing
            $status = [int]$r.StatusCode
            $server = [string]$r.Headers["X-Served-By"]
            if (-not $server) { $server = [string]$r.Headers["X-Upstream"] }
        } catch {
            if ($_.Exception.Response) {
                $status = [int]$_.Exception.Response.StatusCode
                $server = [string]$_.Exception.Response.Headers["X-Upstream"]
            } else { $status = -1 }
        }
        $sw.Stop()
        if ($server) { $server = ($server -split ',')[-1].Trim() }
        [void]$res.Add([pscustomobject]@{ Status = $status; Server = $server; Ms = $sw.ElapsedMilliseconds })
    }
    return $res
}

if ($StopService) {
    Start-Job -ScriptBlock { param($r, $s, $d) Set-Location $r; Start-Sleep -Seconds $d; docker compose stop $s | Out-Null } `
        -ArgumentList $repo, $StopService, $StopAfterSeconds | Out-Null
    Write-Host "Will stop $StopService after $StopAfterSeconds s" -ForegroundColor Yellow
}

$perThread = [int][Math]::Floor($Orders / $Threads)
$extra = $Orders - $perThread * $Threads
$pool = [runspacefactory]::CreateRunspacePool(1, $Threads)
$pool.Open()
$clock = [Diagnostics.Stopwatch]::StartNew()
$jobs = @()
for ($t = 0; $t -lt $Threads; $t++) {
    $n = $perThread
    if ($t -lt $extra) { $n++ }
    $ps = [powershell]::Create().AddScript($work).AddArgument($Base).AddArgument($token).AddArgument($bodyJson).AddArgument($n)
    $ps.RunspacePool = $pool
    $jobs += [pscustomobject]@{ Ps = $ps; Handle = $ps.BeginInvoke() }
}

$all = New-Object System.Collections.ArrayList
foreach ($j in $jobs) {
    foreach ($r in $j.Ps.EndInvoke($j.Handle)) { [void]$all.Add($r) }
    $j.Ps.Dispose()
}
$clock.Stop()
$pool.Close()

$ok = @($all | Where-Object { $_.Status -eq 201 })
$sorted = @($ok | ForEach-Object { $_.Ms } | Sort-Object)
function Pct([int]$p) {
    if ($sorted.Count -eq 0) { return 0 }
    $idx = [Math]::Min($sorted.Count - 1, [Math]::Max(0, [int][Math]::Ceiling($p / 100.0 * $sorted.Count) - 1))
    return $sorted[$idx]
}

Write-Host "`n== Result ==" -ForegroundColor Cyan
"sent={0} created(201)={1} duration={2:N1}s throughput={3:N1} orders/s" -f $all.Count, $ok.Count, $clock.Elapsed.TotalSeconds, ($ok.Count / [Math]::Max(0.001, $clock.Elapsed.TotalSeconds))
"latency ms: p50={0} p95={1} max={2}" -f (Pct 50), (Pct 95), (Pct 100)

Write-Host "`n-- Status codes" -ForegroundColor Cyan
$all | Group-Object Status | Sort-Object Name | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }

Write-Host "`n-- Distribution across instances (created orders)" -ForegroundColor Cyan
$ok | Group-Object Server | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Instance = $_.Name; Orders = $_.Count; Percent = ("{0:N1}%" -f (100.0 * $_.Count / $ok.Count)) }
} | Format-Table -AutoSize

Write-Host "-- Consistency (read the order count 6 times through the balancer)" -ForegroundColor Cyan
$totals = @(1..6 | ForEach-Object { (Invoke-RestMethod $listUrl -Headers $h).total })
"before={0} after={1}" -f $totalBefore, ($totals -join ",")
$expected = $totalBefore + $ok.Count
$consistent = (@($totals | Where-Object { $_ -ne $expected }).Count -eq 0)
if ($consistent) { Write-Host "[PASS] every read returned $expected (= before + created)" -ForegroundColor Green }
else { Write-Host "[FAIL] reads disagree or do not equal before + created ($expected)" -ForegroundColor Red }

$failed = @($all | Where-Object { $_.Status -ne 201 })
if ($failed.Count -eq 0) { Write-Host "[PASS] no failed request" -ForegroundColor Green }
else { Write-Host ("[INFO] {0} request(s) not created (see status codes; 409 usually means no stock)" -f $failed.Count) -ForegroundColor Yellow }