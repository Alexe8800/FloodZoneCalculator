$lines = Get-Content "Presentation/MainForm.cs" -Encoding UTF8
for($i=1741; $i -le 1780; $i++)
{
    Write-Output (($i).ToString() + ': ' + $lines[$i-1])
}