# Builds Screensavers.scr and the installer, Windows11ScreensaverSetup.exe, with the C# compiler
# that comes with Windows. Nothing to install.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

$common = @('/nologo', '/target:winexe', '/optimize+',
            '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
            "/win32icon:$here\assets\icon.ico", "/win32manifest:$here\assets\app.manifest")

# 1. The screensaver itself
& $csc @common "/out:$here\Screensavers.scr" "/recurse:$here\src\*.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built $here\Screensavers.scr"

# 2. The installer, with the screensaver packed inside it
& $csc @common "/resource:$here\Screensavers.scr,Screensavers.scr" "/out:$here\Windows11ScreensaverSetup.exe" "$here\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built $here\Windows11ScreensaverSetup.exe"
