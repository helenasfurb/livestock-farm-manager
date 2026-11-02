# Spec: Catálogo de Enums

**Módulo:** Transversal (todos os módulos)  
**Versão:** 1.0  
**Data:** 02/Nov/2026  
**Fonte:** `Domain/Enums/` (v0.5.2)  
**Status:** Referência

Lista todos os enums da API, com valor numérico, nome no código e texto exibido ao usuário. Serve de referência para o app e para a documentação do TCC.

---

## 1. Convenções

- **Nome no código em inglês, texto exibido em português.** O texto vem do atributo `[Description("...")]` de cada valor.
- **Na entrada (POST/PATCH e filtros), o enum é enviado como número.** Exemplo: `"gender": 1`. Um valor fora da lista responde `400`.
- **Na saída, o enum vem como objeto** `{ "value": 1, "label": "Fêmea" }` (`EnumValueDto`), já com o texto em português.
- **Rotas de lookup** devolvem a lista `[{ "value", "label" }]` (`LookupDto`) para montar os campos de seleção do app. Os enums sem rota de lookup estão marcados como "—".
- **No banco, o enum é gravado como `int`.** Mudar o texto ou o nome no código não exige migration. Mudar o **número** de um valor existente quebra os dados já gravados e não deve ser feito.
- **Enums derivados** (marcados como *derivado*) nunca são gravados: a API calcula o valor na leitura. Eles aparecem só nas respostas e nos filtros.

---

## 2. Animais

### 2.1 `AnimalGender` — Sexo
Lookup: `GET /api/animals/genders`

| Valor | Nome | Texto |
|---|---|---|
| 0 | `M` | Macho |
| 1 | `F` | Fêmea |

> É o único enum que começa em `0`.

### 2.2 `AnimalClassification` — Classificação
Lookup: `GET /api/animals/classifications`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Calf` | Bezerro(a) |
| 2 | `Heifer` | Novilha |
| 3 | `Steer` | Boi |
| 4 | `Bull` | Touro |
| 5 | `Cow` | Vaca |

> Novilha e Vaca são exclusivas de fêmeas; Boi e Touro, de machos.

### 2.3 `AnimalBreed` — Raça
Lookup: `GET /api/animals/breeds`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Holstein` | Holandesa |
| 2 | `Jersey` | Jersey |
| 3 | `Crossbred` | Híbrida/Mestiça |
| 4 | `BrownSwiss` | Pardo Suíço |
| 5 | `Simmental` | Simental |
| 6 | `DairyGir` | Gir Leiteiro |
| 7 | `Girolando` | Girolando |
| 8 | `DairyGuzerat` | Guzerá Leiteiro |
| 9 | `Sindhi` | Sindi |
| 10 | `Angus` | Angus |

### 2.4 `AnimalPurpose` — Finalidade
Lookup: `GET /api/animals/purposes`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Breeder` | Matriz |
| 2 | `ReplacementHeifer` | Novilha de reposição |
| 3 | `CullCow` | Vaca de descarte |
| 4 | `ForSale` | Venda |
| 5 | `Sire` | Reprodutor |

> `Sire` (Reprodutor) é exclusivo da classificação Touro: `400` no cadastro e `422` na edição. `ForSale` (Venda) vale para qualquer classificação.

### 2.5 `AnimalOrigin` — Origem
Lookup: `GET /api/animals/origins`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `BornOnFarm` | Nascido na propriedade |
| 2 | `Purchased` | Adquirido |

### 2.6 `AnimalExitReason` — Motivo de saída
Lookup: `GET /api/animals/exit-reasons`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Sale` | Venda |
| 2 | `NaturalDeath` | Morte natural |
| 3 | `OwnConsumption` | Consumo próprio |

### 2.7 `BodyConditionScore` — Escore de condição corporal (ECC)
Lookup: `GET /api/animals/body-condition-scores`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `SeverelyThin` | Magreza Severa |
| 2 | `Thin` | Estrutura Óssea Visível |
| 3 | `Ideal` | Estrutura Óssea e Cobertura Bem Distribuídas |
| 4 | `Fleshy` | Cobertura Predominante sobre Estrutura Óssea |
| 5 | `SeverelyObese` | Obesidade Severa |

### 2.8 `ProductiveStatus` — Status produtivo *(derivado)*
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `NeverLactated` | Nunca lactou |
| 2 | `Lactating` | Em lactação |
| 3 | `Dry` | Seca |

> Calculado a partir das lactações. Aparece no detalhe e na listagem de animais.

### 2.9 `ReproductiveStatus` — Status reprodutivo *(derivado)*
Lookup: `GET /api/animals/reproductive-statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Open` | Vazia |
| 2 | `AwaitingConfirmation` | Aguardando confirmação de prenhez |
| 3 | `Pregnant` | Prenhe |
| 4 | `Postpartum` | Pós-parto |

### 2.10 `SanitaryStatus` — Status sanitário *(derivado)*
Lookup: `GET /api/animals/sanitary-statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Healthy` | Saudável |
| 2 | `UnderObservation` | Em observação |
| 3 | `UnderTreatment` | Em tratamento |
| 4 | `InWithdrawal` | Em carência |

> Resumo dos casos de saúde e medicações ativos do animal (`AnimalSanitaryStatusResolver`).

---

## 3. Reprodução

### 3.1 `ReproductionType` — Tipo de cobertura
Lookup: `GET /api/breeding-events/reproduction-types`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `ArtificialInsemination` | Inseminação artificial |
| 2 | `NaturalMating` | Monta natural |

