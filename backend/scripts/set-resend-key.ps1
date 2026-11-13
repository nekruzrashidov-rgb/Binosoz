# Переключает отправку писем API (Development) на Resend: сохраняет ключ и отправителя в user secrets.
# Ключ вводится скрыто и нигде не печатается. Запуск из корня репозитория:
#   powershell -ExecutionPolicy Bypass -File scripts/set-resend-key.ps1
# Без своего подтверждённого домена Resend отправляет только с onboarding@resend.dev
# и только на почту, с которой зарегистрирован аккаунт Resend.
param([string]$From = 'onboarding@resend.dev')

$project = Join-Path $PSScriptRoot '..\backend\src\Sitora.WebApi'
$secure = Read-Host 'Ключ Resend (re_...)' -AsSecureString
$key = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)).Trim()
if (-not $key.StartsWith('re_')) { Write-Host 'Ключ Resend начинается с re_ — проверьте, что скопировали его целиком.' -ForegroundColor Red; exit 1 }

dotnet user-secrets set 'Email:Resend:ApiKey' $key --project $project | Out-Null
dotnet user-secrets set 'Email:From' $From --project $project | Out-Null
dotnet user-secrets set 'Email:Provider' 'Resend' --project $project | Out-Null
Write-Host "Готово: письма уходят через Resend от $From. Перезапустите API." -ForegroundColor Green
