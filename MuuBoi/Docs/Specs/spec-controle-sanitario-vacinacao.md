# Spec: Controle Sanitário — Vacinação (`VaccinationEvent`)

**Módulo:** Controle Sanitário → Vacinação
**Versão:** 1.0
**Data:** 03/Set/2026
**Fonte:** Spec de análise "Documentação — Fluxo de Vacinação" (átomo `VaccinationEvent` + leituras derivadas).
**Status:** Especificada (não implementada)
**Depende de:** Spec #1 (Animais) — para `Animal`, tenant/`BaseEntity`. Reaproveita a entidade **`Vaccine`** já existente (catálogo por fazenda).

> **Decisões de escopo desta spec (definidas com a orientadora do TCC em 03/Set):**
> - **Substitui/depreciar** o modelo atual `AnimalVaccination` (registro *por animal*). O novo átomo de escrita é o **`VaccinationEvent`** (em lote, N animais por evento). Ver §7 (migração/depreciação).
> - **Server-side agora**: `Id int`, `PropertyId`, `BaseEntity`, soft delete, `ExceptionMiddleware`, criação do reforço **no serviço** (não no device). Offline-first fica como **evolução futura aditiva** (§8), sem retrabalho de modelo — mesmo racional da Spec 11.1 (D5).
> - **Catálogo `Vaccine` por fazenda** (mantém o `PropertyId` atual). Catálogo global compartilhado fica fora de escopo (Q5).

---

## 1. Contexto e Objetivo

O eixo sanitário é ortogonal ao produtivo e ao reprodutivo. Esta spec cobre **apenas vacinação**: cadastro de eventos de vacinação, controle de reforço e histórico por animal.

O modelo tem **um átomo de escrita** (`VaccinationEvent`) e **uma leitura derivada** (histórico por animal, que já traz a próxima dose prevista). **Não há** tabela de protocolo, **não há** campo de status gravado e **não há** job agendado. Tudo que é "estado" (agendado, vencido, aplicado) é **derivado na leitura**, coerente com o padrão dos resolvers já estabelecidos no projeto (`Application/Helpers/ReproductiveStatusResolver`, `ProductiveStatusResolver`).

> **Ajuste 04/Set (durante a implementação):** a **grade animais × vacinas** saiu de escopo — não haverá tela dessa relação por ora; essa cobertura fica no **histórico do animal**. O histórico passa a trazer, por linha, a **próxima dose prevista** (`nextDoseDate`) quando existir um evento de reforço encadeado.

O **reforço** é a única exceção deliberada à regra "derivar em vez de gravar": ele é **materializado** como um evento futuro (D5), porque é uma *intenção declarada* pelo produtor, não algo calculável a partir de outros fatos.

---

## 2. Escopo

**Dentro:**
- Entidade **`VaccinationEvent`** (átomo de escrita) + ligação **`VaccinationEventAnimal`** (N animais por evento).
- Enum **`DoseType`** (`{ FirstDose, Booster }`) gravado no evento, default vindo da linhagem, editável.
- **Duas datas independentes**: `PredictedDate` (agenda) e `ApplicationDate` (aplicação real, `<= hoje`).
- **Reforço via rota própria** `POST /{id}/booster` (evento filho, herda vacina+animais, `DoseType = Booster`; regra pai → 0..1 filho por `ParentEventId`).
- **Status derivado do evento** (`{ Scheduled, Overdue, Applied }`) via helper resolver.
- **Histórico por animal** (extrato dos eventos aplicados que incluem o animal, com `nextDoseDate` da próxima dose prevista).
- CRUD com **soft delete** e isolamento de tenant no repositório.

