#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
$logPath = Join-Path $env:TEMP 'AlternateEarth-firewall-setup.log'

try {
    $serverPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\AlternateEarth.Server\bin\Release\net8.0\AlternateEarth.Server.exe'))
    $blockingRules = Get-NetFirewallRule -Enabled True -Direction Inbound -Action Block |
        Where-Object {
            $application = $_ | Get-NetFirewallApplicationFilter
            $port = $_ | Get-NetFirewallPortFilter
            $application.Program -ieq $serverPath -and $port.Protocol -eq 'TCP'
        }
    $blockingRules | Disable-NetFirewallRule

    $ruleName = 'AlternateEarth-TCP-5080-LocalSubnet'
    Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule `
        -Name $ruleName `
        -DisplayName 'Alternate Earth Reality Server TCP 5080 (Local subnet)' `
        -Direction Inbound `
        -Action Allow `
        -Enabled True `
        -Profile Any `
        -Protocol TCP `
        -LocalPort 5080 `
        -RemoteAddress LocalSubnet | Out-Null

    "SUCCESS`nRule=$ruleName`nPort=5080`nRemoteAddress=LocalSubnet" | Set-Content -LiteralPath $logPath
    exit 0
}
catch {
    ($_ | Format-List * -Force | Out-String) | Set-Content -LiteralPath $logPath
    exit 1
}
