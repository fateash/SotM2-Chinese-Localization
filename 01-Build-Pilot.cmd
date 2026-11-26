@echo off
chcp 65001 >nul
cd /d "%~dp0"
py -3 tools\catalog.py build --input translations --out build --mode pilot
if errorlevel 1 echo Build failed. Do not copy any old build output into the game.
pause