**Fora (→ specs próprias / futuro):**
- **Grade animais × vacinas** (pivô de cobertura) — retirada em 04/Set; a informação de cobertura fica no histórico do animal. Reavaliar se/quando houver tela dedicada.
- **Tratamentos** (mastite e demais), **carência de leite**, **testes sanitários** — natureza reativa/pontual, spec separada.
- **Aplicação/efetivo por animal** dentro do lote ("28 de 30") — Q1; modelar sem quebrar (§9).
- **Onboarding retroativo explícito** (`Origin { Observed, RetroactiveOnboarding }`) — Q2.
- **Aviso de data fora de ordem** — Q3.
- **Granularidade do reforço** (série × anual, nº da dose) — Q4.
- **Catálogo global de vacinas** — Q5 (decidido: por fazenda).
- **Notificação/calendário do reforço** — Q6 (o modelo já suporta: lê `PredictedDate` de eventos `Scheduled`/`Overdue`).
- **Sincronização offline-first / upsert idempotente** — evolução futura (§8).

---

## 3. Decisões (herdadas da análise + escopadas para o TCC)

| # | Decisão | Origem / Motivo |
|---|---------|-----------------|
| D1 | Escopo restrito a **vacinação** (preventivo movido a calendário). Tratamento (reativo) fica em spec separada. | Análise D1: mantém cada resolver com responsabilidade única. |
| D2 | O **evento é a unidade de escrita**; o "mapa por animal" é **leitura** sobre a ligação `VaccinationEventAnimal`, não dado duplicado. | Análise D2: mesmo razão contábil visto por dois relatórios. Evita campo-espelho. |
| D3 | Duas datas independentes: **`PredictedDate`** (nullable, agenda) e **`ApplicationDate`** (nullable, `<= hoje`, soberana no histórico). | Análise D3: "quando foi previsto" ≠ "quando aconteceu". Histórico é só passado. |
| D4 | **Status do evento é derivado, nunca gravado** (`Applied`/`Scheduled`/`Overdue`), resolvido na leitura em bloco. | Análise D4: "Overdue"→"Applied" no instante da aplicação; sem job, sem campo-espelho stale. |
| D5 | **Reforço é materializado como evento filho**, criado por **ação explícita do usuário** (`POST /{id}/booster`, ajuste 04/Set — não é mais spawn automático), regra única **pai → 0..1 filho** por `ParentEventId`. | Análise D5: reforço é intenção declarada, não derivável. Cobre reforço anual. |
| D6 | **`DoseType`** é flag gravada no evento, com **default vindo da linhagem** (sem pai → `FirstDose`; spawn → `Booster`), **editável**. | Análise D6: dado imutável do nascimento; default editável cobre onboarding num só campo. |
| D7 | **Sem trava** entre `PredictedDate` e `ApplicationDate` — antecipar e atrasar são ambos legítimos. | Análise D7: a data de aplicação registra quando aconteceu, ponto. |
| D8 | **Leitura derivada única**: histórico por animal (extrato), incluindo `nextDoseDate` da próxima dose prevista. A grade animais × vacinas da análise foi **retirada** (04/Set). | Análise D8 (parcial): mantém o extrato; a grade fica para uma eventual tela dedicada. |
| D9 | **Server-side agora** (`Id int`, `PropertyId`, `BaseEntity`, spawn no serviço). Offline-first = evolução futura aditiva. | Decisão TCC (03/Set): implementável já; alinha com Spec 11.1 (D5). Análise D9 vira §8. |
| D10 | **Substitui/depreciar `AnimalVaccination`**; `VaccinationEvent` é o novo átomo. | Decisão TCC (03/Set): evita duas fontes de verdade para vacinação. |
| D11 | Resolver como **helper estático** em `Application/Helpers/` (não interface `I...`). | Convenção real do projeto (`ReproductiveStatusResolver`, `ProductiveStatusResolver`). |
| D12 | **`Vaccine` por fazenda** (mantém `PropertyId`). | Decisão TCC (03/Set): menor esforço; catálogo global fica fora (Q5). |

---

## 4. Histórias de Usuário

### US-01 — Registrar vacinação de um lote de animais
> **Como** produtor,
> **quero** cadastrar uma vacinação selecionando a vacina, a data e vários animais de uma vez,
> **para** registrar a aplicação sem lançar animal por animal.

