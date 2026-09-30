param(
    [switch]$Build,
    [switch]$Check,
    [switch]$UiCheck,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path (Split-Path $repoRoot -Parent) '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localSdk) {
    $sdk = $localSdk
} else {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $command) { throw 'Instala el SDK .NET 10 para compilar End3r Draw.' }
    $sdk = $command.Source
    if (-not ((& $sdk --list-sdks) -match '^10\.')) { throw 'Se necesita el SDK .NET 10; el runtime por si solo no permite compilar.' }
}

$app = Join-Path $repoRoot 'PolyCut/bin/Release/net10.0-windows/PolyCut.dll'
Push-Location $repoRoot
try {
    if ($Build -or -not (Test-Path -LiteralPath $app)) {
        & $sdk build PolyCut/PolyCut.vbproj -c Release --nologo -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'La compilacion fallo.' }
    }
    if ($Check -or $UiCheck) {
        $arguments = @('run', '--project', 'tests/End3rDraw.Checks', '-c', 'Release')
        if ($UiCheck) { $arguments += @('--', '--ui', (Join-Path $repoRoot 'artifacts/smoke')) }
        & $sdk @arguments
        if ($LASTEXITCODE -ne 0) { throw 'Las comprobaciones fallaron.' }
    }
    if (-not $NoLaunch) {
        Start-Process -FilePath $sdk -ArgumentList @('"' + $app + '"') -WorkingDirectory $repoRoot -WindowStyle Hidden
    }
} finally {
    Pop-Location
}
