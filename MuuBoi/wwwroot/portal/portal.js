const Portal = (() => {
    const TOKEN_KEY = 'muuboi.portal.token';
    const EXPIRES_KEY = 'muuboi.portal.expiresAt';

    function toUtc(value) {
        if (!value) return value;
        return /([zZ]|[+-]\d\d:\d\d)$/.test(value) ? value : `${value}Z`;
    }

    function getToken() {
        try {
            const token = sessionStorage.getItem(TOKEN_KEY);
            const expiresAt = sessionStorage.getItem(EXPIRES_KEY);
            if (!token || !expiresAt || new Date(expiresAt) <= new Date()) return null;
            return token;
        } catch {
            return null;
        }
    }

    function saveToken(token, expiresAt) {
        try {
            sessionStorage.setItem(TOKEN_KEY, token);
            sessionStorage.setItem(EXPIRES_KEY, toUtc(expiresAt));
            return true;
        } catch {
            return false;
        }
    }

    function logout() {
        try {
            sessionStorage.removeItem(TOKEN_KEY);
            sessionStorage.removeItem(EXPIRES_KEY);
        } catch {
        }
        window.location.replace('index.html');
    }

    async function get(path) {
        const token = getToken();
        if (!token) {
            logout();
            throw new Error('Session expired.');
        }

        const response = await fetch(path, { headers: { Authorization: `Bearer ${token}` } });
        if (response.status === 401) {
            logout();
            throw new Error('Session expired.');
        }
        if (!response.ok) throw new Error(`Request failed with status ${response.status}.`);

        return response.json();
    }

    return { getToken, saveToken, logout, get, toUtc };
})();