**Critérios de aceite:**
- Informo `vaccineId` e ao menos **um** animal (`animalIds[]`).
- Informo `applicationDate` (aplicação já feita) e/ou `predictedDate` (agendamento). **Ao menos uma** das duas é obrigatória.
- `applicationDate` não pode ser **futura**.
- `doseType` tem default (`FirstDose` para evento sem pai) e é **editável** — no onboarding retroativo troco para `Booster` ao salvar.

### US-02 — Efetivar uma aplicação
> **Como** produtor,
> **quero** marcar que uma vacina agendada foi aplicada,
> **para** manter o registro do que foi feito.

**Critérios de aceite:**
- Ao efetivar, informo a `applicationDate` (`<= hoje`); o evento passa a contar como **Applied**.

### US-02b — Criar uma dose de reforço
> **Como** produtor,
> **quero** criar, a partir de um evento, uma dose de reforço futura,
> **para** planejar a próxima aplicação com a mesma vacina e os mesmos animais.

**Critérios de aceite:**
- Numa **ação/rota própria** (`POST /api/vaccination-events/{id}/booster`), informo apenas a `predictedDate`.
- O reforço **herda** a vacina e a lista de animais do pai; `doseType` = `Booster` automaticamente; nasce sem `applicationDate` (evento futuro).
- **Única regra**: a `predictedDate` do reforço **não pode ser anterior** à `applicationDate` **nem** à `predictedDate` do pai (quando existirem) → senão `422`.
- **Um reforço por pai**: se já existir, `409`. Para encadear novas doses futuras, crio o reforço a partir do próprio reforço (ele vira pai do próximo).

### US-03 — Consultar o histórico de vacinação de um animal
> **Como** produtor,
> **quero** ver o histórico de vacinas aplicadas em um animal,
> **para** saber o que ele já tomou.

**Critérios de aceite:**
- Vejo uma lista com `vaccineName`, `vaccinationEventId` (evento), `applicationDate`, `doseType` e, quando houver reforço encadeado, `nextDoseDate`, **ordenada por data desc**.
- Só aparecem eventos **aplicados**; reforço ainda não aplicado é agenda, não entra como linha do histórico (mas aparece como `nextDoseDate` da dose que o originou).
- Uma linha por animal, mesmo que o evento tenha sido coletivo.

### US-04 — Corrigir/excluir um evento de vacinação
> **Como** produtor,
> **quero** editar ou remover um evento lançado errado,
> **para** manter os dados corretos.

**Critérios de aceite:**
- `PATCH` altera só os campos enviados (reaplica validações).
- `DELETE` inativa (soft delete); o evento some das listagens e do histórico ativos.

---

## 5. Casos de Uso

### CU-01 — Registrar evento
1. `POST /api/vaccination-events` com `VaccinationEventCreateDto`.
2. Valida DTO: `vaccineId` presente, `animalIds[]` com ≥1, **ao menos uma data**, `applicationDate <= hoje`. Inválido → `400`.
3. Valida existência/tenant de `Vaccine` e de cada `Animal` → inexistente → `404`.
4. `doseType` = enviado, ou default (`FirstDose`, pois é evento sem pai).
5. Cria `VaccinationEvent` + linhas `VaccinationEventAnimal`. Retorna `201 Created` com status derivado.

### CU-02 — Efetivar / editar evento
1. `PATCH /api/vaccination-events/{id}` com `VaccinationEventUpdateDto` (`ApplicationDate?`, `PredictedDate?`, `DoseType?`).
2. Se `applicationDate` enviada: valida `<= hoje` (RN-02) e grava.
3. Retorna `200 OK` com o evento (status recalculado + linhagem pai/filho). Inexistente → `404`.

### CU-02b — Criar dose de reforço
1. `POST /api/vaccination-events/{id}/booster` com `VaccinationBoosterCreateDto` (`PredictedDate`).
2. Carrega o pai `{id}` (com animais). Inexistente → `404`.
3. Valida a data (RN-05): `PredictedDate` `>=` `ApplicationDate` do pai (se houver) **e** `>=` `PredictedDate` do pai (se houver) → senão `422`.
4. Já existe filho ativo do pai? → `409`.
5. Cria o filho: `DoseType = Booster`, `PredictedDate` informado, `ApplicationDate = null`, herda `VaccineId` + animais do pai, `ParentEventId = id`. Retorna `201 Created`.
- Editar a data depois: `PATCH /api/vaccination-events/{childId}` com `PredictedDate` (o reforço é um evento normal).

