[CmdletBinding()]
param([switch]$Terminal, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (!$OutputPath) {
    $name = if ($Terminal) { 'Technobabble-Terminal.exe' } else { 'Technobabble.exe' }
    $OutputPath = Join-Path (Join-Path $PSScriptRoot '..') $name
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework 4.x C# compiler is required.' }
Push-Location $PSScriptRoot
try {
    $flags = @('/nologo','/platform:anycpu','/utf8output','/codepage:65001','/optimize+',
        '/win32manifest:app.manifest','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',
        '/resource:assets\map.jpg,map.jpg','/resource:assets\wave.jpg,wave.jpg',
        '/resource:assets\standard.flf,standard.flf',"/out:$OutputPath")
    $sharedSource = @(Get-ChildItem -Path . -Filter *.cs)
    $terminalSource = @(Get-ChildItem -Path terminal -Filter *.cs)
    # Both frontends display the same library. The original three resources
    # keep their names for Classic code and bat.
    foreach ($file in ($sharedSource + $terminalSource)) {
        if ($file.Name -eq 'AssemblyInfo.cs') { continue }
        $name = if ($file.Directory.Name -eq 'terminal') { 'terminal/' + $file.Name } else { $file.Name }
        $flags += '/resource:' + $file.FullName + ',' + $name
    }
    $sources = @($sharedSource.FullName)
    if ($Terminal) {
        $flags += '/target:exe','/define:TERMINAL','/main:Technobabble.TerminalProgram'
        $sources += $terminalSource.FullName
    } else { $flags += '/target:winexe' }
    & $compiler @flags @sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }
} finally { Pop-Location }

