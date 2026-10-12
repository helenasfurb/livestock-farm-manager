# Spec #14.8: Sincronização Offline — Eventos de Vacinação e Reforços

**Módulo:** Infraestrutura / Sincronização — Controle Sanitário (Vacinação)
**Versão:** 0.2
**Data:** 12/Out/2026
**Status:** 🟢 **Aprovada.** Decisões V1–V10 fechadas (V4, V7 e V8 como propostas). Pré-requisito (Spec #14 §13 questão 8) concluído. Nenhuma fase implementada.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 11/Out/2026 | Rascunho: evento de vacinação (com a lista de animais embutida) e reforço planejados **sem alterar nenhuma regra de negócio**; Condição 3 e índice único no reforço; pull com DTO próprio |
| 0.2 | 12/Out/2026 | Aprovada: V4 (corrida tratada no repositório), V7 (`VaccinationEventSyncDto`) e V8 (validação → `400`, se a Fase 2 confirmar) fechadas como propostas. Pré-requisito concluído na branch `fix/state-changes-keep-updated-at` |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões, helpers (§7.2), receita (§7.4), Condições 1–3 (§7.5), §5.9 (`VaccinationEventAnimal` embutido) e §13 questão 8 (mudanças de estado não alteram `UpdatedAt`) · Spec #14.1 (catálogo de vacinas, sincronizável) · Spec #14.4 (animais, sincronizáveis; N6 — pull com DTO próprio) · Spec #14.7 (L2 — Condição 3; L7 — mudança de estado sem `UpdatedAt`). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-controle-sanitario-vacinacao.md` (D1–D12, RN-01–RN-09) · `Docs/catalogo-erros-api.md` (seção de eventos de vacinação)

> **Por que vacinação agora:** é a próxima entidade em complexidade (Spec #14 §8) e depende só de entidades já sincronizáveis (vacina e animais). A Spec #14.1 já previa: "quando os eventos entrarem no offline, uma vacina criada offline precisará estar sincronizada antes do evento que a usa".
>
> **O que a torna diferente das anteriores:** (1) **várias referências por operação** — o evento aponta para uma vacina **e** para uma lista de animais, todos resolvidos pelo app no envio (A5); (2) a lista de animais é uma **tabela de ligação sem identidade própria** (`VaccinationEventAnimal`, chave composta), gravada junto com o evento — já decidido como "embutida" na Spec #14 §5.9; (3) o **reforço** é uma segunda forma de criação (`POST /{id}/booster`) com uma regra de unicidade "um reforço ativo por evento" garantida **também por índice único no banco** (`UX_VaccinationEvents_ParentEventId_Active`), o que muda o tratamento da corrida (V4); (4) o detalhe tem **status derivado do tempo** e **linhagem** (pai e filho) vinda de outras linhas — o filho muda sem o `RowVersion` do pai mudar (V7).
>
> **Pré-requisito (✅ concluído em 12/Out/2026):** a correção da Spec #14 §13 questão 8 (mudanças de estado não alteram `UpdatedAt`) entrou antes, numa tarefa própria (`fix/state-changes-keep-updated-at`); a inativação do evento (V6) já nasce nessa regra.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/vaccination-events` (criação com vacina e lista de animais) com suporte offline | Alterar a lista de animais de um evento existente — não existe rota hoje (R6) |
| `POST /api/vaccination-events/{id}/booster` (reforço) com suporte offline | Aplicação por animal (Q1 da `spec-controle-sanitario-vacinacao.md`) |
| `PATCH` e `DELETE /api/vaccination-events/{id}` com LWW / idempotência | Histórico por animal (`GET /api/animals/{id}/vaccination-history`) — leitura de tela; o app recalcula sobre os dados do pull |
| `GET /api/vaccination-events/changes` (**nova**) | Casos de saúde e medicamentos (spec própria) |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/vaccination-events` | Sempre cria | Aceita `syncId`; se já existir → devolve o existente (`201`), sem checar vacina e animais |
| `POST /api/vaccination-events/{id}/booster` | Cria; segundo reforço → `409`; corrida → `500` (índice único) | Aceita `syncId`; reenvio → `201` com o existente, inclusive quando ele próprio é o "reforço já existente" (Condição 3) e na corrida (V4) |
| `PATCH /api/vaccination-events/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antiga (LWW) |
| `DELETE /api/vaccination-events/{id}` | Já inativo → `409` | Já inativo → `204`, sem gravar; não altera `UpdatedAt` |
| `GET /api/vaccination-events/changes?since=&limit=` | — | **Nova**, com `VaccinationEventSyncDto` (V7) |
| `GET /api/vaccination-events/{id}` e respostas de `POST`/`PATCH` | — | Ganham `syncId` (aditivo) |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.7): nenhuma regra de negócio é alterada.**

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Vacina precisa existir (ativa **ou** inativa — `GetVaccineByIdAsync` não filtra `IsActive`) | `POST` | `404` "Vacina com id '{id}' não encontrada." |
| R2 | Todos os animais precisam existir (ativos **ou** inativos) | `POST` | `404` "Animais não encontrados: {ids}." |
| R3 | Ao menos uma data (`predictedDate` ou `applicationDate`); `applicationDate` não futura; lista de animais sem duplicatas e com ≥ 1 item (RN-01–RN-03) | `POST`, `PATCH` (DTO) | `400` (formato B) |
| R4 | Reforço: **um ativo por evento** (RN-05, D5) — consulta no service **e** índice único filtrado no banco | `POST /booster` | `409` "Já existe um reforço para este evento." |
| R5 | Reforço: data prevista não anterior à aplicação nem à previsão do pai (RN-05) | `POST /booster` | `422` (duas mensagens) |
| R6 | Reforço herda vacina e lista de animais do pai (RN-07); a lista de um evento **não muda** depois de criada (não há rota) | `POST /booster` | Mantido |
| R7 | Evento precisa existir | `GET /{id}`, `PATCH`, `DELETE`, `POST /booster` (pai) | `404` "Evento de vacinação com id '{id}' não encontrado." |
| R8 | `PATCH` em evento inativo é aceito; reforço de pai inativo é aceito | `PATCH`, `POST /booster` | Mantido (Spec #14 §5.4) |
| R9 | Inativar o pai **não** inativa o reforço | `DELETE` | Mantido |
| R10 | Status (`Applied`/`Scheduled`/`Overdue`) é **derivado**, nunca gravado (D4) | Detalhe, lista | Mantido; fora do pull (V7) |

**Ordem entre `SyncId` e regras:** `SyncId` antes de R1, R2, R4, R5 e R7 (pai). O reenvio de uma criação já gravada responde `201` com o existente, mesmo que depois a vacina tenha sido excluída ou o pai inativado.

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `VaccinationEvent : BaseEntity, ITenantEntity` — já tem `PropertyId` e filtro de tenant | Basta `ISyncable` | Receita §7.4 (Fase 1) |
| 2 | `VaccinationEventAnimal` (chave composta, `ITenantEntity`, sem `BaseEntity`) é gravado pela navegação `EventAnimals`, **no mesmo `SaveChanges`** do evento; não há rota para mudar a lista | Condição 1 já atendida. A ligação não precisa de `SyncId`: nunca é endereçada sozinha e não muda depois da criação | Embutida no pull do evento (Spec #14 §5.9, V2) |
| 3 | `CreateAsync` checa vacina e animais e grava via `_repository.CreateAsync` (`Add` + `SaveChanges`) | Reenvio **duplica o evento** silenciosamente (sem regra de unicidade) | `SyncId` + `AddSyncableAsync` (V2) |
| 4 | `CreateBoosterAsync`: "Já existe um reforço para este evento." (`409`) checado depois de carregar o pai | **Condição 3:** o reenvio de um reforço já gravado esbarra no próprio reforço → `409` definitivo para o app | Rechecar o `SyncId` antes do `409` (V3) |
| 5 | Índice único `UX_VaccinationEvents_ParentEventId_Active` (`[ParentEventId] IS NOT NULL AND [IsActive] = 1`) | Na corrida (original e reenvio passando juntos pela checagem do service), o **segundo `INSERT` viola esse índice**, não o do `SyncId`. O `AddSyncableAsync` só reconhece `UX_{Tabela}_SyncId` → a exceção sobe como **`500`** (o mesmo acontece hoje, online, com dois reforços simultâneos de `syncId` diferentes) | V4 |
| 6 | `UpdateAsync` atribui campo a campo e carimba `UpdatedAt = DateTime.UtcNow` | Precisa de LWW | V5 |
| 7 | `DeactivateAsync`: "O evento de vacinação já está inativo." (`409`) e carimba `UpdatedAt` | Viola A6; e a Spec #14 §13 questão 8 | V6 |
| 8 | `VaccinationEventDto` (detalhe) traz `vaccineName`, `animals[]` com nome e brinco, `status` derivado do tempo e `parentEvent`/`childEvent` com status | No pull: 3–4 consultas por página só para nomes e linhagem; o `status` muda com o passar dos dias; e **o `childEvent` do pai muda quando o reforço é criado, sem o `RowVersion` do pai mudar** — o pai baixado ficaria com a linhagem velha | V7 |
| 9 | **Prováveis bugs pré-existentes:** `DoseType` (criação e edição) sem `[ValidEnum]`; `VaccineId` (`int`, `[Required]`) ausente vira `0`; `VaccinationBoosterCreateDto.PredictedDate` (`DateTime`, `[Required]`) ausente vira `0001-01-01` | `doseType: 9` gravado; `vaccineId` ausente → `404` "Vacina com id '0'…" (mensagem errada, mas não grava); reforço sem data → `422` com mensagem errada | **A confirmar na Fase 2.** Se confirmado: V8 |
| 10 | Banco local: 7 eventos (3 reforços), 23 ligações, nenhum inativo | Backfill de `SyncId` pela migração | — |

## 4. Decisões propostas

As marcadas com ⭐ foram escolhidas pela usuária em 12/Out/2026 (todas como propostas); as demais seguem precedentes das specs 14.1–14.7.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| V1 | **`ISyncable` no `VaccinationEvent`** + `ConfigureSyncable()`; migração só com `SyncId`/`RowVersion` e índices. `VaccinationEventAnimal` **não** muda | `SyncId` na ligação — exigiria `BaseEntity` e não tem uso (lista imutável) |
| V2 | **Criação idempotente**: `VaccinationEventCreateDto.SyncId`; checado antes de R1/R2; `AddSyncableAsync`. A lista de animais vai no mesmo `INSERT` (já é assim) | — |
| V3 | **Reforço idempotente com Condição 3**: `VaccinationBoosterCreateDto.SyncId`; checado antes de R7 (pai), R5 e R4; antes do `409` de R4, rechecar o `SyncId` e devolver o existente (L2 da 14.7) | Só a checagem inicial — deixaria a Condição 3 aberta |
| V4 ⭐ | **Corrida no índice do reforço tratada no repositório**: `IVaccinationEventRepository.CreateBoosterAsync` grava e, se o banco recusar por `UX_VaccinationEvents_ParentEventId_Active` (erros 2601/2627 com esse nome), desanexa o reforço e devolve `null`. O service então **rebusca o `SyncId`**: se existir, devolve o existente (`201`); senão, lança o `409` de R4. Resultado: reenvio simultâneo → `201`; dois reforços diferentes ao mesmo tempo → `409` (hoje `500`). O helper genérico `AddSyncableAsync` **não muda** | (a) Deixar como está — a corrida vira `500`, que o app tenta de novo até o limite (Spec #14 §5.10); no reenvio seguinte a Condição 3 resolve, mas um `500` de bug "esperado" polui o log e gasta tentativas. (b) Generalizar o `AddSyncableAsync` para outros índices — mexe no helper de todas as entidades |
| V5 | **`PATCH` com LWW**: R7 → LWW → atribuições; `UpdatedAt = editedAt`. Evento inativo continua editável (R8, Spec #14 §5.4) | — |
| V6 | **Inativação idempotente** (`204` sem gravar no repetido) e **sem alterar `UpdatedAt`** (Spec #14 §13 questão 8, L7 da 14.7). Inativar o pai não toca o reforço (R9) | Manter o `409` — viola A6 |
| V7 ⭐ | **Pull com DTO próprio, `VaccinationEventSyncDto`** (precedente N6 da 14.4): `id`, `syncId`, `vaccineId`, `doseType` (`EnumValueDto`), `predictedDate`, `applicationDate`, `parentEventId`, `notes`, `animalIds` (lista), `isActive`, `createdAt`, `updatedAt`. **Sem** `status`, `vaccineName`, nomes/brincos dos animais e `parentEvent`/`childEvent`: o app monta a linhagem pelo `parentEventId` (o filho de um evento é o evento ativo com `parentEventId` igual), resolve nomes pelos dados locais e **deriva o status** com as regras do `VaccinationEventStatusResolver` (D4). **2 consultas por página**, constante (eventos + ligações) | Mesmo `VaccinationEventDto` com tudo em lote — 4 consultas por página, e a linhagem (`childEvent`) e o status envelheceriam sem o evento reaparecer no pull |
| V8 ⭐ | **Validação de `doseType`, `vaccineId` e da data do reforço → `400`** (se a Fase 2 confirmar o §3 item 9): `[ValidEnum(typeof(DoseType))]` em `DoseType` (criação e edição); `VaccineId` → `int?` com `[Required]`; `VaccinationBoosterCreateDto.PredictedDate` → `DateTime?` com `[Required]`. Correção de bug, não regra nova (RN-01, RN-05; mesma linha da C7 da 14.6 e da L9 da 14.7) | Manter — um `doseType` inválido sincronizaria para todos os celulares |
| V9 | **Pull em `GET /api/vaccination-events/changes`** — sem colisão com `{id:int}` nem com `dose-types`. Traz os eventos de todos os animais da propriedade, inclusive inativos | — |
| V10 | **Vacina ou animal inativos não bloqueiam** a criação (R1, R2 — já é assim). Offline, uma vacina excluída em outro celular não trava a fila do evento que a usa. Vacina/animal **inexistentes** (só por bug do app ou exclusão de conta) continuam `404` | Exigir vacina/animal ativos — regra nova e fonte de conflitos sem ação para o usuário |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** vacina criada offline → animais criados offline → evento → reforço. O worker envia na ordem da fila (A2); o corpo do evento é montado **na hora do envio** com o `ServerId` da vacina e de **cada** animal da lista (A5). Se qualquer um deles falhou, o evento espera.
2. **Reforço:** `POST /api/vaccination-events/{id}/booster` com o `ServerId` do pai e um `syncId` próprio. O reforço herda a vacina e os animais do pai no servidor (R6) — o app não envia a lista.
3. **Datas:** `applicationDate` e `predictedDate` em UTC, com `Z`; a data de aplicação é a do momento real da aplicação.
4. **`409` de R4** (outro celular já criou o reforço deste evento): conflito real. O app mostra o reforço existente (vem no pull) e oferece descartar o seu.
5. **Pull (`GET /api/vaccination-events/changes`):** upsert por `syncId`; a lista `animalIds` **substitui** a lista local do evento (sem identidade própria, Spec #14 §5.9). Puxar **vacinas e animais antes de eventos**.
6. **Derivados locais (V7):** status (`Aplicado` se há data de aplicação; senão `Agendado`/`Atrasado` pela data prevista × hoje), linhagem (pai pelo `parentEventId`; filho = evento ativo cujo `parentEventId` é este), nome da vacina e dos animais, e o histórico por animal (eventos aplicados que contêm o animal, com a próxima dose vinda do reforço). As telas que hoje usam o detalhe online passam a montar isso localmente.
7. **Sem edição da lista de animais** (R6): para incluir um animal esquecido, o usuário cria outro evento.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (V1) | ⏳ |
| 2 — Criação idempotente (+ confirmação do §3 item 9 e V8) | ⏳ |
| 3 — Reforço idempotente: Condição 3 e índice único (V3, V4) | ⏳ |
| 4 — Edição com last-write-wins (V5) | ⏳ |
| 5 — Inativação idempotente (V6) | ⏳ |
| 6 — Pull incremental com `VaccinationEventSyncDto` (V7, V9) | ⏳ |
| 7 — Testes | ⏳ |
| 8 — Documentação | ⏳ |

Branch: `feature/offline-vaccination-events`, a partir da branch que tiver a correção da Spec #14 §13 questão 8 (ou do `dev`, depois do merge). Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com as contas de teste das specs anteriores.

### 6.1 Fase 1 — Domínio e banco

- `VaccinationEvent : BaseEntity, ITenantEntity, ISyncable`.
- `ApplicationDbContext`: `builder.Entity<VaccinationEvent>().ConfigureSyncable();`.
- Migração `Offline_VaccinationEvent_SyncId_RowVersion` (⚠️ **requer aprovação**):

```sql
ALTER TABLE [VaccinationEvents] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [VaccinationEvents] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_VaccinationEvents_PropertyId_RowVersion] ON [VaccinationEvents] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_VaccinationEvents_SyncId] ON [VaccinationEvents] ([SyncId]);
```

| Validação planejada | Esperado |
|---|---|
| SQL / snapshot | Só `VaccinationEvents`; `VaccinationEventAnimals` intacta |
| Eventos existentes | 7, `SyncId` distintos e não vazios |
| Evento novo e reforço novo | `SyncId` ≠ `Guid.Empty` |

### 6.2 Fase 2 — Criação idempotente

- **Antes de mudar o código:** confirmar o §3 item 9 (`doseType: 9`; sem `vaccineId`; reforço sem `predictedDate`).
- `VaccinationEventCreateDto`: `Guid? SyncId` + validação de `Guid.Empty`; V8 se confirmado.
- Repositório: `GetBySyncIdAsync` (com os mesmos `Include` do `GetByIdAsync`); `CreateAsync` → `AddSyncableAsync`.
- `VaccinationEventService.CreateAsync`: bloco do `SyncId` antes de R1/R2; `SyncId = dto.SyncId ?? Guid.NewGuid()`.

| Validação planejada | Esperado |
|---|---|
| `POST` com `syncId` / repetido / payload diferente (outros animais) | `201`; mesmo `Id`; 1 evento e a lista **original** de ligações |
| Sem `syncId` / `syncId` vazio | `201` com `SyncId` gerado / `400` |
| Reenvio depois de excluir a vacina | `201` com o existente |
| `POST` novo com vacina inativa / animal inativo | `201` (R1, R2, V10) |
| Vacina inexistente / animal inexistente | `404` (mantidos) |
| 20 envios simultâneos (mesmo `syncId`, 3 animais) | Todos `201`; 1 evento, 3 ligações |

### 6.3 Fase 3 — Reforço idempotente

- `VaccinationBoosterCreateDto`: `Guid? SyncId` + validação; V8 (`PredictedDate` obrigatória de fato) se confirmado.
- `IVaccinationEventRepository.CreateBoosterAsync(VaccinationEvent booster)` → `VaccinationEvent?` (V4): `AddSyncableAsync` dentro de um `try`; `catch (DbUpdateException)` quando a violação é de `UX_VaccinationEvents_ParentEventId_Active` → desanexa o reforço e as ligações e devolve `null`.
- `CreateBoosterAsync` no service:

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return await GetByIdAsync(existing.Id);
}

// R7 (pai), R5 como hoje

var existingChild = await _repository.GetActiveChildByParentIdAsync(parentId);
if (existingChild != null)
    return await ExistingBySyncIdOrConflictAsync(dto.SyncId);

// monta o reforço com SyncId = dto.SyncId ?? Guid.NewGuid()

var created = await _repository.CreateBoosterAsync(booster);
if (created == null)
    return await ExistingBySyncIdOrConflictAsync(dto.SyncId);

return await GetByIdAsync(created.Id);
```

(`ExistingBySyncIdOrConflictAsync`: se o `SyncId` existir, devolve o detalhe dele; senão lança `ConflictException("Já existe um reforço para este evento.")`.)

| Validação planejada | Esperado |
|---|---|
| Reforço com `syncId` / reenvio | `201`, mesmo `Id` — o reenvio **não** recebe o `409` de R4 (Condição 3) |
| Segundo reforço (outro `syncId`) | `409` (mantido) |
| R5 (data anterior à do pai) / pai inexistente | `422` / `404` (mantidos) |
| 20 envios simultâneos do mesmo reforço | Todos `201`; 1 reforço; **nenhum `500`** |
| 10 + 10 envios simultâneos de **dois** reforços diferentes do mesmo pai | 1 reforço criado; os do outro `syncId` recebem `409` (hoje: `500`) |
| Inativar o reforço → criar outro reforço | `201` (o índice é filtrado por `IsActive`) |

### 6.4 Fase 4 — Edição com last-write-wins

- `VaccinationEventUpdateDto`: `DateTime? UpdatedAt`; V8 (`[ValidEnum]` em `DoseType`).
- `UpdateAsync`: R7 → `ResolveEditedAt`/`IsOutdated` → atribuições → `UpdatedAt = editedAt`.

| Validação planejada | Esperado |
|---|---|
| Mais novo / `-03:00` / reenvio / mais antigo / sem `updatedAt` / relógio adiantado | Como nas specs anteriores |
| `PATCH` em evento inativo | `200`, aplicado (R8) |
| `PATCH` de `applicationDate` futura | `400` (mantido) |

### 6.5 Fase 5 — Inativação idempotente

- `DeactivateAsync`: `if (!vaccinationEvent.IsActive) return true;`; sem `UpdatedAt`.

| Validação planejada | Esperado |
|---|---|
| Inativar → inativar de novo | `204` → `204`; `RowVersion` e `UpdatedAt` inalterados no repetido |
| Inativar o pai | Reforço continua ativo (R9) |
| `UpdatedAt` depois de inativar | Inalterado; `RowVersion` avança |

### 6.6 Fase 6 — Pull incremental

- `VaccinationEventSyncDto` (**novo**) e mapa no profile (`doseType` como `EnumValueDto`; `animalIds` ignorado no mapa e preenchido pelo service).
- `IVaccinationEventRepository.GetChangesAsync(since, take)` → `GetChangesSinceAsync<VaccinationEvent>`; `GetAnimalIdsByEventIdsAsync(ids)` → `Dictionary<int, List<int>>` (uma consulta).
- `IVaccinationEventService.GetChangesAsync(since, limit)` → `SyncPageDto<VaccinationEventSyncDto>`.
- `VaccinationEventsController`: `[HttpGet("changes")]`.

| Validação planejada | Esperado |
|---|---|
| Pull inicial | Todos os eventos (inclusive reforços e inativos) com `syncId`, `vaccineId`, `parentEventId`, `animalIds` |
| Pull × `GET /{id}`, campo a campo (os comuns) | Nenhuma divergência; `animalIds` = ids de `animals[]` |
| Isolamento (duas contas) | Cada uma vê só os seus |
| Cria evento, cria reforço, edita, inativa → pull | Cada evento **uma vez**, com o estado final |
| Criar um reforço → pull | Só o reforço (o pai **não** reaparece — e não precisa, V7) |
| Paginação / cursor / `limit` | Como nas anteriores |
| `GET /dose-types`, `/{id}` | Sem colisão |
| Consultas por página | **2**, constante |

### 6.7 Fase 7 — Testes

`MuuBoi.Tests/Services/VaccinationEventServiceTests.cs` (**novo** — hoje não há testes de vacinação).

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesEventWithGivenSyncIdAndAnimals` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotCheckVaccineOrAnimals` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAsync_WhenVaccineNotFound_ThrowsNotFoundException` (R1) |
| 6 | `CreateAsync_WhenAnimalNotFound_ThrowsNotFoundException` (R2) |
| 7 | `CreateBoosterAsync_WithNewSyncId_CreatesBoosterInheritingVaccineAndAnimals` (R6) |
| 8 | `CreateBoosterAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 9 | `CreateBoosterAsync_WhenActiveChildIsSameSyncId_ReturnsExisting` (Condição 3) |
| 10 | `CreateBoosterAsync_WhenAnotherBoosterExists_ThrowsConflictException` (R4) |
| 11 | `CreateBoosterAsync_WhenIndexRejectsAndSyncIdExists_ReturnsExisting` (V4) |
| 12 | `CreateBoosterAsync_WhenIndexRejectsAndSyncIdMissing_ThrowsConflictException` (V4) |
| 13 | `CreateBoosterAsync_WithPredictedDateBeforeParent_ThrowsBusinessRuleException` (R5) |
| 14 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 15 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 16 | `UpdateAsync_WhenInactive_AppliesChanges` (R8) |
| 17 | `DeactivateAsync_WhenActive_DeactivatesWithoutChangingUpdatedAt` (V6) |
| 18 | `DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` (V6) |
| 19 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 20 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 21 | `GetChangesAsync_FillsAnimalIdsForPageItems` (V7) |

### 6.8 Fase 8 — Documentação

- **Esta spec**; **Spec #14** (§8: `VaccinationEvent` ✅ e `VaccinationEventAnimal` embutido; §5.5: segunda exceção ao "mesmo DTO do detalhe"); **Spec #14.1** (dependência futura cumprida); **`catalogo-erros-api.md`** (eventos de vacinação: `409` de estado repetido removido, `400` novos, nota de rotas offline, corrida do reforço deixa de ser `500`); **`spec-controle-sanitario-vacinacao.md`** (nota no topo; D9 "offline-first = evolução futura" cumprida).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/VaccinationEvent.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_VaccinationEvent_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/VaccinationEventRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `CreateBoosterAsync` (V4), `GetChangesAsync`, `GetAnimalIdsByEventIdsAsync` |
| Application | `Application/DTOs/VaccinationEventCreateDto.cs`, `VaccinationBoosterCreateDto.cs` | `SyncId?`; V8 |
| Application | `Application/DTOs/VaccinationEventUpdateDto.cs` | `UpdatedAt?`; V8 |
| Application | `Application/DTOs/VaccinationEventDto.cs` | `SyncId` |
| Application | `Application/DTOs/VaccinationEventSyncDto.cs` | **Novo** (V7) |
| Application | `Application/Mappings/VaccinationEventProfile.cs` (ou o profile atual da vacinação) | Mapa do `VaccinationEventSyncDto` |
| Application | `Application/Interfaces/IVaccinationEventRepository.cs`, `IVaccinationEventService.cs` | Novos métodos |
| Application | `Application/Services/VaccinationEventService.cs` | Criação e reforço idempotentes, LWW, inativação idempotente, pull |
| Api | `Api/Controllers/VaccinationEventsController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/Services/VaccinationEventServiceTests.cs` | **Novo** |

**Não muda:** regras R1–R10, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização (inclusive `AddSyncableAsync`), `VaccinationEventStatusResolver`, `VaccinationEventAnimal` no banco, histórico por animal, rotas de tela (exceto o `syncId` aditivo).

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Evento offline com um animal que outro celular cadastrou e o pull ainda não trouxe | Não acontece: o app só lista animais que tem localmente, e os criados offline vão antes na fila (A2) |
| Dois celulares criam o reforço do mesmo evento | O segundo recebe `409` (conflito real, §5 item 4) |
| Telas que hoje usam o detalhe online (status, linhagem, nomes) passam a depender do cálculo local | Regras simples e documentadas (§5 item 6); o detalhe online continua igual |
| Reenvio de evento com lista diferente (bug do app) | Devolve o original sem alterar (padrão de todas as criações) |
| `SyncId` repetido de outro evento (erro do app) → `500` | Aceito, como nas anteriores (A4) |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Editar a lista de animais** de um evento (incluir esquecido / remover) | ⏳ Fora — regra nova; exigiria versionar a lista (Spec #14 §5.9 já prevê que a mudança avança o `RowVersion` do evento) |
| Q2 | **Aplicação por animal** (Q1 da `spec-controle-sanitario-vacinacao.md`) | ⏳ Fora |
| Q3 | Mensagem do `404` quando `vaccineId` vem ausente ("Vacina com id '0'…") | Resolvida pela V8, se aprovada |
