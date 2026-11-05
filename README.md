# Technobabble

Making your terminal look like it's doing something important.

Technobabble turns a terminal into a wall of animated diagnostics. Classic Mode builds a random arrangement of panes and periodically reshuffles it. Studio Mode lets you choose, resize, pin, and save your own panes.

Some panes show real system activity. Others tell a fictional story. In **Relay Watch**, radar detects a contact, a signal decoder receives its request, and engineering repairs the fault. All three follow the same incident even if you add a pane halfway through.

![Relay Watch showing the radar contact Relay 07, its decoded auxiliary power request, and the engineering bus voltage recovering](docs/images/relay-watch.png)

## Choose an edition

| Edition | What runs | Quick start |
| --- | --- | --- |
| Windows | A desktop app and an in-terminal app. Both use Windows counters and the Windows .NET Framework. | Extract `Technobabble-1.13.zip`. Double-click `Technobabble.exe` or run the terminal executable below. |
| Linux | A Linux terminal app that reads Linux processes, memory, interfaces, and block devices. Tested on x86-64 Ubuntu 26.04 under WSL 2, including with Windows interop disabled. | Install the Ubuntu package below. It uses the Linux .NET 10 runtime. |

**Windows**, from the extracted folder in PowerShell:

```powershell
.\Technobabble-Terminal.exe --layout "Relay Watch"
```

**Ubuntu**, from the folder containing the downloaded package:

```sh
sudo apt install ./technobabble_1.13.0-1_amd64.deb
technobabble --layout "Relay Watch"
```

Start either app without switches for Classic Mode. Try `--layout "System Watch"` for live monitoring or `--layout "Blank Canvas"` to build a dashboard from one pane.

## Terminal controls

| Key | Action |
| --- | --- |
| **S** | Switch between Classic and Studio. The desktop edition uses **F2**. |
| **Space** | Pause or resume. |
| **Tab**, **E** | Select a Studio pane, then open its editor to replace, split, or remove it. |
| **P**, **B** | Pin a pane or resize its dividers. |
| **W**, **L** | Save or open a Studio layout. The desktop uses **F5** and **F6**. |
| **Y** | On the Studio code pane, cycle through scrolling and three typing speeds. |
| **?**, **Q** | Show terminal controls or quit. |

## Source and releases

The [Windows guide](windows/README.txt) and [Linux guide](linux/README.md) cover platform-specific commands, data sources, and layout locations. Both editions read the same layout file formats. The [contributor guide](CONTRIBUTING.md) shows how to build, test, and package each from a checkout. The [v1.13 release notes](docs/releases/v1.13.md) list the downloadable artifacts and their checksums.

Technobabble is an independent recreation of [Dustin Kirkland’s Hollywood](https://github.com/dustinkirkland/hollywood). The application source is licensed under [Apache 2.0](LICENSE). The [notices](NOTICE) credit the upstream map, soundwave animation, and FIGlet font.
