$lines = Get-Content "FloodZoneDb.Client\FloodZoneConnection.cs" -Encoding UTF8
for($i=1; $i -le $lines.Count; $i++)
{
    if($lines[$i-1] -match 'public async Task SaveCalculationAsync')
    {
        for($j=$i-1; $j -lt $lines.Count -and $j -lt $i+15; $j++)
        {
            Write-Output (($j+1).ToString() + ': ' + $lines[$j])
        }
        break
    }
}