# Spec: Controle Sanitário — Tratamentos / Doenças (`HealthCase`)

**Módulo:** Controle Sanitário → Tratamentos (fluxo curativo)
**Versão:** 1.0
**Data:** 07/Set/2026
**Fonte:** Spec de análise "Controle de Tratamentos (Doenças)" (offline-first) — reenquadrada para o padrão server-side do MuuBoi.
**Status:** Especificada (não implementada)
**Depende de:** Spec #1 (Animais) — `Animal`, tenant/`BaseEntity`. **Reaproveita e estende** `AnimalMedication` (já existente); o fluxo de tratamento grava o medicamento como **nome livre** (`MedicationName`), sem catálogo (D13). Par complementar da spec **Controle Sanitário — Vacinação** (`VaccinationEvent`, preventivo).

> **Decisões de escopo desta spec (07/Set):**
> - **Server-side agora** (`Id int`, `PropertyId`, `BaseEntity`, soft delete, `ExceptionMiddleware`, resolvers como **helpers estáticos** em `Application/Helpers/`). Offline-first fica como **evolução futura aditiva** (§8) — mesmo racional das specs de Vacinação (D9) e 11.1.
> - **`MedicationUse` não é entidade nova**: é o **`AnimalMedication` existente estendido** com `HealthCaseId int?` (nulo = uso avulso). Evita duas fontes de verdade para "medicamento com carência no nível do animal".
> - **`WithdrawalPeriodDays` (existente) é a carência do leite** no v1 — único eixo de carência calculado agora.
> - **Uma linha de `AnimalMedication` = uma aplicação** (append). Carência resolvida por `MAX(data da aplicação + carência)` sobre as linhas — sem agrupar por medicamento.
> - **`AffectedQuarters`** mora como **coluna `int?` de flags** na própria `HealthCase` (sem owned type — o projeto não usa `OwnsOne`).

---

## 1. Contexto e Objetivo

O eixo sanitário é ortogonal ao produtivo e ao reprodutivo. A vacinação (`VaccinationEvent`) cobre o **preventivo**; esta spec cobre o **curativo**: registro de uma ocorrência de doença em um animal (`HealthCase`), a medicação aplicada e o acompanhamento da **carência do leite** (retenção do leite no tanque).

Foco em **mastite** (tela/histórico dedicados), com suporte a outras doenças via nome livre. O **status sanitário** do animal é **derivado na leitura** e aparece na listagem de animais e na single, sempre acompanhado de "até quando o leite fica retido".

Princípios (herdados do MuuBoi e das specs sanitárias):

- **Nunca armazenar estado derivável** — status/carência resolvidos na leitura, por helpers estáticos (padrão `ReproductiveStatusResolver`, `VaccinationEventStatusResolver`).
- **Sem jobs agendados** — a virada "em carência → liberado" é derivada comparando `hoje` (relógio) com a data de liberação.
- **Só persistir fatos observados** — sem campos-espelho. A carência é informada da bula **no momento do uso** (fato imutável da aplicação).
- **Retroativo é o caso normal** — onboarding registra casos já encerrados (datas passadas).
- **Ortogonalidade estrita** — o eixo de tratamento referencia apenas `AnimalId`; não infere nem escreve em produção/reprodução.

**Convenção de tempo:** granularidade de **dia**. Datas são `DateTime` (datetime2) comparadas por `.Date` — mesmo padrão de `VaccinationEvent`. Carência é contada em **dias**; quando a bula der fração de dia (ex.: 180h = 7,5 dias), **arredondar para cima** (8 dias) por segurança do leite — o valor já entra arredondado no `WithdrawalPeriodDays`.

---

## 2. Escopo

**Dentro:**
- Entidade **`HealthCase`** (um caso = uma ocorrência de doença em um animal), com discriminador `DiseaseType { Mastitis, Other }`.
- **Dados de mastite**: `AffectedQuarters` (flags `Quarter`, coluna anulável na `HealthCase`) + coleção **`MastitisTest`** (1:N, `Result` string livre).
- **Medicação do caso** via **`AnimalMedication` estendido** (`HealthCaseId int?`), com carência do leite (`WithdrawalPeriodDays`) copiada por aplicação.
- **Carência do leite resolvida na leitura** (`MAX(aplicação + carência)`), no nível do caso e do animal.
- **Status derivado do caso** (`HealthCaseStatus`) e **status sanitário do animal** (`SanitaryStatus` + `MilkWithheldUntil`), ambos por helper estático; o sanitário é **bulk** na listagem de animais (set-based, sem N+1).
- **Telas**: mastite (dedicada), doenças (genérica), histórico sanitário do animal; "leite retido agora" via **filtro na listagem de animais** (não rota dedicada — D14).
- CRUD com **soft delete** e isolamento de tenant no repositório.

**Fora (→ §9 / futuro):**
- **Uso avulso de medicação pela UI** (`HealthCaseId = null`) — habilitado pelo modelo e já servido pelo `AnimalMedicationsController` existente; o fluxo desta spec cria sempre via `HealthCase`.
- **Carência de carne/outros eixos** — `WithdrawalPeriodDays` é a carência do leite no v1.
- **Dashboard de leite descartado por carência** (JOIN de leitura com o eixo produtivo) — Q4 (fora do v1).
- **Notificação local "carência termina hoje"** (client-side) — Q5 (fora do v1).
- **Descarte por quarto** (litros por teto) — o eixo produtivo mede no nível animal/tanque; v1 não calcula volume descartado por quarto.
- **Sincronização offline-first / upsert idempotente** — evolução futura aditiva (§8).

