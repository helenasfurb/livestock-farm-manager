# Spec #14.3: Sincronização Offline — Estoque de Insumos e Movimentações

**Módulo:** Infraestrutura / Sincronização — Estoque de Insumos
**Versão:** 1.0
**Data:** 08/Out/2026
**Status:** ✅ **Implementada e validada** (08/Out/2026) — decisões E1–E9, Fases 1–11. Branch `feature/offline-stock`. Nenhuma regra de negócio alterada (R1–R11); um bug pré-existente corrigido (§3 item 4).
- **Parte A — Catálogo (`StockItem`):** ✅ Fases 1–5
- **Parte B — Movimentações (`StockMovement`):** ✅ Fases 6–9

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 08/Out/2026 | Rascunho: insumo e movimentações planejados juntos, **sem alterar nenhuma regra de negócio** |
| 1.0 | 08/Out/2026 | Fases 1–11 implementadas e validadas; 113 testes passando. Bug do `PATCH` parcial do insumo confirmado e corrigido. Contrato do app ganha o item 5 da §5 (nome/unidade pelo insumo local) |
| 0.2 | 08/Out/2026 | Decisões E1–E9 aprovadas. O contrato do app (§5 itens 3 e 4) passa a **exigir** que saldo, valor, previsão e alerta exibidos sejam recalculados localmente |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2) e receita (§7.4). Esta spec **não** redecide nada da #14; aplica a receita a `StockItem` e `StockMovement` e resolve a Condição 1 e a Condição 2 da §7.5 para o saldo inicial.
**Relaciona-se com:** Spec #14.2 (Sêmen — mesmo formato catálogo + movimentações; esta spec segue as soluções S1, S3, M1, M2 e M4 de lá) · `spec-estoque.md` (D7–D13, RN-06, RN-08) · `Docs/catalogo-erros-api.md` §4.14

> **Por que estoque agora:** é o último catálogo da Spec #14 §8 (`Medication` saiu do escopo). Não depende de `Animal`; as únicas FKs são `StockCategory` e `UnitOfMeasure`, que são tabelas de referência **globais** (seed no `ApplicationDbContext`, sem `PropertyId`) e **não** sincronizam — o app as recebe pelas rotas de referência que já existem.
>
> **Por que as duas partes juntas:** ao contrário da 14.2, a solução da quantidade inicial (M2 da 14.2) já é conhecida e depende do `SyncId` na movimentação. Planejar insumo e movimentação na mesma spec permite **uma migração só** e evita refazer a criação do insumo.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST`, `PATCH`, `DELETE /api/stock-items[/{id}]` com suporte offline | Rotas de tela: `GET /`, `GET /{id}`, `GET /{stockItemId}/movements`, `GET /{stockItemId}/movements/{movementId}` (contrato inalterado, exceto o `syncId` aditivo) |
| `POST`, `PATCH`, `DELETE /api/stock-items/{stockItemId}/movements[/{movementId}]` com suporte offline | `GET /api/stock/dashboard`, `/alerts`, `/movement-types`, `/movement-reasons` — leitura agregada, sem offline |
| `GET /api/stock-items/changes` e `GET /api/stock-items/movements/changes` (**novas**) | `StockCategory` e `UnitOfMeasure` — referência global, não sincronizável |
| Saldo inicial do cadastro (`initialQuantity`) gravado junto com o insumo e endereçável pelo app | Qualquer mudança de regra de negócio (§2) |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/stock-items` | Sempre cria; saldo inicial em gravação separada | Aceita `syncId` e `initialMovementSyncId`; se já existir → devolve o existente (`201`). Insumo e saldo inicial gravados **juntos**. Resposta ganha `initialMovement { id, syncId }` |
| `PATCH /api/stock-items/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW) |
| `DELETE /api/stock-items/{id}` | `204`; **`409`** se já inativo | `204` **sem gravar** se já inativo |
| `GET /api/stock-items/changes?since=&limit=` | — | **Nova** |
| `POST /{stockItemId}/movements` | Sempre cria | Aceita `syncId`; se já existir → devolve a existente (`201`) |
| `PATCH /{stockItemId}/movements/{movementId}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antiga (LWW) |
| `DELETE /{stockItemId}/movements/{movementId}` | `204`; **`409`** se já inativa | `204` **sem gravar** se já inativa |
| `GET /api/stock-items/movements/changes?since=&limit=` | — | **Nova** |
| `GET /{id}`, `GET /{stockItemId}/movements/{movementId}` e respostas de `POST`/`PATCH` | — | Ganham `syncId` (aditivo) |

## 2. Regras de negócio preservadas

