# Spec #14.2: Sincronização Offline — Catálogo de Sêmen e Movimentações Manuais

**Módulo:** Infraestrutura / Sincronização — Reprodução (Banco de Sêmen)
**Versão:** 0.2
**Data:** 06/Out/2026
**Status:**
- **Parte A — Catálogo (`SemenSample`):** ✅ **Implementada e validada** (06/Out/2026) — Fases 1–7, decisões S1–S3. Branch `feature/offline-semen-samples`.
- **Parte B — Movimentações manuais (`SemenSampleMovement`):** ✅ **Implementada e validada** (06/Out/2026) — Fases B1–B7, decisões M1–M4. Nenhuma regra de negócio alterada (R1–R8).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 06/Out/2026 | Parte A: catálogo de sêmen sincronizável e remoção de `BatchDate` |
| 0.2 | 06/Out/2026 | Parte B: plano das movimentações manuais, **sem alterar nenhuma regra de negócio** |
**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2) e receita (§7.4). Esta spec **não** redecide nada da #14; aplica a receita ao catálogo `SemenSample` e resolve a Condição 1 da §7.5 para a quantidade inicial.
**Relaciona-se com:** Spec #14.1 (Vacinas — mesmo padrão de catálogo) · `spec-banco-semen.md` · `spec-ajustes-banco-semen-doses.md` · `Docs/catalogo-erros-api.md` §4.13

> **Por que sêmen e não medicamentos:** o CRUD de `Medication` será descontinuado (06/Out/2026) e não entra no offline. `SemenSample` é o próximo catálogo da Spec #14 §8.
>
> **Ajuste de domínio aproveitado nesta spec:** remoção da **data de partida** (`BatchDate`). A partida passa a ser identificada **só pelo número** (`BatchNumber`). Decidido em 06/Out/2026; altera a decisão D9 de `spec-ajustes-banco-semen-doses.md` apenas quanto à data.

---

## 1. Escopo e diferenças em relação às vacinas

**Escopo:** somente o **catálogo de amostras de sêmen** (`SemenSample`, rotas `/api/semen-samples` e `/{id}/reactivate`). As movimentações manuais (`SemenSampleMovement`, rotas `/{semenSampleId}/movements`) são planejadas na **Parte B** desta spec (§5 em diante). Também ficam fora: `GET /autocomplete` e `GET /` (rotas de tela, sem mudança de contrato).

