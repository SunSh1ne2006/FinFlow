(function () {
    'use strict';

    var TOKEN_KEY = 'finflow.client.token';

    var state = {
        token: null,
        profile: null,
        deposits: null,
        offers: null,
        tab: 'deposits',
        loadingDetails: false
    };

    function $(selector) {
        return document.querySelector(selector);
    }

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function money(value, currency) {
        var number = Number(value || 0);
        var formatted = new Intl.NumberFormat('ru-RU', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(number);
        return currency ? formatted + ' ' + currency : formatted;
    }

    function rate(value) {
        return new Intl.NumberFormat('ru-RU', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(Number(value || 0)) + '%';
    }

    function date(value) {
        if (!value) {
            return '—';
        }

        var parsed = new Date(String(value).length === 10 ? value + 'T00:00:00' : value);
        if (isNaN(parsed.getTime())) {
            return '—';
        }

        return parsed.toLocaleDateString('ru-RU');
    }

    function plural(count, one, few, many) {
        var mod10 = count % 10;
        var mod100 = count % 100;
        if (mod10 === 1 && mod100 !== 11) {
            return one;
        }
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) {
            return few;
        }
        return many;
    }

    function toast(message, isError) {
        var element = $('#toast');
        element.textContent = message;
        element.className = 'toast' + (isError ? ' error' : '');
        window.clearTimeout(toast.timer);
        toast.timer = window.setTimeout(function () {
            element.className = 'toast hidden';
        }, 4200);
    }

    async function api(path, options) {
        options = options || {};
        var headers = Object.assign({ 'Content-Type': 'application/json' }, options.headers || {});
        if (state.token) {
            headers.Authorization = 'Bearer ' + state.token;
        }

        var response = await fetch(path, Object.assign({}, options, { headers: headers }));
        var text = await response.text();
        var payload = null;

        if (text) {
            try {
                payload = JSON.parse(text);
            } catch (error) {
                payload = null;
            }
        }

        if (response.status === 401 && path !== '/api/portal/auth/login') {
            signOut(true);
            throw new Error('Сеанс истёк. Войдите снова.');
        }

        if (!response.ok) {
            var message = (payload && (payload.title || payload.detail)) || ('Ошибка сервера (' + response.status + ').');
            throw new Error(message);
        }

        return payload;
    }

    /* ---------------- вход и выход ---------------- */

    function showLogin() {
        state.token = null;
        state.profile = null;
        state.deposits = null;
        state.offers = null;
        localStorage.removeItem(TOKEN_KEY);
        $('#app-screen').classList.add('hidden');
        $('#login-screen').classList.remove('hidden');
        $('#login-error').textContent = '';
        $('#login-submit').disabled = false;
        $('#login-document').focus();
    }

    function showApp() {
        $('#login-screen').classList.add('hidden');
        $('#app-screen').classList.remove('hidden');
        $('#client-name').textContent = state.profile.fullName || '';
        $('#client-document').textContent = 'Паспорт ' + (state.profile.documentNumberMasked || '');
        $('#client-greeting').textContent = state.profile.greeting || 'Здравствуйте!';
        $('#client-meta').textContent =
            (state.profile.identificationStatusText || 'Идентифицирован') +
            (state.profile.phoneMasked ? ' · телефон ' + state.profile.phoneMasked : '') +
            (state.profile.email ? ' · ' + state.profile.email : '');
    }

    async function onLogin(event) {
        event.preventDefault();

        var documentNumber = $('#login-document').value.trim();
        var birthDate = $('#login-birth').value;
        var error = $('#login-error');
        var button = $('#login-submit');

        error.textContent = '';

        if (!documentNumber) {
            error.textContent = 'Укажите серию и номер паспорта.';
            return;
        }

        if (!birthDate) {
            error.textContent = 'Укажите дату рождения.';
            return;
        }

        button.disabled = true;
        try {
            var result = await api('/api/portal/auth/login', {
                method: 'POST',
                body: JSON.stringify({ documentNumber: documentNumber, birthDate: birthDate })
            });

            state.token = result.token;
            state.profile = result.client;
            localStorage.setItem(TOKEN_KEY, result.token);
            showApp();
            await loadCabinet();

            if (result.birthDateRegistered) {
                toast('Дата рождения зафиксирована для входа в личный кабинет.');
            }
        } catch (loginError) {
            error.textContent = loginError.message;
        } finally {
            button.disabled = false;
        }
    }

    async function signOut(silent) {
        if (state.token && !silent) {
            try {
                await api('/api/portal/auth/logout', { method: 'POST' });
            } catch (error) {
                /* сеанс мог уже истечь */
            }
        }

        showLogin();
        if (silent) {
            toast('Сеанс завершён, войдите снова.');
        }
    }

    /* ---------------- данные кабинета ---------------- */

    async function loadCabinet() {
        setTab(state.tab);
        try {
            await Promise.all([loadDeposits(), loadOffers()]);
        } catch (error) {
            toast(error.message, true);
        }
    }

    async function loadDeposits() {
        state.deposits = await api('/api/portal/deposits');
        renderTotals();
        renderDeposits();
        if (state.deposits.asOf) {
            $('#server-date').textContent = 'Данные на ' + date(state.deposits.asOf);
        }
    }

    async function loadOffers() {
        state.offers = await api('/api/portal/offers');
        renderOffers();
    }

    function renderTotals() {
        var container = $('#totals');
        var totals = (state.deposits && state.deposits.totals) || [];

        if (!totals.length) {
            container.innerHTML = '';
            return;
        }

        container.innerHTML = totals.map(function (total) {
            return '' +
                '<div class="total-card">' +
                    '<div class="label">Вклады в ' + escapeHtml(total.currency) + '</div>' +
                    '<div class="value">' + money(total.principal) + ' ' + escapeHtml(total.currency) + '</div>' +
                    '<div class="hint">' +
                        total.depositsCount + ' ' + plural(total.depositsCount, 'вклад', 'вклада', 'вкладов') +
                        ' · начислено ' + money(total.accruedInterest) +
                        ' · ожидаемый доход ' + money(total.expectedIncome) +
                    '</div>' +
                '</div>';
        }).join('');
    }

    function depositCard(item) {
        var progress = Math.max(0, Math.min(100, Number(item.progressPercent || 0)));
        var badge = item.isActive ? 'ok' : 'off';
        var termCaption = item.termMonths > 0 ? item.termMonths + ' мес.' : 'бессрочный';

        return '' +
            '<article class="deposit-card ' + (item.isActive ? '' : 'closed') + '">' +
                '<div class="card-head">' +
                    '<div>' +
                        '<h3 class="card-title">' + escapeHtml(item.productName) + '</h3>' +
                        '<div class="card-sub">Договор ' + escapeHtml(item.contractNumber) +
                            (item.accountNumber ? ' · счёт ' + escapeHtml(item.accountNumber) : '') + '</div>' +
                    '</div>' +
                    '<span class="badge ' + badge + '">' + escapeHtml(item.statusText) + '</span>' +
                '</div>' +
                '<div class="amount"><span class="sum">' + money(item.principal) + '</span>' +
                    '<span class="cur">' + escapeHtml(item.currency) + '</span></div>' +
                '<div class="rate-line">' +
                    '<span>Ставка <b>' + rate(item.ratePercent) + '</b> ' + escapeHtml(item.rateTypeText) + '</span>' +
                    '<span>Срок <b>' + termCaption + '</b></span>' +
                    (item.isActive
                        ? '<span>Осталось <b>' + item.daysLeft + ' ' + plural(item.daysLeft, 'день', 'дня', 'дней') + '</b></span>'
                        : '<span>Вклад завершён</span>') +
                '</div>' +
                '<div>' +
                    '<div class="progress"><span style="width:' + progress + '%"></span></div>' +
                    '<div class="progress-caption"><span>' + date(item.openDate) + '</span><span>' + date(item.endDate) + '</span></div>' +
                '</div>' +
                '<div class="kv">' +
                    '<div><span class="k">Начислено процентов</span><span class="v">' + money(item.accruedInterest) + ' ' + escapeHtml(item.currency) + '</span></div>' +
                    '<div><span class="k">Выплачено процентов</span><span class="v">' + money(item.paidInterest) + ' ' + escapeHtml(item.currency) + '</span></div>' +
                    '<div><span class="k">Ожидаемый доход</span><span class="v">' + money(item.expectedIncome) + ' ' + escapeHtml(item.currency) + '</span></div>' +
                    '<div><span class="k">Следующая выплата</span><span class="v">' + (item.nextInterestDate ? date(item.nextInterestDate) : 'в конце срока') + '</span></div>' +
                '</div>' +
                '<div class="card-actions">' +
                    '<span class="hint">' + escapeHtml(item.capitalizationText) + ' · ' + escapeHtml(item.interestPayoutText) + '</span>' +
                    '<button class="btn btn-ghost" type="button" data-details="' + escapeHtml(item.contractId) + '">Подробнее</button>' +
                '</div>' +
            '</article>';
    }

    function renderDeposits() {
        var container = $('#deposits');
        var deposits = (state.deposits && state.deposits.deposits) || [];

        if (!deposits.length) {
            container.innerHTML = '<div class="empty">У вас пока нет вкладов. Посмотрите условия в разделе «Вклады банка».</div>';
            return;
        }

        container.innerHTML = deposits.map(depositCard).join('');
    }

    function offerCard(offer) {
        var chips = [
            escapeHtml(offer.currency),
            'от ' + money(offer.minAmount) + ' ' + escapeHtml(offer.currency),
            offer.isRevocable ? 'отзывный' : 'безотзывный',
            offer.allowsProlongation ? 'пролонгация' : 'без пролонгации'
        ].map(function (text) {
            return '<span class="chip">' + text + '</span>';
        }).join('');

        var rows = (offer.terms || []).map(function (term) {
            return '' +
                '<tr>' +
                    '<td>' + term.termMonths + ' мес.</td>' +
                    '<td class="rate">' + rate(term.ratePercent) + '</td>' +
                    '<td class="num">' + money(term.incomePer1000) + ' ' + escapeHtml(offer.currency) + '</td>' +
                    '<td>' + escapeHtml(term.interestPayoutText) + '</td>' +
                '</tr>';
        }).join('');

        return '' +
            '<article class="offer-card">' +
                '<div class="offer-head">' +
                    '<div>' +
                        '<h3 class="card-title">' + escapeHtml(offer.name) + '</h3>' +
                        '<div class="card-sub">' + escapeHtml(offer.description || '') + '</div>' +
                    '</div>' +
                    '<div class="offer-rate">' + rate(offer.bestRate) + '<br /><small>максимальная ставка</small></div>' +
                '</div>' +
                '<div class="chips">' + chips + '</div>' +
                '<table class="terms">' +
                    '<thead><tr><th>Срок</th><th>Ставка</th><th class="num">Доход с 1 000 ' + escapeHtml(offer.currency) + '</th><th>Выплата</th></tr></thead>' +
                    '<tbody>' + rows + '</tbody>' +
                '</table>' +
                '<div class="hint muted">' + escapeHtml(offer.capitalizationText) + ' · ' + escapeHtml(offer.note || '') + '</div>' +
            '</article>';
    }

    function renderOffers() {
        var container = $('#offers');
        var data = state.offers;

        if (!data || !data.offers || !data.offers.length) {
            container.innerHTML = '<div class="empty">Условия вкладов временно недоступны.</div>';
            $('#offers-note').textContent = '';
            return;
        }

        $('#offers-note').textContent = data.note || '';
        container.innerHTML = data.offers.map(offerCard).join('');
    }

    /* ---------------- детали вклада ---------------- */

    async function openDetails(contractId) {
        if (state.loadingDetails) {
            return;
        }

        state.loadingDetails = true;
        try {
            var details = await api('/api/portal/deposits/' + contractId);
            renderDetails(details);
            $('#modal').classList.remove('hidden');
        } catch (error) {
            toast(error.message, true);
        } finally {
            state.loadingDetails = false;
        }
    }

    function renderDetails(item) {
        var numbers = (item.accountNumbers || []).map(function (number) {
            return '<span class="chip">' + escapeHtml(number) + '</span>';
        }).join('');

        var accruals = (item.accruals || []).map(function (accrual) {
            return '' +
                '<tr>' +
                    '<td>' + date(accrual.date) + '</td>' +
                    '<td class="num">' + money(accrual.base) + '</td>' +
                    '<td class="num">' + rate(accrual.ratePercent) + '</td>' +
                    '<td class="num">' + money(accrual.amount) + '</td>' +
                    '<td>' + escapeHtml(accrual.status) + '</td>' +
                '</tr>';
        }).join('');

        var accrualBlock = accruals
            ? '<h3>Последние начисления</h3>' +
              '<table class="table"><thead><tr><th>Дата</th><th class="num">База</th><th class="num">Ставка</th><th class="num">Проценты</th><th>Статус</th></tr></thead>' +
              '<tbody>' + accruals + '</tbody></table>'
            : '<h3>Начисления</h3><p class="muted">Начисления появятся после закрытия операционного дня.</p>';

        var early = item.earlyReturn || {};
        var earlyBlock;
        if (early.available) {
            earlyBlock = '' +
                '<div class="callout">' +
                    '<strong>Досрочный возврат сегодня:</strong> выплата ' + money(early.payout) + ' ' + escapeHtml(item.currency) +
                    '. Проценты по ставке до востребования — ' + money(early.demandInterest) + ' ' + escapeHtml(item.currency) +
                    ', удержание переплаты — ' + money(early.withheld) + ' ' + escapeHtml(item.currency) + '.' +
                    '<br />' + escapeHtml(early.note || '') +
                '</div>';
        } else {
            earlyBlock = '<div class="callout"><strong>Досрочный возврат:</strong> ' +
                escapeHtml(early.note || item.earlyReturnNote || 'недоступен') + '</div>';
        }

        $('#modal-body').innerHTML = '' +
            '<h2>' + escapeHtml(item.productName) + '</h2>' +
            '<p class="muted">' + escapeHtml(item.productDescription || '') + '</p>' +
            '<div class="amount" style="margin-top:14px">' +
                '<span class="sum">' + money(item.principal) + '</span>' +
                '<span class="cur">' + escapeHtml(item.currency) + '</span>' +
                '<span class="badge ' + (item.isActive ? 'ok' : 'off') + '" style="margin-left:10px">' + escapeHtml(item.statusText) + '</span>' +
            '</div>' +
            '<div class="detail-grid">' +
                '<div class="detail-tile"><div class="k">Ставка</div><div class="v">' + rate(item.ratePercent) + ' (' + escapeHtml(item.rateTypeText) + ')</div></div>' +
                '<div class="detail-tile"><div class="k">Срок</div><div class="v">' + (item.termMonths || 0) + ' мес. · ' + (item.termDays || 0) + ' дн.</div></div>' +
                '<div class="detail-tile"><div class="k">Открыт</div><div class="v">' + date(item.openDate) + '</div></div>' +
                '<div class="detail-tile"><div class="k">Срок возврата</div><div class="v">' + date(item.endDate) + '</div></div>' +
                '<div class="detail-tile"><div class="k">Осталось</div><div class="v">' + item.daysLeft + ' ' + plural(item.daysLeft, 'день', 'дня', 'дней') + '</div></div>' +
                '<div class="detail-tile"><div class="k">Начислено</div><div class="v">' + money(item.accruedInterest) + ' ' + escapeHtml(item.currency) + '</div></div>' +
                '<div class="detail-tile"><div class="k">Выплачено</div><div class="v">' + money(item.paidInterest) + ' ' + escapeHtml(item.currency) + '</div></div>' +
                '<div class="detail-tile"><div class="k">Ожидаемый доход</div><div class="v">' + money(item.expectedIncome) + ' ' + escapeHtml(item.currency) + '</div></div>' +
                '<div class="detail-tile"><div class="k">К получению в конце срока</div><div class="v">' + money(item.expectedTotal) + ' ' + escapeHtml(item.currency) + '</div></div>' +
            '</div>' +
            '<h3>Реквизиты</h3>' +
            '<div class="chips"><span class="chip">Договор ' + escapeHtml(item.contractNumber) + '</span>' + numbers + '</div>' +
            '<h3>Доходность и налоги</h3>' +
            '<div class="callout">' + escapeHtml(item.capitalizationText) + ' · ' + escapeHtml(item.interestPayoutText) +
                (item.nextInterestDate ? ' · ближайшая выплата ' + date(item.nextInterestDate) : '') +
                '<br />' + escapeHtml(item.taxNote || '') + '</div>' +
            '<h3>Досрочный возврат</h3>' + earlyBlock +
            accrualBlock;
    }

    function closeModal() {
        $('#modal').classList.add('hidden');
    }

    /* ---------------- навигация ---------------- */

    function setTab(tab) {
        state.tab = tab;

        document.querySelectorAll('.tab').forEach(function (button) {
            button.classList.toggle('active', button.getAttribute('data-tab') === tab);
        });

        $('#view-deposits').classList.toggle('hidden', tab !== 'deposits');
        $('#view-offers').classList.toggle('hidden', tab !== 'offers');
    }

    /* ---------------- инициализация ---------------- */

    function bindEvents() {
        $('#login-form').addEventListener('submit', onLogin);
        $('#logout-button').addEventListener('click', function () {
            signOut(false);
        });

        document.querySelectorAll('.tab').forEach(function (button) {
            button.addEventListener('click', function () {
                setTab(button.getAttribute('data-tab'));
            });
        });

        $('#deposits').addEventListener('click', function (event) {
            var button = event.target.closest('[data-details]');
            if (button) {
                openDetails(button.getAttribute('data-details'));
            }
        });

        $('#modal-close').addEventListener('click', closeModal);
        $('#modal').addEventListener('click', function (event) {
            if (event.target === $('#modal')) {
                closeModal();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                closeModal();
            }
        });
    }

    async function boot() {
        bindEvents();

        var token = localStorage.getItem(TOKEN_KEY);
        if (!token) {
            showLogin();
            return;
        }

        state.token = token;
        try {
            state.profile = await api('/api/portal/profile');
            showApp();
            await loadCabinet();
        } catch (error) {
            showLogin();
        }
    }

    boot();
})();
