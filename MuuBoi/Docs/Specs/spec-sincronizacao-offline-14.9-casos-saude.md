# Spec #14.9: Sincronização Offline — Casos de Saúde, Aplicações e Testes

**Módulo:** Infraestrutura / Sincronização — Controle Sanitário (Tratamentos)
**Versão:** 1.0
**Data:** 13/Out/2026
**Status:** ✅ **Implementada** (Fases 1–8) na branch `feature/offline-health-cases`. Decisões H1–H10 fechadas (H6, H7 e H8 como propostas).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 12/Out/2026 | Rascunho: caso de saúde, aplicações de medicação e testes de mastite **sem alterar nenhuma regra de negócio**; filhos com `SyncId` próprio; pull com DTO próprio |
| 0.2 | 12/Out/2026 | Aprovada: H6 (filhos embutidos no pull, criar/inativar filho grava o caso), H7 (uso avulso fora) e H8 (validação → `400`, se a Fase 2 confirmar) fechadas como propostas. Q3 mantida em aberto, sem mudança de regra |
| 1.0 | 13/Out/2026 | Implementada: Fases 1–8. §3 item 8 confirmado (H8 aplicada na criação, no `PATCH` e nos filhos). Toque no caso via `ExecuteUpdateAsync` numa transação explícita, em vez de `Entry(...).State = Modified`, para não gerar `500` de concorrência (§6.3). 19 testes novos (244 no total) |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões, helpers (§7.2), receita (§7.4), Condições 1–3 (§7.5), §5.4 (mudanças de estado não alteram `UpdatedAt`), §5.9 (lista embutida avança o `RowVersion` do pai) · Spec #14.4 (animais, sincronizáveis; N6 — pull sem derivados) · Spec #14.8 (V7 — status derivado calculado no app). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-tratamentos-sanitarios.md` (D1–D13) · `Docs/catalogo-erros-api.md` (§4.16)

