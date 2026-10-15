# Spec #14.10: Sincronização Offline — Cobertura, Diagnóstico e Gestação

**Módulo:** Infraestrutura / Sincronização — Reprodução
**Versão:** 1.0
**Data:** 14/Out/2026
**Status:** ✅ **Implementada** (Fases 1–9) na branch `feature/offline-breeding-pregnancies`. Decisões B1–B12 fechadas (B2, B4 e B11 como propostas); Q2 corrigida nesta spec.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 14/Out/2026 | Rascunho: cobertura (com a saída de sêmen), diagnóstico (com a gestação derivada) e gestação retroativa; Condições 1, 2 e 3; doses de sêmen offline (§13 questão 1 da Spec #14) |
| 0.2 | 14/Out/2026 | Aprovada: B2 (`ClientRequestId` vira `SyncId`), B4 (cobertura aceita saldo de sêmen negativo — **única mudança de regra**, online e offline) e B11 (validação → `400`, se a Fase 2 confirmar) fechadas como propostas |
| 1.0 | 14/Out/2026 | Implementada: Fases 1–9. §3 itens 11 e 12 confirmados (B11 aplicada; Q2 corrigida: editar a cobertura move a saída de sêmen). Migração com o `Up` reordenado à mão para preservar o `ClientRequestId`. Corrida do diagnóstico tratada no repositório (`SaveDiagnosisAsync`). Pull da cobertura sem `pregnancyId` (vínculo no `breedingEventId` da gestação). `CreateForBreedingEventAsync`/`InactivateForBreedingEventAsync` da movimentação de sêmen removidos. 28 testes novos (271 no total) |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, helpers (§7.2), receita (§7.4), **Condições 1, 2 e 3 (§7.5)**, §5.4 (mudanças de estado não alteram `UpdatedAt`), §13 questão 1 (saldo de sêmen na cobertura) · Spec #14.2 (sêmen e movimentações; o pull de movimentações já traz as saídas da cobertura, somente leitura) · Spec #14.4 (animais). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-eventos-reprodutivos.md` (CU-01–CU-05, RN-01–RN-09) · `spec-cadastro-retroativo-gestacao.md` (Spec #13, RN-01–RN-07, `ClientRequestId`) · `spec-gestacao-parto-6.1-gravidez.md` · **Spec #14.11** (parto e crias — próxima, depende desta)

> **Por que dividir o grupo reprodutivo:** o grupo tem 4 entidades novas e derivados em 5 entidades já sincronizáveis. Esta spec cobre o que vem **antes** do parto; a 14.11 cobre o parto e as crias, que dependem da gestação.
>
> **O que a torna diferente das anteriores:** (1) **derivados criados pelo servidor numa operação do app** — a cobertura por IA gera a saída de 1 dose de sêmen, e o diagnóstico positivo gera a gestação; hoje cada um é um `SaveChanges` separado (**Condição 1**); (2) o usuário **referencia a gestação derivada** offline (para registrar a perda ou o parto) antes de ela existir no servidor → o app gera o `syncId` da gestação já no diagnóstico (**Condição 2**); (3) **várias regras de estado** que o reenvio pode esbarrar ("cobertura aguardando diagnóstico", "gestação ativa") (**Condição 3**); (4) a regra de **dose disponível** do sêmen, que offline vira corrida entre celulares (§13 questão 1 da Spec #14); (5) a gestação retroativa já tem uma chave de idempotência própria, o `ClientRequestId` (Spec #13), a unificar com o `SyncId`.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/animals/{animalId}/breeding-events` (cobertura) com suporte offline, incluindo a saída de sêmen na mesma transação | Parto, crias e o que o parto gera (Spec #14.11) |
| `PATCH /api/breeding-events/{id}` com LWW | Telas de leitura derivada (previsão de partos, KPIs, autocomplete de elegíveis) — o app recalcula sobre o pull |
| `PATCH /api/breeding-events/{id}/status` (diagnóstico) idempotente, com a gestação na mesma transação | Genealogia (Spec #10) |
| `POST /api/animals/{animalId}/pregnancies` (retroativa) com `syncId` | |
| `PATCH /api/pregnancies/{id}/status` (perda) idempotente | |
| `DELETE` da cobertura (com a saída de sêmen) e da gestação idempotentes | |
| `GET /api/breeding-events/changes` e `GET /api/pregnancies/changes` (**novas**) | |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST .../breeding-events` | Cria a cobertura e, **depois**, a saída de sêmen (dois `SaveChanges`) | Aceita `syncId`; reenvio → `201` com a existente; cobertura e saída numa só transação |
| `PATCH /breeding-events/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; LWW |
| `PATCH /breeding-events/{id}/status` | Grava o diagnóstico e, **depois**, a gestação; repetido → `409` | Aceita `pregnancySyncId`; diagnóstico e gestação numa só transação; repetido com o mesmo resultado → `200` com o estado atual |
| `POST .../pregnancies` | `clientRequestId` (Spec #13) | `syncId` (B2) |
| `PATCH /pregnancies/{id}/status` | Perda repetida → `409` | Perda repetida → `200` com o estado atual |
| `DELETE /breeding-events/{id}`, `DELETE /pregnancies/{id}` | Já inativo → `409`; cobertura e saída de sêmen em dois `SaveChanges` | Já inativo → `204`, sem gravar; cobertura e saída numa só transação |
| `GET /breeding-events/changes`, `GET /pregnancies/changes` | — | **Novas** (B10) |
| Respostas de detalhe, `POST` e `PATCH` | — | Ganham `syncId` (aditivo); a cobertura ganha `pregnancyId`/`pregnancySyncId` quando há gestação |

## 2. Regras de negócio preservadas

**Premissa (como nas 14.2–14.9): nenhuma regra de negócio é alterada** — exceto o que a usuária decidir na B4 (doses de sêmen), que é exatamente a questão em aberto da Spec #14 §13.

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Animal precisa existir, estar **ativo** e ser vaca ou novilha | `POST` cobertura, `POST` retroativa | `404` / `409` / `422` |
| R2 | Só uma cobertura **aguardando diagnóstico** por animal | `POST` cobertura | `422` "O animal já possui uma cobertura aguardando diagnóstico…" |
| R3 | Animal com **gestação ativa confirmada** não recebe cobertura nem nova gestação retroativa | `POST` cobertura, `POST` retroativa | `422` (cobertura) / `409` (retroativa) |
| R4 | IA: sêmen existe, ativo e **com dose disponível**; monta: touro existe, ativo e classificado como touro | `POST` e `PATCH` cobertura | `404` / `409` / `422` (ver B4 para a dose) |
| R5 | Número do serviço = coberturas ativas do animal + 1 | `POST` cobertura | Mantido |
| R6 | Só cobertura **aguardando diagnóstico** pode ser editada | `PATCH` cobertura | `409` "Apenas coberturas com diagnóstico pendente podem ser editadas." |
| R7 | Diagnóstico: só uma vez; data não anterior à cobertura; idade gestacional só no positivo; positivo gera a gestação (data prevista de parto pela idade gestacional ou pela cobertura) | `PATCH .../status` | `409` / `422` / `400` (ver B6 para o repetido) |
| R8 | Retroativa: no máximo um entre touro e sêmen; pai válido e ativo; data prevista de parto posterior à confirmação; **não** consome dose (RN-06 da Spec #13) | `POST` retroativa | `400` / `404` / `409` / `422` |
| R9 | Perda: só de gestação confirmada | `PATCH /pregnancies/{id}/status` | `409` "Apenas gestações confirmadas podem ser marcadas como interrompidas." (ver B8 para a repetida) |
| R10 | Inativar cobertura com gestação ativa é proibido; inativar a cobertura por IA inativa a saída de sêmen | `DELETE` cobertura | `409` / mantido |
| R11 | Inativar gestação com parto ativo é proibido | `DELETE` gestação | `409` |

**Ordem entre `SyncId` e regras:** `SyncId` antes de R1–R4 e R8. O reenvio de uma criação já gravada responde `201` com o existente, mesmo que depois o animal tenha sido vendido, o sêmen inativado ou a última dose usada.

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `BreedingEvent` e `AnimalPregnancy` são `BaseEntity, ITenantEntity` com filtro de tenant | Basta `ISyncable` | Receita §7.4, uma migração (Fase 1) |
| 2 | Cobertura por IA: `CreateAsync` grava a cobertura e **depois** `CreateForBreedingEventAsync` grava a saída de sêmen | **Condição 1:** se cair entre os dois, o reenvio acha o `SyncId` e a dose **nunca é consumida** | Uma transação (B3) |
| 3 | Diagnóstico positivo: grava o status e **depois** `CreateForBreedingEventAsync` grava a gestação | **Condição 1:** cobertura "positiva" sem gestação; **Condição 2:** o app não tem como referenciar a gestação (perda, parto offline) | Uma transação + `pregnancySyncId` vindo do app (B6) |
| 4 | Inativar cobertura por IA: grava a cobertura e **depois** inativa a saída de sêmen | **Condição 1:** cobertura inativa com a dose ainda consumida | Uma transação (B9) |
| 5 | R2 e R3 são checadas entre a busca do `SyncId` e o `INSERT` | **Condição 3:** o reenvio de uma cobertura já gravada esbarra no "aguardando diagnóstico" que ela mesma criou → `422` definitivo para o app | Rechecar o `SyncId` antes de lançar (B3, B7) |
| 6 | R4 (dose disponível) | Dois celulares usam a última dose offline → o segundo recebe `422` no sync, **embora a inseminação tenha acontecido** | B4 ⭐ |
| 7 | Diagnóstico, perda e inativações repetidos → `409` | Violam A6 | B6, B8, B9 |
| 8 | `UpdateAsync`, diagnóstico, perda e inativações carimbam `UpdatedAt = DateTime.UtcNow` | Edição precisa de LWW; mudanças de estado violam a §5.4 | B5, B12 |
| 9 | Retroativa usa `ClientRequestId` (coluna, índice filtrado, `GetByClientRequestIdAsync`); 1 gestação no banco local tem o valor | Duas chaves de idempotência para a mesma coisa | B2 ⭐ |
| 10 | Detalhe da cobertura e da gestação trazem nomes/brincos; a gestação embute o parto (`calving`) | No pull, nomes envelhecem; o parto terá pull próprio (14.11) | DTOs de pull próprios (B10) |
| 11 | **Prováveis bugs pré-existentes:** datas `[Required]` não anuláveis (`BreedingDate`, `DiagnosisDate`, `ConfirmationDate`, `LossDate`) viram `0001-01-01` quando ausentes; enums `[Required]` não anuláveis (`ReproductionType`, `Status` do diagnóstico, `Status` da perda) viram `0`; `DiagnosisMethod` fora do enum não é validado | Cobertura com data de 0001; diagnóstico com status `0` | ✅ **Confirmado** (14/Out/2026): cobertura sem `breedingDate` → gravada com `0001-01-01`; sem `reproductionType` → `0`; `reproductionType: 9` → gravado; diagnóstico **sem `status` → gravado com status `0`** (a cobertura sai de "aguardando" sem virar positiva nem negativa, e nenhuma gestação é criada); `diagnosisMethod: 9` → gravado; diagnóstico sem data → `422` com a mensagem errada ("anterior à cobertura"); retroativa sem `confirmationDate` e perda sem `lossDate` → `0001-01-01`. B11 |
| 12 | **Provável bug pré-existente:** o `PATCH` da cobertura troca o sêmen (`SemenSampleId`) ou a data, mas **não** a saída de sêmen gerada | A dose fica consumida da amostra antiga, com a data antiga | ✅ **Confirmado** (14/Out/2026): cobertura trocada da amostra A para a B (e a data de 10 para 11/10) → a saída continuou na A, com 10/10; saldos A = 3, B = 5 (a B deveria ter perdido a dose). Ver Q2 |
| 13 | Banco local: 14 coberturas, 22 gestações (14 retroativas, 1 com `ClientRequestId`), 6 saídas de sêmen da cobertura | Backfill de `SyncId` pela migração | — |

## 4. Decisões propostas

As marcadas com ⭐ foram escolhidas pela usuária em 14/Out/2026 (todas como propostas); as demais seguem precedentes das specs 14.1–14.9.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| B1 | **`ISyncable` em `BreedingEvent` e `AnimalPregnancy`** + `ConfigureSyncable()`; uma migração | — |
| B2 ⭐ | **`ClientRequestId` vira `SyncId`.** A migração copia o `ClientRequestId` para o `SyncId` onde houver valor e **remove** a coluna e o índice; o DTO da retroativa troca `clientRequestId` por `syncId`. Uma chave só, igual às demais entidades. O app é o único cliente e ainda não usa o offline | (a) Manter as duas (o `ClientRequestId` como alias do `SyncId`) — duas fontes de idempotência para o mesmo registro. (b) Manter só o `ClientRequestId` na retroativa — a gestação do diagnóstico ficaria com outra chave |
| B3 | **Cobertura idempotente, numa transação com a saída de sêmen** (a saída é ligada pela navegação `SemenSampleMovement.BreedingEvent`, no mesmo `SaveChanges`). `SyncId` antes de R1–R4; **Condição 3** em R2 e R3: rechecar o `SyncId` antes de lançar. A saída de sêmen tem `SyncId` gerado pelo servidor (o usuário não a referencia — §7.5) e chega pelo pull de movimentações da 14.2 | Dois `SaveChanges` — viola a Condição 1 |
| B4 ⭐ | **Dose de sêmen: permitir saldo negativo na cobertura.** A inseminação já aconteceu no campo; recusar no sync perde um registro real. A regra "não há doses" deixa de bloquear a cobertura; o saldo negativo aparece na amostra (e o app pode sinalizar). Fecha a §13 questão 1 da Spec #14 do mesmo jeito que o estoque de insumos (14.3) e as saídas manuais de sêmen (14.2), que já aceitam saldo negativo | (a) Manter o `422` — o segundo celular recebe erro definitivo e o usuário precisa corrigir à mão (lançar uma entrada antes), mesmo com a dose usada de verdade. (b) Bloquear só online e liberar no offline — regra diferente por canal |
| B5 | **`PATCH` da cobertura com LWW**: R6 → LWW → atribuições; `UpdatedAt = editedAt`. Editar depois do diagnóstico continua `409` (R6) — conflito real | — |
| B6 | **Diagnóstico idempotente, numa transação com a gestação.** Aceita `pregnancySyncId` (Condição 2): a gestação nasce com o `syncId` vindo do app, para a perda e o parto offline a referenciarem. Repetido com o **mesmo** resultado (e mesmo `pregnancySyncId`, quando positivo) → `200` com o estado atual, sem gravar. Resultado **diferente** de um já registrado → `409` (conflito real, R7). Sem `UpdatedAt` (§5.4) | Diagnóstico sem `pregnancySyncId` — o app não conseguiria referenciar a gestação antes do pull |
| B7 | **Retroativa idempotente** pelo `SyncId` (B2); **Condição 3** em R3 ("gestação ativa confirmada"): rechecar o `SyncId` antes do `409` | — |
| B8 | **Perda idempotente**: gestação já perdida → `200` com o estado atual, sem gravar; gestação com parto → `409` (R9). Sem `UpdatedAt` (§5.4) | — |
| B9 | **Inativações idempotentes** (cobertura, gestação): `204` sem gravar no repetido; sem `UpdatedAt`; a cobertura por IA inativa a saída de sêmen **na mesma transação** | — |
| B10 | **Pull com DTOs próprios**, em duas rotas: `GET /api/breeding-events/changes` (`BreedingEventSyncDto`: ids, tipo, datas, status, método, número do serviço, notas — o vínculo com a gestação fica no `breedingEventId` da gestação, ver Fase 7) e `GET /api/pregnancies/changes` (`AnimalPregnancySyncDto`: ids, `breedingEventId`, pai, datas, idade gestacional, status, notas). **Sem** nomes/brincos e **sem** o parto embutido (pull próprio na 14.11). 1 consulta por página em cada | Embutir a gestação na cobertura — a retroativa não tem cobertura |
| B11 ⭐ | **Validação → `400`** (se a Fase 2 confirmar o §3 item 11): datas `[Required]` → `DateTime?`; enums `[Required]` → anuláveis com `[ValidEnum]`; `[ValidEnum]` no `DiagnosisMethod`. Mesma linha da V8 (14.8) e H8 (14.9) | Manter — datas de 0001 e status `0` sincronizariam para todos os celulares |
| B12 | **Mudanças de estado sem `UpdatedAt`** (diagnóstico, perda, inativações) — aplica a §5.4 a estas entidades | — |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** animal (e sêmen/touro) criados offline → cobertura → diagnóstico → perda. A gestação retroativa vem depois do animal.
2. **Cobertura:** `syncId` próprio. A saída de sêmen **não** é enviada pelo app — o servidor cria e ela chega no pull de movimentações de sêmen (14.2).
3. **Diagnóstico positivo:** o app gera e envia o `pregnancySyncId`; a gestação local nasce com ele. A perda (e o parto, na 14.11) usam o `ServerId` da gestação, resolvido pelo `pregnancySyncId` depois do envio do diagnóstico.
4. **`409` de R6 ou de diagnóstico com resultado diferente:** conflito real (outro celular diagnosticou primeiro). O app mostra o estado do servidor (vem no pull).
5. **Pull:** puxar **animais e sêmen antes**, depois coberturas, depois gestações.
6. **Derivados locais:** previsão de partos, contagens e "elegíveis para cobertura" são recalculados sobre os dados do pull.

## 6. Fases de implementação e validação

| Fase | Status |
|---|---|
| 1 — Domínio e banco (B1, B2) | ✅ 14/Out/2026 — migração `20261014234514_Offline_Breeding_Pregnancy_SyncId_RowVersion` (reordenada à mão: `SyncId` criado, `UPDATE` com o `ClientRequestId`, coluna removida) aplicada no banco local; 14 coberturas e 22 gestações com `SyncId` distintos; a gestação 4 ficou com `SyncId` = o antigo `ClientRequestId`; retroativa nova com `syncId` + reenvio → 1 gestação. §3 itens 11 e 12 **confirmados** (ver §3) |
| 2 — Cobertura idempotente com a saída de sêmen (B3, B4) + confirmação do §3 itens 11 e 12 | ✅ 14/Out/2026 — validações da §6.2 todas como esperado: reenvio, reenvio com outro payload e Condição 3 ("já aguarda diagnóstico") → `201` com a original, 1 saída de sêmen (com `SyncId` próprio), 1 dose consumida; reenvio depois de vender a vaca e inativar o sêmen → `201`; cobertura nova na vaca vendida → `409` (mantido); monta natural sem saída; B4: duas coberturas com 1 dose → `201` e saldo `-1`; B11 → `400`; 20 simultâneos: 20 × `201`, 1 cobertura, 1 saída. `SemenSampleMovementService.CreateForBreedingEventAsync` removido (a saída é montada junto com a cobertura) |
| 3 — Diagnóstico idempotente com a gestação (B6) | ✅ 14/Out/2026 — positivo com `pregnancySyncId` → 1 gestação com aquele `SyncId` (data prevista pela cobertura ou pela idade gestacional, como antes); repetido → `200` sem gravar; negativo depois de positivo → `409`; negativo não cria gestação; B11 → `400`; `UpdatedAt` da cobertura não muda (B12); 20 positivos simultâneos → 20 × `200`, 1 gestação; 10 positivos + 10 negativos simultâneos → 10 × `200` + 10 × `409`, nenhum `500`. Corrida tratada em `SaveDiagnosisAsync` (concorrência do `RowVersion` da cobertura ou índice `UX_AnimalPregnancies_SyncId`/`IX_AnimalPregnancies_BreedingEventId` → rebusca e compara o resultado) |
| 4 — Gestação retroativa e perda (B7, B8) | ✅ 14/Out/2026 — retroativa: reenvio, reenvio com outro payload e Condição 3 ("gestação ativa") → `201` com a original; retroativa nova na mesma vaca → `409` (mantido); sem `confirmationDate` → `400`; 20 simultâneas → 20 × `201`, 1 gestação. Perda: `200`; repetida → `200` sem gravar (`RowVersion` igual); sem `UpdatedAt`; sem `lossDate`/status `9`/status `2` → `400`; gestação com parto → `409` (mantido) |
| 5 — Edição da cobertura com LWW (B5) | ✅ 14/Out/2026 — LWW como nas specs anteriores (mais novo, `-03:00`, reenvio, mais antigo — inclusive uma troca de sêmen mais antiga, ignorada por inteiro —, sem `updatedAt`, relógio adiantado); edição depois do diagnóstico → `409`; **Q2:** trocar o sêmen (A → B) e a data leva a saída junto, no mesmo `SaveChanges` (saldos A = 5, B = 4); monta natural sem saída |
| 6 — Inativações idempotentes (B9, B12) | ✅ 14/Out/2026 — cobertura por IA: cobertura e saída de sêmen inativas no mesmo `SaveChanges` (dose volta ao saldo: 4 → 5); repetido → `204` sem gravar (`RowVersion` igual); `UpdatedAt` inalterado; gestação idem; cobertura com gestação ativa e gestação com parto ativo → `409` (mantidos). `SemenSampleMovementService.InactivateForBreedingEventAsync` removido (e o teste dele) — a saída é inativada junto com a cobertura |
| 7 — Pull incremental (B10) | ✅ 14/Out/2026 — validações da §6.7 todas como esperado: pull inicial (23 coberturas, 11 gestações, com inativos) batendo com o banco; pull × `GET /{id}` sem divergências; isolamento entre duas contas; diagnóstico positivo → cobertura numa rota e gestação (com `breedingEventId`) na outra; inativar cobertura por IA → cobertura inativa no pull e saída inativa no pull de movimentações (14.2); paginação de 5 sem duplicatas; cursor inválido `400`; sem colisão de rotas; **1 consulta por página** em cada rota. **Ajuste da B10:** o pull da cobertura **não** traz `pregnancyId` (exigiria juntar a gestação e sair de 1 consulta) — o vínculo fica só na gestação (`breedingEventId`); o detalhe da cobertura mantém `pregnancyId`/`pregnancySyncId`. O detalhe da gestação ganhou `syncId` |
| 8 — Testes | ✅ 14/Out/2026 — `BreedingEventServiceTests` (20) e `AnimalPregnancyServiceTests` (8), novos; suíte completa 271/271 |
| 9 — Documentação | ✅ 14/Out/2026 — Spec #14 (v1.10: §8, §13 questão 1, sumário), `catalogo-erros-api.md` (§4.8 e §4.9), `spec-eventos-reprodutivos.md` e `spec-cadastro-retroativo-gestacao.md` (notas no topo), Spec #14.2 (nota sobre a saída da cobertura) |

Branch: `feature/offline-breeding-pregnancies`, a partir do `dev`. Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local, com a conta de teste da 14.8.

### 6.1 Fase 1 — Domínio e banco

- `BreedingEvent`, `AnimalPregnancy`: `ISyncable`; `ConfigureSyncable()`.
- **Se B2:** remover `AnimalPregnancy.ClientRequestId` (modelo, índice `IX_AnimalPregnancies_ClientRequestId`, `GetByClientRequestIdAsync`).
- Migração `Offline_Breeding_Pregnancy_SyncId_RowVersion` (⚠️ **requer aprovação**): `RowVersion` + `SyncId` (`DEFAULT NEWID()`) + índices nas duas tabelas; **se B2**, antes de remover a coluna, `UPDATE AnimalPregnancies SET SyncId = ClientRequestId WHERE ClientRequestId IS NOT NULL` (SQL manual na migração).

| Validação planejada | Esperado |
|---|---|
| Registros existentes | 14 coberturas e 22 gestações com `SyncId` distintos; a gestação com `ClientRequestId` tem `SyncId` = o antigo `ClientRequestId` |
| Registros novos | `SyncId` ≠ `Guid.Empty` |

### 6.2 Fase 2 — Cobertura idempotente com a saída de sêmen

- Confirmar o §3 itens 11 e 12 antes de mudar o código.
- `BreedingEventCreateDto.SyncId` + validação; B11 se confirmado.
- Repositório: `GetBySyncIdAsync`; criação com a saída de sêmen no mesmo `SaveChanges` (`AddSyncableAsync`).
- Service: `SyncId` antes de R1–R4; Condição 3 em R2/R3; B4.

| Validação planejada | Esperado |
|---|---|
| Cobertura IA com `syncId` / reenvio / payload diferente | `201`; 1 cobertura; **1** saída de sêmen |
| Reenvio depois de o animal ser vendido / o sêmen inativado | `201` com a existente |
| Reenvio com a cobertura já "aguardando diagnóstico" (Condição 3) | `201`, não `422` |
| Monta natural | `201`; nenhuma saída de sêmen |
| **Se B4:** cobertura com o sêmen sem doses | `201`; saldo da amostra negativo |
| 20 envios simultâneos (mesmo `syncId`) | Todos `201`; 1 cobertura; 1 saída |

### 6.3 Fase 3 — Diagnóstico idempotente com a gestação

- `BreedingEventStatusUpdateDto.PregnancySyncId` (`Guid?`, validação de vazio); B11 se confirmado.
- Gestação criada pela navegação no mesmo `SaveChanges` do diagnóstico, com `SyncId = pregnancySyncId ?? novo`.
- Repetido com o mesmo resultado → `200` sem gravar; diferente → `409`.

| Validação planejada | Esperado |
|---|---|
| Positivo com `pregnancySyncId` | `200`; 1 gestação com aquele `SyncId` |
| Repetido (mesmo resultado) | `200`; nada gravado; ainda 1 gestação |
| Negativo depois de positivo | `409` |
| 20 envios simultâneos do positivo | Todos `200`; 1 gestação |

### 6.4 Fase 4 — Gestação retroativa e perda

- Retroativa por `SyncId` (B2) com Condição 3 em R3.
- Perda: já perdida → `200` sem gravar; sem `UpdatedAt`.

| Validação planejada | Esperado |
|---|---|
| Retroativa com `syncId` / reenvio / reenvio depois de outra gestação ativa | `201`; 1 gestação; reenvio não recebe `409` |
| Perda / perda repetida / perda de gestação com parto | `200` / `200` sem gravar / `409` |

### 6.5 Fase 5 — Edição da cobertura com LWW

| Validação planejada | Esperado |
|---|---|
| Mais novo / `-03:00` / reenvio / mais antigo / sem `updatedAt` / relógio adiantado | Como nas specs anteriores |
| Edição depois do diagnóstico | `409` (R6) |
| Trocar o sêmen e a data da cobertura por IA (Q2) | A saída de sêmen passa para a amostra nova, com a data nova; saldos das duas amostras corretos |

### 6.6 Fase 6 — Inativações idempotentes

| Validação planejada | Esperado |
|---|---|
| Inativar → inativar de novo (cobertura, gestação) | `204` → `204`; nada gravado no repetido; `UpdatedAt` inalterado |
| Inativar cobertura por IA | Cobertura e saída de sêmen inativas, na mesma transação |
| Inativar cobertura com gestação ativa / gestação com parto ativo | `409` (mantidos) |

### 6.7 Fase 7 — Pull incremental

| Validação planejada | Esperado |
|---|---|
| Pull inicial (coberturas e gestações) | Todos, inclusive inativos |
| Pull × `GET /{id}` | Sem divergências nos campos comuns |
| Isolamento (duas contas) | Cada uma vê só os seus |
| Diagnóstico positivo → pull | A cobertura **e** a gestação nova, cada uma na sua rota |
| Paginação / cursor / `limit` / rotas de lookup | Como nas anteriores |
| Consultas por página | 1 em cada rota |

### 6.8 Fase 8 — Testes

`BreedingEventServiceTests.cs` e `AnimalPregnancyServiceTests.cs` (novos ou existentes — a confirmar). Cobrir: criação idempotente (inclusive Condição 3), saída de sêmen junto com a cobertura, B4, diagnóstico idempotente e com `pregnancySyncId`, diagnóstico diferente → `409`, retroativa idempotente com Condição 3, perda idempotente, LWW, inativações idempotentes, pull (cursor inválido, `hasMore`).

### 6.9 Fase 9 — Documentação

**Esta spec**; **Spec #14** (§8: `BreedingEvent` e `AnimalPregnancy` ✅; §13 questão 1 resolvida para a cobertura, se B4); **`catalogo-erros-api.md`** (coberturas e gestações); **`spec-eventos-reprodutivos.md`** e **`spec-cadastro-retroativo-gestacao.md`** (notas no topo; `ClientRequestId` → `SyncId`, se B2); **Spec #14.2** (saída da cobertura agora gravada na mesma transação).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/BreedingEvent.cs`, `AnimalPregnancy.cs` | `ISyncable`; remover `ClientRequestId` (B2) |
| Infrastructure | `ApplicationDbContext.cs` | `ConfigureSyncable()`; remover o índice do `ClientRequestId` (B2) |
| Infrastructure | `Migrations/*_Offline_Breeding_Pregnancy_SyncId_RowVersion*` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `BreedingEventRepository.cs`, `AnimalPregnancyRepository.cs` | `GetBySyncIdAsync`, criações via `AddSyncableAsync`, gravações conjuntas (Condição 1), `GetChangesAsync` |
| Application | DTOs de criação, diagnóstico, retroativa, perda, edição | `SyncId`/`PregnancySyncId`/`UpdatedAt`; B11 |
| Application | `BreedingEventDto`, `AnimalPregnancyDto` | `SyncId` (+ `pregnancyId`/`pregnancySyncId` na cobertura) |
| Application | `BreedingEventSyncDto`, `AnimalPregnancySyncDto` | **Novos** (B10) |
| Application | Profiles de cobertura e gestação | Mapas dos DTOs de pull |
| Application | `BreedingEventService.cs`, `AnimalPregnancyService.cs`, `SemenSampleMovementService.cs` (criação/inativação da saída passam a ser feitas junto com a cobertura) | Idempotência, Condições 1–3, B4, LWW, pull |
| Api | `BreedingEventsController.cs`, `AnimalPregnanciesController.cs` | Rotas `GET changes` |
| Tests | Testes de cobertura e gestação | Novos |

**Não muda:** `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, parto e crias (14.11), relatórios/KPIs.

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Animal vendido ou sêmen inativado em outro celular antes do sync de uma cobertura **nova** | `409` real (R1, R4) — o app mostra o conflito; o reenvio de uma cobertura já gravada não é afetado (`SyncId` antes) |
| Dois celulares diagnosticam a mesma cobertura com resultados diferentes | O segundo recebe `409` (conflito real, §5 item 4) |
| **Se B4:** saldo de sêmen negativo sem ninguém perceber | O saldo aparece na amostra; o app pode sinalizar; mesmo comportamento do estoque (14.3) |
| Diagnóstico positivo enviado sem `pregnancySyncId` (Swagger, bug do app) | O servidor gera; o app só o conhece pelo pull |
| Remover `ClientRequestId` (B2) quebra um cliente que o envie | Não há cliente usando além do app em desenvolvimento; documentado no catálogo |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | Cobertura por IA **sem dose** (B4) | ✅ Decidida (14/Out/2026): aceita, com saldo negativo |
| Q2 | `PATCH` da cobertura não ajusta a saída de sêmen (§3 item 12) | ✅ Confirmado e **decidido corrigir nesta spec** (14/Out/2026): na Fase 5, o `PATCH` atualiza a amostra e a data da saída no mesmo `SaveChanges` da cobertura |
| Q3 | Editar o diagnóstico (trocar positivo/negativo) | ⏳ Fora — não há rota; o fluxo hoje é inativar e lançar de novo |