### 3.2 `ReproductiveEventStatus` — Status da cobertura
Lookup: `GET /api/breeding-events/statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `AwaitingDiagnosis` | Aguardando diagnóstico |
| 2 | `Successful` | Gestação confirmada |
| 3 | `Unsuccessful` | Sem gestação |

### 3.3 `DiagnosisMethod` — Método de diagnóstico
Lookup: `GET /api/breeding-events/diagnosis-methods`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Palpation` | Palpação |
| 2 | `Ultrasound` | Ultrassom |

### 3.4 `AnimalPregnancyStatus` — Status da gestação
Lookup: `GET /api/pregnancies/statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Confirmed` | Gestação confirmada |
| 2 | `Calved` | Parto realizado |
| 3 | `LostPregnancy` | Perda gestacional |

### 3.5 `CalfVitalStatus` — Condição da cria ao nascer
Lookup: `GET /api/calvings/calf-vital-statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Live` | Nascido vivo |
| 2 | `Stillborn` | Natimorto |

> Só a cria `Live` gera um animal novo no rebanho.

---

## 4. Produção de leite

### 4.1 `LactationOrigin` — Origem da lactação
Lookup: `GET /api/lactations/origins`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Calving` | Parto |
| 2 | `InitialSeed` | Cadastro inicial |

> `InitialSeed` é a lactação informada no cadastro do animal, sem parto registrado.

### 4.2 `MilkingShift` — Turno da ordenha
Lookup: `GET /api/milk-productions/milkings`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Morning` | Manhã |
| 2 | `Afternoon` | Tarde |
| 3 | `Evening` | Noite |

---

## 5. Sanidade

### 5.1 `DiseaseType` — Tipo de doença
Lookup: `GET /api/health-cases/disease-types`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Mastitis` | Mastite |
| 2 | `Other` | Outra |

### 5.2 `Quarter` — Quartos mamários afetados *(flags)*
Lookup: `GET /api/health-cases/quarters`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `FrontLeft` | Anterior esquerdo |
| 2 | `FrontRight` | Anterior direito |
| 4 | `RearLeft` | Posterior esquerdo |
| 8 | `RearRight` | Posterior direito |

> **Flags:** um caso pode afetar mais de um quarto, e o valor enviado é a **soma** dos quartos. Exemplo: anterior esquerdo + posterior direito = `1 + 8 = 9`. São aceitos valores de `1` a `15`. O campo só pode ser preenchido quando a doença é Mastite.

### 5.3 `MastitisTestType` — Tipo de teste de mastite
Lookup: `GET /api/health-cases/test-types`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `BlackBottomCup` | Fundo preto |
| 2 | `CMT` | CMT |
| 3 | `CCS` | CCS |
| 4 | `Microbiological` | Microbiológico |

### 5.4 `HealthCaseStatus` — Status do caso de saúde *(derivado)*
Lookup: `GET /api/health-cases/statuses`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Suspected` | Em observação |
| 2 | `UnderTreatment` | Em tratamento |
| 3 | `InWithdrawal` | Em carência |
| 4 | `Resolved` | Resolvido |

> Calculado a partir das medicações, do encerramento (`ResolvedAt`) e da carência do leite (`HealthCaseStatusResolver`). Usado no filtro `status` da listagem, que aceita um valor por vez.

### 5.5 `DoseType` — Tipo de dose da vacinação
Lookup: `GET /api/vaccination-events/dose-types`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `FirstDose` | Primeira dose |
| 2 | `Booster` | Reforço |

> Gravado no evento. O padrão é `FirstDose` para um evento sem pai e `Booster` para o reforço gerado a partir de outro evento; o usuário pode alterar.

### 5.6 `VaccinationEventStatus` — Status da vacinação *(derivado)*
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Scheduled` | Agendado |
| 2 | `Overdue` | Vencido |
| 3 | `Applied` | Aplicado |

> Calculado a partir da data prevista e da data de aplicação, comparadas com a data atual (`VaccinationEventStatusResolver`).

---

## 6. Estoque e banco de sêmen

### 6.1 `StockMovementType` — Tipo de movimentação de estoque
Lookup: `GET /api/stock/movement-types`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Input` | Entrada |
| 2 | `Output` | Saída |

### 6.2 `StockMovementReason` — Motivo da movimentação de estoque
Lookup: `GET /api/stock/movement-reasons`

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Purchase` | Compra |
| 2 | `OpeningBalance` | Saldo inicial |
| 3 | `Consumption` | Consumo |
| 4 | `Loss` | Perda |
| 5 | `Adjustment` | Ajuste |

### 6.3 `ValueEntryMode` — Forma de informar o valor
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `UnitPrice` | Unitário |
| 2 | `TotalPrice` | Total |

> Define se a movimentação de estoque informa o preço unitário (`unitPrice`) ou o valor total (`totalValue`).

### 6.4 `StockAlertSeverity` — Nível de alerta do estoque *(derivado)*
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Ok` | OK |
| 2 | `Attention` | Atenção |
| 3 | `Critical` | Crítico |

### 6.5 `SemenMovementType` — Tipo de movimentação de sêmen
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Input` | Entrada |
| 2 | `Output` | Saída |

---

## 7. Usuários

### 7.1 `UserRole` — Papel do usuário na propriedade
Lookup: —

| Valor | Nome | Texto |
|---|---|---|
| 1 | `Admin` | Administrador |
| 2 | `Member` | Membro |

> Usado no cadastro e na edição de membros (`POST /api/users`, `PATCH /api/users/{id}`).

---

## 8. Manutenção desta spec

Ao criar, renomear ou remover um valor de enum em `Domain/Enums/`, atualize esta spec no mesmo PR. Valores novos entram com o próximo número livre; valores antigos nunca são renumerados.
