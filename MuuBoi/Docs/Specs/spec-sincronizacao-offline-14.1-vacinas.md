# Spec #14.1: Sincronização Offline — Catálogo de Vacinas

**Módulo:** Infraestrutura / Sincronização — Controle Sanitário
**Versão:** 1.0
**Data:** 05/Out/2026
**Status:** 🟢 **Plano aprovado.** Implementação pendente.
**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2) e receita (§7.4). Esta spec **não** redecide nada da #14; só aplica a receita ao catálogo `Vaccine`.
**Relaciona-se com:** `spec-controle-sanitario-vacinacao.md` · `Docs/catalogo-erros-api.md` §4.6

---

## 1. Escopo e diferenças em relação à produção de leite

**Escopo:** somente o **catálogo de vacinas** (`Vaccine`, rotas `/api/vaccines`). Ficam **fora** desta parte: `VaccinationEvent`, `VaccinationEventAnimal` e o reforço (`POST /{id}/booster`) — terão parte própria.

**Por que agora:** é um catálogo (Spec #14 §8, ordem sugerida), sem FK para outras entidades sincronizáveis — como a `MilkProduction`, não exige a resolução de `Id` da A5. Cada operação grava **um único registro**, então a Condição 1 da Spec #14 §7.5 (uma transação por operação) não se aplica.

**Dependência futura:** `VaccinationEvent` referencia `VaccineId`. Quando os eventos entrarem no offline, uma vacina criada offline precisará estar sincronizada antes do evento que a usa — garantido pelo envio sequencial e pela resolução do `ServerId` no app (A2, A5, Spec #14 §5.8).

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/vaccines` | Sempre cria | Aceita `syncId`; se já existir → devolve a existente (`201`) |
| `PATCH /api/vaccines/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW) |
| `DELETE /api/vaccines/{id}` | `204`, mas **regrava** se já inativa | `204` **sem gravar** se já inativa |
| `GET /api/vaccines/changes?since=&limit=` | — | **Nova** |
| `GET /api/vaccines`, `GET /{id}` | — | Resposta ganha `syncId` (aditivo) |

### 1.1 Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | Sem regra de nome único | Reenvio do `POST` **duplica a vacina silenciosamente** (mesmo caso da pesagem, Spec #14 §5.7) | `SyncId` + criação idempotente (Fase 2) |
| 2 | `VaccineRepository.DeleteVaccineAsync` inativa e carimba `UpdatedAt` **mesmo se já inativa** | `RowVersion` avança e o registro "reaparece" no pull — viola a Spec #14 §5.4 | Service retorna antes de chamar o repositório (Fase 4) |
| 3 | `PATCH` via AutoMapper; o `VaccineProfile` grava `UpdatedAt = DateTime.UtcNow` | Com `UpdatedAt` no DTO de edição, o mapeamento precisa ser controlado para não copiar o valor bruto do cliente (sem normalizar fuso nem limitar a "agora") | Profile ignora `UpdatedAt`, `SyncId` e `RowVersion`; LWW no service (Fase 3) |
| 4 | `VaccineDto` não expõe `Manufacturer` nem `RecommendedIntervalDays` | O pull entrega o mesmo DTO do detalhe (Spec #14 §5.5), então esses campos não chegam ao app | **Decidido (05/Out/2026): continuam fora do `VaccineDto`** — não são usados pelo sistema e expô-los induziria a erro (ex.: `RecommendedIntervalDays` sugere um intervalo que não é aplicado em nenhuma regra). Ficam só no banco; não fazem parte do contrato de sincronização |
| 5 | `VaccineCatalogSeeder` cria 4 vacinas padrão por propriedade, sem `SyncId` | Precisam de `SyncId` para entrar no pull | `DEFAULT NEWID()` do `ConfigureSyncable()` preenche; validar na Fase 1 que o EF omite o `Guid.Empty` no `INSERT` |
| 6 | `AccountRepository` remove as vacinas fisicamente (`ExecuteDeleteAsync`) | Nenhum: só ocorre na exclusão da conta, quando a propriedade inteira deixa de existir (cursores perdem o sentido, Spec #14 §6 item 10) | Sem mudança |

> Diferente da produção de leite, o `PATCH` da vacina **continua no AutoMapper** (regra do `CLAUDE.md`). ⚠️ **Correção (05/Out/2026):** a premissa de que a condição `srcMember != null` protegia todos os campos estava errada — ver item 7.

| # | Situação atual | Impacto | Tratamento |
|---|---|---|---|
| 7 | **Bug pré-existente** (encontrado na validação da Fase 3): `RequiresBooster` é `bool?` no DTO e `bool` na entidade. No AutoMapper, o `srcMember` da condição chega já convertido para `false`, não `null` — a condição passa e o campo é sobrescrito | Todo `PATCH` que **não** envia `requiresBooster` grava `false` (inclusive na web, hoje). No offline, uma edição só do nome apagaria o `true` | ✅ Corrigido (05/Out/2026): `.ForMember(dest => dest.RequiresBooster, opt => opt.PreCondition(src => src.RequiresBooster.HasValue))` no profile de edição — a `PreCondition` avalia o DTO antes da conversão |

## 2. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco | ✅ Migração `20261005235404_Offline_Vaccine_SyncId_RowVersion` aplicada |
| 2 — Criação idempotente | ✅ Implementada e validada |
| 3 — Edição com last-write-wins | ✅ Implementada e validada (inclui a correção do item 7 da §1.1) |
| 4 — Inativação idempotente | ✅ Implementada e validada |
| 5 — Pull incremental | ✅ Implementada e validada |
| 6 — Testes | ✅ 15 testes de `VaccineService` passando (34 no projeto, com os 19 da produção de leite) |
| 7 — Documentação | ⏳ |

Branch: `feature/offline-vaccines` (criada a partir de `feature/offline-milk-production`, que contém a base offline). Cada fase é proposta com o código exato e só implementada após aprovação; validação manual na API local, como na produção de leite (Spec #14, Parte II).

### 2.1 Fase 1 — Domínio e banco

- `Vaccine : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<Vaccine>().ConfigureSyncable();` (o índice `IX_Vaccines_PropertyId` existente permanece).
- Migração `Offline_Vaccine_SyncId_RowVersion` (⚠️ **requer aprovação**). SQL esperado:

```sql
ALTER TABLE [Vaccines] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [Vaccines] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_Vaccines_PropertyId_RowVersion] ON [Vaccines] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_Vaccines_SyncId] ON [Vaccines] ([SyncId]);
```

| Validação (05/Out/2026) | Resultado |
|---|---|
| Vacinas existentes após a migração | ✅ 12 vacinas, 12 `SyncId` distintos, nenhum vazio; `RowVersion` preenchido |
| Outras tabelas | ✅ Nenhuma alteração (a migração só toca `Vaccines`) |
| Índices | ✅ `PK_Vaccines`, `IX_Vaccines_PropertyId`, `IX_Vaccines_PropertyId_RowVersion`, `UX_Vaccines_SyncId` |
| Nova propriedade (registro → `VaccineCatalogSeeder`) | ✅ 4 vacinas com `SyncId` distintos e diferentes de `Guid.Empty` — o EF omite o `SyncId` no `INSERT` e o `DEFAULT NEWID()` preenche |

- `Vaccine` implementa `ISyncable` diretamente (como `MilkProduction`). Uma classe base `SyncableEntity` e a implementação em `BaseEntity` foram avaliadas e descartadas em 05/Out/2026 — a segunda levaria `SyncId`/`RowVersion` (e o controle de concorrência) a todas as 22 tabelas de uma vez.
- Validação do seeder feita com a conta de teste `teste.offline.vacinas@muuboi.local` (propriedade "Fazenda Teste Offline Vacinas") no banco local.

### 2.2 Fase 2 — Criação idempotente

- `VaccineCreateDto`: `Guid? SyncId` + `IValidatableObject` → `Guid.Empty` gera `400` "O identificador de sincronização não pode ser vazio.".
- `VaccineDto`: `Guid SyncId` (sem `Manufacturer` nem `RecommendedIntervalDays` — item 4 da §1.1).
- `VaccineProfile` (criação): ignora `SyncId`, `RowVersion` e `PropertyId`.
- `IVaccineRepository`/`VaccineRepository`: `GetVaccineBySyncIdAsync` → `FindBySyncIdAsync<Vaccine>`; `CreateVaccineAsync` → `AddSyncableAsync`.
- `VaccineService.CreateVaccineAsync`: checa o `SyncId` **antes** de qualquer outra coisa (mesmo código da Spec #14 §10.2); `vaccine.SyncId = dto.SyncId ?? Guid.NewGuid()`.
- Controller sem mudança (reenvio responde `201` com o mesmo corpo).

| Validação (05/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` | ✅ `201`, `Id` novo (21) |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id`, 1 linha |
| Mesmo `syncId`, payload diferente | ✅ `201`, devolve a existente sem alterar (nome e `requiresBooster` originais) |
| `POST` sem `syncId` (web) | ✅ `201`, `syncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`) |
| 20 envios simultâneos (mesmo `syncId`) | ✅ Todos `201`; 1 linha |

- Como na produção de leite (Spec #14 §10.2), o `catch` da corrida no `AddSyncableAsync` **não foi exercitado**: o log mostra 3 `INSERT`s (um por `syncId`) e nenhuma violação de `UX_Vaccines_SyncId` — os reenvios foram barrados na checagem do service.
- Mesma diferença de formato já registrada na Spec #14 §10.2: o reenvio devolve `createdAt` sem `Z` (lido do banco).
- Registros de teste (Ids 21–23) inativados ao final.

### 2.3 Fase 3 — Edição com last-write-wins

- `VaccineUpdateDto`: `DateTime? UpdatedAt` (sem validação de data futura — Spec #14 §5.3).
- `VaccineProfile` (edição): troca o `MapFrom(_ => DateTime.UtcNow)` de `UpdatedAt` por `Ignore()`; ignora também `SyncId`, `RowVersion` e `PropertyId`.
- `VaccineService.UpdateVaccineAsync`:

```csharp
var existing = await FindVaccineAsync(id);

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, existing))
    return _mapper.Map<VaccineDto>(existing);

_mapper.Map(dto, existing);
existing.UpdatedAt = editedAt;
var updated = await _vaccineRepository.UpdateVaccineAsync(existing);
return _mapper.Map<VaccineDto>(updated);
```

| Validação (05/Out/2026) | Resultado |
|---|---|
| `updatedAt` UTC mais novo | ✅ Aplicado; `updatedAt` = horário do cliente (`00:12:05Z`) |
| `updatedAt` com fuso `-03:00` | ✅ `21:12:29-03:00` gravado como `00:12:29Z`; aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado |
| Edição mais antiga (−1 h) | ✅ `200`, ignorada; devolve a versão do servidor |
| Web sem `updatedAt` | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) | ✅ Aplicado; `updatedAt` limitado a "agora" |
| `PATCH` parcial (só `description`) | ⚠️ `name` preservado, mas **`requiresBooster` volta para `false`** — bug pré-existente (item 7 da §1.1) |
| Após a correção: `PATCH` só `description` / só `name` | ✅ `requiresBooster: true` preservado |
| Após a correção: `requiresBooster` explícito `false` → só `description` → explícito `true` | ✅ `false` aplicado e preservado; `true` aplicado |

Registros de teste (Ids 24 e 25) inativados ao final.

> O mesmo padrão `ForAllMembers(... srcMember != null)` existe em outros 6 profiles (`Animal`, `AnimalMedication`, `Medication`, `SemenSample`, `Stock`, `WeightRecord`). Campos anuláveis no DTO e não anuláveis na entidade podem ter o mesmo bug — **não verificado**; tratar ao tornar cada entidade sincronizável.

### 2.4 Fase 4 — Inativação idempotente

```csharp
var vaccine = await FindVaccineAsync(id);
if (!vaccine.IsActive)
    return true;

await _vaccineRepository.DeleteVaccineAsync(id);
return true;
```

O repositório não muda. Para a web, o comportamento externo é o mesmo (`204`); a diferença é não gravar de novo.

| Validação (05/Out/2026) | Resultado |
|---|---|
| `DELETE` em vacina ativa | ✅ `204`; `IsActive=0`, `RowVersion` 38073 → 38074 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` e `RowVersion` (38074) **inalterados** |
| `DELETE` em id inexistente | ✅ `404` "Vacina com id '999999' não encontrada." |

Registro de teste (Id 26) já termina inativo.

### 2.5 Fase 5 — Pull incremental

- `IVaccineRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<Vaccine>`.
- `IVaccineService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<VaccineDto>` (mesmo código da Spec #14 §10.5).
- `VaccinesController`: `[HttpGet("changes")]` (sem colisão com `{id:int}`, que tem restrição de tipo).

| Validação (05/Out/2026) | Resultado |
|---|---|
| Pull inicial sem `since` | ✅ 10 vacinas: as 4 do seeder (ativas) e as 6 de teste (inativas), em ordem de `RowVersion` |
| Isolamento por propriedade | ✅ A tabela tinha 22 vacinas; vieram só as 10 da propriedade do usuário |
| `since` = último cursor, sem mudanças | ✅ `items: []`, `nextCursor` mantido (`38074`) |
| Cria A, edita A, cria B, inativa B → pull | ✅ Só A (ativa, nome final "Pull A editada") e B (inativa), cada uma **uma vez** |
| Paginação `limit=1` | ✅ 12 páginas, cursores crescentes (com "buracos" do contador compartilhado), sem repetir nem pular; última com `hasMore: false` |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |

Registros de teste (Ids 27 e 28) inativados ao final.

### 2.6 Fase 6 — Testes

`MuuBoi.Tests/Services/VaccineServiceTests.cs` — padrão de `MilkProductionServiceTests` (repositório mockado, AutoMapper real, sem testar mapeamento).

| # | Teste |
|---|---|
| 1 | `CreateVaccineAsync_WithNewSyncId_CreatesVaccineWithGivenSyncId` |
| 2 | `CreateVaccineAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateVaccineAsync_WithoutSyncId_GeneratesSyncId` |
| 4 | `UpdateVaccineAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 5 | `UpdateVaccineAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 6 | `UpdateVaccineAsync_WithSameClientUpdatedAt_AppliesChanges` |
| 7 | `UpdateVaccineAsync_WithoutClientUpdatedAt_UsesNow` |
| 8 | `UpdateVaccineAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 9 | `UpdateVaccineAsync_WhenVaccineNotFound_ThrowsNotFoundException` |
| 10 | `DeleteVaccineAsync_WhenActive_DeactivatesAndReturnsTrue` |
| 11 | `DeleteVaccineAsync_WhenAlreadyInactive_ReturnsTrueWithoutDeleting` |
| 12 | `DeleteVaccineAsync_WhenVaccineNotFound_ThrowsNotFoundException` |
| 13 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 14 | `GetChangesAsync_WithoutCursor_RequestsChangesFromZero` |
| 15 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |

Os casos de borda dos helpers (fuso `-03:00`, relógio adiantado, limite acima do máximo, página vazia) já são cobertos pelos testes 7, 8, 18 e 19 da `MilkProduction` (Spec #14 §10.6) e não são repetidos.

- **Resultado (05/Out/2026):** 15/15 passando; projeto inteiro 34/34.
- Na exclusão, quem inativa é o `DeleteVaccineAsync` do repositório (mockado); por isso os testes 10–11 verificam se ele foi chamado, não o `IsActive`.
- A correção do `RequiresBooster` (item 7 da §1.1) **não tem teste unitário**: está no profile do AutoMapper e o `CLAUDE.md` proíbe testar mapeamento nos testes de service. Ficou coberta pela validação manual da §2.3.

### 2.7 Fase 7 — Documentação

- **Este documento:** status das fases e resultados de validação.
- **Spec #14 §8:** `Vaccine` → ✅.
- **`spec-controle-sanitario-vacinacao.md`:** §8 aponta para esta spec quanto ao catálogo (eventos continuam pendentes).
- **`Docs/catalogo-erros-api.md` §4.6:** nota "rota com suporte offline" para `/api/vaccines` e `400` do cursor.

## 3. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/Vaccine.cs` | `ISyncable` (`SyncId`, `RowVersion`) |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_Vaccine_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/VaccineRepository.cs` | `GetVaccineBySyncIdAsync`, `CreateVaccineAsync` e `GetChangesAsync` via helpers |
| Application | `Application/DTOs/VaccineCreateDto.cs` | `SyncId?` + validação |
| Application | `Application/DTOs/VaccineUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/VaccineDto.cs` | `SyncId` |
| Application | `Application/Mappings/VaccineProfile.cs` | Ignora `SyncId`, `RowVersion`, `PropertyId`; `UpdatedAt` ignorado na edição |
| Application | `Application/Interfaces/IVaccineRepository.cs`, `IVaccineService.cs` | Novos métodos |
| Application | `Application/Services/VaccineService.cs` | Criação idempotente, LWW, inativação idempotente, pull |
| Api | `Api/Controllers/VaccinesController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/Services/VaccineServiceTests.cs` | **Novo** |

**Não muda:** `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `VaccineCatalogSeeder`, `VaccinationEvent*`.