---

## 3. Decisões

| # | Decisão | Origem / Motivo |
|---|---------|-----------------|
| D1 | **Entidade central `HealthCase`** (um caso = uma ocorrência), discriminador `DiseaseType { Mastitis, Other }`; `DiseaseName` obrigatório quando `Other`. | Casa 1:1 com a leitura de negócio ("um caso de mastite da vaca 42"); discriminador único evita duas árvores de entidades para o mesmo ciclo de vida. |
| D2 | **Sem tabela `MastitisDetail`.** `AffectedQuarters` (flags) é **coluna `int?`** na `HealthCase`; **`MastitisTest`** é tabela 1:N própria. | Normalizar `AffectedQuarters` seria um join para uma coluna. Testes ganham tabela (não JSON) porque o dashboard agrega/filtra por `TestType`/`Result`. Projeto não usa owned type. |
| D3 | **`MedicationUse` = `AnimalMedication` estendido** com `HealthCaseId int?` (nulo = avulso). Não cria entidade paralela. | `AnimalMedication` já é "medicamento aplicado a um animal com carência copiada por uso". Evita duas fontes de verdade — coerente com o projeto ter depreciado `AnimalVaccination` em vez de duplicar. |
| D4 | **Carência do leite = `WithdrawalPeriodDays`** (campo existente), informada da bula no momento do uso (imutável). | Único eixo de carência no v1. Guardar no registro preserva "variações por laboratório" como fato imutável da aplicação. |
| D5 | **Uma linha de `AnimalMedication` = uma aplicação** (append). Carência = **`MAX(ApplicationDate + WithdrawalPeriodDays)`** sobre as linhas — **sem agrupar** por medicamento. | A linha de maior liberação domina naturalmente o `MAX` (cobre múltiplos medicamentos com carências diferentes). Append converge offline (§8) e dá log de auditoria de graça (resolve Q3). |
| D6 | **Boundary de carência inclusivo**: `hoje >= MilkLiberationDate ⇒ liberado`. | Ordenhas de `ApplicationDate` até `MilkLiberationDate - 1` descartadas; no dia da liberação já vai pro tanque. Coerente com "carência de N dias" da bula (resolve Q1). |
| D7 | **Encerramento explícito por um único campo `ResolvedAt`** (produtor marca "encerrei o caso / terminei de medicar"). Não há `TreatmentEndedDate` separado. | As aplicações não distinguem "última dose" de "próxima dose pendente" (intenção, não fato); derivar reintroduz "acabou vs. falta dose". Um campo só evita a redundância término × alta (resolve Q2). |
| D8 | **Status do caso e status sanitário do animal são derivados, nunca gravados**, resolvidos por **helper estático** (`HealthCaseStatusResolver`, `AnimalSanitaryStatusResolver`). | Padrão real do projeto (helpers estáticos, não interfaces `I...Resolver`). A transição temporal acontece "sozinha" na leitura, sem job nem campo stale. |
| D9 | **Segurança do leite é fato do animal, não do caso**: `AnimalMilkWithheldUntil = MAX(MilkLiberationDate)` sobre as `AnimalMedication` do animal com liberação futura. | Desacopla "curado" de "leite seguro" e cobre medicação avulsa. Um animal pode estar de alta (`ResolvedAt`) e ainda em carência. |
| D10 | **Server-side agora** (`Id int`, `PropertyId`, spawn no serviço). Offline-first = evolução futura aditiva (§8). | Implementável já; alinha com Vacinação (D9) e 11.1. |
| D11 | **Ortogonalidade estrita**: o eixo referencia só `AnimalId`; não escreve em `Lactation`/`MilkProduction` nem em entidades reprodutivas; não cria drying-off automático. | Mastite severa que leve a secar o animal é decisão **manual** no fluxo produtivo. Correlação carência × leite descartado, se desejada, é JOIN de leitura (Q4), nunca acoplamento de escrita. |
| D12 | **Renomear `AnimalMedication.StartDate → ApplicationDate`.** `EndDate` permanece (legado do curso avulso; consumido pelo dashboard). | Com "uma linha = uma aplicação" (D5), `StartDate` (de curso) engana. Rename torna a semântica explícita, ao custo de tocar os consumidores avulsos existentes (§7). |
| D13 | **Medicamento é nome livre (`MedicationName` string) no fluxo de tratamento** — sem catálogo/filtro. `MedicationId` vira anulável (só fluxo avulso legado). | Não haverá filtro/catálogo de medicamentos no tratamento; a `WithdrawalPeriodDays` é informada na aplicação (sem default de catálogo). Alinha com a análise original (`MedicationName: string`). |
| D14 | **"Leite retido agora" é filtro na listagem de animais**, não rota dedicada. `AnimalFilterDto` ganha `SanitaryStatus?` (badge) e `MilkWithheldOnly?` (bool). | Reaproveita a listagem que já resolve o status sanitário em bloco. `milkWithheldOnly` é a lista fiel da ordenha (inclui `Em tratamento`); o filtro por badge não incluiria esses (precedência). |

---

## 4. Histórias de Usuário

### US-01 — Registrar um caso de doença
> **Como** produtor, **quero** registrar uma ocorrência de doença (mastite ou outra) em um animal, **para** iniciar o acompanhamento.

