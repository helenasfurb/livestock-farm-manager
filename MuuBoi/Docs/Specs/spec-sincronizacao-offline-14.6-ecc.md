# Spec #14.6: Sincronização Offline — Escore de Condição Corporal (ECC)

**Módulo:** Infraestrutura / Sincronização — ECC
**Versão:** 1.0
**Data:** 10/Out/2026
**Status:** ✅ **Implementada e validada** (10/Out/2026) — decisões C1–C8, Fases 1–7. Branch `feature/offline-body-condition-records`. Nenhuma regra de negócio alterada (R1–R9); `PropertyId` com backfill (Q4 da 14.4) e validação de `score`/`recordedAt` corrigida (§3 item 6, C7).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 10/Out/2026 | Rascunho: ECC planejado **sem alterar nenhuma regra de negócio**, com `PropertyId` e backfill (Q4 da 14.4) e o ECC inicial endereçável (N2 da 14.4), no padrão da Spec #14.5 |
| 0.2 | 10/Out/2026 | Decisões C1–C8 aprovadas conforme recomendado (C2 e C7 pela usuária). Branch criada a partir de `feature/offline-weight-records` (commit `a29d310`) |
| 1.0 | 10/Out/2026 | Fases 1–7 implementadas e validadas; 178 testes passando (18 novos). §3 item 6 confirmado e corrigido (C7). Spec #14 v1.5, Spec #14.4, `catalogo-erros-api.md` §4.3/§4.7 e `spec-ecc.md` atualizados |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2), receita (§7.4) e Condições 1–3 (§7.5) · Spec #14.4 (`Animal` sincronizável; N2 reservou o `initialBodyConditionSyncId`; Q4 registrou a falta de `PropertyId`) · Spec #14.5 (pesagem — mesmo formato de entidade filha do animal; esta spec segue P3, P4, P5 e P6 de lá). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-ecc.md` (D6, D7, RN-01–RN-06) · `spec-gestacao-parto-6.2-parto.md` (D3 — ECC opcional no parto) · `Docs/catalogo-erros-api.md` §4.7

> **Por que ECC agora:** é a próxima entidade em complexidade (Spec #14 §8). Tem **uma FK** (`AnimalId`, já sincronizável), **não tem regra de unicidade** (Condição 3 não se aplica), **cada operação grava um registro** (Condição 1 não se aplica) e **não tem exclusão** — não há tombstone a resolver, ao contrário da pesagem.
>
> **O que a torna diferente da pesagem:** (1) **não tem `PropertyId`** nem `HasQueryFilter` — o tenant vem só pelo animal (RN-06 da `spec-ecc.md`). O helper `ConfigureSyncable()` e o pull exigem `ITenantEntity`, então a tabela precisa de `PropertyId` **preenchido para as linhas existentes** (backfill pelo animal) — é a primeira migração com `UPDATE` de dados desde o início da Spec #14; (2) o `POST` **exige animal ativo** (RN-03, `409`), uma regra de estado que pode virar conflito real entre celulares; (3) o `AnimalDto` traz só o **último** ECC (`lastBodyConditionRecord`), não a lista, o que muda o casamento do ECC inicial (C3).
>
> **Sem cliente web:** o único cliente é o app (Spec #14 v1.2). Requisições sem `syncId`/`updatedAt` vêm só do Swagger/chamada direta.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST`, `PATCH /api/animals/{animalId}/body-condition-records[/{recordId}]` com suporte offline | Exclusão de ECC — **não existe rota** (C2, RN-04 da `spec-ecc.md`) |
| `PropertyId` + filtro de tenant no `BodyConditionRecord`, com backfill (Q4 da 14.4) | ECC gerado pelo **parto** offline — spec do parto (Condição 1, §3 item 9) |
| `GET /api/animals/body-condition-records/changes` (**nova**) | Edição de `notes` no `PATCH` — não existe hoje (Q1) |
| `initialBodyConditionSyncId` no `POST /api/animals` (N2 da 14.4) | Status derivados do animal (N6 da 14.4) — o app passa a calcular o último ECC localmente |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/animals/{animalId}/body-condition-records` | Sempre cria; sem `recordedAt` → grava `0001-01-01`; `score` inválido aceito (a confirmar) | Aceita `syncId`; se já existir → devolve o existente (`201`). `recordedAt` e `score` válidos obrigatórios → `400` (C7) |
| `PATCH /api/animals/{animalId}/body-condition-records/{recordId}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW). `score` inválido → `400` (C7) |
| `GET /api/animals/{animalId}/body-condition-records` | Todos os registros do animal | Sem mudança (não há inativos — C2) |
| `GET /api/animals/body-condition-records/changes?since=&limit=` | — | **Nova** (C5) |
| `POST /api/animals` | ECC inicial recebe `SyncId` do banco | Aceita `initialBodyConditionSyncId` (C3); resposta inalterada — `lastBodyConditionRecord` já traz `id` e (agora) `syncId` |
| Respostas com `BodyConditionRecordDto` (inclui `lastBodyConditionRecord` do `AnimalDto`) | — | Ganham `syncId`, `animalId`, `isActive`, `updatedAt` (aditivo, C4) |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.5): nenhuma regra de negócio é alterada.** As únicas mudanças de comportamento são as exigidas pela sincronização e a correção de validações que hoje deixam passar dados inválidos (C7), que não são regra.

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Animal precisa existir | Todas as rotas | `404` "Animal com id '{animalId}' não encontrado." |
| R2 | Animal precisa estar **ativo** para receber ECC novo (RN-03) | `POST` | `409` "Não é possível registrar ECC de um animal inativo." O `PATCH` aceita animal inativo |
| R3 | Registro precisa existir **e pertencer ao animal da rota** (`GetByIdAsync(id, animalId)`) | `PATCH` | `404` "Registro de ECC com id '{recordId}' não encontrado." |
| R4 | Data da avaliação não pode ser futura (D6, RN-02) | `POST`, `PATCH` (service) | `422` "A data da avaliação não pode ser futura." |
| R5 | Observações com no máximo 500 caracteres (D7) | `POST` (DTO) | `400` (formato B). Não são editáveis no `PATCH` (Q1) |
| R6 | Histórico em ordem decrescente de `RecordedAt` (RN-05); último ECC = maior `RecordedAt` | `GET /`, `AnimalDto.LastBodyConditionRecord` | Mantido |
| R7 | ECC inicial do cadastro sem data → **momento do processamento** (`InitialBodyConditionDate ?? DateTime.UtcNow`); data futura → `400` | `POST /api/animals` | Mantido (R5 da 14.4) |
| R8 | ECC do parto: se informado, o parto cria o registro pelo `BodyConditionRecordService.CreateAsync`, com `RecordedAt = calvingDate` (D3 da 6.2) | `AnimalCalvingService` | Mantido; recebe `SyncId` gerado pelo servidor (§3 item 9) |
| R9 | **Não há exclusão** de ECC (RN-04) | — | Mantido (C2) |

