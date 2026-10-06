$lines = Get-Content "FloodZoneDb.Client\FloodZoneConnection.cs" -Encoding UTF8
$start = [Math]::Max(0, $lines.Count - 25)
for($i=$start; $i -lt $lines.Count; $i++)
{
    Write-Output (($i+1).ToString() + ': ' + $lines[$i])
}