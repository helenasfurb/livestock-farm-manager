# Status derivados do animal — regras para o cálculo no app

**Para:** app (lista de animais offline)
**Origem:** Spec 14.4, decisão N6 — o `GET /api/animals/changes` (`AnimalSyncDto`) traz só o cadastro do animal; os status da listagem são calculados pelo app a partir dos outros pulls.
**Fonte da verdade no servidor:** `ReproductiveStatusResolver`, `ProductiveStatusResolver`, `AnimalSanitaryStatusResolver` (em `Application/Helpers/`), `AnimalRepository.GetReproductiveStatusMapAsync` e `HealthCaseRepository.GetSanitaryFactsMapAsync`. Se uma regra mudar lá, este documento precisa mudar junto.

---

## 1. O que falta no pull de animais

| Campo do `GET /api/animals` | Como obter no app | Seção |
|---|---|---|
| `reproductiveStatus` | coberturas + gestações + partos | 3 |
| `productiveStatus`, `daysInMilk` | lactações | 4 |
| `sanitaryStatus`, `milkWithheldUntil` | casos de saúde (com as aplicações embutidas) | 5 |
| `lastWeightRecord` | pesagens | 6 |
| `lastExitRecord` | já vem: `exitRecords[0]` do próprio `AnimalSyncDto` (lista ordenada por `exitDate` decrescente) | — |

Os demais campos (`name`, `tagNumber`, `propertyTagNumber`, `breed`, `classification`, `isActive`, `photoHash`, `thumbnailUrl`) já vêm iguais no pull.

## 2. Regras gerais

- **Pulls usados:**
  - `GET /api/breeding-events/changes`
  - `GET /api/pregnancies/changes`
  - `GET /api/calvings/changes`
  - `GET /api/lactations/changes`
  - `GET /api/health-cases/changes`
  - `GET /api/animals/weight-records/changes`
- **Calcule sobre o banco local**, que já inclui o que foi registrado offline e ainda está na fila. Assim o status reflete o que o usuário acabou de lançar; se a sincronização rejeitar o evento, ao removê-lo da fila o status se recalcula sozinho.
- **"Hoje" é a data UTC** (o servidor usa `DateTime.UtcNow`). Usando a data local (−03:00), entre 21h e meia-noite o app pode divergir do servidor em um dia nas regras de data (pós-parto, lactação, carência).
- **Enums:** o app monta o `EnumValueDto` (`value` + `label`) com a tabela da seção 7, igual ao servidor.
- **Registros inativos** (`isActive = false`) vêm nos pulls e **não** contam — nem os filhos (aplicações, testes) de um caso inativo.
- **Recalcule ao abrir a lista**, não só quando chega um pull: pós-parto, dias em lactação e carência mudam com o passar dos dias, sem nenhum evento novo.

## 3. Status reprodutivo (`reproductiveStatus`)

**Só para fêmeas adultas:** `classification` = Vaca (`5`) ou Novilha (`2`). Para as demais classificações, `reproductiveStatus = null`.

**Fatos do animal:**

| Fato | Como calcular |
|---|---|
| `hasConfirmedPregnancy` | existe gestação com `isActive` e `status.value == 1` (Gestação confirmada) |
| `lastCalvingDate` | maior `calvingDate` entre os partos com `isActive` (ou nulo) |
| `lastAwaitingBreedingDate` | maior `breedingDate` entre as coberturas com `isActive` e `status.value == 1` (Aguardando diagnóstico) (ou nulo) |

**Regra, na ordem — a primeira que bater vence:**

1. `hasConfirmedPregnancy` → **Prenhe** (`3`)
2. `lastCalvingDate` existe **e** `agora − lastCalvingDate < 60 dias` (diferença em dias **fracionários**, não só a data) **e** (`lastAwaitingBreedingDate` é nulo **ou** `lastAwaitingBreedingDate <= lastCalvingDate`) → **Pós-parto** (`4`)
3. `lastAwaitingBreedingDate` existe → **Aguardando confirmação de prenhez** (`2`)
4. senão → **Vazia** (`1`)

Ou seja: uma cobertura feita **depois** do parto tira a vaca do pós-parto, mesmo antes dos 60 dias.

## 4. Status produtivo (`productiveStatus`) e DEL (`daysInMilk`)

**Só para Vaca (`5`) ou Novilha (`2`)**; nas demais classificações, os dois campos ficam `null`.

Considere só as lactações com `isActive`. As comparações usam **só a data** (sem hora).

- **Em lactação** em uma data de referência: `startDate <= hoje` **e** (`endDate` nulo **ou** `hoje <= endDate`) — intervalo fechado nas duas pontas.
- **Status:**
  1. alguma lactação ativa está em lactação hoje → **Em lactação** (`2`)
  2. existe ao menos uma lactação ativa → **Seca** (`3`)
  3. nenhuma lactação ativa → **Nunca lactou** (`1`)
- **`daysInMilk`:** da lactação que está em lactação hoje (se houver mais de uma, a primeira encontrada):
  - `endDate` nulo → `hoje − startDate` em dias
  - `endDate` preenchido → `endDate − startDate` em dias
  - nunca negativo (mínimo `0`)
  - sem lactação em curso → `null`

> ⚠️ O `LactationDto` do pull traz `isLactating` e `daysInMilk`, mas eles foram calculados **no momento do pull** e envelhecem. Recalcule a partir de `startDate`/`endDate`.

## 5. Status sanitário (`sanitaryStatus`) e carência do leite (`milkWithheldUntil`)

Vale para **todos** os animais. Use os casos de saúde do animal e as aplicações embutidas em cada caso (`medications`).

