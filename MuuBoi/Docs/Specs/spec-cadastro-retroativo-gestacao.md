# Spec #13: Cadastro Retroativo de Gestação (Onboarding e Compra de Animal Gestante) — PROPOSTA

**Módulo:** Gestação e Parto — Onboarding / Rastreabilidade
**Versão:** 0.1 (rascunho para discussão)
**Data:** 30/Ago/2026
**Fonte:** Definição de escopo do TCC — entrada em produção com animais já gestantes; compra de vaca gestante
**Status:** 🟡 **Em discussão — NÃO aprovado para implementação.**
**Depende de:** Spec #2 (Entrada de Animais), Spec #5 (Eventos Reprodutivos), Spec #6 (6.1 Gestação / 6.2 Parto / 6.3 Cria)
**Relaciona-se com:** Spec #10 (Registro Genealógico) — a decisão de "pai desconhecido" aqui influencia a modelagem de genealogia lá.

---

## 1. Contexto e Objetivo

Hoje uma `AnimalPregnancy` só existe como **efeito de uma cobertura** (`BreedingEventService.UpdateStatusAsync` com diagnóstico = sucesso → `CreateForBreedingEventAsync`). O FK `AnimalPregnancy.BreedingEventId` é **obrigatório** (1:1 único). Não há endpoint para cadastrar gestação diretamente.

Isso impede dois cenários reais de **onboarding em produção**:

- Uma vaca **da propriedade** já está prenhe quando o sistema entra no ar — a cobertura ocorreu antes de existir registro.
- Uma vaca **comprada já gestante**, muitas vezes **sem informação do touro/sêmen**.

O objetivo é permitir registrar essa gestação **retroativamente**, decidindo como tratar a **ausência da cobertura e do pai**.

---

## 2. Casos de Uso Motivadores

| # | Cenário | Cobertura conhecida? | Pai (touro/sêmen) conhecido? |
|---|---------|----------------------|------------------------------|
| CU-A | Go-live: vaca da propriedade já prenhe | Não (foi antes do sistema) | Às vezes (produtor pode saber) |
| CU-B | **Compra de vaca já gestante, sem info do touro/sêmen** | Não | **Não** |
| CU-C | Compra de vaca gestante **com** info do pai | Não | Sim (o vendedor informou) |

> CU-B é o caso mais restritivo e o que guia a modelagem: **precisamos representar "pai desconhecido" de forma honesta**, sem impedir o cadastro da gestação.

---

## 3. Questão central: **simular** a cobertura/pai ou representar a **ausência**?

Esta é a decisão de fundo. Duas filosofias:

### 3.1 Simular (fabricar dado)
Criar um `BreedingEvent` sintético (data estimada, tipo de reprodução arbitrário) e/ou um "touro desconhecido" para preencher os campos obrigatórios.

### 3.2 Representar a ausência (null / "desconhecido")
Modelar explicitamente que **não houve cobertura registrada** e que o **pai é desconhecido** (campos nulos), permitindo capturar o pai **opcionalmente** quando conhecido (CU-C).

### 3.3 Comparação