**Diferenças em relação à Vaccine (Spec #14.1):**

| | Vaccine | SemenSample |
|---|---|---|
| Registros por `POST` | 1 | **1 ou 2** — com `initialQuantity`, cria também uma movimentação de entrada (§1.1 item 1) |
| Inativação | `DELETE` → `204` | `DELETE` → **`200` com `bool`** (contrato atual, mantido) |
| Reativação | — | **`PATCH /{id}/reactivate`** — segunda mudança de estado |
| Estado repetido hoje | Regravava | **`409`** nas duas rotas (§1.1 item 2) |
| Campo derivado no DTO | — | **`availableDoses`**, calculado das movimentações (§1.1 item 3) |
| Bug do `ForAllMembers` (14.1 §1.1 item 7) | Presente (`RequiresBooster`) | **Não se aplica** — todo campo anulável no DTO também é anulável na entidade (§1.1 item 5) |

**Dependência futura:** `BreedingEvent.SemenSampleId` e `AnimalPregnancy.SemenSampleId` referenciam a amostra. Quando cobertura e gestação entrarem no offline, uma amostra criada offline precisará estar sincronizada antes do evento que a usa — garantido pelo envio sequencial e pela resolução do `ServerId` no app (A2, A5, Spec #14 §5.8).

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/semen-samples` | Sempre cria; quantidade inicial em gravação separada | Aceita `syncId`; se já existir → devolve a existente (`201`). Amostra e entrada inicial gravadas **juntas** |
| `PATCH /api/semen-samples/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW) |
| `DELETE /api/semen-samples/{id}` | `200 false`; **`409`** se já inativa | `200 false` **sem gravar** se já inativa |
| `PATCH /api/semen-samples/{id}/reactivate` | `200 true`; **`409`** se já ativa | `200 true` **sem gravar** se já ativa |
| `GET /api/semen-samples/changes?since=&limit=` | — | **Nova** |
| `GET /{id}` | — | Resposta ganha `syncId` (aditivo) |
| `POST`, `PATCH`, `GET /`, `GET /{id}`, `GET /autocomplete` | Aceitam/devolvem `batchDate` | **`batchDate` removido** (⚠️ quebra de contrato: a web precisa deixar de enviar e exibir o campo; se ainda for enviado, o binder ignora) |

### 1.1 Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `SemenSampleService.CreateAsync` grava a amostra (`_repository.CreateAsync` → `SaveChanges`) e **depois** a entrada inicial (`_movementService.CreateForSemenSampleAsync` → outro `SaveChanges`) | **Condição 1 da Spec #14 §7.5.** Se o servidor cair entre as duas gravações, o reenvio encontra o `SyncId`, responde "já existe" e **a entrada inicial nunca é criada** — a amostra fica com 0 doses | Amostra e movimentação em **um único `SaveChanges`**, via navegação `Movements` (Fase 2, decisão S1) |
| 2 | `DeactivateAsync` lança `ConflictException` se já inativa; `ReactivateAsync` lança se já ativa | Viola a A6: o reenvio vira `409` (erro definitivo), a fila trava e o usuário é avisado de um erro que não aconteceu | Retorna o estado atual sem gravar (Fase 4) — exceção ao `CLAUDE.md` já registrada na Spec #14 §3.3 |
| 3 | `SemenSampleDto.AvailableDoses` é calculado das movimentações ativas | Movimentações (entrada manual, consumo pela cobertura) **não alteram o `RowVersion` da amostra** — o valor que o app recebeu no pull fica desatualizado até a própria amostra mudar | Decisão S3 (§3) |
| 4 | `SemenSampleProfile` (edição) grava `UpdatedAt = DateTime.UtcNow` | Com `UpdatedAt` no DTO de edição, o mapeamento precisa ignorar o valor bruto do cliente | Profile ignora `UpdatedAt`, `SyncId` e `RowVersion`; LWW no service (Fase 3) |
| 5 | `ForAllMembers(... srcMember != null)` no profile de edição | Verificado: `Name`, `BullRegistration`, `GeneticsCompany`, `BatchNumber`, `Notes` (`string?` → `string`/`string?`), `BullBreed` (`AnimalBreed?` → `AnimalBreed?`). Nenhum par anulável → não anulável de tipo valor — o bug da 14.1 não se repete | Sem mudança; confirmar na validação da Fase 3 (`PATCH` parcial preserva `bullBreed`) |
| 6 | `DELETE` responde `200` com `bool` em vez de `204` | Diverge da convenção HTTP do `CLAUDE.md`, mas a web já consome esse formato | **Mantido** — mudar o contrato está fora do escopo do offline |
| 7 | `AccountRepository` remove amostras e movimentações fisicamente (`ExecuteDeleteAsync`) | Nenhum: só ocorre na exclusão da conta (Spec #14 §6 item 10) | Sem mudança |
| 8 | Não há seeder de amostras | — | Nada a validar além do backfill da migração |
| 9 | `BatchDate` existe no modelo e em 5 DTOs (`Create`, `Update`, detalhe, lista, autocomplete); nenhuma regra, filtro ou ordenação usa o campo | Um campo a menos no contrato de sincronização | **Removido** do modelo, dos DTOs e do banco (`DROP COLUMN`, Fase 1). No banco local: 3 amostras, nenhuma com `BatchDate` preenchido. ⚠️ Conferir o banco de produção antes de aplicar, porque o `DROP` apaga os valores |

## 2. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco | ✅ Migração `20261006233811_Offline_SemenSample_SyncId_RowVersion` aplicada |
| 2 — Criação idempotente e atômica | ✅ Implementada e validada |
| 3 — Edição com last-write-wins | ✅ Implementada e validada |
| 4 — Inativação e reativação idempotentes | ✅ Implementada e validada |
| 5 — Pull incremental | ✅ Implementada e validada |
| 6 — Testes | ✅ 20 testes de `SemenSampleService` passando (54 no projeto) |
| 7 — Documentação | ✅ Spec #14 §8, `catalogo-erros-api.md` §4.13, `spec-ajustes-banco-semen-doses.md` (v3.3) e `spec-banco-semen.md` |

Branch: `feature/offline-semen-samples`, criada a partir de `feature/offline-vaccines` (ou do `dev`, depois do merge das vacinas) para que a migração fique em cima da `Offline_Vaccine_SyncId_RowVersion`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, como nas specs anteriores.

### 2.1 Fase 1 — Domínio e banco

- `SemenSample : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<SemenSample>().ConfigureSyncable();` (o índice `IX_SemenSamples_PropertyId_IsActive` existente permanece).
- `SemenSample`: remove `BatchDate` (item 9 da §1.1).
- Migração `Offline_SemenSample_SyncId_RowVersion` (⚠️ **requer aprovação**). Inclui a remoção da coluna, para o ajuste entrar no mesmo deploy. SQL esperado:

```sql
ALTER TABLE [SemenSamples] DROP COLUMN [BatchDate];
ALTER TABLE [SemenSamples] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [SemenSamples] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_SemenSamples_PropertyId_RowVersion] ON [SemenSamples] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_SemenSamples_SyncId] ON [SemenSamples] ([SyncId]);
```

| Validação (06/Out/2026) | Resultado |
|---|---|
| Amostras existentes após a migração | ✅ 3 amostras, 3 `SyncId` distintos, nenhum vazio; `RowVersion` preenchido (38082–38084) |
| Coluna `BatchDate` removida | ✅ |
| Outras tabelas (inclusive `SemenSampleMovements`) sem alteração | ✅ A migração só toca `SemenSamples`; as 3 movimentações permanecem |
| Índices | ✅ `PK_SemenSamples`, `IX_SemenSamples_PropertyId_IsActive`, `IX_SemenSamples_PropertyId_RowVersion`, `UX_SemenSamples_SyncId` |

### 2.2 Fase 2 — Criação idempotente e atômica

- `SemenSampleCreateDto`: `Guid? SyncId` + `IValidatableObject` → `Guid.Empty` gera `400` "O identificador de sincronização não pode ser vazio.".
- `SemenSampleDto`: `Guid SyncId`.
- `SemenSampleProfile` (criação): ignora também `SyncId` e `RowVersion`.
- `ISemenSampleRepository`/`SemenSampleRepository`: `GetBySyncIdAsync` → `FindBySyncIdAsync<SemenSample>`; `CreateAsync` → `AddSyncableAsync`.
- `SemenSampleService.CreateAsync` (decisão S1):

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return await ComposeDetailAsync(existing);
}

var sample = _mapper.Map<SemenSample>(dto);
sample.SyncId = dto.SyncId ?? Guid.NewGuid();

if (dto.InitialQuantity.HasValue)
    sample.Movements = new List<SemenSampleMovement>
    {
        new()
        {
            MovementType = SemenMovementType.Input,
            MovementDate = DateTime.UtcNow,
            Quantity = dto.InitialQuantity.Value,
            Notes = dto.InitialNotes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }
    };

var created = await _repository.CreateAsync(sample);
return await ComposeDetailAsync(created);
```

- O EF grava amostra e movimentação no **mesmo `SaveChanges`** (transação implícita) e preenche `SemenSampleId` pela navegação; o `PropertyId` dos dois é preenchido pelo override de `SaveChangesAsync` (ambos em `Added`).
- `ISemenSampleMovementService.CreateForSemenSampleAsync` deixa de ter uso e é **removido**. `SemenSampleService` deixa de depender de `ISemenSampleMovementService` (o construtor muda; a DI resolve sozinha, **sem mudança no `Program.cs`**).
- Controller sem mudança (reenvio responde `201` com o mesmo corpo).

| Validação (06/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` e `initialQuantity: 10` | ✅ `201`, `Id` 5, `availableDoses: 10`; 1 amostra + 1 movimentação |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id`; **ainda** 1 amostra + 1 movimentação |
| Mesmo `syncId`, payload diferente (`name` e `initialQuantity: 99`) | ✅ `201`, devolve a existente sem alterar (nome original, 10 doses) |
| `POST` sem `syncId` (web) | ✅ `201`, `syncId` gerado |
| `POST` sem `initialQuantity` | ✅ `201`, `availableDoses: 0`, nenhuma movimentação |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`) |
| `batchDate` enviado no corpo | ✅ `201`, campo ignorado; resposta sem `batchDate` |
| Falha simulada na movimentação (trigger temporário `THROW` em `SemenSampleMovements`, removido após o teste) | ✅ `500`; **nenhuma** amostra gravada — o identity pulou o `Id` 10, confirmando o rollback |
| Reenvio do `POST` que falhou | ✅ `201`, `Id` 11, `availableDoses: 3` — o app recupera sozinho depois do `5xx` |
| 20 envios simultâneos (mesmo `syncId`, `initialQuantity: 7`) | ✅ Todos `201`; 1 amostra + 1 movimentação |
| `PropertyId` da movimentação | ✅ Igual ao da amostra em todos os casos (override de `SaveChangesAsync`) |

- Amostra e movimentação saem em **dois comandos** no log, dentro da transação que o EF abre no `SaveChanges` com mais de um comando. A atomicidade foi confirmada pelo teste de falha, não pelo log.
- Como nas specs anteriores, o `catch` da corrida no `AddSyncableAsync` **não foi exercitado**: 5 `INSERT`s em `SemenSamples` no log, um por `syncId`, nenhuma violação de `UX_SemenSamples_SyncId`.
- Mesma diferença de formato já registrada na Spec #14 §10.2: o reenvio devolve `createdAt` sem `Z`.
- Conta de teste: `teste.offline.semen@muuboi.local` (propriedade "Fazenda Teste Offline Semen"). Registros de teste (Ids 5–9 e 11) inativados ao final.

> **Ponto de atenção — corrida no `AddSyncableAsync`:** no `catch` da violação de `UX_SemenSamples_SyncId`, o helper desanexa só a amostra; a movimentação filha continua `Added` no `ChangeTracker`. Hoje é inofensivo: nenhum outro `SaveChanges` ocorre na mesma requisição depois da criação. **Não** mexer no helper agora (ele é compartilhado por `MilkProduction` e `Vaccine`); registrar e revisitar se uma entidade futura gravar algo depois do `AddSyncableAsync`.

