# Shared PowerShell functions

function Get-Config {
    param (
        [string]$LocalConfigPath = "./appsettings.local.json",
        [string]$DefaultConfigPath = "./appsettings.json"
    )
    # Try to read settings from appsettings.local.json first, fall back to appsettings.json
    if (Test-Path -Path $LocalConfigPath) {
        Write-Host "Reading settings from $LocalConfigPath"
        $appSettings = Get-Content -Path $LocalConfigPath -Raw | ConvertFrom-Json
    } elseif (Test-Path -Path $DefaultConfigPath) {
        Write-Host "Reading settings from $DefaultConfigPath"
        $appSettings = Get-Content -Path $DefaultConfigPath -Raw | ConvertFrom-Json
    } else {
        Write-Host "No configuration files found. Will use hardcoded values."
        # Return null so the calling code knows no settings were found
        return $null
    }
    Write-Host "Settings loaded successfully"
    return $appSettings
}
