param(
    [string]$BaseUrl = "http://localhost:5097",
    [string]$ProductId = ""
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
        [string]$IfNoneMatch = "",
        [string]$JsonBody = ""
    )
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Url)
    if ($IfNoneMatch -ne "") {
        [void]$request.Headers.TryAddWithoutValidation("If-None-Match", $IfNoneMatch)
    }
    if ($JsonBody -ne "") {
        $request.Content = [System.Net.Http.StringContent]::new($JsonBody, [System.Text.Encoding]::UTF8, "application/json")
    }
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $etag = ""
    if ($null -ne $response.Headers.ETag) { $etag = $response.Headers.ETag.ToString() }
    $cache = ""
    if ($null -ne $response.Headers.CacheControl) { $cache = $response.Headers.CacheControl.ToString() }
    return [pscustomobject]@{
        Status       = [int]$response.StatusCode
        ETag         = $etag
        CacheControl = $cache
        Body         = $body
        Bytes        = [System.Text.Encoding]::UTF8.GetByteCount($body)
    }
}

function Check {
    param([string]$Name, [bool]$Condition, [string]$Detail = "")
    if ($Condition) {
        Write-Host ("PASS  " + $Name) -ForegroundColor Green
    } else {
        Write-Host ("FAIL  " + $Name + "   " + $Detail) -ForegroundColor Red
        $script:failed++
    }
}

$api = "$BaseUrl/api/v1/products"

Write-Host "== 1) First GET of the list"
$list = Send-Request -Method "GET" -Url "${api}?pageNumber=1&pageSize=3"
Check "list returns 200" ($list.Status -eq 200) ("status=" + $list.Status)
Check "list has an ETag" ($list.ETag -ne "")
Check "Cache-Control is no-cache and has no max-age" (($list.CacheControl -match "no-cache") -and ($list.CacheControl -notmatch "max-age")) ("value=" + $list.CacheControl)

if ($ProductId -eq "") {
    $parsed = $list.Body | ConvertFrom-Json
    if ($parsed.items.Count -eq 0) {
        Write-Host "No products found. Create one first." -ForegroundColor Yellow
        exit 1
    }
    $ProductId = $parsed.items[0].id
}
$url = "${api}/${ProductId}"
Write-Host ("Using product: " + $ProductId)

Write-Host "== 2) Product details: 200 then 304"
$first = Send-Request -Method "GET" -Url $url
Check "details return 200 with an ETag" (($first.Status -eq 200) -and ($first.ETag -ne "")) ("status=" + $first.Status)
$etag1 = $first.ETag

$second = Send-Request -Method "GET" -Url $url -IfNoneMatch $etag1
Check "same ETag -> 304" ($second.Status -eq 304) ("status=" + $second.Status)
Check "304 has an empty body" ($second.Bytes -eq 0) ("bytes=" + $second.Bytes)
Check "304 keeps the same ETag" ($second.ETag -eq $etag1)

$weak = Send-Request -Method "GET" -Url $url -IfNoneMatch ("W/" + $etag1)
Check "weak form W/ETag -> 304" ($weak.Status -eq 304) ("status=" + $weak.Status)

$multi = Send-Request -Method "GET" -Url $url -IfNoneMatch ('"does-not-exist", ' + $etag1)
Check "several values, one matches -> 304" ($multi.Status -eq 304) ("status=" + $multi.Status)

$wrong = Send-Request -Method "GET" -Url $url -IfNoneMatch '"does-not-exist"'
Check "different ETag -> 200 with a body" (($wrong.Status -eq 200) -and ($wrong.Bytes -gt 0)) ("status=" + $wrong.Status)

Write-Host "== 3) Different query gives a different ETag"
$other = Send-Request -Method "GET" -Url "${api}?pageNumber=1&pageSize=2"
Check "different query -> different ETag" (($other.ETag -ne "") -and ($other.ETag -ne $list.ETag))

Write-Host "== 4) Errors must not carry an ETag"
$missing = Send-Request -Method "GET" -Url "${api}/$([guid]::NewGuid())"
Check "unknown product -> 404" ($missing.Status -eq 404) ("status=" + $missing.Status)
Check "404 has no ETag" ($missing.ETag -eq "")

Write-Host "== 5) Edit the product, then GET with the OLD ETag"
$current = [decimal](($first.Body | ConvertFrom-Json).price)
$newPrice = $current + 1
$patch = Send-Request -Method "PATCH" -Url "${url}/price" -JsonBody ('{"price": ' + $newPrice.ToString($inv) + '}')
Check "price change returns 204" ($patch.Status -eq 204) ("status=" + $patch.Status)

$after = Send-Request -Method "GET" -Url $url -IfNoneMatch $etag1
Check "old ETag after the edit -> 200, not 304" ($after.Status -eq 200) ("status=" + $after.Status)
Check "a new ETag is returned" (($after.ETag -ne "") -and ($after.ETag -ne $etag1))
$seenPrice = [decimal](($after.Body | ConvertFrom-Json).price)
Check "the new price is visible immediately" ($seenPrice -eq $newPrice) ("expected=" + $newPrice + " got=" + $seenPrice)

[void](Send-Request -Method "PATCH" -Url "${url}/price" -JsonBody ('{"price": ' + $current.ToString($inv) + '}'))
Write-Host "Price restored to its original value."

Write-Host "== 6) Bandwidth: 200 versus 304 (response body bytes)"
$big = Send-Request -Method "GET" -Url "${api}?pageNumber=1&pageSize=50"
$big304 = Send-Request -Method "GET" -Url "${api}?pageNumber=1&pageSize=50" -IfNoneMatch $big.ETag
Write-Host ("Details: 200 = " + $first.Bytes + " bytes, 304 = " + $second.Bytes + " bytes")
if ($big.Bytes -gt 0) {
    $saved = [math]::Round(100 * ($big.Bytes - $big304.Bytes) / $big.Bytes, 1)
    Write-Host ("List (50 items): 200 = " + $big.Bytes + " bytes, 304 = " + $big304.Bytes + " bytes, saved " + $saved + " percent")
}

Write-Host ""
if ($script:failed -eq 0) {
    Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host ("FAILED CHECKS: " + $script:failed) -ForegroundColor Red
    exit 1
}