### CU-03 — Histórico por animal
- `GET /api/animals/{id}/vaccination-history` → `[VaccinationHistoryItemDto]` (só aplicados, `ApplicationDate desc`, com `nextDoseDate` quando houver reforço encadeado — resolvido em uma query set-based, sem N+1). Animal inexistente → `404`.

### CU-04 — Consultar eventos
- `GET /api/vaccination-events` (filtro opcional) → `[VaccinationEventListItemDto]` com status derivado.
- `GET /api/vaccination-events/{id}` → `VaccinationEventDto`; inexistente → `404`.

### CU-05 — Inativar
- `DELETE /api/vaccination-events/{id}` → soft delete → `204`. Já inativo → `409`.

---

## 6. Especificação Técnica

### 6.1 Entidade `VaccinationEvent`
> `Domain/Models/VaccinationEvent.cs` — átomo de escrita (lote).

```csharp
public class VaccinationEvent : BaseEntity, ITenantEntity
{
    [Required]
    public int VaccineId { get; set; }

    public DoseType DoseType { get; set; }              // default da linhagem, editável (D6)

    public DateTime? PredictedDate { get; set; }        // agenda; set na criação do reforço (D3)

    public DateTime? ApplicationDate { get; set; }      // aplicação real, <= hoje (D3)

    public int? ParentEventId { get; set; }             // linhagem pai->filho; 0..1 filho (D5)

    public Guid PropertyId { get; set; }

    // Invariante: ao menos uma de (PredictedDate, ApplicationDate) preenchida.

    public Vaccine? Vaccine { get; set; }
    public VaccinationEvent? ParentEvent { get; set; }   // self-ref (WithMany, sem coleção inversa)
    public ICollection<VaccinationEventAnimal>? EventAnimals { get; set; }
}
```

> **Nota:** o self-reference é mapeado como *one-to-many* (`WithMany()` sem navegação de coleção),
> para o SQL Server **não** criar um índice único automático em `ParentEventId` — que rejeitaria os
> muitos `NULL` dos eventos raiz. A regra "1 filho por pai" é garantida pelo **índice único filtrado** (§7).
> A navegação `ChildEvent` foi dispensada: a busca do filho é feita por query em `ParentEventId`.

### 6.2 Entidade `VaccinationEventAnimal` (ligação)
> `Domain/Models/VaccinationEventAnimal.cs` — o "mapa de vacinas por animal" (D2). Chave composta `(VaccinationEventId, AnimalId)`.

```csharp
public class VaccinationEventAnimal
{
    public int VaccinationEventId { get; set; }
    public int AnimalId { get; set; }

    // Extensível (Q1): pode ganhar ApplicationDate?/DoseType? próprios depois,
    // sem quebrar o evento — aplicação por animal ("28 de 30") é frente futura.

    public VaccinationEvent? VaccinationEvent { get; set; }
    public Animal? Animal { get; set; }
}
```

### 6.3 Enums
> `Domain/Enums/` — nomes em **inglês**, `[Description]` em **português** (convenção do projeto).

```csharp
public enum DoseType
{
    [Description("Primeira dose")]
    FirstDose = 1,

    [Description("Reforço")]
    Booster = 2
}

// Derivado — NÃO gravado (produzido pelo resolver na leitura):
public enum VaccinationEventStatus
{
    [Description("Agendado")]  Scheduled = 1,
    [Description("Vencido")]   Overdue = 2,
    [Description("Aplicado")]  Applied = 3
}
```

### 6.4 Resolvers (helpers estáticos)
> `Application/Helpers/` — mesma fonte de verdade para leitura de 1 e de N (padrão `ReproductiveStatusResolver`).

