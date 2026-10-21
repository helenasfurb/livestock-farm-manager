# Spec #14.11: Sincronização Offline — Parto e Crias

**Módulo:** Infraestrutura / Sincronização — Reprodução (Parto)
**Versão:** 1.0
**Data:** 16/Out/2026
**Status:** ✅ **Implementada** (Fases 1–7) na branch `feature/offline-calvings`. Decisões P1–P12 fechadas (P6, P7 e P9 como propostas). **Fecha a §8 da Spec #14.**

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 15/Out/2026 | Rascunho: parto com todos os derivados numa transação (crias, animais das crias, pesagem de nascimento, lactação, ECC, status da gestação); `syncId` dos derivados vindo do app; inativação em cascata numa transação e sem `UpdatedAt` |
| 0.2 | 15/Out/2026 | Aprovada: P6 (crias embutidas no pull do parto), P7 (LWW por registro na edição da cria) e P9 (validação → `400`, inclusive o brinco da cria com 6 dígitos, se a Fase 2 confirmar) fechadas como propostas |
| 1.0 | 16/Out/2026 | Implementada: Fases 1–7. §3 item 10 e P12 confirmados. Corrida do parto tratada no repositório (`ChangeTracker.Clear` + rebusca). Achado na Fase 3 e corrigido (aprovado): a edição do sexo pelo cadastro do animal (14.4) passou a tocar o parto. 21 testes novos (292 no total) |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — **Condições 1, 2 e 3 (§7.5)**, §5.4 (mudanças de estado não alteram `UpdatedAt`), §5.9 · **Spec #14.10** (gestação sincronizável — o parto aponta para ela) · Spec #14.4 (animais), #14.5 (pesagem), #14.6 (ECC), #14.7 (lactação — "a lactação aberta pelo parto fica para a spec do parto"). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-gestacao-parto-6.2-parto.md` (RN-01–RN-09) · `spec-gestacao-parto-6.3-cria.md` · `Docs/catalogo-erros-api.md` (§4.10)

> **Por que é a mais pesada:** o `POST` do parto cria registros em **seis** tabelas — o parto, as crias, um **animal** para cada cria viva, a **pesagem de nascimento** de cada uma, a **lactação** da mãe e o **ECC** da mãe — e ainda muda o **status da gestação** para "parto realizado". Hoje isso são **quatro `SaveChanges` separados** (Condição 1). Quatro desses derivados (animal da cria, pesagem, lactação, ECC) são entidades **já sincronizáveis** que o usuário pode mexer offline logo depois do parto — pesar a cria, secar a vaca, corrigir o ECC —, então o app precisa conhecer o `syncId` de cada um antes de o parto chegar ao servidor (Condição 2). A inativação do parto também mexe em quatro tabelas em `SaveChanges` separados e **carimba `UpdatedAt = agora`** em animais e lactação, que já sincronizam (§5.4).

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/pregnancies/{pregnancyId}/calvings` com suporte offline e todos os derivados numa transação | Editar o parto (data, notas) — não existe rota hoje |
| `PATCH /api/calvings/{calvingId}/calves/{calfId}` (edição da cria) com LWW | Incluir ou remover cria de um parto existente — não existe rota |
| `DELETE /api/calvings/{id}` idempotente, com a cascata numa transação | Genealogia (Spec #10); status reprodutivo "pós-parto" (derivado de leitura) |
| `GET /api/calvings/changes` (**nova**) | |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST .../calvings` | Grava parto + crias + animais + pesagens, **depois** a gestação, **depois** a lactação, **depois** o ECC | Aceita `syncId` do parto e dos derivados (P3); tudo num só `SaveChanges`; reenvio → `201` com o existente |
| `PATCH /calvings/{id}/calves/{calfId}` | Aplica e carimba `UpdatedAt = agora` na cria **e no animal** | Aceita `updatedAt`; LWW (P7) |
| `DELETE /calvings/{id}` | Já inativo → `409`; cascata em `SaveChanges` separados, carimbando `UpdatedAt` | Já inativo → `204`; cascata num só `SaveChanges`, sem `UpdatedAt` |
| `GET /calvings/changes` | — | **Nova** (P6) |
| Respostas | — | Parto e crias ganham `syncId`; a resposta do `POST` traz `{ id, syncId }` de cada derivado (animal, pesagem, lactação, ECC) — padrão do `initialLactation` da 14.4/14.7 |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.10): nenhuma regra de negócio é alterada.**

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Gestação precisa existir e estar **confirmada** (RN-01) | `POST` | `404` / `409` "O parto só pode ser registrado para uma gestação confirmada." |
| R2 | Um parto **ativo** por gestação (RN-02) | `POST` | `409` "Esta gestação já possui um parto ativo registrado." |
| R3 | Data do parto não futura (RN-03) e não anterior à confirmação da gestação (RN-04) | `POST` | `400` / `422` |
| R4 | A mãe não pode ter **lactação aberta** | `POST` | `422` "O animal possui uma lactação em aberto…" |
| R5 | Brinco de cria viva único na base e entre as crias do parto (RN-06 da 6.3) | `POST` | `409` |
| R6 | Cria viva vira `Animal` (bezerro(a), nascido na fazenda, nascimento = data do parto); com peso, ganha a pesagem de nascimento | `POST` | Mantido |
| R7 | O parto abre a lactação da mãe (origem "parto") e muda a gestação para "parto realizado" (RN-05); com ECC, grava o ECC da mãe (RN-06) | `POST` | Mantido |
| R8 | Cria só é editável se ela e o parto estiverem ativos; sexo e peso editados refletem no animal e na pesagem de nascimento | `PATCH` cria | `409` / mantido |
| R9 | Inativar o parto inativa as crias, os animais das crias, a lactação do parto, e volta a gestação para "confirmada" (RN-07, RN-08) | `DELETE` | Mantido |