**Critérios de aceite:**
- Informo `animalId`, `diseaseType` e `diagnosisDate` (`<= hoje`).
- Se `diseaseType = Other`, `diseaseName` é obrigatório; se `Mastitis`, `diseaseName` fica nulo e posso informar `affectedQuarters`.
- `affectedQuarters` só é aceito quando `diseaseType = Mastitis`.
- O caso nasce **`Suspected`** (em observação) enquanto não houver medicação nem alta.

### US-02 — Registrar aplicação de medicamento no caso
> **Como** produtor, **quero** registrar cada medicamento aplicado ao animal dentro do caso, **para** controlar a carência do leite.

**Critérios de aceite:**
- Numa rota do caso (`POST /api/health-cases/{id}/medications`), informo `medicationName` (texto livre), `applicationDate` (`<= hoje`) e a `withdrawalPeriodDays` (carência do leite). Sem catálogo, a carência é informada na aplicação (default `0` se omitida).
- Cada aplicação é uma linha (append); a **liberação do leite** do caso passa a ser `MAX(applicationDate + withdrawalPeriodDays)` sobre as aplicações.
- Com ao menos uma aplicação e sem `resolvedAt`, o caso fica **`UnderTreatment`**.

### US-03 — Registrar teste de mastite
> **Como** produtor, **quero** registrar testes (caneca telada, CMT, CCS, microbiológico) do caso de mastite, **para** apoiar diagnóstico e histórico.

**Critérios de aceite:**
- Numa rota do caso (`POST /api/health-cases/{id}/tests`), informo `testType`, `result` (texto livre: `Alterado`, `++`, `350 mil cél/mL`, `S. aureus`) e `testDate` (`<= hoje`).
- Testes ficam no nível do caso (não por quarto).

### US-04 — Encerrar o caso
> **Como** produtor, **quero** marcar o caso como encerrado, **para** firmar a carência e fechar o acompanhamento.

**Critérios de aceite:**
- `PATCH` do caso com `resolvedAt` firma a liberação (deixa de ser provisória). Com `resolvedAt` preenchido e `hoje < caseLiberationDate`, o caso fica **`InWithdrawal`** (leite ainda retido); quando `hoje >= caseLiberationDate`, fica **`Resolved`**.
- Para caso **sem** medicação, `resolvedAt` leva direto de `Suspected` a `Resolved`.
- Enquanto `resolvedAt` for nulo e houver medicação, o caso fica **`UnderTreatment`** (liberação provisória — novas doses podem empurrá-la).

### US-05 — Consultar status sanitário do animal e leite retido
> **Como** produtor, **quero** ver o status sanitário de cada animal e até quando o leite fica retido, **para** decidir na ordenha o que vai pro tanque.

**Critérios de aceite:**
- Na listagem de animais e na single vejo o **badge `SanitaryStatus`** e, quando houver, **`milkWithheldUntil`**.
- Tenho a lista "leite retido agora" **filtrando a listagem de animais** por `milkWithheldOnly=true` (animais com `milkWithheldUntil` no futuro).
- A single mostra o **badge sanitário** em destaque; o **histórico de `HealthCase`** vem do endpoint próprio `GET /api/animals/{id}/health-history` (não embutido no `AnimalDto` — mesmo padrão do histórico de vacinação).

### US-06 — Corrigir/excluir
> **Como** produtor, **quero** editar ou remover caso/aplicação/teste lançados errado, **para** manter os dados corretos.

**Critérios de aceite:**
- `PATCH` altera só os campos enviados (reaplica validações). `DELETE` inativa (soft delete) e some das listagens/histórico ativos.

---

## 5. Casos de Uso

### CU-01 — Criar caso
1. `POST /api/health-cases` com `HealthCaseCreateDto`.
2. Valida DTO: `animalId`, `diseaseType`, `diagnosisDate <= hoje`; `diseaseName` obrigatório sse `Other`; `affectedQuarters` só sse `Mastitis`. Inválido → `400`.
3. Valida existência/tenant de `Animal` → inexistente → `404`.
4. Cria `HealthCase` (`Suspected`). Retorna `201` com status derivado.

### CU-02 — Registrar aplicação de medicamento
1. `POST /api/health-cases/{id}/medications` com `MedicationUseCreateDto`.
2. Carrega o caso `{id}` (tenant). Inexistente → `404`.
3. Valida `medicationId` (tenant) → `404`; `applicationDate <= hoje` → `422`.
4. `withdrawalPeriodDays` = enviado, ou `0`. `medicationName` é texto livre (sem validação de catálogo).
5. Cria `AnimalMedication` com `HealthCaseId = id`, `AnimalId = case.AnimalId`, `MedicationName`, `ApplicationDate = applicationDate`. Retorna `201` com `milkLiberationDate` derivado.

### CU-03 — Registrar teste de mastite
1. `POST /api/health-cases/{id}/tests` com `MastitisTestCreateDto`.
2. Carrega o caso. Inexistente → `404`.
3. Valida `testType`, `result` (obrigatório), `testDate <= hoje` → `422`.
4. Cria `MastitisTest`. Retorna `201`.

### CU-04 — Editar caso / encerrar / dar alta
1. `PATCH /api/health-cases/{id}` com `HealthCaseUpdateDto` (`DiseaseName?`, `DiagnosisDate?`, `AffectedQuarters?`, `ResolvedAt?`, `Notes?`).
2. Reaplica validações dos campos enviados. Retorna `200` com status recalculado. Inexistente → `404`.

### CU-05 — Consultar casos (telas mastite / doenças)
- `GET /api/health-cases?diseaseType=Mastitis` → tela de mastite (com `affectedQuarters`, badge, liberação).
- `GET /api/health-cases` (filtro genérico) → tela de doenças.
- `GET /api/health-cases/{id}` → detalhe com medicações + testes. Inexistente → `404`.