> **Por que casos de saúde agora:** é o último grupo do controle sanitário e depende só de `Animal`, já sincronizável. Os eventos reprodutivos (cobertura → gestação → parto) ficam para depois: dependem de quase tudo e têm registros derivados em outras entidades (Condições 1 e 2).
>
> **O que o torna diferente das anteriores:** (1) **dois tipos de filho com identidade própria** — aplicações de medicação (`AnimalMedication`) e testes de mastite (`MastitisTest`) —, criados e inativados por rotas próprias e **endereçados pelo app** (`DELETE /{id}/medications/{medicationId}`), então precisam de `SyncId` (diferente do `VaccinationEventAnimal`, que não tem identidade); (2) os filhos **não têm `PATCH`**: só nascem e são inativados; (3) o caso tem **dois derivados de leitura** — status (`HealthCaseStatusResolver`) e data de liberação do leite (maior carência entre as aplicações ativas) — que dependem dos filhos, e o **status sanitário do animal** (lista de animais) é calculado sobre os casos e aplicações; (4) a tabela `AnimalMedications` é **compartilhada** com o uso avulso legado (`HealthCaseId = null`, `/api/animals/{animalId}/medications`), que faz exclusão física (Spec #14 §13 questão 4).

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/health-cases` com suporte offline | Uso avulso de medicação (`/api/animals/{animalId}/medications`) — ver H7 |
| `POST /api/health-cases/{id}/medications` e `/{id}/tests` com suporte offline | Catálogo `Medication` (descontinuado — Spec #14 §8) |
| `PATCH /api/health-cases/{id}` com LWW | Editar uma aplicação ou um teste — não existe rota hoje (R7) |
| `DELETE` do caso, da aplicação e do teste idempotentes | Reabrir um caso encerrado (`ResolvedAt` não pode voltar a `null` — R6) |
| Pull do caso com aplicações e testes (H6) | Histórico por animal e status sanitário no servidor — leitura de tela; o app recalcula sobre o pull |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/health-cases` | Sempre cria | Aceita `syncId`; se já existir → devolve o existente (`201`), sem checar animal nem a regra da mastite |
| `POST /api/health-cases/{id}/medications` | Sempre cria | Aceita `syncId`; reenvio → `201` com a aplicação existente |
| `POST /api/health-cases/{id}/tests` | Sempre cria | Aceita `syncId`; reenvio → `201` com o teste existente |
| `PATCH /api/health-cases/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antiga (LWW) |
| `DELETE /api/health-cases/{id}`, `/{id}/medications/{medicationId}`, `/{id}/tests/{testId}` | Já inativo → `409` | Já inativo → `204`, sem gravar; não altera `UpdatedAt` |
| `GET /api/health-cases/changes?since=&limit=` | — | **Nova**, com `HealthCaseSyncDto` (H6) |
| Respostas de `GET /{id}`, `POST`, `PATCH` e dos filhos | — | Ganham `syncId` (aditivo), no caso e em cada aplicação/teste |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.8): nenhuma regra de negócio é alterada.**

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Animal precisa existir (ativo **ou** inativo — `GetAnimalByIdAsync` não filtra `IsActive`) | `POST` caso | `404` "Animal com id '{id}' não encontrado." |
| R2 | Mastite só para fêmeas (não macho, não touro/boi) | `POST` caso | `422` "Não é possível registrar mastite para machos (touro/boi)." |
| R3 | Validações do DTO: datas não futuras; nome da doença obrigatório quando "Outra"; quartos só na mastite; carência ≥ 0; resultado do teste obrigatório | `POST`, `PATCH`, filhos | `400` (formato B) |
| R4 | Caso precisa existir; aplicação/teste precisa pertencer ao caso | Filhos, `PATCH`, `DELETE` | `404` (mensagens atuais) |
| R5 | `PATCH`: nome da doença só se aplica a "Outra"; quartos só à mastite (ignorados nos demais, sem erro) | `PATCH` | Mantido |
| R6 | `ResolvedAt` (encerramento) é gravado pelo `PATCH`; não há como reabrir (`null` = não altera) | `PATCH` | Mantido |
| R7 | Aplicações e testes **não têm edição**: só criação e inativação | Filhos | Mantido |
| R8 | Caso inativo aceita `PATCH` e novos filhos (não há checagem de `IsActive`) | `PATCH`, filhos | Mantido (Spec #14 §5.4) |
| R9 | Inativar o caso **não** inativa os filhos; as aplicações do caso inativo continuam contando na carência do animal (`GetSanitaryFactsMapAsync` não filtra o caso) | `DELETE` caso | Mantido — ver Q3 |
| R10 | Status do caso, data de liberação e status sanitário do animal são **derivados**, nunca gravados | Leituras | Mantido; fora do pull (H6) |

**Ordem entre `SyncId` e regras:** `SyncId` antes de R1, R2 e R4 (caso). O reenvio de uma criação já gravada responde `201` com o existente, mesmo que depois o animal tenha sido reclassificado ou o caso inativado.

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `HealthCase`, `AnimalMedication` e `MastitisTest` são `BaseEntity, ITenantEntity` com filtro de tenant | Basta `ISyncable` nas três | Receita §7.4, uma migração (Fase 1) |
| 2 | `CreateAsync` (caso), `AddMedicationAsync` e `AddTestAsync` gravam com `Add` + `SaveChanges` | Reenvio **duplica** o caso ou o filho silenciosamente — no caso da aplicação, duplica também a carência | `SyncId` + `AddSyncableAsync` (H2, H3) |
| 3 | Nenhuma regra de unicidade entre a checagem do `SyncId` e o `INSERT` | **Condição 3 não se aplica** (nenhum `409` possível na criação) | — |
| 4 | `UpdateAsync` atribui campo a campo e carimba `UpdatedAt = DateTime.UtcNow` | Precisa de LWW | H4 |
| 5 | `DeactivateAsync`, `DeactivateMedicationAsync` e `DeactivateTestAsync`: "já está inativo" (`409`) e carimbam `UpdatedAt` | Violam A6 e a §5.4 | H5 |
| 6 | O detalhe (`HealthCaseDto`) traz nome/brinco do animal, status, data de liberação e **só os filhos ativos**, sem `syncId` | No pull, os derivados envelhecem com o tempo; filhos inativados somem sem tombstone; e **criar/inativar um filho não muda o `RowVersion` do caso** | H6 |
| 7 | `AnimalMedications` é compartilhada com o uso avulso (`HealthCaseId = null`), servido por `AnimalMedicationsController` com **exclusão física** (`AnimalMedicationRepository.cs:53`) | Banco local: **0** aplicações avulsas (5 do caso); o catálogo `Medication` foi descontinuado | H7 |
| 8 | **Prováveis bugs pré-existentes:** `AnimalId` (`int`, `[Required]`) ausente vira `0`; `DiagnosisDate`, `ApplicationDate` e `TestDate` (`DateTime`, `[Required]`) ausentes viram `0001-01-01` e **passam** na regra "não futura"; `AffectedQuarters` aceita valores fora dos 4 quartos (ex.: `16`) | Caso sem animal → `404` "Animal com id '0'…" (mensagem errada); aplicação com data de 0001 gravada e sincronizada para todos os celulares | **A confirmar na Fase 2.** Se confirmado: H8 |
| 9 | Banco local: 4 casos (1 encerrado), 5 aplicações, 3 testes, nenhum inativo | Backfill de `SyncId` pela migração | — |

## 4. Decisões propostas

As marcadas com ⭐ foram escolhidas pela usuária em 12/Out/2026 (todas como propostas); as demais seguem precedentes das specs 14.1–14.8.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| H1 | **`ISyncable` em `HealthCase`, `AnimalMedication` e `MastitisTest`** + `ConfigureSyncable()`; **uma** migração com `SyncId`/`RowVersion` e índices nas três tabelas | Uma migração por tabela — três migrações para uma mesma feature |
| H2 | **Criação do caso idempotente**: `HealthCaseCreateDto.SyncId`; checado antes de R1/R2; `AddSyncableAsync` | — |
| H3 | **Aplicação e teste idempotentes**: `SyncId` em `MedicationUseCreateDto` e `MastitisTestCreateDto`; checado antes de R4 (caso); `AddSyncableAsync`. O app monta a rota com o `ServerId` do caso (A5) | — |
| H4 | **`PATCH` do caso com LWW**: R4 → LWW → atribuições; `UpdatedAt = editedAt`. Caso inativo continua editável (R8). O encerramento (`resolvedAt`) é uma edição comum e passa pelo LWW | Tratar o encerramento como mudança de estado fora do LWW — exigiria rota própria (regra nova) |
| H5 | **Inativações idempotentes** (caso, aplicação, teste): `204` sem gravar no repetido e **sem alterar `UpdatedAt`** (§5.4). Inativar o caso não toca os filhos (R9) | Manter o `409` — viola A6 |
| H6 ⭐ | **Pull único do caso com os filhos embutidos**, em `GET /api/health-cases/changes`, com `HealthCaseSyncDto`: `id`, `syncId`, `animalId`, `diseaseType`, `diseaseName`, `diagnosisDate`, `affectedQuarters` (lista), `resolvedAt`, `notes`, `isActive`, `createdAt`, `updatedAt`, `medications[]` e `tests[]` — **inclusive os inativos**, cada um com `id`, `syncId`, seus campos, `isActive`, `createdAt`. **Sem** status, data de liberação, nome/brinco do animal. Para o caso reaparecer no pull, **criar ou inativar um filho também grava o caso** (o `RowVersion` do caso avança no mesmo `SaveChanges`, sem mexer no `UpdatedAt` — §5.9). **3 consultas por página**, constante (casos + aplicações + testes). O app substitui as listas locais do caso a cada pull | (a) **Três rotas de pull** (`/changes`, `/medications/changes`, `/tests/changes`), precedente do estoque (14.3): sem "tocar" o caso, mas **3 requisições** a cada sincronização em vez de 1 (memória: servidor fraco e internet ruim). (b) Mesmo `HealthCaseDto` — derivados envelheceriam e filhos inativados sumiriam sem tombstone |
| H7 ⭐ | **Uso avulso fora do offline.** O pull embute só as aplicações **do caso** (`HealthCaseId` preenchido). A rota avulsa (`/api/animals/{animalId}/medications`) não muda — continua com exclusão física, e a §13 questão 4 segue aberta só para ela. O `SyncId` nasce nas avulsas também (é a mesma tabela), mas sem uso | (a) Incluir as avulsas no offline — exigiria converter a exclusão física, um pull próprio e o app voltar a ter tela de uso avulso, para um fluxo sem dados (0 no banco) e cujo catálogo foi descontinuado. (b) Remover a rota avulsa — fora do escopo desta spec |
| H8 ⭐ | **Validação → `400`** (se a Fase 2 confirmar o §3 item 8): `AnimalId` → `int?` com `[Required]`; `DiagnosisDate`, `ApplicationDate` e `TestDate` → `DateTime?` com `[Required]`; `AffectedQuarters` limitado aos 4 quartos (1–15) na criação e no `PATCH`. Correção de bug, não regra nova (mesma linha da V8 da 14.8) | Manter — uma aplicação com data de 0001 sincronizaria para todos os celulares e entraria no cálculo de carência |
| H9 | **Animal inativo ou reclassificado não bloqueia o reenvio** (`SyncId` antes de R1/R2). Criação **nova** continua com R1 e R2 como hoje (animal inativo é aceito, R1) | — |
| H10 | **Derivados no app** (precedente V7 da 14.8): status do caso (`HealthCaseStatusResolver`), data de liberação (maior `applicationDate + withdrawalPeriodDays` entre as aplicações **ativas**), e status sanitário do animal (`AnimalSanitaryStatusResolver`, sobre casos ativos e aplicações ativas — R9) | Mandar os derivados no pull — envelhecem sem o caso reaparecer |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** animal criado offline → caso → aplicações e testes do caso. O corpo do caso usa o `ServerId` do animal; a rota dos filhos usa o `ServerId` do caso (A5). Se o caso falhou, os filhos esperam.
2. **`syncId` próprio** no caso, em cada aplicação e em cada teste. Para inativar um filho criado offline, a rota usa o `ServerId` do filho, resolvido pelo `syncId` depois do envio da criação.
3. **Datas** em UTC, com `Z`.
4. **Pull (`GET /api/health-cases/changes`):** upsert do caso por `syncId`; as listas `medications` e `tests` **substituem** as listas locais do caso (upsert de cada filho por `syncId`, inclusive os inativos). Puxar **animais antes de casos**.
5. **Derivados locais (H10):** status do caso, data de liberação do leite e status sanitário do animal (lista de animais e filtro por status sanitário). As telas que hoje usam esses campos do servidor passam a calculá-los localmente com as mesmas regras.
6. **Sem edição de aplicação/teste** (R7): para corrigir, o usuário inativa e cria de novo.
7. **Sem reabrir caso** (R6).

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (H1) | ✅ 12/Out/2026 — migração `20261012161029_Offline_HealthCase_SyncId_RowVersion` aplicada no banco local; 4 casos, 5 aplicações, 3 testes com `SyncId` distintos; caso, aplicação, teste e aplicação avulsa novos nascem com `SyncId`. §3 item 8 **confirmado** (os cinco casos: `animalId` ausente → `404` "id '0'"; `diagnosisDate`, `applicationDate` e `testDate` ausentes → gravados como `0001-01-01`; `affectedQuarters` `16` no `POST` e `99` no `PATCH` gravados) |
| 2 — Criação do caso idempotente (+ confirmação do §3 item 8 e H8) | ✅ 12/Out/2026 — validações da §6.2 todas como esperado (reenvio com outro payload devolve o original; reenvio de mastite depois de o animal virar boi → `201`, mastite nova no boi → `422`; 20 simultâneos: 20 × `201`, 1 caso). H8 na criação: sem `animalId`, sem `diagnosisDate`, quartos `0` ou `16` → `400` |
| 3 — Aplicações e testes idempotentes (H3, e o "toque" no caso se H6) | ✅ 13/Out/2026 — validações da §6.3 todas como esperado (reenvio com outro payload devolve o original; reenvio depois de inativar o caso → `201`; sem data → `400`; 20 simultâneos: 20 × `201`, 1 aplicação; 10 + 10 de duas aplicações: 20 × `201`, 2 aplicações; criar filho avança o `RowVersion` do caso sem mudar o `UpdatedAt`). Toque no caso via `ExecuteUpdateAsync` (ver §6.3) |
| 4 — Edição com last-write-wins (H4) | ✅ 13/Out/2026 — validações da §6.4 todas como esperado (mais novo, `-03:00`, reenvio, mais antigo, encerramento com `updatedAt` antigo ignorado, sem `updatedAt`, relógio adiantado, caso inativo editável); H8 no `PATCH` (quartos `99` → `400`). O reenvio com o mesmo `updatedAt` grava de novo e avança o `RowVersion` (mesmo comportamento das specs anteriores) |
| 5 — Inativações idempotentes (H5) | ✅ 13/Out/2026 — caso, aplicação e teste: `204` → `204`, nada gravado no repetido, `UpdatedAt` inalterado; inativar aplicação/teste avança o `RowVersion` do caso (toque, H6); inativar o caso não toca os filhos (R9); inexistentes `404` |
| 6 — Pull incremental (H6) | ✅ 13/Out/2026 — validações da §6.6 todas como esperado: pull inicial (12 casos, 3 inativos; 8 aplicações e 4 testes, com os inativos) batendo com o banco; pull × `GET /{id}` sem divergências; isolamento entre duas contas; caso → aplicações → teste → inativar aplicação → o caso uma vez, com as listas finais; só criar **ou** só inativar um teste num caso antigo → o caso reaparece; aplicação avulsa fora (H7); paginação de 4 sem duplicatas; cursor inválido `400`; sem colisão de rotas; **3 consultas por página**. Os DTOs de pull usam datas ISO (contrato da Spec #14); o detalhe continua com `dd/MM/yyyy` (`DateFormatConverter`) |
| 7 — Testes | ✅ 13/Out/2026 — 19 testes da §6.7 acrescentados a `HealthCaseServiceTests`; suíte completa 244/244 |
| 8 — Documentação | ✅ 13/Out/2026 — Spec #14 (v1.9: §5.5, §8, §13 questão 4, sumário), `catalogo-erros-api.md` (§4.16), `spec-tratamentos-sanitarios.md` (nota no topo) |

Branch: `feature/offline-health-cases`, a partir do `dev`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com a conta de teste da 14.8.

### 6.1 Fase 1 — Domínio e banco

- `HealthCase`, `AnimalMedication` e `MastitisTest`: `ISyncable`.
- `ApplicationDbContext`: `ConfigureSyncable()` nas três.
- Migração `Offline_HealthCase_SyncId_RowVersion` (⚠️ **requer aprovação**) — para cada tabela (`HealthCases`, `AnimalMedications`, `MastitisTests`):

```sql
ALTER TABLE [<Tabela>] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [<Tabela>] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_<Tabela>_PropertyId_RowVersion] ON [<Tabela>] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_<Tabela>_SyncId] ON [<Tabela>] ([SyncId]);
```

| Validação planejada | Esperado |
|---|---|
| SQL / snapshot | Só as três tabelas |
| Registros existentes | 4 casos, 5 aplicações, 3 testes com `SyncId` distintos e não vazios |
| Registro novo (caso, aplicação, teste, aplicação avulsa) | `SyncId` ≠ `Guid.Empty` |

### 6.2 Fase 2 — Criação do caso idempotente

- **Antes de mudar o código:** confirmar o §3 item 8 (sem `animalId`; sem `diagnosisDate`; aplicação sem `applicationDate`; teste sem `testDate`; `affectedQuarters: 16`).
- `HealthCaseCreateDto`: `Guid? SyncId` + validação de `Guid.Empty`; H8 se confirmado.
- Repositório: `GetBySyncIdAsync` (com os mesmos `Include` do `GetByIdAsync`); `CreateAsync` → `AddSyncableAsync`.
- `HealthCaseService.CreateAsync`: bloco do `SyncId` antes de R1/R2; `SyncId = dto.SyncId ?? Guid.NewGuid()`.

| Validação planejada | Esperado |
|---|---|
| `POST` com `syncId` / repetido / payload diferente | `201`; mesmo `Id`; 1 caso, com os dados **originais** |
| Sem `syncId` / `syncId` vazio | `201` com `SyncId` gerado / `400` |
| Reenvio de uma mastite depois de o animal virar boi | `201` com o existente (H9) |
| Animal inexistente / mastite em macho | `404` / `422` (mantidos) |
| 20 envios simultâneos (mesmo `syncId`) | Todos `201`; 1 caso |

### 6.3 Fase 3 — Aplicações e testes idempotentes

- `MedicationUseCreateDto` e `MastitisTestCreateDto`: `Guid? SyncId` + validação; H8 nas datas, se confirmado.
- Repositório: `GetMedicationBySyncIdAsync`, `GetTestBySyncIdAsync`; `AddMedicationAsync` e `AddTestAsync` → `AddSyncableAsync` seguido do **toque no caso** (`TouchHealthCaseAsync`: `UPDATE ... SET IsActive = IsActive` via `ExecuteUpdateAsync`), os dois numa transação explícita (Condição 1). O `RowVersion` do caso avança e o `UpdatedAt` não muda.
- **Por que `ExecuteUpdateAsync` e não `Entry(healthCase).State = Modified`** (decidido na implementação, 13/Out/2026): marcar o caso como alterado faz o `UPDATE` conferir o `RowVersion` (concorrência otimista). Dois envios simultâneos — inclusive o reenvio da **mesma** aplicação — terminariam em `DbUpdateConcurrencyException` (`500`). O `UPDATE` direto não confere o `RowVersion`: os envios esperam o lock da linha do caso, em fila.
- Service: bloco do `SyncId` antes de R4 (caso existe) em cada um.

| Validação planejada | Esperado |
|---|---|
| Aplicação com `syncId` / reenvio / payload diferente | `201`; mesmo `Id`; 1 aplicação; carência original |
| Teste com `syncId` / reenvio | `201`; mesmo `Id`; 1 teste |
| Reenvio depois de inativar o caso | `201` com o existente |
| Caso inexistente | `404` (mantido) |
| 20 envios simultâneos da mesma aplicação | Todos `201`; 1 aplicação; nenhum `500` |
| **Se H6:** criar aplicação/teste → `RowVersion` do caso | Avança; `UpdatedAt` do caso inalterado |

### 6.4 Fase 4 — Edição com last-write-wins

- `HealthCaseUpdateDto`: `DateTime? UpdatedAt`; H8 (`AffectedQuarters`).
- `UpdateAsync`: R4 → `ResolveEditedAt`/`IsOutdated` → atribuições → `UpdatedAt = editedAt`.

| Validação planejada | Esperado |
|---|---|
| Mais novo / `-03:00` / reenvio / mais antigo / sem `updatedAt` / relógio adiantado | Como nas specs anteriores |
| Encerrar (`resolvedAt`) com `updatedAt` mais antigo que outra edição | Ignorado (LWW) |
| `PATCH` em caso inativo | `200`, aplicado (R8) |

### 6.5 Fase 5 — Inativações idempotentes

- `DeactivateAsync`, `DeactivateMedicationAsync`, `DeactivateTestAsync`: `if (!x.IsActive) return true;`; sem `UpdatedAt`. **Se H6:** inativar um filho também grava o caso.

| Validação planejada | Esperado |
|---|---|
| Inativar → inativar de novo (caso, aplicação, teste) | `204` → `204`; nada gravado no repetido |
| `UpdatedAt` depois de inativar | Inalterado; `RowVersion` avança (e o do caso, se H6) |
| Inativar o caso | Filhos continuam ativos (R9) |

### 6.6 Fase 6 — Pull incremental

- `HealthCaseSyncDto`, `MedicationUseSyncDto`, `MastitisTestSyncDto` (**novos**) e mapas no profile (enums como `EnumValueDto`; quartos decompostos como no detalhe).
- `IHealthCaseRepository.GetChangesAsync(since, take)` → `GetChangesSinceAsync<HealthCase>`; `GetMedicationsByCaseIdsAsync(ids)` e `GetTestsByCaseIdsAsync(ids)` (uma consulta cada, **inclusive inativos**).
- `IHealthCaseService.GetChangesAsync(since, limit)` → `SyncPageDto<HealthCaseSyncDto>`.
- `HealthCasesController`: `[HttpGet("changes")]`.

| Validação planejada | Esperado |
|---|---|
| Pull inicial | Todos os casos (inclusive inativos) com filhos ativos e inativos |
| Pull × `GET /{id}`, campo a campo (os comuns) | Nenhuma divergência nos campos e nos filhos ativos |
| Isolamento (duas contas) | Cada uma vê só os seus |
| Criar caso → aplicação → teste → inativar aplicação → pull | O caso **uma vez**, com as listas finais |
| Só adicionar um teste a um caso antigo → pull | O caso reaparece (H6) |
| Paginação / cursor / `limit` | Como nas anteriores |
| Rotas `disease-types`, `quarters`, `test-types`, `statuses`, `/{id}` | Sem colisão |
| Consultas por página | **3**, constante |

### 6.7 Fase 7 — Testes

`MuuBoi.Tests/Services/HealthCaseServiceTests.cs` (já existe — acrescentar).

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesCaseWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotCheckAnimalOrMastitisRule` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `AddMedicationAsync_WithNewSyncId_CreatesMedicationWithGivenSyncId` |
| 6 | `AddMedicationAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 7 | `AddMedicationAsync_WithExistingSyncId_DoesNotCheckCase` |
| 8 | `AddTestAsync_WithNewSyncId_CreatesTestWithGivenSyncId` |
| 9 | `AddTestAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 10 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 11 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 12 | `UpdateAsync_WhenInactive_AppliesChanges` (R8) |
| 13 | `DeactivateAsync_WhenActive_DeactivatesWithoutChangingUpdatedAt` |
| 14 | `DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` |
| 15 | `DeactivateMedicationAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` |
| 16 | `DeactivateTestAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` |
| 17 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 18 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 19 | `GetChangesAsync_FillsMedicationsAndTestsIncludingInactive` (H6) |

