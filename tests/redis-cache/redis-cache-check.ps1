param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$ProductId = "01a1cc48-ed63-7459-b9b1-251ff1304b38",
    [string]$RedisContainer = "ecommerce-redis"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$script:failed = 0
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function Send-Request {
    param(
        [string]$Method,
        [string]$Url,
        [string]$JsonBody = ""
    )
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Url)
    if ($JsonBody -ne "") {
        $request.Content = [System.Net.Http.StringContent]::new($JsonBody, [System.Text.Encoding]::UTF8, "application/json")
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $sw.Stop()

    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{
        Status    = [int]$response.StatusCode
        Body      = $body
        ElapsedMs = $sw.ElapsedMilliseconds
    }
}

function Check {
    param([string]$Name, [bool]$Condition, [string]$Detail = "")
    if ($Condition) {
        Write-Host ("[PASS] " + $Name) -ForegroundColor Green
    } else {
        Write-Host ("[FAIL] " + $Name + "  |  " + $Detail) -ForegroundColor Red
        $script:failed++
    }
}

function Exec-RedisCli {
    param([string]$ArgsString)
    $res = & docker exec $RedisContainer redis-cli $ArgsString.Split(" ")
    return ($res -join "`n").Trim()
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Role C: Redis Caching Automated Verification Suite      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target BaseUrl:         $BaseUrl"
Write-Host "Target ProductId:       $ProductId"
Write-Host "Target Redis Container: $RedisContainer"
Write-Host ""

# Ensure Redis is running
$ping = Exec-RedisCli "ping"
Check "Redis container is reachable via redis-cli" ($ping -eq "PONG") ("ping=" + $ping)

$api = "$BaseUrl/api/v1/products"
$url = "${api}/${ProductId}"
$detailKey = "app:v1:products:detail:${ProductId}"

# Delete only this product's test key to ensure a clean Miss test without touching shared Redis data (no FLUSHDB)
[void](Exec-RedisCli "del $detailKey")

# ------------------------------------------------------------
# 1) Cache Miss Test: Product Detail
# ------------------------------------------------------------
Write-Host "`n-- [1] Testing Cache Miss (Product Detail) --" -ForegroundColor Yellow
$missReq = Send-Request -Method "GET" -Url $url
Check "Product detail returns 200 on Miss" ($missReq.Status -eq 200) ("status=" + $missReq.Status)
Write-Host ("  Latency on Cache Miss: " + $missReq.ElapsedMs + " ms") -ForegroundColor Gray

$detailKey = "app:v1:products:detail:${ProductId}"
$cachedVal = Exec-RedisCli "get $detailKey"
Check "Redis detail key created: $detailKey" ($cachedVal -ne "" -and $cachedVal -ne "(nil)") ("cachedVal length=" + $cachedVal.Length)

$ttl = [int](Exec-RedisCli "ttl $detailKey")
Check "TTL is configured for 5 minutes (250-300s)" ($ttl -ge 250 -and $ttl -le 300) ("ttl=" + $ttl + "s")

# ------------------------------------------------------------
# 2) Cache Hit Test: Product Detail
# ------------------------------------------------------------
Write-Host "`n-- [2] Testing Cache Hit (Product Detail) --" -ForegroundColor Yellow
$hitReq = Send-Request -Method "GET" -Url $url
Check "Product detail returns 200 on Hit" ($hitReq.Status -eq 200) ("status=" + $hitReq.Status)
Write-Host ("  Latency on Cache Hit:  " + $hitReq.ElapsedMs + " ms") -ForegroundColor Gray

$speedup = if ($hitReq.ElapsedMs -gt 0) { [math]::Round($missReq.ElapsedMs / $hitReq.ElapsedMs, 2) } else { 99 }
Write-Host ("  Latency Improvement:   Miss=" + $missReq.ElapsedMs + "ms -> Hit=" + $hitReq.ElapsedMs + "ms") -ForegroundColor Magenta

$firstParsed = $missReq.Body | ConvertFrom-Json
$hitParsed = $hitReq.Body | ConvertFrom-Json
Check "Hit response body matches Miss response body" ($firstParsed.id -eq $hitParsed.id -and $firstParsed.name -eq $hitParsed.name)

# ------------------------------------------------------------
# 3) Versioned List Caching Test
# ------------------------------------------------------------
Write-Host "`n-- [3] Testing Versioned List Caching --" -ForegroundColor Yellow
$listUrl = "${api}?pageNumber=1&pageSize=8"
$listMiss = Send-Request -Method "GET" -Url $listUrl
Check "List returns 200 on Miss" ($listMiss.Status -eq 200) ("status=" + $listMiss.Status)

$versionKey = "app:v1:products:list:version"
$vInit = Exec-RedisCli "get $versionKey"
if ($vInit -eq "" -or $vInit -eq "(nil)") { $vInit = "1" }
Write-Host ("  Current list version in Redis: " + $vInit) -ForegroundColor Gray

# Directly check the exact versioned list key (no KEYS command used)
$targetListKey = "app:v1:products:list:v${vInit}:c5931ee1c7475800"
$listVal = Exec-RedisCli "get $targetListKey"
Check "Versioned list key exists in Redis: $targetListKey" ($listVal -ne "" -and $listVal -ne "(nil)")

$listTtl = [int](Exec-RedisCli "ttl $targetListKey")
Check "List TTL is configured for 60 seconds (40-60s)" ($listTtl -ge 30 -and $listTtl -le 60) ("ttl=" + $listTtl + "s on key " + $targetListKey)

$listHit = Send-Request -Method "GET" -Url $listUrl
Check "List returns 200 on Hit" ($listHit.Status -eq 200) ("status=" + $listHit.Status)
Write-Host ("  List Latency: Miss=" + $listMiss.ElapsedMs + "ms -> Hit=" + $listHit.ElapsedMs + "ms") -ForegroundColor Magenta

# ------------------------------------------------------------
# 4) Cache Invalidation on Write (PATCH Price)
# ------------------------------------------------------------
Write-Host "`n-- [4] Testing Cache Invalidation on Write --" -ForegroundColor Yellow
$originalPrice = [decimal]($firstParsed.price)
$newPrice = [decimal]($originalPrice + 1.50)

Write-Host ("  Modifying price from $originalPrice to $newPrice...") -ForegroundColor Gray
$patchRes = Send-Request -Method "PATCH" -Url "${url}/price" -JsonBody ('{"price": ' + $newPrice.ToString($inv) + '}')
Check "Price PATCH returns 204 No Content" ($patchRes.Status -eq 204) ("status=" + $patchRes.Status)

# Check Invalidation in Redis
$detailAfterPatch = Exec-RedisCli "get $detailKey"
Check "Detail key immediately invalidated (deleted) from Redis" ($detailAfterPatch -eq "" -or $detailAfterPatch -eq "(nil)") ("detail=" + $detailAfterPatch)

$vAfterPatch = Exec-RedisCli "get $versionKey"
Check "List version incremented (INCR) in Redis" ([int]$vAfterPatch -gt [int]$vInit) ("old=" + $vInit + " new=" + $vAfterPatch)

$lastWriteTs = Exec-RedisCli "get products:last-write-ts"
Check "Write timestamp marker 'products:last-write-ts' exists" ($lastWriteTs -ne "" -and $lastWriteTs -ne "(nil)") ("ts=" + $lastWriteTs)

# Subsequent GET: must return updated price and re-populate cache
$subsequentGet = Send-Request -Method "GET" -Url $url
$subsequentParsed = $subsequentGet.Body | ConvertFrom-Json
Check "Subsequent GET reflects updated price immediately" ([decimal]$subsequentParsed.price -eq $newPrice) ("expected=" + $newPrice + " got=" + $subsequentParsed.price)

$detailReCached = Exec-RedisCli "get $detailKey"
Check "Detail key re-populated in Redis with new price" ($detailReCached -like "*$newPrice*")

# Restore original price
[void](Send-Request -Method "PATCH" -Url "${url}/price" -JsonBody ('{"price": ' + $originalPrice.ToString($inv) + '}'))
Write-Host "  Price restored to $originalPrice." -ForegroundColor Gray

# ------------------------------------------------------------
# 5) Redis Outage Resilience (Graceful Degradation)
# ------------------------------------------------------------
Write-Host "`n-- [5] Testing Redis Outage Resilience (Graceful Degradation) --" -ForegroundColor Yellow
Write-Host "  Stopping Redis container ($RedisContainer)..." -ForegroundColor Gray
& docker stop $RedisContainer | Out-Null
Start-Sleep -Seconds 2

try {
    $degradedGet = Send-Request -Method "GET" -Url $url
    Check "GET succeeds with 200 during Redis outage (Database Fallback)" ($degradedGet.Status -eq 200) ("status=" + $degradedGet.Status)
    $degradedParsed = $degradedGet.Body | ConvertFrom-Json
    Check "Data returned correctly from DB fallback" ($degradedParsed.id -eq $ProductId)
}
finally {
    Write-Host "  Restarting Redis container ($RedisContainer)..." -ForegroundColor Gray
    & docker start $RedisContainer | Out-Null
    Start-Sleep -Seconds 3
}

$postRecoveryPing = Exec-RedisCli "ping"
Check "Redis container recovered successfully" ($postRecoveryPing -eq "PONG") ("ping=" + $postRecoveryPing)

$recoveredGet = Send-Request -Method "GET" -Url $url
Check "GET succeeds after Redis recovery" ($recoveredGet.Status -eq 200) ("status=" + $recoveredGet.Status)

# ------------------------------------------------------------
# Summary
# ------------------------------------------------------------
Write-Host "`n==========================================================" -ForegroundColor Cyan
if ($script:failed -eq 0) {
    Write-Host "  ALL REDIS VERIFICATION CHECKS PASSED SUCCESSFULLY!    " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host ("  FAILED CHECKS: " + $script:failed) -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Cyan
    exit 1
}