### CU-06 — Histórico sanitário do animal
- `GET /api/animals/{id}/health-history` → `[HealthCaseListItemDto]` do animal (`DiagnosisDate desc`). Animal inexistente → `404`.

### CU-07 — Leite retido agora / status sanitário (filtro na listagem)
- `GET /api/animals?milkWithheldOnly=true` → animais com `MilkWithheldUntil` no futuro (a lista prática da ordenha; inclui os `Em tratamento`).
- `GET /api/animals?sanitaryStatus=InWithdrawal` (ou outro) → filtra pelo badge sanitário derivado. Ambos resolvidos set-based sobre o mesmo mapa de status (sem N+1). Ver D14.

### CU-08 — Inativar
- `DELETE /api/health-cases/{id}` → soft delete → `204`. Já inativo → `409`. (Idem `medications/{medId}` e `tests/{testId}`.)

---

## 6. Especificação Técnica

### 6.1 Entidade `HealthCase`
> `Domain/Models/HealthCase.cs` — o caso (átomo de escrita).

```csharp
public class HealthCase : BaseEntity, ITenantEntity
{
    [Required]
    public int AnimalId { get; set; }

    public DiseaseType DiseaseType { get; set; }        // Mastitis | Other (D1)

    [MaxLength(100)]
    public string? DiseaseName { get; set; }            // obrigatório quando Other; nulo quando Mastitis

    public DateTime DiagnosisDate { get; set; }         // suspeita/diagnóstico, <= hoje

    public Quarter? AffectedQuarters { get; set; }      // flags; só p/ mastite (coluna int? esparsa) (D2)

    public DateTime? ResolvedAt { get; set; }           // encerramento explícito do caso; firma a carência (nulo = em aberto) (D7)

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public Animal? Animal { get; set; }
    public ICollection<MastitisTest>? Tests { get; set; }
    public ICollection<AnimalMedication>? Medications { get; set; }   // as aplicações do caso (HealthCaseId)
}
```

### 6.2 Extensão de `AnimalMedication` (o "MedicationUse")
> `Domain/Models/AnimalMedication.cs` — **estender**, não criar entidade nova (D3).

```csharp
// Adicionar ao AnimalMedication existente:
public int? HealthCaseId { get; set; }     // nulo = uso avulso; preenchido = medicação do caso (D3)
public HealthCase? HealthCase { get; set; }

public string? MedicationName { get; set; }  // nome livre do medicamento (fluxo de tratamento, sem catálogo) (D13)

// MedicationId passa a ser anulável — usado só pelo fluxo avulso legado (catálogo).
public int? MedicationId { get; set; }

// Renomear (migração): StartDate -> ApplicationDate
public DateTime ApplicationDate { get; set; }   // data da aplicação (antes StartDate) (D5)
```

> **Rename `StartDate → ApplicationDate` (D12):** como cada linha passa a ser **uma aplicação** (D5), o nome `StartDate` (de curso) engana. Renomeamos a coluna para `ApplicationDate`. É um rename que toca os consumidores **avulsos existentes** (DTOs/service/repository/dashboard) — ver §6.9 e §7.
>
> **Medicamento como nome livre (D13):** no fluxo de tratamento o medicamento é uma **string livre** (`MedicationName`), sem catálogo/filtro. `MedicationId` fica **anulável** e continua servindo apenas o fluxo avulso legado (catálogo `Medication`). Como não há catálogo aqui, a `WithdrawalPeriodDays` é sempre informada na aplicação (sem default de catálogo).
>
> **Semântica no fluxo de tratamento (D5):** `EndDate` **não é usado** para carência (permanece para o curso avulso legado — o dashboard ainda o consulta). `WithdrawalPeriodDays` é a **carência do leite** (D4). `Diagnosis` (string) fica redundante quando há `HealthCaseId` — mantido nulável para o uso avulso legado.
>
> **Derivado por registro:** `MilkLiberationDate = ApplicationDate + WithdrawalPeriodDays`.

### 6.3 Entidade `MastitisTest`
> `Domain/Models/MastitisTest.cs` — coleção 1:N do caso.

```csharp
public class MastitisTest : BaseEntity, ITenantEntity
{
    [Required]
    public int HealthCaseId { get; set; }

    public MastitisTestType TestType { get; set; }      // BlackBottomCup | CMT | CCS | Microbiological

    [Required, MaxLength(200)]
    public string Result { get; set; } = string.Empty;  // texto livre (Alterado, ++, 350 mil cél/mL, S. aureus)

    public DateTime TestDate { get; set; }              // <= hoje

    public Guid PropertyId { get; set; }

    public HealthCase? HealthCase { get; set; }
}
```

### 6.4 Enums
> `Domain/Enums/` — nomes em **inglês**, `[Description]` em **português** (convenção do projeto).

