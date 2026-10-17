# Spec #14.7: Sincronização Offline — Lactação e Secagem

**Módulo:** Infraestrutura / Sincronização — Produção (Lactação)
**Versão:** 1.0
**Data:** 11/Out/2026
**Status:** ✅ **Implementada e validada** (11/Out/2026) — decisões L1–L10, Fases 1–8. Branch `feature/offline-lactations`. Regras de negócio mantidas (R1–R5, R7–R11); respostas a operações em lactação inativa alteradas (L6), mudanças de estado sem `UpdatedAt` (L7) e validação de datas corrigida nos três DTOs (§3 item 9, L9).
**Nota (16/Out/2026, Spec #14.11):** a lactação aberta pelo **parto** passou a nascer com o `syncId` vindo do app (`lactationSyncId`) e a ser gravada no mesmo `SaveChanges` do parto; a inativação do parto a inativa sem carimbar `UpdatedAt`.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 11/Out/2026 | Rascunho: lactação planejada **sem alterar nenhuma regra de negócio**, com mudanças de estado idempotentes (secar, desfazer secagem, inativar), a Condição 3 na invariante "uma lactação aberta por animal" e a lactação inicial endereçável (N2 da 14.4) |
| 0.2 | 11/Out/2026 | Decisões L1–L10 aprovadas conforme recomendado (L3, L6, L7 e L9 pela usuária, depois da explicação da L3). Branch criada a partir de `feature/offline-body-condition-records` (commit `1d1c431`) |
| 1.0 | 11/Out/2026 | Fases 1–8 implementadas e validadas; 203 testes passando (25 novos). §3 item 9 confirmado e corrigido nos três DTOs (antecipado para a Fase 2). Fluxo "secar antes do parto" da 14.4 validado. Spec #14 v1.6 (§13 questão 8 = Q3 desta spec), Spec #14.4, `catalogo-erros-api.md` §4.3/§4.11 e `spec-producao-11.2-lactacao-secagem.md` atualizados |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2), receita (§7.4) e Condições 1–3 (§7.5) · Spec #14.4 (`Animal` sincronizável; N1 grava a lactação inicial pela navegação; N2 reservou o `initialLactationSyncId`; pendência "secagem offline antes do parto") · Spec #14.3 (E1/E3 — referência ao derivado do cadastro na resposta; derivados como valor inicial) · Spec #14.2 (S2 — mudanças de estado idempotentes sem LWW). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-producao-11.2-lactacao-secagem.md` (D2–D5, D10, D11, D17, RN-01–RN-12) · `spec-gestacao-parto-6.2-parto.md` (parto abre a lactação) · `Docs/catalogo-erros-api.md` §4.11

> **Por que lactação agora:** é a última entidade "do animal" antes dos eventos (Spec #14 §8). Já é `ITenantEntity` com `HasQueryFilter` (não precisa de backfill, ao contrário do ECC) e tem uma FK sincronizável (`AnimalId`). E é pré-requisito prático: a Spec #14.4 registrou que uma vaca cadastrada com lactação inicial aberta **não pode parir** sem antes secar (RN-05) — sem a secagem offline, o fluxo "cadastrar vaca → secar → registrar parto" exige conexão.
>
> **O que a torna mais complexa que pesagem e ECC:** (1) **três mudanças de estado** (secar, desfazer secagem, inativar), todas hoje com `409` no estado repetido (viola A6); (2) uma **invariante** — no máximo uma lactação aberta por animal (RN-01) — checada entre o `SyncId` e o `INSERT` (**Condição 3**) e capaz de gerar conflito real entre celulares; (3) operações em lactação **inativa** hoje são `409`, ao contrário da Spec #14 §5.4; (4) o `AnimalDto` **não** traz as lactações, então o app não tem como saber o `Id` da lactação inicial pela resposta do cadastro (L3); (5) o `LactationDto` tem campos **derivados** (`isLactating`, `daysInMilk`) e um campo de **outra tabela** (`animalTagNumber`).
>
> **Sem cliente web:** o único cliente é o app (Spec #14 v1.2). Requisições sem `syncId`/`updatedAt` vêm só do Swagger/chamada direta.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/animals/{animalId}/lactations` (abertura manual, `InitialSeed`) com suporte offline | Lactação aberta pelo **parto** offline — spec do parto (Condição 1, §3 item 11) |
| `PATCH /api/lactations/{id}` (corrigir `startDate`) com LWW | Inativação da lactação pelo cancelamento do parto (RN-07) — continua no `AnimalCalvingService`, sem mudança |
| `POST` e `DELETE /api/lactations/{id}/dry-off`, `DELETE /api/lactations/{id}` idempotentes | Índice único "uma aberta por animal" no banco (Q2) |
| `GET /api/lactations/changes` (**nova**) | Marcador de data estimada (`StartDateEstimated`, D11 da 11.2) |
| `initialLactationSyncId` no `POST /api/animals` e referência à lactação inicial na resposta (L3) | Rotas de tela (`GET` de lista e `current`) — contrato inalterado |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/animals/{animalId}/lactations` | Sempre cria; sem `startDate` → grava `0001-01-01` (a confirmar) | Aceita `syncId`; se já existir → devolve a existente (`201`). Reenvio que cai em "já possui lactação em aberto" por causa da **própria** original → `201` (Condição 3) |
| `PATCH /api/lactations/{id}` | Aplica e carimba `UpdatedAt = agora`; inativa → `409` | Aceita `updatedAt`; aplica só se não for mais antiga (LWW). Inativa → `200` com o estado do servidor (L6) |
| `POST /api/lactations/{id}/dry-off` | Já seca → `409`; inativa → `409` | Já seca → `200` com o estado atual, sem gravar; inativa → `200` (L6) |
| `DELETE /api/lactations/{id}/dry-off` | Já aberta → `409`; inativa → `409` | Já aberta → `200`, sem gravar; inativa → `200` (L6). "Animal já possui **outra** lactação em aberto" continua `409` |
| `DELETE /api/lactations/{id}` | Já inativa → `409` | Já inativa → `204`, sem gravar |
| `GET /api/lactations/changes?since=&limit=` | — | **Nova** (L10) |
| `POST /api/animals` | Lactação inicial recebe `SyncId` do banco; resposta sem a lactação | Aceita `initialLactationSyncId`; resposta ganha `initialLactation { id, syncId }` (L3) |
| `GET /api/lactations/{id}`, `GET .../current` e respostas de `POST`/`PATCH`/`dry-off` | — | Ganham `syncId` (aditivo) |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.6): nenhuma regra de negócio é alterada.** As mudanças de comportamento são as exigidas pela sincronização (estado repetido → `2xx`, A6; Condição 3), a resposta a operações em lactação inativa (L6, ⭐) e a correção de validações que deixam passar dados inválidos (L9, ⭐, se confirmadas).

| # | Regra (código atual) | Onde | Comportamento |
|---|---|---|---|
| R1 | Animal precisa existir | `POST`, `GET` por animal | `404` "Animal com id '{animalId}' não encontrado." — mantido |
| R2 | Animal precisa estar **ativo** para abrir lactação | `POST` | `409` "Não é possível cadastrar lactação para um animal inativo." — mantido |
| R3 | Só vacas e novilhas têm lactação | `POST` (`422`), cadastro do animal com `initialLactation` (`422`, RN-11) | Mantido |
| R4 | **Invariante RN-01:** no máximo uma lactação aberta e ativa por animal | `POST` (`409` "O animal já possui uma lactação em aberto…"), desfazer secagem (`409` "…outra lactação em aberto."), parto (`422`, RN-05) | Mantido. No `POST`, recheca o `SyncId` antes do `409` (Condição 3, L2) |
| R5 | Lactação precisa existir | `GET /{id}`, `PATCH`, `dry-off`, `DELETE` | `404` "Lactação com id '{id}' não encontrada." — mantido |
| R6 | Operações em lactação **inativa**: editar, secar, reabrir | `PATCH`, `POST`/`DELETE /dry-off` | Hoje `409`. **Muda para `200` com o estado do servidor, sem gravar** (L6, ⭐) |
| R7 | Datas não futuras: início (DTO, `400`), secagem (DTO, `400`) | `POST`, `PATCH`, `dry-off`, cadastro do animal | Mantido |
| R8 | `startDate` não posterior à secagem (RN-09); secagem não anterior ao início (RN-04) | `PATCH` (`422`), `dry-off` (`422`), cadastro (`400`) | Mantido |
| R9 | Desfazer a secagem limpa `EndDate` **e** `DryOffNotes` | `DELETE /dry-off` | Mantido |
| R10 | Cancelar o parto inativa a lactação vinculada (RN-07) | `AnimalCalvingService.InactivateAsync` | Mantido (fora do escopo) |
| R11 | `isLactating` e `daysInMilk` são derivados, nunca persistidos (RN-08) | `LactationDto` | Mantido; no pull são **valor inicial** (L8) |

**Ordem entre `SyncId` e regras no `POST`:** `SyncId` antes de R1–R4. Reenvio de uma abertura já gravada → `201` com a existente, mesmo que outro celular tenha registrado a saída do animal depois (R2) ou que a própria lactação esteja aberta (R4 — Condição 3).

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `Lactation : BaseEntity, ITenantEntity` — já tem `PropertyId`, `HasQueryFilter` e índices de tenant | Basta `ISyncable`; **sem backfill** | Receita §7.4 (Fase 1) |
| 2 | `CreateAsync` checa R4 (`HasOpenByAnimalIdAsync`) depois de carregar o animal | **Condição 3:** se o reenvio passar pela checagem do `SyncId` antes de o original gravar e chegar a R4 depois, recebe `409` por causa da **própria** lactação | Rechecar o `SyncId` antes do `409` de R4 (L2), como o brinco na 14.4 |
| 3 | `DryOffAsync`: "Esta lactação já está seca." (`409`) | Viola A6: reenvio de uma secagem que deu certo trava a fila | `200` com o estado atual, sem gravar (L5) |
| 4 | `UndoDryOffAsync`: "Esta lactação já está em aberto." (`409`) | Viola A6 | `200`, sem gravar (L5) |
| 5 | `DeactivateAsync`: "A lactação já está inativa." (`409`) | Viola A6 | `204`, sem gravar (L5) |
| 6 | `UpdateAsync`, `DryOffAsync`, `UndoDryOffAsync` → `409` se a lactação estiver inativa | Contraria a Spec #14 §5.4 ("inativação sempre vence"; `PATCH` em inativo não é erro). Caso real: o produtor cancela o parto num celular (R10) e seca a vaca offline no outro → `409`, fila travada, e não há o que o usuário corrigir | L6 |
| 7 | `UpdateAsync`, `DryOffAsync`, `UndoDryOffAsync` e `DeactivateAsync` carimbam `UpdatedAt = DateTime.UtcNow` (hora do **servidor**) | **LWW perde edição do próprio celular:** offline, o usuário seca a vaca às 10:00 e corrige o `startDate` às 10:05; no sync (18:00) a secagem carimba `UpdatedAt = 18:00`, e o `PATCH` (editado às 10:05) é descartado por ser "mais antigo". O mesmo acontece hoje com saída/reativação do animal (14.4) e da amostra de sêmen (14.2) — §9 Q3 | L7 |
| 8 | `UpdateAsync` atribui campo a campo (só `StartDate`), sem AutoMapper | Sem risco do bug do `ForAllMembers` | LWW no service (L4) |
| 9 | **Prováveis bugs pré-existentes:** `LactationCreateDto.StartDate`, `LactationDryOffDto.EndDate` e `LactationSeedDto.StartDate` são `DateTime` (não anuláveis) com `[Required]` — o mesmo padrão do ECC (14.6 §3 item 6) | `POST` sem `startDate` grava `0001-01-01` (não é futura); cadastro do animal com `initialLactation: {}` idem; `dry-off` sem `endDate` cai em "anterior ao início" (`422`, mensagem errada) | **Confirmado** (§6.2, primeira linha): os três casos. Tratamento: L9 |
| 10 | O `AnimalDto` **não** traz lactações (só `productiveStatus`/`daysInMilk` derivados); a lactação inicial (N1 da 14.4) recebe `SyncId` do banco | **Condição 2:** o app não tem o `Id` da lactação inicial depois do cadastro. Um "secar" offline logo depois do cadastro (o caso da pendência da 14.4) não pode ser montado até o próximo pull — e o pull só roda depois de a fila esvaziar (Spec #14 §6 item 9), então a fila **travaria** | L3 |
| 11 | O parto cria a lactação com `_lactationRepository.CreateAsync` **depois** de gravar o parto (outro `SaveChanges`) | Condição 1 — problema **do parto** | Fora (spec do parto). Aqui a lactação do parto só recebe `SyncId` do servidor e chega pelo pull |
| 12 | `LactationDto` tem `isLactating`/`daysInMilk` (calculados com "agora") e `animalTagNumber` (navegação `Animal`, carregada pelo `Include` do `GetByIdAsync`) | No pull, o helper `GetChangesSinceAsync` não faz `Include` → `animalTagNumber` viria `null`; os derivados mudam com o passar dos dias sem o `RowVersion` mudar | L8 |
| 13 | `LactationDto` não tem `syncId` (já tem `animalId`, `isActive`, `createdAt`, `updatedAt`) | — | Campo aditivo (L8) |
| 14 | Não há índice único "uma aberta por animal"; a invariante R4 é só consulta | Dois celulares abrindo lactação para o mesmo animal ao mesmo tempo poderiam passar juntos | Sem mudança (fila única por celular torna raro; o segundo a chegar normalmente recebe `409`). Q2 |
| 15 | Banco local: 21 lactações (11 de parto, 2 inativas, 12 abertas), nenhum animal com mais de uma aberta | — | Backfill de `SyncId` pela migração |

## 4. Decisões (aprovadas em 11/Out/2026)

As marcadas com ⭐ pediam escolha da usuária (aprovadas conforme a recomendação); as demais seguem precedentes das specs 14.1–14.6.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| L1 | **`ISyncable` na `Lactation`** + `ConfigureSyncable()`; migração só com `SyncId`/`RowVersion` e índices (a tabela já tem `PropertyId`) | — |
| L2 | **Abertura idempotente com Condição 3:** `SyncId` antes de R1–R4; antes de lançar o `409` de R4, buscar o `SyncId` de novo e, se existir, devolver a existente (mesmo código da 14.4 §6.2) | Só a checagem inicial do `SyncId` — deixaria a corrida da Condição 3 aberta |
| L3 ⭐ | **Lactação inicial endereçável, com referência na resposta do cadastro** (E1 da 14.3): `AnimalCreateDto.InitialLactationSyncId` (`Guid?`; ausente → servidor gera; sem `initialLactation` → ignorado; `Guid.Empty` → `400`). A resposta do `POST /api/animals` passa a ser `AnimalCreatedDto : AnimalDto` com `LactationRefDto? InitialLactation` (`Id`, `SyncId`). No reenvio, a referência é buscada pelo `initialLactationSyncId` **desde que pertença ao animal**. Com isso o app monta "secar" logo depois do cadastro, sem esperar o pull (§3 item 10) | (a) Só o `initialLactationSyncId`, sem referência na resposta (como pesagem e ECC, que vêm no `AnimalDto`) — o "secar" offline depois do cadastro travaria a fila até um pull que só roda com a fila vazia. (b) Incluir a lista de lactações no `AnimalDto` — muda a resposta do detalhe/lista e duplica o `GET /lactations` |
| L4 | **`PATCH`: R5 → LWW → (L6) → R8.** Uma edição mais antiga é descartada antes de checar estado e datas, para não virar erro definitivo (N4 da 14.4). `UpdatedAt = editedAt` | R8 antes do LWW — uma edição antiga com data inconsistente viraria `422` em vez de ser ignorada |
| L5 | **Mudanças de estado idempotentes sem LWW** (S2 da 14.2, Spec #14 §5.4): secar já seca → `200` com o estado atual (inclusive se outro celular secou com outra data — efeito colateral aceito, Spec #14 §3.3); desfazer secagem em lactação já aberta → `200`; inativar já inativa → `204`. **Nenhuma grava** no estado repetido (`UpdatedAt`/`RowVersion` intactos). Desfazer secagem com **outra** lactação aberta continua `409` (R4, conflito real) | Secagem com LWW pelo `updatedAt` — regra diferente da §5.4 para mudanças de estado |
| L6 ⭐ | **Operações em lactação inativa → `200` com o estado do servidor, sem gravar** (`PATCH`, secar, desfazer secagem). Aplica a Spec #14 §5.4 ("a inativação sempre vence"): a lactação foi excluída (manualmente ou pelo cancelamento do parto, R10), e não há o que o usuário corrigir no item da fila. O app sobrescreve o local com a resposta (`isActive: false`) | (a) Manter `409` — conflito "real" sem ação possível: o item vira `Failed`, trava a fila e o usuário só pode descartar. (b) Aplicar a operação na inativa (como o `PATCH` da pesagem, P2 da 14.5) — secar/reabrir uma lactação excluída é regra nova |
| L7 ⭐ | **Mudanças de estado não alteram `UpdatedAt`** (secar, desfazer secagem, inativar). O `UpdatedAt` passa a ser só o "momento da última edição" do LWW (`startDate`), como a Spec #14 §5.3 define; o `RowVersion` continua avançando, então o pull traz a mudança normalmente. Resolve o §3 item 7 sem mudar contrato. A inativação pelo cancelamento do parto (R10) fica como está (fora do escopo) | (a) Manter o carimbo com a hora do servidor — o celular perde as próprias correções feitas depois de uma secagem offline. (b) Aceitar `updatedAt` também nas mudanças de estado — o `DELETE /dry-off` e o `DELETE` não têm corpo (exigiria query string ou mudar a rota) |
| L8 | **Pull com o mesmo `LactationDto`** (+ `syncId`): `isLactating`/`daysInMilk` calculados no momento do pull pelo `ProductiveStatusResolver` (função pura, sem consulta) e tratados pelo app como **valor inicial** — o app **deve** recalcular com as mesmas regras (DEL muda todo dia; E3 da 14.3, N6 da 14.4). `animalTagNumber` preenchido por **uma consulta em lote** dos animais da página (E9 da 14.3): `IAnimalRepository.GetTagNumbersByIdsAsync(ids)`; o app exibe o brinco pelo animal local | `animalTagNumber: null` no pull — quebra o "mesmo DTO do detalhe" (Spec #14 §5.5). DTO próprio de sync — sem motivo |
| L9 ⭐ | **Datas obrigatórias de fato → `400`** (se a Fase 2 confirmar o §3 item 9): `LactationCreateDto.StartDate`, `LactationDryOffDto.EndDate` e `LactationSeedDto.StartDate` passam a `DateTime?` com `[Required]` (o service usa `.Value`). Correção de bug, não regra nova: RN-02, RN-04 e RN-12 já dizem que são obrigatórias (mesma escolha da C7 da 14.6) | Manter — a lactação com início `0001-01-01` sincronizaria e seria "aberta há 2000 anos" em todos os celulares |
| L10 | **Pull em `GET /api/lactations/changes`** — a lactação já tem raiz própria (`/api/lactations/{id}`), sem colisão com `{id:int}`. Traz as lactações de **todos** os animais da propriedade, inclusive inativas | `GET /api/animals/lactations/changes` (padrão de pesagem/ECC) — essas não têm rota própria fora do animal; a lactação tem |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem e ids:** a lactação de um animal criado offline vai **depois** do `POST` do animal; `animalId` = `ServerId` do animal. Secar, desfazer secagem, corrigir e excluir usam o `ServerId` da lactação, resolvido na hora do envio (A2, A5).
2. **Lactação inicial (L3):** um único item na fila — `POST /api/animals` com `initialLactation` e `initialLactationSyncId`. No `201`, o app grava o `ServerId` da lactação a partir de `initialLactation.id`. Uma secagem registrada logo depois do cadastro vai para a fila **depois** do `POST` do animal e usa esse `Id`.
3. **Secagem antes do parto (pendência da 14.4):** com a L3 e a L5, o fluxo "cadastrar vaca com lactação aberta → secar" funciona offline. O **parto** continua exigindo conexão até a spec do parto; enviado depois da secagem na mesma fila, ele não cai mais no `422` de RN-05.
4. **Datas:** `startDate` e `endDate` sempre enviadas (UTC, com `Z`), com o momento real (não o do envio).
5. **Estado repetido (L5) e lactação inativa (L6):** `200` com o estado do servidor — o app sobrescreve o local. Se o servidor mostrar a lactação seca com outra data, ou inativa, vale o do servidor.
6. **Conflitos reais (`409`):** abrir lactação com outra aberta (outro celular abriu antes, ou o parto abriu); desfazer secagem com outra aberta; abrir em animal inativo. O item vira `Failed` (A3); o app mostra o motivo e oferece descartar.
7. **Pull (`GET /api/lactations/changes`):** upsert por `syncId`, ligado ao animal pelo `animalId`; puxar **animais antes de lactações**. `isLactating`, `daysInMilk` e o status produtivo do animal **devem** ser recalculados localmente com as regras do `ProductiveStatusResolver` (intervalo fechado `[início, secagem]`; DEL congelado na secagem). O brinco vem do animal local.
8. **Lactação do parto** chega pelo pull com `syncId` gerado pelo servidor; pode ser secada e corrigida offline.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (L1) | ✅ Migração `20261011164733_Offline_Lactation_SyncId_RowVersion` aplicada |
| 2 — Abertura idempotente (+ Condição 3, confirmação do §3 item 9 e L9) | ✅ Implementada e validada (§3 item 9 confirmado e corrigido nos três DTOs) |
| 3 — Lactação inicial endereçável (L3) | ✅ Implementada e validada (inclui o fluxo "secar antes do parto" da 14.4) |
| 4 — Edição com last-write-wins (L4, L6, L7) | ✅ Implementada e validada (cenário da L7 validado na Fase 5) |
| 5 — Secagem, desfazer secagem e inativação idempotentes (L5, L6, L7) | ✅ Implementada e validada |
| 6 — Pull incremental (L8, L10) | ✅ Implementada e validada |
| 7 — Testes | ✅ 22 testes de `LactationService` + 3 em `AnimalServiceTests`; 203 no projeto |
| 8 — Documentação | ✅ Spec #14 (v1.6), Spec #14.4, `catalogo-erros-api.md` §4.3/§4.11, `spec-producao-11.2-lactacao-secagem.md` |

Branch: `feature/offline-lactations`, criada a partir de `feature/offline-body-condition-records` (commit `1d1c431`; ou do `dev`, depois do merge do ECC), para que a migração fique em cima da `Offline_BodyConditionRecord_PropertyId_SyncId_RowVersion`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com as contas de teste da 14.6.

### 6.1 Fase 1 — Domínio e banco

- `Lactation : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `ApplicationDbContext`: `builder.Entity<Lactation>().ConfigureSyncable();` (índices existentes permanecem).
- Migração `Offline_Lactation_SyncId_RowVersion` (⚠️ **requer aprovação**). SQL esperado:

```sql
ALTER TABLE [Lactations] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [Lactations] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_Lactations_PropertyId_RowVersion] ON [Lactations] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_Lactations_SyncId] ON [Lactations] ([SyncId]);
```

| Validação (11/Out/2026) | Resultado |
|---|---|
| SQL gerado | ✅ Idêntico ao esperado; snapshot só muda em `Lactation` (`RowVersion`, `SyncId` e os dois índices) |
| Lactações existentes | ✅ 21, com 21 `SyncId` distintos, nenhum vazio; `RowVersion` preenchido |
| Índices | ✅ Os 5 anteriores + `IX_Lactations_PropertyId_RowVersion` e `UX_Lactations_SyncId` |
| Cadastro de vaca com lactação inicial (animal 126 → lactação 22), `POST` de lactação numa novilha (127 → 23) e parto (vaca 128 → 24) | ✅ Os três com `SyncId` preenchido pelo banco |
| Testes existentes | ✅ 178/178 |

- Registros de teste (conta `teste.offline.pesagem@muuboi.local`): vaca 126 (`514030`) com a lactação inicial 22 aberta — usada no fluxo "secar antes do parto" da Fase 3; novilha 127 (lactação 23); vaca 128 com gestação, parto e lactação 24.

### 6.2 Fase 2 — Abertura idempotente

- **Antes de mudar o código:** confirmar o §3 item 9 (`POST` de lactação sem `startDate`; cadastro de vaca com `initialLactation: {}`; `dry-off` sem `endDate`).
- `LactationCreateDto`: `Guid? SyncId` + validação de `Guid.Empty`; L9 se confirmado (`DateTime? StartDate` com `[Required]`; a checagem de data futura passa a usar `.HasValue`).
- `ILactationRepository`/`LactationRepository`: `GetBySyncIdAsync` (com `Include(Animal)`, para o `animalTagNumber` da resposta); `CreateAsync` → `AddSyncableAsync`.
- `LactationService.CreateAsync` (L2):

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return ToDto(existing);
}

var animal = await FindAnimalAsync(animalId);

if (!animal.IsActive)
    throw new ConflictException("Não é possível cadastrar lactação para um animal inativo.");

EnsureLactatingClassification(animal);

if (await _repository.HasOpenByAnimalIdAsync(animalId))
{
    if (dto.SyncId.HasValue)
    {
        var concurrent = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
        if (concurrent != null)
            return ToDto(concurrent);
    }

    throw new ConflictException("O animal já possui uma lactação em aberto. Seque a atual antes de abrir outra.");
}

var lactation = new Lactation
{
    SyncId = dto.SyncId ?? Guid.NewGuid(),
    // demais campos como hoje
};
```

| Validação (11/Out/2026) | Resultado |
|---|---|
| §3 item 9, **antes** da correção (11/Out/2026, feito ao fim da Fase 1) | ⚠️ **Confirmado nos três DTOs:** `POST` de lactação com `{}` (novilha 130) → `201`, lactação 25 com `startDate: 0001-01-01` e `daysInMilk: 739899`; cadastro de vaca com `initialLactation: {}` (vaca 131) → `201`, lactação 26 com `0001-01-01`; `dry-off` sem `endDate` → `422` "A data da secagem não pode ser anterior ao início da lactação." (mensagem errada). As lactações 25 e 26 foram inativadas (`DELETE`) para não deixar os dois animais "em lactação" |
| `POST` com `syncId` (novilha 130) / repetido / payload diferente (outra data) | ✅ `201`, lactação 27 com o `SyncId` do app; reenvios `201` com a 27, 1 linha, data original mantida. O reenvio já encontra a própria 27 aberta (R4) e devolve `201` — o `SyncId` vem antes |
| `POST` sem `syncId` (vaca 131) | ✅ `201`, lactação 28, `SyncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." (formato B) |
| R4: `POST` novo (outro `syncId`) na 130, que já tem a 27 aberta | ✅ `409` "O animal já possui uma lactação em aberto…" (mantido) |
| R3: macho (animal 111) / R2: novilha inativa (132, saída por venda) | ✅ `422` "Somente vacas e novilhas podem ter lactação." / `409` "Não é possível cadastrar lactação para um animal inativo." (mantidos) |
| Saída da novilha 130 → reenvio da 27 | ✅ `201` com a 27 — `SyncId` antes de R2 |
| L9: `POST` com `{}` / cadastro com `initialLactation: {}` / `dry-off` com `{}` | ✅ `400` "A data de início da lactação é obrigatória." (campo `StartDate`) / mesma mensagem (campo `InitialLactation.StartDate`) / "A data da secagem é obrigatória." (campo `EndDate`) |
| L9 sem regressão: cadastro com `initialLactation` fechada (início 01/05, secagem 01/09) / `dry-off` da lactação 23 com data | ✅ `201`, lactação 29 com as duas datas / `200`, `endDate` aplicada |
| 20 envios simultâneos (mesmo `syncId`, novilha 134) | ✅ Todos `201`; 1 linha (lactação 30), uma única lactação no animal; **nenhum** `409` de R4 |
| Testes existentes | ✅ 178/178 |

- **Corrida exercitada:** 15 das 20 requisições passaram juntas pela checagem do `SyncId` e chegaram ao `INSERT`; o banco barrou as 15 em `UX_Lactations_SyncId` (identity pulou de 30 para 45) e o `AddSyncableAsync` devolveu a existente. As outras 4 foram resolvidas antes do `INSERT` — pela checagem inicial ou pela recheca da Condição 3 (o log não distingue). Nenhuma recebeu o `409` de R4.
- A correção do `LactationDryOffDto` e do `LactationSeedDto` (L9) foi antecipada das Fases 5 e 3 para esta fase, já que o bug foi confirmado nos três DTOs ao mesmo tempo.
- Registros de teste: lactações 27 (novilha 130, agora inativa), 28 (vaca 131), 29 (vaca 133 `514037`, fechada), 30 (novilha 134 `514038`); lactação 23 (novilha 127) agora seca; novilha 132 inativa sem lactação.

### 6.3 Fase 3 — Lactação inicial endereçável

- `AnimalCreateDto`: `Guid? InitialLactationSyncId` + validação ("O identificador de sincronização da lactação inicial não pode ser vazio.").
- `LactationRefDto` (`Id`, `SyncId`) e `AnimalCreatedDto : AnimalDto` com `LactationRefDto? InitialLactation` (**novos**, padrão `StockItemCreatedDto`/`StockMovementRefDto`).
- `AnimalService.CreateInitialLactation`: `SyncId = dto.InitialLactationSyncId ?? Guid.NewGuid()`.
- `IAnimalService.CreateAnimalAsync` passa a devolver `AnimalCreatedDto`; o service preenche `InitialLactation` com a lactação criada (ou, no reenvio, com a encontrada por `GetBySyncIdAsync(initialLactationSyncId)` **se** `AnimalId == animal.Id`). `AnimalsController.Create` sem outra mudança.

| Validação (11/Out/2026) | Resultado |
|---|---|
| Cadastro de vaca (135, `514040`) com `initialLactation` aberta e `initialLactationSyncId` | ✅ `201`; `initialLactation: { id: 46, syncId: "…0b02" }` (o `syncId` do app) |
| Reenvio do cadastro | ✅ `201`, mesmo animal, mesma `initialLactation` (46); 1 lactação no animal |
| Reenvio sem `initialLactationSyncId` | ✅ `201`, `initialLactation: null`; nada duplicado |
| Sem `initialLactation`, com `initialLactationSyncId` (vaca 136) / macho sem lactação | ✅ `201`, `initialLactation: null`, sem erro |
| `initialLactationSyncId` vazio | ✅ `400` "O identificador de sincronização da lactação inicial não pode ser vazio." (formato B, campo `InitialLactationSyncId`) |
| **Fluxo da pendência da 14.4:** vaca 135 → `POST /api/lactations/46/dry-off` (com o `id` da resposta) → gestação → parto | ✅ `201` → `200` (`isLactating: false`) → `201` (parto 15). A vaca fica com a lactação 46 seca e a 47 aberta pelo parto |
| Controle: vaca 126 (lactação 22 aberta, sem secar) → parto | ✅ `422` "O animal possui uma lactação em aberto. Registre a secagem antes de lançar um novo parto." (RN-05 mantida) |
| Testes | ✅ 181/181 (testes 23–25 da §6.7 incluídos) |

- Observação de formato: no JSON do `AnimalCreatedDto`, o `initialLactation` sai **antes** do `id` (o serializador escreve primeiro as propriedades da classe derivada). Não muda nada para o app (JSON por nome), só para quem lê a resposta à mão.
- Registros de teste: vaca 135 (lactações 46 e 47, gestação 22, parto 15), vaca 136 (sem lactação), boi 137; vaca 126 ganhou uma gestação confirmada (sem parto).

### 6.4 Fase 4 — Edição com last-write-wins

- `LactationUpdateDto`: `DateTime? UpdatedAt`.
- `LactationService.UpdateAsync` (L4, L6):

```csharp
var lactation = await _repository.GetByIdAsync(id)
    ?? throw new NotFoundException($"Lactação com id '{id}' não encontrada.");

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (!lactation.IsActive || SyncTimestampResolver.IsOutdated(editedAt, lactation))
    return ToDto(lactation);

if (dto.StartDate.HasValue)
{
    if (lactation.EndDate.HasValue && dto.StartDate.Value > lactation.EndDate.Value)
        throw new BusinessRuleException("A data de início não pode ser posterior à data da secagem.");

    lactation.StartDate = dto.StartDate.Value;
}

lactation.UpdatedAt = editedAt;
```

| Validação (11/Out/2026) | Resultado |
|---|---|
| `updatedAt` mais novo na lactação 30 (novilha 134, nunca editada) | ✅ Aplicado; `updatedAt` = horário do cliente (`17:02:11Z`) — comparado com o `CreatedAt` |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| `updatedAt` com fuso `-03:00` (`14:03:11-03:00`) | ✅ Aplicado; gravado como `17:03:11Z` |
| Edição mais antiga (−1 h, início 01/01) | ✅ `200`, ignorada; início 12/09 mantido; `RowVersion` inalterado |
| Sem `updatedAt` (Swagger/chamada direta) / relógio adiantado (+1 dia) | ✅ Aplicado com "agora" / limitado a "agora" |
| L6: `PATCH` na lactação 25 (inativa) | ✅ `200`, `isActive: false`; nada gravado (`UpdatedAt` e `RowVersion` inalterados). Antes: `409` "Não é possível editar uma lactação inativa." |
| R8: início (01/09) depois da secagem (01/08) na lactação 46 | ✅ `422` "A data de início não pode ser posterior à data da secagem." (mantido) |
| L4: a mesma edição, mas **mais antiga** que a versão do servidor | ✅ `200`, ignorada **sem** checar R8 — não vira erro definitivo |
| R5: lactação inexistente | ✅ `404` "Lactação com id '999999' não encontrada." |
| **§3 item 7** (secar e depois corrigir o início) | Validado na Fase 5, depois da L7 |
| Testes existentes | ✅ 181/181 |

- Como nas specs anteriores, o reenvio idêntico (empate, Spec #14 §5.3) regrava os mesmos valores e o `RowVersion` avança (38673 → 38674) — a lactação reaparece uma vez no pull, sem mudança de conteúdo.
- Registro de teste: lactação 30 termina com início 15/09.

### 6.5 Fase 5 — Secagem, desfazer secagem e inativação idempotentes

- `LactationService.DryOffAsync`: `if (!lactation.IsActive || lactation.EndDate.HasValue) return ToDto(lactation);` antes de R8; **sem** `UpdatedAt` (L7).
- `UndoDryOffAsync`: `if (!lactation.IsActive || lactation.EndDate == null) return ToDto(lactation);` antes de R4 ("outra aberta", mantido `409`); **sem** `UpdatedAt`.
- `DeactivateAsync`: `if (!lactation.IsActive) return true;`; **sem** `UpdatedAt`.
- L9 no `LactationDryOffDto` (se confirmado): `DateTime? EndDate` com `[Required]`.

| Validação (11/Out/2026) | Resultado |
|---|---|
| Secar a lactação 30 (05/10, "primeira") | ✅ `200`; `EndDate` e observação gravadas; `RowVersion` 38677 → 38678; **`UpdatedAt` inalterado** (`17:04:13`, L7) |
| Secar de novo — mesma data / outra data ("segunda", 08/10) | ✅ `200` → `200`, as duas com a **primeira** secagem; `RowVersion` (38678) e `UpdatedAt` inalterados |
| **§3 item 7 / L7:** depois da secagem (feita "no sync"), `PATCH` do início com `updatedAt` 30 s antes do momento da secagem | ✅ **Aplicado** (início 15/09 → 20/09; `updatedAt` = `17:06:39`). Com o carimbo antigo (`UpdatedAt` = hora do servidor na secagem), essa edição seria descartada |
| Desfazer a secagem → desfazer de novo | ✅ `200` (`endDate: null`, observação limpa — R9) → `200`, sem gravar (`RowVersion` 38680 nos dois) |
| R8: secagem antes do início | ✅ `422` "A data da secagem não pode ser anterior ao início da lactação." (mantido) |
| R4: desfazer a secagem da 46 com a 47 aberta (vaca 135) | ✅ `409` "O animal já possui outra lactação em aberto." (mantido) |
| Inativar a 28 → inativar de novo | ✅ `204` (`IsActive=0`, `RowVersion` 38640 → 38682, `UpdatedAt` continua `NULL`) → `204`, sem gravar. Antes: `409` "A lactação já está inativa." |
| L6: secar / desfazer secagem na 28 (inativa) | ✅ `200`, `isActive: false`; nada gravado (`RowVersion` 38682). Antes: `409` |
| **Caso do §3 item 6:** cancelar o parto 15 (vaca 135) → secar a lactação 47 (inativada pelo cancelamento, R10) | ✅ Cancelamento `204`, lactação 47 inativa; secagem `200` com `isActive: false`, nada gravado |
| R5: lactação inexistente | ✅ `404` |
| Testes existentes | ✅ 181/181 |

- O cancelamento do parto (R10, fora do escopo) continua carimbando `UpdatedAt` na lactação que inativa (`17:07:11`), como previsto na L7.
- Registros de teste: lactação 30 aberta (início 20/09), 28 inativa, 46 seca e 47 inativa (vaca 135, parto 15 cancelado — a cria do parto também foi inativada).

### 6.6 Fase 6 — Pull incremental

- `LactationDto`: `Guid SyncId` (L8). Os mapas do `LactationProfile` são por convenção.
- `IAnimalRepository.GetTagNumbersByIdsAsync(IReadOnlyCollection<int> ids)` → `Dictionary<int, string?>` (sem filtro de `IsActive`; o tenant vem do `HasQueryFilter`).
- `ILactationRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<Lactation>`.
- `ILactationService.GetChangesAsync(string? since, int? limit)`: cursor/limite como nas anteriores; preenche `animalTagNumber` pelo dicionário e os derivados pelo `ToDto`.
- `LactationsController`: `[HttpGet("api/lactations/changes")]` (L10).

| Validação (11/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 14 lactações (4 inativas: 25, 26, 28, 47; 5 abertas por parto), todas com `syncId` e `animalTagNumber`, e `isLactating`/`daysInMilk` calculados (ex.: lactação 30 — `daysInMilk: 21`); `nextCursor` `38684`, `hasMore: false` |
| Pull × `GET /api/lactations/{id}`, item a item (JSON completo) | ✅ **Nenhuma divergência** — inclusive `animalTagNumber` (lote) e os derivados |
| Isolamento (duas contas) | ✅ A tabela tinha 35 lactações; a conta A vê as suas 14; a conta B cadastra uma vaca com lactação inicial e vê **só** a sua (48); nenhuma vê a da outra |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido |
| Abre A (49), corrige o início, seca, desfaz a secagem; abre B (50) e inativa B → pull | ✅ A (início 02/09, aberta, ativa) e B (inativa), cada uma **uma vez**, com o estado final |
| Editar o animal → pull de lactações | ✅ `items: []` |
| Paginação `limit=1` | ✅ 16 páginas, 16 itens distintos, mesma ordem do pull completo |
| `since=abc` / `since=-5` / `limit=0` / `limit=100000` | ✅ `400` / `400` / `200` / `200` |
| `GET /api/lactations/origins`, `/api/lactations/30` e `/api/animals/changes` | ✅ `200`, sem colisão |
| Consultas por página (log do EF) | ✅ **2** com 1 item e **2** com a página completa (pull + brincos) |
| Testes existentes | ✅ 181/181 |

- Registros de teste: novilhas 140 (`514044`, lactação 49) e 141 (`514045`, lactação 50 inativa); conta B — vaca 139 (`524002`, lactação 48).

### 6.7 Fase 7 — Testes

`MuuBoi.Tests/Services/LactationServiceTests.cs` (**novo** — hoje não há testes de lactação).

| # | Teste |
|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesLactationWithGivenSyncId` |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAsync_WithExistingSyncId_DoesNotLoadAnimal` |
| 4 | `CreateAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAsync_WhenOpenLactationIsSameSyncId_ReturnsExisting` (Condição 3) |
| 6 | `CreateAsync_WhenAnotherLactationIsOpen_ThrowsConflictException` (R4) |
| 7 | `CreateAsync_WhenAnimalInactive_ThrowsConflictException` (R2) |
| 8 | `CreateAsync_WhenAnimalIsMale_ThrowsBusinessRuleException` (R3) |
| 9 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 10 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 11 | `UpdateAsync_WhenInactive_ReturnsWithoutUpdating` (L6) |
| 12 | `UpdateAsync_WithStartDateAfterEndDate_ThrowsBusinessRuleException` (R8) |
| 13 | `DryOffAsync_WhenOpen_ClosesWithoutChangingUpdatedAt` (L7) |
| 14 | `DryOffAsync_WhenAlreadyDry_ReturnsWithoutUpdating` (L5) |
| 15 | `DryOffAsync_WhenInactive_ReturnsWithoutUpdating` (L6) |
| 16 | `DryOffAsync_WithEndDateBeforeStartDate_ThrowsBusinessRuleException` (R8) |
| 17 | `UndoDryOffAsync_WhenAlreadyOpen_ReturnsWithoutUpdating` (L5) |
| 18 | `UndoDryOffAsync_WhenAnotherLactationIsOpen_ThrowsConflictException` (R4) |
| 19 | `DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` (L5) |
| 20 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 21 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 22 | `GetChangesAsync_FillsAnimalTagNumberForPageItems` (L8) |

Em `AnimalServiceTests`:

| # | Teste |
|---|---|
| 23 | `CreateAnimalAsync_WithInitialLactationSyncId_ReturnsInitialLactationReference` (L3) |
| 24 | `CreateAnimalAsync_WithExistingSyncIdAndInitialLactationSyncId_ReturnsSameReference` (L3) |
| 25 | `CreateAnimalAsync_WithoutInitialLactation_ReturnsNullReference` (L3) |

- Os testes 6, 7, 8, 12, 16 e 18 fixam regras que já existem e hoje não têm cobertura (não havia `LactationServiceTests`).
- O teste 5 simula a corrida da Condição 3: a primeira busca pelo `SyncId` não acha nada, R4 encontra a lactação aberta e a segunda busca encontra a original.
- Fora dos testes de service: a L9 (validação dos DTOs) e a referência no `AnimalCreatedDto` vista de ponta a ponta — cobertas pela validação manual das Fases 2 e 3.
- **Resultado (11/Out/2026):** 22/22 em `LactationServiceTests` na primeira execução; 23–25 em `AnimalServiceTests` (Fase 3); projeto inteiro **203/203** (178 anteriores + 25 novos).

### 6.8 Fase 8 — Documentação

- **Esta spec:** status das fases e resultados.
- **Spec #14:** §8 (`Lactation` ✅), cabeçalho, histórico, sumário; §13 com a Q3 desta spec (carimbo de `UpdatedAt` nas mudanças de estado de animal e sêmen).
- **Spec #14.4:** §5 item 3 (só a lactação faltava) e a pendência "secagem offline antes do parto" resolvidas.
- **`catalogo-erros-api.md` §4.11:** remover os `409` de estado repetido e de lactação inativa (se L6); acrescentar `400` de `syncId` vazio, do cursor e (se L9) das datas obrigatórias; nota de rotas offline. §4.3: `400` de `initialLactationSyncId` vazio e a `initialLactation` na resposta do cadastro.
- **`spec-producao-11.2-lactacao-secagem.md`:** nota no topo.

**Feito (11/Out/2026):**
- Spec #14 → v1.6: cabeçalho e histórico; sumário com a 14.7; §8 (`Lactation` ✅); §13 **questão 8** (nova) — o carimbo de `UpdatedAt` nas mudanças de estado do animal e do sêmen (Q3 desta spec).
- Spec #14.4: §5 item 3 (a lactação era o último registro inicial que exigia conexão) e a pendência "secagem offline antes do parto" marcada como resolvida.
- `catalogo-erros-api.md`: §4.11 sem os seis `409` de estado repetido e de lactação inativa, com os `400` de datas obrigatórias, `syncId` vazio e cursor, e a nota de rotas offline; §4.3 com o `400` de `initialLactationSyncId` vazio, o `initialLactation` na resposta e o `400` do `startDate` do bloco `initialLactation`.
- `spec-producao-11.2-lactacao-secagem.md`: nota no topo.

**Registros de teste** (banco local, conta `teste.offline.pesagem@muuboi.local`): vaca 126 (lactação 22 aberta; gestação confirmada sem parto), novilha 127 (lactação 23 seca), vaca 128 (lactação 24, de parto), novilha 130 (inativa; lactações 25 inativa e 27), vaca 131 (lactações 26 inativa e 28 inativa), novilha 132 (inativa), vaca 133 (lactação 29 fechada), novilha 134 (lactação 30), vaca 135 (lactações 46 seca e 47 inativa; parto 15 cancelado), vaca 136, boi 137, novilhas 140 (lactação 49) e 141 (lactação 50 inativa); conta `teste.offline.ecc2@muuboi.local` — vaca 139 (lactação 48).

**Pendências observadas (fora desta spec):**
- **Spec #14 §13 questão 8** (Q3 desta spec): aplicar a regra da L7 à saída/reativação do animal e à inativação/reativação da amostra de sêmen.
- Próxima spec sugerida (ordem de complexidade da Spec #14 §8): **evento de vacinação** (`VaccinationEvent` + `VaccinationEventAnimal` embutido + reforço) — depende de vacina e animais, já sincronizáveis; traz uma lista de animais por evento. Os eventos reprodutivos (cobertura, gestação, parto) ficam por último (Condições 1–3 em todas as etapas).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/Lactation.cs` | `ISyncable` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` |
| Infrastructure | `Infrastructure/Migrations/*_Offline_Lactation_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/LactationRepository.cs` | `GetBySyncIdAsync`, `CreateAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Infrastructure | `Infrastructure/Repositories/AnimalRepository.cs` | `GetTagNumbersByIdsAsync` |
| Application | `Application/DTOs/LactationCreateDto.cs`, `LactationDryOffDto.cs`, `LactationSeedDto.cs` | `SyncId?` (criação); L9 |
| Application | `Application/DTOs/LactationUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/LactationDto.cs` | `SyncId` |
| Application | `Application/DTOs/AnimalCreateDto.cs` | `InitialLactationSyncId?` + validação |
| Application | `Application/DTOs/AnimalCreatedDto.cs`, `LactationRefDto.cs` | **Novos** (L3) |
| Application | `Application/Interfaces/ILactationRepository.cs`, `ILactationService.cs`, `IAnimalRepository.cs`, `IAnimalService.cs` | Novos métodos; `CreateAnimalAsync` devolve `AnimalCreatedDto` |
| Application | `Application/Services/LactationService.cs` | Abertura idempotente (Condição 3), LWW, estados idempotentes, L6, L7, pull |
| Application | `Application/Services/AnimalService.cs` | `InitialLactationSyncId` e referência na resposta |
| Api | `Api/Controllers/LactationsController.cs` | Rota `GET api/lactations/changes` |
| Tests | `MuuBoi.Tests/Services/LactationServiceTests.cs` | **Novo** |
| Tests | `MuuBoi.Tests/Services/AnimalServiceTests.cs` | Testes 23–25 |

**Não muda:** regras R1–R5 e R7–R11, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `ProductiveStatusResolver`, `AnimalCalvingService`, `LactationProfile`, rotas de tela (exceto os campos aditivos).

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Dois celulares abrem lactação para o mesmo animal (offline) | O segundo recebe `409` (R4) — conflito real; o app oferece descartar (§5 item 6) |
| Corrida simultânea com `SyncId` diferentes passando juntas por R4 (sem índice único) | Rara (fila única por celular). Q2 |
| Secagem repetida com outra data devolve a primeira (L5) | Efeito colateral aceito (Spec #14 §3.3); o app mostra o estado do servidor |
| L6 esconde uma operação feita offline numa lactação que outro celular excluiu | Comportamento coerente com "a inativação sempre vence" (Spec #14 §5.4); o app mostra a lactação como excluída |
| Derivados do pull (`isLactating`, `daysInMilk`) envelhecem | Valor inicial; o app recalcula (L8, §5 item 7) |
| Reenvio do cadastro com `initialLactationSyncId` de outra lactação existente (erro do app) | Referência só é devolvida se `AnimalId` bater (L3); senão `null` |
| `initialLactationSyncId` repetido de outra lactação (erro do app) → `500` | Aceito, como nas anteriores (A4) |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Corrigir a data/observações da secagem** sem desfazer e secar de novo (hoje o `PATCH` só altera `startDate`) | ⏳ Fora desta spec (mudaria o contrato da edição) |
| Q2 | **Índice único filtrado** `(AnimalId) WHERE EndDate IS NULL AND IsActive = 1` para garantir RN-01 no banco | ⏳ Fora desta spec (mudança de esquema com regra); avaliar se houver relato |
| Q3 | **§3 item 7 nas entidades já sincronizadas:** saída/reativação do animal (14.4) e inativação/reativação da amostra de sêmen (14.2) carimbam `UpdatedAt` com a hora do servidor; uma edição offline do **mesmo** celular feita depois da mudança de estado é descartada pelo LWW no sync | ⏳ Registrada como **questão 8** da Spec #14 §13. Corrigir numa tarefa própria com a regra da L7 (aprovada e aplicada aqui) |
| Q4 | **Lactação aberta pelo parto fora da transação do parto** (§3 item 11, Condição 1) | ⏳ Spec do parto |