**Ordem entre `SyncId` e regras:** `SyncId` antes de R1–R5. O reenvio de um parto já gravado responde `201` com o existente, mesmo que a gestação já esteja "parto realizado" e a lactação aberta — ambas causadas pelo próprio parto.

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `AnimalCalving` e `AnimalCalvingCalf` são `BaseEntity, ITenantEntity` com filtro de tenant | Basta `ISyncable` | Receita §7.4 (Fase 1) |
| 2 | `CreateAsync` grava o parto (com crias, animais e pesagens pela navegação), **depois** atualiza a gestação, **depois** cria a lactação, **depois** chama o service do ECC | **Condição 1:** se cair no meio, o reenvio acha o `SyncId` e a gestação fica "confirmada", a lactação nunca abre ou o ECC nunca é gravado | Tudo num só `SaveChanges` (P2) |
| 3 | O ECC do parto é gravado pelo `BodyConditionRecordService`, que exige a mãe **ativa** | Mãe inativa com ECC informado → o parto **já foi gravado** e o ECC lança `409` → parto sem ECC e o app recebe erro | ✅ **Confirmado** (15/Out/2026): o app recebe `409` "Não é possível registrar ECC de um animal inativo.", mas o parto, a lactação e o status "parto realizado" da gestação **ficaram gravados**. Checar antes de gravar (P12) |
| 4 | Animais, pesagens, lactação e ECC derivados nascem com `SyncId` gerado pelo banco | **Condição 2:** pesar a cria, secar a vaca ou corrigir o ECC offline, logo depois do parto, exige o `syncId` desses registros | `syncId` de cada derivado vindo do app (P3) |
| 5 | R1 (gestação confirmada), R2 (parto ativo), R4 (lactação aberta) e R5 (brincos) são causados pelo **próprio** parto num reenvio concorrente | **Condição 3** | Rechecar o `SyncId` antes de lançar (P4) |
| 6 | O `UPDATE` da gestação no mesmo `SaveChanges` confere o `RowVersion` (a gestação já é sincronizável) | Dois envios simultâneos do mesmo parto → o segundo esbarra no `RowVersion` (`500`) | Tratar no repositório (P5) |
| 7 | `UpdateCalfAsync` carimba `UpdatedAt = agora` na cria, no animal e na pesagem | Precisa de LWW; o animal e a pesagem são sincronizáveis e têm o seu próprio LWW | P7 ⭐ |
| 8 | `InactivateAsync`: "já está inativo" (`409`); cascata em três `SaveChanges`; carimba `UpdatedAt` no parto, nas crias, nos **animais** e na **lactação** | Viola A6, a Condição 1 e a §5.4 (em entidades que já sincronizam) | P8 |
| 9 | O detalhe do parto (`AnimalCalvingDto`) embute só as crias **ativas**, com nome/brinco do animal | No pull, crias inativadas somem sem tombstone; editar uma cria não muda o `RowVersion` do parto | P6 ⭐ |
| 10 | **Prováveis bugs pré-existentes:** `CalvingDate` (`DateTime`, `[Required]`) ausente vira `0001-01-01`; `Sex` (`AnimalGender`, `[Required]`) ausente vira **`0` = Macho**; `VitalStatus` ausente vira `0`; `Sex` e `VitalStatus` fora do enum não são validados; `BodyConditionScore` fora do enum não é validado; o brinco da cria só tem `MaxLength(6)` — **não** exige 6 dígitos, como o cadastro de animal exige | Cria sem sexo vira macho; cria viva com brinco `abc` vira animal com brinco inválido | ✅ **Confirmado** (15/Out/2026): parto sem data → `422` com a mensagem errada ("anterior à confirmação"); cria sem `sex` → gravada como **macho** (`0`), e o animal também; cria sem `vitalStatus` → `0` (sem animal); `sex: 9` e `vitalStatus: 9` → gravados; `bodyConditionScore: 99` → ECC da mãe gravado com `99`; brinco `abc` → animal criado com brinco `abc`; `PATCH` da cria com `sex: 9` → gravado (na cria e no animal). P9 |
| 11 | Banco local: 16 partos (3 inativos), 16 crias (3 natimortas), 14 lactações abertas por parto | Backfill de `SyncId` pela migração | — |