**Ordem entre `SyncId` e regras:** como nas specs anteriores, o `SyncId` é checado **antes** de R1, R2 e R4. O reenvio de um ECC já gravado responde `201` com o existente, mesmo que outro celular tenha registrado a saída do animal depois. Um `POST` **novo** (outro `syncId`) em animal inativo continua `409` (R2) — conflito real (§5 item 6). O `syncId` não é conferido contra o `animalId` da rota (mesma escolha da 14.5).

**Divergência registrada:** a `spec-ecc.md` (RN-04) diz que registros são imutáveis, sem `PATCH` nem `DELETE`. O `PATCH` foi acrescentado depois (commit `fbdc3a7`, "add crud and validations for body condition records") e está no catálogo de erros (§4.7). Esta spec trata o código atual como fonte da verdade: `PATCH` existe (score e data), `DELETE` não.

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `BodyConditionRecord : BaseEntity` — **sem `PropertyId`, sem `ITenantEntity`, sem `HasQueryFilter`**. O tenant vem só pelo animal (o service sempre carrega o animal antes) | `ConfigureSyncable()` e `GetChangesSinceAsync` exigem `ITenantEntity`; sem filtro, o pull traria ECC de **todas** as propriedades | `PropertyId` + `ITenantEntity` + `HasQueryFilter` + índice; migração com backfill pelo `Animals.PropertyId` (C1) |
| 2 | 13 registros no banco local, todos com animal existente, nenhum inativo (consulta de 10/Out/2026) | O backfill precisa preencher todos; nenhum órfão | `UPDATE ... FROM Animals` na migração; validar zero `PropertyId` vazio (Fase 1) |
| 3 | O EF emite o aviso "Entity 'Animal' has a global query filter defined and is the required end of a relationship with the entity 'BodyConditionRecord'" | — | Some com o filtro do C1 (validar no log da Fase 1) |
| 4 | **Não há rota de exclusão** nem nenhum caminho que inative um ECC (o cancelamento do parto inativa crias, animais e lactação, mas não o ECC) | Não há tombstone a sincronizar; `isActive` é sempre `true` | Sem mudança (C2) |
| 5 | `UpdateAsync` atribui campo a campo (sem AutoMapper) e carimba `UpdatedAt = DateTime.UtcNow` | Precisa do LWW; **sem** risco do bug do `ForAllMembers` (14.1, 14.3, 14.5) | LWW no service; R4 continua antes das atribuições (C6) |
| 6 | `BodyConditionRecordCreateDto.Score` é `BodyConditionScore` (não anulável) com `[Required]` e **sem `[ValidEnum]`**; `RecordedAt` é `DateTime` (não anulável) com `[Required]` | **Prováveis bugs pré-existentes:** `[Required]` não pega tipo de valor ausente. `POST` sem `recordedAt` grava `0001-01-01` (não é futura → passa R4); `score` ausente vira `0`, e `score: 9` passaria — valores fora do enum (o `AnimalCreateDto` já usa `[ValidEnum]` no ECC inicial). No `PATCH`, `score` também sem `[ValidEnum]` | **Confirmado no início da Fase 2** (§6.2): sem `recordedAt` → `201` com `0001-01-01`; sem `score` → `201` com escore `0` (`scoreLabel: "0"`); `score: 9` → `201`; `PATCH` com `score: 9` → `200`. Tratamento: C7 |
| 7 | `CreateAsync` usa `_repository.CreateAsync` (`Add` + `SaveChanges`) | Reenvio duplica o ECC silenciosamente (sem regra de unicidade, como a pesagem) | `SyncId` + `AddSyncableAsync` (Fase 2) |
| 8 | O ECC inicial do cadastro do animal recebe `SyncId` do banco | **Condição 2:** o app não casa o ECC do pull com o local nem endereça uma correção feita antes do primeiro sync | `initialBodyConditionSyncId` (C3) |
| 9 | O parto cria o ECC chamando `_bodyConditionRecordService.CreateAsync` **depois** de gravar o parto (outro `SaveChanges`) | **Condição 1** — mas é problema **do parto**: uma queda entre as gravações deixa o parto sem ECC. Aqui o ECC só recebe `SyncId` do servidor e chega pelo pull | Nenhum agora; registrar para a spec do parto (§9 Q3) |
| 10 | `AnimalDto` traz só o **último** ECC (`LastBodyConditionRecord`, `Include` com `Take(1)`), ao contrário das pesagens (lista completa) | Para casar o ECC inicial (C3), o app depende de ele ser o último. No cadastro é sempre o único; no reenvio, só deixa de ser se **outro celular** tiver registrado um ECC mais recente nesse intervalo | C3 usa `lastBodyConditionRecord`; caso raro coberto pelo pull (§5 item 3, §8) |
| 11 | `BodyConditionRecordDto` tem `Id`, `Score`, `ScoreLabel`, `RecordedAt`, `Notes`, `CreatedAt` — sem `[JsonConverter]` de data (já sai em ISO) | No pull faltam o animal, a identidade, o estado e a data de edição | Campos aditivos (C4); nada a mudar no formato (diferente da P8 da 14.5) |
| 12 | Não há `GET /{recordId}`; o `POST` responde `CreatedAtAction(nameof(GetAll))` (`Location` aponta para a lista) | O "mesmo DTO do detalhe" do pull (Spec #14 §5.5) não tem rota de detalhe para comparar | Pull usa o `BodyConditionRecordDto` das rotas existentes; validação compara com `GET /` (Fase 5). Sem rota nova |
| 13 | `AccountRepository` remove os ECC por `animalIds` (`ExecuteDeleteAsync`) | Só na exclusão da conta | Sem mudança (poderia passar a usar `PropertyId`, mas não é necessário) |
| 14 | `AnimalRepository` (linhas 111 e 120) inclui o último ECC sem filtro de `IsActive` | Nenhum (não há inativos — item 4) | Sem mudança |

## 4. Decisões (aprovadas em 10/Out/2026)

As marcadas com ⭐ pediam escolha da usuária (aprovadas conforme a recomendação); as demais seguem precedentes das specs 14.1–14.5.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| C1 | **`PropertyId` no `BodyConditionRecord`** (`ITenantEntity`), com `HasQueryFilter(r => r.PropertyId == _propertyId)` e índice `IX_BodyConditionRecords_PropertyId`, como `WeightRecord`. **Uma migração** com tudo: coluna `PropertyId` (`NOT NULL`, default `Guid.Empty`), **`UPDATE` pelo animal** (SQL acrescentado à migração gerada), `SyncId`/`RowVersion` e índices. Novos registros recebem o `PropertyId` pelo override do `SaveChangesAsync` (já trata `ITenantEntity` adicionados) — cadastro do animal, rota de ECC e parto | Filtrar o tenant por join com `Animals` só no pull — exigiria uma variante do helper genérico `GetChangesSinceAsync` (que filtra pelo `HasQueryFilter` da própria entidade) e deixaria o ECC diferente de todas as outras sincronizáveis. Embutir o ECC no pull do animal (como o `AnimalExitRecord`, N5 da 14.4) — o ECC tem `PATCH` próprio e precisa de `SyncId` |
| C2 ⭐ | **Sem exclusão de ECC**, como hoje (RN-04 / R9). O pull traz `isActive` (sempre `true`) por uniformidade, e a estrutura fica pronta para um `DELETE` lógico no futuro, sem nova migração | Criar `DELETE` lógico agora (`204`, idempotente, como a P1/P7 da 14.5) — permitiria ao produtor desfazer um ECC lançado por engano, mas é **regra nova** (contradiz RN-04). Pode virar ajuste de domínio separado |
| C3 | **ECC inicial endereçável** (N2 da 14.4, P3 da 14.5): `AnimalCreateDto.InitialBodyConditionSyncId` (`Guid?`; ausente → servidor gera; sem `initialBodyConditionScore` → ignorado, sem erro; `Guid.Empty` → `400`). `CreateBodyConditionRecord` usa `dto.InitialBodyConditionSyncId ?? Guid.NewGuid()`. **A resposta não muda de formato:** o app localiza o ECC inicial em `lastBodyConditionRecord` pelo `syncId` (§3 item 10) | `initialBodyCondition { id, syncId }` na resposta (como o `initialMovement` da E1) — resolveria o caso raro do §3 item 10, mas cria um DTO novo para um caso que o pull já cobre |
| C4 | **`BodyConditionRecordDto` ganha `SyncId`, `AnimalId`, `IsActive`, `UpdatedAt`** (já tem `CreatedAt`) — mesmo DTO na lista, no `AnimalDto` e no pull (Spec #14 §5.5, sem exceção) | DTO próprio de sync — sem motivo |
| C5 | **Pull global**: `GET /api/animals/body-condition-records/changes` traz os ECC de **todos** os animais da propriedade (P4 da 14.5). Rota com caminho absoluto no `BodyConditionRecordsController` (`[HttpGet("~/api/animals/body-condition-records/changes")]`) | Pull por animal (um request por animal) |
| C6 | **`PATCH`: R1 → R3 → R4 → LWW → atribuições.** R4 (`422`, data futura) é validação do próprio pedido e já roda antes de qualquer gravação; mantê-la antes do LWW não muda nada para edições válidas (mesma escolha da 14.3, Fase 7). `UpdatedAt = editedAt` em vez de `DateTime.UtcNow` | LWW antes de R4 — uma edição antiga com data futura seria descartada em silêncio em vez de `422`; diferença irrelevante, e manter a ordem atual mexe menos |
| C7 ⭐ | **Validação de `score` e `recordedAt` no `POST`/`PATCH` → `400`** (se a Fase 2 confirmar o §3 item 6): `[ValidEnum(typeof(BodyConditionScore))]` em `Score` (criação e edição, como no `AnimalCreateDto`); `RecordedAt` da criação passa a `DateTime?` com `[Required]` (o service usa `.Value`). Correção de bug, não regra nova: RN-01/RN-02 já dizem que os dois são obrigatórios. No offline, um ECC com `0001-01-01` ou escore `0` sincronizaria normalmente e corromperia o "último ECC" em todos os celulares | Manter como está — o dado inválido é aceito com `201` e só aparece na tela |
| C8 | **`POST` em animal inativo continua `409`** (R2), checado depois do `SyncId`. É conflito real: outro celular registrou a saída do animal antes do sync. O app avisa e o usuário decide (descartar o ECC ou reativar o animal) — §5 item 6 | Aceitar ECC em animal inativo no offline — regra diferente online/offline (as rotas são as mesmas) |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem e `animalId`:** o ECC de um animal criado offline é enviado **depois** do `POST` do animal; o `animalId` da rota é o `ServerId` do animal, resolvido na hora do envio (A2, A5).
2. **Data da avaliação:** o app **deve** enviar `recordedAt` (UTC, com `Z`) — é obrigatória (RN-02). No ECC inicial, enviar `initialBodyConditionDate`; se omitida, o servidor usa o momento do processamento (R7).
3. **ECC inicial (C3):** um único item na fila — `POST /api/animals` com `initialBodyConditionScore`, `initialBodyConditionDate` e `initialBodyConditionSyncId`. No `201`, o app grava o `ServerId` do ECC lendo `lastBodyConditionRecord` **se o `syncId` for o mesmo**; se não for (reenvio depois de outro celular registrar um ECC mais recente — §3 item 10), o `ServerId` chega no próximo pull. Correção do ECC inicial antes de ter o `ServerId` espera na fila.
4. **Pull (`GET /api/animals/body-condition-records/changes`):** upsert por `syncId`; ligado ao animal local pelo `animalId`. Puxar **animais antes de ECC**.
5. **Último ECC** exibido é calculado sobre os ECC locais (maior `recordedAt`) — R6. Substitui o item 3 da §5 da 14.4 para o ECC: criar e editar ECC passa a funcionar offline.
6. **`409` de R2** (animal inativo): conflito real. O item vira `Failed` (A3); o app mostra que o animal saiu do rebanho e oferece descartar o ECC. Os itens seguintes daquele animal esperam (ver Spec #14 §12, "falha bloqueia só os dependentes").
7. **`422` de R4:** só com relógio do celular adiantado — erro definitivo (mesmo risco da 14.3 §5 item 8).
8. **Sem exclusão (C2):** o app não oferece excluir ECC. Um ECC lançado por engano é corrigido pelo `PATCH` (score e data).
9. **ECC do parto** chega pelo pull com `syncId` gerado pelo servidor; pode ser editado offline como qualquer outro.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco: `PropertyId` + backfill + `SyncId`/`RowVersion` (C1) | ✅ Migração `20261010164221_Offline_BodyConditionRecord_PropertyId_SyncId_RowVersion` aplicada |
| 2 — Criação idempotente (+ confirmação do §3 item 6 e C7) | ✅ Implementada e validada (§3 item 6 confirmado e corrigido) |
| 3 — ECC inicial endereçável (C3) | ✅ Implementada e validada |
| 4 — Edição com last-write-wins (C6) | ✅ Implementada e validada |
| 5 — Pull incremental (C4, C5) | ✅ Implementada e validada |
| 6 — Testes | ✅ 16 testes de `BodyConditionRecordService` + 2 em `AnimalServiceTests`; 178 no projeto |
| 7 — Documentação | ✅ Spec #14 (v1.5), Spec #14.4, `catalogo-erros-api.md` §4.3/§4.7, `spec-ecc.md` |

Sem fase de inativação (C2). Branch: `feature/offline-body-condition-records`, criada a partir de `feature/offline-weight-records` (ou do `dev`, depois do merge da pesagem), para que a migração fique em cima da `Offline_WeightRecord_SyncId_RowVersion`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com a conta de teste da 14.5 (`teste.offline.pesagem@muuboi.local`) e uma **segunda conta** para o isolamento entre propriedades.

### 6.1 Fase 1 — Domínio e banco

- `BodyConditionRecord : BaseEntity, ITenantEntity, ISyncable` — `Guid PropertyId`, `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`:

```csharp
builder.Entity<BodyConditionRecord>().HasQueryFilter(r => r.PropertyId == _propertyId);
builder.Entity<BodyConditionRecord>().HasIndex(r => r.PropertyId).HasDatabaseName("IX_BodyConditionRecords_PropertyId");
builder.Entity<BodyConditionRecord>().ConfigureSyncable();
```

- Migração `Offline_BodyConditionRecord_PropertyId_SyncId_RowVersion` (⚠️ **requer aprovação**). O EF gera as colunas e os índices; o `UPDATE` é **acrescentado à mão** no `Up`, logo depois do `AddColumn` do `PropertyId` (`migrationBuilder.Sql`). SQL esperado:

```sql
ALTER TABLE [BodyConditionRecords] ADD [PropertyId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
UPDATE r SET r.[PropertyId] = a.[PropertyId]
FROM [BodyConditionRecords] r INNER JOIN [Animals] a ON a.[Id] = r.[AnimalId];
ALTER TABLE [BodyConditionRecords] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [BodyConditionRecords] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_BodyConditionRecords_PropertyId] ON [BodyConditionRecords] ([PropertyId]);
CREATE INDEX [IX_BodyConditionRecords_PropertyId_RowVersion] ON [BodyConditionRecords] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_BodyConditionRecords_SyncId] ON [BodyConditionRecords] ([SyncId]);
```

- O `Down` remove índices e colunas (o EF gera); o `UPDATE` não precisa de reverso.

| Validação (10/Out/2026) | Resultado |
|---|---|
| SQL gerado + `UPDATE` | ✅ Idêntico ao esperado (o `UPDATE` executado logo após o `ADD [PropertyId]`); snapshot só muda em `BodyConditionRecord` (`PropertyId`, `RowVersion`, `SyncId` e os três índices) |
| Backfill | ✅ 13 registros, os 13 com `PropertyId` igual ao do animal, **nenhum** `Guid.Empty`; 2 propriedades distintas na tabela |
| `SyncId` / `RowVersion` | ✅ 13 `SyncId` distintos, nenhum vazio |
| Índices | ✅ `PK_BodyConditionRecords`, `IX_BodyConditionRecords_AnimalId_RecordedAt`, `IX_BodyConditionRecords_PropertyId`, `IX_BodyConditionRecords_PropertyId_RowVersion`, `UX_BodyConditionRecords_SyncId` |
| Aviso do EF sobre `BodyConditionRecord` (§3 item 3) | ✅ Não aparece mais (nem no `migrations add` nem no log da API); continua o do `AnimalExitRecord` (sem `PropertyId`, fora do escopo) |
| Cadastro de animal (118) com ECC inicial, `POST` de ECC e parto com ECC | ✅ ECC 14 (inicial), 15 (rota) e 16 (parto, `RecordedAt` = data do parto), os três com o `PropertyId` do usuário (pelo override) e `SyncId` preenchido |
| `GET /` e `PATCH` de ECC do animal 118 pela segunda conta (`teste.offline.ecc2@muuboi.local`) | ✅ `404` "Animal com id '118' não encontrado." (R1) — como antes |
| `GET /` pelo dono | ✅ `200`, ECC em ordem decrescente de `recordedAt` (R6) |
| Testes existentes | ✅ 160/160 |

- Registros de teste: vaca 118 (brinco `514010`) com os ECC 14–16, gestação e parto com cria; conta `teste.offline.ecc2@muuboi.local` (propriedade "Fazenda Teste Offline ECC 2") para o isolamento da Fase 5.

### 6.2 Fase 2 — Criação idempotente

- **Antes de mudar o código:** confirmar o §3 item 6 na API (`POST` sem `recordedAt`; `score: 0`; `score: 9`).
- `BodyConditionRecordCreateDto`: `Guid? SyncId` + `IValidatableObject` (`Guid.Empty` → `400`). Se o §3 item 6 se confirmar (C7): `[ValidEnum(typeof(BodyConditionScore))]` em `Score` e `DateTime? RecordedAt` com `[Required]`.
- `IBodyConditionRecordRepository`/`BodyConditionRecordRepository`: `GetBySyncIdAsync` → `FindBySyncIdAsync<BodyConditionRecord>`; `CreateAsync` → `AddSyncableAsync`.
- `BodyConditionRecordService.CreateAsync`: só o bloco do `SyncId` é novo, **antes** de R1, R2 e R4:

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return _mapper.Map<BodyConditionRecordDto>(existing);
}

var animal = await FindActiveAnimalAsync(animalId, requireActive: true);

if (dto.RecordedAt > DateTime.UtcNow)
    throw new BusinessRuleException("A data da avaliação não pode ser futura.");

var record = new BodyConditionRecord
{
    SyncId = dto.SyncId ?? Guid.NewGuid(),
    AnimalId = animal.Id,
    Score = dto.Score!.Value,
    RecordedAt = dto.RecordedAt!.Value,
    Notes = dto.Notes,
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};
```

- C7 aplicada: no `BodyConditionRecordCreateDto`, `Score` passou a `BodyConditionScore?` com `[Required]` + `[ValidEnum]` e `RecordedAt` a `DateTime?` com `[Required]` — as mensagens de "obrigatório" que já existiam passam a aparecer. No `BodyConditionRecordUpdateDto`, `[ValidEnum]` em `Score`. O parto (`AnimalCalvingService`) compila sem mudança (conversão implícita para o anulável).

| Validação (10/Out/2026) | Resultado |
|---|---|
| §3 item 6, **antes** da correção (animal 111) | ⚠️ **Confirmado:** ECC 17 (sem `recordedAt` → `0001-01-01`), 18 (sem `score` → `0`), 19 (`score: 0`) e 20 (`score: 9`) gravados com `201`; `PATCH` do 17 com `score: 9` → `200`. Ficam no banco local (não há exclusão — C2) |
| `POST` com `syncId` (animal 116) | ✅ `201`, `Id` 21, `SyncId` do app no banco |
| Mesmo `POST` repetido / mesmo `syncId` com payload diferente (escore 5, outra data, com observação) | ✅ `201`, mesmo `Id` (21), 1 linha, sem alterar (escore 3) |
| `POST` sem `syncId` (Swagger/chamada direta) | ✅ `201`, `Id` 22, `SyncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`) |
| C7: sem `recordedAt` / sem `score` | ✅ `400` "A data da avaliação é obrigatória." / "O escore de condição corporal é obrigatório." (formato B) |
| C7: `score: 0` / `score: 9` no `POST`; `score: 9` no `PATCH` | ✅ `400` "Valor inválido. Valores aceitos: Magreza Severa (1), …, Obesidade Severa (5)." (formato B, campo `Score`) |
| R4: data futura | ✅ `422` "A data da avaliação não pode ser futura." (mantido) |
| Saída do animal 116 → reenvio do ECC 21 | ✅ `201` com o existente — `SyncId` antes de R2 |
| R2: `POST` novo (outro `syncId`) no animal 116, inativo | ✅ `409` "Não é possível registrar ECC de um animal inativo." (mantido) |
| R1: `POST` novo com animal inexistente | ✅ `404` |
| Parto com ECC (vaca 120) | ✅ ECC 23 criado, com `SyncId` gerado |
| 20 envios simultâneos (mesmo `syncId`, animal 111) | ✅ Todos `201`; 1 linha (`Id` 24) |
| Testes existentes | ✅ 160/160 |

- **O `catch` da corrida foi exercitado:** 11 das 20 requisições passaram juntas pela checagem do service; o banco barrou os `INSERT` em `UX_BodyConditionRecords_SyncId` (11 falhas no log do EF; identity pulou de 24 para 35) e o `AddSyncableAsync` devolveu o existente. Nenhum erro não tratado.
- Registros de teste: ECC 21 e 22 (animal 116, agora inativo), 23 (vaca 120, com gestação e parto) e 24 (animal 111).

### 6.3 Fase 3 — ECC inicial endereçável

- `AnimalCreateDto`: `Guid? InitialBodyConditionSyncId` + validação no `Validate` ("O identificador de sincronização do ECC inicial não pode ser vazio.", campo `InitialBodyConditionSyncId`).
- `AnimalService.CreateBodyConditionRecord`: `SyncId = dto.InitialBodyConditionSyncId ?? Guid.NewGuid()`.

| Validação (10/Out/2026) | Resultado |
|---|---|
| `POST /api/animals` com pesagem **e** ECC iniciais, cada um com seu `syncId` | ✅ `201`, animal 122; ECC 36 e pesagem 42 com os `SyncId` do app (no banco; a pesagem já mostra o `syncId` na resposta — 14.5; o ECC passa a mostrar na Fase 5) |
| Reenvio do mesmo cadastro | ✅ `201`, mesmo animal (122), `lastBodyConditionRecord` = ECC 36; 1 linha com o `SyncId` |
| `initialBodyConditionSyncId` sem `initialBodyConditionScore` | ✅ `201`, animal 123, `lastBodyConditionRecord: null`, nenhum ECC com o `SyncId` |
| `initialBodyConditionSyncId` vazio | ✅ `400` "O identificador de sincronização do ECC inicial não pode ser vazio." (formato B, campo `InitialBodyConditionSyncId`) |
| Testes | ✅ 162/162 (testes 17 e 18 da §6.6 incluídos) |

- Registros de teste: animais 122 (ECC 36, pesagem 42) e 123.

### 6.4 Fase 4 — Edição com last-write-wins

- `BodyConditionRecordUpdateDto`: `DateTime? UpdatedAt`; `[ValidEnum(typeof(BodyConditionScore))]` em `Score` se a C7 for aprovada.
- `BodyConditionRecordService.UpdateAsync` (C6):

```csharp
await FindActiveAnimalAsync(animalId, requireActive: false);

var record = await _repository.GetByIdAsync(recordId, animalId)
    ?? throw new NotFoundException($"Registro de ECC com id '{recordId}' não encontrado.");

if (dto.RecordedAt.HasValue && dto.RecordedAt.Value > DateTime.UtcNow)
    throw new BusinessRuleException("A data da avaliação não pode ser futura.");

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, record))
    return _mapper.Map<BodyConditionRecordDto>(record);

