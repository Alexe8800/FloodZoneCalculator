$content = Get-Content "C:\temp\pg_hba_new.conf" -Encoding UTF8
$fixed = $content | ForEach-Object {
    if ($_ -match 'host\s+all\s+all\s+(127.0.0.1/32|::1/128)\s+trust')
    {
        $_ -replace 'trust', 'md5'
    }
    else
    {
        $_
    }
}
$fixed | Out-File "C:\Program Files\PostgreSQL\12\data\pg_hba.conf" -Encoding UTF8 -NoNewline
Write-Output "RESTORED:"
for ($i = 85; $i -lt 90; $i++)
{
    Write-Output (($i + 1).ToString() + ': ' + $content[$i])
}