```csharp
public enum DiseaseType
{
    [Description("Mastite")] Mastitis = 1,
    [Description("Outra")]   Other = 2
}

[Flags]
public enum Quarter
{
    [Description("Anterior esquerdo")]  FrontLeft = 1,
    [Description("Anterior direito")]   FrontRight = 2,
    [Description("Posterior esquerdo")] RearLeft = 4,
    [Description("Posterior direito")]  RearRight = 8
}

public enum MastitisTestType
{
    [Description("Fundo preto")]    BlackBottomCup = 1,
    [Description("CMT")]            CMT = 2,
    [Description("CCS")]            CCS = 3,
    [Description("Microbiológico")] Microbiological = 4
}

// Derivados — NÃO gravados (produzidos pelos resolvers na leitura):
public enum HealthCaseStatus
{
    [Description("Em observação")] Suspected = 1,
    [Description("Em tratamento")] UnderTreatment = 2,
    [Description("Em carência")]   InWithdrawal = 3,
    [Description("Resolvido")]     Resolved = 4
}

public enum SanitaryStatus
{
    [Description("Saudável")]      Healthy = 1,
    [Description("Em observação")] UnderObservation = 2,
    [Description("Em tratamento")] UnderTreatment = 3,
    [Description("Em carência")]   InWithdrawal = 4
}
```

### 6.5 Resolvers (helpers estáticos)
> `Application/Helpers/` — mesma fonte de verdade para leitura de 1 e de N (padrão `ReproductiveStatusResolver`).

**`HealthCaseStatusResolver`** — status do caso (D8):
```csharp
// caseLiberationDate = MAX(ApplicationDate + WithdrawalPeriodDays) sobre as AnimalMedication ativas do caso (null se nenhuma).
public static HealthCaseStatus Resolve(
    bool hasMedication,
    DateTime? resolvedAt,
    DateTime? caseLiberationDate,
    DateTime utcNow)
{
    if (!hasMedication)
        return resolvedAt.HasValue ? HealthCaseStatus.Resolved : HealthCaseStatus.Suspected;

    // medicado, ainda não encerrado
    if (!resolvedAt.HasValue)
        return HealthCaseStatus.UnderTreatment;

    // encerrado: carência inclusiva (D6)
    if (caseLiberationDate.HasValue && utcNow.Date < caseLiberationDate.Value.Date)
        return HealthCaseStatus.InWithdrawal;

    return HealthCaseStatus.Resolved;
}
```

> **Papel do `ResolvedAt` (campo único, D7):** é o **encerramento explícito** do caso. Enquanto nulo e havendo medicação, o caso é `UnderTreatment` (liberação provisória — novas doses empurram a carência). Ao ser preenchido, **firma a carência**: o status passa a `InWithdrawal` (se `hoje < caseLiberationDate`) ou `Resolved` (se `hoje >= caseLiberationDate`). No caminho **sem medicação**, leva direto de `Suspected` a `Resolved`. **Consequência:** preencher `ResolvedAt` durante a carência **não** mostra `Resolved` na hora — o badge fica `InWithdrawal` até o leite liberar. Ou seja, `ResolvedAt` = "produtor encerrou o caso", não "leite já liberado". As aplicações sozinhas **não** distinguem "última dose" de "próxima dose pendente" (intenção, não fato) — por isso o encerramento é explícito.

**`AnimalSanitaryStatusResolver`** — status sanitário do animal + leite retido (D8/D9). Função pura por animal; o serviço reúne os fatos set-based (padrão `GetReproductiveStatusMapAsync`):
```csharp
// milkWithheldUntil = MAX(ApplicationDate + WithdrawalPeriodDays) sobre as AnimalMedication ativas do animal
//                     cuja liberação é futura (> hoje); null se nenhuma reter leite.
public static SanitaryStatus Resolve(
    bool hasCaseUnderTreatment,
    DateTime? milkWithheldUntil,
    bool hasSuspectedCase)
{
    if (hasCaseUnderTreatment)            return SanitaryStatus.UnderTreatment;
    if (milkWithheldUntil.HasValue)       return SanitaryStatus.InWithdrawal;
    if (hasSuspectedCase)                 return SanitaryStatus.UnderObservation;
    return SanitaryStatus.Healthy;
}
```
> Precedência do badge = ordem do `if` (do mais acionável ao menos). `milkWithheldUntil` é devolvido ao lado do status (não é só insumo do badge — vira campo do DTO).

### 6.6 DTOs
> `Application/DTOs/` — enums como `EnumValueDto { Value, Label }` via `GetDescription()`; datas com `DateFormatConverter`.