**Premissa (como na 14.2 Parte B): nenhuma regra de negócio é alterada.** A sincronização só acrescenta identidade, versionamento, reenvio idempotente e pull. As únicas mudanças de comportamento são as de "estado repetido" exigidas pela A6 (Spec #14 §3.3), que não são regra de negócio.

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Insumo precisa existir | `PATCH`/`DELETE /{id}` e todas as rotas de movimentação | `404` "Insumo com id '{id}' não encontrado." |
| R2 | Categoria e unidade precisam existir | `POST`, `PATCH /{id}` | `404` "Categoria com id '{id}' não encontrada." / "Unidade de medida com id '{id}' não encontrada." |
| R3 | Movimentação precisa existir | `PATCH`, `DELETE` de movimentação | `404` "Movimentação com id '{movementId}' não encontrada." |
| R4 | Não registra movimentação em insumo inativo | `POST` de movimentação | `409` "Não é possível registrar movimentação para um insumo inativo." |
| R5 | Data da movimentação não pode ser futura | `POST` (validação do DTO, `400`) e `PATCH` (service, `422`) | Mantido, inclusive a diferença de status entre as duas rotas |
| R6 | Motivo exige tipo (`Purchase`/`OpeningBalance` → entrada; `Consumption`/`Loss` → saída; `Adjustment` livre) e compra/saldo inicial exigem valor | `POST` de movimentação (DTO) | `400` (formato B) |
| R7 | Valoração (`spec-estoque.md` D7–D9, RN-06): compra/saldo inicial levam o valor informado; saídas congelam `UnitCostSnapshot` pelo **custo médio vigente no momento em que o servidor processa**; ajuste de entrada recebe `TotalValue` = custo médio × quantidade | `StockMovementService.ApplyValuationAsync` | Sem mudança. Ver E7 para o efeito no offline |
| R8 | Correções (`PATCH`) **não** recalculam o `UnitCostSnapshot` (D13); `MovementType`/`MovementReason` não são editáveis | `PATCH` de movimentação | Sem mudança |
| R9 | **Não há** checagem de saldo em saídas (saldo negativo é permitido — `spec-estoque.md` US-03: "não bloqueia"), nem de insumo ativo no `PATCH`/`DELETE` de movimentação | — | Continua sem checagem |
| R10 | Saldo = Σ entradas ativas − Σ saídas ativas; valor = Σ `TotalValue` das entradas − Σ (`UnitCostSnapshot` × quantidade) das saídas (RN-08) | `StockMovementRepository.GetLevelsAsync` | Sem mudança. O app usa a mesma fórmula sobre os dados do pull |
| R11 | Saldo inicial do cadastro vira movimentação `Input`/`OpeningBalance`, datada no **momento do processamento** (`DateTime.UtcNow`), com `ValueEntryMode = TotalPrice` se houver valor | `StockMovementService.CreateOpeningBalanceAsync` | Mantido (a gravação passa para o `StockItemService`, E1). Ver E5 para o efeito no offline |

**Ordem entre `SyncId` e regras:** como na 14.2, o `SyncId` é checado **antes** de R1/R2/R4. O reenvio de uma operação já gravada responde `201` com o registro existente, mesmo que depois outro dispositivo tenha inativado o insumo. Um `POST` **novo** (outro `syncId`) em insumo inativo continua `409` (R4).

**Questão em aberto 1 da Spec #14 (saldo negativo no sync):** como não há regra de saldo (R9), duas saídas offline simplesmente somam, como online, e o modelo append-only converge. **Resolvida para o estoque de insumos: nada a fazer.** Continua aberta apenas para a cobertura (dose de sêmen).

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `StockItemService.CreateAsync` grava o insumo (`_repository.CreateAsync` → `SaveChanges`) e **depois** o saldo inicial (`_movementService.CreateOpeningBalanceAsync` → outro `SaveChanges`) | **Condição 1 da Spec #14 §7.5.** Se o servidor cair entre as duas gravações, o reenvio encontra o `SyncId` e responde "já existe" — **o saldo inicial nunca é criado** | Insumo e movimentação num **único `SaveChanges`**, via navegação `Movements` (E1, mesma solução da S1 da 14.2) |
| 2 | O saldo inicial recebe `SyncId` do banco e o `POST` não devolve o `Id` dele | **Condição 2:** o app não consegue casar a entrada do pull com a local, nem endereçar uma correção dela feita antes do primeiro sync | `initialMovementSyncId` na entrada e `initialMovement { id, syncId }` na resposta (E1, mesma solução da M2 da 14.2) |
| 3 | `StockItemService.DeactivateAsync` e `StockMovementService.DeactivateAsync` lançam `ConflictException` se já inativos | Violam a A6: o reenvio vira `409`, a fila trava | Retornam sem gravar (Fases 4 e 8) |
| 4 | **Provável bug pré-existente** no `StockProfile` (edição): `ForAllMembers(... srcMember != null)` com `StockCategoryId` e `UnitOfMeasureId` `int?` no DTO e `int` na entidade — o mesmo par anulável → não anulável que causou o bug do `RequiresBooster` (14.1 §1.1 item 7). O `srcMember` chegaria convertido para `0` e a condição passaria | Um `PATCH` só do `name` gravaria `StockCategoryId = 0` → violação de FK → `500`. No offline, o `500` é tentado de novo até o limite e trava a fila | **A confirmar na validação da Fase 3.** Se confirmado: `PreCondition(src => src.X.HasValue)` nos dois campos, como na 14.1. `ReorderPoint` (`decimal?`→`decimal?`) e `ReplenishmentLeadDays` (`int?`→`int?`) não são afetados |
| 5 | O mesmo profile de edição mapeia **todo** membro não nulo do DTO | Com `UpdatedAt` no DTO de edição, o valor bruto do cliente seria copiado para a entidade antes do LWW | Profile ignora `UpdatedAt`, `SyncId` e `RowVersion`; LWW no service (Fase 3) |
| 6 | `StockItemDto` tem `StockCategory` e `UnitOfMeasure` (navegações) e seis campos derivados (`CurrentBalance`, `StockValue`, `AverageUnitCost`, `DaysOfCoverage`, `EstimatedRunOutDate`, `AlertSeverity`). O helper `GetChangesSinceAsync` não faz `Include`, e o `ComposeDetailAsync` faz 3 consultas **por item** | No pull: categoria e unidade viriam `null`; com 500 itens, seriam 1.500 consultas | Navegações pela tabela de referência (E4); derivados em lote com os métodos `*BatchAsync` que já existem (E3) |
| 7 | Movimentações não alteram o `RowVersion` do insumo | Os derivados do item 6 recebidos no pull ficam desatualizados até o próprio insumo mudar | E3 — mesmo tratamento da S3 da 14.2 |
| 8 | `StockMovementDto.StockItemName` e `UnitAbbreviation` vêm da navegação `StockItem.UnitOfMeasure` | No pull, viriam vazios | Consulta em lote dos insumos da página (E9) |
| 9 | `StockMovementService.UpdateAsync` atribui campo a campo (sem AutoMapper) e a checagem de data futura (R5) está intercalada com as atribuições | Precisa do LWW sem mudar a ordem das regras | R5 sobe para antes do LWW (nada é gravado antes dela, então o resultado é o mesmo); sem risco do bug do `ForAllMembers` |
| 10 | **Achado (bug pré-existente, igual ao Q1 da 14.2):** `GetByIdAsync(movementId)` não confere se a movimentação pertence ao `stockItemId` da rota | Nenhum direto: o app sempre envia o par certo | **Não corrigido aqui** (premissa da §2). Questão em aberto Q1 (§9) |
| 11 | `UnitCostSnapshot` das saídas é calculado no momento em que o servidor processa o `POST` (R7) | Uma compra feita offline e sincronizada depois de saídas de outro dispositivo muda o custo médio vigente na hora do processamento | Regra mantida (E7). Risco já registrado em `spec-estoque.md` (Fora de escopo: "risco de compra sincronizada em atraso documentado — não corrompe retroativo") |
| 12 | O `AccountRepository` remove insumos e movimentações fisicamente | Só na exclusão da conta (Spec #14 §6 item 10) | Sem mudança |
| 13 | `StockItemService` depende de `IStockMovementService` **só** para `CreateOpeningBalanceAsync` | Com E1, a dependência deixa de ter uso | Troca por `IStockMovementRepository` (já injetado); `CreateOpeningBalanceAsync` é removido. Construtor muda; a DI resolve sozinha, **sem mudança no `Program.cs`** |

## 4. Decisões (aprovadas em 08/Out/2026)

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| E1 | **Saldo inicial via navegação `Movements`, num único `SaveChanges`** (S1 da 14.2), **com `SyncId` gerado pelo app** (M2 da 14.2): `StockItemCreateDto.InitialMovementSyncId` (`Guid?`; ausente → servidor gera; sem `initialQuantity` → ignorado, sem erro). A resposta do `POST` passa a ser `StockItemCreatedDto : StockItemDto` com `StockMovementRefDto? InitialMovement` (`Id`, `SyncId`). O reenvio localiza a entrada pelo `initialMovementSyncId` **desde que pertença ao insumo** | Transação explícita no service (exigiria expor transação no repositório). Enviar o saldo inicial como `POST /movements` separado (mudaria o fluxo de cadastro). Generalizar o `SemenSampleMovementRefDto` num `SyncRefDto` comum (mexeria em código já validado da 14.2 — pode ser feito numa limpeza futura) |
| E2 | **Inativação do insumo e da movimentação sem LWW**, as duas idempotentes (`204` sem gravar) | Inativação com `updatedAt` no corpo — regra diferente da Spec #14 §5.4 |
| E3 | **Derivados vão no pull, calculados em lote no momento do pull**, e o app os trata como **valor inicial**, exibido só até o pull de movimentações terminar. Depois disso, o app **deve** exibir saldo e valor recalculados sobre as movimentações (R10, E6), e previsão e alerta recalculados com as regras de `StockForecastResolver` (função pura: saldo, consumo de 30 dias, ponto crítico, prazo de reposição, hoje) — §5 itens 3 e 4. O cálculo em lote dá o mesmo resultado do cálculo item a item; a defasagem possível vem do `RowVersion` (§3 item 7), não do lote | Tirar os derivados do pull (quebra "mesmo DTO do detalhe", Spec #14 §5.5). Avançar o `RowVersion` do insumo a cada movimentação (gravação extra e contenção na mesma linha) |
| E4 | **Categoria e unidade no pull preenchidas pela tabela de referência**: `IStockReferenceRepository.GetCategoriesAsync`/`GetUnitsAsync` (já injetado no `StockItemService`; 8 e 7 linhas) — duas consultas por página, independente do tamanho | `Include` no helper genérico `GetChangesSinceAsync` (compartilhado por todas as entidades). Consulta extra dos insumos da página com `Include` (traz de novo o que já foi lido) |
| E5 | **Data do saldo inicial continua sendo a do processamento** (R11). Um insumo cadastrado offline no dia X e sincronizado no dia X+3 terá o saldo inicial datado X+3. Efeito só de exibição: `OpeningBalance` não entra em `PeriodSpent` (só `Purchase`) nem no consumo/previsão (só `Consumption`), e o saldo não depende de data | Aceitar `initialMovementDate` no cadastro — muda o contrato e a regra R11. Pode virar ajuste de domínio separado se o usuário sentir falta |
| E6 | **Pull global de movimentações:** `GET /api/stock-items/movements/changes` traz as movimentações de **todos** os insumos da propriedade, inclusive as inativas (M1 da 14.2). Com elas o app calcula saldo e valor localmente (R10) | Pull por insumo (um request por insumo, ruim em link instável). Rota em `/api/stock/movements/changes` (o `StockController` só tem leitura agregada; os recursos ficam em `/api/stock-items`) |
| E7 | **`UnitCostSnapshot` continua sendo calculado pelo servidor ao processar** (R7, D8). Saídas e ajustes de entrada criados offline ficam **sem custo definitivo** até o sync; o app pode exibir uma estimativa pelo custo médio local, substituída pelo valor do servidor no pull. O reenvio devolve a existente **sem recalcular** (o `SyncId` é checado antes da valoração) | Aceitar `unitCostSnapshot` do cliente — duas fontes de verdade para o custo médio e regra nova. Recalcular snapshots por `MovementDate` — contraria D8/D13 |
| E8 | **`DELETE` de movimentação repetido → `204` sem gravar**, checado depois de R1/R3. Não há movimentação gerada pelo sistema no estoque (nenhum outro service cria `StockMovement`), então não existe o equivalente a R5 da 14.2 | — |
| E9 | **`StockItemName`/`UnitAbbreviation` no pull de movimentações por consulta em lote**: `IStockItemRepository.GetByIdsAsync(ids)` com `Include(UnitOfMeasure)`, sem filtro de `IsActive` (o tenant vem do `HasQueryFilter`); o service atribui `movement.StockItem` antes do mapeamento — o profile não muda | Dicionário só de nomes (como a M4 da 14.2) — aqui são dois campos vindos de duas tabelas; atribuir a navegação reaproveita o mapeamento existente |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** o insumo criado offline é enviado **antes** das suas movimentações; o `Id` do `201` é usado na rota das movimentações (A2, A5).
2. **Saldo inicial offline (E1):** um único item na fila — `POST /api/stock-items` com `syncId`, `initialQuantity`, `initialTotalValue`, `initialNotes` e `initialMovementSyncId`. O app grava localmente o insumo **e** a entrada; no `201`, grava o `ServerId` dos dois (`id` e `initialMovement.id`). Correção ou inativação da entrada feita antes do sync vai para a fila **depois** do `POST` do insumo e usa `initialMovement.id` na rota.
3. **Saldo e valor exibidos (obrigatório):** o app **deve** exibir saldo e valor calculados pela fórmula R10 sobre as movimentações locais ativas, nunca o `currentBalance`/`stockValue` do pull depois de carregar as movimentações. Para saídas ainda não sincronizadas, o valor usa o custo médio local como estimativa (E7); o pull traz o `unitCost` definitivo.
4. **Previsão e alerta exibidos (obrigatório):** o app **deve** recalcular `daysOfCoverage`, `estimatedRunOutDate` e `alertSeverity` localmente, com as regras de `StockForecastResolver` (`spec-estoque.md`); o consumo considerado são as saídas `Consumption` ativas dos últimos 30 dias. A previsão muda com o passar dos dias mesmo sem gravação, então a do pull envelhece. Os valores do pull servem só como valor inicial (E3).
5. **Nome e unidade nas movimentações:** exibir pelo insumo local (`stockItemId`), não pelos campos `stockItemName`/`unitAbbreviation` da movimentação, que não são atualizados quando o insumo é renomeado (Fase 9).
6. **Categorias e unidades:** carregadas pelas rotas de referência existentes (`GET /api/stock-categories` e `GET /api/units-of-measure`) e guardadas localmente; são globais e praticamente estáticas.
7. **`409` de R4** (outro dispositivo inativou o insumo antes do sync) é **conflito real**: o item vira `Failed` e o usuário é avisado (A3).
8. **`422` de R5 no `PATCH`** só ocorre se o relógio do celular estiver adiantado a ponto de virar o dia em UTC — erro definitivo (A3).

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (as duas tabelas) | ✅ Migração `20261009001106_Offline_Stock_SyncId_RowVersion` aplicada |
| 2 — Insumo: criação idempotente e atômica (E1) | ✅ Implementada e validada |
| 3 — Insumo: edição com last-write-wins (+ item 4 da §3) | ✅ Implementada e validada (bug do item 4 confirmado e corrigido) |
| 4 — Insumo: inativação idempotente (E2) | ✅ Implementada e validada |
| 5 — Insumo: pull incremental (E3, E4) | ✅ Implementada e validada |
| 6 — Movimentação: criação idempotente | ✅ Implementada e validada |
| 7 — Movimentação: edição com last-write-wins | ✅ Implementada e validada |
| 8 — Movimentação: inativação idempotente (E8) | ✅ Implementada e validada |
| 9 — Movimentação: pull incremental (E6, E9) | ✅ Implementada e validada |
| 10 — Testes | ✅ 19 testes de `StockItemService` + 18 de `StockMovementService`; 113 no projeto |
| 11 — Documentação | ✅ Spec #14 (v1.2), `catalogo-erros-api.md` §4.14, `spec-estoque.md` |

Branch: `feature/offline-stock`, criada a partir de `feature/offline-semen-samples` (ou do `dev`, depois do merge do sêmen), para que a migração fique em cima da `Offline_SemenSampleMovement_SyncId_RowVersion`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, como nas specs anteriores, com uma conta de teste própria.

### 6.1 Fase 1 — Domínio e banco

- `StockItem : BaseEntity, ITenantEntity, ISyncable` e `StockMovement : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<StockItem>().ConfigureSyncable();` e `builder.Entity<StockMovement>().ConfigureSyncable();` (índices existentes permanecem).
- Migração **única** `Offline_Stock_SyncId_RowVersion` (⚠️ **requer aprovação**). SQL esperado:

```sql
ALTER TABLE [StockItems] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [StockItems] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
ALTER TABLE [StockMovements] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [StockMovements] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_StockItems_PropertyId_RowVersion] ON [StockItems] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_StockItems_SyncId] ON [StockItems] ([SyncId]);
CREATE INDEX [IX_StockMovements_PropertyId_RowVersion] ON [StockMovements] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_StockMovements_SyncId] ON [StockMovements] ([SyncId]);
```

| Validação (08/Out/2026) | Resultado |
|---|---|
| Insumos e movimentações existentes ganham `SyncId` distintos, nenhum vazio, e `RowVersion` preenchido | ✅ 1 insumo e 3 movimentações; `SyncId` distintos, nenhum vazio; `RowVersion` 38185–38188 |
| Índices novos criados; existentes mantidos | ✅ `StockItems`: `PK`, os 4 existentes, `IX_StockItems_PropertyId_RowVersion`, `UX_StockItems_SyncId`. `StockMovements`: `PK`, os 4 existentes, `IX_StockMovements_PropertyId_RowVersion`, `UX_StockMovements_SyncId` |
| Nenhuma outra tabela alterada (inclusive `StockCategories` e `UnitsOfMeasure`) | ✅ A migração e o snapshot (+36 linhas) só tocam `StockItems` e `StockMovements`; `Down` remove os 4 índices e as 4 colunas |
| Cadastro com saldo inicial e movimentação manual continuam gravando, com `SyncId` gerado pelo banco | ✅ Insumo 2 com `initialQuantity: 100` e `initialTotalValue: 250` → `201`, saldo 100, valor 250, custo médio 2,50; saída de consumo de 10 → `201`, `unitCost` 2,50; `GET` → saldo 90, valor 225. Insumo e as 2 movimentações com `SyncId` gerado e `RowVersion` (38193–38195) |

- SQL gerado conferido com `dotnet ef migrations script` antes do `database update`: idêntico ao esperado (só muda a ordem das tabelas).
- Conta de teste: `teste.offline.estoque@muuboi.local` (propriedade "Fazenda Teste Offline Estoque"). Insumo de teste (Id 2) inativado ao final.

### 6.2 Fase 2 — Insumo: criação idempotente e atômica

- `StockItemCreateDto`: `Guid? SyncId` e `Guid? InitialMovementSyncId` + `IValidatableObject` (`Guid.Empty` → `400` "O identificador de sincronização não pode ser vazio." / "O identificador de sincronização da entrada inicial não pode ser vazio.").
- `StockItemDto`: `Guid SyncId`.
- `StockItemCreatedDto : StockItemDto` (**novo**) com `StockMovementRefDto? InitialMovement`; `StockMovementRefDto` (**novo**: `Id`, `SyncId`).
- `StockProfile`: criação ignora `SyncId` e `RowVersion`; `CreateMap<StockItem, StockItemCreatedDto>().IncludeBase<StockItem, StockItemDto>()` ignorando `InitialMovement`.
- `IStockItemRepository`/repositório:
  - `GetBySyncIdAsync` com `Include(StockCategory)` e `Include(UnitOfMeasure)` (a resposta precisa das navegações — por isso não usa o `FindBySyncIdAsync` puro);
  - `CreateAsync` → `AddSyncableAsync`.
- `IStockMovementRepository.GetBySyncIdAsync` (também usado na Fase 6).
- `StockItemService.CreateAsync`:

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return await ComposeCreatedAsync(existing, dto.InitialMovementSyncId);
}

await ValidateReferencesAsync(dto.StockCategoryId, dto.UnitOfMeasureId);

var item = _mapper.Map<StockItem>(dto);
item.SyncId = dto.SyncId ?? Guid.NewGuid();

if (dto.InitialQuantity.HasValue)
    item.Movements = new List<StockMovement>
    {
        new()
        {
            SyncId = dto.InitialMovementSyncId ?? Guid.NewGuid(),
            MovementType = StockMovementType.Input,
            MovementReason = StockMovementReason.OpeningBalance,
            MovementDate = DateTime.UtcNow,
            Quantity = dto.InitialQuantity.Value,
            TotalValue = dto.InitialTotalValue,
            ValueEntryMode = dto.InitialTotalValue.HasValue ? ValueEntryMode.TotalPrice : null,
            Notes = dto.InitialNotes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }
    };

var created = await _repository.CreateAsync(item);
var reloaded = await _repository.GetByIdAsync(created.Id) ?? created;
return await ComposeCreatedAsync(reloaded, item.Movements?.FirstOrDefault()?.SyncId);
```

- Os campos da movimentação são **os mesmos** do `CreateOpeningBalanceAsync` atual (R11); o método é removido de `IStockMovementService`/`StockMovementService` e o `StockItemService` deixa de depender de `IStockMovementService` (§3 item 13).
- `ComposeCreatedAsync(item, initialMovementSyncId)`: monta o detalhe como `ComposeDetailAsync` e, se houver `initialMovementSyncId`, preenche `InitialMovement` com a movimentação encontrada **desde que `StockItemId == item.Id`**.
- `StockItemsController.Create`: `ActionResult<StockItemCreatedDto>`; `CreatedAtAction` igual.

| Validação (08/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId`, `initialQuantity: 50`, `initialTotalValue: 100` e `initialMovementSyncId` | ✅ `201`; insumo 3 com o `syncId` do app; `initialMovement { id: 1004, syncId }` com o `SyncId` do app; saldo 50, valor 100, custo médio 2 |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id` (3) e mesmo `initialMovement`; 1 insumo + 1 movimentação |
| Mesmo `syncId`, payload diferente (`name` e `initialQuantity: 99`) | ✅ `201`, devolve o existente sem alterar (nome original, saldo 50) |
| Reenvio sem `initialMovementSyncId` | ✅ `201`, `initialMovement: null`; nada duplicado |
| `POST` sem `syncId` (Swagger/chamada direta; o app sempre envia) | ✅ `201`, `syncId` gerado; `initialMovement` preenchido com `SyncId` gerado (movimentação 1005) |
| `POST` sem `initialQuantity`, com `initialMovementSyncId` | ✅ `201`, saldo 0, `initialMovement: null`, nenhuma movimentação |
| `syncId` / `initialMovementSyncId` vazios | ✅ `400` (formato B) nos campos `SyncId` / `InitialMovementSyncId`, com as mensagens acima |
| R2: categoria/unidade inexistentes | ✅ `404` "Categoria com id '999' não encontrada." / "Unidade de medida com id '999' não encontrada." (regra mantida) |
| Reenvio de `syncId` existente com categoria inválida | ✅ `201` com o existente — o `SyncId` é checado antes de R2 |
| `initialTotalValue` informado | ✅ Movimentação `OpeningBalance` com `ValueEntryMode = 2` (`TotalPrice`), `totalValue` 100, `unitCost` 2, notas preservadas (R11) |
| `PropertyId` da movimentação | ✅ Igual ao do insumo em todos os casos (override de `SaveChangesAsync`) |
| Falha simulada na movimentação (trigger temporário `THROW` em `StockMovements`, removido após o teste) | ✅ `500`; **nenhum** insumo nem movimentação com os `SyncId` enviados — o identity pulou o `Id` 7, confirmando o rollback |
| Reenvio do `POST` que falhou | ✅ `201`, insumo 8, saldo 3, `initialMovement` com o `SyncId` do app — o app se recupera sozinho depois do `5xx` |
| 20 envios simultâneos (mesmo `syncId`, `initialQuantity: 7`) | ✅ Todos `201`; 1 insumo (6) + 1 movimentação (1006) |
| `PATCH /3/movements/1004` (pela `initialMovement.id`) logo após o `POST` | ✅ `200`; quantidade 50 → 60; saldo 60 — a entrada é endereçável pelo app (Condição 2) |
| Testes existentes | ✅ 76/76 passando após a mudança |

- Como nas specs anteriores, o `catch` da corrida no `AddSyncableAsync` **não foi exercitado** aqui: 4 `INSERT`s em `StockItems` no log, nenhuma violação de `UX_StockItems_SyncId`. Todos os simultâneos foram barrados pela checagem do service.
- Validação com scripts Node (`fetch`) na conta `teste.offline.estoque@muuboi.local`. Registros de teste (insumos 3, 4, 5, 6 e 8) inativados ao final.

> **Ponto de atenção herdado da 14.2 §2.2:** na corrida, o `AddSyncableAsync` desanexa só o insumo; a movimentação filha continua `Added` no `ChangeTracker`. Aqui há um `GetByIdAsync` depois do `CreateAsync`, mas é leitura — nenhum outro `SaveChanges` ocorre na requisição. Continua inofensivo; **não** mexer no helper.

### 6.3 Fase 3 — Insumo: edição com last-write-wins

- `StockItemUpdateDto`: `DateTime? UpdatedAt`.
- `StockProfile` (edição): `UpdatedAt`, `SyncId` e `RowVersion` ignorados (§3 item 5). Se a validação confirmar o item 4 da §3: `PreCondition(src => src.StockCategoryId.HasValue)` e `PreCondition(src => src.UnitOfMeasureId.HasValue)`.
- `StockItemService.UpdateAsync`: R1 e R2 continuam idênticos e vêm **antes** do LWW.

```csharp
var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, item))
    return await ComposeDetailAsync(item);