### 2.3 Fase 3 — Edição com last-write-wins

- `SemenSampleUpdateDto`: `DateTime? UpdatedAt`.
- `SemenSampleProfile` (edição): troca o `MapFrom(_ => DateTime.UtcNow)` de `UpdatedAt` por `Ignore()`; ignora também `SyncId` e `RowVersion`.
- `SemenSampleService.UpdateAsync`:

```csharp
var sample = await FindAsync(id);

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, sample))
    return await ComposeDetailAsync(sample);

_mapper.Map(dto, sample);
sample.UpdatedAt = editedAt;
var updated = await _repository.UpdateAsync(sample);
return await ComposeDetailAsync(updated);
```

- `PATCH` em amostra inativa continua aceito (edita sem reativar — Spec #14 §5.4).

| Validação (06/Out/2026) | Resultado |
|---|---|
| `updatedAt` UTC mais novo | ✅ Aplicado; `updatedAt` = horário do cliente (`00:19:05Z`) |
| `updatedAt` com fuso `-03:00` | ✅ `21:19:09-03:00` gravado como `00:19:09Z`; aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| Edição mais antiga | ✅ `200`, ignorada; devolve a versão do servidor (`name` "LWW v6" mantido) |
| Web sem `updatedAt` | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) / `updatedAt` no futuro | ✅ Aplicado; `updatedAt` limitado a "agora" |
| `PATCH` só `notes` | ✅ `name`, `bullBreed` (Holandesa) e `batchNumber` preservados — confirma o item 5 da §1.1 |

Registro de teste (Id 12) inativado ao final.

### 2.4 Fase 4 — Inativação e reativação idempotentes

```csharp
public async Task<bool> DeactivateAsync(int id)
{
    var sample = await FindAsync(id);
    if (!sample.IsActive)
        return false;

    sample.IsActive = false;
    sample.UpdatedAt = DateTime.UtcNow;
    await _repository.UpdateAsync(sample);
    return false;
}

public async Task<bool> ReactivateAsync(int id)
{
    var sample = await FindAsync(id);
    if (sample.IsActive)
        return true;

    sample.IsActive = true;
    sample.UpdatedAt = DateTime.UtcNow;
    await _repository.UpdateAsync(sample);
    return true;
}
```

- **Nenhuma das duas passa pelo LWW** (decisão S2): mesma regra da inativação na Spec #14 §5.4 — mudança de estado é intenção explícita e não leva `updatedAt`. Vale o efeito colateral já aceito na §3.3: se o celular inativar offline e a web reativar antes da sincronização, vence quem chegar por último ao servidor.
- As mensagens "A amostra de sêmen já está inativa." / "já está ativa." deixam de existir.

| Validação (06/Out/2026) | Resultado |
|---|---|
| `DELETE` em amostra ativa | ✅ `200 false`; `IsActive=0`, `RowVersion` 38114 → 38115 |
| `DELETE` repetido | ✅ `200 false`; `UpdatedAt` e `RowVersion` (38115) **inalterados** |
| `reactivate` em amostra inativa | ✅ `200 true`; `IsActive=1`, `RowVersion` 38115 → 38116 |
| `reactivate` repetido | ✅ `200 true`; `UpdatedAt` e `RowVersion` (38116) **inalterados** |
| Id inexistente (as duas rotas) | ✅ `404` "Amostra de sêmen com id '999999' não encontrada." |
| Cobertura com amostra inativa (regra existente) | ✅ Continua `409` "A amostra de sêmen selecionada está inativa." — regra de negócio, não reenvio; nenhuma cobertura gravada |

- A mensagem real da cobertura é "selecionada" (não "informada", como estava no rascunho); "informada" é a da gestação retroativa (`catalogo-erros-api.md`).
- Registro de teste (Id 13) termina inativo. A vaca de teste (Id 23, brinco `914201`) ficou ativa na propriedade de teste: a inativação de animal é pela rota de saída (`PATCH /{id}/exit`), fora deste escopo.

### 2.5 Fase 5 — Pull incremental

