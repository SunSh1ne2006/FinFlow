# Сквозная проверка работоспособности FinFlow через REST API.
# Запуск (сервер уже работает):  powershell -File scripts/smoke.ps1
# Запуск с автозапуском API:     powershell -File scripts/smoke.ps1 -StartApi
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [switch]$StartApi,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:failures = 0
$script:apiProcess = $null
$script:employee = $null
$script:head = $null
$script:admin = $null
$script:clientId = $null
$script:contractId = $null
$script:accountId = $null
$script:operationId = $null

function Write-Ok([string]$name) {
    Write-Host ("[ ok ] " + $name) -ForegroundColor Green
}

function Write-Fail([string]$name, [string]$message) {
    $script:failures++
    Write-Host ("[FAIL] " + $name + " -> " + $message) -ForegroundColor Red
}

function Test-Case([string]$name, [scriptblock]$body) {
    try {
        & $body
        Write-Ok $name
    }
    catch {
        Write-Fail $name $_.Exception.Message
    }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers,
        $Body
    )

    $uri = $BaseUrl.TrimEnd('/') + $Path
    $request = @{
        Method      = $Method
        Uri         = $uri
        Headers     = $Headers
        ErrorAction = 'Stop'
    }

    if ($null -ne $Body) {
        $request['ContentType'] = 'application/json; charset=utf-8'
        $request['Body'] = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }

    try {
        return Invoke-RestMethod @request
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if (-not $detail) { $detail = $_.Exception.Message }
        throw ("{0} {1}: {2}" -f $Method, $Path, $detail)
    }
}

function New-StaffSession([string]$login, [string]$password) {
    $response = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ login = $login; password = $password }
    if (-not $response.token) { throw 'сервер не вернул токен' }
    return @{ Authorization = 'Bearer ' + $response.token }
}

function Wait-Api([int]$timeoutSeconds = 240) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 5
            if ($health.status -eq 'ok') { return }
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }
    throw "API не ответил на $BaseUrl/api/health за $timeoutSeconds с."
}