- **`HealthCaseCreateDto`** — `AnimalId` (`[Required]`), `DiseaseType` (`[Required]`), `DiseaseName?`, `DiagnosisDate` (`[Required]`), `AffectedQuarters?`, `Notes?`. `IValidatableObject`: `DiseaseName` obrigatório sse `Other`; `AffectedQuarters` nulo sse não-`Mastitis`; `DiagnosisDate <= hoje`.
- **`HealthCaseUpdateDto`** (PATCH parcial) — `DiseaseName?`, `DiagnosisDate?`, `AffectedQuarters?`, `ResolvedAt?`, `Notes?` (null = não altera; datas `<= hoje` quando presentes).
- **`MedicationUseCreateDto`** (append no caso) — `MedicationName` (`[Required]`, texto livre), `ApplicationDate` (`[Required]`, `<= hoje`), `WithdrawalPeriodDays?` (default `0`), `Dose?`, `Responsible?`. (Mapeia p/ `AnimalMedication`: `MedicationName`, `ApplicationDate`, `HealthCaseId` da rota.)
- **`MedicationUseDto`** (resposta) — `Id`, `MedicationName`, `ApplicationDate`, `WithdrawalPeriodDays`, **`MilkLiberationDate`** (derivado), `Dose?`, `Responsible?`.
- **`MastitisTestCreateDto`** — `TestType` (`[Required]`), `Result` (`[Required]`), `TestDate` (`[Required]`, `<= hoje`).
- **`MastitisTestDto`** — `Id`, `TestType` (`EnumValueDto`), `Result`, `TestDate`.
- **`HealthCaseDto`** (detalhe) — `Id`, `AnimalId`, `AnimalName`, `AnimalTagNumber`, `DiseaseType` (`EnumValueDto`), `DiseaseName`, `DiagnosisDate`, `AffectedQuarters` (`[EnumValueDto]` — flags decompostas), `Status` (`EnumValueDto`, derivado), **`CaseLiberationDate?`** (derivado), `ResolvedAt?`, `Notes?`, `Medications` (`[MedicationUseDto]`), `Tests` (`[MastitisTestDto]`), `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`HealthCaseListItemDto`** — `Id`, `AnimalId`, `AnimalName`, `AnimalTagNumber`, `DiseaseType` (`EnumValueDto`), `DiseaseName`, `DiagnosisDate`, `Status` (`EnumValueDto`), `MilkLiberationDate?`, `AffectedQuarters` (`[EnumValueDto]`, p/ mastite).
- **`HealthCaseFilterDto`** — `DiseaseType?`, `AnimalId?`, `Status?`, `DateFrom?`, `DateTo?`, `IsActive?`.
- **Adições a DTOs de Animal:**
  - `AnimalListItemDto` **+=** `SanitaryStatus` (`EnumValueDto?`), `MilkWithheldUntil` (`DateTime?`).
  - `AnimalDto` (single) **+=** `SanitaryStatus`, `MilkWithheldUntil` (o histórico **não** é embutido — vem do endpoint `health-history`, como o de vacinação).
  - `AnimalFilterDto` **+=** `SanitaryStatus?` (filtro por badge) e `MilkWithheldOnly?` (bool; só animais com carência futura) (D14).

Mapeamento em `Application/Mappings/HealthCaseProfile.cs`. `Status`, `CaseLiberationDate`, `MilkLiberationDate` e `MilkWithheldUntil` são preenchidos no service (resolver + memória, sobre queries set-based); DTOs simples passam pelo AutoMapper.

#### 6.6.1 Convenção de enums no payload (front)

A API **não** usa `JsonStringEnumConverter` — nos corpos de requisição, todo enum é enviado pelo **valor numérico** (não pelo nome). Valores:

- `DiseaseType`: `Mastitis = 1`, `Other = 2`.
- `MastitisTestType`: `BlackBottomCup = 1`, `CMT = 2`, `CCS = 3`, `Microbiological = 4`.

**`AffectedQuarters` é `[Flags]`** — envie a **soma (OR bit a bit)** dos quartos afetados:

| Quarto | Valor |
|--------|-------|
| `FrontLeft` (anterior esquerdo)  | `1` |
| `FrontRight` (anterior direito)  | `2` |
| `RearLeft` (posterior esquerdo)  | `4` |
| `RearRight` (posterior direito)  | `8` |

Exemplos: só anterior esquerdo → `1`; os dois anteriores → `1+2 = 3`; anterior esquerdo + posterior direito → `1+8 = 9`; todos → `15`.

**Body — mastite** (`POST /api/health-cases`):
```json
{
  "animalId": 42,
  "diseaseType": 1,
  "diagnosisDate": "2026-09-07",
  "affectedQuarters": 9,
  "notes": "Mastite clínica na ordenha da manhã"
}
```

**Body — outra doença** (`diseaseName` obrigatório; **sem** `affectedQuarters`):
```json
{
  "animalId": 42,
  "diseaseType": 2,
  "diseaseName": "Pododermatite",
  "diagnosisDate": "2026-09-07"
}
```

> Validação: `affectedQuarters` só é aceito com `diseaseType = 1` (Mastitis) → senão `400`. As respostas continuam devolvendo enums como `EnumValueDto { value, label }` (e `AffectedQuarters` já **decomposto** em `[{value,label}]`), então o front lê rótulo pronto e só precisa somar na escrita. Os lookups (`/quarters`, `/disease-types`, `/test-types`, `/statuses`, `/sanitary-statuses`) devolvem `{value,label}` para montar os selects.

### 6.7 Endpoints da API
> Auth: Bearer Token obrigatório.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/health-cases` | Criar caso | `201 HealthCaseDto` / `400` / `404` |
| `GET` | `/api/health-cases` | Listar (filtro; `diseaseType=Mastitis` p/ tela de mastite) | `200 [HealthCaseListItemDto]` |
| `GET` | `/api/health-cases/{id}` | Detalhe (medicações + testes) | `200 HealthCaseDto` / `404` |
| `PATCH` | `/api/health-cases/{id}` | Editar / encerrar / dar alta | `200 HealthCaseDto` / `400` / `404` |
| `DELETE` | `/api/health-cases/{id}` | Inativar (soft delete) | `204` / `404` / `409` |
| `POST` | `/api/health-cases/{id}/medications` | Registrar aplicação (append) | `201 MedicationUseDto` / `404` / `422` |
| `DELETE` | `/api/health-cases/{id}/medications/{medId}` | Inativar aplicação | `204` / `404` |
| `POST` | `/api/health-cases/{id}/tests` | Registrar teste de mastite | `201 MastitisTestDto` / `404` / `422` |
| `DELETE` | `/api/health-cases/{id}/tests/{testId}` | Inativar teste | `204` / `404` |
| `GET` | `/api/animals/{id}/health-history` | Histórico sanitário do animal | `200 [HealthCaseListItemDto]` / `404` |
| `GET` | `/api/animals?milkWithheldOnly=true` | Leite retido agora (filtro na listagem) | `200 [AnimalListItemDto]` |
| `GET` | `/api/animals?sanitaryStatus=…` | Filtro por badge sanitário (na listagem) | `200 [AnimalListItemDto]` |
| `GET` | `/api/animals/sanitary-statuses` | Lookup `SanitaryStatus` | `200 [{value,label}]` |
| `GET` | `/api/health-cases/disease-types` | Lookup `DiseaseType` | `200 [{value,label}]` |
| `GET` | `/api/health-cases/quarters` | Lookup `Quarter` | `200 [{value,label}]` |
| `GET` | `/api/health-cases/test-types` | Lookup `MastitisTestType` | `200 [{value,label}]` |
| `GET` | `/api/health-cases/statuses` | Lookup `HealthCaseStatus` | `200 [{value,label}]` |

