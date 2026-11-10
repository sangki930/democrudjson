param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$id = $null
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Request([string]$method, [string]$path, $body = $null) {
    $options = @{ Method = $method; Uri = "$BaseUrl$path"; SkipHttpErrorCheck = $true }
    if ($null -ne $body) {
        $options.ContentType = 'application/json; charset=utf-8'
        $options.Body = $body | ConvertTo-Json -Compress
    }
    Invoke-WebRequest @options
}
try {
    Assert ((Request GET '/health').StatusCode -eq 200) 'Database health check failed.'
    $created = Request POST '/api/items' @{ name = "CRUD 한글 ' test"; description = 'integration test' }
    Assert ($created.StatusCode -eq 201) 'Create must return 201.'
    $item = $created.Content | ConvertFrom-Json
    $id = $item.id
    Assert ($item.createdAt -and $item.updatedAt) 'Database-generated timestamps missing.'
    Assert ($created.Headers.Location[0] -eq "/api/items/$id") 'Location header mismatch.'
    $read = Request GET "/api/items/$id"
    Assert ($read.StatusCode -eq 200) 'Read must return 200.'
    Assert (($read.Content | ConvertFrom-Json).name -eq "CRUD 한글 ' test") 'Name did not round trip.'
    $list = (Request GET '/api/items').Content | ConvertFrom-Json
    Assert ($id -in $list.id) 'Created item missing from list.'
    $updated = Request PUT "/api/items/$id" @{ name = 'Updated'; description = $null; isCompleted = $true }
    Assert ($updated.StatusCode -eq 200) 'Update must return 200.'
    $after = (Request GET "/api/items/$id").Content | ConvertFrom-Json
    Assert ($after.name -eq 'Updated' -and $after.isCompleted -and $null -eq $after.description) 'Update was not persisted.'
    Assert ($after.createdAt -eq $item.createdAt) 'Update changed creation timestamp.'
    Assert ([datetime]$after.updatedAt -ge [datetime]$item.updatedAt) 'Update timestamp went backwards.'
    Assert ((Request POST '/api/items' @{ description = 'Missing name' }).StatusCode -eq 400) 'Missing name must return 400.'
    Assert ((Request POST '/api/items' @{ name = $null }).StatusCode -eq 400) 'Null name must return 400.'
    Assert ((Request POST '/api/items' @{ name = ' ' }).StatusCode -eq 400) 'Blank name must return 400.'
    Assert ((Request POST '/api/items' @{ name = ('a' * 201) }).StatusCode -eq 400) 'Long name must return 400.'
    Assert ((Request PUT "/api/items/$id" @{ name = 'Valid'; description = ('x' * 2001) }).StatusCode -eq 400) 'Long description must return 400.'
    Assert ((Request DELETE "/api/items/$id").StatusCode -eq 204) 'Delete must return 204.'
    Assert ((Request GET "/api/items/$id").StatusCode -eq 404) 'Deleted item must return 404.'
    Assert ((Request PUT "/api/items/$id" @{ name = 'Missing' }).StatusCode -eq 404) 'Missing update must return 404.'
    Assert ((Request DELETE "/api/items/$id").StatusCode -eq 404) 'Missing delete must return 404.'
    $id = $null
    Write-Output 'PASS: PostgreSQL health, CRUD, Unicode/quote round trip, validation, and 404 responses.'
}
finally {
    if ($id) { $null = Request DELETE "/api/items/$id" }
}
