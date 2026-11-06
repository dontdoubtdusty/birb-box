@echo off
rem Starts the Jello task board and opens it in your browser (http://localhost:5178/).
rem The board lives in claude\board\ as one JSON file per card; Claude reads and edits the same files.
rem Close this window to stop the board. Every change is saved the moment you make it.

cd /d "%~dp0"
title Jello board
dotnet run --project "tools\JelloBoard" -c Release -- --board "%~dp0claude\board"
if errorlevel 1 pause