> A listagem de animais (`GET /api/animals`) passa a devolver `SanitaryStatus` + `MilkWithheldUntil` por animal (resolvidos em bloco) e aceita os filtros `sanitaryStatus` e `milkWithheldOnly` (D14).

### 6.8 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `AnimalId`, `DiseaseType`, `DiagnosisDate` obrigatórios; `DiagnosisDate <= hoje`. | DTO (DataAnnotations + `IValidatableObject`) |
| RN-02 | `DiseaseName` obrigatório sse `DiseaseType = Other`; nulo em `Mastitis`. | DTO (`IValidatableObject`) |
| RN-03 | `AffectedQuarters` só aceito quando `DiseaseType = Mastitis`. | DTO (`IValidatableObject`) |
| RN-04 | `ApplicationDate` / `TestDate` `<= hoje`. | DTO + Service |
| RN-05 | `WithdrawalPeriodDays` = enviado, senão `0`; `>= 0`. `MedicationName` é texto livre (sem catálogo). | DTO + Service |
| RN-06 | **Carência inclusiva** (D6): retém enquanto `hoje < MilkLiberationDate`; libera em `hoje >= MilkLiberationDate`. | Resolver |
| RN-07 | `caseLiberationDate = MAX(ApplicationDate + WithdrawalPeriodDays)` sobre `AnimalMedication` **ativas** do caso. | Service (leitura, set-based) |
| RN-08 | `AnimalMilkWithheldUntil = MAX(ApplicationDate + WithdrawalPeriodDays)` sobre `AnimalMedication` **ativas** do animal com liberação **futura**; null = liberado. | Service (leitura, set-based) |
| RN-09 | Encerramento explícito via `ResolvedAt` (D7): status nunca deriva "fim" do último `ApplicationDate`; `ResolvedAt` firma a carência. | Resolver + Service |
| RN-10 | Isolamento de tenant: repositório filtra por `PropertyId`; `Animal`/`Medication` referenciados devem ser do tenant. | Repository / Service |
| RN-11 | Ortogonalidade (D11): o eixo não escreve em produção/reprodução. | Ausência intencional de escrita |

### 6.9 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `HealthCase.cs`, `MastitisTest.cs` | **Criar** |
| `Domain/Models` | `AnimalMedication.cs` | **Estender** (`HealthCaseId int?` + nav) e **renomear** `StartDate → ApplicationDate` (D12) |
| *(consumidores do rename)* | `AnimalMedicationDto.cs`, `AnimalMedicationCreateDto.cs`, `AnimalMedicationUpdateDto.cs`, `AnimalMedicationService.cs`, `AnimalMedicationRepository.cs` (`OrderByDescending`), `AnimalMedicationProfile.cs` | **Ajustar** `StartDate → ApplicationDate` |
| `Domain/Models` | `Animal.cs` | **Adicionar** nav `ICollection<HealthCase>?` (opcional) |
| `Domain/Enums` | `DiseaseType.cs`, `Quarter.cs`, `MastitisTestType.cs`, `HealthCaseStatus.cs`, `SanitaryStatus.cs` | **Criar** |
| `Application/Helpers` | `HealthCaseStatusResolver.cs`, `AnimalSanitaryStatusResolver.cs` | **Criar** |
| `Application/DTOs` | DTOs do §6.6 | **Criar** |
| `Application/DTOs` | `AnimalListItemDto.cs`, `AnimalDto.cs` | **Adicionar** `SanitaryStatus`/`MilkWithheldUntil` (histórico não embutido; via endpoint `health-history`) |
| `Application/Mappings` | `HealthCaseProfile.cs` | **Criar** |
| `Application/Interfaces` | `IHealthCaseService`, `IHealthCaseRepository` | **Criar** |
| `Application/Interfaces` | `IAnimalRepository` | **Adicionar** `GetSanitaryFactsMapAsync(ids)` (fatos set-based p/ o resolver bulk) |
| `Application/Services` | `HealthCaseService.cs` | **Criar** |
| `Application/Services` | `AnimalService.cs` | **Compor** `SanitaryStatus`/`MilkWithheldUntil` na listagem e na single (padrão reprodutivo) |
| `Infrastructure/Repositories` | `HealthCaseRepository.cs` | **Criar** (filtro tenant; joins de medicações/testes) |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | `DbSet`s, query filters por `PropertyId`, relacionamentos, índices |
| `Api/Controllers` | `HealthCasesController.cs` | **Criar** rotas do §6.7 |
| `Api/Controllers` | `AnimalsController.cs` | **Adicionar** `GET {id}/health-history`, lookup `GET sanitary-statuses`; filtros `sanitaryStatus`/`milkWithheldOnly` via `AnimalFilterDto` (D14) |
| `Program.cs` | DI | Registrar repositório e serviço *(requer aprovação)* |
| `Infrastructure/Migrations` | *(ver §7)* | **Requer aprovação antes de criar** |

