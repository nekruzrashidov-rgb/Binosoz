@echo off
rem Двойной щелчок по этому файлу запускает API и фронтенд (см. run.ps1).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1"
pause
