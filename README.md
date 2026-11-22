# 🌙 Windows 11 Screensaver

Custom screensavers for Windows 11 in one file, with a picker to choose between them.
Written in C#, nothing to install.

## The screensavers

| | |
|---|---|
| 🦸 **Superhero** | A caped hero flies over the city at night: stars, the Moon, searchlights, windows lighting up, a plane blinking by, cars on the street, shooting stars and loop-the-loops. |
| 💻 **Hacker** | Green code pours down the screen. Terminal windows pop up and type out a break-in, and now and then a progress bar decrypts the mainframe and **ACCESS GRANTED** flashes up. |
| 🌍 **Space** | The Earth turns slowly in front of the Milky Way, with drifting clouds, city lights on the night side and a sun glint on the oceans. The Moon and satellites orbit it, a space station drifts past, comets streak by, rockets launch from the surface, and an astronaut floats across. |

## The picker

Open `Screensavers.scr` (or click **Settings…** in Windows' Screen Saver Settings) to get the picker:

- A big **live preview** of the one you've picked, and a card with its own little live preview for each screensaver.
- **Shuffle**: they take turns, fading from one to the next every 3 minutes.
- **Speed**: Slow, Normal or Fast.
- **Preview full screen** to watch it right away (move the mouse or press a key to come back).
- **Set as Windows screensaver** opens Windows' Screen Saver Settings with it picked: choose the wait time and click **OK**.
- **Save** keeps your choice. Keys work too: arrows to pick, Space for Shuffle, Enter to save, Esc to cancel.

Your choice is saved in the registry under `HKEY_CURRENT_USER\Software\Windows 11 Screensaver`.

## Install

1. It's already built: [`Screensavers.scr`](Screensavers.scr). **Right-click it → Install** (or open it and click
   **Set as Windows screensaver**).
2. In Screen Saver Settings, choose how many minutes to wait and click **OK**. The **Settings…** button there opens the
   picker.

> [!NOTE]
> Windows remembers where the file is, so if you move or delete this folder, install it again.

## Build

Run `build.ps1` in PowerShell. It compiles everything in `src\` with the C# compiler that comes with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) and writes `Screensavers.scr`.

| File | What's in it |
|---|---|
| `src\Program.cs` | Starts the right thing for how Windows runs it (`/s`, `/p`, `/c`) |
| `src\PickerForm.cs` | The picker window |
| `src\SaverForm.cs` | The full-screen and preview windows |
| `src\Core.cs` | The `Scene` base class, the list of screensavers, settings, shuffle and speed |
| `src\CityScene.cs`, `CodeScene.cs`, `SpaceScene.cs` | The three screensavers |

To add a screensaver, write a class that extends `Scene` (an `Update` and a `Draw`) and add it to `Scenes.All` in
`Core.cs`. It shows up in the picker and in Shuffle automatically.

For testing without covering the screen:

| Command | What it does |
|---|---|
| `Screensavers.scr /w space` | Runs one screensaver in a normal window |
| `ss.exe /x shot.png 1920 1080 9 hacker` | Saves a picture of a screensaver after 9 seconds, plus timings in `shot.png.txt` |
| `ss.exe /xp picker.png` | Saves a picture of the picker |

Double-clicking a `.scr` always starts it full screen, so for `/x` and `/xp` copy it to a `.exe` first.