_mapper.Map(dto, item);
item.UpdatedAt = editedAt;
```

- `PATCH` em insumo inativo continua aceito (edita sem reativar — Spec #14 §5.4).

| Validação (08/Out/2026) | Resultado |
|---|---|
| **Antes da mudança:** `PATCH /9` só com `name` | ❌ **Bug confirmado:** `500`; log com violação de `FK_StockItems_StockCategories_StockCategoryId` — o `StockCategoryId` virou `0`. Com `stockCategoryId` e `unitOfMeasureId` no corpo, o `PATCH` funcionava |
| **Depois da mudança:** `PATCH /9` só com `name` | ✅ `200`; categoria (3), unidade (2), ponto crítico (10) e prazo (5) preservados |
| `updatedAt` UTC mais novo | ✅ Aplicado; `updatedAt` = horário do cliente (`00:27:28.472Z`) |
| `updatedAt` com fuso `-03:00` | ✅ `21:27:29.472-03:00` gravado como `00:27:29.472Z`; aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| Edição mais antiga (anterior à última aplicada; e −1 h com `reorderPoint: 999`) | ✅ `200`, ignorada; devolve a versão do servidor (nome e ponto crítico mantidos) |
| Sem `updatedAt` (Swagger/chamada direta) | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) e `updatedAt` 1–2 s no futuro | ✅ Aplicado; `updatedAt` limitado a "agora" |
| `PATCH` só `notes` | ✅ Nome, categoria, unidade, ponto crítico e prazo preservados |
| `PATCH` só `stockCategoryId: 4` | ✅ Categoria trocada; unidade (2) preservada — confirma a `PreCondition` nos dois campos |
| R1 / R2 | ✅ `404` "Insumo com id '999999' não encontrado." / "Categoria com id '999' não encontrada." / "Unidade de medida com id '999' não encontrada." |
| Testes existentes | ✅ 76/76 |

- O bug do item 4 da §3 afetava **qualquer** `PATCH` parcial do insumo que não trouxesse categoria e unidade (inclusive fora do offline). Correção no `StockProfile`: `PreCondition(src => src.StockCategoryId.HasValue)` e `PreCondition(src => src.UnitOfMeasureId.HasValue)`, como no `RequiresBooster` da 14.1. Sem teste unitário (mapeamento — `CLAUDE.md`); coberto por esta validação.
- O primeiro roteiro mandou `updatedAt` 1–2 s **à frente** do relógio do servidor; o limite a "agora" agiu (comportamento correto), mas não provava o uso do horário do cliente. Refeito com horários no passado, mais novos que a última edição.
- Quando a edição é ignorada, o `updatedAt` da resposta sai sem `Z` (lido do banco) — mesma diferença de formato já registrada na Spec #14 §10.2.
- Insumo de teste (Id 9) mantido ativo para a Fase 4.

### 6.4 Fase 4 — Insumo: inativação idempotente

```csharp
if (!item.IsActive)
    return;
