[CmdletBinding()]
param(
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$certificatePath = Join-Path $root 'CrosshairOverlayWidget.cer'
$packagePath = Join-Path $root 'CrosshairOverlayWidget.msix'
$desktopPath = Join-Path $root 'CrosshairOverlay\CrosshairOverlay.exe'

function Install-CrosshairPackage {
    if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
        throw "Certificate not found: $certificatePath"
    }

    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Widget package not found: $packagePath"
    }

    & "$env:SystemRoot\System32\certutil.exe" -user -addstore -f TrustedPeople $certificatePath *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to trust the Widget signing certificate."
    }

    Add-AppxPackage -LiteralPath $packagePath -ForceUpdateFromAnyVersion -ErrorAction Stop

    $installedPackage = Get-AppxPackage -Name 'CrosshairOverlayWidget' -ErrorAction SilentlyContinue
    if ($null -eq $installedPackage) {
        throw 'The Widget package was not registered after installation.'
    }
}

try {
    try {
        Install-CrosshairPackage
    }
    catch {
        if ($Elevated) {
            throw
        }

        Write-Host 'Retrying installation with administrator privileges...'
        $argumentList = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Elevated"
        $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -PassThru -ArgumentList $argumentList
        if ($process.ExitCode -ne 0) {
            throw "Elevated installation failed with exit code $($process.ExitCode)."
        }
    }

    if (-not (Test-Path -LiteralPath $desktopPath -PathType Leaf)) {
        throw "Desktop executable not found: $desktopPath"
    }

    Start-Process -FilePath $desktopPath
    Write-Host 'Crosshair Overlay installed and started.'
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
