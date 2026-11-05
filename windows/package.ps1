[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'source/build.ps1')
& (Join-Path $PSScriptRoot 'source/build.ps1') -Terminal

$desktop = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Technobabble.exe')
$terminal = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Technobabble-Terminal.exe')
if ($desktop.VersionInfo.FileVersion -ne $terminal.VersionInfo.FileVersion) {
    throw 'Windows executables have different versions.'
}
$version = [version]$desktop.VersionInfo.FileVersion
$parts = if ($version.Revision -gt 0) { 4 } elseif ($version.Build -gt 0) { 3 } else { 2 }
$directory = [IO.Directory]::CreateDirectory($OutputDirectory).FullName
$target = Join-Path $directory ('Technobabble-' + $version.ToString($parts) + '.zip')
if ([IO.File]::Exists($target)) { throw "Release already exists: $target" }

$files = @($desktop, $terminal)
foreach ($name in 'LICENSE.txt','NOTICE.txt','README.txt','VALIDATION.txt','technobabble-terminal-wsl.py','package.ps1') {
    $files += Get-Item -LiteralPath (Join-Path $PSScriptRoot $name)
}
$files += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Recurse -File |
    Where-Object { $_.Extension -in '.cs','.ps1','.py','.jpg','.flf','.manifest' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$stage = Join-Path $directory ([guid]::NewGuid().ToString('N') + '.tmp')
try {
    $archive = [IO.Compression.ZipFile]::Open($stage, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in ($files | Sort-Object FullName)) {
            $relative = $file.FullName.Substring($PSScriptRoot.Length + 1).Replace('\','/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative)
        }
    } finally { $archive.Dispose() }
    [IO.File]::Move($stage, $target)
} finally {
    if ([IO.File]::Exists($stage)) { [IO.File]::Delete($stage) }
}
Get-FileHash -Algorithm SHA256 -LiteralPath $target
