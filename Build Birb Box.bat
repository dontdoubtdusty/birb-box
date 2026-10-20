@echo off
rem Makes a fast (Release) copy of Birb Box in the "Birb Box" folder next to this project,
rem then puts "Birb Box" shortcuts on the Desktop and in the Start menu.
rem Run it again after changing the code. Close the game first, or the old copy can't be replaced.

cd /d "%~dp0"
set "GAME=%~dp0..\Birb Box"

dotnet publish -c Release -r win-x64 --self-contained false -o "%GAME%"
if errorlevel 1 (
    echo.
    echo Build failed. See the errors above.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$game = (Resolve-Path '%GAME%').Path;" ^
  "$exe = Join-Path $game 'BirbBox.exe';" ^
  "$shell = New-Object -ComObject WScript.Shell;" ^
  "foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {" ^
  "  $link = $shell.CreateShortcut((Join-Path $folder 'Birb Box.lnk'));" ^
  "  $link.TargetPath = $exe; $link.WorkingDirectory = $game; $link.IconLocation = $exe + ',0'; $link.Save() }"

echo.
echo Done. Start Birb Box from the Desktop or the Start menu.
pause
