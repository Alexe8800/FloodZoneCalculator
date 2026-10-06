$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines before: $($f.Length)"
$idx58 = 0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i] -like "layout.Controls.Add(_connectButton, 1, 2);") { $idx58=$i; break } }
Write-Host "removing old connectButton line at $idx58"
$f = $f[0..$idx58] + $f[($idx58+1)..($f.Length-1)]
Write-Host "after remove: $($f.Length)"
$f = $f -replace '                                if (!await database.CanConnectAsync\(\))', '                if (!await database.CanConnectAsync())'
$f = $f -replace 'using \(var objects = new ObjectBrowserForm\(database\)\)', 'using (var objects = new ObjectBrowserForm(database, isOffline: false))'
Write-Host "final lines: $($f.Length)"
$f = $f -join "`n"
[System.IO.File]::WriteAllText($p, $f, [System.Text.UTF8Encoding]::new($false))
Write-Host "written"
