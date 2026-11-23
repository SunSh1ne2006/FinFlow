# REST API

Базовый адрес: `http://localhost:5080`. Полная интерактивная документация —
Swagger UI: `/swagger`, схема OpenAPI: `/swagger/v1/swagger.json`.

JSON в `camelCase`; перечисления передаются строками. Даты — `YYYY-MM-DD`,
моменты времени — ISO 8601.

## 1. Аутентификация

Сотрудник получает непрозрачный токен и передаёт его в заголовке:

```
Authorization: Bearer <token>
```

| Метод | Путь | Доступ | Назначение |
|-------|------|--------|-----------|
| POST | `/api/auth/login` | анонимно | вход по логину и паролю |
| GET | `/api/auth/me` | сотрудник | текущий пользователь и роли |
| POST | `/api/auth/logout` | сотрудник | завершение сеанса |

```json
POST /api/auth/login
{ "login": "employee1", "password": "FinFlow#2026!" }

200 OK
{
  "token": "ff_...",
  "expiresAt": "2026-11-12T18:00:00Z",
  "user": {
    "id": "…",
    "login": "employee1",
    "fullName": "Сотрудник ЦБУ № 1 Иванова А. П.",
    "roles": ["Employee"],
    "roleNames": ["Сотрудник банка"]
  }
}
```

## 2. Клиенты (роль `Employee`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/clients?query=&page=1&pageSize=20` | поиск с постраничной выдачей |
| GET | `/api/clients/{id}` | карточка клиента со счетами |
| POST | `/api/clients` | регистрация клиента |
| PUT | `/api/clients/{id}` | изменение данных и идентификация |

```json
POST /api/clients
{
  "type": "Individual",
  "name": "Иванов Иван Иванович",
  "idNumber": "301AB1234",
  "birthDate": "1985-03-14",
  "phone": "+375 (29) 123-45-67",
  "email": "ivanov@example.by"
}
```

`type`: `Individual`, `LegalEntity`, `Entrepreneur`. Для физического лица
`birthDate` обязательна.

```json
PUT /api/clients/{id}
{ "phone": "+375 (29) 765-43-21", "identify": true }
```

## 3. Счета и ограничения (`Employee`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/accounts?clientId={id}` | счета клиента |
| GET | `/api/accounts/{id}` | карточка счёта |
| GET | `/api/accounts/lookup?number=...` | поиск по прежнему или изменённому номеру |
| GET | `/api/accounts/{id}/balance?date=` | снимок остатка B/L/H/D |
| POST | `/api/accounts` | открытие счёта (`Employee`) |
| PUT | `/api/accounts/{id}/overdraft` | изменение лимита овердрафта |
| POST | `/api/accounts/{id}/close` | закрытие счёта |
| GET | `/api/accounts/{id}/holds` | действующие ограничения |
| POST | `/api/accounts/{id}/holds` | регистрация ограничения |
| POST | `/api/accounts/holds/{holdId}/release` | снятие ограничения |

```json
POST /api/accounts
{
  "clientId": "…",
  "productId": "…",
  "currency": "BYN",
  "overdraftLimit": 5000,
  "initialAmount": 1000
}
```

```json
POST /api/accounts/{id}/holds
{
  "source": "Bank",
  "basisNumber": "АРЕСТ-2026-771",
  "basisAuthority": "Отдел принудительного исполнения",
  "amount": 4000,
  "isArrest": true,
  "comment": "Постановление об аресте"
}
```

`source`: `Bank`, `CardAuthorization`, `Booking`.

## 4. Продукты

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/products?kind=Deposit` | справочник продуктов и версий условий |
| GET | `/api/products/{id}` | продукт с версиями |

`kind`: `Deposit` или `CurrentAccount`. Создание и утверждение версий через API
не выполняется: справочник формируется при инициализации базы.

## 5. Договоры вкладов (`Employee`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/contracts?clientId=&status=` | договоры клиента или отделения |
| GET | `/api/contracts/{id}` | договор |
| POST | `/api/contracts` | открыть вклад (`Employee`) |
| POST | `/api/contracts/{id}/consent` | согласие банка на досрочный возврат |
| GET | `/api/contracts/{id}/early-return-quote?days=` | расчёт досрочного возврата |
| GET | `/api/contracts/{id}/accruals` | начисления по договору |

```json
POST /api/contracts
{
  "clientId": "…",
  "productVersionId": "…",
  "amount": 30000,
  "termMonths": 12,
  "notes": "Оформлен в ЦБУ № 1"
}
```

## 6. Операции (`Employee`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/operations?accountId=&status=&take=100` | операции по счёту |
| GET | `/api/operations/{id}` | операция |
| POST | `/api/operations` | провести операцию |
| POST | `/api/operations/{id}/reverse` | сторнировать операцию |