```

- A mensagem "O insumo já está inativo." deixa de existir.

| Validação (08/Out/2026) | Resultado |
|---|---|
| `DELETE` em insumo ativo (Id 9) | ✅ `204`; `IsActive=0`, `RowVersion` 38231 → 38232 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` (`00:29:40.765`) e `RowVersion` (38232) **inalterados** |
| Id inexistente | ✅ `404` "Insumo com id '999999' não encontrado." (R1) |
| R4: `POST` de movimentação no insumo inativo | ✅ Continua `409` "Não é possível registrar movimentação para um insumo inativo."; nada gravado |
| Testes existentes | ✅ 76/76 |

- A mensagem "O insumo já está inativo." deixa de existir (remover do `catalogo-erros-api.md` §4.14 na Fase 11).
- Insumo de teste (Id 9) termina inativo.

### 6.5 Fase 5 — Insumo: pull incremental

- `IStockItemRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<StockItem>`.
- `IStockItemService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<StockItemDto>`:
  1. `TryDecodeCursor` (`400` "Cursor de sincronização inválido.") → `ResolveLimit` → repositório com `take + 1`;
  2. categorias e unidades pelo `IStockReferenceRepository`, atribuídas às navegações antes do mapeamento (E4);
  3. `BuildPage`;
  4. derivados em lote sobre os itens da página com `GetLevelsBatchAsync` e `GetConsumptionSinceBatchAsync` (E3) — o cálculo por item sai do `ComposeDetailAsync` para um método privado compartilhado, para detalhe e pull usarem a mesma conta.