| Critério | Simular | Representar ausência |
|----------|---------|----------------------|
| Honestidade do dado | ❌ Cria serviço/pai que não existiram | ✅ Diz a verdade ("desconhecido") |
| Índices reprodutivos (Spec #7) | ❌ Polui taxa de concepção, serviços/concepção com serviços falsos | ✅ Basta excluir gestações sem cobertura |
| Genealogia (Spec #10) | ❌ Pai fabricado engana a filiação | ✅ Pai nulo = "desconhecido", verdadeiro |
| Animal-fantasma no rebanho | ❌ "Touro desconhecido" viraria um `Animal` falso | ✅ Não cria animal |
| Necessidade de flag p/ distinguir real x fake | Sim (inevitável) | Já é distinguível (cobertura nula) |
| Complexidade | Alta (ver §4) | Menor |

### 3.4 Recomendação (§3)
> **Não simular.** Representar a ausência explicitamente (`BreedingEventId = null`, pai nulo) e permitir **captura opcional** do pai quando o produtor souber (CU-C). Fabricar cobertura/pai corrompe justamente os dados que o sistema existe para dar — KPIs reprodutivos e genealogia — e ainda assim exigiria uma flag para separar o que é real. A ausência é um estado legítimo e informativo.

---

## 4. Por que "cadastrar cobertura a partir da gestação" é mais complicado

Uma tentativa natural seria: no endpoint de gestação, criar por baixo um `BreedingEvent` que "gera" a gestação pelo fluxo normal. Na prática, isso **enfraquece várias invariantes** hoje garantidas:

| Invariante atual | Conflito no cadastro retroativo |
|------------------|---------------------------------|
| `ReproductionType` obrigatório (IA ou Monta) | Se o pai é desconhecido (CU-B), não há tipo — exigiria um tipo novo "Desconhecido/Retroativo" ou tornar `SireAnimalId`/`SemenSampleId` nuláveis, quebrando a regra "IA tem sêmen, monta tem touro". |
| IA consome **dose** de sêmen (`SemenSampleMovement`) | Uma IA retroativa **baixaria uma dose real** do estoque — incorreto: a dose não passou pelo sistema. Exigiria pular o movimento (caso especial). |
| RN-18 (bloqueia nova cobertura com diagnóstico pendente) e **RN-19 (bloqueia cobertura para animal prenhe)** | A vaca **já está prenhe** — a própria cobertura retroativa seria **barrada pelas nossas regras**. Exigiria bypass. |
| Gestação só nasce no **diagnóstico = sucesso** | O fluxo retroativo teria que criar a cobertura **e** marcá-la como sucesso **e** gerar a gestação num passo só, furando o workflow de diagnóstico. |

**Conclusão:** derivar uma cobertura a partir da gestação obriga a: (a) tornar touro/sêmen opcionais, (b) pular consumo de dose, (c) burlar RN-18/RN-19, (d) atalhar o diagnóstico. É **mais** código e **mais** exceções — e no fim ainda fabrica um serviço que não existiu. Não compensa.

---

## 5. Abordagem proposta — gestação retroativa **direta**

Coerente com §3.4 e §4: criar a gestação como fato de primeira classe, **sem** cobertura.

### 5.1 Modelo (mudança de nulabilidade + migração)
- `AnimalPregnancy.BreedingEventId` → `int?` (nulável).
- Relação `AnimalPregnancy ⇄ BreedingEvent` vira **opcional**; o índice único vira **filtrado** (`WHERE [BreedingEventId] IS NOT NULL`) — mesmo padrão já usado em `AnimalCalvingCalf.AnimalId` (Spec 6.4).
- `AnimalPregnancyDto.BreedingEventId` → `int?`.

### 5.2 Endpoint novo
```
POST /api/animals/{animalId}/pregnancies        (cadastro retroativo)
```
Entrada (`AnimalPregnancyRetroactiveCreateDto`):
| Campo | Obrigatório | Notas |
|-------|-------------|-------|
| `ConfirmationDate` | Sim | Data em que a prenhez foi/está confirmada. |
| `ExpectedCalvingDate` | Sim | **Informada manualmente** — sem cobertura não há `BreedingDate` para calcular. |
| `SireAnimalId` | Não | Touro do rebanho, se conhecido (CU-C). |
| `SemenSampleId` | Não | Sêmen, se conhecido (CU-C). |
| `Notes` | Não | Ex.: "vaca comprada gestante". |

- Status inicial = `Confirmed`. `BreedingEventId = null`.
- `SireAnimalId`/`SemenSampleId` são **mutuamente exclusivos** e **opcionais** — capturam o pai **sem** fabricar uma cobertura.

### 5.3 Regras de negócio
| # | Regra | Onde |
|---|-------|------|
| RN-01 | O animal deve ser fêmea `Cow`/`Heifer` ativa. | Service |
| RN-02 | O animal **não** pode ter gestação ativa confirmada (`HasActiveConfirmedByAnimalIdAsync`) — evita empilhar. | Service |
| RN-03 | `ExpectedCalvingDate` > `ConfirmationDate`. | DTO/Service |
| RN-04 | Informar **no máximo um** entre `SireAnimalId` e `SemenSampleId`; ambos opcionais. | DTO (IValidatableObject) |
| RN-05 | Se `SireAnimalId` informado, deve ser `Animal` `Bull` ativo; se `SemenSampleId`, amostra ativa. | Service |
| RN-06 | Uma gestação retroativa (`BreedingEventId == null`) **não** consome dose de sêmen mesmo que `SemenSampleId` seja informado — o vínculo é só genealógico. | Service |

### 5.4 Onde guardar o pai capturado (§5.2) — **depende da Spec #10**
Duas opções, a alinhar com a decisão de genealogia:
- **Se Spec #10 for "snapshot" (Alt. B):** o pai vai para as FKs de filiação do `Animal`/`AnimalParentage` — a gestação retroativa só as preenche. Encaixe perfeito.
- **Se Spec #10 for "derivação" (Alt. A):** a derivação não acha pai (não há cobertura). Então esses campos `SireAnimalId?`/`SemenSampleId?` teriam que morar na **própria `AnimalPregnancy`** e a genealogia passaria a ler "cobertura **ou** pai direto da gestação".

> **Observação de projeto:** o cadastro retroativo é um **argumento a favor do snapshot (Spec #10 Alt. B)** — com snapshot, "pai conhecido sem cobertura" é trivial; com derivação pura, ele não tem onde existir.

---

## 6. Genealogia do bezerro nascido de gestação retroativa

- **Mãe:** sempre conhecida (`pregnancy.AnimalId` → `AnimalCalving.AnimalId`). ✅
- **Pai:** `null` (desconhecido) por padrão; preenchido se CU-C informou `SireAnimalId`/`SemenSampleId`.
- Consumidores da genealogia (Spec #10) devem exibir **"pai desconhecido"** quando nulo — nunca fabricar.

---

## 7. Impactos e camadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalPregnancy.cs` | `BreedingEventId` → `int?`; (se §5.4 opção A-derivação) adicionar `SireAnimalId?`/`SemenSampleId?` |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Relação com `BreedingEvent` opcional; índice único **filtrado**; FKs de pai `Restrict` (se aplicável) |
| `Infrastructure/Migrations` | *(nova)* | **Requer aprovação** — nulabilidade + índice filtrado + FKs |
| `Application/DTOs` | `AnimalPregnancyRetroactiveCreateDto` (novo); `AnimalPregnancyDto` (`BreedingEventId` → `int?`) | Criar/ajustar |
| `Application/Interfaces` + `Application/Services` | `IAnimalPregnancyService` / `AnimalPregnancyService` | `CreateRetroactiveAsync` com RN-01..06 |
| `Api/Controllers` | `PregnanciesController` | `POST /api/animals/{animalId}/pregnancies` |
| `Program.cs` | — | Sem novos registros de DI (serviços/repos já existentes) |

---

## 8. O que continua funcionando sem mudança (confirmado no código)

- **Status "Prenhe"** deriva da gestação (`HasActiveConfirmedByAnimalIdAsync`), não da cobertura → correto.
- **RN-19 + autocomplete de cobertura** já filtram por *gestação confirmada ativa* → a vaca com gestação retroativa **não** recebe nova cobertura nem aparece no autocomplete. Consistente.
- **Parto** (`AnimalCalvingService.CreateAsync`) só depende da gestação (status `Confirmed`, `ConfirmationDate`) → gera o bezerro normalmente.
- **Mapeamentos:** `AnimalPregnancyProfile` **não** desreferencia `BreedingEvent` → sem risco de `NullReferenceException` com `BreedingEventId = null`.

---

## 9. Índices zootécnicos (Spec #7)

Gestações com `BreedingEventId == null` **não** têm serviço associado. Métricas baseadas em cobertura (taxa de concepção, serviços/concepção, dias em aberto, intervalo cobertura–concepção) devem **excluí-las** (facilmente distinguíveis pelo FK nulo). Métricas baseadas só em prenhez/parto (nº de gestações, nascimentos) as incluem normalmente.

---

## 10. Questões em aberto (para discutir com o parceiro de TCC)

1. **Local do pai capturado (§5.4):** FKs na `AnimalPregnancy` (derivação) ou snapshot no `Animal` (Spec #10 Alt. B)? — decisão conjunta com a Spec #10.
2. **`ExpectedCalvingDate`:** pedir a data prevista de parto direto, ou pedir a **data estimada de concepção** e calcular com a duração média de gestação?
3. **Marca de origem:** basta inferir "retroativa" por `BreedingEventId == null`, ou vale um campo explícito (`Origin`/`IsRetroactive`) para auditoria/relatórios?
4. **Compra gestante (CU-B):** o cadastro da gestação deve ser um passo **da entrada por compra** (Spec #2) ou um endpoint independente chamado depois?
5. **Perda/parto retroativos:** permitir também registrar perda ou parto já ocorridos de uma gestação retroativa (datas passadas)?

---

## 11. Recomendação preliminar (não vinculante)

1. **Representar a ausência, não simular** (§3.4).
2. **Gestação retroativa direta** com `BreedingEventId` nulável (§5), **não** cobertura fabricada (§4).
3. **Captura opcional do pai** para CU-C, de preferência via o **snapshot de genealogia** da Spec #10 — o que também resolve a compra de vaca gestante com pai conhecido sem inventar serviço.
4. Para **CU-B** (pai desconhecido), pai = `null` e pronto: é a informação correta.

---

## 12. Fora do Escopo desta Spec

- **Modelagem final da genealogia** → Spec #10 (esta spec apenas alimenta os campos de pai).
- **Cálculo de índices zootécnicos** → Spec #7 (aqui só se define a exclusão das gestações sem cobertura).
- **Fluxo normal de cobertura → gestação** → Specs #5 e #6.
- **Entrada de animais por compra (dados gerais)** → Spec #2.
