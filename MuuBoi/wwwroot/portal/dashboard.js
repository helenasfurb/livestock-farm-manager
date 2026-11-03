if (!Portal.getToken()) Portal.logout();

const PAGE_SIZE = 50;
const MONTHS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const GREEN = '#2e7d32';
const BLUE = '#1e6fd9';

const numberFormat = new Intl.NumberFormat('pt-BR');
const dateFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'America/Sao_Paulo' });
const dateTimeFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' });

const state = { page: 1, sort: 'lastSeen', search: '' };
const charts = {};
let searchTimer;

document.getElementById('logout').addEventListener('click', Portal.logout);
document.getElementById('period').addEventListener('change', loadSummary);
document.getElementById('sort').addEventListener('change', (event) => {
    state.sort = event.target.value;
    state.page = 1;
    loadProperties();
});
document.getElementById('search').addEventListener('input', (event) => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => {
        state.search = event.target.value.trim();
        state.page = 1;
        loadProperties();
    }, 400);
});
document.getElementById('previous').addEventListener('click', () => {
    state.page--;
    loadProperties();
});
document.getElementById('next').addEventListener('click', () => {
    state.page++;
    loadProperties();
});

loadSummary();
loadProperties();

async function loadSummary() {
    const days = document.getElementById('period').value;
    try {
        const summary = await Portal.get(`/api/portal/summary?days=${days}`);
        renderStats(summary);
        renderCharts(summary);
    } catch {
        showError('Não foi possível carregar os indicadores.');
    }
}

async function loadProperties() {
    const params = new URLSearchParams({ page: state.page, pageSize: PAGE_SIZE, sort: state.sort });
    if (state.search) params.set('search', state.search);

    try {
        renderProperties(await Portal.get(`/api/portal/properties?${params}`));
    } catch {
        showError('Não foi possível carregar as propriedades.');
    }
}

function renderStats(summary) {
    const { properties, users, animals, usage } = summary;
    const items = [
        ['Propriedades', properties.total, `${properties.new} novas no período · ${properties.active} ativas em 30 dias`],
        ['Usuários', users.total, `${users.active} ativos · ${users.revoked} revogados · ${users.admins} admins · ${users.members} membros`],
        ['Usuários ativos (30 dias)', users.activeLastMonth, `${users.activeLastDay} em 24h · ${users.activeLastWeek} em 7 dias`],
        ['Animais no rebanho', animals.active, `${numberFormat.format(animals.total)} cadastrados no total`],
        ['Logins', usage.logins, `nos últimos ${summary.days} dias`],
        ['Sincronizações', usage.pushes + usage.pulls, `${numberFormat.format(usage.pushes)} envios · ${numberFormat.format(usage.pulls)} consultas`]
    ];

    document.getElementById('stats').replaceChildren(...items.map(([label, value, detail]) => {
        const card = createElement('div', 'card');
        card.append(
            createElement('div', 'stat-label', label),
            createElement('div', 'stat-value', numberFormat.format(value)),
            createElement('div', 'stat-detail', detail));
        return card;
    }));
}

function renderCharts(summary) {
    const dayLabels = summary.dailyUsage.map((day) => formatDay(day.date));

    drawChart('chart-logins', 'bar', dayLabels, [
        { label: 'Logins', data: summary.dailyUsage.map((day) => day.logins), backgroundColor: GREEN }
    ]);

    drawChart('chart-sync', 'line', dayLabels, [
        { label: 'Envios (push)', data: summary.dailyUsage.map((day) => day.pushes), borderColor: GREEN, backgroundColor: GREEN, tension: 0.3 },
        { label: 'Consultas (pull)', data: summary.dailyUsage.map((day) => day.pulls), borderColor: BLUE, backgroundColor: BLUE, tension: 0.3 }
    ]);

    drawChart('chart-properties', 'bar',
        summary.newPropertiesByMonth.map((month) => `${MONTHS[month.month - 1]}/${String(month.year).slice(2)}`),
        [{ label: 'Novas propriedades', data: summary.newPropertiesByMonth.map((month) => month.count), backgroundColor: GREEN }]);
}

function drawChart(id, type, labels, datasets) {
    if (typeof Chart === 'undefined') return;

    charts[id]?.destroy();
    charts[id] = new Chart(document.getElementById(id), {
        type,
        data: { labels, datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: datasets.length > 1, position: 'bottom' } },
            scales: {
                y: { beginAtZero: true, ticks: { precision: 0 } },
                x: { grid: { display: false } }
            }
        }
    });
}

function renderProperties(page) {
    const body = document.getElementById('rows');

    if (page.items.length === 0) {
        const cell = createElement('td', 'empty', state.search ? 'Nenhuma propriedade encontrada.' : 'Nenhuma propriedade cadastrada.');
        cell.colSpan = 8;
        const row = document.createElement('tr');
        row.append(cell);
        body.replaceChildren(row);
    } else {
        body.replaceChildren(...page.items.map((item) => {
            const row = document.createElement('tr');
            row.append(
                createElement('td', '', item.name),
                createElement('td', '', dateFormat.format(new Date(Portal.toUtc(item.createdAt)))),
                createElement('td', 'num', `${item.activeUsers}/${item.totalUsers}`),
                createElement('td', 'num', numberFormat.format(item.activeAnimals)),
                createElement('td', '', item.lastSeenAt ? dateTimeFormat.format(new Date(Portal.toUtc(item.lastSeenAt))) : 'Sem registro'),
                createElement('td', 'num', numberFormat.format(item.logins)),
                createElement('td', 'num', numberFormat.format(item.pushes)),
                createElement('td', 'num', numberFormat.format(item.pulls)));
            return row;
        }));
    }

    const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
    document.getElementById('page-info').textContent =
        `Página ${page.page} de ${totalPages} · ${numberFormat.format(page.totalCount)} propriedades`;
    document.getElementById('previous').disabled = page.page <= 1;
    document.getElementById('next').disabled = page.page >= totalPages;
}

function formatDay(value) {
    const [, month, day] = value.slice(0, 10).split('-');
    return `${day}/${month}`;
}

function createElement(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

function showError(message) {
    const errorBox = document.getElementById('error');
    errorBox.textContent = message;
    errorBox.hidden = false;
}