if (dto.Score.HasValue)
    record.Score = dto.Score.Value;

if (dto.RecordedAt.HasValue)
    record.RecordedAt = dto.RecordedAt.Value;

record.UpdatedAt = editedAt;
```

| Validação (10/Out/2026) | Resultado |
|---|---|
| `PATCH` só `score` no ECC 36 (animal 122) | ✅ Escore 3 → 4; data preservada; `updatedAt` = horário do cliente (`17:04:39Z`) |
| `PATCH` só `recordedAt` | ✅ Data aplicada; escore preservado |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| `updatedAt` com fuso `-03:00` (`14:06:19-03:00`) | ✅ Aplicado; gravado como `17:06:19Z` |
| Edição mais antiga (−1 h, `score: 1`) | ✅ `200`, ignorada; escore 5 mantido |
| Sem `updatedAt` (Swagger/chamada direta) / relógio adiantado (+1 dia) | ✅ Aplicado com "agora" / limitado a "agora" |
| `PATCH` em ECC de animal inativo (ECC 21, animal 116) | ✅ `200`, aplicado (R2 só vale para o `POST`) |
| R4: data futura | ✅ `422` "A data da avaliação não pode ser futura."; nada gravado |
| R3: ECC inexistente / ECC 36 pela rota do animal 111 | ✅ `404` "Registro de ECC com id '...' não encontrado." |
| Testes existentes | ✅ 162/162 |

- Registro de teste: ECC 36 termina com escore 3, data 09/10.

### 6.5 Fase 5 — Pull incremental

- `BodyConditionRecordDto`: `SyncId`, `AnimalId`, `IsActive`, `UpdatedAt` (C4). O mapa do `BodyConditionRecordProfile` é por convenção e pega os campos novos.
- `IBodyConditionRecordRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<BodyConditionRecord>`.
- `IBodyConditionRecordService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<BodyConditionRecordDto>` (mesmo código da 14.5 §6.6).
- `BodyConditionRecordsController`: `[HttpGet("~/api/animals/body-condition-records/changes")]` (C5).

| Validação (10/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 12 ECC de 5 animais (inclusive os do backfill, os do parto e os 4 inválidos do §6.2), todos com `syncId`, `animalId`, `scoreLabel` e `isActive: true`; `nextCursor` `38596`, `hasMore: false` |
| Pull × `GET /` de cada animal, item a item (JSON completo) | ✅ **Nenhuma divergência** |
| **Isolamento por propriedade** (primeiro pull que depende do backfill) | ✅ A tabela tinha 29 ECC; a conta A vê os seus 12; a conta B (`teste.offline.ecc2`) cadastra um animal com ECC inicial e vê **só** esse (ECC 37); a conta A não vê o da B |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido |
| Cria A (38), edita A, cria B (39) → pull | ✅ A (escore final 3) e B, cada um **uma vez** |
| Editar o animal 122 → pull de ECC | ✅ `items: []` |
| **C3 de ponta a ponta:** cadastro com `initialBodyConditionSyncId` | ✅ `201`, animal 125; `lastBodyConditionRecord.syncId` = o enviado (ECC 40) |
| Paginação `limit=1` | ✅ 15 páginas, 15 itens distintos, mesma ordem do pull completo |
| `since=abc` / `since=-5` / `limit=0` / `limit=100000` | ✅ `400` / `400` / `200` / `200` |
| `GET /api/animals/changes`, `GET /api/animals/weight-records/changes` e `GET /api/animals/122` | ✅ `200`, sem colisão |
| Consultas por página (log do EF) | ✅ **1** com 1 item e **1** com a página completa |
| Testes existentes | ✅ 162/162 |

- Registros de teste: ECC 37 (conta B, vaca 124 `524001`), 38–39 (animal 122) e 40 (animal 125).

### 6.6 Fase 6 — Testes

`MuuBoi.Tests/Services/BodyConditionRecordServiceTests.cs` (**novo** — hoje não há testes de ECC) — padrão de `WeightRecordServiceTests`.

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesRecordWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotLoadAnimal` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAsync_WhenAnimalNotFound_ThrowsNotFoundException` (R1) |
| 6 | `CreateAsync_WhenAnimalInactive_ThrowsConflictException` (R2) |
| 7 | `CreateAsync_WithFutureDate_ThrowsBusinessRuleException` (R4) |
| 8 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 9 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 10 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` |
| 11 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` |
| 12 | `UpdateAsync_WithFutureDate_ThrowsBusinessRuleException` (R4) |
| 13 | `UpdateAsync_WhenRecordNotFound_ThrowsNotFoundException` (R3) |
| 14 | `UpdateAsync_WhenAnimalInactive_AppliesChanges` (R2 só no `POST`) |
| 15 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 16 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |

Em `AnimalServiceTests` (existente):

| # | Teste |
|---|---|
| 17 | `CreateAnimalAsync_WithInitialBodyConditionSyncId_AttachesRecordWithGivenSyncId` (C3) |
| 18 | `CreateAnimalAsync_WithoutInitialBodyConditionSyncId_GeneratesRecordSyncId` (C3) |

- Os testes 5, 6, 7, 12 e 13 fixam regras que já existem e hoje não têm cobertura.
- Fora dos testes de service: o backfill e o filtro de tenant (migração/`DbContext`), a C7 (validação do DTO). Cobertos pela validação manual das Fases 1, 2 e 5.
- **Resultado (10/Out/2026):** 16/16 em `BodyConditionRecordServiceTests` na primeira execução; 17 e 18 em `AnimalServiceTests` (Fase 3); projeto inteiro **178/178** (160 anteriores + 18 novos).

### 6.7 Fase 7 — Documentação

- **Esta spec:** status das fases e resultados.
- **Spec #14:** §8 (`BodyConditionRecord` ✅), cabeçalho, histórico e sumário.
- **Spec #14.4:** Q4 resolvida; §5 item 3 aponta para esta spec quanto ao ECC.
- **`Docs/catalogo-erros-api.md` §4.7:** `400` de `syncId` vazio, do cursor e (se C7) de `score`/`recordedAt`; nota de rota com suporte offline. §4.3: `400` de `initialBodyConditionSyncId` vazio.
- **`spec-ecc.md`:** nota no topo — sincronização, `PropertyId` (RN-06 passa a ser garantida também pelo filtro da própria tabela) e a divergência RN-04 × `PATCH` existente.

**Feito (10/Out/2026):**
- Spec #14 → v1.5: cabeçalho e histórico; sumário com a 14.6; §8 (`BodyConditionRecord` ✅; `AnimalMedication` continua ⏳).
- Spec #14.4: §5 item 3 aponta para esta spec quanto ao ECC (só a lactação continua exigindo conexão); Q4 resolvida.
- `catalogo-erros-api.md`: §4.3 com o `400` de `initialBodyConditionSyncId` vazio; §4.7 com os `400` de escore/data obrigatórios, escore fora do enum, `syncId` vazio e cursor, nota de rotas offline e registro da correção da validação.
- `spec-ecc.md`: nota no topo (sincronização, `PropertyId`, RN-01/RN-02 agora validadas, divergência RN-04 × `PATCH`).

**Registros de teste** (banco local): conta `teste.offline.pesagem@muuboi.local` — animais 111 (ECC 17–20 inválidos, gravados antes da correção, e 24), 116 (inativo; ECC 21–22), 118 (vaca; ECC 14–16), 120 (vaca; ECC 23), 122 (ECC 36, 38, 39), 123 e 125 (ECC 40); conta `teste.offline.ecc2@muuboi.local` — vaca 124 (ECC 37). Como não há exclusão de ECC (C2), os inválidos 17–20 ficam no banco local.

**Pendências observadas (fora desta spec):**
- Próxima spec sugerida (ordem de complexidade da Spec #14 §8): **lactação** (`Lactation`) — mudança de estado (secar/desfazer secagem) com `2xx` no estado repetido (A6) e `initialLactationSyncId` no cadastro do animal (N2 da 14.4). A lactação aberta pelo parto fica para a spec do parto.
- Q3 desta spec (ECC do parto fora da transação) entra na spec do parto.

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/BodyConditionRecord.cs` | `ITenantEntity` (`PropertyId`), `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `HasQueryFilter`, índice de `PropertyId`, `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_BodyConditionRecord_PropertyId_SyncId_RowVersion*.cs` + snapshot | **Nova migração com backfill** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/BodyConditionRecordRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Application | `Application/DTOs/BodyConditionRecordCreateDto.cs` | `SyncId?` + validação; C7 |
| Application | `Application/DTOs/BodyConditionRecordUpdateDto.cs` | `UpdatedAt?`; C7 |
| Application | `Application/DTOs/BodyConditionRecordDto.cs` | `SyncId`, `AnimalId`, `IsActive`, `UpdatedAt` |
| Application | `Application/DTOs/AnimalCreateDto.cs` | `InitialBodyConditionSyncId?` + validação |
| Application | `Application/Interfaces/IBodyConditionRecordRepository.cs`, `IBodyConditionRecordService.cs` | Novos métodos |
| Application | `Application/Services/BodyConditionRecordService.cs` | Criação idempotente, LWW, pull |
| Application | `Application/Services/AnimalService.cs` | `CreateBodyConditionRecord` com `InitialBodyConditionSyncId` |
| Api | `Api/Controllers/BodyConditionRecordsController.cs` | Rota `GET ~/api/animals/body-condition-records/changes` |
| Tests | `MuuBoi.Tests/Services/BodyConditionRecordServiceTests.cs` | **Novo** |
| Tests | `MuuBoi.Tests/Services/AnimalServiceTests.cs` | Testes 17 e 18 |

**Não muda:** regras R1–R9, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `BodyConditionRecordProfile` (convenção), `AnimalCalvingService`, `AnimalRepository`, `AccountRepository`, rotas de tela (exceto os campos aditivos).

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Backfill incompleto (ECC com animal inexistente) deixaria linhas com `PropertyId = Guid.Empty`, invisíveis para todos | Consulta de 10/Out/2026: nenhum órfão (FK obrigatória para `Animals`). Validar zero `Guid.Empty` após a migração |
| Banco de produção com mais dados que o local | O `UPDATE` é um único comando por join, sem laço; a tabela é pequena (um registro por avaliação) |
| Filtro novo esconder ECC hoje visíveis | Só esconderia ECC de outra propriedade, que o service já não deixava acessar (o animal é carregado com filtro) |
| Casamento do ECC inicial pelo `lastBodyConditionRecord` falha no reenvio se outro celular registrou ECC mais recente (§3 item 10) | O `ServerId` chega no pull (§5 item 3). Correções do ECC inicial esperam até lá |
| ECC offline em animal que outro celular tirou do rebanho → `409` trava a fila (R2) | Conflito real, esperado (C8); o app oferece descartar |
| LWW por registro inteiro | Risco já aceito (Spec #14 §5.3) |
| `initialBodyConditionSyncId` repetido (erro do app) → `500` | Aceito, como na 14.2–14.5 (A4) |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **`notes` não é editável no `PATCH`** (o `BodyConditionRecordUpdateDto` só tem `score` e `recordedAt`) | ⏳ Fora desta spec (mudaria o contrato da edição). Avaliar junto com a divergência RN-04 da `spec-ecc.md` |
| Q2 | **Exclusão de ECC** (C2) | ⏳ Se o produtor precisar desfazer um lançamento, `DELETE` lógico idempotente (estrutura já pronta) |
| Q3 | **ECC do parto fora da transação do parto** (§3 item 9, Condição 1) | ⏳ Resolver na spec do parto: ECC pela navegação, no mesmo `SaveChanges`, e `bodyConditionSyncId` gerado pelo app |
