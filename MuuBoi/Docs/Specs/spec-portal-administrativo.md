# Spec #22: Portal Administrativo da Plataforma

**Módulo:** Portal (operação da plataforma) — autenticação própria, rastreamento de uso e métricas
**Versão:** 1.0
**Data:** 16/Out/2026 (proposta) · 18/Out/2026 (implementação)
**Fonte:** Pedido da usuária (16/Out/2026): portal com login de um único operador para acompanhar quantidade de propriedades, animais e acessos.
**Status:** Implementada na branch `feature/admin-portal` (fases 1–8). Ajustes feitos durante a implementação na §11.
**Depende de:** Spec #21 (validação do JWT a cada requisição, `SessionValidator`; exclusão física da propriedade). Spec #14 (rotas de push e pull `/changes`).

> **Decisões de escopo (respondidas pela usuária em 16/Out/2026):**
> - O portal roda **dentro da API**: páginas estáticas em `wwwroot/portal/` + rotas `/api/portal/*`. Um deploy só no Container App.
> - Login de **um único operador**, com usuário e hash da senha na configuração (`user-secrets` local, variáveis de ambiente no Azure). Não há tabela de operadores.
> - Conta como acesso: **logins**, **última atividade** e **sincronizações** (push e pull).
> - Nível de detalhe: **totais gerais + lista por propriedade** (nome, nº de usuários, nº de animais, último acesso). Sem e-mails e sem dados zootécnicos.

---

## 1. Contexto e objetivo

Hoje não há como saber, sem abrir o banco, quantas propriedades usam o MuuBoi, quantos animais estão cadastrados ou se as pessoas estão de fato usando o app. Esses números servem tanto para o acompanhamento do TCC (adoção, uso do offline) quanto para a operação do servidor, que é fraco (ver capacidade e crescimento).

Três lacunas no código atual:

1. **Não existe operador da plataforma.** O papel `UserRole.Admin` é o **administrador da propriedade**. Ele não pode enxergar outras propriedades, e não deve.
2. **Nenhum acesso é registrado.** O login (`AuthController.Login`) só emite o token. Como o JWT dura 24h e o app trabalha offline, contar só os logins subestima o uso.
3. **Toda consulta é filtrada pela propriedade** (`HasQueryFilter` no `ApplicationDbContext`). Para contar entre propriedades, é preciso desligar esse filtro de forma explícita e restrita.

---

## 2. Decisões

