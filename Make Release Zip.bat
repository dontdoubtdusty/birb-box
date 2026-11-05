@echo off
rem Makes the zip you put on GitHub Releases: a self-contained Windows build (no .NET needed to play),
rem with the default radio stations and a Read Me First.txt. Needs the .NET 10 SDK and internet (first run).
rem The result is "Birb Cube Release\BirbCube-<version>-win-x64.zip" next to this project.
rem It never touches your own "Birb Cube" play folder, your save, or your music.

cd /d "%~dp0"
set "OUT=%~dp0..\Birb Cube Release"

rem The version comes from GameEngine.cs (the "Birb Cube 5.17 Alpha" text), so the zip name can't drift.
set "VER="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Select-String -Path 'GameEngine.cs' -Pattern 'Version = .Birb Cube ([0-9.]+)').Matches[0].Groups[1].Value"`) do set "VER=%%V"
if "%VER%"=="" (
    echo Could not read the version from GameEngine.cs.
    pause
    exit /b 1
)

set "NAME=BirbCube-%VER%-win-x64"
set "STAGE=%OUT%\%NAME%"
set "ZIP=%OUT%\%NAME%.zip"

echo Making %NAME% ...
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del "%ZIP%"

dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=none -p:SatelliteResourceLanguages=en -o "%STAGE%"
if errorlevel 1 (
    echo.
    echo Build failed. See the errors above.
    pause
    exit /b 1
)

if not exist "%STAGE%\BirbCube.exe" (
    echo BirbCube.exe is missing from the build.
    pause
    exit /b 1
)
if not exist "%STAGE%\resources\shaders" (
    echo The resources folder did not come along.
    pause
    exit /b 1
)

rem The default radio stations (no music: players add their own) and the instructions.
xcopy "release\RadioDefaults" "%STAGE%\Radio\" /E /I /Q /Y >nul
copy /y "release\Read Me First.txt" "%STAGE%\Read Me First.txt" >nul

rem tar ships with Windows 10 and later and writes a normal zip.
tar -a -c -f "%ZIP%" -C "%OUT%" "%NAME%"
if errorlevel 1 (
    echo.
    echo Making the zip failed.
    pause
    exit /b 1
)

echo.
echo Done: %ZIP%
for %%F in ("%ZIP%") do echo Size: %%~zF bytes
echo.
echo Next: unzip it somewhere else and double-click BirbCube.exe to check it, then
echo upload the zip on GitHub (Releases, Draft a new release, tag v%VER%).
pause
