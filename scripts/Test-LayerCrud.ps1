param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$layerId = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(12)).ToLowerInvariant()
$createdByTest = $false
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
function Request([string]$method, [string]$path, $body = $null) {
    $options = @{ Method = $method; Uri = "$BaseUrl$path"; SkipHttpErrorCheck = $true }
    if ($null -ne $body) {
        $options.ContentType = 'application/json; charset=utf-8'
        $options.Body = $body | ConvertTo-Json -Depth 30 -Compress
    }
    Invoke-WebRequest @options
}
function SameJson($expected, $actual) {
    $left = [System.Text.Json.Nodes.JsonNode]::Parse(($expected | ConvertTo-Json -Depth 30 -Compress))
    $right = [System.Text.Json.Nodes.JsonNode]::Parse(($actual | ConvertTo-Json -Depth 30 -Compress))
    [System.Text.Json.Nodes.JsonNode]::DeepEquals($left, $right)
}
try {
    $data = @{
        layer_id = 'sadasdnbvns'; layer_desc = ''
        mark_ups = @(@{ mark_up_id = 1; mark_up_name = "abcd ' 한글" }, @{ mark_up_id = 2; mark_up_name = 'second' })
        tag = @{ tag_idx = 'dasdfasdaa'; tag_desc = '레이어내의 부품' }
        extra_field = @{ nullable = $null; enabled = $true; value = 12.5 }
    }
    $created = Request POST '/api/layers' @{ id = $layerId; data = $data }
    Assert ($created.StatusCode -eq 201) "Create failed: $($created.StatusCode) $($created.Content)"
    $createdByTest = $true
    Assert ($created.Headers.Location[0].EndsWith("/api/layers/$layerId")) 'Location mismatch.'
    $read = Request GET "/api/layers/$layerId"
    Assert ($read.StatusCode -eq 200) 'Read failed.'
    $row = $read.Content | ConvertFrom-Json
    Assert ($row.id -eq $layerId) 'ID mismatch.'
    Assert (SameJson $data $row.data) 'JSON structure, fields, values or array order changed.'
    Assert ((Request POST '/api/layers' @{ id = $layerId; data = $data }).StatusCode -eq 409) 'Duplicate must return 409.'
    $rows = (Request GET '/api/layers').Content | ConvertFrom-Json
    Assert ($layerId -in $rows.id) 'List missing created layer.'
    foreach ($invalid in @(@{id='';data=$data}, @{id=('a'*16);data=$data}, @{id=('a'*23);data=$data}, @{id=('a'*25);data=$data}, @{id=('g'*24);data=$data}, @{id=$layerId;data=$null}, @{id=$layerId;data='text'}, @{id=$layerId;data=@(1,2)}, @{id=$layerId})) {
        Assert ((Request POST '/api/layers' $invalid).StatusCode -eq 400) 'Invalid create must return 400.'
    }
    Assert ((Request PUT "/api/layers/$layerId" @{data=$null}).StatusCode -eq 400) 'Invalid update must return 400.'
    $replacement = @{ layer_id='sadasdnbvns'; layer_desc='변경'; mark_ups=@(); tag=@{tag_idx='new';tag_desc='수정'} }
    Assert ((Request PUT "/api/layers/$layerId" @{data=$replacement}).StatusCode -eq 200) 'Update failed.'
    $after = (Request GET "/api/layers/$layerId").Content | ConvertFrom-Json
    Assert (SameJson $replacement $after.data) 'PUT did not replace the entire JSON object.'
    Assert ((Request DELETE "/api/layers/$layerId").StatusCode -eq 204) 'Delete failed.'
    $createdByTest = $false
    Assert ((Request GET "/api/layers/$layerId").StatusCode -eq 404) 'Deleted row must return 404.'
    Assert ((Request PUT "/api/layers/$layerId" @{data=$replacement}).StatusCode -eq 404) 'Missing update must return 404.'
    Assert ((Request DELETE "/api/layers/$layerId").StatusCode -eq 404) 'Missing delete must return 404.'
    Write-Output 'PASS: LAYER CRUD, JSON round trip, full replacement, duplicate ID, validation and 404 responses.'
}
finally { if ($createdByTest) { $null = Request DELETE "/api/layers/$layerId" } }
