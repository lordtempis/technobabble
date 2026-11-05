# Building and testing Technobabble

Run these commands from the repository root. Each edition is self-contained. For a change to a feature present in both editions, check both implementations and run both suites. For a platform-specific change, test the platform it affects.

## Windows

The build scripts use the C# compiler included with Windows .NET Framework 4.x. The desktop and terminal targets were tested on Windows 11 with .NET Framework 4.8.

```powershell
.\windows\source\build.ps1
.\windows\source\tests\check.ps1
.\windows\source\build.ps1 -Terminal
.\windows\source\tests\check.ps1 -Terminal
```

The executables appear in `windows/`. To exercise the terminal UI, run `.\windows\Technobabble-Terminal.exe --layout "Relay Watch"` in an interactive console. The optional `windows/source/tests/refresh-live.ps1` test needs working local disk and GPU counters.

```powershell
.\windows\package.ps1
```

The packaging script builds both targets and writes the Windows release ZIP to `windows/dist/`.

## Linux

Install the Linux .NET 10 SDK and Python 3. The terminal integration test uses Python's `pyte` library. The optional Ubuntu package also requires `dpkg-deb`.

```sh
cd linux
dotnet build -c Release
dotnet run -c Release --project tests/Checks.csproj
/usr/bin/python3 tests/terminal.py ./bin/Release/net10.0/technobabble
python3 package.py ./dist
python3 package-portable.py ./dist
```

The PTY test starts real Linux terminal sessions, saves layouts in a temporary data directory, and checks that the shell settings are restored. Both packaging commands write to `linux/dist/`. The portable script restores Microsoft's generic `linux-x64` packs from NuGet, bundles the runtime and its notices, and writes a SHA-256 file beside the archive. CI extracts this archive and runs the terminal suite in a Debian container with no system .NET installation.

## Changes and release boundaries

- Classic has 20 widgets and randomized pane rebuilding. Studio additions belong in the Studio selector and layouts.
- For saved-layout changes, check reading older files and round-tripping current files. For terminal rendering changes, test a narrow pane and a paused resize.
- Keep upstream asset notices with redistributed files. Commit source, tests, and input assets. Attach built packages to a tagged release.
