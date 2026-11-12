# Спецификация REST API

Базовый адрес: `http://localhost:5080`. Формат — JSON (`camelCase`,
перечисления — строки). Интерактивная документация — `/swagger`.

## 1. Аутентификация

Схема `FinFlowToken`: заголовок `Authorization: Bearer <token>`.
Токен выдаётся на 15 минут; роли проверяются на каждом действии.

```bash
curl -s -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"operator1","password":"FinFlow#2026!"}'
```

```json
{
  "token": "…",
  "expiresAt": "2026-11-12T10:15:00Z",
  "secondFactorRequired": false,
  "user": {
    "id": "…",
    "login": "operator1",
    "fullName": "Операционист ЦБУ № 1",
    "roles": ["Operator"],
    "roleNames": ["Операционист ЦБУ"],
    "twoFactorEnabled": false,
    "isBlocked": false
  }
}
```

Контролёр и администратор передают `secondFactor`. Если код не указан, сервер
возвращает `secondFactorRequired: true` без токена.

| Метод | Путь | Доступ | Назначение |
|-------|------|--------|-----------|
| POST | `/api/auth/login` | анонимно | вход |
| GET | `/api/auth/me` | любой авторизованный | текущий пользователь |
| POST | `/api/auth/logout` | любой авторизованный | отзыв токена |
| GET | `/api/meta` | авторизованный | роли, валюты, виды операций, лимиты |
| GET | `/api/meta/roles` | авторизованный | справочник ролей |
| GET | `/api/health` | анонимно | состояние сервера |

## 2. Клиенты и ПОД/ФТ

| Метод | Путь | Роли |
|-------|------|------|
| GET | `/api/clients?query=&page=1&pageSize=20` | Operator, Controller, Accountant, Administrator, Head |
| GET | `/api/clients/{id}` | те же |
| POST | `/api/clients` | Operator, Administrator |
| PUT | `/api/clients/{id}/kyc` | Operator, Administrator, AmlOfficer |
| POST | `/api/clients/{id}/representatives` | Operator, Administrator |
| POST | `/api/clients/{id}/msi-sync` | Operator, Administrator |

## 3. Продукты, счета, договоры

| Метод | Путь | Роли |
|-------|------|------|
| GET | `/api/products` | авторизованный |
| GET | `/api/products/{id}` | авторизованный |
| POST | `/api/products` | Administrator, Head, Treasurer |
| POST | `/api/products/{id}/versions` | Administrator, Head, Treasurer |
| POST | `/api/products/versions/{versionId}/approve?force=false` | Head |
| GET | `/api/products/versions/{versionId}/rvsr` | авторизованный |
| GET | `/api/accounts?clientId=` | операционный контур, казначейство |
| GET | `/api/accounts/{id}` | те же |
| GET | `/api/accounts/lookup?number=` | те же |
| GET | `/api/accounts/{id}/balance?date=` | те же |
| POST | `/api/accounts` | Operator |
| PUT | `/api/accounts/{id}/overdraft` | Operator, Controller, Administrator |
| POST | `/api/accounts/{id}/holds` | Operator, Controller, Administrator |
| GET | `/api/accounts/{id}/holds` | операционный контур |
| POST | `/api/accounts/holds/{holdId}/release` | Operator, Controller, Administrator |
| POST | `/api/accounts/{id}/close` | Operator, Controller, Administrator |
| GET | `/api/contracts?clientId=&status=` | операционный контур, казначейство |
| GET | `/api/contracts/{id}` | те же |
| POST | `/api/contracts` | Operator |
| POST | `/api/contracts/{id}/consent` | Controller, Head, Administrator |
| GET | `/api/contracts/{id}/early-return-quote?days=` | операционный контур |
| GET | `/api/contracts/{id}/accruals` | операционный контур |

## 4. Операции

| Метод | Путь | Роли |
|-------|------|------|
| GET | `/api/operations?accountId=&status=&take=100` | авторизованный |
| GET | `/api/operations/{id}` | авторизованный |
| GET | `/api/operations/queue?take=200` | Controller, Administrator, Head |
| GET | `/api/operations/suspended` | AmlOfficer, Administrator, Head |
| POST | `/api/operations` | Operator, Administrator |
| POST | `/api/operations/{id}/submit` | Operator, Administrator |
| POST | `/api/operations/{id}/approve` | Controller, Administrator, Head |
| POST | `/api/operations/{id}/reject` | Controller, Administrator, Head, AmlOfficer |
| POST | `/api/operations/{id}/reverse` | Controller, Administrator |