| # | Decisão | Motivo |
|---|---|---|
| P1 | O operador do portal **não é um `ApplicationUser`**. As credenciais ficam em `Portal:Username` e `Portal:PasswordHash`, gerado com `PasswordHasher<T>`. Nunca guardar a senha em texto | Pedido da usuária ("apenas um usuário e uma senha"). Um operador fora do Identity não pertence a nenhuma propriedade, então não tem `property_id` |
| P2 | **Esquema JWT separado** (`"Portal"`), com chave própria (`Portal:JwtKey`), `audience = muuboi-portal` e validade de 2h. A policy `PortalAdmin` exige esse esquema e o claim `role = PortalAdmin` | O esquema atual já rejeita tokens sem `security_stamp` (`OnTokenValidated`). Com chave separada, um token do portal nunca abre a API do app, nem um token do app abre o portal |
| P3 | **Rate limit no login do portal**: 5 tentativas a cada 15 min por IP, usando o `RateLimiter` nativo do .NET 8, com resposta `429` | Credencial única e rota pública: sem limite, a força bruta é trivial |
| P4 | **Exceção declarada à regra de multi-tenancy**: o `PortalRepository` usa `IgnoreQueryFilters()` e **só devolve agregados** (contagens, datas). Ele não recebe `ITenantProvider` e não é usado por nenhum outro service | A regra do projeto diz que todo repositório filtra por `PropertyId`. Concentrar a exceção num lugar só facilita auditar |
| P5 | **Login**: uma linha por login bem-sucedido em `LoginEvents` (`UserId`, `PropertyId`, `OccurredAt`) | Volume baixo (no máximo alguns logins por dia por usuário); permite contar acessos por período |
| P6 | **Última atividade**: coluna `ApplicationUser.LastSeenAt`, atualizada quando o `SessionValidator` **recarrega** o usuário (o cache dele dura 60s) e só se o valor gravado tiver mais de **15 min** | Aproveita a leitura que já acontece em toda requisição autenticada. São no máximo 4 escritas por hora por usuário, aceitável para o servidor fraco |
| P7 | **Sincronizações**: um middleware conta as requisições do app em memória e grava uma vez por minuto em `UsageDaily` (`PropertyId`, `Date`, `Logins`, `Pushes`, `Pulls`). Push = `POST`/`PATCH`/`DELETE` autenticado com `2xx`; pull = `GET .../changes` com `2xx` | Uma escrita por requisição dobraria a carga do banco. Acumular em memória e gravar a cada minuto custa uma escrita por propriedade por minuto. **Risco aceito:** um reinício do container perde até 1 min de contagem |
| P8 | O "dia" de `UsageDaily` e dos gráficos é o dia em **`America/Sao_Paulo`**, e o banco continua em UTC | Um acesso às 22h no Brasil cairia no dia seguinte se o dia fosse contado em UTC |
| P9 | **LGPD**: a exclusão de conta (Spec #21) passa a apagar também as linhas de `LoginEvents` e `UsageDaily` da propriedade. A `privacy.html` ganha um item sobre o registro de acessos. `LoginEvents` com mais de **12 meses** são apagados | Registro de acesso é dado pessoal. Os totais históricos da plataforma "perdem" a propriedade excluída, e isso é esperado |
| P10 | **Uma requisição por tela**: `GET /api/portal/summary` devolve todos os totais e séries de uma vez | [[constrained-environment-api-design]]: poucas requisições, servidor fraco |

---

## 3. Métricas

### 3.1 Cartões (totais)

| Métrica | Definição |
|---|---|
| Propriedades | Total; novas no período (`Property.CreatedAt`) |
| Propriedades ativas | Com pelo menos um usuário com `LastSeenAt` nos últimos 30 dias |
| Usuários | Total, ativos (`IsActive`), revogados, por papel (Admin/Membro) |
| Animais | Ativos no rebanho (`IsActive` e sem saída em aberto); total histórico |
| Usuários ativos | Distintos com `LastSeenAt` em 1, 7 e 30 dias (DAU/WAU/MAU) |
| Logins | No período |
| Sincronizações | Pushes e pulls no período |

### 3.2 Séries (gráficos)

- Logins por dia (últimos 30 dias)
- Pushes e pulls por dia (últimos 30 dias)
- Novas propriedades por mês (últimos 12 meses)

### 3.3 Lista de propriedades

| Coluna | Origem |
|---|---|
| Nome | `Property.Name` |
| Criada em | `Property.CreatedAt` |
| Usuários (ativos/total) | `ApplicationUser` |
| Animais ativos | `Animal` |
| Último acesso | `MAX(LastSeenAt)` dos usuários |
| Logins e sincronizações em 30 dias | `LoginEvents`, `UsageDaily` |

Paginação de 50 linhas, ordenada por último acesso (padrão), nome ou nº de animais. Busca por nome.

---

## 4. Contrato da API

| Rota | Auth | Resposta |
|---|---|---|
| `POST /api/portal/auth/login` `{ username, password }` | Pública, com rate limit | `200 { token, expiresAt }` · `401` credenciais inválidas · `429` |
| `GET /api/portal/summary?days=30` | `PortalAdmin` | Cartões + séries (§3.1, §3.2) |
| `GET /api/portal/properties?page=&pageSize=&sort=&search=` | `PortalAdmin` | Lista paginada (§3.3) |

Erros seguem o `ExceptionMiddleware`. Um token do app nessas rotas recebe `401`, porque é de outro esquema.

---

## 5. Front-end

- `wwwroot/portal/index.html` (login) e `wwwroot/portal/dashboard.html`. HTML + JS puro, com Chart.js por CDN (cdnjs). Sem build e sem projeto novo.
- O token fica em `sessionStorage` (some ao fechar a aba). Um `401` volta para o login.
- Layout responsivo simples: cartões no topo, três gráficos e a tabela de propriedades.
- Sem CORS: o portal é servido pela própria API, na mesma origem.

---

## 6. Arquitetura no servidor

| Camada | Itens novos |
|---|---|
| Domain | `LoginEvent`, `UsageDaily`; `ApplicationUser.LastSeenAt` |
| Application | `IPortalAuthService`/`PortalAuthService`, `IPortalMetricsService`/`PortalMetricsService`, `IPortalRepository`, `IUsageTracker`; DTOs `PortalLoginDto`, `PortalTokenDto`, `PortalSummaryDto`, `PortalPropertyRowDto` |
| Infrastructure | `PortalRepository` (P4), `UsageTracker` (contador em memória, singleton) + `UsageFlushService` (`BackgroundService`, grava a cada 1 min e apaga `LoginEvents` antigos 1x/dia), ajuste no `SessionValidator` (P6) e na exclusão de conta (P9) |
| Api | `PortalAuthController`, `PortalController`, `UsageTrackingMiddleware`; `AuthController.Login` registra o `LoginEvent` |
| `Program.cs` | Esquema `"Portal"`, policy `PortalAdmin`, rate limiter, middleware e hosted service ⚠️ **precisa de confirmação** |

---

## 7. Fases de implementação

| Fase | Entrega | Validação |
|---|---|---|
| 1 — Domínio e banco | Entidades + migração `Portal_UsageTracking` ⚠️ confirmar antes | Migração aplicada; colunas e índices (`IX_LoginEvents_OccurredAt`, `UX_UsageDaily_PropertyId_Date`) |
| 2 — Autenticação do portal | P1–P3, `POST /api/portal/auth/login` | Senha certa → token; senha errada → `401`; 6ª tentativa → `429`; token do portal em `/api/animals` → `401`; token do app em `/api/portal/summary` → `401` |
| 3 — Rastreamento | P5–P8 | Login gera `LoginEvent`; `LastSeenAt` só muda depois de 15 min; push e pull aparecem em `UsageDaily` depois de 1 min; acesso às 22h (BRT) cai no dia certo |
| 4 — Métricas | `PortalRepository` + `PortalMetricsService` + rotas | Números conferidos contra consultas SQL manuais com duas propriedades de teste |
| 5 — Front-end | Páginas do portal | Login, cartões, gráficos e lista funcionando; logout no `401` |
| 6 — LGPD | P9 + `privacy.html` | Excluir conta apaga `LoginEvents`/`UsageDaily` da propriedade; limpeza de 12 meses |
| 7 — Testes | xUnit + Moq: `PortalAuthService`, `PortalMetricsService`, `UsageTracker` (acúmulo e flush) | `dotnet test` verde |
| 8 — Docs | `deploy.md` (variáveis `Portal__*` no Container App, como gerar o hash) | — |

---

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Credencial única vazada | Rate limit, hash forte, token curto, troca da senha só pela variável de ambiente + reinício |
| Contagem perdida num reinício | Aceito (P7): no máximo 1 min |
| Várias réplicas do Container App | Cada réplica tem seu contador. Funciona se o flush **somar** (`UPDATE ... SET Pushes = Pushes + @n`), em vez de sobrescrever |
| Consulta pesada em `summary` | Agregações diretas no SQL com índices; se ficar lento, cache de 5 min no `IMemoryCache` |
| Animais "ativos" com regra divergente da do app | Reaproveitar a mesma definição usada no dashboard da propriedade |

---

## 9. Questões em aberto

| # | Questão | Sugestão |
|---|---|---|
| 1 | Retenção de `LoginEvents` | 12 meses |
| 2 | Definição de propriedade "ativa" | Acesso nos últimos 30 dias |
| 3 | O portal deve ter ações (ex.: desativar propriedade) ou só leitura? | Só leitura nesta versão |
| 4 | Métricas de servidor (tempo de resposta, erros 5xx) | Fora: o Azure já mostra em Metrics/Log stream |

## 10. Fora do escopo

- Mais de um operador, troca de senha pela tela, 2FA.
- Ver ou editar dados zootécnicos de qualquer propriedade.
- Exportação (CSV/PDF).

---

## 11. Implementação (18/Out/2026)

### 11.1 Questões em aberto (§9) — resolvidas

Todas as sugestões foram adotadas: retenção de 12 meses, propriedade ativa = acesso nos últimos 30 dias, portal só de leitura, sem métricas de servidor.

### 11.2 Ajustes em relação à proposta

| # | Proposta | Implementado | Motivo |
|---|---|---|---|
| A1 | Erros só por `NotFound`/`Conflict`/`BusinessRule` | Nova `UnauthorizedException` → `401`, tratada no `ExceptionMiddleware` (CLAUDE.md atualizado) | Login inválido do portal precisa de 401 sem retornar `null` |
| A2 | P1: sem configuração, comportamento não definido | Sem `Portal:JwtKey`, o esquema `"Portal"` usa uma chave aleatória e o login sempre responde `401` | A API sobe no Azure antes das variáveis `Portal__*` existirem |
| A3 | P3: rate limit por IP | IP lido do **último** valor de `X-Forwarded-For` (ingress do Container App), com fallback para `RemoteIpAddress` | Atrás da ingress, `RemoteIpAddress` é o proxy; o limite valeria para todos os clientes juntos |
| A4 | Logins contados em `LoginEvents` e `UsageDaily` | **Todos** os números de acesso do portal (cartões, séries e lista) vêm de `UsageDaily`. `LoginEvents` fica como registro durável, para auditoria e retenção | `UsageDaily` já está no dia de Brasília; nenhuma consulta precisa converter fuso |
| A5 | Pull = ciclo de sincronização | Pull = **requisição** `GET .../changes` com `2xx` (~14 por ciclo completo). Rótulo "Consultas (pull)" no portal | O app não envia identificador de ciclo |
| A6 | `UsageTracker` em Infrastructure | `UsageTracker` em `Application/Services` (sem dependências de infraestrutura, testável como service). `UsageFlushService` continua em Infrastructure | Testes seguem a convenção de testar services |
| A7 | Fase 6: retenção de 12 meses | Implementada na fase 3, dentro do `UsageFlushService` (1x/dia) | O serviço já existia |
| A8 | Fase 6: exclusão de conta apaga `LoginEvents`/`UsageDaily` | Implementado na fase 1 (`AccountRepository.DeletePropertyAsync`) | As FKs são `Restrict`; sem isso a exclusão de conta falharia a partir da fase 3 |
| A9 | — | `UsageRepository` também não recebe `ITenantProvider`, mas só grava com `PropertyId` explícito e não lê dados de propriedades. A leitura entre propriedades (P4) continua restrita ao `PortalRepository` | Flush roda fora de requisição, sem tenant |
| A10 | — | `TimeProvider` registrado como singleton e usado por `UsageTracker`, `UsageService` e `PortalMetricsService` | Permite controlar a data nos testes |
| A11 | — | O flush **soma** (`UPDATE ... SET x = x + @n`) e, se a linha não existe, insere; conflito do índice único ou FK de propriedade excluída → tenta somar de novo e descarta se não houver linha. Em falha de banco, os contadores não gravados voltam para a memória | Várias réplicas e propriedades excluídas entre a contagem e o flush |
| A12 | §5: Chart.js por CDN | Chart.js 4.4.1 do cdnjs com `integrity` (SRI sha512 publicado pelo cdnjs). Sem o CDN, cartões e tabela continuam funcionando | Integridade do script de terceiros |
| A13 | — | `app.UseDefaultFiles()` no `Program.cs`, para `/portal/` abrir o `index.html` | URL curta |
| A14 | — | Gerador do hash da senha versionado em `tools/PortalPasswordHash` (fora de `MuuBoi/`, não entra na imagem) | O `deploy.md` precisa de uma ferramenta permanente |

### 11.3 Contrato final

| Rota | Parâmetros | Resposta |
|---|---|---|
| `POST /api/portal/auth/login` | `{ username, password }` | `200 { token, expiresAt }` · `401` · `429` |
| `GET /api/portal/summary` | `days` (1–365, padrão 30) | `{ days, properties{total,new,active}, users{total,active,revoked,admins,members,activeLastDay,activeLastWeek,activeLastMonth}, animals{active,total}, usage{logins,pushes,pulls}, dailyUsage[{date,logins,pushes,pulls}], newPropertiesByMonth[{year,month,count}] }` |
| `GET /api/portal/properties` | `page` (≥1), `pageSize` (1–100, padrão 50), `sort` (`lastSeen`\|`name`\|`animals`), `search` | `{ items[{id,name,createdAt,activeUsers,totalUsers,activeAnimals,lastSeenAt,logins,pushes,pulls}], page, pageSize, totalCount }` — uso dos últimos 30 dias |

`days` vale para os cartões e para a série diária; novas propriedades por mês cobrem sempre 12 meses. Datas de `dailyUsage` são o dia de Brasília; `createdAt` e `lastSeenAt` são UTC.

### 11.4 Validação

- Fases 2, 4 e 5 validadas ponta a ponta com a API local e duas propriedades de teste: login/`401`/`429`, isolamento entre os esquemas JWT, contagens conferidas contra SQL direto, exclusão de conta apagando `LoginEvents`/`UsageDaily`, flush descartando contagens de propriedades excluídas, páginas estáticas e script do dashboard executado contra a API.
- Fase 7: 21 testes novos (`PortalAuthServiceTests`, `UsageTrackerTests`, `UsageServiceTests`, `PortalMetricsServiceTests`); suíte com 313 testes verdes.
- Configuração no Azure, troca de senha e exclusão manual de membro: `Docs/deploy.md`, seção "Portal administrativo".
