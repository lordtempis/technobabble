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

Install the Linux .NET 10 SDK and Python 3. The terminal integration test uses Python's `pyte` library. The Debian packages also require `dpkg-deb`.

```sh
cd linux
dotnet build -c Release
dotnet run -c Release --project tests/Checks.csproj
/usr/bin/python3 tests/terminal.py ./bin/Release/net10.0/technobabble
python3 package.py ./dist
python3 package-portable.py ./dist
python3 package.py ./dist --portable ./dist/technobabble-1.13.0-linux-x64.tar.gz
```

The PTY test starts real Linux terminal sessions, saves layouts in a temporary data directory, and checks that the shell settings are restored. Both packaging commands write to `linux/dist/`. The portable script restores Microsoft's generic `linux-x64` packs from NuGet, bundles the runtime and its notices, and writes a SHA-256 file beside the archive. CI extracts this archive and runs the terminal suite in a Debian container with no system .NET installation.

## Publish an APT update

Build the bundled `.deb` with a higher Debian version than the last published package. Test it with APT on Debian and Ubuntu before distributing it. The repository keeps each version's bytes under an immutable filename.

Clone the `apt` branch in a separate directory. On Linux, install `apt-utils` and GnuPG and unlock the archive signing key in your local keyring. Verify its full fingerprint against `linux/apt/technobabble.asc`. From the source checkout, run:

```sh
bash linux/apt/publish.sh /path/to/apt-checkout /path/to/package.deb A533F774E961CCB6FDA00A90B588F42F73E51C2D
git -C /path/to/apt-checkout add -- dists pool
git -C /path/to/apt-checkout commit -m "Publish Technobabble APT update"
git -C /path/to/apt-checkout push origin apt
```

The script signs both repository manifests, verifies their signatures, and retains older package files and index hashes. Push the package and index together. Run **Build and test** manually in GitHub Actions to exercise the public APT install on Debian and Ubuntu. The signing key and its recovery material belong in the maintainer's private storage.

## Changes and release boundaries

- Classic has 20 widgets and randomized pane rebuilding. Studio additions belong in the Studio selector and layouts.
- For saved-layout changes, check reading older files and round-tripping current files. For terminal rendering changes, test a narrow pane and a paused resize.
- Keep upstream asset notices with redistributed files. Commit source, tests, and input assets. Attach built packages to a tagged release.
