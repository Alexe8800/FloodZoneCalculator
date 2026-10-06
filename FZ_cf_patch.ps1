$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines before: $($f.Length)"

# 2: isOffline: false (index 82 = строка 83)
$f[82] = $f[82] -replace 'new ObjectBrowserForm\(database\)', 'new ObjectBrowserForm(database, isOffline: false)'
Write-Host "after 2: $($f.Length)"

# 1: вернуть var database после _connectButton.Enabled (index 68)
$new1 = "                var database = new FloodZoneConnection(_connectionText.Text);"
$f = $f[0..68] + $new1 + $f[69..($f.Length-1)]
Write-Host "after 1: $($f.Length)"

Set-Content -Path $p -Value ($f -join "`n") -Encoding UTF8
Write-Host "written"