Создание операции:

```json
POST /api/operations
{
  "accountId": "…",
  "contractId": "…",
  "kind": "CashIn",
  "amount": 12000,
  "description": "Взнос наличными в кассу",
  "documentNumber": "КО-000123",
  "sourceSystem": "Branch",
  "externalId": null
}
```

Ответ — `OperationDto` со статусом `Draft`. После `POST /submit` статус
становится `Executed` (сумма в пределах лимита), `OnControl` (свыше 5 000 р.)
или `Suspended` (признаки ПОД/ФТ).

Подтверждение контролёром:

```json
POST /api/operations/{id}/approve
{ "signature": "SIG-20261112101500-controller1" }
```

Ошибки бизнес-правил возвращаются с HTTP 403/409 и понятным сообщением:

```json
{ "status": 409, "title": "Изменился доступный остаток на момент подтверждения: доступно 9000.00 р., требуется 12000.00 р." }
```

## 5. Аналитика

| Метод | Путь | Роли |
|-------|------|------|
| GET | `/api/liquidity/forecast?days=30&scenario=model|db` | Treasurer, Head, RiskManager, Administrator |
| GET | `/api/liquidity/settings` | те же |
| PUT | `/api/liquidity/settings` | Treasurer, Head, Administrator |
| GET | `/api/margin?scenario=model|db` | Head, Treasurer, RiskManager, Administrator |
| GET | `/api/risks` | RiskManager, Head, Treasurer, Administrator |
| GET | `/api/risks/summary` | те же |
| POST | `/api/risks/{id}/assessments` | RiskManager, Head, Administrator |
| GET | `/api/aml/queue` | AmlOfficer, Administrator, Head |
| POST | `/api/aml/{operationId}/decision` | AmlOfficer, Administrator, Head |
| GET | `/api/parameters` | Administrator, Head, Treasurer, Accountant |
| POST | `/api/parameters` | Administrator |
| GET | `/api/audit?entity=&action=&page=1&pageSize=50` | Administrator, Head |
| GET | `/api/users` | Administrator |
| POST | `/api/users` | Administrator |
| POST | `/api/users/{id}/block?blocked=true` | Administrator |

Отчёты (`Head`, `Accountant`, `Treasurer`, `RiskManager`, `Administrator`):
`/api/reports/portfolio`, `/api/reports/portfolio.csv`, `/api/reports/agv`,
`/api/reports/holds`, `/api/reports/tax`.

## 6. Интеграции

| Метод | Путь | Доступ | Назначение |
|-------|------|--------|-----------|
| POST | `/api/integrations/ais-ido/documents` | анонимно (сервисный вызов) | приём ареста/приостановления/требования |
| POST | `/api/integrations/ledger/sync` | Accountant, Administrator, Head | передача проводок в главную книгу |

```json
POST /api/integrations/ais-ido/documents
{
  "externalId": "IDO-2026-000741",
  "kind": "Arrest",
  "accountNumber": "3014000000001",
  "amount": 4000,
  "basisNumber": "ИД-14/2026",
  "basisAuthority": "Отдел принудительного исполнения",
  "validFrom": "2026-11-12",
  "comment": "Арест в пределах суммы"
}
```

Ответ:

```json
{ "accepted": true, "duplicate": false, "message": "Ограничение зарегистрировано",
  "holdId": "…", "accountId": "…", "documentId": "…" }
```

Повторная отправка того же `externalId` возвращает `duplicate: true` и ссылки
на ранее созданные записи (требование 23).

## 7. День закрытия

| Метод | Путь | Роли |
|-------|------|------|
| GET | `/api/dayclose/current` | Accountant, Administrator, Head |
| GET | `/api/dayclose/history?take=30` | те же |
| POST | `/api/dayclose/start` | те же |
| POST | `/api/dayclose/continue` | те же |
| GET | `/api/dayclose/reconciliation?date=` | те же |

## 8. Коды ошибок

| Код | Значение |
|-----|----------|
| 400 | некорректные параметры запроса (ProblemDetails) |
| 401 | отсутствует или истёк токен |
| 403 | недостаточно прав или нарушено бизнес-правило (например, автор ≠ контролёр) |
| 404 | объект не найден |
| 409 | конфликт состояния или конкурентное изменение остатка |

`GET /swagger/v1/swagger.json` содержит полную схему; в Swagger UI доступна
кнопка `Authorize` для подстановки токена.
