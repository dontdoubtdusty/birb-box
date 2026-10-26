@echo off
rem Makes a fast (Release) copy of Birb Cube in the "Birb Cube" folder next to this project,
rem then puts "Birb Cube" shortcuts on the Desktop and in the Start menu.
rem Run it again after changing the code. Close the game first, or the old copy can't be replaced.

cd /d "%~dp0"
set "GAME=%~dp0..\Birb Cube"
set "OLDGAME=%~dp0..\Birb Box"

dotnet publish -c Release -r win-x64 --self-contained false -o "%GAME%"
if errorlevel 1 (
    echo.
    echo Build failed. See the errors above.
    pause
    exit /b 1
)

rem From when the game was called Birb Box: bring the radio's music over once (saves start fresh).
if exist "%OLDGAME%\Radio" if not exist "%GAME%\Radio" (
    echo Copying the radio's music from the old "Birb Box" folder...
    xcopy "%OLDGAME%\Radio" "%GAME%\Radio\" /E /I /Q /Y >nul
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$game = (Resolve-Path '%GAME%').Path;" ^
  "$exe = Join-Path $game 'BirbCube.exe';" ^
  "$shell = New-Object -ComObject WScript.Shell;" ^
  "foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {" ^
  "  Remove-Item -LiteralPath (Join-Path $folder 'Birb Box.lnk') -ErrorAction SilentlyContinue;" ^
  "  $link = $shell.CreateShortcut((Join-Path $folder 'Birb Cube.lnk'));" ^
  "  $link.TargetPath = $exe; $link.WorkingDirectory = $game; $link.IconLocation = $exe + ',0'; $link.Save() }"

echo.
echo Done. Start Birb Cube from the Desktop or the Start menu.
pause
