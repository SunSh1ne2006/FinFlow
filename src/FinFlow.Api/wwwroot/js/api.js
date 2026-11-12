/* Клиент REST API: токен доступа, обработка ошибок, повторный вход при истечении сессии. */
const API = (() => {
    const storageKey = 'finflow.token';
    let token = localStorage.getItem(storageKey);

    function setToken(value) {
        token = value || null;
        if (token) {
            localStorage.setItem(storageKey, token);
        } else {
            localStorage.removeItem(storageKey);
        }
    }

    async function request(method, url, body) {
        const headers = { Accept: 'application/json' };
        if (body !== undefined) {
            headers['Content-Type'] = 'application/json';
        }
        if (token) {
            headers.Authorization = 'Bearer ' + token;
        }

        const response = await fetch(url, {
            method,
            headers,
            body: body !== undefined ? JSON.stringify(body) : undefined
        });

        if (response.status === 401) {
            setToken(null);
            window.dispatchEvent(new CustomEvent('finflow:unauthorized'));
            throw new Error('Сеанс завершён. Войдите в систему повторно.');
        }

        const text = await response.text();
        let data = null;
        if (text) {
            try {
                data = JSON.parse(text);
            } catch (error) {
                data = text;
            }
        }

        if (!response.ok) {
            const message = (data && (data.title || data.detail || data.message)) || ('Ошибка ' + response.status);
            throw new Error(message);
        }

        return data;
    }

    return {
        get: (url) => request('GET', url),
        post: (url, body) => request('POST', url, body === undefined ? {} : body),
        put: (url, body) => request('PUT', url, body === undefined ? {} : body),
        setToken,
        hasToken: () => Boolean(token),
        getToken: () => token
    };
})();