**`VaccinationEventStatusResolver`** — status do evento (D4):
```csharp
public static VaccinationEventStatus Resolve(DateTime? applicationDate, DateTime? predictedDate, DateTime utcNow)
{
    if (applicationDate.HasValue)                 return VaccinationEventStatus.Applied;
    if (predictedDate.HasValue && predictedDate.Value.Date >= utcNow.Date)
                                                  return VaccinationEventStatus.Scheduled;
    return VaccinationEventStatus.Overdue;        // sem aplicação e previsto no passado
}
```

### 6.5 DTOs
> `Application/DTOs/`

- **`VaccinationEventCreateDto`** — `VaccineId` (`[Required]`), `AnimalIds` (`[Required]`, `[MinLength(1)]`), `ApplicationDate?`, `PredictedDate?`, `DoseType?`. Validação cruzada via `IValidatableObject`: ao menos uma data; `ApplicationDate <= hoje`.
- **`VaccinationEventUpdateDto`** (efetivar/editar) — `ApplicationDate?`, `PredictedDate?`, `DoseType?` — todos opcionais (PATCH parcial; null = não altera). `ApplicationDate <= hoje` quando presente. (Criar reforço não é aqui — é rota própria.)
- **`VaccinationBoosterCreateDto`** (criar reforço) — `PredictedDate` (`[Required]`). Vacina, animais e `DoseType = Booster` são atribuídos pelo service (herdados do pai).
- **`VaccinationEventDto`** (resposta/detalhe) — `Id`, `VaccineId`, `VaccineName`, `DoseType` (`EnumValueDto`), `PredictedDate`, `ApplicationDate`, `Status` (`EnumValueDto`, derivado), `ParentEventId`, `Animals` (`[{ AnimalId, Name, TagNumber }]`), **`ParentEvent`** e **`ChildEvent`** (`VaccinationEventLineageDto?`), `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`VaccinationEventLineageDto`** (pai/filho no detalhe) — `Id`, `DoseType` (`EnumValueDto`), `PredictedDate`, `ApplicationDate`, `Status` (`EnumValueDto`).
- **`VaccinationEventListItemDto`** — `Id`, `VaccineName`, `DoseType` (`EnumValueDto`), `PredictedDate`, `ApplicationDate`, `Status` (`EnumValueDto`), `AnimalCount`.
- **`VaccinationHistoryItemDto`** — `VaccinationEventId`, `VaccineId`, `VaccineName`, `ApplicationDate`, `DoseType` (`EnumValueDto`), **`NextDoseDate?`** (data prevista do reforço/próximo evento filho, quando houver).
- **`VaccinationEventFilterDto`** — `VaccineId?`, `AnimalId?`, `Status?`, `DateFrom?`, `DateTo?`, `IsActive?`.

Mapeamento em `Application/Mappings/VaccinationEventProfile.cs` (enums como `EnumValueDto` via `GetDescription()`, no padrão dos demais). O `Status` e o `NextDoseDate` são preenchidos no service (resolver + memória; `NextDoseDate` vem de uma query set-based única `GetChildPredictedDatesAsync`, sem N+1); DTOs simples passam pelo AutoMapper.

### 6.6 Endpoints da API
> Auth: Bearer Token obrigatório.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/vaccination-events` | Criar evento (lote) | `201 VaccinationEventDto` / `400` / `404` |
| `GET` | `/api/vaccination-events` | Listar eventos (status derivado) com filtro | `200 [VaccinationEventListItemDto]` |
| `GET` | `/api/vaccination-events/{id}` | Detalhe | `200 VaccinationEventDto` / `404` |
| `POST` | `/api/vaccination-events/{id}/booster` | Criar dose de reforço a partir do evento `{id}` | `201 VaccinationEventDto` / `400` / `404` / `409` / `422` |
| `PATCH` | `/api/vaccination-events/{id}` | Efetivar / editar (datas, dose) | `200 VaccinationEventDto` / `400` / `404` |
| `DELETE` | `/api/vaccination-events/{id}` | Inativar (soft delete) | `204` / `404` / `409` |
| `GET` | `/api/animals/{id}/vaccination-history` | Histórico do animal (só aplicados; inclui `nextDoseDate`) | `200 [VaccinationHistoryItemDto]` / `404` |
| `GET` | `/api/vaccination-events/dose-types` | Lookup do enum `DoseType` | `200 [{value, label}]` |