---

## 7. Notas de Migração
> **Requer aprovação explícita antes de executar** (criação de migração e mudança de DI).

**Criar tabela `HealthCases`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `AnimalId` | int | not null, FK → `Animals` (`DeleteBehavior.Restrict`) |
| `DiseaseType` | int | not null |
| `DiseaseName` | nvarchar(100) | nullable |
| `DiagnosisDate` | datetime2 | not null |
| `AffectedQuarters` | int | nullable (flags) |
| `ResolvedAt` | datetime2 | nullable |
| `Notes` | nvarchar(1000) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Criar tabela `MastitisTests`:** `Id` (PK), `HealthCaseId` (FK → `HealthCases`, `Cascade`), `TestType` int, `Result` nvarchar(200), `TestDate` datetime2, `PropertyId`, `IsActive`/`CreatedAt`/`UpdatedAt`.

**Alterar `AnimalMedications`:**
- Adicionar `HealthCaseId int NULL`, FK → `HealthCases` (`DeleteBehavior.Restrict`).
- Adicionar `MedicationName nvarchar(200) NULL` (nome livre; fluxo de tratamento) (D13).
- Tornar `MedicationId int NULL` (antes not null) — passa a servir só o fluxo avulso legado; FK recriada como opcional.
- **Renomear coluna `StartDate → ApplicationDate`** (D12) — `RenameColumn` no EF (preserva dados). `EndDate` **permanece** (o dashboard filtra por ela em `DashboardRepository`). Atualizar os consumidores avulsos listados em §6.9 no mesmo PR para não quebrar o build.

**Índices:**
| Colunas | Motivo |
|---------|--------|
| `HealthCases (PropertyId)` | Isolamento de tenant. |
| `HealthCases (PropertyId, AnimalId)` | Histórico do animal / resolver bulk. |
| `HealthCases (PropertyId, DiagnosisDate)` | Listagem por período / dashboard. |
| `HealthCases (PropertyId, DiseaseType)` | Telas mastite × doenças. |
| `MastitisTests (HealthCaseId)` | Join do detalhe. |
| `MastitisTests (PropertyId)` | Tenant / agregações do dashboard. |
| `AnimalMedications (HealthCaseId)` | Medicações do caso + resolver de carência. |

**DbContext:** `DbSet<HealthCase>`, `DbSet<MastitisTest>`; `HasQueryFilter(x => x.PropertyId == _propertyId)` em ambas; relacionamento `HealthCase 1—N AnimalMedication` e `HealthCase 1—N MastitisTest`.

> Migração sugerida: `Spec_ControleSanitario_Tratamentos`.

---

## 8. Como habilitar offline-first depois (sem retrabalho)
A análise original é offline-first (Id GUID de cliente, `FarmId`, resolvers no device, Room/outbox). Server-side agora **não** impede a evolução — a via é **aditiva** (mesmo racional da Vacinação §8 e da 11.1):

- Adicionar **`ClientId Guid` único** em `HealthCase`, `MastitisTest` e `AnimalMedication` (chave de idempotência), mantendo o `Id int` (PK interna). Ingestão vira **upsert idempotente por `ClientId`**.
- **Adição de aplicação é append com GUID próprio** → adições offline convergem sem conflito (D5). Last-write-wins nos campos escalares (baixa contenção; produtor único).
- Os **resolvers** (`HealthCaseStatusResolver`, `AnimalSanitaryStatusResolver`) são funções puras sobre os fatos — rodam idênticos sobre o cache local (Room) ou o banco, sem alteração de semântica. "Barracão sem sinal: a lista mostra 'vaca 42 — em carência até 05/09' sem servidor."
- `PropertyId` continua o tenant; se/quando `FarmId` entrar como raiz, é renomeação/alias, não reescrita de regra.

---

## 9. Fora do Escopo / Questões Futuras
- **Dashboard de leite descartado por carência** (Q4): JOIN de **leitura** com o eixo produtivo (`MilkProduction`/`Lactation`), mantendo a escrita desacoplada (D11). O modelo já suporta (`MilkWithheldUntil` × produção). Fora do v1.
- **Notificação local "carência termina hoje"** (Q5): client-side/offline, lê `MilkWithheldUntil`; sem job de servidor. Fora do v1.
- **Carência de carne / outros eixos**: `WithdrawalPeriodDays` é a carência do leite no v1; um `MeatWithdrawalDays` pode ser aditivo depois.
- **Descarte por quarto** (litros por teto): o eixo produtivo mede no nível animal/tanque; fora do v1.
- **Uso avulso de medicação pela UI** (`HealthCaseId = null`): habilitado pelo modelo e já servido pelo `AnimalMedicationsController` existente; o fluxo desta spec cria sempre via caso.
- **Agregações do dashboard** (prioridade, sobre os fatos): nº de animais em carência agora; casos de mastite por mês; quartos mais afetados; medicamentos mais usados; duração média de tratamento; recorrência por animal. Especificáveis à parte sobre as tabelas desta spec.

**Resolvidas nesta iteração:** Q1 (boundary inclusivo — D6); Q2 (término/alta explícitos — D7); Q3 (append por aplicação, log completo de graça — D5); Q4/Q5 (fora do v1). Carência em dias; testes no nível do caso; `Result` string; medicação reusando `AnimalMedication` com carência no nível do animal; status sanitário na lista e na single.