## 4. Decisões propostas

As marcadas com ⭐ foram escolhidas pela usuária em 15/Out/2026 (todas como propostas); as demais seguem precedentes das specs 14.1–14.10.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| P1 | **`ISyncable` em `AnimalCalving` e `AnimalCalvingCalf`**; uma migração | — |
| P2 | **Parto num só `SaveChanges`**: parto + crias + animais + pesagens (pela navegação, como hoje) + **lactação** (pela navegação `Lactation.Calving`) + **ECC** (montado no service, sem chamar o `BodyConditionRecordService`) + **gestação** "parto realizado" (já rastreada) | Transação explícita em volta das chamadas atuais — funcionaria, mas o ECC continuaria passando pelo outro service e pelas regras dele no meio da transação |
| P3 | **`syncId` do parto e dos derivados vindos do app** (Condição 2): `syncId` (parto), `lactationSyncId`, `bodyConditionSyncId` (se houver ECC) e, em cada cria, `syncId` (cria), `animalSyncId` (cria viva) e `weightSyncId` (cria com peso). Ausentes → o servidor gera. A resposta do `POST` traz `{ id, syncId }` de cada derivado | O app só conhecer os derivados pelo pull — não consegue pesar a cria ou secar a vaca offline antes de sincronizar |
| P4 | **Condição 3** em R1, R2, R4 e R5: antes de lançar, rechecar o `SyncId` do parto e devolver o existente | — |
| P5 | **Corrida tratada no repositório** (precedente da V4/14.8 e do diagnóstico/14.10): se o `SaveChanges` do parto falhar por concorrência (`RowVersion` da gestação) ou por índice único de `SyncId` (parto, crias, animais, pesagens, lactação, ECC), o repositório limpa o rastreamento e devolve `null`; o service rebusca o `SyncId` do parto → existente (`201`) ou o `409` de R2 | Deixar o `500` — reenvio simultâneo é justamente o caso do offline |
| P6 ⭐ | **Pull do parto com as crias embutidas** (`GET /api/calvings/changes`, `AnimalCalvingSyncDto`): parto (`id`, `syncId`, `animalPregnancyId`, `animalId`, `calvingDate`, `notes`, `isActive`, datas) + `calves[]` **inclusive inativas** (`id`, `syncId`, `animalId`, `sex`, `weightKg`, `vitalStatus`, `notes`, `isActive`). **Sem** nome/brinco/raça (vêm no pull de animais). Editar uma cria "toca" o parto (`UPDATE` direto, na mesma transação — precedente H6 da 14.9). **2 consultas por página**. Animais, pesagens, lactação e ECC chegam pelos **pulls que já existem** | Rota própria para as crias (`/calvings/calves/changes`) — sem "tocar" o parto, mas uma requisição a mais por sincronização |
| P7 ⭐ | **Edição da cria com LWW por registro**: o `updatedAt` do app é comparado com o da **cria** (aplica ou não a edição da cria); o reflexo no **animal** (sexo) e na **pesagem de nascimento** (peso) só é aplicado se o `updatedAt` também não for mais antigo que o **daquele** registro — cada um guarda `UpdatedAt = editedAt`. Assim, uma edição antiga da cria não desfaz uma edição mais nova feita direto no animal ou na pesagem | (a) LWW só na cria e reflexo sempre aplicado — uma edição antiga da cria poderia sobrescrever o sexo corrigido depois no cadastro do animal. (b) Parar de refletir no animal/pesagem — muda a regra R8 |
| P8 | **Inativação idempotente e num só `SaveChanges`**: `204` sem gravar no repetido; parto, crias, animais das crias, lactação do parto e gestação mudam juntos; **nenhum** `UpdatedAt` carimbado (§5.4) | — |
| P9 ⭐ | **Validação → `400`** (se a Fase 2 confirmar o §3 item 10): `CalvingDate` → `DateTime?` `[Required]`; `Sex` e `VitalStatus` da cria → anuláveis com `[Required]` e `[ValidEnum]`; `[ValidEnum]` no `BodyConditionScore` e no `Sex` da edição; brinco da cria viva com a **mesma** regra do cadastro de animal (6 dígitos), já que ela **vira** um animal | Manter — cria sem sexo continua virando macho e brincos fora do padrão entram por esta rota |
| P10 | **Pull dos derivados pelas rotas existentes**: animais (14.4), pesagens (14.5), ECC (14.6) e lactações (14.7) já trazem o que o parto cria e o que a inativação muda | Embutir os derivados no pull do parto — duplicaria dados que já sincronizam |
| P11 | **Status da gestação** muda no mesmo `SaveChanges` (criação → "parto realizado"; inativação → "confirmada") e aparece no pull de gestações (14.10); sem `UpdatedAt` (§5.4) | — |
| P12 | **ECC da mãe inativa:** a regra "não registra ECC de animal inativo" passa a ser checada **antes** de gravar o parto (mesmo `409` e mensagem). Hoje o parto é gravado e o ECC falha depois — com a P2 não há como gravar "metade". Comportamento visível: o parto com ECC de mãe inativa passa a ser recusado por inteiro (antes: gravado sem ECC + `409`) | Ignorar o ECC quando a mãe está inativa — perderia dado sem avisar |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** gestação (diagnóstico positivo ou retroativa, 14.10) → parto → operações sobre os derivados (pesar a cria, secar a vaca, editar a cria). O `POST` usa o `ServerId` da gestação (A5).
2. **`syncId` dos derivados:** o app gera e envia todos (P3) e cria os registros locais com eles — animal da cria, pesagem de nascimento, lactação, ECC. As operações seguintes usam o `ServerId` resolvido pelo `syncId` depois do envio do parto.
3. **Pull:** puxar animais, pesagens, ECC, lactações e gestações; o parto vem no seu pull com as crias. As listas de crias **substituem** as locais (upsert por `syncId`).
4. **Edição da cria:** `updatedAt` = momento da edição; uma edição mais antiga que a do servidor é ignorada por registro (P7).
5. **`409` de R2 com outro `syncId`:** conflito real (outro celular registrou o parto da mesma gestação). O app mostra o parto existente.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (P1) | ✅ 15/Out/2026 — migração `20261015005826_Offline_Calving_SyncId_RowVersion` aplicada no banco local; 16 partos e 16 crias com `SyncId` distintos; parto novo (cria viva + natimorto) nasce com `SyncId` no parto e nas crias. §3 item 10 e P12 **confirmados** (ver §3) |
| 2 — Parto idempotente numa transação, com os `syncId` dos derivados (P2–P5, P11, P12) + confirmação do §3 item 10 | ✅ 16/Out/2026 — parto com 3 crias (2 vivas, 1 com peso, 1 natimorta) + ECC → 1 parto, 3 crias, 2 animais, 1 pesagem, 1 lactação, 1 ECC, **todos com os `syncId` enviados**, gestação "parto realizado" sem `UpdatedAt`; resposta com `animalSyncId`/`birthWeight` de cada cria, `lactation` e `bodyCondition`; reenvio, reenvio com outro payload e Condição 3 → `201` com o existente, nada duplicado; parto novo na mesma gestação → `409`; P12 → `409` e **nada** gravado; P9 → `400` (8 casos); 20 simultâneos → 20 × `201`, 1 de cada registro. O ECC devolvido no reenvio depende do `bodyConditionSyncId` (o ECC não tem vínculo com o parto). `IBodyConditionRecordService` saiu do service do parto |
| 3 — Edição da cria com LWW (P7) | ✅ 16/Out/2026 — LWW na cria (mais novo, `-03:00`, reenvio, mais antigo, sem `updatedAt`, relógio adiantado); editar a cria toca o parto (`RowVersion` avança, `UpdatedAt` do parto não muda); peso sem pesagem de nascimento → cria a pesagem com o `weightSyncId` enviado; peso de novo → atualiza a mesma; `sex: 9` → `400`; cria de parto inativo → `409`. **P7:** a correção do sexo direto no animal (`PATCH /api/animals/{id}`, 14.4) já reflete na cria com o mesmo `updatedAt`; uma edição da cria mais antiga que essa correção é ignorada por inteiro — o registro mais novo vence nos dois. **Achado e corrigido (aprovado):** essa correção pelo cadastro do animal mudava a cria sem tocar o parto — a cria embutida no pull ficaria com o sexo antigo. `AnimalRepository.UpdateAnimalAndTouchCalvingAsync` (transação + toque) passou a ser usado quando o sexo muda e há cria vinculada |
| 4 — Inativação idempotente em cascata (P8) | ✅ 16/Out/2026 — inativar → parto, crias, animal da cria viva e lactação inativos e gestação "confirmada", num só `SaveChanges`; **nenhum** `UpdatedAt` alterado; `RowVersion` de todos avança; repetido → `204` sem gravar (`RowVersion` iguais); parto novo na mesma gestação depois de inativar → `201`; inexistente → `404` |
| 5 — Pull incremental (P6) | ✅ 16/Out/2026 — `GET /api/calvings/changes`: pull inicial (15 partos, 2 inativos; 19 crias, 3 inativas) batendo com o banco; pull × detalhe (`GET /api/pregnancies/{id}`) sem divergências em 13 partos ativos; isolamento entre duas contas; editar só a cria **ou** mudar o sexo pelo cadastro do animal → o parto reaparece com a cria atualizada; inativar → parto e crias inativos no pull, e animal, lactação e gestação nos pulls deles; paginação de 4 sem duplicatas; cursor inválido `400`; sem colisão com `/calf-vital-statuses`; **2 consultas por página** |
| 6 — Testes | ✅ 16/Out/2026 — `AnimalCalvingServiceTests` (21, novo); `AnimalServiceTests` ajustado (reflexo do sexo na cria agora toca o parto); suíte completa 292/292 |
| 7 — Documentação | ✅ 16/Out/2026 — Spec #14 (v1.11: §5.5, §8 — fechada —, sumário), `catalogo-erros-api.md` (§4.10), `spec-gestacao-parto-6.2-parto.md` e `6.3-cria.md` (notas no topo), Specs #14.4 e #14.7 (notas) |

