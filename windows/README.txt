TECHNOBABBLE 1.13.1
=========================

Technobabble is an independent native Windows recreation of Dustin Kirkland's
Hollywood (https://github.com/dustinkirkland/hollywood). The desktop and terminal
editions include Classic Mode and Studio monitoring and layout controls.
Earlier releases of this port were called Hollywood for Windows.

Quick start
-----------
Extract this ZIP. Double-click Technobabble.exe for Classic Mode, or run from
PowerShell or CMD in the extracted folder:

  .\Technobabble.exe --layout "Process Watch"
  .\Technobabble-Terminal.exe --layout "Process Watch"
  .\Technobabble-Terminal.exe --layout "System Watch" --refresh 5

With the extracted folder on PATH, omit .\ and .exe. PATH must contain the
folder, not an executable filename. Both executables are standalone, including
fonts, images, animation frames and source excerpts. Windows .NET Framework 4.x
is required; validation used Windows 11 and .NET Framework 4.8. Neither native
edition needs Linux, WSL, Python, downloads, or an installer.

To upgrade, close both editions and replace the files in your existing folder.
Saved layouts are separate and are not overwritten. See LICENSE.txt and
NOTICE.txt for licensing, upstream references and asset attribution.

Relay Watch in 1.13
-------------------
From the extracted Windows folder in PowerShell or CMD, run
.\Technobabble-Terminal.exe --layout "Relay Watch". The preset pins all three panes.
You can also press E in Studio and choose radar, signal or engineering for any
individual pane. These are fictional instruments driven by the same incident.

Radar tracks a moving contact on a sweep. Signal tunes a carrier and decodes a
request. Engineering types a repair command, checks its telemetry, and sends an
acknowledgment. Each incident lasts 36 seconds of animation time. Opening a layout
starts a fresh shuffle. All twelve incidents play once per round (7 minutes,
12 seconds), then the order is shuffled again. Consecutive rounds always end
and start with different contacts.

  Relay 07       Route auxiliary power to the relay bus.
  Kestrel 12     Open a coolant bypass after a pressure drop.
  Beacon 31      Synchronize the beacon clock.
  Orbiter 04     Release a jammed solar array.
  Harbor 22      Realign a docking clamp.
  Mirage 18      Filter a delayed radar echo to resolve a split track.
  Rime 09        Warm an iced propellant valve.
  Atlas 55       Balance a vibrating flywheel.
  Lantern 03     Align a faded optical downlink.
  Kepler 46      Correct attitude drift with a thruster pulse.
  Vault 29       Rebuild an archive parity stripe.
  Sundial 63     Deploy a reserve radiator to cool a hot loop.

Adding a pane halfway through joins the active incident. Space pauses all three.
Visiting Classic suspends the incident. Saving stores the pane arrangement. Layouts using the three new widgets require
Technobabble 1.12 or later.

Code typing in 1.10
-------------------
In Studio, select the code pane with Tab or a desktop click. Press Y to cycle
through slow typing, normal typing, fast typing and continuous scrolling.
The footer shows the active setting. Space pauses the animation. Each character
has its own timing, with longer pauses at punctuation and line breaks.
W (terminal) or F5 (desktop or terminal) saves the setting with the layout.
Typing layouts use format 4 and require Technobabble or Hollywood for Windows 1.10. Files saved
with scrolling use the existing formats. Loading a layout starts a new playback.

Studio pane editor in 1.9
-------------------------
Press E in Studio to choose the selected pane's widget, add a pane by splitting
it, or remove it. All 26 widgets are available, including disk, GPU and Process
Watch. The desktop uses a native dialog with Pane, Action and Widget dropdowns.
Click Apply to commit, or use Remove pane, Undo last edit or Cancel.

Terminal editor controls (including Ubuntu):
  Tab / Shift+Tab     Choose the next / previous pane
  R                  Replace / swap the selected pane's widget
  H                  Add a pane below (split top / bottom)
  V                  Add a pane to the right (split left / right)
  Up / Down          Choose a widget
  Page Up / Down     Scroll the widget list
  Home / End         First / last widget
  Enter              Apply the choice
  X                  Remove the selected pane immediately
  U                  Undo the last pane edit
  Esc                Cancel without applying a choice

U also works outside the editor in either edition. Up to 20 pane edits can be
undone in this session. Undo restores the arrangement, pins and selection while
keeping current refresh, network and process preferences. Opening another
layout clears undo; switching through Classic keeps it. Undo restores fresh
widget instances, so animations and process histories may restart.

Successful edits pin every visible pane and set the layout's pane count to its
current count. This prevents automatic rebuilding from undoing your choices.
P can unpin individual panes afterward. Resize the dividers with B, or drag them
in the desktop edition. W/F5 saves your changes; E does not save automatically.

Technobabble keeps one instance of each widget. Choosing a widget already visible
elsewhere swaps it with the selected pane; the picker labels the other pane's
number. Splits offer unused widgets only. Removing a pane expands its sibling
pane or group to fill the space. Keep at least one pane; the maximum is 20.
If a pane is too small to split, enlarge it with B or enlarge the window.

To create a layout from one pane in PowerShell:
  .\Technobabble-Terminal.exe --layout "Blank Canvas"
Press E, then V or H, choose the new widget with the arrow keys and press Enter.
Repeat to build your board, and press W to save it under a name. To reopen it:
  .\Technobabble-Terminal.exe --layout "My board"

Classic's widgets, random layout engine and controls are unchanged. E and U are
inactive there. This release also preserves the direct-output Ubuntu launcher.

Mode switching correction in 1.8.1
----------------------------------
S (terminal) or F2 now remembers the current Studio layout before entering
Classic. Switching back restores that layout, including unsaved pane sizes,
pins, the selected pane, refresh setting, process sorting/filtering and network
choice. Classic can reshuffle and the terminal can resize while you are away;
Studio returns with the stored proportions fitted to the current window.

This is a temporary session memory, not an automatic file save. Use W/F5 to keep
a layout after quitting. Live readings and animations refresh when restored;
the selected process and its history restart. A new no-switch launch still
starts Classic, and the first entry into Studio keeps that current arrangement.
Use L/F6 to choose a dashboard if you have not opened one in the session yet.

Studio upgrades in 1.8
---------------------
1. Process Watch: an interactive Windows process list, CPU/memory/name sorting,
   case-insensitive name or PID filtering, stable selection, scrolling, and
   CPU/RAM history for the selected process. Larger or filtered panes devote
   extra space to the graphs. Saved layouts retain sorting and filtering.
2. Network selection: choose an adapter with N. All network panes follow that
   choice. Layouts retain its Windows identity even if its name changes. A
   disconnected or missing selection is labeled and never silently substituted.
3. Pane resizing: drag a divider in the desktop edition, or use B for keyboard
   resizing in either edition. Layouts save the divider proportions.

Classic keeps the original 20 widgets, split/rebuild behavior, automatic network
selection, and standard sampling delay. Studio-only controls are inactive in
Classic. Returning to Classic applies its original settings while keeping a temporary
Studio layout for your return. Saved layout files are never changed by toggling modes.

Process Watch
-------------
Open --layout "Process Watch", or press C in Studio to place/select its pane.
The built-in layout pins the process and network panes alongside animations.
Select the process pane with Tab or a desktop click, then use:

  O                  Cycle CPU usage, memory usage, and name sorting
  F                  Filter by process name or PID; empty text shows all
  Up / Down          Select a process
  Page Up / Down     Move by a visible page
  Home / End         Select first / last matching process
  Mouse wheel        Scroll selection in the desktop edition
  Click a row        Select that process in the desktop edition

The selection follows the same process when rankings change. CPU percentage is
a share of the whole machine's CPU capacity; RAM is the process working set.
History is collected only for the selected process, up to 120 samples. Selecting
another process or changing the sampling rate restarts the graphs. CPU uses a
fixed 0-100% graph; RAM scales to its displayed recent peak. An exited process is
marked until you select another. Process IDs and creation times prevent a reused
ID from inheriting another process's history. If Windows denies creation-time
access, current readings remain visible but history is unavailable. Some
protected processes cannot be read and may be omitted. No process-management or
termination commands are provided.

Network adapter selection
-------------------------
Press N, choose with Up/Down, then Enter. The desktop uses a native dropdown.
The choice applies on the next sample. Automatic selects an active, non-loopback
interface with the largest cumulative received-byte count, matching Classic's
selection policy. Explicit choices can include Ethernet, Wi-Fi and virtual
adapters exposed by Windows. Counters have independent baselines, so switching,
disconnecting or reconnecting does not produce an artificial traffic spike.
Graphs restart when the monitored adapter changes; unavailable data is labeled.
If a saved adapter no longer exists, N lets you choose a replacement or Automatic.

Pane sizes and saved layouts
---------------------------
In the desktop edition, drag the blue divider between panes. In either edition:

  B                  Enter / leave resize mode
  Tab / Shift+Tab     Choose the highlighted divider
  Left / Up          Move it toward the left / top
  Right / Down       Move it toward the right / bottom
  0                  Reset that divider to equal halves
  Enter / Esc        Finish resizing

Reshuffling is suspended while adjusting dividers; animation continues.
Proportions range from 10% to 90% and adapt to the current window size. Very small
panes clip their own labels and content. Pin panes with P to preserve their
boundaries through automatic reshuffles. Free regions continue to split and
reshuffle, so their temporary divider sizes can change during a rebuild.

F5 saves the current layout and F6 opens one. Terminal W and L are alternatives.
Layouts include panes, pins, proportions, shuffle timing, sampling delay, process
sorting/filtering and network choice. A running process selection and history
are not restored across sessions. Saved profiles are shared by both editions
under %LOCALAPPDATA%\Technobabble\Layouts. Layouts stored by earlier releases
under %LOCALAPPDATA%\Hollywood\Layouts are also listed and readable. New saves
go to the Technobabble folder. A saved copy there takes precedence.
TECHNOBABBLE_LAYOUT_DIR overrides both locations. HOLLYWOOD_LAYOUT_DIR remains
a supported fallback override. Layout file signatures stay compatible with 1.10.
Saving over a name asks for confirmation; loading never rewrites the file.

Built-in layouts: Network Watch, Disk Watch, GPU Watch, System Watch, Process Watch,
Blank Canvas (one pinned Matrix pane), and Relay Watch.
An existing user layout with a built-in name takes precedence. Save changes to a
built-in example under another name. Older version 1/2 layouts remain supported
and load with default new preferences. Continuous scrolling uses formats 1/2/3.
Layouts saved with a typing speed use format 4 and open in version 1.10 or later.
Invalid files leave the current scene and settings intact.

Monitoring and refresh
----------------------
System Watch pins CPU/RAM, GPU, disk and network panels, with a small animation
area below. D and G place/select disk and GPU panes in any Studio arrangement.
Disk reports logical-volume read/write throughput. GPU reports hardware engine
usage, busiest-engine utilization, and dedicated/shared memory. Shared memory is
system RAM used by the GPU, not a permanently reserved allocation. Memory limits
come from the matching DXGI adapter and are omitted when they cannot be mapped
safely. Missing counters are labeled and retried; multiple adapters/engine groups
page through compact panes. These are Windows readings even when viewed in WSL.

T cycles sampling delays: standard 2 seconds, lower-overhead 5 seconds, then
faster 1 second. --refresh 1|2|5 enables Studio and overrides a loaded layout at
startup. Windows counter collection takes additional time; the setting is the
wait between collections, not a guaranteed completed-update frequency. Slower
sampling does not reduce animation frame rate. Event logs refresh about every
30 seconds. Disk/GPU counters and process identity queries run only when needed.

General controls and options
----------------------------
  F1 / ? (terminal)   Controls
  F2 / S (terminal)   Classic / Studio
  Space              Pause / resume
  R                  Reshuffle free panes
  Tab / Shift+Tab     Select Studio pane
  P                  Pin / unpin selected pane
  E                  Edit Studio panes
  U                  Undo last pane edit
  Ctrl+C             Quit
  Q (terminal)       Quit
  Ctrl+L (terminal)  Repair/redraw the terminal screen
  F11 (desktop)      Full screen
  + / - (desktop)    Font size
  Double-click       Focus a desktop pane; repeat or Escape to return
  Right-click        Desktop command menu

Escape closes an editor/help/resize mode before leaving the application. In the
desktop edition it also returns from pane focus or full screen. The terminal
host controls font size and full screen. PowerShell ISE supports the desktop
edition; use a normal console or Windows Terminal tab for in-terminal rendering.

  -s, --splits 1..20  Maximum pane count; default twice CPU count, capped at 20
  -d, --delay seconds Completed-arrangement hold time; default 10 seconds
  --classic          Default original experience
  --studio           Start Studio without loading a layout
  --layout NAME      Open a named or built-in layout in Studio
  --refresh 1|2|5     Studio sampling delay
  --fullscreen       Desktop full screen
  --help             Command-line reference

Optional Windows bridge for WSL
-------------------------------
For the native Linux application in Ubuntu, see ../linux/README.md.
To display Windows data in a WSL terminal, launch this edition's Python
bridge explicitly. Substitute the folder where you extracted the Windows ZIP:

  python3 /mnt/c/Tools/Technobabble/technobabble-terminal-wsl.py --layout "Process Watch"

The bridge uses Python 3's standard library, forwards keys and terminal size,
and starts the adjacent Windows executable. Reserve the Ubuntu commands
technobabble and technobabble-terminal for the native Linux package.

Classic recreation
------------------
One pane appears first. After half a second the display splits randomly, adding
widgets about every 0.2 seconds and alternating cut directions. After the hold
time, the bottom-right widget survives and the other panes rebuild. Choices are
unique within an arrangement and adapt to available character cells. Classic
retains its tmux-style status line. The Windows widgets recreate the command
displays; code, process data, paths and logs reflect Windows. The original
soundwave animation is sampled and silent. The htop function-key legend is part
of Classic's appearance; interactive process controls belong to Process Watch.

Original widgets
----------------
  apg           Pronounceable, multicolored display passwords.
  atop / htop   Live Windows processes, CPU and physical memory. Richer layouts
                show individual processor bars. Protected processes can be absent
                from the table. The function-key legend is part of the display.
  bat           Syntax-colored source excerpts with line numbers.
  code          Studio scrolls through complete bundled source files, one screen
                row at a time. Long lines wrap to the pane width. Classic shows
                its original rotating excerpts.
  bmon          Live interface rates and history, with the original 48-column,
                11-row threshold for switching to the compact speedometer.
  cmatrix       Green digital rain with independent trails and bright heads.
  errno         Shuffled POSIX error names, numbers and descriptions.
  figlet        The original banner word list in FIGlet's Standard typeface.
  hexdump       Offset, hexadecimal and ASCII views of real Windows system
                binaries, with consecutive identical rows collapsed.
  jp2a          Color ASCII views of installed Windows wallpapers. Falls back
                to the included map if the wallpapers are unavailable.
  logs          Recent readable Windows System event metadata. When access is
                unavailable the pane displays its waiting status.
  man           A rotating set of short, locally written manual pages.
  map           The original Hollywood world map converted to colored text.
  mplayer       The original soundwave clip, sampled into an embedded frame
                atlas and replayed as fast, silent colored ASCII animation.
  pv            Classic repeats a 10 MiB data stream at 1 MiB/s. In Studio, pv
                simulates transfers of 12 named files with different sizes and
                shows progress and completed files. All transfers are simulated
                in memory. Select pv in the Studio pane editor with E.
  speedometer   Receive and transmit rate histories for an active interface.
  sshart        OpenSSH-style randomart from ephemeral Windows ECDSA P-256 keys.
  stat / tree   Metadata and bounded directory listings from Windows system paths.


Source and verification
-----------------------
The complete C# source, embedded assets and tests are in source. From PowerShell:

  .\source\build.ps1
  .\source\tests\check.ps1
  .\source\build.ps1 -Terminal
  .\source\tests\check.ps1 -Terminal
  .\source\tests\refresh-live.ps1

Builds use the .NET Framework compiler included with Windows, without package
restore. The optional refresh-live test needs working disk/GPU counters and can
take a minute. Launcher key tests: python3 source/tests/wsl-keys.py in Ubuntu.

Shared scene/layout code serves both frontends. ProcessWatch.cs owns process
selection, ordering and bounded histories. NetworkSampler.cs owns independent
adapter baselines. PaneSizing.cs owns divider geometry. PaneEditing.cs owns
validated tree edits; PaneEditor.cs shares the picker and bounded undo state
between frontends. StudioControls.cs owns common commands. Desktop dialogs and
rendering stay in their frontend;
the terminal adapter is unchanged. PaneEditorChecks.cs covers editor behavior
alongside StudioChecks.cs and the existing Classic/layout/monitor regression
suite. See VALIDATION.txt for tested paths and the limits of live hardware coverage.