```json
POST /api/operations
{
  "accountId": "…",
  "contractId": "…",
  "kind": "CashIn",
  "amount": 2500,
  "description": "Пополнение счёта наличными",
  "documentNumber": "ПКО-20261112-0001"
}
```

Допустимые виды для ручного проведения: `CashIn`, `TransferIn`, `CashOut`,
`TransferOut`, `PartialWithdrawal`, `EarlyReturn`. Системные виды
(`InterestPayment`, `Capitalization`, `TaxWithholding`) и `Reversal` создаются
автоматически.

Ответ содержит статус `Executed` или `Rejected` с причиной:

```json
{
  "id": "…",
  "kind": "CashOut",
  "amount": 8000,
  "status": "Rejected",
  "rejectionReason": "Недостаточно средств: доступный остаток 200.00 р., требуется 8000.00 р.",
  "checkedAvailable": 200.00,
  "executedAt": null
}
```

Для вида `EarlyReturn` поле `amount` не используется: сумма выплаты рассчитывается
по формулам (1.9)–(1.10) из тела вклада, ранее выплаченных процентов и ставки до
востребования; после исполнения счёт и договор закрываются.

Сторнирование: `{ "reason": "Ошибочная сумма" }`. Доступно для исполненной и ещё
не сторнированной операции ручного вида.

## 7. Закрытие операционного дня (`Employee`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/dayclose/current` | последний операционный день |
| GET | `/api/dayclose/history?take=30` | история закрытий |
| POST | `/api/dayclose/start` | запустить закрытие дня |
| POST | `/api/dayclose/continue` | продолжить с сохранённого этапа |

```json
{
  "id": "…",
  "date": "2026-11-12",
  "status": "Closed",
  "stage": "Completed",
  "stageText": "Закрытие завершено",
  "accrualCount": 9,
  "rejectedOperations": 0,
  "interestAccrued": 1234.56,
  "taxWithheld": 98.76
}
```

## 8. Отчёт о работе сотрудников (`Head`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/reports/staff?from=2026-10-01&to=2026-10-31` | отчёт за период |

```json
{
  "from": "2026-10-01",
  "to": "2026-10-31",
  "generatedAt": "2026-11-12T09:00:00Z",
  "totals": {
    "employees": 2,
    "clientsRegistered": 8,
    "accountsOpened": 14,
    "depositsOpened": 6,
    "depositAmount": 132500,
    "operationsCount": 5,
    "operationsAmount": 7000,
    "dayCloses": 3
  },
  "rows": [
    {
      "userId": "…",
      "login": "employee1",
      "fullName": "Сотрудник ЦБУ № 1 Иванова А. П.",
      "role": "Сотрудник банка",
      "clientsRegistered": 5,
      "accountsOpened": 8,
      "depositsOpened": 4,
      "depositAmount": 92000,
      "operationsCount": 3,
      "operationsAmount": 4500,
      "dayCloses": 2
    }
  ]
}
```

## 9. Пользователи (`Administrator`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/users` | список сотрудников |
| POST | `/api/users` | создание учётной записи |
| POST | `/api/users/{id}/block?blocked=true` | блокировка / разблокировка |

```json
POST /api/users
{
  "login": "employee3",
  "fullName": "Сотрудник ЦБУ № 3",
  "password": "FinFlow#2026!",
  "role": "Employee"
}
```

`role`: `Employee`, `Administrator`, `Head`.

## 10. Журнал аудита (`Administrator`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/audit?entity=&action=&page=1&pageSize=50` | записи журнала |

## 11. Личный кабинет клиента

Отдельная схема аутентификации; токен также передаётся в `Authorization: Bearer`.

| Метод | Путь | Назначение |
|-------|------|-----------|
| POST | `/api/portal/auth/login` | вход по документу и дате рождения |
| POST | `/api/portal/auth/logout` | завершение сеанса |
| GET | `/api/portal/profile` | профиль клиента |
| GET | `/api/portal/deposits` | вклады с суммами и сроками |
| GET | `/api/portal/deposits/{contractId}` | детали вклада и оценка досрочного возврата |
| GET | `/api/portal/offers` | опубликованные условия вкладов |

```json
POST /api/portal/auth/login
{ "documentNumber": "301AB1234", "birthDate": "1985-03-14" }
```

## 12. Служебные

| Метод | Путь | Назначение |
|-------|------|-----------|
| GET | `/api/health` | состояние сервера и доступность базы данных (`database`: `ok` / `unavailable`) |
| GET | `/` | перенаправление на `/client/` |
| GET | `/swagger` | Swagger UI |

## 13. Ошибки

Ошибки возвращаются в формате ProblemDetails; поле `title` содержит текст на
русском языке.

| Код | Причина |
|-----|---------|
| 400 | некорректные параметры запроса |
| 401 | нет или истёк токен, неверный пароль |
| 403 | роль не имеет прав |
| 404 | объект не найден |
| 409 | конфликт бизнес-правила (недостаточно средств, закрытый счёт, дубль) |