- `StockItemsController`: `[HttpGet("changes")]` — sem colisão com `{id:int}`.

| Validação (08/Out/2026) | Resultado |
|---|---|
| Pull inicial sem `since` | ✅ 8 insumos (7 inativos de testes anteriores + o 10, com saldo e consumo), em ordem de `RowVersion`; `nextCursor` `38233`, `hasMore: false` |
| Pull × `GET /{id}`, item a item (JSON completo) | ✅ **Nenhuma divergência** — categoria, unidade e todos os derivados iguais. Insumo 10 (100 de entrada a R$ 300, consumo de 30, ponto crítico 80, prazo 10 dias): saldo 70, valor 210, custo médio 3, cobertura 70 dias, ruptura 18/12/2026, alerta "Atenção" |
| Isolamento por propriedade | ✅ A tabela tinha 11 insumos; vieram só os 10 da propriedade do usuário (8 no pull inicial + os 2 criados depois) |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido (`38233`) |
| Cria A (11), edita A, cria B (12), inativa B → pull | ✅ Só A (ativo, nome final "F5 A editado") e B (inativo), cada um **uma vez** |
| Entrada de 10 em A → pull de insumos | ✅ `items: []` — A **não** reaparece (E3); `GET /11` mostra saldo 10 |
| Paginação `limit=1` | ✅ 10 páginas, 10 itens distintos, mesma ordem do pull completo; última com `hasMore: false` |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |
| Número de consultas por página (log do EF) | ✅ **5** com 1 item e **5** com 10 itens — pull + categorias + unidades + saldos + consumo, constante |
| Testes existentes | ✅ 76/76 |

