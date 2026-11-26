@echo off
chcp 65001 >nul
cd /d "%~dp0"
py -3 tools\catalog.py build --input translations --out build --mode reviewed
if errorlevel 1 echo No reviewed build produced. Do not use old build output.
pause
