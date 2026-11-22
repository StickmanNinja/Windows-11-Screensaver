# Builds Superhero.scr next to this script, with the C# compiler that comes with Windows.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

& $csc /nologo /target:winexe /optimize+ `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/out:$here\Superhero.scr" "$here\Superhero.cs"

if ($LASTEXITCODE -eq 0) { Write-Host "Built $here\Superhero.scr" }