function Start-Api {
    $outLog = Join-Path ([System.IO.Path]::GetTempPath()) 'finflow-api.out.log'
    $errLog = Join-Path ([System.IO.Path]::GetTempPath()) 'finflow-api.err.log'
    Remove-Item $outLog, $errLog -ErrorAction SilentlyContinue

    Write-Host ("Запуск API: dotnet run --project src/FinFlow.Api -c " + $Configuration + " --urls " + $BaseUrl)
    $script:apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', 'src/FinFlow.Api', '-c', $Configuration, '--urls', $BaseUrl) `
        -WorkingDirectory $repoRoot -PassThru -NoNewWindow `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog

    try {
        Wait-Api
    }
    catch {
        Write-Host '--- журнал API ---' -ForegroundColor Yellow
        if (Test-Path $outLog) { Get-Content $outLog -Tail 100 }
        if (Test-Path $errLog) { Get-Content $errLog -Tail 100 }
        throw
    }
}

function Stop-Api {
    if ($null -eq $script:apiProcess) { return }
    & taskkill /PID $script:apiProcess.Id /T /F 2>$null | Out-Null
    $script:apiProcess = $null
}

try {
    if ($StartApi) { Start-Api } else { Wait-Api 10 }

    Test-Case 'сервер отвечает /api/health' {
        $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health')
        if ($health.status -ne 'ok') { throw 'status != ok' }
    }

    Test-Case 'личный кабинет отдаёт страницу /client/' {
        $page = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/client/') -UseBasicParsing
        if ($page.StatusCode -ne 200) { throw "код $($page.StatusCode)" }
        if ($page.Content -notmatch 'FinFlow') { throw 'страница не содержит FinFlow' }
    }

    Test-Case 'вход сотрудника employee1' {
        $script:employee = New-StaffSession 'employee1' 'FinFlow#2026!'
    }

    Test-Case 'список клиентов не пуст' {
        if ($null -eq $script:employee) { throw 'нет сеанса' }
        $clients = Invoke-Api -Method Get -Path '/api/clients?page=1&pageSize=10' -Headers $script:employee
        if ($clients.total -lt 1) { throw 'нет клиентов в демо-базе' }
    }

    Test-Case 'регистрация клиента' {
        if ($null -eq $script:employee) { throw 'нет сеанса' }
        $stamp = (Get-Date).ToString('yyMMddHHmmss')
        $unique = [guid]::NewGuid().ToString('N').Substring(0, 6)
        $created = Invoke-Api -Method Post -Path '/api/clients' -Headers $script:employee -Body @{
            type       = 'Individual'
            name       = 'Smoke Test Client ' + $stamp
            idNumber   = 'SMK' + $stamp + $unique
            birthDate  = '1990-01-01'
            phone      = '+375 (29) 000-00-01'
            isResident = $true
        }
        if (-not $created.id) { throw 'клиент не создан' }
        $script:clientId = $created.id
    }

    Test-Case 'открытие вклада по продукту' {
        $products = Invoke-Api -Method Get -Path '/api/products?kind=Deposit' -Headers $script:employee
        $product = $products | Where-Object { $_.code -eq 'BYN-OTZYV' } | Select-Object -First 1
        if ($null -eq $product) { throw 'продукт BYN-OTZYV не найден' }
        $details = Invoke-Api -Method Get -Path ('/api/products/' + $product.id) -Headers $script:employee
        $version = $details.versions | Where-Object { $_.isApproved } | Select-Object -First 1
        if ($null -eq $version) { throw 'нет утверждённой версии условий' }
        $contract = Invoke-Api -Method Post -Path '/api/contracts' -Headers $script:employee -Body @{
            clientId         = $script:clientId
            productVersionId = $version.id
            amount           = 1000
            termMonths       = 6
        }
        if ([decimal]$contract.principal -ne 1000) { throw ("тело вклада " + $contract.principal) }
        $script:contractId = $contract.id
        $script:accountId = $contract.accountId
    }

    Test-Case 'взнос наличными по вкладу' {
        $operation = Invoke-Api -Method Post -Path '/api/operations' -Headers $script:employee -Body @{
            accountId   = $script:accountId
            contractId  = $script:contractId
            kind        = 'CashIn'
            amount      = 500
            description = 'Smoke test cash in'
        }
        if ($operation.status -ne 'Executed') { throw ("статус " + $operation.status + ': ' + $operation.rejectionReason) }
        $script:operationId = $operation.id
    }

    Test-Case 'остаток счёта после взноса' {
        $account = Invoke-Api -Method Get -Path ('/api/accounts/' + $script:accountId) -Headers $script:employee
        if ([decimal]$account.balance -ne 1500) { throw ("остаток " + $account.balance) }
    }

    Test-Case 'сторнирование операции' {
        $reversal = Invoke-Api -Method Post -Path ('/api/operations/' + $script:operationId + '/reverse') -Headers $script:employee -Body @{
            reason = 'Smoke test reversal'
        }
        if ($reversal.status -ne 'Executed') { throw "статус $($reversal.status)" }
        $account = Invoke-Api -Method Get -Path ('/api/accounts/' + $script:accountId) -Headers $script:employee
        if ([decimal]$account.balance -ne 1000) { throw ("остаток после сторно " + $account.balance) }
    }

    Test-Case 'расчёт досрочного возврата' {
        $quote = Invoke-Api -Method Get -Path ('/api/contracts/' + $script:contractId + '/early-return-quote') -Headers $script:employee
        if (-not $quote.allowed) { throw 'досрочный возврат запрещён без причины' }
        if ([decimal]$quote.netPayout -le 0) { throw 'нулевая выплата' }
    }

    Test-Case 'досрочный возврат закрывает вклад и счёт' {
        $operation = Invoke-Api -Method Post -Path '/api/operations' -Headers $script:employee -Body @{
            accountId  = $script:accountId
            contractId = $script:contractId
            kind       = 'EarlyReturn'
            amount     = 1
        }
        if ($operation.status -ne 'Executed') { throw ("статус " + $operation.status + ': ' + $operation.rejectionReason) }
        $account = Invoke-Api -Method Get -Path ('/api/accounts/' + $script:accountId) -Headers $script:employee
        if ($account.status -ne 'Closed') { throw ("счёт в статусе " + $account.status) }
        if ([decimal]$account.balance -ne 0) { throw ("остаток " + $account.balance) }
    }

    Test-Case 'закрытие операционного дня' {
        $day = Invoke-Api -Method Post -Path '/api/dayclose/start' -Headers $script:employee
        if ($day.stage -ne 'Completed') { throw ("этап " + $day.stage) }
        $history = Invoke-Api -Method Get -Path '/api/dayclose/history?take=5' -Headers $script:employee
        if (@($history).Count -lt 1) { throw 'история закрытий пуста' }
    }

    Test-Case 'отчёт руководителя о работе сотрудников' {
        $script:head = New-StaffSession 'head' 'FinFlow#2026!'
        $from = (Get-Date).AddDays(-30).ToString('yyyy-MM-dd')
        $to = (Get-Date).ToString('yyyy-MM-dd')
        $report = Invoke-Api -Method Get -Path ("/api/reports/staff?from=$from&to=$to") -Headers $script:head
        if (@($report.rows).Count -lt 1) { throw 'нет строк отчёта' }
        if ($report.totals.employees -lt 2) { throw ("сотрудников " + $report.totals.employees) }
        if ($report.totals.clientsRegistered -lt 1) { throw 'не учтены клиенты' }
    }

    Test-Case 'администрирование: пользователи и журнал аудита' {
        $script:admin = New-StaffSession 'admin' 'FinFlow#2026!'
        $users = Invoke-Api -Method Get -Path '/api/users' -Headers $script:admin
        if (@($users).Count -lt 4) { throw ("пользователей " + @($users).Count) }
        $audit = Invoke-Api -Method Get -Path '/api/audit?page=1&pageSize=10' -Headers $script:admin
        if ($audit.total -lt 1) { throw 'журнал аудита пуст' }
    }

    Test-Case 'личный кабинет клиента: вход и вклады' {
        $portal = Invoke-Api -Method Post -Path '/api/portal/auth/login' -Body @{
            documentNumber = '301AB1234'
            birthDate      = '1985-03-14'
        }
        if (-not $portal.token) { throw 'нет токена кабинета' }
        $portalHeaders = @{ Authorization = 'Bearer ' + $portal.token }
        $deposits = Invoke-Api -Method Get -Path '/api/portal/deposits' -Headers $portalHeaders
        if (@($deposits).Count -lt 1) { throw 'у клиента нет вкладов' }
        $offers = Invoke-Api -Method Get -Path '/api/portal/offers' -Headers $portalHeaders
        if (@($offers.offers).Count -lt 1) { throw 'нет опубликованных условий' }
    }
}
finally {
    Stop-Api
}

if ($script:failures -gt 0) {
    Write-Host ("Проверок не пройдено: " + $script:failures) -ForegroundColor Red
    exit 1
}

Write-Host 'Все проверки пройдены.' -ForegroundColor Green
exit 0