- O cálculo dos derivados saiu do `ComposeDetailAsync` para `FillDerived` (estático), usado pelo detalhe e pelo pull; a comparação item a item confirma que os dois dão o mesmo resultado.
- **Limitação registrada (E4):** `GetCategoriesAsync`/`GetUnitsAsync` filtram `IsActive`. Hoje todas as categorias e unidades são do seed, ativas, e nenhuma rota as inativa. Se uma fosse inativada, o pull traria `stockCategory`/`unitOfMeasure` `null` para os insumos dela, enquanto o `GET /{id}` (com `Include`) continuaria trazendo. Aceito.
- Registros de teste (insumos 10, 11 e 12) inativados ao final.

### 6.6 Fase 6 — Movimentação: criação idempotente

- `StockMovementCreateDto`: `Guid? SyncId` + validação de `Guid.Empty` no `Validate` existente.
- `StockMovementDto`: `Guid SyncId`.
- `StockProfile` (criação de movimentação): ignora `SyncId` e `RowVersion`.
- Repositório: `GetBySyncIdAsync` com `Include(StockItem).ThenInclude(UnitOfMeasure)`; `CreateAsync` → `AddSyncableAsync`.
- `StockMovementService.CreateAsync`: só o bloco do `SyncId` é novo, **antes** de R1, R4 e da valoração (R7) — o reenvio não recalcula o `UnitCostSnapshot` (E7).

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return _mapper.Map<StockMovementDto>(existing);
}

// R1, R4, mapeamento e ApplyValuationAsync como hoje
movement.SyncId = dto.SyncId ?? Guid.NewGuid();
```

| Validação (08/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` (compra de 100 por R$ 200) no insumo 13 | ✅ `201`, `Id` 1011, `syncId` do app, `stockItemName` e `unitAbbreviation` preenchidos; saldo 0 → 100, valor 200 |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id`; saldo e valor **inalterados** |
| Mesmo `syncId`, payload diferente (999 por R$ 1) | ✅ `201`, devolve a existente sem alterar |
| Saída de 10 com `syncId` → compra de 10 por R$ 500 (custo médio 2 → 6,8) → reenvio da saída | ✅ Saída `201` com `unitCost` 2; reenvio `201`, mesmo `Id` (1012), `unitCost` **continua 2** (E7) |
| `POST` sem `syncId` (Swagger/chamada direta) | ✅ `201`, `syncId` gerado; `unitCost` 6,8 (custo médio vigente) |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (campo `SyncId`) |
| R5: data futura | ✅ `400` "A data da movimentação não pode ser futura." |
| R6: motivo × tipo / compra sem valor | ✅ `400` "O motivo 'Consumo' exige movimentação do tipo 'Saída'." / "O valor total é obrigatório para compra ou saldo inicial." |
| R1: insumo inexistente | ✅ `404` "Insumo com id '999999' não encontrado." |
| R4: `POST` novo em insumo inativo | ✅ `409` "Não é possível registrar movimentação para um insumo inativo." |
| Reenvio de `POST` já gravado, depois de inativar o insumo | ✅ `201` com a existente (`Id` 1011) — `SyncId` checado antes de R1/R4 |
| 20 envios simultâneos (mesmo `syncId`, saída de 2) | ✅ Todos `201`; 1 linha (`Id` 1015) |
| Testes existentes | ✅ 76/76 |

- **O `catch` da corrida foi exercitado:** nos 20 simultâneos, 3 requisições passaram juntas pela checagem do service; o banco barrou o `INSERT` em `UX_StockMovements_SyncId` (3 violações no log, cada uma registrada 2 vezes pelo EF; identity pulou de 1015 para 1018) e o `AddSyncableAsync` devolveu a existente. Nenhum erro não tratado; 1 linha por `syncId`.
- Registro de teste (insumo 13) termina inativo; as movimentações 1011–1015 ficam ativas para a Fase 7.

### 6.7 Fase 7 — Movimentação: edição com last-write-wins

- `StockMovementUpdateDto`: `DateTime? UpdatedAt`.
- `StockMovementService.UpdateAsync`: R1, R3 e R5 (data futura, agora checada **antes** das atribuições — §3 item 9) vêm antes do LWW; as atribuições campo a campo não mudam.

```csharp
if (dto.MovementDate.HasValue && dto.MovementDate.Value.Date > DateTime.UtcNow.Date)
    throw new BusinessRuleException("A data da movimentação não pode ser futura.");

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, movement))
    return _mapper.Map<StockMovementDto>(movement);

