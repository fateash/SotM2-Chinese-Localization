@echo off
chcp 65001 >nul
cd /d "%~dp0"
py -3 tools\inventory.py --input inventory-translations --out build --mode reviewed
if errorlevel 1 echo Operation failed. Do not use stale output.
pause
