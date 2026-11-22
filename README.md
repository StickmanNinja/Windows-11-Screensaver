# 🌙 Windows 11 Screensaver

Custom screensavers for Windows 11 in one file, with a picker to choose between them.

## ⬇️ Download

1. **Download [Windows11ScreensaverSetup.exe](https://github.com/StickmanNinja/Windows-11-Screensaver/releases/latest/download/Windows11ScreensaverSetup.exe)**
   (or grab it from the [Releases](https://github.com/StickmanNinja/Windows-11-Screensaver/releases) page).
2. **Run it.** Windows may say *"Windows protected your PC"* because the installer isn't signed: click
   **More info → Run anyway**.
3. Click **Install**, then **Set as my screensaver**, choose how many minutes to wait and click **OK**.

It installs just for you, in your user folder, with no admin rights needed. You'll find **Screensavers** in the Start
menu to change which one plays. To remove it: **Settings › Apps › Installed apps › Windows 11 Screensaver › Uninstall**.

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

## Without the installer

[`Screensavers.scr`](Screensavers.scr) works on its own too: **right-click it → Install**, choose the wait time and
click **OK**. Windows remembers where the file is, so if you move or delete it, install it again.

## Build

Run `build.ps1` in PowerShell. It uses the C# compiler that comes with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, nothing to install) and writes two files:

- `Screensavers.scr`, compiled from `src\`
- `Windows11ScreensaverSetup.exe`, the installer from `installer\Setup.cs`, with `Screensavers.scr` packed inside it

The installer copies the screensaver to `%LOCALAPPDATA%\Programs\Windows 11 Screensaver`, adds a Start menu shortcut
and an Installed apps entry, and can run without a window: `Windows11ScreensaverSetup.exe /silent` to install,
`Uninstall.exe /uninstall /silent` (in the install folder) to remove it. The icon comes from `tools\make-icon.ps1`.

| File | What's in it |
|---|---|
| `src\Program.cs` | Starts the right thing for how Windows runs it (`/s`, `/p`, `/c`) |
| `src\PickerForm.cs` | The picker window |
| `src\SaverForm.cs` | The full-screen and preview windows |
| `src\Core.cs` | The `Scene` base class, the list of screensavers, settings, shuffle and speed |
| `src\CityScene.cs`, `CodeScene.cs`, `SpaceScene.cs` | The three screensavers |
| `installer\Setup.cs` | The installer and uninstaller |
| `assets\` | The icon, and the manifest (runs without admin rights, sharp on high-DPI screens) |

To add a screensaver, write a class that extends `Scene` (an `Update` and a `Draw`) and add it to `Scenes.All` in
`Core.cs`. It shows up in the picker and in Shuffle automatically.

For testing without covering the screen:

| Command | What it does |
|---|---|
| `Screensavers.scr /w space` | Runs one screensaver in a normal window |
| `ss.exe /x shot.png 1920 1080 9 hacker` | Saves a picture of a screensaver after 9 seconds, plus timings in `shot.png.txt` |
| `ss.exe /xp picker.png` | Saves a picture of the picker |
| `Windows11ScreensaverSetup.exe /shot a.png b.png` | Saves pictures of the installer's two screens, without installing |

Double-clicking a `.scr` always starts it full screen, so for `/x` and `/xp` copy it to a `.exe` first.