- `ISemenSampleRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<SemenSample>`.
- `ISemenSampleService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<SemenSampleDto>` (mesmo código da Spec #14 §10.5) + preenchimento de `AvailableDoses` com `GetAvailableDosesBatchAsync` sobre os itens da página (decisão S3).
- `SemenSamplesController`: `[HttpGet("changes")]` — sem colisão com `{id:int}` nem com `autocomplete`.

| Validação (06/Out/2026) | Resultado |
|---|---|
| Pull inicial sem `since` | ✅ 8 amostras (todas inativas, de testes anteriores), em ordem de `RowVersion`, com `availableDoses` corretos; `nextCursor` `38117` |
| Isolamento por propriedade | ✅ A tabela tinha 11 amostras; vieram só as 8 da propriedade do usuário |
| `since` = último cursor, sem mudanças | ✅ `items: []`, `nextCursor` mantido (`38117`) |
| Cria A, edita A, cria B, inativa B → pull | ✅ Só A (ativa, nome final "Pull A editada", 4 doses) e B (inativa), cada uma **uma vez** |
| Movimentação manual em A (saída de 1) → pull | ✅ `items: []` — A **não** reaparece (S3); `GET /{id}` mostra 3 doses |
| Paginação `limit=1` | ✅ 10 páginas (Ids 5–9, 11–15), sem repetir nem pular; última com `hasMore: false` |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |

Registros de teste (Ids 14 e 15) inativados ao final.

### 2.6 Fase 6 — Testes

`MuuBoi.Tests/Services/SemenSampleServiceTests.cs` (**novo**) — padrão de `MilkProductionServiceTests`/`VaccineServiceTests` (repositório mockado, AutoMapper real, sem testar mapeamento).

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesSampleWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 4 | `CreateAsync_WithInitialQuantity_CreatesSampleWithInputMovementInSameCall` |
| 5 | `CreateAsync_WithoutInitialQuantity_CreatesSampleWithoutMovements` |
| 6 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 7 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 8 | `UpdateAsync_WithSameClientUpdatedAt_AppliesChanges` |
| 9 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` |
| 10 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 11 | `UpdateAsync_WhenSampleNotFound_ThrowsNotFoundException` |
| 12 | `DeactivateAsync_WhenActive_DeactivatesAndReturnsFalse` |
| 13 | `DeactivateAsync_WhenAlreadyInactive_ReturnsFalseWithoutUpdating` |
| 14 | `ReactivateAsync_WhenInactive_ReactivatesAndReturnsTrue` |
| 15 | `ReactivateAsync_WhenAlreadyActive_ReturnsTrueWithoutUpdating` |
| 16 | `DeactivateAsync_WhenSampleNotFound_ThrowsNotFoundException` |
| 17 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 18 | `GetChangesAsync_WithoutCursor_RequestsChangesFromZero` |
| 19 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 20 | `GetChangesAsync_FillsAvailableDosesForPageItems` |

- O teste 4 verifica que `_repository.CreateAsync` recebe a amostra com **uma** movimentação de entrada (quantidade e observação do DTO) — é o que garante a Condição 1 no nível do service. A atomicidade em si (um `SaveChanges`) é do EF e fica coberta pela validação manual da §2.2.
- Os casos de borda dos helpers (fuso, relógio adiantado, limite, página vazia) já são cobertos pelos testes da `MilkProduction` e não são repetidos.
- **Resultado (06/Out/2026):** 20/20 passando; projeto inteiro 54/54 (19 `MilkProduction` + 15 `Vaccine` + 20 `SemenSample`).

### 2.7 Fase 7 — Documentação

- **Este documento:** status das fases e resultados de validação.
- **Spec #14 §8:** `SemenSample` → ✅; `Medication` → ❌ fora (CRUD descontinuado).
- **`spec-banco-semen.md`:** nota apontando para esta spec.
- **`spec-ajustes-banco-semen-doses.md`:** D9 e tabelas de campos — `BatchDate` removido (06/Out/2026, Spec #14.2).
- **`Docs/catalogo-erros-api.md` §4.13:** remover os `409` de `DELETE /{id}` e `PATCH /{id}/reactivate`; nota "rota com suporte offline" e `400` do cursor.

## 3. Decisões a confirmar

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| S1 | **Quantidade inicial via navegação `Movements`**, num único `SaveChanges`. A movimentação inicial **não** recebe `SyncId` agora (movimentações ainda não são sincronizáveis). | Transação explícita (`BeginTransactionAsync`) no service — exigiria expor a transação na interface do repositório e manteria duas gravações. Remover `initialQuantity` do `POST` offline — a quantidade inicial faz parte do cadastro e deve virar movimentação. |
| S2 | **Inativação e reativação sem LWW**, as duas idempotentes. | Reativação com `updatedAt` no corpo — criaria uma regra diferente da do `DELETE` para o mesmo campo `IsActive`. |
| S3 | **`availableDoses` vai no pull**, calculado no momento do pull, e o app o trata como **referência offline**: não é atualizado por movimentações até elas serem sincronizáveis. O saldo confiável continua sendo o do servidor (cobertura com dose indisponível → `422` no sync). | Tirar `availableDoses` do pull — quebraria a regra "mesmo DTO do detalhe" (Spec #14 §5.5) e deixaria o app sem nenhum saldo offline. Bumpar o `RowVersion` da amostra a cada movimentação — gravação extra em todo consumo de dose, com contenção na mesma linha. |

**Pendência para a parte de movimentações:** o usuário pode editar/inativar a entrada inicial pela rota de movimentações. Quando `SemenSampleMovement` virar sincronizável, a Condição 2 da Spec #14 §7.5 se aplica. → **Resolvida na Parte B (decisão M2):** a quantidade inicial continua no cadastro e é convertida em movimentação; o app informa o `initialMovementSyncId` e recebe `initialMovement { id, syncId }` na resposta.

## 4. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/SemenSample.cs` | `ISyncable` (`SyncId`, `RowVersion`); remove `BatchDate` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_SemenSample_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/SemenSampleRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` e `GetChangesAsync` via helpers |
| Application | `Application/DTOs/SemenSampleCreateDto.cs` | `SyncId?` + validação; remove `BatchDate` |
| Application | `Application/DTOs/SemenSampleUpdateDto.cs` | `UpdatedAt?`; remove `BatchDate` |
| Application | `Application/DTOs/SemenSampleDto.cs` | `SyncId`; remove `BatchDate` |
| Application | `Application/DTOs/SemenSampleListItemDto.cs`, `SemenSampleAutocompleteItemDto.cs` | Remove `BatchDate` |
| Application | `Application/Mappings/SemenSampleProfile.cs` | Ignora `SyncId`, `RowVersion`; `UpdatedAt` ignorado na edição |
| Application | `Application/Interfaces/ISemenSampleRepository.cs`, `ISemenSampleService.cs` | Novos métodos |
| Application | `Application/Interfaces/ISemenSampleMovementService.cs`, `Application/Services/SemenSampleMovementService.cs` | Remove `CreateForSemenSampleAsync` |
| Application | `Application/Services/SemenSampleService.cs` | Criação idempotente e atômica, LWW, inativação/reativação idempotentes, pull; sem dependência do serviço de movimentações |
| Api | `Api/Controllers/SemenSamplesController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/Services/SemenSampleServiceTests.cs` | **Novo** |

**Não muda:** `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `SemenSampleMovement` (modelo e rotas), `BreedingEvent*`, `AnimalPregnancy*`, rotas `GET /` e `GET /autocomplete`.

---

# Parte B — Movimentações manuais de sêmen

## 5. Plano offline das movimentações manuais

**Premissa (06/Out/2026): nenhuma regra de negócio é alterada.** A sincronização só acrescenta identidade (`SyncId`), versionamento (`RowVersion`), reenvio idempotente e pull. A única mudança de comportamento é a de "estado repetido" exigida pela A6 (§5.2). A Spec #14 §3.3 já classifica essa mudança como exceção de contrato, não como regra de negócio.

### 5.1 Escopo

**Movimentação manual** = `SemenSampleMovement` com `BreedingEventId = null`, criada pelo usuário em `/api/semen-samples/{semenSampleId}/movements` (entrada de compra, saída avulsa, ajuste), **mais a entrada inicial** criada pelo `initialQuantity` (Parte A, S1).

| Dentro | Fora |
|---|---|
| `POST`, `PATCH`, `DELETE /{semenSampleId}/movements[/{movementId}]` com suporte offline | **Criar ou inativar** as saídas geradas pelo sistema (consumo de dose pela cobertura). Elas nascem e morrem com o `BreedingEvent` e entram no offline junto com a spec da cobertura |
| Pull de **todas** as movimentações da propriedade, **inclusive** as geradas pelo sistema e as inativas (M1) | Regra de saldo da cobertura offline (`422` "Não há doses disponíveis…"). Ela pertence à cobertura (questão em aberto 1 da Spec #14) |
| | Rotas de tela `GET /{semenSampleId}/movements` e `GET /{semenSampleId}/movements/{movementId}` (sem mudança de contrato) |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /{semenSampleId}/movements` | Sempre cria | Aceita `syncId`; se já existir → devolve a existente (`201`) |
| `PATCH /{semenSampleId}/movements/{movementId}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW) |
| `DELETE /{semenSampleId}/movements/{movementId}` | `204`; **`409`** se já inativa | `204` **sem gravar** se já inativa |
| `GET /api/semen-samples/movements/changes?since=&limit=` | — | **Nova** (M1) |
| `GET /{semenSampleId}/movements/{movementId}` e respostas de `POST`/`PATCH` | — | Ganham `syncId` (aditivo) |

### 5.2 Regras de negócio preservadas

Todas continuam **exatamente** como hoje, na mesma ordem de checagem, e passam a ter teste unitário (Fase B6):

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Amostra precisa existir | Todas as rotas | `404` "Amostra de sêmen com id '{semenSampleId}' não encontrada." |
| R2 | Movimentação precisa existir | `PATCH`, `DELETE` | `404` "Movimentação com id '{movementId}' não encontrada." |
| R3 | Não registra movimentação em amostra inativa | `POST` | `409` "Não é possível registrar movimentação para uma amostra de sêmen inativa." |
| R4 | Movimentação gerada pelo sistema não é editável | `PATCH` | `409` "Movimentações geradas pelo sistema não podem ser editadas diretamente." |
| R5 | Movimentação gerada pelo sistema não é inativável | `DELETE` | `409` "Movimentações geradas pelo sistema não podem ser inativadas diretamente." Vale também se ela já estiver inativa, porque R5 é checada antes do estado |
| R6 | Validação do DTO: tipo e data obrigatórios, quantidade de 1 a 9.999, observações até 500 caracteres | `POST`, `PATCH` | `400` (formato B) |
| R7 | **Não há** checagem de saldo em saídas manuais, de data futura, nem de amostra ativa no `PATCH`/`DELETE` | — | Continua sem checagem. Uma saída manual ainda pode deixar o saldo negativo, como hoje |
| R8 | Saldo = Σ entradas ativas − Σ saídas ativas | `SemenSampleRepository.GetAvailableDosesAsync` | Sem mudança. O app usa a mesma fórmula sobre os dados do pull |

**Única mudança de comportamento:** o `DELETE` em movimentação manual já inativa deixa de responder `409` "A movimentação já está inativa." e passa a responder `204` sem gravar (A6, Spec #14 §3.3 e §5.4). Isso não é regra de negócio: é o reenvio de uma operação que já foi aplicada.

**Ordem entre `SyncId` e regras:** como na Parte A e na Spec #14 §10.2, o `SyncId` é checado **antes** de R1/R3. Se o primeiro envio foi gravado e depois outro dispositivo inativou a amostra, o reenvio responde `201` com a movimentação existente, porque a operação já tinha dado certo. Um `POST` **novo** (com outro `syncId`) em amostra inativa continua `409` (R3).

### 5.3 Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `SemenSampleMovement` sem `SyncId`/`RowVersion` | O reenvio do `POST` **duplica a movimentação**, e com isso altera o saldo | Fase B1 + criação idempotente (B2) |
| 2 | `DeactivateAsync` lança `ConflictException` se já inativa | Viola a A6 | Retorna sem gravar, **depois** de R5 (B4) |
| 3 | `UpdateAsync` atribui campo a campo (não usa AutoMapper) e carimba `UpdatedAt = DateTime.UtcNow` | O LWW precisa do horário do cliente | `ResolveEditedAt`/`IsOutdated` **depois** de R4 (B3). Como não há profile de edição, não existe risco do bug do `ForAllMembers` (14.1 §1.1 item 7) |
| 4 | `SemenSampleMovementDto` não tem `SyncId` | O app não consegue casar o registro do pull com o local | `Guid SyncId` no DTO (B2) |
| 5 | O `SemenSampleName` do DTO vem da navegação `SemenSample`, e o helper `GetChangesSinceAsync` não faz `Include` | No pull, o nome viria vazio | Decisão M4 |
| 6 | A entrada inicial (S1) recebe o `SyncId` do servidor (`DEFAULT NEWID()`), e o `POST` da amostra não devolve o `Id` dela | O app não consegue casar a entrada do pull com a local, nem endereçar uma edição dela feita antes do primeiro sync (Condição 2, Spec #14 §7.5) | Decisão M2: `initialMovementSyncId` na entrada e `initialMovement { id, syncId }` na resposta. A conversão em movimentação (S1) não muda |
| 7 | As saídas geradas pela cobertura (`CreateForBreedingEventAsync`) e a inativação delas (`InactivateForBreedingEventAsync`) usam o mesmo repositório | Com `CreateAsync` → `AddSyncableAsync`, elas passam a receber `SyncId` gerado pelo banco e a aparecer no pull | É o comportamento desejado (M1). O fluxo da cobertura **não muda** |
| 8 | **Achado (bug pré-existente):** `GetByIdAsync(movementId)` não confere se a movimentação pertence ao `semenSampleId` da rota. `PATCH /1/movements/50` altera a movimentação 50 mesmo que ela seja da amostra 2 (mesmo tenant), e a resposta sai com o nome da amostra 1 | Nenhum direto: o app sempre envia o par certo | **Não corrigido aqui**, pela premissa de não alterar regras. Questão em aberto Q1 (§5.8) |
| 9 | Questão em aberto 1 da Spec #14 (saldo negativo no sync) | Para movimentações **manuais** não existe regra de saldo (R7). Duas saídas offline simplesmente somam, como online, e o modelo append-only converge | **Resolvida para as manuais:** nada a fazer. Continua aberta para a cobertura |
| 10 | O `AccountRepository` remove movimentações fisicamente | Só acontece na exclusão da conta | Sem mudança |

No banco local hoje há 11 movimentações: 9 manuais e 2 geradas pelo sistema.

### 5.4 Decisões a confirmar

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| M1 | **Pull global:** `GET /api/semen-samples/movements/changes` traz as movimentações de **todas** as amostras da propriedade, inclusive as geradas pelo sistema e as inativas. Com elas o app calcula o saldo localmente (R8). Isso também melhora a S3: o `availableDoses` do pull da amostra deixa de ser a única fonte de saldo offline | Pull por amostra (`/{semenSampleId}/movements/changes`): um request por amostra, ruim num link instável. Rota raiz `/api/semen-movements/changes`: criaria um prefixo novo só para o pull |
| M2 | **A quantidade inicial continua no cadastro da amostra e é convertida em movimentação pelo servidor** (S1, regra mantida). Para a Condição 2 (Spec #14 §7.5), o app **gera o `SyncId` da movimentação derivada**: `SemenSampleCreateDto.InitialMovementSyncId` (`Guid?`, opcional — ausente, o servidor gera). A resposta do `POST` traz `initialMovement { id, syncId }`, para o app gravar o `ServerId` da entrada e endereçá-la depois (A5, Spec #14 §5.8). O reenvio devolve o mesmo `initialMovement` | Cadastrar a amostra sem quantidade e enviar a entrada como `POST /movements` separado — mudaria o fluxo de cadastro (a quantidade inicial faz parte do cadastro). Descobrir o `Id` da entrada pelo pull (M1) antes de enviar a edição — faria o envio depender de um pull no meio da fila |
| M3 | **`DELETE` repetido → `204` sem gravar**, checado **depois** de R5. Uma movimentação do sistema continua dando `409`, mesmo se já estiver inativa | Checar o estado antes de R5. Mudaria a resposta de uma regra existente |
| M4 | **`SemenSampleName` no pull preenchido por uma consulta em lote** (`ISemenSampleRepository.GetNamesByIdsAsync`), no mesmo padrão do `availableDoses` da Parte A | `Include` no helper genérico `GetChangesSinceAsync`, que é compartilhado por todas as entidades. Deixar o nome vazio no pull, o que quebra a regra "mesmo DTO do detalhe" (Spec #14 §5.5) com um valor enganoso |

### 5.5 Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** a amostra criada offline é enviada **antes** das suas movimentações. O `Id` devolvido no `201` da amostra é usado na rota das movimentações (A2, A5).
2. **Entrada inicial offline (M2):** um único item na fila — `POST /api/semen-samples` com `syncId`, `initialQuantity` e `initialMovementSyncId`. O app grava localmente a amostra **e** a entrada (com o `SyncId` que gerou); no `201`, grava `ServerId` das duas (`id` e `initialMovement.id`). Uma edição ou inativação da entrada feita antes do sync vai para a fila **depois** do `POST` da amostra e usa `initialMovement.id` na rota.
3. **Saldo offline:** Σ entradas ativas − Σ saídas ativas sobre as movimentações locais (R8). As saídas geradas pelo sistema chegam só pelo pull (`breedingEventId != null`, `isSystemGenerated: true`) e são **somente leitura** no app (R4, R5).
4. **`409` de R3** (outro dispositivo inativou a amostra antes do sync) é **conflito real**: o item vira `Failed` e o usuário é avisado (A3). Não é reenvio.

### 5.6 Fases de implementação e validação

| Fase | Status |
|---|---|
| B1 — Domínio e banco | ✅ Migração `20261007004410_Offline_SemenSampleMovement_SyncId_RowVersion` aplicada |
| B2 — Criação idempotente | ✅ Implementada e validada (inclui M2) |
| B3 — Edição com last-write-wins | ✅ Implementada e validada |
| B4 — Inativação idempotente | ✅ Implementada e validada |
| B5 — Pull incremental | ✅ Implementada e validada |
| B6 — Testes | ✅ 19 testes de `SemenSampleMovementService` + 3 da M2 em `SemenSampleServiceTests`; 76 no projeto |
| B7 — Documentação | ✅ Spec #14 §8 e §13 (questão 1), `catalogo-erros-api.md` §4.13 |

Mesma branch (`feature/offline-semen-samples`). Cada fase é proposta com o código exato e só implementada após aprovação.

#### B1 — Domínio e banco

- `SemenSampleMovement : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<SemenSampleMovement>().ConfigureSyncable();`
- Migração `Offline_SemenSampleMovement_SyncId_RowVersion` (⚠️ **requer aprovação**). SQL esperado:

```sql
ALTER TABLE [SemenSampleMovements] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [SemenSampleMovements] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_SemenSampleMovements_PropertyId_RowVersion] ON [SemenSampleMovements] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_SemenSampleMovements_SyncId] ON [SemenSampleMovements] ([SyncId]);
```

| Validação (06/Out/2026) | Resultado |
|---|---|
| As 11 movimentações existentes ganham `SyncId` distintos, nenhum vazio, e `RowVersion` preenchido | ✅ 11 movimentações (9 manuais, 2 da cobertura), 11 `SyncId` distintos, nenhum vazio |
| Índices | ✅ `PK`, os 3 existentes, `IX_SemenSampleMovements_PropertyId_RowVersion` e `UX_SemenSampleMovements_SyncId` |
| Nenhuma outra tabela alterada | ✅ A migração só toca `SemenSampleMovements` |
| A entrada inicial da Parte A (S1) e a saída da cobertura continuam gravando, com `SyncId` gerado pelo banco | ✅ Amostra 16 com `initialQuantity: 2` → entrada Id 15 com `SyncId` gerado; cobertura com IA → saída Id 16 (`BreedingEventId` 11) com `SyncId` gerado; saldo 2 → 1 |
| Inativar a cobertura | ✅ `204`; saída inativada e `RowVersion` 38137 → 38138 (vai aparecer no pull como tombstone) |

Registros de teste (amostra 16, cobertura 11) inativados ao final.

#### B2 — Criação idempotente

- `SemenSampleMovementCreateDto`: `Guid? SyncId` + `IValidatableObject` (`Guid.Empty` → `400` "O identificador de sincronização não pode ser vazio.").
- `SemenSampleMovementDto`: `Guid SyncId`.
- `SemenSampleMovementProfile` (criação): ignora `SyncId` e `RowVersion`.
- `ISemenSampleMovementRepository` e repositório:
  - `GetBySyncIdAsync` com `Include(m => m.SemenSample)`, para ter o nome na resposta;
  - `CreateAsync` → `AddSyncableAsync`.
- `SemenSampleMovementService.CreateAsync`: só o bloco do `SyncId` é novo; R1 e R3 continuam idênticos.

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return _mapper.Map<SemenSampleMovementDto>(existing);
}

var semenSample = await _semenSampleRepository.GetByIdAsync(semenSampleId)
    ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

if (!semenSample.IsActive)
    throw new ConflictException("Não é possível registrar movimentação para uma amostra de sêmen inativa.");

var movement = _mapper.Map<SemenSampleMovement>(dto);
movement.SemenSampleId = semenSampleId;
movement.SyncId = dto.SyncId ?? Guid.NewGuid();
```

| Validação (06/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` (entrada de 5) | ✅ `201`, `Id` 17; saldo 0 → 5 |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id`; 1 linha; saldo **inalterado** (5) |
| Mesmo `syncId`, payload diferente (saída de 3) | ✅ `201`, devolve a entrada de 5 sem alterar; saldo 5 |
| `POST` sem `syncId` (saída de 1) | ✅ `201`, `syncId` gerado; saldo 4 |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (campo `SyncId`) |
| R3: `POST` novo em amostra inativa | ✅ `409` "Não é possível registrar movimentação para uma amostra de sêmen inativa." (regra mantida) |
| Reenvio de `POST` já gravado, depois de a amostra ser inativada | ✅ `201` com a existente (`Id` 17) (§5.2) |
| R1: amostra inexistente | ✅ `404` (regra mantida) |
| R6: quantidade 0 / 10.000 | ✅ `400` "A quantidade deve ser entre 1 e 9.999." (regra mantida) |
| 20 envios simultâneos (mesmo `syncId`, entrada de 2) | ✅ Todos `201`; 1 linha; saldo 4 → 6 |
| Cobertura com IA depois da mudança no repositório | ✅ `201`; saída (`Id` 23) gravada via `AddSyncableAsync` com `SyncId` gerado pelo banco; fluxo da cobertura inalterado |

- **Primeira vez que o `catch` da corrida foi exercitado** (nas Partes A, 14.1 e na produção de leite ele não tinha sido): nos 20 envios simultâneos, dois passaram juntos pela checagem do service; o banco barrou o segundo em `UX_SemenSampleMovements_SyncId`, o `AddSyncableAsync` tratou a violação e devolveu a existente. Resultado: todos `201`, 1 linha.
- Mesma diferença de formato já registrada na Spec #14 §10.2: o reenvio devolve datas sem `Z`.

**Entrada inicial com `SyncId` do app (M2)** — também na B2, porque depende do `SyncId` nas movimentações:

- `SemenSampleCreateDto`: `Guid? InitialMovementSyncId` + validação no `Validate` existente (`Guid.Empty` → `400` "O identificador de sincronização da entrada inicial não pode ser vazio."). Sem `initialQuantity`, o campo é **ignorado** (não vira erro, para não criar regra nova).
- `SemenSampleCreatedDto : SemenSampleDto` (**novo**) com `SemenSampleMovementRefDto? InitialMovement` (`Id`, `SyncId`). Só o `POST` devolve esse formato; `GET /{id}`, `PATCH` e o pull continuam com `SemenSampleDto`.
- `SemenSampleProfile`: `CreateMap<SemenSample, SemenSampleCreatedDto>().IncludeBase<SemenSample, SemenSampleDto>()`, ignorando `InitialMovement`.
- `SemenSampleService` passa a receber `ISemenSampleMovementRepository` (já registrado na DI — **sem mudança no `Program.cs`**). `CreateAsync` devolve `SemenSampleCreatedDto`:

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return await ComposeCreatedAsync(existing, dto.InitialMovementSyncId);
}

var sample = _mapper.Map<SemenSample>(dto);
sample.SyncId = dto.SyncId ?? Guid.NewGuid();

if (dto.InitialQuantity.HasValue)
    sample.Movements = new List<SemenSampleMovement>
    {
        new()
        {
            SyncId = dto.InitialMovementSyncId ?? Guid.NewGuid(),
            MovementType = SemenMovementType.Input,
            // ...demais campos como hoje (S1)
        }
    };

var created = await _repository.CreateAsync(sample);
return await ComposeCreatedAsync(created, sample.Movements?.FirstOrDefault()?.SyncId);
```

- `ComposeCreatedAsync(sample, initialMovementSyncId)`: monta o detalhe (como `ComposeDetailAsync`) e, se houver `initialMovementSyncId`, busca a movimentação por `_movementRepository.GetBySyncIdAsync` **desde que pertença à amostra** e preenche `InitialMovement`. No reenvio, a entrada é localizada pelo `initialMovementSyncId` que o app manda de novo; se o reenvio vier sem ele, `initialMovement` vem `null` (o app sempre envia — §5.5).
- `SemenSamplesController.Create`: `ActionResult<SemenSampleCreatedDto>`; `CreatedAtAction` igual.
- A conversão da quantidade inicial em movimentação, a atomicidade (S1) e as validações de `initialQuantity`/`initialNotes` **não mudam**.

| Validação (06/Out/2026) | Resultado |
|---|---|
| `POST` com `initialQuantity: 10` e `initialMovementSyncId` | ✅ `201`; amostra 18; movimentação 21 gravada com o `SyncId` do app; `initialMovement { id: 21, syncId }` |
| Reenvio idêntico | ✅ `201`; mesmo `initialMovement`; 1 amostra + 1 movimentação |
| Reenvio **sem** `initialMovementSyncId` | ✅ `201`; `initialMovement: null` (comportamento documentado); nada duplicado |
| `POST` com `initialQuantity` sem `initialMovementSyncId` | ✅ `201`; `SyncId` gerado; `initialMovement` preenchido (movimentação 22) |
| `POST` sem `initialQuantity`, com `initialMovementSyncId` | ✅ `201`; `initialMovement: null`; campo ignorado; nenhuma movimentação |
| `initialMovementSyncId` vazio | ✅ `400` "O identificador de sincronização da entrada inicial não pode ser vazio." (campo `InitialMovementSyncId`) |
| `PATCH /18/movements/21` (pela `initialMovement.id`) logo após o `POST` | ✅ `200`; quantidade 10 → 12; saldo 12 — a entrada é endereçável pelo app (Condição 2) |
| Pull de movimentações (B5) | ✅ A entrada chega com o `syncId` gerado pelo app (ver B5) |
| Teste de falha da Parte A (trigger na movimentação) | ✅ `500`; nenhuma amostra e nenhuma movimentação gravadas (atomicidade mantida). Trigger removido |

- Na resposta do `POST`, o `initialMovement` aparece **antes** do `id` no JSON (o serializador escreve primeiro as propriedades da classe derivada). Não afeta o app: a ordem dos campos no JSON não tem significado.
- Registros de teste (amostras 17–20, cobertura 12) inativados ao final.

- **Ponto de atenção:** se o `initialMovementSyncId` repetir o de **outra** movimentação já existente (erro do app), a violação de `UX_SemenSampleMovements_SyncId` não é a da amostra e o `AddSyncableAsync` não a trata → `500`. Aceito: o UUID v7 gerado no app torna isso impraticável (A4).

#### B3 — Edição com last-write-wins

- `SemenSampleMovementUpdateDto`: `DateTime? UpdatedAt`.
- `SemenSampleMovementService.UpdateAsync`: R1, R2 e R4 continuam idênticos e vêm **antes** do LWW. As atribuições campo a campo também não mudam.

```csharp
if (movement.BreedingEventId.HasValue)
    throw new ConflictException("Movimentações geradas pelo sistema não podem ser editadas diretamente.");

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, movement))
    return _mapper.Map<SemenSampleMovementDto>(movement);

if (dto.MovementDate.HasValue)
    movement.MovementDate = dto.MovementDate.Value;
// ...Quantity e Notes como hoje

movement.UpdatedAt = editedAt;
```

- Quando a edição é ignorada, o nome na resposta vem da navegação que o `GetByIdAsync` já carrega (`Include`).

| Validação (06/Out/2026) | Resultado |
|---|---|
| `updatedAt` UTC mais novo | ✅ Aplicado (quantidade 10 → 11); `updatedAt` = horário do cliente (`00:54:17Z`) |
| `updatedAt` com fuso `-03:00` | ✅ `21:54:20-03:00` gravado como `00:54:20Z`; aplicado (11 → 12) |
| Reenvio idêntico | ✅ `200`, mesmo resultado |
| Edição mais antiga (−1 h, quantidade 99) | ✅ `200`, ignorada; quantidade 12 mantida |
| Sem `updatedAt` | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) | ✅ Aplicado (12 → 13); `updatedAt` limitado a "agora" |
| Editar a quantidade recalcula o saldo | ✅ `availableDoses` acompanhou: 11 → 12 → 13 |
| R4: `PATCH` em saída gerada pela cobertura, com `updatedAt` | ✅ `409` "Movimentações geradas pelo sistema não podem ser editadas diretamente." (regra mantida) |
| `PATCH` em movimentação inativa | ✅ `200`, aceito como hoje (R7) |

Amostra 22 (entrada 24) e cobertura 13 (saída 25) mantidas ativas para a B4 e a B5.

#### B4 — Inativação idempotente

```csharp
if (movement.BreedingEventId.HasValue)
    throw new ConflictException("Movimentações geradas pelo sistema não podem ser inativadas diretamente.");

if (!movement.IsActive)
    return;

movement.IsActive = false;
movement.UpdatedAt = DateTime.UtcNow;
await _repository.UpdateAsync(movement);
```

| Validação (06/Out/2026) | Resultado |
|---|---|
| `DELETE` em movimentação manual ativa (entrada 27, 4 doses) | ✅ `204`; `IsActive=0`, `RowVersion` 38172 → 38173; saldo 16 → 12 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` e `RowVersion` (38173) **inalterados** |
| R5: `DELETE` em saída da cobertura **ativa** (25) | ✅ `409` "Movimentações geradas pelo sistema não podem ser inativadas diretamente."; nada gravado |
| R5: `DELETE` na mesma saída **já inativa** (após inativar a cobertura 13) | ✅ `409` — R5 continua antes do estado (M3); `RowVersion` (38174) inalterado |
| Ids inexistentes | ✅ `404` "Amostra de sêmen com id '999999' não encontrada." (R1) e "Movimentação com id '999999' não encontrada." (R2) |

#### B5 — Pull incremental

- `ISemenSampleMovementRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<SemenSampleMovement>`.
- `ISemenSampleRepository.GetNamesByIdsAsync(IEnumerable<int> ids)` → `Dictionary<int, string>` (M4). O tenant vem do `HasQueryFilter`, e amostras inativas entram.
- `ISemenSampleMovementService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<SemenSampleMovementDto>`. É o mesmo código da Parte A §2.5, preenchendo o `SemenSampleName` pelo dicionário.
- `SemenSamplesController`: `[HttpGet("movements/changes")]`. Não colide com `{id:int}` nem com `{semenSampleId:int}/movements`.

| Validação (06/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 20 movimentações (17 manuais, 3 do sistema, 5 inativas), todas com `syncId` e `semenSampleName`, sem repetição; `nextCursor` `38174` |
| Isolamento por propriedade | ✅ A tabela tinha 26 movimentações; vieram só as da propriedade do usuário |
| Entrada inicial da M2 | ✅ Chega com o `syncId` gerado pelo app (`44444444-…0001`, movimentação 24) |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido |
| Cria A (7) → edita A (8), cria B → inativa B → pull | ✅ Só A (8, ativa) e B (inativa), cada uma **uma vez** |
| Cobertura com IA → pull | ✅ A saída 30 aparece com `isSystemGenerated: true`, `breedingEventId` 14 e o nome da amostra |
| Inativar a cobertura → pull | ✅ A saída 30 reaparece inativa (tombstone) |
| Paginação `limit=1` | ✅ 23 páginas, 23 itens, sem repetir nem pular — os mesmos do pull completo |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |
| **Saldo calculado sobre o pull = `availableDoses` do servidor** | ✅ 16 amostras, **nenhuma divergência** — confirma que o app consegue calcular o saldo offline com a fórmula R8 (M1) |

Registros de teste (amostra 22, coberturas 13 e 14) inativados ao final. Validação feita com um script Node (`fetch`), por não haver `jq` no ambiente.

#### B6 — Testes

`MuuBoi.Tests/Services/SemenSampleMovementServiceTests.cs` (**novo**):

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesMovementWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncIdAndInactiveSample_ReturnsExisting` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAsync_WhenSampleNotFound_ThrowsNotFoundException` (R1) |
| 6 | `CreateAsync_WhenSampleInactive_ThrowsConflictException` (R3) |
| 7 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 8 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 9 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` |
| 10 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 11 | `UpdateAsync_WhenSystemGenerated_ThrowsConflictException` (R4) |
| 12 | `UpdateAsync_WhenMovementNotFound_ThrowsNotFoundException` (R2) |
| 13 | `DeactivateAsync_WhenActive_Deactivates` |
| 14 | `DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating` |
| 15 | `DeactivateAsync_WhenSystemGenerated_ThrowsConflictException` (R5) |
| 16 | `DeactivateAsync_WhenSystemGeneratedAndInactive_ThrowsConflictException` (R5 antes do estado — M3) |
| 17 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 18 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 19 | `GetChangesAsync_FillsSemenSampleNameForPageItems` |

Os testes 5, 6, 11, 12, 15 e 16 fixam regras que já existem e hoje não têm cobertura.

- **Resultado (06/Out/2026):** 19/19 em `SemenSampleMovementServiceTests` e 23/23 em `SemenSampleServiceTests` (20 da Parte A + 3 da M2); projeto inteiro **76/76** (19 `MilkProduction` + 15 `Vaccine` + 23 `SemenSample` + 19 `SemenSampleMovement`).
- O mapper dos testes de `SemenSampleService` passou a registrar também o `SemenSampleMovementProfile`, porque a resposta do `POST` mapeia `SemenSampleMovementRefDto`.

**Em `SemenSampleServiceTests` (Parte A), pela M2:** o construtor passa a receber o mock de `ISemenSampleMovementRepository`, e entram mais 3 testes:

| # | Teste |
|---|---|
| 21 | `CreateAsync_WithInitialMovementSyncId_CreatesMovementWithGivenSyncIdAndReturnsIt` |
| 22 | `CreateAsync_WithExistingSyncIdAndInitialMovementSyncId_ReturnsSameInitialMovement` |
| 23 | `CreateAsync_WithoutInitialQuantity_ReturnsNullInitialMovement` |

#### B7 — Documentação

- **Esta spec:** status das fases e resultados.
- **Spec #14 §8:** `SemenSampleMovement` → ✅ para as movimentações manuais (as do sistema seguem com a cobertura). A questão em aberto 1 fica resolvida para as manuais.
- **`catalogo-erros-api.md` §4.13:** remover o `409` "A movimentação já está inativa."; acrescentar o `400` do cursor da rota nova e a nota de rota offline.

### 5.7 Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/SemenSampleMovement.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_SemenSampleMovement_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/SemenSampleMovementRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Infrastructure | `Infrastructure/Repositories/SemenSampleRepository.cs` | `GetNamesByIdsAsync` |
| Application | `Application/DTOs/SemenSampleMovementCreateDto.cs` | `SyncId?` + validação |
| Application | `Application/DTOs/SemenSampleMovementUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/SemenSampleMovementDto.cs` | `SyncId` |
| Application | `Application/Mappings/SemenSampleMovementProfile.cs` | Ignora `SyncId` e `RowVersion` na criação |
| Application | `Application/Interfaces/ISemenSampleMovementRepository.cs`, `ISemenSampleMovementService.cs`, `ISemenSampleRepository.cs` | Novos métodos |
| Application | `Application/Services/SemenSampleMovementService.cs` | Criação idempotente, LWW, inativação idempotente, pull |
| Application | `Application/DTOs/SemenSampleCreateDto.cs` | `InitialMovementSyncId?` + validação (M2) |
| Application | `Application/DTOs/SemenSampleCreatedDto.cs`, `SemenSampleMovementRefDto.cs` | **Novos** — resposta do `POST` com `initialMovement` (M2) |
| Application | `Application/Mappings/SemenSampleProfile.cs` | Mapa para `SemenSampleCreatedDto` (M2) |
| Application | `Application/Interfaces/ISemenSampleService.cs`, `Application/Services/SemenSampleService.cs` | `CreateAsync` devolve `SemenSampleCreatedDto`; usa `ISemenSampleMovementRepository` (M2) |
| Api | `Api/Controllers/SemenSamplesController.cs` | Rota `GET movements/changes`; `Create` devolve `SemenSampleCreatedDto` |
| Tests | `MuuBoi.Tests/Services/SemenSampleMovementServiceTests.cs` | **Novo** |
| Tests | `MuuBoi.Tests/Services/SemenSampleServiceTests.cs` | Mock novo no construtor + 3 testes (M2) |

**Não muda:** regras R1–R8, a conversão da quantidade inicial em movimentação (S1), `Program.cs`/DI, helpers de sincronização, `BreedingEventService` (inclusive `CreateForBreedingEventAsync`/`InactivateForBreedingEventAsync`) e as rotas de tela das movimentações.

### 5.8 Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Movimentação de outra amostra pela rota** (§5.3 item 8): `GET`/`PATCH`/`DELETE /{semenSampleId}/movements/{movementId}` não confere se a movimentação pertence à amostra da rota | ⏳ Bug pré-existente, deixado fora desta parte para não alterar regras. Correção sugerida numa tarefa própria: `404` "Movimentação com id '{movementId}' não encontrada." quando `movement.SemenSampleId != semenSampleId` |
| Q2 | **`InactivateForBreedingEventAsync` regrava** se a saída já estiver inativa (o `RowVersion` avança sem mudança real) | ⏳ Inofensivo hoje, porque a cobertura barra a inativação repetida com `409` antes. Tratar na spec offline da cobertura |
