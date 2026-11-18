param(
    [string]$Url = "http://localhost:5200/scalar",
    [int]$TimeoutSeconds = 120,
    [int]$PollIntervalSeconds = 1,
    [switch]$Watch
)

if (-not $Watch) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $escapedUrl = $Url.Replace("'", "''")

    Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$scriptPath`"",
        "-Url", "`"$escapedUrl`"",
        "-TimeoutSeconds", $TimeoutSeconds,
        "-PollIntervalSeconds", $PollIntervalSeconds,
        "-Watch"
    ) | Out-Null

    exit 0
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

while ((Get-Date) -lt $deadline) {
    try {
        Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 | Out-Null
        Start-Process $Url
        exit 0
    }
    catch {
        Start-Sleep -Seconds $PollIntervalSeconds
    }
}

Write-Error "Timed out waiting for $Url after $TimeoutSeconds seconds."
exit 1