// MovementDate, Quantity, TotalValue e Notes como hoje

movement.UpdatedAt = editedAt;
```

| Validação (08/Out/2026) | Resultado |
|---|---|
| `updatedAt` UTC mais novo (compra 1011 do insumo 13, **inativo**) | ✅ Aplicado; `updatedAt` = horário do cliente (`00:42:30.895Z`). Também confirma R9: `PATCH` não exige insumo ativo |
| `updatedAt` com fuso `-03:00` | ✅ `21:42:31.895-03:00` gravado como `00:42:31.895Z`; aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| Edição mais antiga (com `quantity: 999`) | ✅ `200`, ignorada; quantidade 100 e saldo 97 mantidos |
| Sem `updatedAt` (Swagger/chamada direta) / relógio adiantado (+1 dia) | ✅ Aplicado com "agora" / limitado a "agora" |
| Editar a quantidade da saída 1012 (10 → 20) | ✅ Saldo 97 → 87, valor 659,6 → 639,6; `unitCost` **continua 2** (R8) |
| R5: `PATCH` com data futura | ✅ `422` "A data da movimentação não pode ser futura." (regra mantida) |
| R1 / R3 | ✅ `404` "Insumo com id '999999' não encontrado." / "Movimentação com id '999999' não encontrada." |
| `PATCH` em movimentação inativa (1014) | ✅ `200`, aceito como hoje (R9); continua inativa |
| Testes existentes | ✅ 76/76 |

- Como no insumo, quando a edição é ignorada o `updatedAt` da resposta sai sem `Z` (Spec #14 §10.2).

### 6.8 Fase 8 — Movimentação: inativação idempotente

```csharp
if (!movement.IsActive)
    return;
```

| Validação (08/Out/2026) | Resultado |
|---|---|
| `DELETE` na compra 1013 (10 por R$ 500) do insumo 13 | ✅ `204`; `IsActive=0`, `RowVersion` 38249 → 38265; saldo 88 → 78, valor 646,4 → 146,4 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` (`00:45:38.386`) e `RowVersion` (38265) **inalterados** |
| Inativar o saldo inicial pela `initialMovement.id` (insumo 14, entrada 1019 de 40 por R$ 80) | ✅ `204`; saldo 40 → 0, valor 0 |
| Ids inexistentes | ✅ `404` "Insumo com id '999999' não encontrado." (R1) / "Movimentação com id '999999' não encontrada." (R3) |
| Testes existentes | ✅ 76/76 |

- A mensagem "A movimentação já está inativa." deixa de existir (remover do `catalogo-erros-api.md` §4.14 na Fase 11).
- Registros de teste: insumo 14 inativado; insumo 13 continua inativo, com as movimentações 1011, 1012 e 1015 ativas e 1013 e 1014 inativas (usadas na Fase 9).

### 6.9 Fase 9 — Movimentação: pull incremental

- `IStockMovementRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<StockMovement>`.
- `IStockItemRepository.GetByIdsAsync(IEnumerable<int> ids)` com `Include(UnitOfMeasure)` (E9).
- `IStockMovementService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<StockMovementDto>`; atribui `movement.StockItem` pelo dicionário antes do `BuildPage`.
- `StockItemsController`: `[HttpGet("movements/changes")]` — sem colisão com `{id:int}` nem com `{stockItemId:int}/movements`.

