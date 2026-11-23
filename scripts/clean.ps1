# Очистка артефактов сборки и повторное восстановление пакетов.
# Используется при ошибке NETSDK1005 (устаревший obj/project.assets.json).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'FinFlow.sln'

Get-ChildItem -Path $root -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('obj', 'bin') } |
    ForEach-Object {
        Write-Host "Удаление $($_.FullName)"
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }

dotnet restore $solution
Write-Host 'Готово: артефакты удалены, пакеты восстановлены.'
