[CmdletBinding()]
param([switch]$Terminal)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$temp = Join-Path ([IO.Path]::GetTempPath()) ("technobabble-check-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
try {
    $name = if ($Terminal) { 'Technobabble-Terminal.exe' } else { 'Technobabble.exe' }
    $exe = Join-Path $temp $name
    Copy-Item (Join-Path $root $name) $exe
    $test = Join-Path $temp 'Technobabble.Tests.exe'
    $flags = @('/nologo','/codepage:65001','/reference:System.Drawing.dll',"/reference:$exe","/out:$test")
    if ($Terminal) { $flags += '/define:TERMINAL' }
    & $compiler @flags (Join-Path $PSScriptRoot 'Checks.cs') (Join-Path $PSScriptRoot 'StudioChecks.cs') (Join-Path $PSScriptRoot 'PaneEditorChecks.cs') (Join-Path $PSScriptRoot 'CodeScrollChecks.cs') (Join-Path $PSScriptRoot 'RelayChecks.cs')
    if ($LASTEXITCODE) { throw 'Test compilation failed.' }
    & $test
    if ($LASTEXITCODE) { throw 'Checks failed.' }
} finally {
    foreach ($item in 'Technobabble.Tests.exe',$name) {
        $file = Join-Path $temp $item
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    if ((Get-ChildItem -LiteralPath $temp).Count -eq 0) { Remove-Item -LiteralPath $temp }
}