| Validação (08/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 17 movimentações (3 inativas: 1013, 1014, 1019), todas com `syncId`, `stockItemName` e `unitAbbreviation`; `nextCursor` `38272`, `hasMore: false` |
| Pull × `GET /{stockItemId}/movements/{id}`, item a item (JSON completo) | ✅ **Nenhuma divergência** |
| Isolamento por propriedade | ✅ A tabela tinha 22 movimentações; vieram só as 19 da propriedade do usuário (17 + 2 criadas depois) |
| Saldo inicial da E1 | ✅ Movimentação 1020 chega com o `syncId` gerado pelo app, motivo "Saldo inicial", nome e unidade (`L`) do insumo 15 |
| **Saldo e valor calculados sobre o pull = `currentBalance` e `stockValue` do servidor** | ✅ 10 insumos, **nenhuma divergência** — confirma que o app calcula saldo e valor offline com a fórmula R10 (E6), inclusive com movimentações inativas e insumos inativos |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido |
| Cria A (1022), edita A, cria B (1023), inativa B → pull | ✅ Só A (ativa, quantidade final 4) e B (inativa), cada uma **uma vez** |
| Renomear o insumo → pull de movimentações | ✅ `items: []` — as movimentações **não** reaparecem (ver nota) |
| Paginação `limit=1` | ✅ 19 páginas, 19 itens distintos, mesma ordem do pull completo |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |
| Número de consultas por página (log do EF) | ✅ **2** com 1 item e **2** com 19 itens — pull + insumos da página, constante |
| Testes existentes | ✅ 76/76 |

- **Nota para o app (complementa a §5):** `stockItemName` e `unitAbbreviation` da movimentação são cópia do insumo no momento do pull. Renomear o insumo (ou trocar a unidade) avança só o `RowVersion` do insumo, então as movimentações já baixadas ficam com o nome antigo. O app deve exibir nome e unidade **a partir do insumo local** (pelo `stockItemId`), não do campo da movimentação — mesmo raciocínio da E3.
- Registros de teste (insumo 15) inativados ao final.

### 6.10 Fase 10 — Testes

`MuuBoi.Tests/Services/StockItemServiceTests.cs` e `StockMovementServiceTests.cs` (**novos**) — padrão de `MilkProductionServiceTests`/`VaccineServiceTests` (repositórios mockados, AutoMapper real com `StockProfile`, sem testar mapeamento).

**`StockItemServiceTests`**

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesItemWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotValidateReferences` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAsync_WithInitialQuantity_CreatesItemWithOpeningBalanceMovementInSameCall` |
| 6 | `CreateAsync_WithInitialMovementSyncId_CreatesMovementWithGivenSyncIdAndReturnsIt` |
| 7 | `CreateAsync_WithExistingSyncIdAndInitialMovementSyncId_ReturnsSameInitialMovement` |
| 8 | `CreateAsync_WithoutInitialQuantity_ReturnsNullInitialMovement` |
| 9 | `CreateAsync_WhenCategoryNotFound_ThrowsNotFoundException` (R2) |
| 10 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 11 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 12 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` |
| 13 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 14 | `UpdateAsync_WhenItemNotFound_ThrowsNotFoundException` (R1) |
| 15 | `DeactivateAsync_WhenActive_Deactivates` |
| 16 | `DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating` |
| 17 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 18 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 19 | `GetChangesAsync_FillsDerivedFieldsAndReferencesForPageItems` (inclui categoria e unidade — E4) |

**`StockMovementServiceTests`**

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesMovementWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotRecalculateValuation` (E7) |
| 4 | `CreateAsync_WithExistingSyncIdAndInactiveItem_ReturnsExisting` |
| 5 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 6 | `CreateAsync_WhenItemNotFound_ThrowsNotFoundException` (R1) |
| 7 | `CreateAsync_WhenItemInactive_ThrowsConflictException` (R4) |
| 8 | `CreateAsync_WithOutput_FreezesCurrentAverageUnitCost` (R7) |
| 9 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 10 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 11 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` |
| 12 | `UpdateAsync_WithFutureDate_ThrowsBusinessRuleException` (R5) |
| 13 | `UpdateAsync_WhenMovementNotFound_ThrowsNotFoundException` (R3) |
| 14 | `DeactivateAsync_WhenActive_Deactivates` |
| 15 | `DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating` |
| 16 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 17 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 18 | `GetChangesAsync_FillsStockItemNameAndUnitForPageItems` |

- Os testes 9 e 14 (insumo) e 6, 7, 8, 12 e 13 (movimentação) fixam regras que já existem e hoje não têm cobertura.
- A correção do item 4 da §3, se necessária, fica coberta pela validação manual da Fase 3 (está no profile; o `CLAUDE.md` proíbe testar mapeamento nos testes de service), como na 14.1.
- Casos de borda dos helpers (fuso, relógio adiantado, limite, página vazia) já são cobertos pelos testes da `MilkProduction` e não são repetidos.
- **Resultado (08/Out/2026):** 19/19 em `StockItemServiceTests` e 18/18 em `StockMovementServiceTests`, na primeira execução; projeto inteiro **113/113** (19 `MilkProduction` + 15 `Vaccine` + 23 `SemenSample` + 19 `SemenSampleMovement` + 19 `StockItem` + 18 `StockMovement`).

### 6.11 Fase 11 — Documentação

- **Esta spec:** status das fases e resultados.
- **Spec #14 §8:** `StockItem` e `StockMovement` → ✅. **§13 questão 1:** resolvida para o estoque de insumos (R9).
- **`spec-estoque.md`:** nota apontando para esta spec.
- **`Docs/catalogo-erros-api.md` §4.14:** remover os `409` "O insumo já está inativo." e "A movimentação já está inativa."; acrescentar os `400` de `syncId`/`initialMovementSyncId` vazios e do cursor; nota de rota com suporte offline.

**Feito (08/Out/2026):**
- Spec #14 → v1.2: §8 (`StockItem`/`StockMovement` ✅; `Vaccine` atualizada para ✅, já integrada no PR #37), §13 questão 1 (resolvida para o estoque; aberta só para a cobertura), cabeçalho e histórico. **Correção de premissa pedida pela usuária:** não existe cliente web — menções a "web" trocadas por "outro celular" (conflitos) ou "Swagger/chamada direta" (requisições sem `syncId`/`updatedAt`).
- `catalogo-erros-api.md` §4.14: `409` de estado repetido removidos, `400` do cursor na tabela, nota de rotas offline e registro da correção do `PATCH` parcial.
- `spec-estoque.md`: nota no topo apontando para esta spec.

**Pendências observadas (fora desta spec):**
- A branch saiu de `feature/offline-semen-samples`; a `main` tem `MuuBoi.Tests/Services/HealthCaseServiceTests.cs` (hotfix de mastite, `4bb9370`) que não está aqui. Volta no merge com `dev`/`main`.
- O cabeçalho da Spec #14.1 (vacinas) ainda diz "Implementação pendente", e o da `spec-estoque.md` diz "não implementada" — ambos desatualizados.

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/StockItem.cs`, `StockMovement.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` nas duas entidades |
| Infrastructure | `Infrastructure/Migrations/*_Offline_Stock_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/StockItemRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `GetChangesAsync`, `GetByIdsAsync` |
| Infrastructure | `Infrastructure/Repositories/StockMovementRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Application | `Application/DTOs/StockItemCreateDto.cs` | `SyncId?`, `InitialMovementSyncId?` + validação |
| Application | `Application/DTOs/StockItemUpdateDto.cs`, `StockMovementUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/StockItemDto.cs`, `StockMovementDto.cs` | `SyncId` |
| Application | `Application/DTOs/StockMovementCreateDto.cs` | `SyncId?` + validação |
| Application | `Application/DTOs/StockItemCreatedDto.cs`, `StockMovementRefDto.cs` | **Novos** (E1) |
| Application | `Application/Mappings/StockProfile.cs` | Ignora `SyncId`/`RowVersion`/`UpdatedAt` onde preciso; mapa para `StockItemCreatedDto`; `PreCondition` se o item 4 da §3 se confirmar |
| Application | `Application/Interfaces/IStockItemRepository.cs`, `IStockMovementRepository.cs`, `IStockItemService.cs`, `IStockMovementService.cs` | Novos métodos; `CreateOpeningBalanceAsync` removido |
| Application | `Application/Services/StockItemService.cs` | Criação idempotente e atômica, LWW, inativação idempotente, pull; sem dependência de `IStockMovementService` |
| Application | `Application/Services/StockMovementService.cs` | Criação idempotente, LWW, inativação idempotente, pull; remove `CreateOpeningBalanceAsync` |
| Api | `Api/Controllers/StockItemsController.cs` | Rotas `GET changes` e `GET movements/changes`; `Create` devolve `StockItemCreatedDto` |
| Tests | `MuuBoi.Tests/Services/StockItemServiceTests.cs`, `StockMovementServiceTests.cs` | **Novos** |

**Não muda:** regras R1–R11, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `StockForecastResolver`, `StockController` (dashboard, alertas, enums), `StockCategory`/`UnitOfMeasure` e as rotas de tela.

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Custo médio "fora de ordem" quando compras e saídas de dispositivos diferentes sincronizam intercaladas (E7) | Risco já aceito em `spec-estoque.md` (D8): o snapshot é estimativa congelada e não corrompe valores passados. O saldo em quantidade é sempre exato |
| `initialMovementSyncId` igual ao de outra movimentação já existente (erro do app) → violação de `UX_StockMovements_SyncId` não tratada → `500` | Aceito, como na 14.2: UUID v7 gerado no app torna isso impraticável (A4) |
| Pull de insumos com muitos itens ficar lento pelos derivados | Cálculo em lote (2 consultas de movimentação + 2 de referência por página), igual à listagem `GET /` atual |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Movimentação de outro insumo pela rota** (§3 item 10): `GET`/`PATCH`/`DELETE /{stockItemId}/movements/{movementId}` não confere se a movimentação pertence ao insumo da rota | ⏳ Bug pré-existente, fora desta spec. Corrigir junto com o Q1 da 14.2 numa tarefa própria: `404` "Movimentação com id '{movementId}' não encontrada." quando `movement.StockItemId != stockItemId` |
| Q2 | **Data do saldo inicial offline** (E5) | ⏳ Mantida como hoje. Reavaliar se o usuário quiser a data real do cadastro no histórico |
| Q3 | **`SyncRefDto` comum** para `initialMovement` de sêmen e estoque (E1) | ⏳ Limpeza opcional depois do merge das duas specs |
