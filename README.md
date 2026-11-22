# 🌙 Windows 11 Screensaver

Custom screensavers for Windows 11, written in C# with nothing to install.

## Superhero

A hero in a blue suit and red cape flies over a big city at night, under the stars and a full Moon. Searchlights sweep
the sky, windows switch on and off, a plane blinks by, cars drive along the street, there's the odd shooting star,
and now and then he does a loop-the-loop. It runs on every monitor and in the little preview box.

## Install

1. It's already built: [`Superhero.scr`](Superhero.scr). **Right-click it → Install.**
2. Windows opens Screen Saver Settings with it picked: choose how many minutes to wait and click **OK**.
3. Double-click it (or right-click → Test) to watch it right away. Move the mouse or press a key to stop.

> [!NOTE]
> Windows remembers where the file is, so if you move or delete this folder, install it again.

## Build

Run `build.ps1` in PowerShell. It uses the C# compiler that comes with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) and writes `Superhero.scr` next to it.

For testing without covering the screen:

| Command | What it does |
|---|---|
| `Superhero.scr /w` | Runs in a normal window |
| `Superhero.scr /x shot.png 1920 1080 9` | Saves a picture of the scene after 9 seconds, plus timings in `shot.png.txt` |

Double-clicking a `.scr` always starts it full screen, so for `/x` copy it to a `.exe` first.
