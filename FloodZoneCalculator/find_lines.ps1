$lines = Get-Content "Presentation/MainForm.cs" -Encoding UTF8
for($i=1; $i -le $lines.Count; $i++)
{
    if($lines[$i-1] -match 'await _database.SaveCalculationAsync\(_objectId,')
    {
        Write-Output ($i.ToString() + ": " + $lines[$i-1])
    }
}