Branch: `feature/offline-calvings`, a partir do `dev`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com a conta de teste da 14.8.

### 6.1 Fase 1 — Domínio e banco

- `AnimalCalving`, `AnimalCalvingCalf`: `ISyncable`; `ConfigureSyncable()`.
- Migração `Offline_Calving_SyncId_RowVersion` (⚠️ **requer aprovação**): `RowVersion` + `SyncId` (`DEFAULT NEWID()`) + índices nas duas tabelas.

| Validação planejada | Esperado |
|---|---|
| Registros existentes | 16 partos e 16 crias com `SyncId` distintos |
| Registros novos | `SyncId` ≠ `Guid.Empty` |

### 6.2 Fase 2 — Parto idempotente numa transação

| Validação planejada | Esperado |
|---|---|
| Parto com 2 crias vivas (uma com peso) + ECC, todos os `syncId` enviados | `201`; 1 parto, 2 crias, 2 animais, 1 pesagem, 1 lactação, 1 ECC, gestação "parto realizado" — **todos com os `syncId` enviados** |
| Reenvio / reenvio com outro payload | `201` com o existente; nada duplicado |
| Condição 3 (reenvio com a gestação já "parto realizado" e a lactação aberta) | `201`, não `409`/`422` |
| Natimorto | Cria sem animal |
| Mãe inativa com ECC (P12) | `409` e **nada** gravado |
| Parto novo (outro `syncId`) na mesma gestação | `409` (R2, mantido) |
| 20 envios simultâneos do mesmo parto | Todos `201`; nada duplicado; nenhum `500` |
| P9 | `400` nos casos do §3 item 10 |

