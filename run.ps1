# Запуск для разработки: API в отдельном окне, сайт (папка web/) в этом окне, браузер открывается сам.
# Секреты API берутся из user secrets (см. README, раздел «Переменные окружения»).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$api = 'http://localhost:5194'
$site = 'http://localhost:5500'

function Test-Api {
    try { (Invoke-WebRequest "$api/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Не найден .NET SDK (dotnet). Установите .NET 10 SDK.' }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Не найден Node.js (нужен для статик-сервера сайта). Установите Node.js.' }

if (Test-Api) {
    Write-Host "API уже запущен: $api" -ForegroundColor Green
} else {
    Write-Host 'Запускаю API в отдельном окне…' -ForegroundColor Cyan
    # Окружение задаётся переменной: флаг --environment у dotnet run в .NET 10 означает переменные окружения процесса.
    Start-Process powershell -WorkingDirectory "$root\src\Sitora.WebApi" -ArgumentList '-NoExit', '-Command', '$Host.UI.RawUI.WindowTitle = ''Sitora API''; $env:ASPNETCORE_ENVIRONMENT = ''Development''; dotnet run --no-launch-profile --urls http://localhost:5194'

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

Write-Host "Сайт: $site — CRM и кабинет открываются из меню сайта. Остановить: Ctrl+C (окно API закройте отдельно)." -ForegroundColor Cyan
Start-Process $site
node "$root\scripts\serve-web.mjs" 5500
