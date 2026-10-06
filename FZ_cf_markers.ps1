$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines: $($f.Length)"

function FindLine($pattern) {
    for ($i=0; $i -lt $f.Length; $i++) {
        if ($f[$i] -match $pattern) { return $i }
    }
    return -1
}

$m1 = FindLine "private Button _connectButton = null!;"
$m2 = FindLine "RowCount = 4"
$m3 = FindLine 'Text = "Подключиться и открыть объекты"'
$m4 = FindLine 'Controls.Add\(_connectButton, 1, 2\)'
$m5 = FindLine 'Controls.Add\(_status, 1, 3\)'
$m6 = FindLine 'await database\.EnsureDatabaseAndSchemaAsync\(\)'
$m7 = FindLine 'using \(var objects = new ObjectBrowserForm\(database\)\)'
$m8 = FindLine 'Text = "Подключение и применение миграции\.\.\.";', -exact
$m9 = FindLine '^\s*}\s*$' | Sort-Object | Select-Object -Last 2

Write-Host "m1 (connectButton field) = $m1"
Write-Host "m2 (RowCount) = $m2"
Write-Host "m3 (connectBtn button) = $m3"
Write-Host "m4 (add connectButton control) = $m4"
Write-Host "m5 (add status control) = $m5"
Write-Host "m6 (EnsureDatabaseAndSchema) = $m6"
Write-Host "m7 (ObjectBrowserForm call) = $m7"
Write-Host "m8 (status text) = $m8"
Write-Host "m9 last lines = $m9"