### 6.3 Fase 3 — Edição da cria com LWW

| Validação planejada | Esperado |
|---|---|
| Mais novo / mais antigo / `-03:00` / reenvio / sem `updatedAt` / relógio adiantado | Como nas specs anteriores, na cria |
| Edição da cria mais antiga que uma edição do sexo direto no animal (P7) | Cria atualizada (se for mais nova que a cria); **sexo do animal mantido** |
| Peso editado | Pesagem de nascimento atualizada (ou criada, se não havia) |
| Cria de parto inativo | `409` (R8, mantido) |

### 6.4 Fase 4 — Inativação em cascata

| Validação planejada | Esperado |
|---|---|
| Inativar → inativar de novo | `204` → `204`; nada gravado no repetido |
| Inativar | Parto, crias, animais das crias e lactação inativos; gestação "confirmada"; **nenhum** `UpdatedAt` alterado; `RowVersion` de todos avança |

### 6.5 Fase 5 — Pull incremental

| Validação planejada | Esperado |
|---|---|
| Pull inicial | Todos os partos, inclusive inativos, com crias ativas e inativas |
| Pull × `GET` (detalhe do parto via gestação) | Sem divergências nos campos comuns |
| Isolamento | Cada conta vê só os seus |
| Editar só uma cria → pull | O parto reaparece (P6) |
| Inativar → pull | Parto e crias inativos; animais, lactação e gestação nos seus pulls |
| Consultas por página | 2 |