### 6.7 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `AnimalIds` com ≥1 item; `VaccineId` obrigatório. | DTO (DataAnnotations) |
| RN-02 | `ApplicationDate` não pode ser **futura** (`<= hoje`). | DTO (`IValidatableObject`) |
| RN-03 | **Invariante**: ao menos uma de `PredictedDate`/`ApplicationDate`. | DTO (`IValidatableObject`) + Service |
| RN-04 | **Sem trava** entre `PredictedDate` e `ApplicationDate` (antecipar/atrasar legítimos). | Ausência intencional de regra (D7) |
| RN-05 | **Reforço via rota própria** (`POST /{id}/booster`), herdando vacina+animais, `DoseType = Booster`. **Um por pai** (2º → `409`). `PredictedDate` do reforço não pode ser anterior à `ApplicationDate` nem à `PredictedDate` do pai (→ `422`). | Service + índice único filtrado (§7) |
| RN-06 | `DoseType` default da linhagem (sem pai → `FirstDose`; spawn → `Booster`), editável. | Service |
| RN-07 | Filho herda `VaccineId` e lista de animais do pai. | Service (na criação do reforço) |
| RN-08 | Isolamento de tenant: repositório filtra por `PropertyId`; `Vaccine`/`Animal` referenciados devem ser do tenant. | Repository / Service |
| RN-09 | Histórico só inclui eventos **aplicados** (`ApplicationDate != null`) que contenham o animal. | Service (leitura) |

### 6.8 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `VaccinationEvent.cs`, `VaccinationEventAnimal.cs` | **Criar** |
| `Domain/Models` | `AnimalVaccination.cs` | **Depreciar** (ver §7) |
| `Domain/Enums` | `DoseType.cs`, `VaccinationEventStatus.cs` | **Criar** |
| `Application/Helpers` | `VaccinationEventStatusResolver.cs` | **Criar** |
| `Application/Interfaces` | `IAnimalRepository` | `GetExistingAnimalIdsAsync` (validação set-based de animais no lote) |
| `Application/DTOs` | DTOs do §6.5 | **Criar** |
| `Application/Mappings` | `VaccinationEventProfile.cs` | **Criar** |
| `Application/Interfaces` | `IVaccinationEventService`, `IVaccinationEventRepository` | **Criar** |
| `Application/Services` | `VaccinationEventService.cs` | **Criar** |
| `Infrastructure/Repositories` | `VaccinationEventRepository.cs` | **Criar** (filtro de tenant; `AsSplitQuery` no join evento→animais) |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | `DbSet`s, query filters por `PropertyId`, chave composta do join, self-ref FK, índice único filtrado de `ParentEventId` |
| `Api/Controllers` | `VaccinationEventsController.cs` | **Criar** rotas do §6.6 |
| `Api/Controllers` | `AnimalsController.cs` | **Adicionar** rota `GET {id}/vaccination-history`, injetando `IVaccinationEventService` |
| `Api/Controllers` | `AnimalVaccinationsController.cs` | **Depreciar** (ver §7) |
| `Program.cs` | DI | Registrar repositório e serviço *(requer aprovação)* |
| `Infrastructure/Migrations` | *(ver §7)* | **Requer aprovação antes de criar** |

---

## 7. Notas de Migração e Depreciação

> **Requer aprovação explícita antes de executar** (criação/edição de migração e mudança de DI).

**Criar tabela `VaccinationEvents`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `VaccineId` | int | not null, FK → `Vaccines` |
| `DoseType` | int | not null |
| `PredictedDate` | datetime2 | nullable |
| `ApplicationDate` | datetime2 | nullable |
| `ParentEventId` | int | nullable, FK → `VaccinationEvents.Id` (self, `DeleteBehavior.Restrict`) |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Criar tabela `VaccinationEventAnimals`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `VaccinationEventId` | int | not null, FK → `VaccinationEvents` (`DeleteBehavior.Cascade`) |
| `AnimalId` | int | not null, FK → `Animals` (`DeleteBehavior.Restrict`) |