Nenhum teste atual de `HealthCaseServiceTests` esperava o `409` de "já inativo" — não houve teste a ajustar. O toque no caso e a transação ficam no repositório e foram validados na API local (Fases 3, 5 e 6), já que os testes cobrem só services.

### 6.8 Fase 8 — Documentação

- **Esta spec**; **Spec #14** (§8: `HealthCase`, `AnimalMedication` do caso e `MastitisTest` ✅; §5.5: terceira exceção ao "mesmo DTO do detalhe"; §13 questão 4 restrita ao uso avulso, se H7); **`catalogo-erros-api.md`** (§4.16: `409` de estado repetido removidos, `400` novos, nota de rotas offline); **`spec-tratamentos-sanitarios.md`** (nota no topo).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/HealthCase.cs`, `AnimalMedication.cs`, `MastitisTest.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` nas três |
| Infrastructure | `Infrastructure/Migrations/*_Offline_HealthCase_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/HealthCaseRepository.cs` | `GetBySyncIdAsync` (caso, aplicação, teste), criações via `AddSyncableAsync`, "toque" no caso (H6), `GetChangesAsync`, filhos por ids de caso |
| Application | `Application/DTOs/HealthCaseCreateDto.cs`, `MedicationUseCreateDto.cs`, `MastitisTestCreateDto.cs` | `SyncId?`; H8 |
| Application | `Application/DTOs/HealthCaseUpdateDto.cs` | `UpdatedAt?`; H8 |
| Application | `Application/DTOs/HealthCaseDto.cs`, `MedicationUseDto.cs`, `MastitisTestDto.cs` | `SyncId` |
| Application | `Application/DTOs/HealthCaseSyncDto.cs` (+ filhos) | **Novos** (H6) |
| Application | `Application/Mappings/HealthCaseProfile.cs` (ou o profile atual) | Mapas dos DTOs de pull |
| Application | `Application/Interfaces/IHealthCaseRepository.cs`, `IHealthCaseService.cs` | Novos métodos |
| Application | `Application/Services/HealthCaseService.cs` | Criações idempotentes, LWW, inativações idempotentes, pull |
| Api | `Api/Controllers/HealthCasesController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/Services/HealthCaseServiceTests.cs` | Novos testes; ajuste dos `409` |

