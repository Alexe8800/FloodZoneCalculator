$p = "FloodZoneDb.Client\FloodZoneConnection.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines: $($f.Length)"
$insert="            VerifyDatabaseAccess();"
# Remove existing call (currently at 0-based index 72, 1-based line 73)
$f2 = $f[0..70]
$f2 += $insert
$f3 = $f[73..($f.Length-1)]
$f2 += $f3
$f2 = $f2 -join "`n"
[System.IO.File]::WriteAllText($p, $f2, [System.Text.UTF8Encoding]::new($false))
Write-Host "lines now: $($f2.Length)"