> PK composta `(VaccinationEventId, AnimalId)`.

**Índices:**
| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(PropertyId)` | Simples | Isolamento de tenant (padrão do projeto). |
| `(PropertyId, VaccineId)` | Composto | Filtros por vacina / consultas. |
| `(PropertyId, ApplicationDate)` | Composto | Histórico e listagem por período. |
| `(ParentEventId)` **UNIQUE FILTERED** | Único filtrado — `WHERE ParentEventId IS NOT NULL AND IsActive = 1` | **Garante 1 filho por pai** (RN-05) no banco, além da guarda no service. |

**Depreciação de `AnimalVaccination` (concluída em 04/Set):**
- Decisão do TCC: **sem migração de dados** — a tabela `AnimalVaccinations` foi **dropada** (base de desenvolvimento, sem dados a preservar).
- Removidos: modelo, DTOs, service, repository, interfaces, controller (`AnimalVaccinationsController`), profile e as navegações em `Animal`/`Vaccine`; DI no `Program.cs`.
- **Dashboard**: `GetVaccinesPerMonthAsync` foi **repontado** para o novo modelo — conta doses por mês via `VaccinationEventAnimal` de eventos aplicados (mesma métrica de antes).
- `Vaccine` permanece intacto (por fazenda), reaproveitado.

> Migrações: `Spec_ControleSanitario_Vacinacao` (cria as tabelas novas) e `Deprecate_AnimalVaccination` (dropa `AnimalVaccinations`). Ambas **aplicadas** ao banco local.

---

## 8. Como habilitar offline-first depois (sem retrabalho)

A análise original é offline-first (Id = GUID de cliente, `FarmId`, spawn no *device*, Room/outbox). Escolher server-side agora **não** impede essa evolução — a via é **aditiva**, no mesmo racional da Spec 11.1 (§8):

- Adicionar **`ClientId Guid` único** em `VaccinationEvent` (chave de idempotência do cliente), mantendo o `Id int` (PK interna).
- Ingestão passa a **upsert idempotente por `ClientId`** — reenvio após falha de rede não duplica.
- O **spawn do reforço** pode migrar para o device (nasce com `ClientId`, `PredictedDate`, `DoseType` já carimbados), com a guarda "1 filho por pai" reconferida no servidor (o índice único filtrado do §7 já protege).
- Os **resolvers** (status, histórico) são funções puras sobre os fatos — rodam idênticos sobre cache local (Room) ou sobre o banco, sem alteração de semântica.
- `PropertyId` continua o tenant; se/quando `FarmId` entrar como raiz, é renomeação/alias, não reescrita de regra.

---

## 9. Fora do Escopo desta Spec

- **Tratamentos** (mastite/demais), **carência de leite**, **testes sanitários** → specs próprias (natureza reativa).
- **Aplicação/efetivo por animal** dentro do lote ("28 de 30") → Q1. A ligação `VaccinationEventAnimal` já é modelada para ganhar `ApplicationDate?`/`DoseType?` próprios depois, sem quebrar o evento.
- **Onboarding retroativo explícito** (`Origin { Observed, RetroactiveOnboarding }`) → Q2. Por ora, onboarding = evento manual com `ApplicationDate` passada e `DoseType` editado.
- **Aviso de data fora de ordem** (aplicação anterior à última do mesmo par animal/vacina) → Q3. Distinto de D7.
- **Granularidade do reforço** (série 21–30 dias × anual, número da dose) → Q4. `DoseType` é binário.
- **Catálogo global de vacinas** → Q5 (decidido: por fazenda).
- **Notificação/calendário do reforço** → Q6. O modelo já suporta: lê `PredictedDate` de eventos `Scheduled`/`Overdue`.
- **Sincronização offline-first / upsert idempotente / spawn no device** → evolução futura (§8).