**Não muda:** regras R1–R10, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização (inclusive `AddSyncableAsync`), resolvers de status, rota avulsa e `AnimalMedicationRepository` (H7), histórico por animal, status sanitário calculado no servidor para a lista de animais.

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Dois celulares adicionam aplicações ao mesmo caso ao mesmo tempo → o "toque" no caso poderia esbarrar no `RowVersion` (concorrência otimista) → `500` | **Resolvido na Fase 3:** o toque é um `UPDATE` direto (`ExecuteUpdateAsync`), sem checagem de `RowVersion`; validado com 20 envios simultâneos da mesma aplicação e 10 + 10 de duas aplicações no mesmo caso — todos `201`, sem deadlock |
| Esquecer de "tocar" o caso numa operação nova sobre filhos | Centralizar o toque no repositório (criação e inativação dos filhos) e cobrir no teste de pull da Fase 6 |
| Status sanitário calculado no app divergir do servidor | Regras documentadas (H10) e a mesma fonte de dados; o servidor continua calculando para as telas online |
| Aplicação de caso inativo segurando o leite (R9) pode surpreender | Comportamento atual mantido; registrado em Q3 |
| Reenvio com payload diferente (bug do app) | Devolve o original sem alterar (padrão de todas as criações) |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Uso avulso de medicação** (rota legada com exclusão física) | ⏳ Fora (H7); remover ou converter numa tarefa própria |
| Q2 | **Editar aplicação/teste** e **reabrir caso** | ⏳ Fora — regras novas (R6, R7) |
| Q3 | **Aplicações de caso inativo contam na carência** (R9) — intencional (segurança do leite) ou bug? | ⏳ Mantido como está; investigar numa tarefa própria (12/Out/2026) |
| Q4 | Mensagem do `404` quando `animalId` vem ausente ("Animal com id '0'…") | Resolvida pela H8, se aprovada |
