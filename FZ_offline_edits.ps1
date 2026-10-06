$fullPath = "FloodZoneDb.Client\FloodZoneConnection.cs"
$f = Get-Content $fullPath -Encoding UTF8
Write-Host "lines before: $($f.Length)"

# --- A: конструктор ---
$start=0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i].Trim() -eq "public FloodZoneConnection(string connectionString)") { $start=$i; break } }
$end=$start; for ($i=$start; $i -lt $f.Length; $i++) { if ($f[$i] -eq "        _connectionString = connectionString;") { $end=$i; break } }
$nlA = @("    private readonly string? _connectionString;", "    private readonly bool _isOffline;", "", "    public FloodZoneConnection(string connectionString, bool isOffline = false)", "    {", "        _connectionString = connectionString;", "        _isOffline = isOffline;", "    }", "", "    public FloodZoneConnection(string? connectionString)", "    {", "        _connectionString = connectionString;", "        _isOffline = connectionString is null;", "    }")
$f = $f[0..($start-1)] + $nlA + $f[($end+1)..($f.Length-1)]

# --- B: свойство IsOffline ---
$idx=0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i].Trim() -eq "public static FloodZoneConnection FromEnvironment()") { $idx=$i; break } }
$f = $f[0..($idx-1)] + @("    public bool IsOffline => _isOffline;", "") + $f[$idx..($f.Length-1)]

# --- D: VerifyDatabaseAccess метод ---
$idx=0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i].Trim() -eq "public static FloodZoneConnection FromEnvironment()") { $idx=$i; break } }
$nlD = @("    private void VerifyDatabaseAccess()", "    {", "        if (_isOffline) throw new InvalidOperationException(""Работа с базой данных недоступна в оффлайн-режиме."");", "        if (_connectionString == null || string.IsNullOrWhiteSpace(_connectionString))", "            throw new InvalidOperationException(""Строка подключения не задана."");", "    }", "")
$f = $f[0..($idx-1)] + $nlD + $f[$idx..($f.Length-1)]

# --- C: CanConnectAsync ---
$idx=0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i].Trim() -eq "public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)") { $idx=$i; break } }
$tryIdx=0; for ($i=$idx; $i -lt $f.Length; $i++) { if ($f[$i] -eq "        try") { $tryIdx=$i; break } }
$nlC = @("        if (_isOffline)", "        {", "            DatabaseLogger.Info(""Оффлайн-режим: проверка подключения пропущена."");", "            return false;", "        }")
$f = $f[0..($tryIdx-1)] + $nlC + $f[$tryIdx..($f.Length-1)]

# --- E: вызов в CRUD ---
$inserts = @()
for ($i=0; $i -lt $f.Length-1; $i++) {
  $line=$f[$i]
  if ($line.Trim().EndsWith("CancellationToken cancellationToken = default)") -and $f[$i+1].Trim() -eq "{") {
    $k=$i-1
    while ($k -ge 0 -and $f[$k] -notmatch "public async Task") { $k-- }
    if ($k -ge 0 -and $f[$k] -match "public async Task" -and $f[$k] -notlike "*CanConnectAsync*") {
      $i1=$i+1
      $indent = $f[$i1].Substring(0, $f[$i1].Length - $f[$i1].TrimStart().Length)
      $inserts += [PSCustomObject]@{ idx=$i1; text=($indent + "    " + "VerifyDatabaseAccess();"); method=$f[$k] }
    }
  }
}
Write-Host "inserts: $($inserts.Count)"
foreach ($it in $inserts) { Write-Host "  at $((($it.idx)+1)): $($it.method)" }
foreach ($it in ($inserts | Sort-Object -Property idx -Descending)) {
  $f = $f[0..$($it.idx)] + $it.text + $f[($($it.idx)+1)..($f.Length-1)]
}
Write-Host "after E: $($f.Length)"

$f = $f -join "`n"
[System.IO.File]::WriteAllText($fullPath, $f, [System.Text.UTF8Encoding]::new($false))
Write-Host "file written OK"
