param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$id = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$created = $false
function Check($condition, $message) { if (-not $condition) { throw $message } }
function Send($method, $body, $contentType = 'application/json') {
    $uri = if ($method -eq 'POST') { "$BaseUrl/api/layers" } else { "$BaseUrl/api/layers/$id" }
    $args = @{ Method=$method; Uri=$uri; SkipHttpErrorCheck=$true }
    if ($null -ne $body) { $args.Body = ConvertTo-Json -InputObject $body -Depth 30 -Compress; $args.ContentType=$contentType }
    Invoke-WebRequest @args
}
function Patch($ops) { Send 'PATCH' $ops 'application/json-patch+json' }
try {
    $response = Send POST @{id=$id;data=@{tag=@{tag_desc='before';keep=$true};mark_ups=@(@{mark_up_id=1;mark_up_name='first'})}}
    Check ($response.StatusCode -eq 201) 'Create failed'
    $created = $true
    $response = Patch @(
        @{op='replace';path='/data/tag/tag_desc';value='변경'},
        @{op='replace';path='/data/mark_ups/0/mark_up_name';value='changed'},
        @{op='add';path='/data/mark_ups/-';value=@{mark_up_id=2;mark_up_name='second'}},
        @{op='add';path='/data/a~1b';value=@{'~key'=$null}},
        @{op='test';path='/data/a~1b/~0key';value=$null}
    )
    Check ($response.StatusCode -eq 200) "Patch failed: $($response.Content)"
    $row = (Send GET $null).Content | ConvertFrom-Json
    Check ($row.data.tag.tag_desc -eq '변경' -and $row.data.tag.keep -and $row.data.mark_ups.Count -eq 2 -and $row.data.mark_ups[0].mark_up_name -eq 'changed') 'Nested update failed'
    $before = (Send GET $null).Content
    $response = Patch @(@{op='replace';path='/data/tag/tag_desc';value='must rollback'},@{op='remove';path='/data/mark_ups/99'})
    Check ($response.StatusCode -eq 400) 'Invalid index must return 400'
    Check ((Send GET $null).Content -eq $before) 'Partial patch persisted after failure'
    $response = Patch @(@{op='replace';path='/data/tag/tag_desc';value='must rollback'},@{op='test';path='/data/tag/keep';value=$false})
    Check ($response.StatusCode -eq 409) 'Failed test must return 409'
    Check ((Send GET $null).Content -eq $before) 'Failed test persisted changes'
    foreach ($op in @(
        @{op='replace';path='/id';value='forbidden'},
        @{op='remove';path='/data'},
        @{op='replace';path='/data';value=@()},
        @{op='replace';path='/data/missing';value=1},
        @{op='remove';path='/data/mark_ups/01'},
        @{op='remove';path='/data/bad~2escape'},
        @{op='move';path='/data/tag';from='/data/mark_ups'},
        @{op='add';path='/data/missing_value'}
    )) { Check ((Patch @($op)).StatusCode -eq 400) 'Invalid operation must return 400' }
    Check ((Send PATCH @(@{op='remove';path='/data/tag'}) 'application/json').StatusCode -eq 415) 'Wrong content type must return 415'
    Check ((Patch @(@{op='add';path='/data/mark_ups/1';value=@{mark_up_id=3}},@{op='remove';path='/data/mark_ups/0'},@{op='remove';path='/data/tag/tag_desc'})).StatusCode -eq 200) 'Insert/remove failed'
    $row = (Send GET $null).Content | ConvertFrom-Json
    Check ($row.data.mark_ups[0].mark_up_id -eq 3 -and $row.data.mark_ups[1].mark_up_id -eq 2 -and $null -eq $row.data.tag.tag_desc) 'Array insert/remove order incorrect'

    # Concurrent disjoint changes must both survive: row lock protects read/modify/write.
    $statuses = 1..8 | ForEach-Object -Parallel {
        $body = '[{"op":"add","path":"/data/concurrent' + $_ + '","value":true}]'
        (Invoke-WebRequest -Method Patch -Uri "$using:BaseUrl/api/layers/$using:id" -ContentType 'application/json-patch+json' -Body $body -SkipHttpErrorCheck).StatusCode
    } -ThrottleLimit 8
    Check (@($statuses | Where-Object { $_ -ne 200 }).Count -eq 0) 'Concurrent requests failed'
    $row = (Send GET $null).Content | ConvertFrom-Json
    foreach ($number in 1..8) { Check ($row.data."concurrent$number" -eq $true) 'Concurrent update lost' }
    Check ((Send DELETE $null).StatusCode -eq 204) 'Cleanup failed'
    $created = $false
    Check ((Patch @()).StatusCode -eq 404) 'Missing layer must return 404'
    Write-Output 'PASS: nested/array PATCH, escaped paths, null, atomic rollback, validation, concurrent updates and 404.'
} finally { if ($created) { $null = Send DELETE $null } }
