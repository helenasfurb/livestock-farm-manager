# Spec #14.5: Sincronização Offline — Pesagens

**Módulo:** Infraestrutura / Sincronização — Pesagens
**Versão:** 1.0
**Data:** 10/Out/2026
**Status:** ✅ **Implementada e validada** (10/Out/2026) — decisões P1–P8, Fases 1–8. Branch `feature/offline-weight-records`. Nenhuma regra de negócio alterada (R1–R7); exclusão física convertida em lógica (Spec #14 §13, questão 4), um bug pré-existente corrigido (§3 item 5), um `500` convertido em `400` (P6) e o formato de `recordedAt` alterado para ISO (P8).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 10/Out/2026 | Rascunho: pesagem planejada **sem alterar nenhuma regra de negócio**, com a conversão para exclusão lógica (Spec #14 §13, questão 4) e a pesagem inicial endereçável (N2 da 14.4) |
| 0.2 | 10/Out/2026 | Decisões P1–P7 aprovadas conforme recomendado (P1, P2 e P6 escolhidas pela usuária). Branch criada a partir de `feature/offline-animals` |
| 0.3 | 10/Out/2026 | Fases 1–6 implementadas e validadas. §3 item 5 (bug do `PATCH` parcial) confirmado e corrigido. **P8** nova (encontrada na Fase 6), aprovada pela usuária: `recordedAt` em ISO completo |
| 1.0 | 10/Out/2026 | Fases 7 e 8: 18 testes novos (160 no projeto); Spec #14 v1.4, Spec #14.4, `catalogo-erros-api.md` §4.3/§4.4 e `spec-animais.md` atualizados |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2), receita (§7.4) e Condições 1–3 (§7.5) · Spec #14.4 (`Animal` sincronizável; N2 reservou o `initialWeightSyncId` para esta spec). Esta spec **não** redecide nada da #14; aplica a receita ao `WeightRecord`.
**Relaciona-se com:** Spec #14.3 (E1 — derivado do cadastro endereçável pelo app; E6 — pull global de registros filhos) · `spec-animais.md` (peso inicial, último peso e histórico) · `spec-gestacao-parto-6.3-cria.md` (peso de nascimento da cria) · `Docs/catalogo-erros-api.md` §4.4

> **Por que pesagem agora:** é a entidade restante de menor complexidade (Spec #14 §8). Tem **uma FK** (`AnimalId`, já sincronizável), **não tem regra de estado nem de unicidade** (Condição 3 não se aplica) e **cada operação grava um registro** (Condição 1 não se aplica). Já é `ITenantEntity` com `HasQueryFilter` — ao contrário do ECC, não precisa de backfill de `PropertyId`.
>
> **O que a torna diferente das anteriores:** (1) é a **primeira entidade filha de um registro sincronizável criado offline** — a rota leva o `animalId` do servidor, resolvido pelo app no envio (A5, Spec #14 §5.8); (2) hoje faz **exclusão física** (Spec #14 §13, questão 4), que precisa virar exclusão lógica antes de qualquer coisa — sem tombstone, a exclusão nunca chega aos outros celulares pelo pull.
>
> **Sem cliente web:** o único cliente é o app (Spec #14 v1.2). Requisições sem `syncId`/`updatedAt` vêm só do Swagger/chamada direta; o fallback do servidor continua, mas sem compromisso de compatibilidade além dele.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST`, `PATCH`, `DELETE /api/animals/{animalId}/weight-records[/{weightRecordId}]` com suporte offline | ECC (`BodyConditionRecord`) — spec própria (Q4 da 14.4: `PropertyId` com backfill) |
| Exclusão lógica da pesagem (questão 4 da Spec #14 §13) e o filtro de inativas nas leituras | Pesagem da cria **criada pelo parto** offline — spec do parto (Condição 2) |
| `GET /api/animals/weight-records/changes` (**nova**) | Regras novas de peso (valor positivo, data não futura) — Q1 |
| `initialWeightSyncId` no `POST /api/animals` (N2 da 14.4) | Status derivados do animal (N6 da 14.4) — o app passa a calcular último peso e histórico localmente |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/animals/{animalId}/weight-records` | Sempre cria; sem `weight` → **`500`** | Aceita `syncId`; se já existir → devolve a existente (`201`). Sem `weight` → `400` (P6) |
| `PATCH /api/animals/{animalId}/weight-records/{weightRecordId}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antiga (LWW). Aceito em pesagem inativa (Spec #14 §5.4) |
| `DELETE /api/animals/{animalId}/weight-records/{weightRecordId}` | `204` com **exclusão física**; repetido → `404` | `204` com **exclusão lógica**; repetido → `204` **sem gravar** |
| `GET /api/animals/{animalId}/weight-records` | Todas (não havia inativas) | Só as **ativas** (resultado externo igual ao de hoje) |
| `GET /api/animals/{animalId}/weight-records/{weightRecordId}` | Excluída → `404` (linha não existe) | Inativa → `200` com `isActive: false` (P2) |
| `GET /api/animals/weight-records/changes?since=&limit=` | — | **Nova** (P4) |
| `POST /api/animals` | Pesagem inicial recebe `SyncId` do banco | Aceita `initialWeightSyncId` (P3); resposta inalterada — `weightRecords` já traz `id` e (agora) `syncId` |
| `GET /api/animals`, `GET /api/animals/{id}` | Último peso e histórico incluem todas as pesagens | Só as **ativas** (P1) |
| Respostas com `WeightRecordDto` | `recordedAt` em `dd/MM/yyyy` | Ganham `syncId`, `animalId`, `isActive`, `createdAt`, `updatedAt` (aditivo, P5); `recordedAt` em ISO completo (P8) |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.4): nenhuma regra de negócio é alterada.** As únicas mudanças de comportamento são as exigidas pela sincronização (estado repetido → `2xx`, A6) e a correção de um `500` (P6), que não é regra.

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Animal precisa existir (ativo **ou** inativo — `GetAnimalByIdAsync` não filtra `IsActive`) | Todas as rotas | `404` "Animal com id '{animalId}' não encontrado." Pesar animal que saiu do rebanho continua permitido |
| R2 | Pesagem precisa existir **e pertencer ao animal da rota** (`GetWeightRecordByIdAsync(id, animalId)`) | `GET /{id}`, `PATCH`, `DELETE` | `404` "Pesagem com id '{weightRecordId}' não encontrada." |
| R3 | Data ausente no `POST` → **momento do processamento** (`WeightDate ?? DateTime.UtcNow`) | `POST` | Mantido. Ver §5 item 2 |
| R4 | Observações com no máximo 500 caracteres | `POST`, `PATCH` (DTO) | `400` (formato B) |
| R5 | **Não há** validação de peso positivo nem de data futura | `POST`, `PATCH` | Continua sem checagem (Q1) |
| R6 | Histórico em ordem crescente de `RecordedAt`; último peso = maior `RecordedAt` | `GET /`, `AnimalDto.LastWeightRecord`, `AnimalListItemDto.LastWeightRecord` | Mantido, considerando só as ativas |
| R7 | Peso de nascimento da cria: o parto cria a pesagem da cria (`BuildCalfAnimal`) e a edição da cria atualiza a pesagem com `RecordedAt == calvingDate` (`ApplyBirthWeight`) | `AnimalCalvingService` | Mantido, considerando só as ativas (§3 item 7) |

**Ordem entre `SyncId` e regras:** como nas specs anteriores, o `SyncId` é checado **antes** de R1. O reenvio de uma pesagem já gravada responde `201` com a existente. O `syncId` **não** é conferido contra o `animalId` da rota — divergência só ocorre por bug do app (mesma escolha das movimentações da 14.3, Fase 6).

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `WeightRecordRepository.DeleteWeightRecordAsync` faz `_context.WeightRecords.Remove` (exclusão física) | **Questão 4 da Spec #14 §13.** Sem tombstone, a exclusão não chega aos outros celulares pelo pull — a pesagem continuaria aparecendo neles para sempre | Exclusão lógica: `IsActive = false`, `UpdatedAt = agora` (P1) |
| 2 | `DELETE` repetido → `404` (a linha não existe mais) | Viola a A6: o reenvio de uma exclusão que deu certo vira `Failed` e trava a fila | Com a P1, a pesagem continua existindo; o service retorna antes de gravar se já estiver inativa (Fase 2) |
| 3 | As leituras **não filtram `IsActive`** (não era preciso): `WeightRecordRepository.GetAllWeightRecordsAsync`; `AnimalRepository` linhas 23 (lista — último peso), 110 (`GetAnimalByIdAsync`) e 119 (`GetBySyncIdAsync`); `AnimalCalvingRepository` linha 101 (`GetCalfByIdAsync`) | Com a P1, pesagens excluídas apareceriam no histórico e poderiam virar o "último peso" | `Where(w => w.IsActive)` na consulta e nos `Include` filtrados (P1) |
| 4 | `WeightRecordProfile` (edição): `UpdatedAt` mapeado de `DateTime.UtcNow` e `ForAllMembers(srcMember != null)` | Com `UpdatedAt` no DTO, o mapeamento precisa ser controlado (LWW no service) | Profile ignora `UpdatedAt`, `SyncId`, `RowVersion`, `AnimalId` e `PropertyId` (Fase 5) |
| 5 | **Provável bug pré-existente** no mesmo profile: `Weight` (`decimal?` → `decimal`) e `WeightDate` → `RecordedAt` (`DateTime?` → `DateTime`) — o mesmo par anulável → não anulável do bug do `RequiresBooster` (14.1 §1.1 item 7) e do estoque (14.3 §3 item 4) | Um `PATCH` só das observações gravaria `Weight = 0` e/ou `RecordedAt = 0001-01-01` — corromperia o histórico e o último peso | **Confirmado na Fase 4** (§6.4): os dois campos foram zerados. ✅ Corrigido na Fase 5: `PreCondition(src => src.X.HasValue)` nos dois campos, como na 14.1 |
| 6 | `CreateWeightRecordAsync` usa `weightRecordCreateDto.Weight!.Value` sem validação de obrigatório | `POST` sem `weight` → `InvalidOperationException` → **`500`**. No app, `500` é tentado de novo até o limite e trava a fila sem mensagem útil | `[Required]` no `Weight` do DTO → `400` (P6) |
| 7 | `ApplyBirthWeight` procura a pesagem de nascimento por `RecordedAt == calvingDate` entre as pesagens carregadas pelo `GetCalfByIdAsync` | Com a P1, poderia achar e editar uma pesagem **inativa** — a edição "sumiria" | Coberto pelo filtro do item 3 no `GetCalfByIdAsync`: sem pesagem ativa, cria uma nova (comportamento de hoje quando não há pesagem) |
| 8 | A pesagem inicial do cadastro do animal recebe `SyncId` do banco | **Condição 2:** o app não consegue casar a pesagem do pull com a local, nem endereçar uma correção dela feita antes do primeiro sync | `initialWeightSyncId` no `AnimalCreateDto` (P3) |
| 9 | Pesagens criadas pelo parto (`BuildCalfAnimal`) e pela edição da cria (`ApplyBirthWeight`) | Recebem `SyncId` do banco (`NEWID()`) e aparecem no pull como qualquer pesagem | Nenhum agora. A criação offline é da spec do parto |
| 10 | `WeightRecordDto` só tem `Id`, `Weight`, `RecordedAt`, `Observations` | No pull global faltam o animal, a identidade e o estado | Campos aditivos (P5) |
| 11 | `AnimalDto.WeightRecords` já é a lista de `WeightRecordDto` do animal, incluída na resposta do `POST /api/animals` | — | Com a P5, a resposta do cadastro já devolve `id` + `syncId` da pesagem inicial — **não** é preciso um DTO de referência como o `initialMovement` da E1 (P3) |
| 12 | `AccountRepository` remove as pesagens fisicamente (`ExecuteDeleteAsync`) | Só na exclusão da conta (Spec #14 §6 item 10) | Sem mudança |
| 13 | `WeightRecordCreateDto`/`UpdateDto` não têm `syncId`/`updatedAt`; não há `GetBySyncId` nem `GetChanges` | — | Receita §7.4 |
| 14 | **Encontrado na Fase 6:** `WeightRecordDto.RecordedAt` usa `[JsonConverter(typeof(DateFormatConverter))]` → `"10/10/2026"` (sem hora, fora de ISO). As demais entidades sincronizáveis devolvem a data completa | No pull, o app sobrescreveria a hora local da pesagem com `00:00`; o "último peso" (R6) ficaria ambíguo com duas pesagens no mesmo dia | Converter removido (P8) |

## 4. Decisões (aprovadas em 10/Out/2026)

As marcadas com ⭐ pediam escolha da usuária (aprovadas conforme a recomendação); as demais seguem precedentes das specs 14.1–14.4.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| P1 ⭐ | **Exclusão lógica**: `DeleteWeightRecordAsync` passa a `IsActive = false` + `UpdatedAt = DateTime.UtcNow` + `SaveChanges` (mesmo padrão do `VaccineRepository`). **Todas as leituras de tela filtram as ativas** (§3 item 3), inclusive o último peso da lista e do detalhe do animal e a pesagem de nascimento da cria. Pesagens excluídas fisicamente antes da migração não existem mais e não há o que recuperar (não há app em uso com elas) | Tabela de tombstones (`DeletedRecords`) mantendo a exclusão física — contrato e mecanismo novos só para esta entidade, contra a D6 |
| P2 ⭐ | **`GET /{id}`, `PATCH` e `DELETE` encontram a pesagem ativa ou inativa** (o `GetWeightRecordByIdAsync` continua sem filtro de `IsActive`; o DTO informa `isActive`). Necessário para a Spec #14 §5.4: o `PATCH` de um celular numa pesagem que outro celular excluiu deve ser `200` (edita sem reativar), não `404` — `404` é definitivo e travaria a fila | `GET /{id}` de inativa → `404` (como hoje, por acidente da exclusão física) — exigiria dois métodos no repositório só para manter um `404` que o app não usa (ele lê do Room) |
| P3 | **Pesagem inicial endereçável** (N2 da 14.4, E1 da 14.3): `AnimalCreateDto.InitialWeightSyncId` (`Guid?`; ausente → servidor gera; sem `initialWeight` → ignorado, sem erro; `Guid.Empty` → `400`). `CreateWeightRecord` usa `dto.InitialWeightSyncId ?? Guid.NewGuid()`. **A resposta não muda de formato:** o app localiza a pesagem inicial em `weightRecords` pelo `syncId` (§3 item 11). O reenvio do cadastro devolve o animal pelo `GetBySyncIdAsync`, que já inclui as pesagens | `initialWeight { id, syncId }` na resposta (como o `initialMovement` da E1) — redundante, a lista já vem no `AnimalDto` |
| P4 | **Pull global**: `GET /api/animals/weight-records/changes` traz as pesagens de **todos** os animais da propriedade, inclusive as inativas (E6 da 14.3). Rota declarada no `WeightRecordsController` com caminho absoluto (`[HttpGet("~/api/animals/weight-records/changes")]`); não colide com o `AnimalsController` (`{id:int}` e `changes`) | Pull por animal (um request por animal — ruim em link instável). Prefixo novo `/api/weight-records/changes` (único recurso fora de `/api/animals`) |
| P5 | **`WeightRecordDto` ganha `SyncId`, `AnimalId`, `IsActive`, `CreatedAt`, `UpdatedAt`** — mesmo DTO no detalhe, na lista, dentro do `AnimalDto` e no pull (regra da Spec #14 §5.5, **sem exceção**). Não há derivados: o pull é 1 consulta por página | DTO próprio de sync — sem motivo (não há campos compostos) |
| P6 ⭐ | **`Weight` obrigatório no `POST` → `400`** ("O peso é obrigatório.") em vez do `500` atual (§3 item 6). Correção de bug, não regra nova: hoje a requisição já falha, só que com o status errado. No `PATCH` continua opcional | Manter o `500` — o app tentaria de novo até o limite de tentativas sem mensagem útil (Spec #14 §5.10) |
| P8 ⭐ | **`recordedAt` em ISO completo** (decidida na Fase 6, §3 item 14): o `WeightRecordDto` deixa de usar o `DateFormatConverter` (`dd/MM/yyyy`). Vale para todas as rotas de pesagem e para `weightRecords`/`lastWeightRecord` do `AnimalDto` e do `AnimalListItemDto`. Sem quebra: o único cliente é o app, ainda não escrito | DTO próprio de sync com a data completa e o detalhe em `dd/MM/yyyy` — segunda exceção ao "mesmo DTO do detalhe" e dois formatos para o mesmo campo |
| P7 | **Inativação sem LWW e idempotente** (Spec #14 §5.4): `DELETE` repetido → `204` sem gravar, checado depois de R1/R2 | Inativação com `updatedAt` no corpo — regra diferente da Spec #14 §5.4 |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem e `animalId`:** a pesagem de um animal criado offline é enviada **depois** do `POST` do animal; o `animalId` da rota é o `ServerId` do animal, resolvido **na hora do envio** (A2, A5, Spec #14 §5.8). Se o `POST` do animal falhar (ex.: `409` de brinco, N3 da 14.4), as pesagens dele esperam na fila.
2. **Data da pesagem:** o app **deve** enviar `weightDate` com o momento da pesagem (UTC, com `Z`). Se omitida, o servidor usa o momento do processamento (R3), que no offline pode ser dias depois e mudaria o "último peso".
3. **Pesagem inicial (P3):** um único item na fila — `POST /api/animals` com `initialWeight`, `initialWeightDate` e `initialWeightSyncId`. No `201`, o app grava o `ServerId` da pesagem buscando em `weightRecords` o item com o mesmo `syncId`. Correção ou exclusão dela feita antes do sync vai para a fila **depois** do `POST` do animal e usa esse `Id` na rota.
4. **Pull (`GET /api/animals/weight-records/changes`):** upsert por `syncId`; a pesagem é ligada ao animal local pelo `animalId` (= `ServerId` do animal). No ciclo de pull, puxar **animais antes de pesagens**, para o animal já existir no Room.
5. **Último peso e histórico** exibidos são calculados sobre as pesagens **ativas** locais (maior `recordedAt`; ordem crescente no histórico) — R6. Substitui o item 3 da §5 da 14.4 para a pesagem: criar, editar e excluir pesagens passa a funcionar offline.
6. **Pesagens criadas pelo servidor** (parto e edição da cria — §3 item 9) chegam pelo pull com `syncId` gerado pelo servidor; podem ser editadas e excluídas offline como qualquer outra.
7. **`PATCH` em pesagem excluída por outro celular** → `200` com `isActive: false` (P2); o app sobrescreve o local com a resposta (a edição fica gravada, mas a pesagem continua excluída — Spec #14 §5.4).
8. **`404`** de R1/R2 é erro definitivo (A3) — só ocorre com `animalId`/`weightRecordId` errados (bug do app) ou conta excluída.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco | ✅ Migração `20261010154755_Offline_WeightRecord_SyncId_RowVersion` aplicada |
| 2 — Exclusão lógica, filtros de leitura e inativação idempotente (P1, P2, P7) | ✅ Implementada e validada |
| 3 — Criação idempotente (+ P6) | ✅ Implementada e validada |
| 4 — Pesagem inicial endereçável (P3) | ✅ Implementada e validada (revelou o bug do §3 item 5 — **confirmado**) |
| 5 — Edição com last-write-wins (+ §3 item 5) | ✅ Implementada e validada (bug do §3 item 5 corrigido) |
| 6 — Pull incremental (P4, P5, P8) | ✅ Implementada e validada |
| 7 — Testes | ✅ 16 testes de `WeightRecordService` + 2 em `AnimalServiceTests`; 160 no projeto |
| 8 — Documentação | ✅ Spec #14 (v1.4), Spec #14.4, `catalogo-erros-api.md` §4.3/§4.4, `spec-animais.md` |

Branch: `feature/offline-weight-records`, criada a partir de `feature/offline-animals` (ou do `dev`, depois do merge dos animais), para que a migração fique em cima da `Offline_Animal_SyncId_RowVersion`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, como nas specs anteriores, com uma conta de teste própria.

### 6.1 Fase 1 — Domínio e banco

- `WeightRecord : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<WeightRecord>().ConfigureSyncable();` (o índice `IX_WeightRecords_PropertyId` existente permanece).
- Migração `Offline_WeightRecord_SyncId_RowVersion` (⚠️ **requer aprovação**). SQL esperado:

```sql
ALTER TABLE [WeightRecords] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [WeightRecords] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_WeightRecords_PropertyId_RowVersion] ON [WeightRecords] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_WeightRecords_SyncId] ON [WeightRecords] ([SyncId]);
```

| Validação (10/Out/2026) | Resultado |
|---|---|
| SQL gerado | ✅ Idêntico ao esperado; o snapshot só muda em `WeightRecord` (`RowVersion`, `SyncId` e os dois índices) |
| Pesagens existentes após a migração | ✅ 25 pesagens, 25 `SyncId` distintos, nenhum vazio; `RowVersion` preenchido |
| Outras tabelas | ✅ Nenhuma alteração (a migração só toca `WeightRecords`) |
| Índices | ✅ `PK_WeightRecords`, `IX_WeightRecords_AnimalId`, `IX_WeightRecords_PropertyId`, `IX_WeightRecords_PropertyId_RowVersion`, `UX_WeightRecords_SyncId` |
| Cadastro de animal com peso inicial (sem `syncId` de pesagem) | ✅ Animal 111, pesagem 26 com `SyncId` `3F73D752-…` (≠ `Guid.Empty`) — o EF omite o `SyncId` no `INSERT` e o `DEFAULT NEWID()` preenche |
| Testes existentes | ✅ 142/142 |

- Validação feita com a conta de teste `teste.offline.pesagem@muuboi.local` (propriedade "Fazenda Teste Offline Pesagem"); o animal 111 (brinco `514001`) fica ativo para as próximas fases.
- O parto com peso da cria foi exercitado na Fase 2 (pesagem 28, `SyncId` preenchido pelo banco).

### 6.2 Fase 2 — Exclusão lógica, filtros de leitura e inativação idempotente

- `WeightRecordRepository.DeleteWeightRecordAsync`: troca o `Remove` por `IsActive = false` + `UpdatedAt = DateTime.UtcNow` (P1).
- `WeightRecordRepository.GetAllWeightRecordsAsync`: `Where(w => w.AnimalId == animalId && w.IsActive)`.
- `WeightRecordRepository.GetWeightRecordByIdAsync`: **sem mudança** (encontra ativa ou inativa — P2).
- `AnimalRepository` (linhas 23, 110, 119) e `AnimalCalvingRepository.GetCalfByIdAsync` (linha 101): `Include` filtrado — `a.WeightRecords!.Where(w => w.IsActive).OrderByDescending(...)`.
- `WeightRecordService.DeleteWeightRecordAsync` (P7):

```csharp
await FindAnimalAsync(animalId);
var record = await FindWeightRecordAsync(id, animalId);
if (!record.IsActive)
    return true;

await _weightRecordRepository.DeleteWeightRecordAsync(id, animalId);
return true;
```

| Validação (10/Out/2026) | Resultado |
|---|---|
| `DELETE` na pesagem 27 (400 kg, a mais recente do animal 111) | ✅ `204`; linha **continua** na tabela com `IsActive=0`; `RowVersion` 38496 → 38497 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` (`15:52:49.218`) e `RowVersion` (38497) **inalterados** |
| `GET /` do animal | ✅ Só a pesagem 26 |
| `GET /{id}` da inativa | ✅ `200` com a pesagem (o `isActive` entra no DTO na Fase 6 — P5) |
| `GET /api/animals/111` e `GET /api/animals` | ✅ Último peso volta a ser a pesagem 26 (350,5 kg; antes da exclusão era a 27); histórico só com a 26 |
| Parto (vaca 112, gestação retroativa 17, parto 11) com cria de 35 kg → excluir a pesagem de nascimento (28) → `PATCH` da cria com 38 kg | ✅ Pesagem 28 continua inativa (35 kg) e é criada a 29 (38 kg, ativa, `RecordedAt` = data do parto) — §3 item 7 |
| Novo `PATCH` da cria com 39 kg | ✅ Edita a 29 (ativa); nenhuma pesagem nova; detalhe da cria mostra só a 29 |
| Pesagem do parto | ✅ A 28 nasceu com `SyncId` preenchido pelo banco (complementa a §6.1) |
| Pesagem inexistente / animal inexistente / pesagem do animal 111 pela rota da vaca 112 | ✅ `404` "Pesagem com id '999999' não encontrada." / "Animal com id '999999' não encontrado." / "Pesagem com id '26' não encontrada." (R1/R2) |
| Testes existentes | ✅ 142/142 |

- Registros de teste: animal 111 com a pesagem 26 ativa e a 27 inativa; vaca 112, gestação 17, parto 11 e cria 113 (pesagens 28 inativa e 29 ativa) — usados nas próximas fases.

### 6.3 Fase 3 — Criação idempotente

- `WeightRecordCreateDto`: `Guid? SyncId` + `IValidatableObject` (`Guid.Empty` → `400` "O identificador de sincronização não pode ser vazio."); `[Required(ErrorMessage = "O peso é obrigatório.")]` no `Weight` (P6).
- `WeightRecordProfile` (criação): ignora `SyncId`, `RowVersion` e `PropertyId` (o mapa não é usado pelo service hoje, mas fica coerente).
- `IWeightRecordRepository`/`WeightRecordRepository`: `GetWeightRecordBySyncIdAsync` → `FindBySyncIdAsync<WeightRecord>`; `CreateWeightRecordAsync` → `AddSyncableAsync`.
- `WeightRecordService.CreateWeightRecordAsync`: só o bloco do `SyncId` é novo, **antes** de R1:

```csharp
if (weightRecordCreateDto.SyncId.HasValue)
{
    var existing = await _weightRecordRepository.GetWeightRecordBySyncIdAsync(weightRecordCreateDto.SyncId.Value);
    if (existing != null)
        return _mapper.Map<WeightRecordDto>(existing);
}

var animal = await FindAnimalAsync(animalId);

var weightRecord = new WeightRecord
{
    SyncId = weightRecordCreateDto.SyncId ?? Guid.NewGuid(),
    AnimalId = animal.Id,
    Weight = weightRecordCreateDto.Weight!.Value,
    RecordedAt = weightRecordCreateDto.WeightDate ?? DateTime.UtcNow,
    Observations = weightRecordCreateDto.WeightObservations
};
```

| Validação (10/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId` (360 kg, animal 111) | ✅ `201`, `Id` 30; `SyncId` do app gravado (conferido no banco — o `syncId` entra na resposta na Fase 6) |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id`, 1 linha |
| Mesmo `syncId`, payload diferente (999 kg, com observação) | ✅ `201`, devolve a 30 sem alterar (360 kg, sem observação) |
| `POST` sem `syncId` (Swagger/chamada direta) | ✅ `201`, `Id` 31, `SyncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`) |
| `POST` sem `weight` | ✅ `400` "O peso é obrigatório." (formato B, campo `Weight`) — antes, `500` (P6) |
| `POST` em animal inativo (animal 114, saída por venda) | ✅ `201`, `Id` 32 (R1 mantida) |
| Reenvio da 30 por uma rota com animal inexistente (`/animals/999999`) | ✅ `201` com a 30 — `SyncId` checado antes de R1 |
| `POST` novo (outro `syncId`) com animal inexistente | ✅ `404` "Animal com id '999999' não encontrado." |
| 20 envios simultâneos (mesmo `syncId`) | ✅ Todos `201`; 1 linha (`Id` 33) |
| Testes existentes | ✅ 142/142 |

- **O `catch` da corrida foi exercitado:** nos 20 simultâneos, 4 requisições passaram juntas pela checagem do service; o banco barrou o `INSERT` em `UX_WeightRecords_SyncId` (4 violações no log; identity pulou de 33 para 37) e o `AddSyncableAsync` devolveu a existente. Nenhum erro não tratado. Confirma o risco da §8 (animal rastreado no mesmo contexto): só a pesagem é desanexada.
- Registros de teste: pesagens 30, 31 e 33 do animal 111 e 32 do animal 114 (inativo), todas ativas.

### 6.4 Fase 4 — Pesagem inicial endereçável

- `AnimalCreateDto`: `Guid? InitialWeightSyncId` + validação no `Validate` existente ("O identificador de sincronização da pesagem inicial não pode ser vazio.", campo `InitialWeightSyncId`).
- `AnimalService.CreateWeightRecord`: `SyncId = dto.InitialWeightSyncId ?? Guid.NewGuid()`.
- Controller e `AnimalDto` sem mudança de formato (P3).

| Validação (10/Out/2026) | Resultado |
|---|---|
| `POST /api/animals` com `syncId`, `initialWeight` 280 e `initialWeightSyncId` | ✅ `201`, animal 115; pesagem 38 com o `SyncId` do app (conferido no banco — `weightRecords[].syncId` só aparece na resposta a partir da Fase 6) |
| Reenvio do mesmo cadastro | ✅ `201`, mesmo animal (115) e mesma pesagem (38); 1 linha com o `SyncId` |
| `initialWeightSyncId` sem `initialWeight` | ✅ `201`, animal 116, `weightRecords: []`, nenhuma pesagem com o `SyncId` |
| `initialWeightSyncId` vazio | ✅ `400` "O identificador de sincronização da pesagem inicial não pode ser vazio." (formato B, campo `InitialWeightSyncId`) |
| `PATCH` da pesagem inicial pelo `id` devolvido, **só** com `weightObservations` | ⚠️ `200`, mas gravou `weight = 0` e `recordedAt = 0001-01-01` — **§3 item 5 confirmado** (bug pré-existente do profile de edição; corrigido na Fase 5) |
| `DELETE` da pesagem inicial | ✅ `204`; `IsActive=0` |
| Testes | ✅ 144/144 (testes 17 e 18 da §6.7 incluídos) |

- Registros de teste: animais 115 (pesagem 38 inativa, com os valores corrompidos pelo bug) e 116.

### 6.5 Fase 5 — Edição com last-write-wins

- `WeightRecordUpdateDto`: `DateTime? UpdatedAt`.
- `WeightRecordProfile` (edição): `UpdatedAt` passa a `Ignore()`; ignora também `SyncId`, `RowVersion`, `AnimalId` e `PropertyId`. **Se o §3 item 5 se confirmar:** `PreCondition(src => src.Weight.HasValue)` em `Weight` e `PreCondition(src => src.WeightDate.HasValue)` em `RecordedAt`.
- `WeightRecordService.UpdateWeightRecordAsync`: R1 e R2 antes do LWW (são `404` de existência, como na 14.3):

```csharp
await FindAnimalAsync(animalId);
var existing = await FindWeightRecordAsync(id, animalId);

var editedAt = SyncTimestampResolver.ResolveEditedAt(weightRecordUpdateDto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, existing))
    return _mapper.Map<WeightRecordDto>(existing);

_mapper.Map(weightRecordUpdateDto, existing);
existing.UpdatedAt = editedAt;
var updated = await _weightRecordRepository.UpdateWeightRecordAsync(existing);
return _mapper.Map<WeightRecordDto>(updated);
```

| Validação (10/Out/2026) | Resultado |
|---|---|
| `PATCH` só `weightObservations` (**antes** da correção) | ⚠️ Bug confirmado já na Fase 4 (§6.4): `weight = 0`, `recordedAt = 0001-01-01` |
| `PATCH` só `weightObservations` na pesagem 31 (361 kg, 16:05Z) | ✅ Observação aplicada; peso e data **preservados**; `updatedAt` = horário do cliente (`16:08:10Z`) |
| `PATCH` só `weight` (365) | ✅ Peso aplicado; data e observação preservadas |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| `PATCH` só `weightDate` com `updatedAt` `13:10:25-03:00` | ✅ Data aplicada; `updatedAt` gravado como `16:10:25Z` |
| Edição mais antiga (−1 h, com `weight: 999`) | ✅ `200`, ignorada; peso 365 mantido |
| Sem `updatedAt` (Swagger/chamada direta) | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) | ✅ Aplicado; `updatedAt` limitado a "agora" (também observado num envio com fuso calculado errado no roteiro — `16:09:40-03:00`, futuro — que foi limitado a "agora") |
| `PATCH` em pesagem inativa (27) | ✅ `200`, observação aplicada, continua inativa (P2) |
| Pesagem inexistente | ✅ `404` "Pesagem com id '999999' não encontrada." |
| Testes existentes | ✅ 144/144 |

- Registro de teste: pesagem 31 termina com 365 kg, data 09/10 e observação "relogio adiantado".

### 6.6 Fase 6 — Pull incremental

- `WeightRecordDto`: `SyncId`, `AnimalId`, `IsActive`, `CreatedAt`, `UpdatedAt` (P5); sem o `DateFormatConverter` no `RecordedAt` (P8). Os dois mapas `WeightRecord → WeightRecordDto` (`WeightRecordProfile` e `AnimalProfile`) são por convenção e pegam os campos novos sem mudança.
- `IWeightRecordRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<WeightRecord>`.
- `IWeightRecordService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<WeightRecordDto>` (mesmo código da Spec #14 §10.5).
- `WeightRecordsController`: `[HttpGet("~/api/animals/weight-records/changes")]` (P4).

| Validação (10/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 9 pesagens (3 inativas: 27, 28, 38), de 4 animais, inclusive a da cria criada pelo parto (28/29); todas com `syncId` e `animalId`; `recordedAt` em ISO (`2026-10-10T12:00:00`); `nextCursor` `38530`, `hasMore: false` |
| Pull × `GET /{id}`, item a item (JSON completo) | ✅ **Nenhuma divergência** |
| Isolamento por propriedade | ✅ A tabela tinha 37 pesagens; vieram só as da propriedade do usuário |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido |
| Cria A (39), edita A, cria B (40), exclui B → pull | ✅ Só A (ativa, peso final 371) e B (inativa), cada uma **uma vez** |
| Editar o animal 111 (nome) → pull de pesagens | ✅ `items: []` — as pesagens **não** reaparecem |
| Paginação `limit=1` | ✅ 11 páginas, 11 itens distintos, mesma ordem do pull completo |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |
| `GET /api/animals/changes` e `GET /api/animals/111` | ✅ `200`, sem colisão com a rota nova |
| Consultas por página (log do EF) | ✅ **1** com 1 item e **1** com a página completa |
| **P3 de ponta a ponta:** `POST /api/animals` com `initialWeightSyncId` | ✅ `201`, animal 117; `weightRecords` traz a pesagem 41 com o `syncId` do app — o app a encontra por ele; `lastWeightRecord` idem |
| Animal 115 (pesagem inicial excluída na Fase 4) | ✅ `weightRecords: []` |
| Testes existentes | ✅ 144/144 |

- Mesma diferença de formato já registrada na Spec #14 §10.2: valores recém-gravados saem com `Z` (vindos da requisição) e os lidos do banco saem sem `Z` (ex.: `recordedAt` `2026-10-10T19:00:00Z` no `201` × `2026-10-10T12:00:00` no pull). O app deve tratar datas sem fuso como UTC (contrato §5.3).
- Registros de teste: animal 117 (pesagem 41) e pesagens 39 (ativa) e 40 (inativa) do animal 111.

### 6.7 Fase 7 — Testes

`MuuBoi.Tests/Services/WeightRecordServiceTests.cs` (**novo**) — padrão de `MilkProductionServiceTests`/`VaccineServiceTests` (repositórios mockados, AutoMapper real, sem testar mapeamento).

| # | Teste |
|---|---|
| 1 | `CreateWeightRecordAsync_WithNewSyncId_CreatesRecordWithGivenSyncId` |
| 2 | `CreateWeightRecordAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateWeightRecordAsync_WithExistingSyncId_DoesNotLoadAnimal` |
| 4 | `CreateWeightRecordAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateWeightRecordAsync_WithoutWeightDate_UsesNow` (R3) |
| 6 | `CreateWeightRecordAsync_WhenAnimalNotFound_ThrowsNotFoundException` (R1) |
| 7 | `UpdateWeightRecordAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 8 | `UpdateWeightRecordAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 9 | `UpdateWeightRecordAsync_WithoutClientUpdatedAt_UsesNow` |
| 10 | `UpdateWeightRecordAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 11 | `UpdateWeightRecordAsync_WhenRecordNotFound_ThrowsNotFoundException` (R2) |
| 12 | `DeleteWeightRecordAsync_WhenActive_DeactivatesAndReturnsTrue` |
| 13 | `DeleteWeightRecordAsync_WhenAlreadyInactive_ReturnsTrueWithoutDeleting` |
| 14 | `DeleteWeightRecordAsync_WhenRecordNotFound_ThrowsNotFoundException` (R2) |
| 15 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 16 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |

Em `AnimalServiceTests` (existente):

| # | Teste |
|---|---|
| 17 | `CreateAnimalAsync_WithInitialWeightSyncId_AttachesRecordWithGivenSyncId` (P3) |
| 18 | `CreateAnimalAsync_WithoutInitialWeightSyncId_GeneratesRecordSyncId` (P3) |

- Os testes 5, 6, 11 e 14 fixam regras que já existem e hoje não têm cobertura (não há `WeightRecordServiceTests`).
- Fora dos testes de service: o filtro de inativas (repositórios — regra "só services" do `CLAUDE.md`), o `[Required]` da P6 (validação do DTO), a correção do §3 item 5 (profile) e o formato ISO da P8 (serialização). Ficam cobertos pela validação manual das Fases 2, 3, 5 e 6.
- **Resultado (10/Out/2026):** 16/16 em `WeightRecordServiceTests` na primeira execução; 17 e 18 em `AnimalServiceTests` (Fase 4); projeto inteiro **160/160** (142 anteriores + 18 novos).

### 6.8 Fase 8 — Documentação

- **Esta spec:** status das fases e resultados de validação.
- **Spec #14:** §8 (`WeightRecord` ✅), §13 questão 4 (resolvida para a pesagem; `AnimalMedication` também faz exclusão física — registrar na questão), cabeçalho e histórico.
- **Spec #14.4:** Q3 resolvida; §5 item 3 aponta para esta spec quanto à pesagem.
- **`Docs/catalogo-erros-api.md` §4.4:** `400` de peso obrigatório, de `syncId` vazio e do cursor; nota de rota com suporte offline; `DELETE` repetido deixa de ser `404`. §4.3: `400` de `initialWeightSyncId` vazio.
- **`spec-animais.md`:** nota sobre o histórico e o último peso considerarem só as pesagens ativas.

**Feito (10/Out/2026):**
- Spec #14 → v1.4: cabeçalho e histórico; sumário com as cinco specs filhas; §8 (`WeightRecord` ✅; `BodyConditionRecord` e `AnimalMedication` continuam ⏳); §13 questão 4 resolvida para a pesagem e mantida aberta para `AnimalMedication`.
- Spec #14.4: §5 item 3 aponta para esta spec quanto à pesagem; Q3 resolvida; "próxima spec sugerida" marcada como feita.
- `catalogo-erros-api.md`: §4.3 com o `400` de `initialWeightSyncId` vazio; §4.4 com os `400` de peso obrigatório, `syncId` vazio e cursor, nota de rotas offline (inclui o `DELETE` repetido deixando de ser `404` e o `GET /{id}` de excluída), mudança de formato do `recordedAt` e a correção do `PATCH` parcial.
- `spec-animais.md`: nota no topo sobre pesagens (exclusão lógica, `initialWeightSyncId`, `recordedAt` em ISO).

**Registros de teste** (conta `teste.offline.pesagem@muuboi.local`, propriedade "Fazenda Teste Offline Pesagem"): animais 111, 112 (vaca), 113 (cria do parto 11), 114 (inativo), 115, 116 e 117; pesagens 26–33 e 38–41. Ficam no banco local como os das specs anteriores.

**Pendências observadas (fora desta spec):**
- Próxima spec sugerida (ordem de complexidade da Spec #14 §8): **ECC** (`BodyConditionRecord`) — exige `PropertyId` com backfill (Q4 da 14.4) e acrescentar `initialBodyConditionSyncId` ao `POST` do animal, no mesmo padrão da P3.
- `AnimalMedication` também faz exclusão física (Q2 desta spec).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/WeightRecord.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_WeightRecord_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/WeightRecordRepository.cs` | Exclusão lógica; filtro de ativas na lista; `GetWeightRecordBySyncIdAsync`, `CreateWeightRecordAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Infrastructure | `Infrastructure/Repositories/AnimalRepository.cs` | `Include` filtrado por `IsActive` nas pesagens (3 consultas) |
| Infrastructure | `Infrastructure/Repositories/AnimalCalvingRepository.cs` | `Include` filtrado por `IsActive` no `GetCalfByIdAsync` |
| Application | `Application/DTOs/WeightRecordCreateDto.cs` | `SyncId?` + validação; `[Required]` no `Weight` |
| Application | `Application/DTOs/WeightRecordUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/WeightRecordDto.cs` | `SyncId`, `AnimalId`, `IsActive`, `CreatedAt`, `UpdatedAt` |
| Application | `Application/DTOs/AnimalCreateDto.cs` | `InitialWeightSyncId?` + validação |
| Application | `Application/Mappings/WeightRecordProfile.cs` | Ignora `SyncId`, `RowVersion`, `PropertyId` (e `AnimalId`/`UpdatedAt` na edição); `PreCondition` se o §3 item 5 se confirmar |
| Application | `Application/Interfaces/IWeightRecordRepository.cs`, `IWeightRecordService.cs` | Novos métodos |
| Application | `Application/Services/WeightRecordService.cs` | Criação idempotente, LWW, inativação idempotente, pull |
| Application | `Application/Services/AnimalService.cs` | `CreateWeightRecord` com `InitialWeightSyncId` |
| Api | `Api/Controllers/WeightRecordsController.cs` | Rota `GET ~/api/animals/weight-records/changes` |
| Tests | `MuuBoi.Tests/Services/WeightRecordServiceTests.cs` | **Novo** |
| Tests | `MuuBoi.Tests/Services/AnimalServiceTests.cs` | Testes 17 e 18 |

**Não muda:** regras R1–R7, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `AnimalCalvingService`, `AnimalSyncDto` e o pull de animais, rotas de tela (exceto os campos aditivos).

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Pesagens excluídas fisicamente antes da migração não chegam como tombstone | Sem efeito prático: o app ainda não tem dados locais; o pull inicial traz o estado atual |
| Esquecer uma leitura sem o filtro de `IsActive` e a pesagem excluída voltar a aparecer (ex.: relatório ou dashboard futuro) | Lista do §3 item 3 levantada por busca de `WeightRecords` no código inteiro; nova leitura deve filtrar ativas |
| `ApplyBirthWeight` grava `UpdatedAt = agora` (horário do servidor) e pode descartar uma edição offline mais antiga da mesma pesagem (LWW) | Mesmo comportamento já aceito para mudanças feitas pelo servidor (S2 da 14.2); vale a mais recente |
| LWW por registro inteiro: edições de campos diferentes em celulares diferentes — vence a mais recente | Risco já aceito (Spec #14 §5.3); evolução: controle por versão |
| `initialWeightSyncId` igual ao de outra pesagem já existente (erro do app) → violação de `UX_WeightRecords_SyncId` não tratada → `500` | Aceito, como na 14.2 e 14.3: UUID v7 gerado no app torna isso impraticável (A4) |
| Corrida no `AddSyncableAsync` com o animal rastreado no mesmo contexto (carregado pelo `FindAnimalAsync`) | O helper desanexa só a pesagem que falhou e devolve a existente; o animal não é modificado. Validar nos 20 envios simultâneos da Fase 3 |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Peso positivo e data não futura** (R5): hoje não há validação; offline, uma data futura por relógio adiantado passaria a ser o "último peso" | ⏳ Fora desta spec (regra nova). Se adotada, `400` no DTO (definitivo para o app) |
| Q2 | **`AnimalMedication` também faz exclusão física** (`AnimalMedicationRepository.cs:53`) | ⏳ Mesma conversão da P1, na spec dos casos de saúde |
