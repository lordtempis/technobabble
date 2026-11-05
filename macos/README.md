# Technobabble for macOS (preview)

This terminal edition shares Classic animations, the Studio pane editor, saved layouts, and Relay Watch with the Linux edition. It samples macOS CPU, memory, processes and network interfaces. Disk Watch reads IOKit storage counters where the hardware exposes them. GPU activity is labeled unavailable. The log pane reads accessible lines from `/var/log/system.log`. Some Macs expose little or no data there.

## Install and launch

Use macOS 15 or later. Choose the `osx-arm64` installer for an Apple Silicon Mac, or `osx-x64` for an Intel Mac. Both packages include .NET. The preview packages are unsigned and have not been notarized by Apple.

1. Place the `.pkg` and its matching `.sha256` file in one directory. Run `shasum -a 256 -c <filename>.pkg.sha256`.
2. Open the installer to install the command-line program system-wide. Installation requires administrator authorization.
3. In Terminal, run `technobabble` for Classic or `technobabble --layout "Relay Watch"` for Studio. If your shell cannot find the command, use `/usr/local/bin/technobabble`.

Press **S** to switch modes, **E** to edit a Studio pane, **W** to save, **?** for controls, and **Q** to quit. Layouts are stored in `~/Library/Application Support/Technobabble/layouts`. The `TECHNOBABBLE_LAYOUT_DIR` override also works.

## Build on a Mac

Install the .NET 10 SDK, Python 3, and Apple's Command Line Tools. From the full source checkout, run `python3 macos/package.py`. The installer and checksum appear under `macos/dist/`. The project uses the shared sources and notices in `linux/`; the macOS sensors use Mach and IOKit. The installer receipt is `io.github.lordtempis.technobabble.cli`.

The installer includes the application and asset notices, the FIGlet font and distribution terms, and the bundled runtime's original licenses under `/usr/local/share/doc/technobabble/`.
