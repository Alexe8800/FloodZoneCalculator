$p = "FloodZoneDb.Client\FloodZoneConnection.cs"
$f = Get-Content $p -Encoding UTF8
$i=71
$insert="            VerifyDatabaseAccess();"
$f2 = $f[0..$i]
$f2 += $insert
$f3 = $f[($i+1)..($f.Length-1)]
$f2 += $f3
$f2 = $f2 -join "`n"
[System.IO.File]::WriteAllText($p, $f2, [System.Text.UTF8Encoding]::new($false))
Write-Host "lines now: $($f2.Length)"
