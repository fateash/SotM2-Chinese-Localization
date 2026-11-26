@echo off
chcp 65001 >nul
cd /d "%~dp0"
py -3 tools\catalog.py export --input translations --out paratranz-import
if errorlevel 1 echo Export failed. Read the error above.
pause