### 6.6 Fase 6 — Testes

`AnimalCalvingServiceTests.cs` (novo). Cobrir: criação com todos os derivados e `syncId` do app; reenvio; Condição 3 em R1/R2/R4/R5; corrida (repositório devolve `null`); P12; natimorto sem animal; edição da cria com LWW por registro (P7); inativação idempotente sem `UpdatedAt`; pull (cursor inválido, `hasMore`, crias inativas embutidas).

### 6.7 Fase 7 — Documentação

**Esta spec**; **Spec #14** (§8: `AnimalCalving` e `AnimalCalvingCalf` ✅ — **fecha a §8**); **`catalogo-erros-api.md`** (§4.10); **`spec-gestacao-parto-6.2-parto.md`** e **`6.3-cria.md`** (notas no topo; P12); **Spec #14.7** (a lactação do parto agora tem `syncId` do app).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `AnimalCalving.cs`, `AnimalCalvingCalf.cs` | `ISyncable` |
| Infrastructure | `ApplicationDbContext.cs`, nova migração | `ConfigureSyncable()` (⚠️ aprovação da migração) |
| Infrastructure | `AnimalCalvingRepository.cs` | `GetBySyncIdAsync`, criação conjunta com tratamento da corrida (P5), toque no parto (P6), `GetChangesAsync`, crias por parto |
| Application | `AnimalCalvingCreateDto.cs`, `AnimalCalvingCalfCreateDto.cs`, `AnimalCalvingCalfUpdateDto.cs` | `syncId` dos derivados (P3), `updatedAt`, P9 |
| Application | `AnimalCalvingDto.cs`, `AnimalCalvingCalfDto.cs` (+ referências dos derivados) | `syncId`; `{ id, syncId }` dos derivados no `POST` |
| Application | `AnimalCalvingSyncDto.cs` (+ cria) | **Novos** (P6) |
| Application | `AnimalCalvingService.cs` | Transação única, Condições 2 e 3, P7, P8, P12, pull |
| Api | `AnimalCalvingsController.cs` | Rota `GET changes` |
| Tests | `AnimalCalvingServiceTests.cs` | Novo |

**Não muda:** `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, regras R1–R9, os services de animal, pesagem, ECC e lactação.

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Transação grande (até seis tabelas) num servidor fraco | Um `SaveChanges` só, em lote; um parto típico tem 1–2 crias |
| Dois celulares registram o parto da mesma gestação | O segundo recebe `409` (R2) — conflito real |
| Edição antiga da cria sobrescrevendo dado mais novo do animal/pesagem | P7 (LWW por registro) |
| P12 muda o comportamento visível do parto com mãe inativa e ECC | Antes era um erro parcial (parto gravado sem ECC + `409`); agora é um `409` limpo; documentado no catálogo |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | Editar o parto (data/notas) e incluir/remover crias | ⏳ Fora — não há rota |
| Q2 | Inativar o parto **não** inativa o ECC gravado no parto | ⏳ Mantido (comportamento atual); registrar se a usuária quiser mudar |
