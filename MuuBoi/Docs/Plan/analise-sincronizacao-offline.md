# Análise: Comportamento Offline e Sincronização — MuuBoi

**Data:** 01/Out/2026
**Relaciona-se com:** Spec #14 — `Docs/Specs/spec-sincronizacao-offline.md`
**Objetivo:** comparar abordagens de sincronização com o que é usado hoje em produção, definir como fazer controle de idempotência e como o app Android sabe quando pode parar de reenviar dados.

---

## 1. Padrão base: offline-first com "lazy writes"

O guia oficial do Android ([Build an offline-first app](https://developer.android.com/topic/architecture/data-layer/offline-first)) define três estratégias de escrita:

| Estratégia | Como funciona | Quando usar |
|---|---|---|
| Online-only | Tenta a rede; só grava local se der certo | Transferência bancária |
| Queued writes | Enfileira e drena quando houver rede | Logs, analytics |
| **Lazy writes** | **Grava no banco local primeiro e enfileira o envio** | **Dado crítico do usuário — caso do MuuBoi** |

- O app sempre lê do banco local (Room), que é a fonte da verdade da UI.
- A sincronização roda em segundo plano com **WorkManager**: respeita bateria/Doze, sobrevive ao fechamento do app e só executa com rede (`NetworkType.CONNECTED`).
- Se o worker retorna `Result.retry()`, o WorkManager reagenda com **backoff exponencial**.
- Para conflito, o próprio guia recomenda **last-write-wins por timestamp** — a decisão D2 da Spec #14.

---

## 2. Uma rota de sincronização ou adaptar cada POST/PATCH?

As duas abordagens existem em produção.

### Opção A — Adaptar cada rota (sync orientado a recurso)

Cada entidade mantém `POST /api/animals`, `PATCH /api/animals/{id}` etc. O app esvazia a fila fazendo **uma requisição por operação**.

**Quem usa:** [Datasync Community Toolkit](https://communitytoolkit.github.io/Datasync/) (Microsoft / .NET Foundation, sucessor do Azure Mobile Apps, feito para ASP.NET Core + EF Core). O push é feito operação por operação — *"completed operations are removed and failed operations are marked as failed"* — e o pull é por tabela, com *delta token* baseado em `UpdatedAt` ([docs do cliente](https://communitytoolkit.github.io/Datasync/in-depth/client/index.html)).

| Prós | Contras |
|---|---|
| Reaproveita controllers, validações e DTOs existentes | Muitos round-trips (80 pesagens = 80 requisições em internet ruim) |
| Erro isolado por operação | Ordem de dependência (animal → gestação → parto) fica no app |
| Fácil de depurar no Swagger | Idempotência precisa ser implementada em **todas** as rotas |

### Opção B — Rota dedicada de sincronização

`GET /api/sync/changes?since=` (pull) e `POST /api/sync/changes` (push em lote).

**Quem usa:** [WatermelonDB](https://watermelondb.dev/docs/Sync/Backend) define exatamente o protocolo `pullChanges` / `pushChanges`. CouchDB/PouchDB, RxDB e PowerSync seguem a mesma ideia de "canal de replicação".

| Prós | Contras |
|---|---|
| Poucos round-trips | Contrato novo para projetar e testar |
| Servidor resolve ordem de dependências dentro do lote | Precisa definir o comportamento em falha parcial |
| Idempotência, LWW e tenant centralizados num lugar só | Risco de o `SyncService` **duplicar regras de negócio** |

### Recomendação para o MuuBoi: híbrido

É o que a Spec #14 já propõe:

1. **Pull dedicado** — `GET /api/sync/changes?since={cursor}`. Mesmo o Datasync, que faz push por rota, tem um mecanismo de delta separado. As rotas de listagem não servem para pull: não trazem tombstones (excluídos) nem têm cursor.
2. **Push em lote** — `POST /api/sync/changes`. Combina com servidor fraco + internet instável e com o caso gestação + parto criados offline (dependência no mesmo lote).
3. **As rotas REST atuais continuam** para o uso online (web).
4. **Regra de ouro:** o `SyncService` **não reimplementa regras**. Para cada item do lote ele chama os mesmos services de domínio usados pelos controllers (`AnimalService.CreateAsync` etc.). Assim RN-18, consumo de dose e demais regras valem igualmente nos dois caminhos.

### Transacional total vs. resultado por item

O WatermelonDB exige push **totalmente transacional**: se um item falha, o lote inteiro é revertido e o servidor retorna erro. A Spec #14 propõe **resultado por item**.

Para internet ruim, resultado por item é melhor — não se perde o lote inteiro por causa de um registro inválido. Recomendação: manter resultado por item, aplicar em ordem topológica e **rejeitar automaticamente os filhos cujo pai foi rejeitado**.

---

## 3. Controle de idempotência

**Problema:** o app envia o POST, o servidor grava, a resposta `201` se perde no caminho e o app reenvia. Sem proteção, o registro é duplicado.

A solução tem duas camadas complementares.

### Camada 1 — Idempotência natural pela identidade (`SyncId`)

Já está na decisão D1. Como **o app gera o `Guid`** do registro, o servidor reconhece o reenvio pelo próprio dado. O WatermelonDB transforma isso em regra do protocolo:

> Se chegar um registro "novo" com ID que já existe, o servidor **DEVE atualizar e NÃO DEVE retornar erro**. Excluir algo que não existe é ignorado.

Na prática o push vira um **upsert por `SyncId`**:

- não existe → insere;
- existe → aplica LWW.

Aplicar o mesmo estado duas vezes produz o mesmo resultado — a operação já é idempotente sem tabela extra.

**Condição:** isso só funciona se as operações descreverem **estado** ("o peso é 450 kg") e não **incremento** ("diminua 1 dose"). Por isso o estoque de sêmen (Spec #14, §10.1) deve ser modelado como **movimentações append-only**, cada uma com seu `SyncId`, e o saldo é calculado a partir delas. Reenviar a mesma movimentação não consome a dose duas vezes.

### Camada 2 — Chave de idempotência por requisição (padrão Stripe / IETF)

Cobre o **lote** como um todo e operações que não são um upsert simples.

- A [Stripe](https://docs.stripe.com/api/idempotent_requests) salva o **status e o corpo da primeira resposta** para cada chave; um reenvio com a mesma chave recebe a mesma resposta. Mesma chave com parâmetros diferentes gera erro.
- A IETF está padronizando o header [`Idempotency-Key`](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07), com UUID recomendado. O draft sugere `422` para mesma chave com payload diferente e `409` para requisição concorrente com a mesma chave ainda em processamento.

**Modelo de tabela:**

```
IdempotencyRecords
  Key (Guid, PK) | PropertyId | RequestHash | StatusCode | ResponseBody | CreatedAt
```

**Fluxo:**

1. Requisição chega com `Idempotency-Key`.
2. Chave já existe:
   - mesmo hash → devolve a resposta salva;
   - hash diferente → `422`.
3. Chave não existe → processa e grava o registro **na mesma transação** do efeito. Isso é crítico: gravando separado, um crash entre as duas gravações quebra a garantia.

**Atenção específica de offline:** a Stripe mantém as chaves por **24h**, mas um app de fazenda pode ficar dias sem sinal. Por isso:

- a **Camada 1 (`SyncId`) é a proteção principal** — ela é permanente;
- se a tabela de chaves for usada, a retenção deve ser maior que o maior período offline esperado (ex.: **30 dias**).

---

## 4. Como o Android sabe que pode parar de enviar

O padrão é uma **outbox** (fila persistente) no Room, ou uma flag de status em cada registro (o WatermelonDB usa `_status = created / updated / deleted / synced`).

```
OutboxEntry
  Id | EntityType | SyncId | Operation | PayloadJson | Attempts | Status | LocalVersion
```

### Fluxo do worker

1. Lê a outbox em ordem.
2. Envia o lote com `Idempotency-Key`.
3. Para **cada item**, decide pelo resultado retornado pelo servidor:

| Resposta do servidor | Ação no app |
|---|---|
| `Applied` / `201` / `200` | **Remove da outbox** |
| Duplicado reconhecido (mesmo `SyncId` ou chave) | **Remove** — o servidor já tinha recebido |
| `ConflictResolved` (LWW, servidor venceu) | Remove e aplica a versão do servidor no banco local |
| `Rejected` / `422` / `404` (regra de negócio) | **Para de tentar.** Marca como `Failed` e mostra ao usuário — reenviar daria o mesmo erro |
| `401` (JWT expirado) | **Não descarta.** Renova o token e tenta de novo |
| Timeout / sem rede / `5xx` | **Mantém** e retorna `Result.retry()` — o WorkManager faz o backoff |

**Regra:** o app só para de enviar quando recebe uma **resposta definitiva** do servidor (sucesso ou rejeição de negócio). Falha de transporte nunca tira o item da fila. Se a resposta se perdeu, o reenvio é seguro por causa da idempotência, e o servidor devolve o mesmo resultado.

### Armadilhas comuns

- **Edição durante o envio:** o usuário edita o animal enquanto o lote está em trânsito; a resposta de sucesso limpa a flag "sujo" e a edição nova se perde. Solução: guardar a `LocalVersion` enviada e só limpar se ela não mudou.
- **Cursor do pull:** só salvar o novo cursor (`rowversion`) **depois** de aplicar a página inteira no Room. Se salvar antes e o app cair, essas mudanças nunca mais são baixadas.

### Ordem do ciclo

**push → pull**, repetindo o pull até não haver mais páginas. O pull traz de volta inclusive o que o servidor resolveu via LWW.

---

## 5. Ajustes sugeridos na Spec #14

1. **§5.3 — Idempotência:** definir o `SyncId` como deduplicação principal e permanente; a `Idempotency-Key` do lote como complementar, com retenção ≥ 30 dias. Exigir gravação na mesma transação do efeito.
2. **§5.2 — Push:** formalizar os status por item (`Applied`, `Duplicate`, `ConflictResolved`, `Rejected` + motivo) e quais são definitivos vs. passíveis de retry — o app depende disso para decidir quando parar de reenviar.
3. **§5.2 — Push:** registrar que o `SyncService` delega aos services de domínio e não duplica regras de negócio.
4. **§10.1 — Estoque de sêmen:** resolver com movimentações append-only e saldo calculado. Saldo negativo no sync vira `Rejected` com motivo (ou alerta).
5. **§10.5 — Autenticação:** `401` nunca descarta a fila. Prever refresh token de validade longa.

### Sobre soluções prontas

O Realm / Atlas Device Sync (MongoDB), uma das soluções de sync mais usadas, foi descontinuado em 2025. Isso reforça a escolha de um contrato próprio, simples e documentado — também mais defensável academicamente num TCC.

---

## Fontes

- [Android Developers — Build an offline-first app](https://developer.android.com/topic/architecture/data-layer/offline-first)
- [WatermelonDB — Sync Backend](https://watermelondb.dev/docs/Sync/Backend)
- [WatermelonDB — Sync Frontend](https://watermelondb.dev/docs/Sync/Frontend)
- [Datasync Community Toolkit (Microsoft / .NET Foundation)](https://communitytoolkit.github.io/Datasync/)
- [Datasync Community Toolkit — Cliente: push/pull](https://communitytoolkit.github.io/Datasync/in-depth/client/index.html)
- [Datasync Community Toolkit — GitHub](https://github.com/adrianhall/CommunityToolkit-Datasync)
- [Stripe — Idempotent requests](https://docs.stripe.com/api/idempotent_requests)
- [IETF — draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07)
- [IETF HTTPAPI WG — repositório do draft de idempotência](https://github.com/ietf-wg-httpapi/idempotency)
- Leitura complementar: [droidcon — The Complete Guide to Offline-First Architecture in Android](https://www.droidcon.com/2025/12/16/the-complete-guide-to-offline-first-architecture-in-android/)
