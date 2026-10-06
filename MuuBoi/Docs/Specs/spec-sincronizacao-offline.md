# Spec #14: Sincronização Offline — Especificação e Plano de Implementação

**Módulo:** Infraestrutura / Sincronização — transversal a toda a aplicação
**Versão:** 1.1
**Data:** 05/Out/2026
**Status:** 🟢 **Aprovada.** Implementada para **Produção de Leite** (`MilkProduction`); **Vacinas** (`Vaccine`) com plano aprovado (Spec #14.1); demais entidades seguem a receita da §7.4, cada uma numa spec filha (§8).
**Abrangência:** contrato de sincronização do servidor/API (identidade, versionamento, tombstones, idempotência, tenant) **e** o contrato que o app offline precisa seguir para consumi-lo.
**Relaciona-se com:** Spec 11.1 (Produção de Leite, §8) · Spec #13 (Cadastro Retroativo de Gestação, §6) · todos os specs de entidade · `Docs/catalogo-erros-api.md`

> **Documento único.** Consolida a antiga Spec #14 v0.1, a análise de alternativas (`Docs/Plan/analise-sincronizacao-offline.md`) e o plano de implementação da produção de leite (`Docs/Plan/plano-offline-producao-leite.md`). Os dois documentos de `Docs/Plan` foram incorporados aqui e removidos.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 05/Set/2026 | Rascunho para discussão. Decisões D1–D6; push **em lote** (`POST /api/sync/changes`) e pull global (`GET /api/sync/changes`). |
| 1.0 | 03/Out/2026 | Decisões revisadas (A1–A8): **sem rota em lote** — o app envia pelas **rotas REST normais**, em ordem; pull **por recurso** (`GET /api/<recurso>/changes`). Implementação completa da produção de leite (Fases 1–7), com helpers reutilizáveis e 19 testes. |
| 1.1 | 05/Out/2026 | A implementação de cada nova entidade passa a ficar em uma **spec filha** (`spec-sincronizacao-offline-14.N-<entidade>.md`), para este documento não crescer a cada entidade. Primeira: **Spec #14.1 — catálogo de vacinas**. |

---

## Sumário

**Parte I — Especificação**
1. Contexto e objetivo
2. Análise de alternativas
3. Decisões
4. Casos de uso motivadores
5. Contrato da API
6. Contrato do cliente (app)
7. Arquitetura no servidor
8. Entidades no escopo

**Parte II — Implementação: Produção de Leite**
9. Por que começar por `MilkProduction`
10. Fases de implementação e validação
11. Arquivos impactados

**Parte III — Riscos, pendências e referências**
12. Riscos e pontos de atenção
13. Questões em aberto
14. Fora do escopo
15. Fontes

**Specs filhas (implementação por entidade)** — ver §8
- Spec #14.1 — Catálogo de Vacinas (`spec-sincronizacao-offline-14.1-vacinas.md`)

---

# Parte I — Especificação

## 1. Contexto e objetivo

Até a versão 1.0, a API era **online-only** e **server-authoritative**: toda escrita passava pelo servidor, que gerava a identidade (`BaseEntity.Id` é `int` identity) e era a única fonte da verdade. O escopo do TCC prevê que o app **funcione offline** e **sincronize** depois, e que **mesmo online** a conexão e o servidor sejam **instáveis** (servidor fraco e internet ruim — ver memória de ambiente restrito).

Isso quebra três premissas:

1. **Identidade** — um registro criado offline não pode esperar o `int` do servidor para existir nem para ser referenciado por dependentes locais.
2. **Fonte única da verdade** — duas origens (app offline + web) podem editar o mesmo dado; é preciso **reconciliar**.
3. **Entrega confiável** — sob instabilidade, requisições são reenviadas; sem **idempotência** geram duplicatas.

Esta spec define o **contrato de servidor** que suporta a sincronização e o que o app precisa fazer para consumi-lo, e documenta a primeira implementação (produção de leite).

---

## 2. Análise de alternativas

### 2.1 Padrão base: offline-first com "lazy writes"

O guia oficial do Android ([Build an offline-first app](https://developer.android.com/topic/architecture/data-layer/offline-first)) define três estratégias de escrita:

| Estratégia | Como funciona | Quando usar |
|---|---|---|
| Online-only | Tenta a rede; só grava local se der certo | Transferência bancária |
| Queued writes | Enfileira e drena quando houver rede | Logs, analytics |
| **Lazy writes** | **Grava no banco local primeiro e enfileira o envio** | **Dado crítico do usuário — caso do MuuBoi** |

- O app sempre lê do banco local (Room), que é a fonte da verdade da tela.
- A sincronização roda em segundo plano com **WorkManager**: respeita bateria/Doze, sobrevive ao fechamento do app e só executa com rede.
- `Result.retry()` reagenda com **backoff exponencial**.
- Para conflito, o próprio guia recomenda **last-write-wins por timestamp**.

### 2.2 Rota de sincronização em lote × rotas REST normais

| | **A. Rotas REST normais** (escolhida) | B. Rota dedicada em lote |
|---|---|---|
| Como | Cada entidade mantém `POST`/`PATCH`/`DELETE`; o app envia **uma operação por requisição** | `POST /api/sync/changes` com um lote de mudanças |
| Quem usa | [Datasync Community Toolkit](https://communitytoolkit.github.io/Datasync/) (Microsoft) — push operação a operação; Android "lazy writes" | [WatermelonDB](https://watermelondb.dev/docs/Sync/Backend) (`pushChanges`), CouchDB/PouchDB, RxDB, PowerSync |
| Prós | Reaproveita controllers, validações e DTOs; erro isolado por operação; fácil de depurar no Swagger | Poucos round-trips; servidor ordena dependências; idempotência centralizada |
| Contras | Mais round-trips; ordem das dependências fica com o app | Contrato novo; falha parcial a definir; risco de o `SyncService` **duplicar regras de negócio** |

**Escolha:** A, com o app enviando **em ordem, uma por vez** (decisões A1–A2). O envio sequencial resolve a ordem das dependências (animal → gestação → parto) e permite a resolução de `Id` no app (A5). Se o volume de requisições pesar, uma rota em lote pode ser criada depois só para os registros mais frequentes (pesagem, produção de leite), sem mudar o resto.

**Pull:** mesmo o Datasync, que faz push por rota, tem um mecanismo de delta separado. As rotas de listagem não servem para pull (§5.5), então cada recurso ganha uma rota `GET /changes`.

### 2.3 Idempotência — duas camadas possíveis

| Camada | Como | Decisão |
|---|---|---|
| **Natural, pela identidade (`SyncId`)** | O app gera o UUID do registro; o servidor reconhece o reenvio pelo próprio dado. É regra do protocolo do WatermelonDB: *"se chegar um registro novo com ID que já existe, o servidor DEVE atualizar e NÃO DEVE retornar erro"*. | ✅ **Adotada** — permanente, sem tabela extra |
| Chave por requisição (`Idempotency-Key`) | Padrão [Stripe](https://docs.stripe.com/api/idempotent_requests) / [IETF](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07): o servidor guarda a resposta de cada chave e a devolve no reenvio | ❌ **Não necessária** no MuuBoi (§5.6) |

Observação de offline: a Stripe guarda as chaves por **24h**, mas um app de fazenda pode ficar dias sem sinal. Por isso a proteção principal tem de ser permanente — o `SyncId`.

---

## 3. Decisões

### 3.1 Decisões da v0.1 (situação na v1.0)

| # | Decisão | Situação |
|---|---|---|
| D1 | **Identidade: `SyncId` (`Guid`) secundário**, gerado no cliente; PK `int` interna e FKs permanecem. Migração aditiva. | ✅ Mantida e implementada |
| D2 | **Conflito: last-write-wins (LWW) por `UpdatedAt`.** | ✅ Mantida. Origem do `UpdatedAt` definida: **momento da edição no cliente**, normalizado para UTC e limitado a "agora" (§5.3) |
| D3 | **Escopo: somente servidor/API.** | ⚠️ Ampliada: o contrato do cliente passa a ser documentado aqui (§6), embora a implementação do app continue fora |
| D4 | **Cursor por `rowversion`**, distinto de `UpdatedAt`. | ✅ Mantida, com `MIN_ACTIVE_ROWVERSION()` (§5.5) |
| D5 | **Isolamento por tenant** no repositório (`PropertyId`). | ✅ Mantida — o `HasQueryFilter` vale também no pull com `FromSqlRaw` (validado) |
| D6 | **Soft delete (`IsActive = false`) = tombstone.** | ✅ Mantida. Inativação **não** passa pelo LWW: sempre vence (§5.4) |
| — | Push em lote (`POST /api/sync/changes`) e pull global (`GET /api/sync/changes`) | ❌ **Substituídas** por A1, A2 e A8 |

### 3.2 Decisões da v1.0

| # | Decisão |
|---|---|
| A1 | **Não há rota de sincronização em lote.** O app envia as operações pelas **rotas REST normais** de cada entidade (`POST`, `PATCH`, `DELETE`), que passam a suportar reenvio. |
| A2 | **O cliente ordena e envia uma requisição por vez**, na ordem em que as operações foram feitas. Um único worker esvazia a fila. |
| A3 | **Falha para a fila.** Erro temporário (timeout, sem rede, `5xx`, `401`) → para e tenta de novo depois. Erro definitivo (`400`, `404`, `409`, `422`) → para, marca o item como `Failed` e avisa o usuário. |
| A4 | **Identidade gerada no cliente:** `SyncId` (UUID, preferencialmente v7), criado **quando o usuário salva** e nunca regenerado. |
| A5 | **Referências entre entidades usam o `Id` do servidor**, resolvido pelo app no momento do envio (o `201` devolve o `Id`). As rotas continuam recebendo `int`. |
| A6 | **Reenvio é sucesso, não erro.** Repetir uma operação já aplicada devolve `2xx` com o estado atual do recurso. |
| A7 | **Conflito: last-write-wins** pelo momento da edição informado pelo cliente (D2). |
| A8 | **Pull incremental por `rowversion`** (D4), com rota de mudanças **por recurso**: `GET /api/<recurso>/changes`. |

### 3.3 Por que A6 ("reenvio é sucesso")

O app **não consegue distinguir** "minha requisição falhou" de "minha requisição deu certo, mas a resposta se perdeu". Quando a primeira tentativa foi gravada, a operação **deu certo**; responder com erro seria informar algo falso ao app. Se o reenvio retornasse `409`:

1. **A fila travaria sem motivo** — `409` é erro definitivo; o item viraria `Failed` e o usuário seria avisado de um erro que não aconteceu.
2. **O app ficaria sem o `Id`** — pela A5, ele precisa do `Id` para enviar os itens dependentes; uma resposta de erro não traz o recurso.
3. **Ambiguidade** — o `409` do MuuBoi também significa conflitos reais (ex.: outra gestação ativa). O app não conseguiria separar "já recebi" de "conflito de verdade".

Com `2xx` + estado atual, a lógica do app tem **uma regra só**: remove da fila, grava o retorno e segue. É o comportamento da Stripe (mesma chave → mesma resposta), do WatermelonDB e o objetivo declarado do draft IETF `Idempotency-Key` (*"fault-tolerant"*).

- A RFC 9110 define idempotência pelo **efeito no servidor**, não pelo status; responder `2xx` é uma **escolha de projeto** para simplificar o cliente.
- **Conflitos reais continuam sendo erro.** A regra só vale quando **a mesma operação** já foi aplicada (mesmo `SyncId` na criação; mesmo estado pedido numa mudança de estado). Um animal com o mesmo brinco e **outro** `SyncId` continua `409`.
- **Efeito colateral aceito:** numa mudança de estado, "já está no estado pedido" pode ter sido causado por outra pessoa (ex.: a web secou a lactação em 08/09 e o celular pede secagem em 10/09). O servidor responde `200` com o estado real e o app atualiza o local com ele.
- **Exceção ao `CLAUDE.md`**, que manda usar `ConflictException` quando a entidade "já está no estado pedido" — vale só para rotas com suporte offline.

### 3.4 Outras decisões registradas durante a implementação

| Decisão | Seção |
|---|---|
| `updatedAt` do cliente **sempre em UTC com `Z`**; servidor normaliza outros formatos e limita a "agora" | §5.3 |
| `DbUpdateConcurrencyException` (edições simultâneas) **sem tratamento** — vira `500` e o app tenta de novo | §5.3 |
| `DELETE` **não** passa pelo LWW — a inativação sempre vence | §5.4 |
| Pull: filtro em `FromSqlRaw` (opção B), `RowVersion` mantido como `byte[]` — sem migração extra | §5.5 |
| **Sem chave de idempotência por requisição** — o `SyncId` basta | §5.6 |
| Resolução de `Id` no app (Solução A); referência por `SyncId` descartada | §5.8 |
| Helpers genéricos reutilizáveis para todas as entidades | §7.2 |

---

## 4. Casos de uso motivadores

| # | Cenário | Como é resolvido |
|---|---|---|
| CU-1 | Produtor cadastra animais/eventos **offline** no curral e sincroniza ao voltar ao sinal. | `SyncId` gerado no app (A4) + criação idempotente (§5.2) |
| CU-2 | Cadastro retroativo de gestação offline e, na sequência, **parto** da mesma gestação, ainda offline. | Envio sequencial + resolução do `Id` no app (A2, A5, §5.8) |
| CU-3 | Web e app editam o **mesmo animal** enquanto o app está offline. | Last-write-wins pelo momento da edição (§5.3) |
| CU-4 | `POST` reenviado porque o `201` não chegou (link instável), mesmo online. | Reenvio é sucesso (A6, §5.7) |
| CU-5 | App fica dias offline e depois puxa tudo que mudou na web. | Pull incremental por `rowversion` (§5.5) |

---

## 5. Contrato da API

### 5.1 Identidade: `SyncId`

"UUID" é o **tipo do valor**; "`SyncId`" é o **nome do campo** — como `int` e `Id`.

| Onde | Nome / tipo |
|---|---|
| Padrão (RFC 9562) | **UUID** |
| C# / .NET | `Guid` |
| SQL Server | `uniqueidentifier` |
| Kotlin / Room | `Uuid` / `UUID` |
| Contrato da API | Campo **`syncId`** |

Todo registro sincronizável tem **dois identificadores**:

- **`Id` (`int`)** — identidade interna, gerada pelo servidor; usada nas rotas e nas FKs.
- **`SyncId` (UUID)** — gerado pelo celular **quando o usuário salva**; existe antes de o registro chegar ao servidor e permite reconhecer o reenvio. O mesmo valor existe no celular e no servidor para sempre.

**Versão do UUID:**

- **v7** (RFC 9562, 2024): 48 bits de timestamp em ms + 74 bits aleatórios; ordenável por tempo. Kotlin: `Uuid.generateV7()` (stdlib, experimental — `@OptIn(ExperimentalUuidApi::class)`).
- **v4**: totalmente aleatório. Kotlin: `UUID.randomUUID()`.
- Recomendação: **v7 se disponível, senão v4** — ambos servem para idempotência. No Room (SQLite), o v7 mantém fila e índices locais em ordem cronológica.
- **No SQL Server o v7 não traz ganho de índice:** o `uniqueidentifier` compara primeiro os bytes 10–15 e por último os 0–3, onde fica o timestamp. Pesa pouco aqui: o `SyncId` fica num **índice secundário** (a PK continua `int`).
- `Guid.CreateVersion7()` só existe no **.NET 9+** (o MuuBoi está em net8.0); não faz falta, pois quem gera é o celular e o backfill usa `NEWID()`.
- **O servidor aceita qualquer versão de UUID.** Só recusa `Guid.Empty` (`400`).

### 5.2 Criação (`POST`) — idempotente

- O DTO de criação aceita `syncId` **opcional**: o app sempre envia; a web pode omitir (o servidor gera).
- O service checa o `SyncId` **antes de qualquer regra de negócio**. Se já existe → devolve o registro existente com o **mesmo corpo do `201`** (incluindo o `Id`), **sem alterar** — mesmo que o payload seja diferente (alterações chegam pelo `PATCH` que vem depois na fila). Registro inativo é devolvido como está, sem reativar.
- **Corrida** (original e reenvio chegando ao mesmo tempo): o índice único `UX_{Tabela}_SyncId` barra o segundo `INSERT`; a violação é capturada e o registro existente é devolvido.
- A resposta inclui `syncId`, para o app ligar `SyncId` ↔ `Id`.

### 5.3 Edição (`PATCH`) — last-write-wins

O DTO de edição aceita `updatedAt` **opcional**: o momento em que o usuário **editou** no celular. A web omite e o servidor usa "agora".

**O problema:** "último" segundo qual relógio? Usar a **hora de chegada** dá o vencedor errado:

```
Segunda 08:00  Celular (offline) corrige o volume para 120 L
Terça   10:00  Web corrige o mesmo lançamento para 125 L   ← edição mais recente
Sexta   18:00  Celular sincroniza a correção de segunda
```

Por hora de chegada, o celular venceria e a correção de terça se perderia. Por isso o servidor compara o **momento da edição**.

**Regras (helper `SyncTimestampResolver`):**

| `updatedAt` recebido | `DateTimeKind` | Ação |
|---|---|---|
| `...Z` | `Utc` | Usa como está |
| `...-03:00` | `Local` (o .NET converte para o fuso do servidor) | `ToUniversalTime()` |
| Sem fuso | `Unspecified` | Assume UTC (contrato) |
| Depois de "agora" | — | Limita a "agora" |
| Ausente (web) | — | "Agora" |

- **Comparação:** se `editedAt < (UpdatedAt ?? CreatedAt)` → **não aplica** e devolve `200` com a versão do servidor; o app sobrescreve o local com ela. Caso contrário aplica e grava `UpdatedAt = editedAt`.
- **Empate aplica** (`<`, não `<=`): é o reenvio da mesma edição. A precisão de `datetime2` (100 ns) é a mesma do .NET, então o valor volta idêntico.
- **Por que normalizar o fuso:** o `DateTime` do .NET compara só os *ticks*, ignorando o `Kind`. Um `14:30-03:00` seria lido como `14:30` local e comparado com `17:30` UTC do banco — edições até 3 horas mais novas seriam descartadas.
- **Por que limitar a "agora" e não recusar:** um celular com relógio adiantado venceria todas as edições futuras; recusar com `400` (definitivo) travaria a fila.
- **Contrato do app:** enviar **sempre em UTC, com `Z`**.
- **Concorrência:** com `IsRowVersion()`, dois `UPDATE` simultâneos na mesma linha geram `DbUpdateConcurrencyException` → `500`. **Sem tratamento, de propósito:** o app tenta de novo, o servidor relê, refaz o LWW e aplica ou descarta. Tratar no repositório foi descartado (ele não sabe refazer o LWW); converter em `409` também (seria definitivo para o app).

**Riscos residuais:**

| Situação | Consequência |
|---|---|
| Relógio do celular **atrasado** | As edições dele perdem sempre — o servidor não distingue de uma edição offline legítima. **Risco aceito** (D2). |
| Relógio adiantado | Mitigado (limite a "agora"). |

**Evolução possível — controle por versão:** o app envia o `RowVersion` da versão que editou; se o servidor estiver em outra, é conflito real. Elimina a dependência de relógio, mas exige resolver o conflito (perguntar ao usuário ou merge por campo). A coluna já existe.

### 5.4 Mudanças de estado e inativação (`DELETE`)

- **Estado já é o pedido** (ex.: inativar algo inativo) → `2xx` com o recurso, **sem gravar nada**: `UpdatedAt` e `RowVersion` não mudam, para o registro não "reaparecer" no pull.
- **A inativação não passa pelo LWW — sempre vence.** O `DELETE` não leva `updatedAt`. Se o celular inativar offline na segunda e a web editar na terça, a sincronização de sexta inativa o registro (com a edição da web preservada nos campos). Motivos: excluir é intenção explícita, e com soft delete nada se perde (dá para reativar). A alternativa exigiria corpo no `DELETE` e criaria o caso de um registro excluído que "volta".
- `PATCH` em registro inativo continua aceito (edita sem reativar).

### 5.5 Pull incremental: `GET /api/<recurso>/changes?since=&limit=`

```http
GET /api/milk-productions/changes
→ 200 { "items": [ ...registros completos, inclusive inativos... ], "nextCursor": "38027", "hasMore": false }

GET /api/milk-productions/changes?since=38027
→ 200 { "items": [], "nextCursor": "38027", "hasMore": false }

GET /api/milk-productions/changes?since=abc
→ 400 { "error": "Cursor de sincronização inválido." }
```

| Parâmetro / campo | Regra |
|---|---|
| `since` | Ausente ou vazio → carga completa. Não numérico ou negativo → `400`. |
| `limit` | Ausente, zero ou negativo → 500; acima de 500 → 500. **Ajusta, não dá erro.** |
| `items` | Registros com `RowVersion > since`, em ordem crescente de `RowVersion`, **inclusive inativos** (tombstones). Mesmo DTO do detalhe. |
| `nextCursor` | `RowVersion` do **último item da página**, como texto. Página vazia → **o mesmo cursor recebido**. |
| `hasMore` | `true` se há mais páginas; o app pede de novo imediatamente com o `nextCursor`. |

**O que é o `rowversion`:** tipo de coluna do SQL Server (8 bytes) que o próprio banco preenche. O banco mantém **um contador único para o banco inteiro**; a cada INSERT/UPDATE numa tabela com `rowversion`, o contador avança e a linha recebe o novo valor. **Não tem relação com data/hora** (o nome antigo era `timestamp`). Inativar também é UPDATE, por isso exclusões aparecem no pull. Os valores não começam em 1 e têm "buracos" (o contador é compartilhado) — o cursor é **opaco** e o app nunca inventa um valor de `since`: usa vazio na primeira vez e depois exatamente o `nextCursor` recebido.

**`MIN_ACTIVE_ROWVERSION()` é obrigatório:** o número é reservado quando a gravação começa e só fica visível no commit. Uma transação lenta pode gravar um valor **menor** que um cursor já entregue e ela nunca seria baixada. O filtro `RowVersion < MIN_ACTIVE_ROWVERSION()` só entrega o que já está confirmado.

**Por que texto e não número:** deixa o cursor opaco no contrato (o servidor pode mudar o formato sem quebrar o app) e evita perda de precisão em JavaScript acima de 2⁵³.

**Por que não reutilizar o `GET` existente:**

| | Rotas de tela (`GET /`, `/by-date`) | `GET /changes` |
|---|---|---|
| Pergunta | "O que mostrar?" | "O que mudou desde X?" |
| Formato | Resumo por dia / item enxuto (sem `syncId`, `isActive`, `updatedAt`) | Registro completo |
| Filtro | Data **da produção** — a correção de um lançamento antigo não aparece | Momento **da mudança** (`RowVersion`) |
| Inativos | Não interessam | Obrigatórios (exclusões) |
| Tamanho | Proporcional ao período pedido | Proporcional ao que mudou |

Um `?updatedSince=` no `GET` atual também não serve: o `UpdatedAt` é o momento da edição no celular (uma edição de segunda enviada na sexta passaria despercebida), mudaria o formato que a web usa e misturaria dois contratos numa URL.

### 5.6 Idempotência sem chave por requisição

| | `SyncId` | `Idempotency-Key` |
|---|---|---|
| Identifica | O **registro** | A **tentativa de operação** |
| Duração | Permanente | Temporária (24h na Stripe) |
| Guardado | Na própria tabela | Tabela à parte, com a resposta salva |

Cada tipo de escrita já está protegido:

| Tipo de escrita | Rotas do MuuBoi | O que protege o reenvio |
|---|---|---|
| **Cria registro** | `POST` de criação: animal, pesagem, ECC, cobertura, gestação, parto, vacinação, reforço, caso de saúde e seus medicamentos/testes, movimentações de sêmen e de estoque, catálogos | O **`SyncId`** |
| **Define valores** | `PATCH` de edição | Aplicar duas vezes dá o mesmo resultado; o LWW descarta o mais antigo |
| **Muda estado** | `DELETE`, `exit`, `reactivate`, `dry-off` e desfazer, diagnóstico, perda da gestação | Estado já é o pedido → `2xx` |

Fora do offline (exigem conexão): `auth/register`, `auth/login`, `users`.

**Conclusão: nenhuma rota precisa de chave por requisição.** Ela só seria necessária para uma operação **acumulativa** que não cria registro (ex.: `POST /stock-items/{id}/consume { quantidade: 1 }`). Isso não existe: os saldos de estoque e de sêmen são **calculados a partir das movimentações**, e cada movimentação tem seu `SyncId`.

> **Regra:** toda escrita offline **cria um registro**, **define valores** ou **muda um estado**. Um campo de saldo atualizado com `+=`/`-=` vira **movimentação**. Só uma ação acumulativa que não possa virar registro justificaria `Idempotency-Key`.

O que a chave daria a mais, e por que não compensa: detectar o mesmo `SyncId` com payload diferente (só ocorre por bug no app; custo de tabela, hash e limpeza) e rastreamento — este último pode ser feito com um header `X-Request-Id` só no log (melhoria opcional).

### 5.7 Resposta perdida

Do ponto de vista do celular, três situações parecem iguais (timeout ou conexão interrompida):

| # | O que aconteceu | O servidor gravou? |
|---|---|---|
| 1 | A requisição nem chegou | Não |
| 2 | Chegou, mas o servidor caiu no meio | Não (se houver transação) |
| 3 | **Chegou, foi gravada, mas a resposta se perdeu** | **Sim** |

O app **não tem como saber**. A regra é única: **reenviar exatamente a mesma requisição, com o mesmo `SyncId`**.

```
App                                       Servidor
 │ POST /milk-productions (SyncId=a1b2) ─►  grava Id=42
 │                ✕ ◄───────────────────  201 {id:42}   (resposta perdida)
 │ timeout → mantém na fila ... backoff ...
 │ POST /milk-productions (SyncId=a1b2) ─►  encontra SyncId=a1b2 → não grava de novo
 │ ◄──────────────────────────────────── 201 {id:42}
 │ salva ServerId=42, remove da fila
```

**Problema no código das próximas entidades:** no caso 3, vários services hoje respondem `ConflictException` (`409`) ao reenvio — a fila travaria mesmo a operação tendo dado certo:

| Operação reenviada | Resposta hoje |
|---|---|
| `POST` animal | `AnimalService.cs:121` — "Já existe um animal com o brinco '...'" |
| `POST` gestação | `AnimalPregnancyService.cs:79` — "O animal já possui uma gestação ativa confirmada." |
| `POST` parto | `AnimalCalvingService.cs:44` — "Esta gestação já possui um parto ativo registrado." |
| Diagnóstico da cobertura | `BreedingEventService.cs:185` — "O diagnóstico desta cobertura já foi registrado." |
| Secar lactação | `LactationService.cs:109` — "Esta lactação já está seca." |
| Inativar (várias entidades) | "... já está inativo(a)." |

Em pesagem (sem regra de unicidade), o reenvio **duplica o registro silenciosamente**. A receita da §7.4 corrige isso por entidade.

### 5.8 Referência entre entidades criadas offline

**Cenário:** animal criado offline e, em seguida, gestação desse animal, ainda offline. A rota da gestação espera o `Id` (`int`) do animal.

**Solução A — o app troca a referência na hora do envio (adotada, A5).** O corpo da requisição é montado **quando o worker vai enviar**, não quando o usuário salva:

```
Animal (Room)
  SyncId (Guid)     ← gerado no celular
  ServerId (int?)   ← null até sincronizar

Outbox: { Operation: "CreatePregnancy", AnimalSyncId: "a1b2...", Payload: {...} }
```

1. Worker envia `POST /api/animals` com o `SyncId` → `201 { "id": 42 }` (o `CreatedAtAction` devolve o recurso completo).
2. Grava `ServerId = 42` no animal local e remove o item da fila.
3. Próximo item (gestação): busca o animal pelo `SyncId`, lê `ServerId = 42`, monta a requisição com `animalId = 42`.

O **envio sequencial** garante que o pai já tem `ServerId` quando o filho é enviado. Se o pai falhou, a fila parou antes. Se o `201` se perdeu, o reenvio devolve o mesmo `Id`.

**Solução B — servidor aceita referência por `SyncId` (descartada):**

| | A: troca no app | B: referência por `SyncId` |
|---|---|---|
| Mudança no servidor | Mínima | Grande (todos os DTOs com FK e rotas com `{id}`) |
| Complexidade no app | Montar o payload no envio e guardar `ServerId` | Menor |
| Depende de envio sequencial | Sim (já adotado) | Não |

A B só compensaria com envio em paralelo ou em lote.

### 5.9 `UpdatedAt` × `RowVersion`

| Campo | Para que serve | Quem preenche | Ordem garantida? |
|---|---|---|---|
| `UpdatedAt` | Resolver **conflito** (LWW) | O **celular** (momento da edição) ou o servidor (web) | **Não** |
| `RowVersion` | Saber **o que mudou** (cursor do pull) | O **SQL Server**, a cada INSERT/UPDATE | **Sim** |

O `UpdatedAt` não serve como cursor: ele vem do celular (edições offline chegam com data antiga) e, mesmo com horário do servidor, commits simultâneos podem terminar fora de ordem.

| Tabela | `UpdatedAt` | `RowVersion` + `SyncId` |
|---|---|---|
| Entidades sincronizáveis | Já tem (`BaseEntity`) | Adicionar via `ISyncable`, uma entidade por vez |
| `VaccinationEventAnimal` | Não precisa | Não sincroniza sozinha — vai dentro do `VaccinationEvent`; quando a lista muda, o `RowVersion` do evento avança |
| `ApplicationUser`, `Property` | — | Fora do offline |

Melhoria opcional: preencher `UpdatedAt = CreatedAt` já na criação, para o LWW comparar sempre o mesmo campo.

### 5.10 Erros

O catálogo completo está em `Docs/catalogo-erros-api.md`. Para o app, a decisão é **só pelo status HTTP**:

| Resposta | Ação no app |
|---|---|
| `2xx` | Remove da fila e grava o retorno |
| `401` | Renova o token e repete o mesmo item |
| Timeout / sem rede / `5xx` | Para e `Result.retry()` (backoff), **com limite de tentativas** — um `500` causado por bug sempre falharia e travaria a fila |
| `400` / `404` / `409` / `422` | Para, marca `Failed` e avisa o usuário |

O corpo serve só para a mensagem ao usuário. A API ainda tem formatos diferentes (`error`, `message`, `errors` como objeto ou lista, corpo vazio — ver catálogo §2); ordem sugerida de leitura: `error` → `message` (+ `errors` lista) → `errors` (objeto) → mensagem genérica pelo status.

---

## 6. Contrato do cliente (app)

A implementação do app está fora desta spec, mas ele **precisa** seguir isto para o servidor funcionar como especificado.

1. Gerar o `SyncId` **ao salvar** e persistir no Room. **Nunca regenerar** — gerado no envio, cada tentativa teria um UUID novo e a idempotência deixaria de funcionar (erro mais comum).
2. Fila (outbox) persistente no Room, esvaziada por **um único worker** (`enqueueUniqueWork` + `ExistingWorkPolicy.KEEP`), em ordem.
3. Montar o corpo da requisição **na hora do envio** (resolve `ServerId`, §5.8).
4. No `2xx` do `POST`: gravar `ServerId = Id` e remover da fila.
5. `PATCH` leva o `updatedAt` do momento da edição, **sempre em UTC com `Z`** (ex.: `"2026-10-02T17:30:00Z"`).
6. Decidir pelo status HTTP (§5.10). Edição durante o envio: guardar a versão local enviada e só limpar o "sujo" se ela não mudou.
7. Timeout adequado no OkHttp/Retrofit (ex.: 30–60 s).
8. **Compactação:** criado e excluído offline antes de sincronizar → remover os dois itens sem enviar.
9. Ciclo: **esvaziar a fila → pull `/changes` de cada recurso até `hasMore = false`**. Gravar o `nextCursor` **só depois** de aplicar a página no Room (aplicar duas vezes é seguro — upsert por `SyncId`; o contrário perderia dados).
10. **Estado da sincronização** — o servidor não sabe quais celulares existem; quem guarda é o app (mesmo modelo do `lastPulledAt` do WatermelonDB e do *delta token* do Datasync):

```
SyncState (Room)
  Resource      → "milk-productions"
  Cursor        → nextCursor da última página aplicada (texto opaco)
  LastSyncedAt  → fim do último ciclo completo (só para a interface)
```

- **Um cursor por recurso**; `LastSyncedAt` pode usar o relógio do celular (não participa de nenhuma decisão).
- **Zerar os cursores** no logout, na troca de usuário/propriedade e na reinstalação → o próximo pull é completo.

```
1º uso      Cursor vazio → GET /changes                → tudo (paginado)
Seguintes   GET /changes?since={Cursor}                → só o que mudou
            aplica a página → Cursor = nextCursor → repete enquanto hasMore
            LastSyncedAt = agora
```

### 6.1 Bibliotecas do app (Android nativo, Kotlin)

Não existe biblioteca que faça essa sincronização com a API do MuuBoi — o protocolo é próprio. O app monta a sincronização com o conjunto recomendado pelo guia oficial de offline-first:

| Biblioteca | Papel |
|---|---|
| **Room** | Banco local, **fila de envio** e **`SyncState`** |
| **WorkManager** | Sync em segundo plano: só com rede (`NetworkType.CONNECTED`), sobrevive ao app fechado, **retry com backoff**, worker único |
| **Retrofit + OkHttp** | HTTP; **timeouts**, **`Interceptor`** com o JWT, **`Authenticator`** que trata o `401` |
| **kotlinx.serialization** (ou Moshi) | JSON |
| **Coroutines + Flow** | A tela observa o Room e se atualiza quando o pull grava |
| **Hilt** | Injeção de dependência; `@HiltWorker` |
| **`kotlin.uuid.Uuid`** (stdlib) | Gera o `SyncId` |

Opcionais: DataStore (token), Paging 3. Não é preciso monitorar a conexão manualmente (a restrição de rede do WorkManager já faz isso).

| Framework pronto | Por que não serve |
|---|---|
| Datasync Community Toolkit | Cliente é **.NET**, não Kotlin |
| WatermelonDB | **React Native**, protocolo próprio (push em lote) |
| PowerSync, Couchbase Lite, Firebase | Exigem o **backend deles** |
| Realm / Atlas Device Sync | **Descontinuado** em 2025 |

**Referência de código:** [Now in Android](https://github.com/android/nowinandroid) (Room + WorkManager + Retrofit + Hilt, com `SyncWorker` e pull incremental). Não tem fila de envio, mas é a melhor referência para a estrutura do worker.

---

## 7. Arquitetura no servidor

### 7.1 Modelo

- **`ISyncable`** (`Domain/Models/ISyncable.cs`): `Guid SyncId`, `byte[] RowVersion`. Interface separada de `BaseEntity` para incluir as entidades **uma a uma**.
- `RowVersion` é `byte[]` — mapeamento padrão do EF Core para `rowversion`. A conversão para número/texto do cursor fica concentrada no helper `SyncPaging`. Trocar para `ulong` exigiria migração e não eliminaria o texto da API nem o SQL do `MIN_ACTIVE_ROWVERSION()`.
- **Multi-tenancy:** `SyncId` é único **globalmente**; tudo é filtrado por `PropertyId` pelo `HasQueryFilter` do `ApplicationDbContext` — inclusive o pull em `FromSqlRaw` (validado). Services não adicionam filtro de tenant (regra do projeto).

### 7.2 Helpers reutilizáveis

| Helper | Camada | Métodos | Uso |
|---|---|---|---|
| `SyncableModelBuilderExtensions` | `Infrastructure/Data` | `ConfigureSyncable<T>()` | No `ApplicationDbContext`: `builder.Entity<T>().ConfigureSyncable();` — default `NEWID()` no `SyncId`, índice único `UX_{Tabela}_SyncId`, `IsRowVersion()` e índice `IX_{Tabela}_PropertyId_RowVersion`. Exige `T : ISyncable, ITenantEntity`. |
| `SyncableDbContextExtensions` | `Infrastructure/Data` | `FindBySyncIdAsync<T>` · `AddSyncableAsync<T>` · `GetChangesSinceAsync<T>` | No repositório: busca por `SyncId`; inserção que devolve o existente se o índice único reclamar (erros SQL 2601/2627 no índice `UX_{Tabela}_SyncId`; senão relança com `throw;`); consulta do pull com `MIN_ACTIVE_ROWVERSION()`. |
| `SyncTimestampResolver` | `Application/Helpers` | `ResolveEditedAt(clientUpdatedAt, now)` · `IsOutdated(editedAt, entity)` | No service (`UpdateAsync`): normaliza para UTC, limita a "agora", compara com `UpdatedAt ?? CreatedAt`. Recebe `now` por parâmetro (testável). |
| `SyncPaging` | `Application/Helpers` | `ResolveLimit` · `TryDecodeCursor` · `ToRowVersionBytes` · `FromRowVersionBytes` · `BuildPage` | No service (`GetChangesAsync`): limite (padrão e máx. 500), cursor (texto ↔ `ulong` ↔ 8 bytes big-endian), montagem da página. |
| `SyncPageDto<T>` | `Application/DTOs` | — | Resposta do pull. |

> ⚠️ **Convenção obrigatória:** o índice único do `SyncId` **precisa** se chamar `UX_{Tabela}_SyncId` — o `AddSyncableAsync` identifica a corrida por esse nome. Usar `ConfigureSyncable()` garante isso.

**Pull com `FromSqlRaw`:** o C# não tem `>` para `byte[]`, então `RowVersion > since` não compila em LINQ. O filtro vai em SQL (`SELECT * FROM [Tabela] WHERE [RowVersion] > @p0 AND [RowVersion] < MIN_ACTIVE_ROWVERSION()`), e ordenação, `Take`, `AsNoTracking` e o filtro de tenant continuam no EF. O nome da tabela vem do modelo do EF (sem injeção); `since` vai como parâmetro.

**Erros:** cursor inválido → `System.ComponentModel.DataAnnotations.ValidationException` → `400` pelo `ExceptionMiddleware`. Nenhuma das três exceções de domínio descreve um parâmetro malformado.

### 7.3 Bibliotecas na API

**Nenhuma biblioteca nova** — tudo vem de EF Core 8 + SQL Server + ASP.NET Core 8 (`Guid`/`NEWID()`, `rowversion` + `IsRowVersion()`, `FromSqlRaw`, `HasIndex().IsUnique()`, `DbUpdateException` + `SqlException` de `Microsoft.Data.SqlClient`, `DbUpdateConcurrencyException`, `HasQueryFilter`, xUnit + Moq).

| Opcional nativo (⚠️ `Program.cs`, requer aprovação) | Motivo |
|---|---|
| Compressão de resposta (`AddResponseCompression`) | O JSON da carga inicial do pull pode ser grande; gzip/brotli reduz 70–90% |
| Refresh token (`POST /api/auth/refresh`) | Para o app voltar de dias offline sem perder a fila |

| Não adicionar | Por quê |
|---|---|
| Datasync Community Toolkit (server) | Exige .NET 10, `Id` em `string` e protocolo próprio |
| Pacotes de idempotência (ex.: `IdempotentAPI`) | Chave por requisição é desnecessária (§5.6) |
| MediatR, Polly, Hangfire, bulk insert | Não resolvem nenhum problema desta spec |

### 7.4 Receita: tornar uma entidade sincronizável

1. Implementar `ISyncable` no modelo (`SyncId`, `RowVersion`).
2. `builder.Entity<T>().ConfigureSyncable();` no `ApplicationDbContext`.
3. Migração (⚠️ aprovação) — `SyncId uniqueidentifier NOT NULL DEFAULT NEWID()` faz o backfill; `rowversion` é preenchido pelo SQL Server.
4. Repositório: `GetBySyncIdAsync` → `_context.FindBySyncIdAsync<T>(syncId)`; `CreateAsync` → `_context.AddSyncableAsync(entity)`; `GetChangesAsync` → `_context.GetChangesSinceAsync<T>(since, take)`.
5. DTO de criação: `Guid? SyncId` + validação de `Guid.Empty`. DTO de saída: `Guid SyncId`. AutoMapper: ignorar `SyncId` e `RowVersion` na criação.
6. Service `CreateAsync`: checar `SyncId` **antes** das regras de negócio; `entity.SyncId = dto.SyncId ?? Guid.NewGuid()`.
7. DTO de edição: `DateTime? UpdatedAt`. Service `UpdateAsync`: `ResolveEditedAt` + `IsOutdated` antes de aplicar; gravar `UpdatedAt = editedAt`.
8. Mudanças de estado repetidas → `2xx` sem gravar, em vez de `ConflictException` (A6).
9. Service `GetChangesAsync`: `SyncPaging.TryDecodeCursor` (`400` se inválido) → `ResolveLimit` → repositório com `take + 1` → `SyncPaging.BuildPage`. Controller: `[HttpGet("changes")]`.
10. Testes de service no padrão de `MilkProductionServiceTests`.

### 7.5 Condições para as próximas entidades

Não afetam a `MilkProduction` (grava um registro só), mas são **obrigatórias** antes de liberar o offline para:

**Condição 1 — uma transação por operação.** Várias operações criam **registros derivados**:

| Operação | Também cria |
|---|---|
| Parto (`AnimalCalvingService.cs:73–92`) | Crias/animais, lactação e ECC |
| Cobertura | Movimentação de sêmen (consumo da dose, `SemenSampleMovementService.cs:132`) |
| Diagnóstico positivo | Gestação (`AnimalPregnancyService.cs:196`) |
| Saída do animal | Registro de saída (`AnimalService.cs:184`) |

Hoje **cada `repository.CreateAsync` chama `SaveChangesAsync` separadamente**. Se o servidor cair entre o parto e a lactação, o reenvio encontra o `SyncId` do parto, responde "já existe" e **a lactação nunca é criada**. O `SyncId` do principal só protege os derivados se tudo for gravado na **mesma transação**.

**Condição 2 — o app gera o `SyncId` dos derivados que o usuário pode referenciar.** Ex.: parto offline seguido da pesagem da cria, ainda offline:

```json
POST /api/pregnancies/42/calvings
{
  "syncId": "parto-uuid",
  "calves": [
    { "syncId": "cria-1-uuid", "tagNumber": "..." },
    { "syncId": "cria-2-uuid", "tagNumber": "..." }
  ]
}
```

Derivados que o usuário não referencia diretamente (lactação aberta pelo parto, movimentação gerada pela cobertura) podem ter `SyncId` gerado pelo servidor e chegam ao app pelo pull.

---

## 8. Entidades no escopo

| Entidade | Status | Observação |
|---|---|---|
| `MilkProduction` | ✅ **Sincronizável** (03/Out/2026) | Parte II |
| `Vaccine` | 🚧 **Plano aprovado** (05/Out/2026) | **Spec #14.1** (`spec-sincronizacao-offline-14.1-vacinas.md`) |
| `Medication`, `StockItem`, `SemenSample` | ⏳ | Catálogos — próximos, sem dependências entre si |
| `Animal`, `AnimalExitRecord` | ⏳ | Pré-requisito dos eventos do animal |
| `WeightRecord`, `BodyConditionRecord`, `AnimalMedication` | ⏳ | `WeightRecord` hoje faz **hard delete** (questão em aberto 4) |
| `BreedingEvent`, `AnimalPregnancy`, `AnimalCalving`, `AnimalCalvingCalf` | ⏳ | Condições 1 e 2 (§7.5) |
| `SemenSampleMovement`, `StockMovement` | ⏳ | Saldo calculado por movimentações (questão em aberto 1) |
| `Lactation` | ⏳ | Depende de `Animal` |
| `VaccinationEvent` (+ `VaccinationEventAnimal` embutido), `HealthCase` (+ medicações e testes) | ⏳ | |
| `ApplicationUser`, `Property` | ❌ Fora | Autenticação e provisionamento exigem conexão |

**Ordem sugerida:** catálogos → `Animal` → eventos do animal (respeitando dependências). A lista definitiva ainda é questão em aberto (§13).

---

# Parte II — Implementação: Produção de Leite

## 9. Por que começar por `MilkProduction`

- **Sem dependências:** não tem `AnimalId` nem outra FK (Spec 11.1, D1) — não exige a resolução de `Id` da A5.
- **Sem saldo ou estado:** os lançamentos são fatos que apenas se somam (Spec 11.1, D2); sem unicidade por dia, o único risco de reenvio era **duplicar o registro**.
- **Base pronta:** filtro de tenant (`HasQueryFilter`), preenchimento automático de `PropertyId` no `SaveChangesAsync`, soft delete e índice `(PropertyId, Date)`.
- A Spec 11.1 §8 já previa a evolução de forma aditiva.

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/milk-productions` | Sempre cria | Aceita `syncId`; se já existir → devolve o existente |
| `PATCH /api/milk-productions/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW) |
| `DELETE /api/milk-productions/{id}` | `409` se já inativo | `204` se já inativo |
| `GET /api/milk-productions/changes?since=&limit=` | — | **Nova** |
| Demais `GET` | — | Sem mudança |

**Não mudou:** `Program.cs` e DI, `ExceptionMiddleware`, AutoMapper do `PATCH` (manual), banco além da migração da Fase 1.

## 10. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco | ✅ Migração `20261002224240_Offline_MilkProduction_SyncId_RowVersion` aplicada |
| 2 — Criação idempotente | ✅ Implementada e validada |
| 3 — Edição com last-write-wins (+ helpers) | ✅ Implementada e validada |
| 4 — Inativação idempotente | ✅ Implementada e validada |
| 5 — Pull incremental | ✅ Implementada e validada |
| 6 — Testes | ✅ 19 testes passando |
| 7 — Documentação | ✅ Este documento, Spec 11.1 v1.2 e catálogo de erros |

Validação manual feita a cada fase na API local (login com o usuário do seeder), com os registros de teste inativados ao final.

### 10.1 Fase 1 — Domínio e banco

```csharp
public interface ISyncable
{
    Guid SyncId { get; set; }
    byte[] RowVersion { get; set; }
}

public class MilkProduction : BaseEntity, ITenantEntity, ISyncable
{
    ...
    public Guid SyncId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

Configuração EF (desde a Fase 3, via helper): `builder.Entity<MilkProduction>().ConfigureSyncable();` — o EF confirmou modelo idêntico às quatro configurações manuais originais (`has-pending-model-changes` → sem mudanças).

Migração aplicada (02/Out/2026):

```sql
ALTER TABLE [MilkProductions] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [MilkProductions] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_MilkProductions_PropertyId_RowVersion] ON [MilkProductions] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_MilkProductions_SyncId] ON [MilkProductions] ([SyncId]);
```

Conferido no banco: os 2 registros existentes receberam `SyncId` distintos e `RowVersion` preenchido; nenhuma outra tabela foi alterada.

### 10.2 Fase 2 — Criação idempotente

- `MilkProductionCreateDto`: `Guid? SyncId` + validação "O identificador de sincronização não pode ser vazio.".
- `MilkProductionDto`: `Guid SyncId`.
- `MilkProductionProfile`: ignora `SyncId` e `RowVersion` na criação.
- Repositório: `GetBySyncIdAsync` e `CreateAsync` via helpers (§7.2).

```csharp
public async Task<MilkProductionDto> CreateAsync(MilkProductionCreateDto dto)
{
    if (dto.SyncId.HasValue)
    {
        var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
        if (existing != null)
            return _mapper.Map<MilkProductionDto>(existing);
    }

    var production = _mapper.Map<MilkProduction>(dto);
    production.SyncId = dto.SyncId ?? Guid.NewGuid();
    var created = await _repository.CreateAsync(production);
    return _mapper.Map<MilkProductionDto>(created);
}
```

Controller sem mudança: o reenvio responde `201` com o mesmo corpo ("mesma chave, mesma resposta").

| Validação (02/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` | ✅ `201`, `Id` novo |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id` |
| Mesmo `syncId`, payload diferente | ✅ `201`, devolve o existente sem alterar |
| `POST` sem `syncId` (web) | ✅ `201`, `syncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." |
| 83 envios simultâneos (4 `syncId`) | ✅ Todos `201`; 1 linha por `syncId` |

- O `catch` da corrida **não chegou a ser exercitado**: todos os reenvios simultâneos foram barrados antes, na checagem do service. Comprovado: sem duplicata. Não comprovado: o retorno do existente via `catch` (exigiria teste de integração com banco real).
- Diferença de formato no reenvio: `120.5` × `120.50` e `createdAt` com × sem `Z` (o reenvio lê do banco). A data sem `Z` já acontece em **todos os `GET`** da API — tarefa separada.

### 10.3 Fase 3 — Edição com last-write-wins

- `MilkProductionUpdateDto`: `DateTime? UpdatedAt` (sem validação de data futura, de propósito — §5.3).
- Service:

```csharp
var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, production))
    return _mapper.Map<MilkProductionDto>(production);

// aplica os campos como antes
production.UpdatedAt = editedAt;
```

- Nesta fase foram criados os helpers `SyncTimestampResolver`, `SyncableDbContextExtensions` e `SyncableModelBuilderExtensions` (§7.2).
- Concorrência: sem tratamento (§5.3).

| Validação (02/Out/2026) | Resultado |
|---|---|
| `updatedAt` com fuso `-03:00` | ✅ `20:29:07-03:00` gravado como `23:29:07Z`; aplicado |
| `updatedAt` UTC mais novo | ✅ Aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado |
| Edição mais antiga | ✅ `200`, ignorada; devolve a versão do servidor |
| Web sem `updatedAt` | ✅ Aplicado com "agora" |
| Relógio adiantado 1 dia | ✅ Aplicado; `updatedAt` limitado a "agora" |
| Reenvio do `POST` via helper genérico | ✅ Mesmo `Id`, 1 linha |

### 10.4 Fase 4 — Inativação idempotente

```csharp
if (!production.IsActive)
    return true;
```

Substitui `throw new ConflictException("O lançamento de produção de leite já está inativo.")`. Para a web, inativar duas vezes deixa de dar erro.

| Validação (02/Out/2026) | Resultado |
|---|---|
| `DELETE` em registro ativo | ✅ `204`; `IsActive=0`, `RowVersion` 38026 → 38027 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` e `RowVersion` **inalterados** |
| `DELETE` em id inexistente | ✅ `404` |

### 10.5 Fase 5 — Pull incremental

- `SyncPageDto<T>`, helper `SyncPaging`, `GetChangesSinceAsync<T>` (§7.2).
- Repositório `GetChangesAsync(ulong since, int take)`; service `GetChangesAsync(string? since, int? limit)`; controller `[HttpGet("changes")]`.

```csharp
public async Task<SyncPageDto<MilkProductionDto>> GetChangesAsync(string? since, int? limit)
{
    if (!SyncPaging.TryDecodeCursor(since, out var cursor))
        throw new ValidationException("Cursor de sincronização inválido.");

    var take = SyncPaging.ResolveLimit(limit);
    var fetched = await _repository.GetChangesAsync(cursor, take + 1);

    return SyncPaging.BuildPage(fetched, take, cursor, p => _mapper.Map<MilkProductionDto>(p));
}
```

| Validação (02/Out/2026) | Resultado |
|---|---|
| Pull inicial sem `since` | ✅ Todos os registros da propriedade, inclusive inativos |
| **Isolamento por propriedade** | ✅ A tabela tinha 10 registros; vieram só os 8 da propriedade do usuário — o `HasQueryFilter` vale sobre o `FromSqlRaw` |
| `since` igual ao último cursor, sem mudanças | ✅ `items: []`, cursor mantido |
| Cria A, edita A, cria B, inativa B → pull | ✅ Só A (ativo, volume final) e B (inativo), cada um **uma vez** |
| Paginação `limit=1` | ✅ 10 páginas em ordem, cursores crescentes, sem repetir nem pular; última com `hasMore: false` |
| `since=abc` e `since=-5` | ✅ `400` |
| `limit=0` e `limit=100000` | ✅ `200` (ajustados para 500) |

### 10.6 Fase 6 — Testes

**Projeto de testes:** o `MuuBoi.Tests.csproj` existia só na branch `feat/tests` (commit `48535dc`, nunca integrado), com `bin/`/`obj/` commitados, um `UnitTest1.cs` vazio e `Moq` adicionado sem necessidade ao `MuuBoi.csproj`. Foi recriado só o necessário — `.csproj` idêntico (net10.0, xUnit 2.9.3, Moq 4.20.72, coverlet, Test SDK 17.14.1) e inclusão no `MuuBoi.sln`. A branch `feat/tests` não foi alterada.

`MuuBoi.Tests/Services/MilkProductionServiceTests.cs` — repositório mockado com Moq, **AutoMapper real** (nenhum teste verifica mapeamento), Arrange/Act/Assert sem linhas em branco. **19 testes, todos passando.**

| # | Teste | Verifica |
|---|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesProductionWithGivenSyncId` | Cria com o `SyncId` informado |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` | Devolve o existente sem chamar `CreateAsync` |
| 3 | `CreateAsync_WithoutSyncId_GeneratesSyncId` | Gera `SyncId`; não consulta por `SyncId` |
| 4 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` | Aplica; `UpdatedAt` = horário do cliente |
| 5 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` | Não aplica nem grava |
| 6 | `UpdateAsync_WithSameClientUpdatedAt_AppliesChanges` | Empate (reenvio) aplica |
| 7 | `UpdateAsync_WithFutureClientUpdatedAt_ClampsToNow` | Limita a "agora" |
| 8 | `UpdateAsync_WithOffsetClientUpdatedAt_ConvertsToUtc` | `Local` → UTC (máquina em UTC-03:00, teste significativo) |
| 9 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` | Web: "agora" |
| 10 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` | Compara com `CreatedAt` |
| 11 | `UpdateAsync_WhenProductionNotFound_ThrowsNotFoundException` | `404` |
| 12 | `DeactivateAsync_WhenActive_DeactivatesAndReturnsTrue` | Inativa e grava |
| 13 | `DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` | Idempotente, sem gravar |
| 14 | `DeactivateAsync_WhenProductionNotFound_ThrowsNotFoundException` | `404` |
| 15 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` | `400`; não consulta o repositório |
| 16 | `GetChangesAsync_WithoutCursor_RequestsChangesFromZero` | Cursor vazio → 0; pede `DefaultLimit + 1` |
| 17 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` | `hasMore`; cursor do último item da página |
| 18 | `GetChangesAsync_WhenNoChanges_KeepsReceivedCursor` | Página vazia mantém o cursor |
| 19 | `GetChangesAsync_WithLimitAboveMax_RequestsMaxPlusOne` | Limite ajustado |

`SyncTimestampResolver` (4–10) e `SyncPaging` (15–19) ficam cobertos **indiretamente** (regra "só services" do `CLAUDE.md`). Fora dos testes: métodos anteriores ao offline e os helpers de infraestrutura, que dependem do SQL Server real e foram validados manualmente.

### 10.7 Fase 7 — Documentação

- **Este documento** (consolidação da Spec #14, da análise e do plano).
- **Spec 11.1 → v1.2:** DTOs, rotas, RN-05 a RN-07 e §8 "Suporte offline".
- **`Docs/catalogo-erros-api.md`:** comportamento offline da produção de leite e `400` do cursor.
- **`CLAUDE.md`:** exceção à regra de `ConflictException` para rotas offline — ⏳ proposta, aguardando aprovação.

## 11. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/ISyncable.cs` | **Novo** |
| Domain | `Domain/Models/MilkProduction.cs` | `SyncId`, `RowVersion` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Data/SyncableModelBuilderExtensions.cs` | **Novo** |
| Infrastructure | `Infrastructure/Data/SyncableDbContextExtensions.cs` | **Novo** |
| Infrastructure | `Infrastructure/Migrations/20261002224240_Offline_MilkProduction_SyncId_RowVersion*.cs` + snapshot | **Nova migração** |
| Infrastructure | `Infrastructure/Repositories/MilkProductionRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` e `GetChangesAsync` via helpers |
| Application | `Application/Helpers/SyncTimestampResolver.cs` | **Novo** |
| Application | `Application/Helpers/SyncPaging.cs` | **Novo** |
| Application | `Application/DTOs/SyncPageDto.cs` | **Novo** |
| Application | `Application/DTOs/MilkProductionCreateDto.cs` | `SyncId?` + validação |
| Application | `Application/DTOs/MilkProductionUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/MilkProductionDto.cs` | `SyncId` |
| Application | `Application/Mappings/MilkProductionProfile.cs` | Ignora `SyncId` e `RowVersion` na criação |
| Application | `Application/Interfaces/IMilkProductionRepository.cs`, `IMilkProductionService.cs` | Novos métodos |
| Application | `Application/Services/MilkProductionService.cs` | Criação idempotente, LWW, inativação idempotente, pull |
| Api | `Api/Controllers/MilkProductionsController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/MuuBoi.Tests.csproj`, `MuuBoi.Tests/Services/MilkProductionServiceTests.cs` | **Novos** |
| Solução | `MuuBoi/MuuBoi.sln` | + `MuuBoi.Tests` |

---

# Parte III — Riscos, pendências e referências

## 12. Riscos e pontos de atenção

| Risco | Mitigação |
|---|---|
| Registro "pulado" no pull por transação ainda não confirmada | `MIN_ACTIVE_ROWVERSION()` (§5.5) |
| Relógio do celular adiantado vence o LWW | Limite a "agora" (§5.3) |
| Relógio do celular atrasado perde sempre | **Risco aceito** (D2); evolução: controle por versão |
| `DbUpdateConcurrencyException` (edições simultâneas) | Vira `500`; o app tenta de novo (§5.3) |
| Índice único fora da convenção `UX_{Tabela}_SyncId` | `AddSyncableAsync` não reconhece a corrida → `500`. Usar sempre `ConfigureSyncable()` |
| Duplicata se o app regenerar o `SyncId` | Contrato do cliente, item 1 (§6) |
| Fila travada por `500` permanente (bug) | Limite de tentativas no app (§5.10) |
| **Fila travada por um erro definitivo** (A3): um item `Failed` (`400`/`409`/`422`) segura todos os seguintes, mesmo sem relação (ex.: um evento de vacinação recusado impede o envio das ordenhas) | Aceito para propriedade pequena (1 usuário resolve o aviso). **Evolução, só no app:** o item que falhou bloqueia apenas os **dependentes** — cada item da fila já conhece os `SyncId`s que referencia (§5.8); o worker pula os que referenciam um `SyncId` com falha e segue com o resto. Sem mudança no servidor (§12.1) |
| `409` em reenvio nas próximas entidades | Receita §7.4, passo 8 |
| Registros derivados incompletos após falha | Condição 1 (§7.5) |
| Banco restaurado de backup ou recriado | Cursores dos celulares perdem o sentido → exigiria pull completo (zerar cursores). Raro num TCC; limitação registrada |
| JWT expirado durante dias offline | Refresh token — pendente (§13) |

### 12.1 Avaliação da fila única sequencial para uma propriedade pequena

Revisão de A1–A3 (05/Out/2026) diante do cenário do TCC: propriedade pequena, 1–2 celulares, servidor fraco e internet ruim.

**Por que a escolha se sustenta:**

| Aspecto | Avaliação |
|---|---|
| **Volume** | Poucas dezenas de operações por dia (ordenhas, pesagens, eventos). Mesmo após dias offline, a fila tem centenas de itens; a 0,5–2 s por requisição em link ruim, esvazia em minutos, em segundo plano. |
| **Servidor fraco** | Uma requisição por vez é a carga mais leve possível; lote ou paralelismo concentrariam o trabalho em picos. |
| **Dependências** | A ordem (animal → gestação → parto; vacina → evento) e a resolução do `Id` (§5.8) saem de graça do envio sequencial. |
| **Regras de negócio** | Validações e regras existentes valem igual para web e app; não há `SyncService` duplicando lógica (§2.2). |
| **Erros** | Status HTTP por operação; não existe "falha parcial de lote". |
| **Custo por entidade** | Confirmado nas vacinas (Spec #14.1): poucas linhas por camada usando os helpers da §7.2. |

O overhead de várias requisições (cabeçalhos, JWT, handshake) é reduzido no app com conexão reaproveitada (keep-alive do OkHttp, padrão) e compressão (§13, item 7) — não justifica rota em lote nesta escala.

**Ponto fraco:** o bloqueio da fila por um único erro definitivo (tabela acima). A evolução "falha bloqueia só os dependentes" resolve no app, sem mudar o servidor.

**Quando deixaria de ser adequada:** muitos dispositivos enviando ao mesmo tempo, milhares de operações por ciclo, ou link em que cada requisição leve dezenas de segundos. Nesses casos, criar uma rota em lote **só** para os registros mais frequentes (ordenha, pesagem), como previsto na §2.2. Nenhum desses é o cenário do TCC.

## 13. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| 1 | **Estoque de sêmen e de insumos offline:** dois dispositivos consumindo a última dose → saldo negativo no sync. | Direção: movimentações append-only com `SyncId` e saldo calculado (já é o modelo atual); saldo negativo no sync vira `422` com motivo ou alerta. **A definir** ao sincronizar essas entidades. |
| 2 | **Lista definitiva de sincronizáveis** (§8), inclusive mídias/fotos (payload grande em link instável). | ⏳ Aberta |
| 3 | ~~Origem do `UpdatedAt` para LWW~~ | ✅ **Resolvida:** momento da edição no cliente, UTC, limitado a "agora" (§5.3) |
| 4 | **Hard delete:** `WeightRecordRepository.DeleteWeightRecordAsync` faz `Remove` (exclusão física). Sem tombstone, a exclusão não chega aos celulares pelo pull. | ⏳ Converter para soft delete antes de sincronizar pesagens (ou tabela de tombstones) |
| 5 | **Reautenticação JWT** após dias offline. | ⏳ Refresh token (`POST /api/auth/refresh`) — fase própria; `401` nunca descarta a fila |
| 6 | ~~Tamanho do lote / paginação~~ | ✅ **Resolvida:** sem lote no push; pull com `limit` padrão e máximo de 500 |
| 7 | **Compressão de resposta** para a carga inicial do pull | ⏳ Opcional (`Program.cs`) |

## 14. Fora do escopo

- **Implementação do app** (Room, WorkManager, UX de conflito) — esta spec define só o contrato que ele deve seguir (§6).
- **Modelagem de cada entidade** — permanece em seus specs; aqui só se define como torná-la sincronizável.
- **Regras de negócio de domínio** — inalteradas; a sincronização as respeita (o `SyncId` só é checado antes delas).
- **Índices zootécnicos** → Spec #7.
- **Formato único de erro da API** (ProblemDetails) — ver `Docs/catalogo-erros-api.md`, §7.

## 15. Fontes

- [Android Developers — Build an offline-first app](https://developer.android.com/topic/architecture/data-layer/offline-first)
- [Now in Android (GitHub)](https://github.com/android/nowinandroid)
- [WatermelonDB — Sync Backend](https://watermelondb.dev/docs/Sync/Backend) · [Sync Frontend](https://watermelondb.dev/docs/Sync/Frontend)
- [Datasync Community Toolkit](https://communitytoolkit.github.io/Datasync/) · [Cliente: push/pull](https://communitytoolkit.github.io/Datasync/in-depth/client/index.html)
- [Stripe — Idempotent requests](https://docs.stripe.com/api/idempotent_requests)
- [IETF — draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07)
- [RFC 9562 — UUIDs](https://www.rfc-editor.org/rfc/rfc9562)
- [Kotlin — UUIDs](https://kotlinlang.org/docs/uuids.html) · [`Uuid.generateV7`](https://kotlinlang.org/api/core/kotlin-stdlib/kotlin.uuid/-uuid/-companion/generate-v7.html)
- [Guid.CreateVersion7() is NOT a sequential guid for SQL Server](https://daily.dev/posts/guid-createversion7-is-not-a-sequential-guid-for-sql-server-rlojn9pdl) · [UUID v7 for SQL Server Indexes: Still a Bad Idea](https://pejmannik.dev/blog/uuid_v7_for_sql_server_indexes_still_a_bad_idea/)
- [rowversion (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/data-types/rowversion-transact-sql) · [MIN_ACTIVE_ROWVERSION (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/functions/min-active-rowversion-transact-sql) · [EF Core — conflitos de concorrência](https://learn.microsoft.com/ef/core/saving/concurrency)
- [droidcon — The Complete Guide to Offline-First Architecture in Android](https://www.droidcon.com/2025/12/16/the-complete-guide-to-offline-first-architecture-in-android/)

