# Запуск для разработки: API в отдельном окне, фронтенд в этом окне, браузер открывается сам.
# Секреты API берутся из user secrets (см. README, раздел «Переменные окружения»).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$api = 'http://localhost:5194'

function Test-Api {
    try { (Invoke-WebRequest "$api/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Не найден .NET SDK (dotnet). Установите .NET 10 SDK.' }
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'Не найден Node.js (npm). Установите Node.js.' }

if (Test-Api) {
    Write-Host "API уже запущен: $api" -ForegroundColor Green
} else {
    Write-Host 'Запускаю API в отдельном окне…' -ForegroundColor Cyan
    Start-Process powershell -WorkingDirectory "$root\src\Sitora.WebApi" -ArgumentList '-NoExit', '-Command', '$Host.UI.RawUI.WindowTitle = ''Sitora API''; dotnet run --no-launch-profile --urls http://localhost:5194 --environment Development'

    # Первый запуск собирает проект и применяет миграции — это может занять пару минут.
    $deadline = (Get-Date).AddMinutes(4)
    while (-not (Test-Api)) {
        if ((Get-Date) -gt $deadline) { throw 'API не ответил за 4 минуты. Посмотрите ошибку в окне «Sitora API».' }
        Start-Sleep -Seconds 2
        Write-Host '.' -NoNewline
    }
    Write-Host ''
    Write-Host "API готов: $api (Swagger: $api/swagger)" -ForegroundColor Green
}

Set-Location "$root\frontend"
if (-not (Test-Path node_modules)) {
    Write-Host 'Устанавливаю пакеты фронтенда (первый запуск)…' -ForegroundColor Cyan
    npm install --legacy-peer-deps --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm install завершился с ошибкой.' }
}

Write-Host 'Запускаю фронтенд. Остановить: Ctrl+C (окно API закройте отдельно).' -ForegroundColor Cyan
npm run dev -- --open