**Fatos do animal:**

| Fato | Como calcular |
|---|---|
| `hasCaseUnderTreatment` | existe caso com `isActive`, `resolvedAt` nulo e **ao menos uma** aplicação com `isActive` |
| `hasSuspectedCase` | existe caso com `isActive`, `resolvedAt` nulo e **nenhuma** aplicação com `isActive` |
| `milkWithheldUntil` | para cada aplicação com `isActive`, de um caso com `isActive`, e `withdrawalPeriodDays` preenchido: `liberação = data(applicationDate) + withdrawalPeriodDays`. Fique só com as que têm `liberação > hoje` e pegue a **maior**; se não sobrar nenhuma, `null` |

**Regra, na ordem — a primeira que bater vence:**

1. `hasCaseUnderTreatment` → **Em tratamento** (`3`)
2. `milkWithheldUntil` não nulo → **Em carência** (`4`)
3. `hasSuspectedCase` → **Em observação** (`2`)
4. senão → **Saudável** (`1`)

`milkWithheldUntil` vai para a lista com o valor calculado, independente do status — um animal "Em tratamento" pode ter `milkWithheldUntil` preenchido. O filtro "só com leite retido" da listagem usa `milkWithheldUntil != null`.

O limite é inclusivo para liberar: no dia da liberação o leite **já** está liberado (`liberação > hoje` ainda retém; `liberação == hoje` não retém).

### 5.1. Histórico sanitário do animal (tela)

O pull `GET /api/health-cases/changes` traz **de propósito** os casos inativados e, em cada caso, **todas** as aplicações (`medications`) e testes (`tests`), inclusive os inativados (Spec 14.9, H6). É assim que o app fica sabendo que algo foi excluído. Para a tela de histórico ficar igual ao `GET /api/animals/{id}/health-history` do servidor:

- mostre só os casos com `isActive == true`, ordenados por `diagnosisDate` decrescente;
- dentro de cada caso, mostre só as `medications` e os `tests` com `isActive == true`;
- calcule o status do caso e a data de liberação do leite **só com as aplicações ativas** — um caso cuja única aplicação foi excluída volta a ser "Suspeito" (sem `resolvedAt`) ou "Resolvido" (com `resolvedAt`).

**Reativar um caso excluído:** `PATCH /api/health-cases/{id}/reactivate`, sem corpo; responde `200` com o caso (repetir num caso já ativo também responde `200`, sem alterar — seguro para a fila). O caso volta no pull com `isActive: true`, com as aplicações e testes **como estavam** (excluir o caso nunca excluiu os filhos). A partir daí ele volta ao histórico e as aplicações ativas voltam a contar na carência (seção 5).

## 6. Última pesagem (`lastWeightRecord`)

Entre as pesagens do animal com `isActive`, a de maior `recordedAt`. Sem pesagens ativas → `null`.

## 7. Tabela de enums

| Enum | `value` → `label` |
|---|---|
| `ReproductiveStatus` | `1` Vazia · `2` Aguardando confirmação de prenhez · `3` Prenhe · `4` Pós-parto |
| `ProductiveStatus` | `1` Nunca lactou · `2` Em lactação · `3` Seca |
| `SanitaryStatus` | `1` Saudável · `2` Em observação · `3` Em tratamento · `4` Em carência |
| `AnimalPregnancyStatus` (gestação) | `1` Gestação confirmada · `2` Parto realizado · `3` Perda gestacional |
| `ReproductiveEventStatus` (cobertura) | `1` Aguardando diagnóstico · `2` Gestação confirmada · `3` Sem gestação |
| `AnimalClassification` | `1` Bezerro(a) · `2` Novilha · `3` Boi · `4` Touro · `5` Vaca |

## 8. Casos para testar (hoje = 01/11/2026)

**Reprodutivo** (vaca)

| Situação | Esperado |
|---|---|
| Gestação ativa confirmada (mesmo com parto há 10 dias) | Prenhe |
| Parto em 20/10, nenhuma cobertura aguardando | Pós-parto |
| Parto em 20/10, cobertura aguardando de 25/10 | Aguardando confirmação |
| Parto em 20/10, cobertura aguardando de 15/10 (antes do parto) | Pós-parto |
| Parto em 01/08 (mais de 60 dias), nada mais | Vazia |
| Gestação confirmada, mas inativada | ignora a gestação → segue a regra |
| Boi / bezerro | `null` |

**Produtivo** (vaca)

| Situação | Esperado |
|---|---|
| Lactação ativa de 01/10, sem `endDate` | Em lactação, DEL 31 |
| Lactação ativa de 01/10 a 01/11 (termina hoje) | Em lactação, DEL 31 |
| Lactação ativa de 01/08 a 15/10 | Seca, DEL `null` |
| Só lactações inativas | Nunca lactou, DEL `null` |

**Sanitário**

| Situação | Esperado |
|---|---|
| Caso aberto sem aplicação | Em observação, `milkWithheldUntil` `null` |
| Caso aberto com aplicação de 30/10, carência 5 dias | Em tratamento, `milkWithheldUntil` 04/11 |
| Caso encerrado (`resolvedAt`) com aplicação de 30/10, carência 5 dias | Em carência, até 04/11 |
| Caso encerrado com aplicação de 27/10, carência 5 dias (libera 01/11 = hoje) | Saudável, `null` |
| Caso aberto cuja única aplicação foi inativada | Em observação |
| Caso inativado com aplicação ativa de 30/10, carência 5 dias | Saudável, `null` (caso inativo não conta) |
