@echo off
rem Remove o plugin PASB do Advance Steel 2027 (feche o Advance Steel antes).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bundle\Uninstall-Bundle.ps1"
pause
