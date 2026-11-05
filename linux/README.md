# Technobabble for Linux

Technobabble is a Linux terminal application with Classic and Studio modes. It has 26 widgets, the Studio pane editor, typing effects, and Relay Watch's twelve shuffled incidents. This edition runs as a Linux process using the Linux .NET 10 runtime. Its executable, data collection, and saved layouts are independent of the Windows edition.

## Install and run

### Debian and Ubuntu with APT (amd64)

The project APT repository provides Technobabble with .NET bundled. On 64-bit Intel/AMD Debian or Ubuntu, including Ubuntu under WSL, follow the [one-time repository setup](https://github.com/lordtempis/technobabble#debian-and-ubuntu-apt). Then install and launch:

    sudo apt update
    sudo apt install technobabble
    technobabble --layout "Relay Watch"

Future published versions are installed by `sudo apt upgrade`. Remove the app with `sudo apt remove technobabble`. For a one-time install, download `technobabble_1.13.0-2_amd64.deb` from the [release](https://github.com/lordtempis/technobabble/releases/tag/v1.13) and run `sudo apt install ./technobabble_1.13.0-2_amd64.deb` in the download directory.

### Portable Linux archive (x86-64)

Use the binary archive on a glibc-based Linux distribution with its standard system libraries, including ICU and OpenSSL. It includes .NET 10 and runs from your own directory without an installer or administrator access. The target is Intel/AMD 64-bit Linux with glibc 2.27 or later. Alpine/musl and ARM use different runtime builds.

Download the archive and its matching `.sha256` file from the [v1.13 release](https://github.com/lordtempis/technobabble/releases/tag/v1.13). In the download directory, run:

    sha256sum -c technobabble-1.13.0-linux-x64.tar.gz.sha256
    tar -xzf technobabble-1.13.0-linux-x64.tar.gz
    cd technobabble-1.13.0-linux-x64
    ./technobabble --layout "Relay Watch"

Launch `./technobabble` for Classic Mode. Keep the extracted directory together. The executable loads its runtime from `lib/` and the runtime's licenses are in `dotnet-notices/`.

### Original Ubuntu package (revision 1)

The `.deb` was built and tested on x86-64 Ubuntu 26.04 under WSL 2. From Ubuntu, in the directory containing the package, run:

    sudo apt install ./technobabble_1.13.0-1_amd64.deb
    technobabble --version
    technobabble --layout "Relay Watch"

The package installs the Linux runtime dependency through Ubuntu's package manager. The two commands `technobabble` and `technobabble-terminal` launch the same Linux app. If you set a Bash alias for the old Windows bridge, update it to point to `/usr/bin/technobabble`. You can always use that full path directly.

Launch `technobabble` with no options for Classic. Use `--studio` for Studio or `--layout "System Watch"` for a dashboard. **S** switches modes, **Space** pauses, **?** shows controls, and **Q** exits and restores the terminal. In Studio, **E** edits panes, **Tab** selects, **P** pins, **B** sizes dividers, **Y** cycles code typing speed, **W** saves, and **L** opens a layout.

## Linux data and layouts

CPU and process activity come from Linux kernel counters. Process identity includes PID and kernel start time. Memory uses `MemTotal` and `MemAvailable`. Network panes show Linux interfaces. Disk panes show Linux block devices and derive transfer rates from sector-counter differences. Under WSL these readings describe the Linux VM and its virtual disks.

GPU Watch uses readable Linux DRM metrics or the native Linux NVIDIA utility when available. Under WSL, NVIDIA utilization and memory can describe the physical GPU shared with Windows. Missing readings are labeled. The log widget reads accessible Linux syslog files or the system journal. The tree, stat and hexdump panes use Linux paths. SSH randomart uses Linux-supported P-256 cryptography. The image panes use the bundled map and soundwave assets, including a map fallback for the wallpaper display.

Saved layouts go in `$XDG_DATA_HOME/technobabble/layouts`, normally `~/.local/share/technobabble/layouts`. `TECHNOBABBLE_LAYOUT_DIR` overrides that location. To import a Windows layout, copy its `.layout` file here. Both editions support formats 1–4. Network choices identify interfaces on the machine where the layout was saved; press **N** to select a Linux interface if needed.

## Build and test

Install the .NET 10 SDK in Ubuntu and run the following in the extracted source directory:

    dotnet build -c Release
    dotnet run -c Release --project tests/Checks.csproj
    python3 tests/terminal.py ./bin/Release/net10.0/technobabble
    python3 package.py ./dist
    python3 package-portable.py ./dist
    python3 package.py ./dist --portable ./dist/technobabble-1.13.0-linux-x64.tar.gz

The terminal tests use Python's `pyte` library. Building the Ubuntu package uses `dpkg-deb`. The portable packager downloads the generic Linux runtime packs from NuGet when needed. Both packages include preconverted image resources.

Technobabble is an independent recreation of Dustin Kirkland's Hollywood. Licensing and asset credits are in `LICENSE.txt` and `NOTICE.txt`.
