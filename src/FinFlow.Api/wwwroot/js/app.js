/* FinFlow — аналитический веб-клиент. Один файл без сборки: маршруты, представления, вызовы API. */
(() => {
    'use strict';

    const state = {
        user: null,
        meta: null,
        route: 'dashboard',
        liquidityScenario: 'db',
        liquidityDays: 30,
        marginScenario: 'model',
        auditFilter: ''
    };

    const routes = {
        dashboard: {
            title: 'Панель руководителя',
            subtitle: 'Сводные показатели портфеля, стоимости ресурсов, ликвидности и рисков',
            roles: ['Head', 'Treasurer', 'RiskManager', 'Administrator'],
            render: renderDashboard
        },
        liquidity: {
            title: 'Ликвидность',
            subtitle: 'Прогноз оттока, запаса и разрыва по формулам (1.11)–(1.13)',
            roles: ['Treasurer', 'Head', 'RiskManager', 'Administrator'],
            render: renderLiquidity
        },
        margin: {
            title: 'Ресурсы и маржа',
            subtitle: 'Средневзвешенная стоимость ресурсов и маржа вкладного портфеля',
            roles: ['Head', 'Treasurer', 'RiskManager', 'Administrator'],
            render: renderMargin
        },
        risks: {
            title: 'Риски и резерв',
            subtitle: 'Реестр R-01 – R-12, матрица вероятности и влияния, резерв P80',
            roles: ['RiskManager', 'Head', 'Treasurer', 'Administrator'],
            render: renderRisks
        },
        products: {
            title: 'Продукты и ставки',
            subtitle: 'Версии условий, контроль РВСР при утверждении',
            roles: ['Head', 'Treasurer', 'Administrator'],
            render: renderProducts
        },
        aml: {
            title: 'ПОД/ФТ',
            subtitle: 'Приостановленные операции и решения специалиста',
            roles: ['AmlOfficer', 'Head', 'Administrator'],
            render: renderAml
        },
        reports: {
            title: 'Отчёты',
            subtitle: 'Портфель, реестр для АГВ, налог, сверка с главной книгой',
            roles: ['Head', 'Accountant', 'Treasurer', 'RiskManager', 'Administrator'],
            render: renderReports
        },
        audit: {
            title: 'Журнал аудита',
            subtitle: 'Неизменяемые записи о действиях пользователей',
            roles: ['Head', 'Administrator'],
            render: renderAudit
        }
    };

    const moneyFormat = new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const integerFormat = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });

    const $ = (selector) => document.querySelector(selector);

    function esc(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function money(value, unit) {
        return moneyFormat.format(Number(value || 0)) + (unit ? ' ' + unit : '');
    }

    function rate(value) {
        const number = Number(value || 0);
        return number.toLocaleString('ru-RU', { maximumFractionDigits: 4 }) + ' %';
    }

    function count(value) {
        return integerFormat.format(Number(value || 0));
    }

    function shortDate(value) {
        if (!value) return '—';
        const parts = String(value).slice(0, 10).split('-');
        return parts.length === 3 ? parts[2] + '.' + parts[1] : String(value);
    }

    function dateTime(value) {
        if (!value) return '—';
        const date = new Date(value);
        if (isNaN(date.getTime())) return String(value);
        return date.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function toast(message, kind) {
        const node = $('#toast');
        node.textContent = message;
        node.className = 'toast show ' + (kind || '');
        clearTimeout(toast.timer);
        toast.timer = setTimeout(() => { node.className = 'toast'; }, 4200);
    }

    function kpi(label, value, hint, accent) {
        return '<div class="kpi' + (accent ? ' accent' : '') + '">' +
            '<div class="label">' + esc(label) + '</div>' +
            '<div class="value mono">' + value + '</div>' +
            (hint ? '<div class="hint">' + esc(hint) + '</div>' : '') +
            '</div>';
    }

    function card(title, subtitle, body, actions) {
        return '<section class="card">' +
            '<div class="card-head"><div><h2>' + esc(title) + '</h2>' +
            (subtitle ? '<p>' + esc(subtitle) + '</p>' : '') + '</div>' +
            (actions ? '<div class="btn-row">' + actions + '</div>' : '') +
            '</div>' + body + '</section>';
    }

    function table(headers, rows, footer) {
        const head = '<thead><tr>' + headers.map((header) =>
            '<th' + (header.num ? ' class="num"' : '') + '>' + esc(header.text || header) + '</th>').join('') + '</tr></thead>';
        const body = '<tbody>' + (rows.length ? rows.join('') : '<tr><td colspan="' + headers.length + '"><div class="empty">Нет данных</div></td></tr>') + '</tbody>';
        const foot = footer ? '<tfoot><tr>' + footer.map((cell) =>
            '<td' + (cell.num ? ' class="num"' : '') + ' colspan="' + (cell.colspan || 1) + '">' + cell.text + '</td>').join('') + '</tr></tfoot>' : '';
        return '<div class="table-wrap"><table>' + head + body + foot + '</table></div>';
    }

    function zoneBadge(zone, text) {
        const map = { Low: 'badge-green', Moderate: 'badge-yellow', High: 'badge-orange', Critical: 'badge-red' };
        return '<span class="badge ' + (map[zone] || 'badge-blue') + '">' + esc(text || zone) + '</span>';
    }

    function setLoading() {
        $('#view').innerHTML = '<div class="loading">Загрузка данных…</div>';
    }

    function renderError(error) {
        $('#view').innerHTML = card('Не удалось загрузить данные', null,
            '<p class="section-note">' + esc(error.message || error) + '</p>' +
            '<div class="btn-row" style="margin-top:12px"><button class="btn" onclick="location.reload()">Обновить страницу</button></div>');
    }

    /* ---------------- Вход и каркас ---------------- */

    function showLogin(message) {
        $('#login').classList.remove('hidden');
        $('#app').classList.add('hidden');
        $('#login-error').textContent = message || '';
        $('#second-factor-row').classList.add('hidden');
        state.user = null;
    }

    async function handleLogin(event) {
        event.preventDefault();
        const login = $('#login-username').value.trim();
        const password = $('#login-password').value;
        const secondFactor = $('#login-second-factor').value.trim();
        const errorNode = $('#login-error');
        errorNode.textContent = '';

        try {
            const response = await API.post('/api/auth/login', {
                login,
                password,
                secondFactor: secondFactor || null
            });

            if (response.secondFactorRequired) {
                $('#second-factor-row').classList.remove('hidden');
                errorNode.textContent = 'Введите код второго фактора (демонстрационный код 123456).';
                return;
            }

            API.setToken(response.token);
            state.user = response.user;
            await startApp();
        } catch (error) {
            errorNode.textContent = error.message;
        }
    }

    async function startApp() {
        $('#login').classList.add('hidden');
        $('#app').classList.remove('hidden');

        if (!state.meta) {
            try {
                state.meta = await API.get('/api/meta');
            } catch (error) {
                state.meta = null;
            }
        }

        if (!state.user) {
            state.user = await API.get('/api/auth/me');
        }

        const roles = state.user.roles || [];
        $('#current-user').innerHTML = '<strong>' + esc(state.user.fullName) + '</strong>' +
            esc((state.user.roleNames || []).join(', ')) + '<br>Логин: ' + esc(state.user.login);

        $('#server-date').textContent = new Date().toLocaleDateString('ru-RU', {
            day: '2-digit', month: 'long', year: 'numeric'
        });

        renderNav(roles);

        const hash = (location.hash || '').replace('#', '');
        const route = routes[hash] && hasAccess(hash, roles) ? hash : firstAllowed(roles);
        state.route = route;

        if (location.hash !== '#' + route) {
            location.hash = '#' + route;
        }

        await render();
    }

    function hasAccess(route, roles) {
        const allowed = routes[route].roles;
        return roles.some((role) => allowed.indexOf(role) >= 0);
    }

    function firstAllowed(roles) {
        const order = ['dashboard', 'liquidity', 'margin', 'risks', 'products', 'aml', 'reports', 'audit'];
        const found = order.find((route) => hasAccess(route, roles));
        return found || 'dashboard';
    }

    function renderNav(roles) {
        document.querySelectorAll('#nav a').forEach((link) => {
            const route = link.getAttribute('data-route');
            link.classList.toggle('hidden', !routes[route] || !hasAccess(route, roles));
            link.classList.toggle('active', route === state.route);
        });
    }

    async function render() {
        const route = routes[state.route];
        if (!route) return;

        $('#page-title').textContent = route.title;
        $('#page-subtitle').textContent = route.subtitle;
        document.querySelectorAll('#nav a').forEach((link) => {
            link.classList.toggle('active', link.getAttribute('data-route') === state.route);
        });

        setLoading();

        try {
            await route.render();
        } catch (error) {
            renderError(error);
        }
    }

    /* ---------------- Панель руководителя ---------------- */

    async function renderDashboard() {
        const roles = (state.user && state.user.roles) || [];
        const tasks = [
            API.get('/api/margin?scenario=model'),
            API.get('/api/risks/summary'),
            API.get('/api/reports/portfolio').catch(() => null),
            hasAccess('liquidity', roles)
                ? API.get('/api/liquidity/forecast?days=30&scenario=db').catch(() => null)
                : Promise.resolve(null)
        ];

        const [margin, risks, portfolio, liquidity] = await Promise.all(tasks);

        const topRisks = (risks.risks || []).slice().sort((a, b) => b.emv - a.emv).slice(0, 6);
        const planReserve = margin.riskReserve;

        let html = '<div class="grid grid-kpi">' +
            kpi('Портфель привлечённых средств', money(margin.totalVolume, margin.unit), 'модельный портфель п. 1.3.5', true) +
            kpi('Стоимость ресурсов r̄', rate(margin.weightedRate), 'взвешенная по объёмам (1.14)') +
            kpi('Маржа M', money(margin.margin, margin.unit), 'до резерва на риски (1.15)') +
            kpi('Маржа с резервом П', money(margin.netMargin, margin.unit), 'П = M − Rрез (1.16)') +
            kpi('EMV рисков', money(risks.expectedMonetaryValue, 'тыс. р.'), 'Σp×C (1.18)') +
            kpi('Резерв P80', money(risks.reserve, 'тыс. р.'), 'EMV + 0,84σ (1.20)') +
            '</div>';

        const liquidityKpis = liquidity
            ? '<div class="grid grid-kpi">' +
                kpi('Запас ликвидности на конец горизонта', money(liquidity.minCash, liquidity.settings.unit), 'минимум за 30 дней') +
                kpi('Максимальный разрыв', money(liquidity.maxGap, liquidity.settings.unit), 'Gap = max(0; Cash_min − Cash)') +
                kpi('Дней с разрывом', count(liquidity.gapDays), 'требуют решения казначейства') +
                '</div>'
            : '';

        html += liquidityKpis;
        html += '<div class="grid grid-2">' +
            card('Прогноз запаса ликвидности', '30 дней, данные системы', '<div class="chart-box"><canvas id="dash-cash"></canvas></div>' +
                '<div class="chart-legend"><span>Запас денежных средств</span><span>Минимальный запас КУАП</span></div>') +
            card('Риски с наибольшей денежной оценкой', 'EMV, тыс. р.', '<div class="chart-box"><canvas id="dash-risks"></canvas></div>') +
            '</div>';

        const riskRows = topRisks.map((risk) => '<tr>' +
            '<td><strong>' + esc(risk.code) + '</strong></td>' +
            '<td>' + esc(risk.event) + '</td>' +
            '<td>' + esc(risk.ownerRole) + '</td>' +
            '<td class="num">' + count(risk.score) + '</td>' +
            '<td>' + zoneBadge(risk.zone, risk.zoneText) + '</td>' +
            '<td class="num">' + money(risk.emv, 'тыс. р.') + '</td>' +
            '</tr>');

        html += card('Ключевые риски', 'приоритизация по EMV, затем по интегральной оценке R',
            table([{ text: 'Код' }, { text: 'Событие' }, { text: 'Владелец' }, { text: 'R', num: true }, { text: 'Зона' }, { text: 'EMV', num: true }], riskRows));

        if (portfolio) {
            const rows = (portfolio.segments || []).map((segment) => '<tr>' +
                '<td>' + esc(segment.name) + '</td>' +
                '<td>' + esc(segment.currency) + '</td>' +
                '<td class="num">' + count(segment.contracts) + '</td>' +
                '<td class="num">' + money(segment.volume) + '</td>' +
                '<td class="num">' + rate(segment.ratePercent) + '</td>' +
                '</tr>');

            html += card('Портфель по продуктам', 'активные договоры вкладов и текущих счетов',
                table([{ text: 'Продукт' }, { text: 'Валюта' }, { text: 'Договоров', num: true }, { text: 'Объём', num: true }, { text: 'Ставка', num: true }], rows));
        }

        html += '<p class="section-note">Показатели маржи и резерва рассчитаны по модельному портфелю и реестру рисков ' +
            'пояснительной записки: r̄ = 7,725 %, M = 2 220 тыс. р., Rрез = 1 979 тыс. р., П = ' + money(planReserve, 'тыс. р.') + '.</p>';

        $('#view').innerHTML = html;

        requestAnimationFrame(() => {
            if (liquidity) {
                const canvas = document.getElementById('dash-cash');
                if (canvas) {
                    Charts.lineChart(canvas, {
                        labels: liquidity.days.map((day) => shortDate(day.date)),
                        series: [{
                            points: liquidity.days.map((day) => ({ y: day.cash })),
                            fill: 'rgba(29, 78, 216, .10)'
                        }],
                        minimum: liquidity.settings.minimumCash,
                        minimumLabel: 'Минимальный запас'
                    });
                }
            }

            const riskCanvas = document.getElementById('dash-risks');
            if (riskCanvas) {
                Charts.barChart(riskCanvas, {
                    items: topRisks.map((risk) => ({ label: risk.code + ' · ' + risk.event, value: risk.emv })),
                    formatter: (value) => money(value, 'тыс. р.')
                });
            }
        });
    }

    /* ---------------- Ликвидность ---------------- */

    async function renderLiquidity() {
        const forecast = await API.get('/api/liquidity/forecast?days=' + state.liquidityDays + '&scenario=' + state.liquidityScenario);

        const rows = forecast.days.map((day) => {
            const gapClass = day.gap > 0 ? ' style="background:#fef2f2"' : '';
            return '<tr' + gapClass + '>' +
                '<td>' + shortDate(day.date) + '</td>' +
                '<td class="num">' + money(day.maturingPrincipal) + '</td>' +
                '<td class="num">' + money(day.prolongedPrincipal) + '</td>' +
                '<td class="num">' + money(day.earlyWithdrawal) + '</td>' +
                '<td class="num">' + money(day.interestPayable) + '</td>' +
                '<td class="num">' + money(day.inflow) + '</td>' +
                '<td class="num">' + money(day.outflow) + '</td>' +
                '<td class="num"><strong>' + money(day.cash) + '</strong></td>' +
                '<td class="num">' + (day.gap > 0 ? '<span class="badge badge-red">' + money(day.gap) + '</span>' : '0,00') + '</td>' +
                '</tr>';
        });

        const scenarioButtons = '<button class="btn btn-sm ' + (state.liquidityScenario === 'db' ? 'btn-primary' : '') +
            '" data-scenario="db">Данные системы</button>' +
            '<button class="btn btn-sm ' + (state.liquidityScenario === 'model' ? 'btn-primary' : '') +
            '" data-scenario="model">Модель (табл. 1.5)</button>';

        const horizonButtons = [30, 90, 180].map((days) =>
            '<button class="btn btn-sm ' + (state.liquidityDays === days ? 'btn-primary' : '') +
            '" data-days="' + days + '">' + days + ' дн.</button>').join('');

        let html = '<div class="grid grid-kpi">' +
            kpi('Минимальный запас за горизонт', money(forecast.minCash, forecast.settings.unit), 'Cash_t по формуле (1.12)', true) +
            kpi('Максимальный разрыв', money(forecast.maxGap, forecast.settings.unit), 'Gap_t по формуле (1.13)') +
            kpi('Дней с разрывом', count(forecast.gapDays), forecast.firstGapDate ? 'первый разрыв ' + shortDate(forecast.firstGapDate) : 'разрывов нет') +
            kpi('Минимальный запас КУАП', money(forecast.settings.minimumCash, forecast.settings.unit), 'порог для Gap_t') +
            '</div>';

        html += card('Прогноз запаса ликвидности', 'сценарий: ' + (forecast.scenario === 'model' ? 'модельный (тыс. р.)' : 'по данным системы (р.)'),
            '<div class="chart-box"><canvas id="liq-chart"></canvas></div>' +
            '<div class="chart-legend"><span>Запас денежных средств</span><span>Минимальный запас</span></div>',
            scenarioButtons + horizonButtons);

        const settingsForm = '<form id="liq-settings" class="form-grid">' +
            '<label>Доля пролонгации ρ<input id="liq-rho" type="number" step="0.01" min="0" max="1" value="' + forecast.settings.prolongationShare + '"></label>' +
            '<label>Доля изъятия λ<input id="liq-lambda" type="number" step="0.001" min="0" max="1" value="' + forecast.settings.earlyWithdrawalShare + '"></label>' +
            '<label>Минимальный запас, р.<input id="liq-min" type="number" step="1000" min="0" value="' + forecast.settings.minimumCash + '"></label>' +
            '<label>Запас на начало, р.<input id="liq-open" type="number" step="1000" min="0" value="' + forecast.settings.openingCash + '"></label>' +
            '<label>Поступления дня, р.<input id="liq-inflow" type="number" step="1000" min="0" value="' + forecast.settings.expectedDailyInflow + '"></label>' +
            '<button type="submit" class="btn btn-primary">Сохранить допущения</button>' +
            '</form>';

        html += card('Допущения прогноза', 'значения хранятся в справочнике параметров с датой действия', settingsForm);
        html += card('Прогноз по дням', 'отток = (1 − ρ) × погашение + λ × портфель отзывных вкладов + проценты к выплате',
            table([
                { text: 'Дата' }, { text: 'Погашение', num: true }, { text: 'Пролонгация', num: true },
                { text: 'Досрочное изъятие', num: true }, { text: 'Проценты', num: true },
                { text: 'Поступления', num: true }, { text: 'Отток', num: true },
                { text: 'Запас', num: true }, { text: 'Разрыв', num: true }
            ], rows));

        html += '<p class="section-note">Модельный сценарий воспроизводит таблицу 1.5 записки: исходный запас 20 000, минимальный 10 000, ' +
            'ρ = 0,40, λ = 0,002, портфель отзывных вкладов 40 000 тыс. р. Разрыв 2 500 тыс. р. выявляется в день 3.</p>';

        $('#view').innerHTML = html;

        requestAnimationFrame(() => {
            const canvas = document.getElementById('liq-chart');
            if (canvas) {
                Charts.lineChart(canvas, {
                    labels: forecast.days.map((day) => shortDate(day.date)),
                    series: [{
                        points: forecast.days.map((day) => ({ y: day.cash })),
                        fill: 'rgba(14, 165, 233, .12)'
                    }],
                    minimum: forecast.settings.minimumCash,
                    minimumLabel: 'Минимальный запас'
                });
            }
        });

        document.querySelectorAll('[data-scenario]').forEach((button) => {
            button.addEventListener('click', () => {
                state.liquidityScenario = button.getAttribute('data-scenario');
                render();
            });
        });

        document.querySelectorAll('[data-days]').forEach((button) => {
            button.addEventListener('click', () => {
                state.liquidityDays = Number(button.getAttribute('data-days'));
                render();
            });
        });

        const form = document.getElementById('liq-settings');
        if (form) {
            form.addEventListener('submit', async (event) => {
                event.preventDefault();
                try {
                    await API.put('/api/liquidity/settings', {
                        prolongationShare: Number($('#liq-rho').value),
                        earlyWithdrawalShare: Number($('#liq-lambda').value),
                        minimumCash: Number($('#liq-min').value),
                        openingCash: Number($('#liq-open').value),
                        expectedDailyInflow: Number($('#liq-inflow').value)
                    });
                    toast('Допущения прогноза сохранены.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        }
    }

    /* ---------------- Ресурсы и маржа ---------------- */

    async function renderMargin() {
        const report = await API.get('/api/margin?scenario=' + state.marginScenario);
        const total = report.totalVolume || 1;

        const rows = (report.segments || []).map((segment) => '<tr>' +
            '<td>' + esc(segment.name) + '</td>' +
            '<td class="num">' + money(segment.volume, report.unit) + '</td>' +
            '<td class="num">' + ((segment.volume / total) * 100).toFixed(1).replace('.', ',') + ' %</td>' +
            '<td class="num">' + rate(segment.ratePercent) + '</td>' +
            '<td class="num">' + money(segment.interestExpense, report.unit) + '</td>' +
            '</tr>');

        const footer = [{
            text: '<strong>Итого</strong>', colspan: 1
        }, {
            text: '<strong>' + money(report.totalVolume, report.unit) + '</strong>', num: true
        }, {
            text: '100 %', num: true
        }, {
            text: '<strong>' + rate(report.weightedRate) + '</strong>', num: true
        }, {
            text: '<strong>' + money(report.annualInterestExpense, report.unit) + '</strong>', num: true
        }];

        const buttons = '<button class="btn btn-sm ' + (state.marginScenario === 'model' ? 'btn-primary' : '') +
            '" data-margin="model">Модельный портфель</button>' +
            '<button class="btn btn-sm ' + (state.marginScenario === 'db' ? 'btn-primary' : '') +
            '" data-margin="db">Данные системы</button>';

        let html = '<div class="grid grid-kpi">' +
            kpi('Объём портфеля', money(report.totalVolume, report.unit), report.scenario === 'model' ? 'модельный портфель 240 000 тыс. р.' : 'активные договоры', true) +
            kpi('Средневзвешенная ставка r̄', rate(report.weightedRate), 'Σ(S×r) / ΣS (1.14)') +
            kpi('Процентные расходы', money(report.annualInterestExpense, report.unit), 'за год') +
            kpi('Трансфертная ставка', rate(report.transferRate), 'ставка размещения ресурсов') +
            kpi('Операционные расходы', rate(report.costPercent), 'c, % от объёма') +
            kpi('Маржа M', money(report.margin, report.unit), 'V × (rтр − r̄ − c) / 100') +
            kpi('Резерв на риски', money(report.riskReserve, report.unit), 'P80, формула (1.20)') +
            kpi('Маржа с резервом П', money(report.netMargin, report.unit), 'П = M − Rрез') +
            '</div>';

        html += card('Сегменты портфеля', 'объём, ставка и процентные расходы по сегментам',
            table([
                { text: 'Сегмент' }, { text: 'Объём', num: true }, { text: 'Доля', num: true },
                { text: 'Ставка', num: true }, { text: 'Расходы за год', num: true }
            ], rows, footer), buttons);

        html += card('Как читать отчёт', null,
            '<p class="section-note">Модельный сценарий воспроизводит п. 1.3.5 записки: r̄ = 7,725 %, годовые процентные расходы 18 540 тыс. р., ' +
            'M = 2 220 тыс. р. При резерве P80 1 979 тыс. р. маржа П снижается до 241 тыс. р. — это показывает, что ставочная политика ' +
            'без учёта рисков создаёт иллюзию запаса прочности.</p>');

        $('#view').innerHTML = html;

        document.querySelectorAll('[data-margin]').forEach((button) => {
            button.addEventListener('click', () => {
                state.marginScenario = button.getAttribute('data-margin');
                render();
            });
        });
    }

    /* ---------------- Риски и резерв ---------------- */

    async function renderRisks() {
        const summary = await API.get('/api/risks/summary');
        const byCode = {};
        (summary.risks || []).forEach((risk) => { byCode[risk.code] = risk; });

        const matrix = buildMatrix(summary);
        const highRisks = (summary.risks || []).filter((risk) => risk.score >= 10);

        const riskRows = (summary.risks || []).slice().sort((a, b) => b.score - a.score).map((risk) => '<tr>' +
            '<td><strong>' + esc(risk.code) + '</strong></td>' +
            '<td>' + esc(risk.event) + '</td>' +
            '<td>' + esc(risk.category) + '</td>' +
            '<td>' + esc(risk.ownerRole) + '</td>' +
            '<td class="num">' + Number(risk.probability).toFixed(2).replace('.', ',') + '</td>' +
            '<td class="num">' + count(risk.probabilityScore) + ' / ' + count(risk.impactScore) + '</td>' +
            '<td class="num"><strong>' + count(risk.score) + '</strong></td>' +
            '<td>' + zoneBadge(risk.zone, risk.zoneText) + '</td>' +
            '<td class="num">' + money(risk.emv, 'тыс. р.') + '</td>' +
            '<td class="num">' + (risk.residualEmv !== null && risk.residualEmv !== undefined ? money(risk.residualEmv, 'тыс. р.') : '—') + '</td>' +
            '</tr>');

        const riskOptions = (summary.risks || []).map((risk) =>
            '<option value="' + esc(risk.id) + '">' + esc(risk.code + ' · ' + risk.event) + '</option>').join('');

        const assessForm = '<form id="risk-form" class="form-grid">' +
            '<label>Риск<select id="risk-id">' + riskOptions + '</select></label>' +
            '<label>Вероятность p (0–1)<input id="risk-p" type="number" step="0.01" min="0" max="1" value="0.20" required></label>' +
            '<label>Последствия C, тыс. р.<input id="risk-c" type="number" step="10" min="0" value="500" required></label>' +
            '<label>Вероятность после мероприятий p′<input id="risk-p2" type="number" step="0.01" min="0" max="1" value="0.10"></label>' +
            '<label>Комментарий<input id="risk-note" type="text" placeholder="Обоснование оценки"></label>' +
            '<button type="submit" class="btn btn-primary">Сохранить оценку</button>' +
            '</form>';

        let html = '<div class="grid grid-kpi">' +
            kpi('ΣEMV', money(summary.expectedMonetaryValue, 'тыс. р.'), 'формула (1.18)', true) +
            kpi('σ суммарных потерь', money(summary.standardDeviation, 'тыс. р.'), 'при допущении о независимости (1.19)') +
            kpi('Резерв P80', money(summary.reserve, 'тыс. р.'), 'EMV + 0,84σ (1.20)') +
            kpi('Остаточный EMV', money(summary.residualEmv, 'тыс. р.'), 'после мероприятий') +
            kpi('Эффект мероприятий', money(summary.mitigationEffect, 'тыс. р.'), 'снижение EMV') +
            kpi('Риски R ≥ 10', count(highRisks.length), 'требуют эскалации в КУАП') +
            '</div>';

        html += '<div class="grid grid-2">' +
            card('Матрица вероятности и влияния', 'R = pб × Q; зоны: 1–4 низкий, 5–9 умеренный, 10–16 высокий, 17–25 критический', matrix +
                '<div class="matrix-legend">' +
                '<span><i class="dot zone-low"></i>низкий</span>' +
                '<span><i class="dot zone-moderate"></i>умеренный</span>' +
                '<span><i class="dot zone-high"></i>высокий</span>' +
                '<span><i class="dot zone-critical"></i>критический</span>' +
                '</div>') +
            card('Диаграмма «торнадо»', 'анализ чувствительности (таблица 1.18)',
                '<div class="chart-box"><canvas id="tornado"></canvas></div>') +
            '</div>';

        html += card('Реестр рисков', 'R-01 – R-12, оценки и владельцы', table([
            { text: 'Код' }, { text: 'Событие' }, { text: 'Категория' }, { text: 'Владелец' },
            { text: 'p', num: true }, { text: 'pб / Q', num: true }, { text: 'R', num: true },
            { text: 'Зона' }, { text: 'EMV', num: true }, { text: 'Остаточный EMV', num: true }
        ], riskRows));

        html += card('Новая оценка риска', 'версионируемая оценка с датой', assessForm);

        $('#view').innerHTML = html;

        requestAnimationFrame(() => {
            const canvas = document.getElementById('tornado');
            if (canvas) {
                Charts.barChart(canvas, {
                    items: (summary.sensitivity || []).map((item) => ({
                        label: item.factor,
                        value: item.impactValue
                    })),
                    formatter: (value) => money(value, 'тыс. р.')
                });
            }
        });

        const form = document.getElementById('risk-form');
        if (form) {
            form.addEventListener('submit', async (event) => {
                event.preventDefault();
                try {
                    const mitigated = $('#risk-p2').value;
                    await API.post('/api/risks/' + $('#risk-id').value + '/assessments', {
                        probability: Number($('#risk-p').value),
                        moneyImpact: Number($('#risk-c').value),
                        mitigatedProbability: mitigated === '' ? null : Number(mitigated),
                        notes: $('#risk-note').value
                    });
                    toast('Оценка риска сохранена.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        }
    }

    function buildMatrix(summary) {
        const cells = {};
        (summary.matrix || []).forEach((cell) => {
            cells[cell.probabilityScore + ':' + cell.impactScore] = cell;
        });

        let html = '<div class="matrix"><div class="matrix-axis"></div>';
        for (let impact = 1; impact <= 5; impact++) {
            html += '<div class="matrix-axis">Q=' + impact + '</div>';
        }

        for (let probability = 5; probability >= 1; probability--) {
            html += '<div class="matrix-axis">p=' + probability + '</div>';
            for (let impact = 1; impact <= 5; impact++) {
                const cell = cells[probability + ':' + impact];
                const zone = cell ? cell.zone.toLowerCase() : 'low';
                const codes = cell && cell.riskCodes.length ? cell.riskCodes.join(' ') : '—';
                const score = cell ? cell.score : probability * impact;
                html += '<div class="matrix-cell zone-' + zone + '" title="R=' + score + '">' +
                    '<div class="codes">' + esc(codes) + '</div>' +
                    '<div class="score">R=' + score + '</div></div>';
            }
        }

        html += '</div>';
        return html;
    }

    /* ---------------- Продукты и ставки ---------------- */

    async function renderProducts() {
        const products = await API.get('/api/products');
        const roles = (state.user && state.user.roles) || [];
        const canApprove = roles.indexOf('Head') >= 0;

        const cards = products.map((product) => {
            const versions = (product.versions || []).map((version) => {
                const actions = [];
                if (!version.isApproved && canApprove) {
                    actions.push('<button class="btn btn-sm" data-rvsr="' + version.id + '">Проверить РВСР</button>');
                    actions.push('<button class="btn btn-sm btn-primary" data-approve="' + version.id + '">Утвердить</button>');
                }

                return '<tr>' +
                    '<td>v' + version.version + '</td>' +
                    '<td class="num"><strong>' + rate(version.ratePercent) + '</strong></td>' +
                    '<td>' + esc(version.depositTypeText) + '</td>' +
                    '<td class="num">' + (version.termMonths ? version.termMonths + ' мес.' : '—') + '</td>' +
                    '<td>' + (version.isRevocable ? '<span class="badge badge-blue">отзывный</span>' : '<span class="badge badge-dark">безотзывный</span>') + '</td>' +
                    '<td>' + (version.capitalization === 'Monthly' ? 'ежемесячная' : 'нет') + '</td>' +
                    '<td>' + esc(payoutText(version.interestPayout)) + '</td>' +
                    '<td class="num">' + money(version.minAmount) + '</td>' +
                    '<td>' + (version.isApproved ? '<span class="badge badge-green">утверждена</span>' : '<span class="badge badge-yellow">проект</span>') + '</td>' +
                    '<td class="num">' + (version.rvsrLimit ? rate(version.rvsrLimit) : '—') + '</td>' +
                    '<td>' + (actions.length ? '<div class="btn-row">' + actions.join('') + '</div>' : '') + '</td>' +
                    '</tr>';
            });

            return card(product.name, product.code + ' · ' + product.kindText + ' · ' + product.currency +
                ' · балансовый счёт ' + product.balanceAccount,
                table([
                    { text: 'Версия' }, { text: 'Ставка', num: true }, { text: 'Вид вклада' },
                    { text: 'Срок', num: true }, { text: 'Возврат' }, { text: 'Капитализация' },
                    { text: 'Выплата' }, { text: 'Мин. сумма', num: true },
                    { text: 'Статус' }, { text: 'РВСР', num: true }, { text: '' }
                ], versions));
        }).join('');

        const productOptions = products.map((product) =>
            '<option value="' + esc(product.id) + '">' + esc(product.name + ' (' + product.code + ')') + '</option>').join('');

        const versionForm = '<form id="version-form" class="form-grid">' +
            '<label>Продукт<select id="ver-product">' + productOptions + '</select></label>' +
            '<label>Ставка, % годовых<input id="ver-rate" type="number" step="0.01" min="0" value="11.80" required></label>' +
            '<label>Вид вклада<select id="ver-type"><option value="Term">Срочный</option><option value="Demand">До востребования</option><option value="Conditional">Условный</option><option value="None">Не вклад</option></select></label>' +
            '<label>Срок, мес.<input id="ver-term" type="number" min="0" value="12"></label>' +
            '<label>Отзывный<select id="ver-revocable"><option value="true">Да</option><option value="false">Нет</option></select></label>' +
            '<label>Капитализация<select id="ver-cap"><option value="None">Нет</option><option value="Monthly">Ежемесячная</option></select></label>' +
            '<label>Выплата процентов<select id="ver-payout"><option value="AtEnd">В конце срока</option><option value="Monthly">Ежемесячно</option><option value="OnDemand">По требованию</option></select></label>' +
            '<label>Мин. сумма, р.<input id="ver-min" type="number" step="10" min="0" value="100"></label>' +
            '<button type="submit" class="btn btn-primary">Создать версию</button>' +
            '</form>';

        let html = '<p class="section-note">Изменение условий продукта создаёт новую версию: действующие договоры сохраняют свою версию ' +
            '(бизнес-правило 3). Ставка выше РВСР не может быть утверждена без отдельного решения КУАП.</p>';
        html += cards;
        html += card('Новая версия условий', 'утверждается руководителем (членом КУАП)', versionForm);

        $('#view').innerHTML = html;

        document.querySelectorAll('[data-rvsr]').forEach((button) => {
            button.addEventListener('click', async () => {
                try {
                    const check = await API.get('/api/products/versions/' + button.getAttribute('data-rvsr') + '/rvsr');
                    toast(check.message, check.isExceeded ? 'error' : 'ok');
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        });

        document.querySelectorAll('[data-approve]').forEach((button) => {
            button.addEventListener('click', async () => {
                try {
                    const check = await API.get('/api/products/versions/' + button.getAttribute('data-approve') + '/rvsr');
                    const force = check.isExceeded ? confirm(check.message + ' Утвердить с отдельным решением КУАП?') : true;
                    if (!force) return;
                    await API.post('/api/products/versions/' + button.getAttribute('data-approve') + '/approve?force=' + (check.isExceeded ? 'true' : 'false'));
                    toast('Версия условий утверждена.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        });

        const form = document.getElementById('version-form');
        if (form) {
            form.addEventListener('submit', async (event) => {
                event.preventDefault();
                try {
                    await API.post('/api/products/' + $('#ver-product').value + '/versions', {
                        ratePercent: Number($('#ver-rate').value),
                        depositType: $('#ver-type').value,
                        termMonths: Number($('#ver-term').value),
                        isRevocable: $('#ver-revocable').value === 'true',
                        capitalization: $('#ver-cap').value,
                        interestPayout: $('#ver-payout').value,
                        minAmount: Number($('#ver-min').value),
                        rateType: 'Fixed',
                        dayBase: 0
                    });
                    toast('Версия условий создана и ожидает утверждения.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        }
    }

    function payoutText(value) {
        if (value === 'Monthly') return 'ежемесячно';
        if (value === 'OnDemand') return 'по требованию';
        return 'в конце срока';
    }

    /* ---------------- ПОД/ФТ ---------------- */

    async function renderAml() {
        const queue = await API.get('/api/aml/queue');

        const cards = queue.map((item) => {
            const operation = item.operation;
            return '<div class="card">' +
                '<div class="card-head"><div>' +
                '<h2>' + esc(operation.kindText || operation.kind) + ' · ' + money(operation.amount, operation.currency) + '</h2>' +
                '<p>' + esc(operation.clientName || 'Клиент') + ' · счёт ' + esc(operation.accountNumber) +
                ' · документ ' + esc(operation.documentNumber) + '</p>' +
                '</div>' + zoneBadge(item.clientRiskLevel === 'High' ? 'Critical' : 'Moderate', 'риск ' + (item.clientRiskLevel || '—')) + '</div>' +
                '<p class="section-note">' + esc(item.suspicionReason) + '</p>' +
                '<div class="divider"></div>' +
                '<div class="row-between">' +
                '<div class="small muted">Создана: ' + dateTime(operation.createdAt) + ' · ' + esc(operation.statusText) + '</div>' +
                '<div class="btn-row">' +
                '<button class="btn btn-sm btn-primary" data-aml-approve="' + operation.id + '">Одобрить</button>' +
                '<button class="btn btn-sm btn-danger" data-aml-reject="' + operation.id + '">Отказать</button>' +
                '<button class="btn btn-sm" data-aml-open="' + operation.id + '">Карточка операции</button>' +
                '</div></div></div>';
        }).join('');

        let html = '<div class="grid grid-kpi">' +
            kpi('В очереди', count(queue.length), 'приостановленные операции', true) +
            kpi('Порог обязательного контроля', money(state.meta ? state.meta.amlThreshold : 0), 'справочник параметров') +
            '</div>';

        html += queue.length
            ? '<div class="grid" style="gap:14px">' + cards + '</div>'
            : card('Очередь пуста', null, '<div class="empty">Приостановленных операций нет.</div>');

        html += '<p class="section-note">Операция приостанавливается при сумме не ниже порога ПОД/ФТ или при высоком уровне риска клиента. ' +
            'Средства на время рассмотрения не списываются; решение фиксируется в журнале аудита.</p>';

        $('#view').innerHTML = html;

        document.querySelectorAll('[data-aml-approve]').forEach((button) => {
            button.addEventListener('click', async () => {
                const reason = prompt('Обоснование решения об исполнении операции:', 'Признаки ПОД/ФТ не подтверждены');
                if (reason === null) return;
                try {
                    await API.post('/api/aml/' + button.getAttribute('data-aml-approve') + '/decision', { approve: true, reason });
                    toast('Операция одобрена и исполнена.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        });

        document.querySelectorAll('[data-aml-reject]').forEach((button) => {
            button.addEventListener('click', async () => {
                const reason = prompt('Причина отказа:', 'Подтверждены признаки операции, подлежащей особому контролю');
                if (reason === null) return;
                try {
                    await API.post('/api/aml/' + button.getAttribute('data-aml-reject') + '/decision', { approve: false, reason });
                    toast('Операция отклонена.', 'ok');
                    await render();
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        });

        document.querySelectorAll('[data-aml-open]').forEach((button) => {
            button.addEventListener('click', async () => {
                try {
                    const operation = await API.get('/api/operations/' + button.getAttribute('data-aml-open'));
                    alert('Операция ' + operation.documentNumber + '\n' + operation.kindText + ': ' + money(operation.amount, operation.currency) +
                        '\nКлиент: ' + operation.clientName + '\nСчёт: ' + operation.accountNumber +
                        '\nДоступно при проверке: ' + money(operation.checkedAvailable) +
                        '\nСостояние: ' + operation.statusText);
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        });
    }

    /* ---------------- Отчёты ---------------- */

    async function renderReports() {
        const [portfolio, agv, tax] = await Promise.all([
            API.get('/api/reports/portfolio'),
            API.get('/api/reports/agv'),
            API.get('/api/reports/tax')
        ]);

        const reconciliation = await API.get('/api/dayclose/reconciliation').catch(() => null);
        const holds = await API.get('/api/reports/holds').catch(() => []);

        const portfolioRows = (portfolio.segments || []).map((segment) => '<tr>' +
            '<td>' + esc(segment.name) + '</td>' +
            '<td>' + esc(segment.currency) + '</td>' +
            '<td class="num">' + count(segment.contracts) + '</td>' +
            '<td class="num">' + money(segment.volume) + '</td>' +
            '<td class="num">' + rate(segment.ratePercent) + '</td>' +
            '</tr>');

        const agvRows = (agv.rows || []).slice(0, 100).map((row) => '<tr>' +
            '<td>' + esc(row.clientName) + '</td>' +
            '<td>' + esc(row.idNumber) + '</td>' +
            '<td>' + esc(row.contractNumber) + '</td>' +
            '<td>' + esc(row.currency) + '</td>' +
            '<td class="num">' + money(row.amount) + '</td>' +
            '<td class="num">' + money(row.accruedInterest) + '</td>' +
            '<td class="num"><strong>' + money(row.total) + '</strong></td>' +
            '</tr>');

        const taxRows = (tax.rows || []).slice(0, 100).map((row) => '<tr>' +
            '<td>' + esc(row.contractNumber) + '</td>' +
            '<td>' + esc(row.clientName) + '</td>' +
            '<td>' + esc(row.currency) + '</td>' +
            '<td class="num">' + money(row.accrued) + '</td>' +
            '<td class="num">' + money(row.taxableBase) + '</td>' +
            '<td class="num"><strong>' + money(row.tax) + '</strong></td>' +
            '<td>' + (row.isExempt ? '<span class="badge badge-green">освобождено</span>' : '<span class="badge badge-blue">удержано</span>') + '</td>' +
            '</tr>');

        const holdRows = (holds || []).map((hold) => '<tr>' +
            '<td>' + esc(hold.sourceText) + '</td>' +
            '<td>' + esc(hold.basisNumber) + '</td>' +
            '<td>' + esc(hold.basisAuthority) + '</td>' +
            '<td class="num">' + money(hold.amount) + '</td>' +
            '<td>' + (hold.isArrest ? '<span class="badge badge-red">арест</span>' : '<span class="badge badge-yellow">блокировка</span>') + '</td>' +
            '<td>' + shortDate(hold.validFrom) + '</td>' +
            '</tr>');

        let html = '<div class="grid grid-kpi">' +
            kpi('Договоров', count(portfolio.totalContracts), 'активные вклады и счета', true) +
            kpi('Объём портфеля', money(portfolio.totalPrincipal), 'по данным системы') +
            kpi('Начисленные проценты', money(portfolio.totalAccruedInterest), 'к выплате') +
            kpi('Реестр АГВ', money(agv.totalAmount), count(agv.clientsCount) + ' вкладчиков') +
            '</div>';

        html += card('Портфель привлечённых средств', 'на ' + shortDate(portfolio.asOf),
            table([{ text: 'Продукт' }, { text: 'Валюта' }, { text: 'Договоров', num: true }, { text: 'Объём', num: true }, { text: 'Ставка', num: true }], portfolioRows),
            '<button class="btn btn-sm" id="download-csv">Выгрузить CSV</button>');

        if (reconciliation) {
            const badge = reconciliation.isBalanced
                ? '<span class="badge badge-green">расхождений нет</span>'
                : '<span class="badge badge-red">расхождение ' + money(reconciliation.difference) + '</span>';

            html += card('Сверка с главной книгой АБС', 'операционный день ' + shortDate(reconciliation.date),
                '<div class="grid grid-3">' +
                '<div><div class="small muted">Обороты по дебету</div><strong class="mono">' + money(reconciliation.debitTotal) + '</strong></div>' +
                '<div><div class="small muted">Обороты по кредиту</div><strong class="mono">' + money(reconciliation.creditTotal) + '</strong></div>' +
                '<div><div class="small muted">Проводок</div><strong class="mono">' + count(reconciliation.postingCount) + '</strong></div>' +
                '</div>' +
                '<div class="row" style="margin-top:12px">' + badge + '<span class="small muted">' + esc(reconciliation.message) + '</span></div>');
        }

        html += card('Реестр обязательств для АГВ', 'вкладчики – физические лица, гарантированное возмещение 100 %',
            table([{ text: 'Вкладчик' }, { text: 'Идентификационный номер' }, { text: 'Договор' }, { text: 'Валюта' },
                { text: 'Сумма', num: true }, { text: 'Проценты', num: true }, { text: 'Итого', num: true }], agvRows));

        html += card('Сведения об удержанном подоходном налоге', 'период ' + shortDate(tax.from) + ' – ' + shortDate(tax.to),
            table([{ text: 'Договор' }, { text: 'Клиент' }, { text: 'Валюта' },
                { text: 'Начислено', num: true }, { text: 'Налоговая база', num: true }, { text: 'Налог', num: true }, { text: 'Статус' }], taxRows),
            '<span class="pill">Итого налог: ' + money(tax.totalTax) + '</span>');

        html += card('Действующие ограничения', 'аресты, приостановления и блокировки',
            table([{ text: 'Источник' }, { text: 'Документ' }, { text: 'Орган' }, { text: 'Сумма', num: true }, { text: 'Тип' }, { text: 'С даты' }], holdRows));

        $('#view').innerHTML = html;

        const download = document.getElementById('download-csv');
        if (download) {
            download.addEventListener('click', async () => {
                try {
                    const response = await fetch('/api/reports/portfolio.csv', {
                        headers: { Authorization: 'Bearer ' + API.getToken() }
                    });
                    if (!response.ok) throw new Error('Не удалось сформировать файл.');
                    const blob = await response.blob();
                    const link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = 'portfolio.csv';
                    document.body.appendChild(link);
                    link.click();
                    link.remove();
                    toast('Файл сформирован.', 'ok');
                } catch (error) {
                    toast(error.message, 'error');
                }
            });
        }
    }

    /* ---------------- Журнал аудита ---------------- */

    async function renderAudit() {
        const query = state.auditFilter ? '?page=1&pageSize=100&entity=' + encodeURIComponent(state.auditFilter) : '?page=1&pageSize=100';
        const result = await API.get('/api/audit' + query);

        const rows = (result.items || []).map((record) => '<tr>' +
            '<td>' + dateTime(record.at) + '</td>' +
            '<td>' + esc(record.userLogin) + '</td>' +
            '<td><span class="badge badge-blue">' + esc(record.action) + '</span></td>' +
            '<td>' + esc(record.entityName) + '</td>' +
            '<td class="small mono">' + esc((record.newValue || '').slice(0, 90)) + '</td>' +
            '</tr>');

        const form = '<form id="audit-form" class="form-grid">' +
            '<label>Объект (EntityName)<input id="audit-entity" value="' + esc(state.auditFilter) + '" placeholder="Operation, Account, Hold…"></label>' +
            '<button type="submit" class="btn btn-primary">Показать</button>' +
            '</form>';

        let html = '<div class="grid grid-kpi">' +
            kpi('Записей в выборке', count(result.total), 'журнал только на добавление', true) +
            kpi('Срок хранения', '5 лет', 'требование 19') +
            '</div>';

        html += card('Фильтр', null, form);
        html += card('Журнал аудита', 'пользователь, время, объект, прежнее и новое значение',
            table([{ text: 'Время' }, { text: 'Пользователь' }, { text: 'Действие' }, { text: 'Объект' }, { text: 'Данные' }], rows));

        $('#view').innerHTML = html;

        const auditForm = document.getElementById('audit-form');
        if (auditForm) {
            auditForm.addEventListener('submit', (event) => {
                event.preventDefault();
                state.auditFilter = $('#audit-entity').value.trim();
                render();
            });
        }
    }

    /* ---------------- Инициализация ---------------- */

    function bindShell() {
        $('#login-form').addEventListener('submit', handleLogin);

        $('#logout').addEventListener('click', async () => {
            try {
                await API.post('/api/auth/logout');
            } catch (error) {
                /* токен мог истечь — выходим в любом случае */
            }
            API.setToken(null);
            showLogin('Вы вышли из системы.');
        });

        $('#menu-toggle').addEventListener('click', () => {
            $('#sidebar').classList.toggle('open');
        });

        window.addEventListener('hashchange', async () => {
            if (!state.user) return;
            const hash = (location.hash || '').replace('#', '');
            if (!routes[hash]) {
                location.hash = '#' + state.route;
                return;
            }
            state.route = hash;
            $('#sidebar').classList.remove('open');
            await render();
        });

        window.addEventListener('finflow:unauthorized', () => showLogin('Сеанс истёк. Войдите в систему повторно.'));

        let resizeTimer;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => {
                if (state.user) {
                    render();
                }
            }, 350);
        });
    }

    async function init() {
        bindShell();

        if (!API.hasToken()) {
            showLogin();
            return;
        }

        try {
            state.user = await API.get('/api/auth/me');
            await startApp();
        } catch (error) {
            showLogin('Требуется вход в систему.');
        }
    }

    document.addEventListener('DOMContentLoaded', init);
})();
