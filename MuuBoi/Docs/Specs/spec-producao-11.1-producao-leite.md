# Spec 11.1: Registro de Produção de Leite (CRUD de `MilkProduction`)

**Módulo:** Produção — Leite
**Versão:** 1.1
**Data:** 31/Ago/2026 (rev. 1.0 em 30/Ago/2026)
**Fonte:** Divisão do Spec #11 (Produção Leiteira e Status de Lactação) em 11.1 (produção) e 11.2 (lactação/secagem)
**Status:** Implementada

> **Alterações da 1.1 (durante a implementação):**
> - Campo `Liters` renomeado para **`Volume`** — nome desacoplado da unidade de medida (D7).
> - O `GET /api/milk-productions` passa a retornar **agrupado por dia** (total do dia + lançamentos aninhados), como agregação **apenas de exibição** (D8). Índices reais continuam fora de escopo (→ 11.2).
**Depende de:** Spec #1 (Animais) — para o paradigma de tenant/`BaseEntity`.
**Parte de:** Spec #11 (Produção). **Antecede:** Spec 11.2 (Lactação e Secagem).

---

## 1. Contexto e Objetivo

O produtor-piloto **não mede o leite por animal** — ele registra apenas o **total diário de leite do rebanho** (Spec #11, D1). Esta spec cobre **exclusivamente** esse registro: a entidade `MilkProduction` (total do rebanho por lançamento) e o seu **CRUD** — cadastrar, consultar, editar e inativar lançamentos de leite.

**Divisão do Spec #11:**
- **11.1 (esta):** registro de produção de leite — `MilkProduction` e CRUD.
- **11.2 (futura):** **lactação** (ciclo parto→secagem, entidade `Lactation`, status derivado) e **secagem** (dry-off medicado, event-driven), incluindo DEL, onboarding de lactações pré-existentes, etc.

> **Limites explícitos desta spec:**
> - **Não** há medição por animal — `MilkProduction` é do **rebanho**. Medição individual (`MilkYield`) é evolução futura (Spec #11, D7) e fica **fora de escopo**.
> - **Não** há índices/agregações aqui (total por período, média por vaca em lactação, DEL, proporção). Todos os índices ficam para o 11.2 / spec de indicadores.
> - **Não** há vínculo com lactação — registrar leite não exige lactação aberta (isso é 11.2).

---

## 2. Escopo

**Dentro (11.1):**
- Entidade `MilkProduction` = **total diário do rebanho** por lançamento (`Date`, `Volume`, `Milking?`).
- **Múltiplos lançamentos por dia** permitidos (ex.: ordenha da manhã e da tarde) — eles apenas coexistem; a soma persistida/indexada é responsabilidade da futura camada de índices (Spec #11, D6).
- CRUD completo com **soft delete**.
- Filtros de consulta (período, turno de ordenha).
- Enum `MilkingShift` (turno de ordenha), opcional.
- **Agrupamento de exibição por dia** na listagem (`GET`): total do dia + lançamentos aninhados, calculado em memória a partir dos fatos filtrados (D8). Não persiste nem indexa nada.

**Fora (→ 11.2 / futuro):**
- Lactação, secagem, DEL, status "em lactação".
- Índices/agregações **persistidos ou por período arbitrário** (média por vaca em lactação, DEL, proporção, total do rebanho por intervalo). O total diário de exibição da listagem (D8) **não** é um índice — é apenas soma dos itens já retornados.
- `MilkYield` / medição individual por animal.

---

## 3. Decisões (herdadas e escopadas do Spec #11)

| # | Decisão | Origem / Motivo |
|---|---------|-----------------|
| D1 | `MilkProduction` é o **total do rebanho** por lançamento — **sem `AnimalId`**. | Spec #11 D1/D6: o produtor só fornece o total diário; individual é futuro. |
| D2 | **Múltiplos lançamentos por dia** são permitidos e **não** há unicidade por dia. | Spec #11 D6: casa com registro incremental por ordenha; a soma é da camada de índices. |
| D3 | `Milking` (turno de ordenha) é **opcional** (`MilkingShift?`). | Spec #11 D6: alguns lançam por ordenha, outros um total; nulo = não especificado. |
| D4 | `Volume` é `decimal` (precisão ampla), valor > 0. | Spec #11 D6/D8: evita armadilha de tipo em agregação futura; total de rebanho pode ser grande. |
| D5 | Alinhar ao **backend atual**: `Id int`, `PropertyId`, `BaseEntity`, soft delete, `ExceptionMiddleware`. | Decisão do TCC (30/Ago): implementável já; **offline-first fica para frente futura** (ver §8), sem retrabalho de modelo. |
| D6 | **Nenhum índice persistido/por período** nesta spec. | Decisão do TCC (30/Ago): 11.1 é estritamente registro/CRUD. |
| D7 | Campo renomeado de `Liters` para **`Volume`**. | Decisão do TCC (31/Ago): nome desacoplado da unidade — se a unidade mudar, `Volume` continua legível/correto. |
| D8 | `GET` retorna **agrupado por dia** (total do dia + lançamentos aninhados), agregação **apenas de exibição** (em memória, sobre os fatos já filtrados). | Decisão do TCC (31/Ago): melhora o consumo pelo frontend e reduz requisições (ambiente com internet fraca). Não é índice — não persiste nem cria contrato de período. |

---

## 4. Histórias de Usuário

### US-01 — Registrar produção de leite do rebanho
> **Como** produtor,
> **quero** lançar o total de leite produzido (por dia e, se quiser, por ordenha),
> **para** ter o histórico de produção do rebanho.

**Critérios de aceite:**
- Informo `Date` e `Volume` (obrigatórios); `Milking` e `Notes` são opcionais.
- `Volume` > 0; `Date` não pode ser futura.
- Posso lançar **mais de um** registro no mesmo dia (ex.: manhã e tarde).

### US-02 — Consultar lançamentos
> **Como** produtor,
> **quero** listar os lançamentos de leite por período,
> **para** acompanhar a produção.

**Critérios de aceite:**
- Listo com filtros opcionais de período (`DateFrom`/`DateTo`) e turno (`Milking`).
- A listagem vem **agrupada por dia**: cada item mostra a data e o **total do dia** (`totalVolume`), e traz os lançamentos daquele dia aninhados, cada um com turno (rótulo), volume individual e observações.

### US-03 — Corrigir/excluir um lançamento
> **Como** produtor,
> **quero** editar ou remover um lançamento errado,
> **para** manter os dados corretos.

**Critérios de aceite:**
- `PATCH` altera só os campos enviados (reaplica as validações de `Volume`/`Date`).
- `DELETE` inativa (soft delete); o lançamento some das listagens ativas.

---

## 5. Casos de Uso

### CU-01 — Registrar
1. `POST /api/milk-productions` com `MilkProductionCreateDto`.
2. Valida DTO (`Volume` > 0, `Date` não futura). Inválido → `400`.
3. Cria (`IsActive = true`) e retorna `201 Created`.

### CU-02 — Consultar
- `GET /api/milk-productions` — lista **agrupada por dia** (`MilkProductionDayDto`) com `MilkProductionFilterDto`.
- `GET /api/milk-productions/{id}` — detalhe de um lançamento (`MilkProductionDto`); inexistente → `404`.

### CU-03 — Editar
- `PATCH /api/milk-productions/{id}` com `MilkProductionUpdateDto` (campos opcionais). Reaplica RN-01/RN-02. `200` / `404`.

### CU-04 — Inativar
- `DELETE /api/milk-productions/{id}` → soft delete → `204`. Já inativo → `409`.

---

## 6. Especificação Técnica

### 6.1 Entidade `MilkProduction`
> `Domain/Models/MilkProduction.cs` — **sem `AnimalId`** (total do rebanho).

```csharp
public class MilkProduction : BaseEntity, ITenantEntity
{
    public DateTime Date { get; set; }

    public MilkingShift? Milking { get; set; }        // null = não especificado / total do dia

    [Range(0.01, 9999999.99)]
    public decimal Volume { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }
}
```

### 6.2 Enum `MilkingShift`
> `Domain/Enums/MilkingShift.cs` — nomes em inglês, `[Description]` em português (convenção do projeto).

```csharp
public enum MilkingShift
{
    [Description("Manhã")]
    Morning = 1,

    [Description("Tarde")]
    Afternoon = 2,

    [Description("Noite")]
    Evening = 3
}
```

### 6.3 DTOs
> `Application/DTOs/`

- **`MilkProductionCreateDto`** — `Date` (`[Required]`; não futura via `IValidatableObject`), `Milking?`, `Volume` (`[Required]`, `[Range(0.01, 9999999.99)]`), `Notes?` (`[MaxLength(500)]`).
- **`MilkProductionUpdateDto`** — `Date?` (não futura via `IValidatableObject`), `Milking?`, `Volume?` (`[Range]`), `Notes?` — todos opcionais (PATCH parcial, padrão do projeto: null = não altera).
- **`MilkProductionDto`** (resposta / detalhe) — `Id`, `Date`, `Milking` (`EnumValueDto?`), `Volume`, `Notes`, `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`MilkProductionDayDto`** (item da listagem — agrupamento por dia, D8) — `Date`, `TotalVolume`, `Records` (`List<MilkProductionListItemDto>`).
- **`MilkProductionListItemDto`** (lançamento aninhado no dia) — `Id`, `Milking` (`EnumValueDto?`), `Volume`, `Notes`.
- **`MilkProductionFilterDto`** — `DateFrom?`, `DateTo?`, `Milking?`, `IsActive?`.

Mapeamento em `Application/Mappings/MilkProductionProfile.cs` (turno como `EnumValueDto` com `GetDescription()`, no padrão dos demais). O agrupamento/soma do `MilkProductionDayDto` é montado no service (em memória); apenas os `Records` passam pelo AutoMapper.

### 6.4 Endpoints da API
> Auth: Bearer Token obrigatório. **Recurso de nível de rebanho — não aninhado sob animal.**

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/milk-productions` | Registrar lançamento | `201 MilkProductionDto` / `400` |
| `GET` | `/api/milk-productions` | Listar (agrupado por dia) com filtro | `200 [MilkProductionDayDto]` |
| `GET` | `/api/milk-productions/{id}` | Detalhe | `200 MilkProductionDto` / `404` |
| `PATCH` | `/api/milk-productions/{id}` | Editar (parcial) | `200 MilkProductionDto` / `400` / `404` |
| `DELETE` | `/api/milk-productions/{id}` | Inativar (soft delete) | `204` / `404` / `409` |
| `GET` | `/api/milk-productions/milkings` | Lookup do enum de turno | `200 [{value, label}]` |

### 6.5 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `Volume` > 0 (`Range(0.01, 9999999.99)`). | DTO (DataAnnotations) |
| RN-02 | `Date` não pode ser futura. | DTO (`IValidatableObject`) |
| RN-03 | Vários lançamentos no mesmo dia são válidos — **não** há unicidade/upsert por dia. | Service (ausência de checagem) |
| RN-04 | Isolamento de tenant: repositório filtra por `PropertyId`. | Repository |

### 6.6 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `MilkProduction.cs` | **Criar** |
| `Domain/Enums` | `MilkingShift.cs` | **Criar** |
| `Application/DTOs` | `MilkProductionCreateDto`, `MilkProductionUpdateDto`, `MilkProductionDto`, `MilkProductionDayDto`, `MilkProductionListItemDto`, `MilkProductionFilterDto` | **Criar** |
| `Application/Mappings` | `MilkProductionProfile.cs` | **Criar** |
| `Application/Interfaces` | `IMilkProductionService`, `IMilkProductionRepository` | **Criar** |
| `Application/Services` | `MilkProductionService.cs` | **Criar** |
| `Infrastructure/Repositories` | `MilkProductionRepository.cs` | **Criar** (filtro de tenant) |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | `DbSet<MilkProduction>`, query filter por `PropertyId`, precisão `(11,2)`, índices |
| `Api/Controllers` | `MilkProductionsController.cs` | **Criar** rotas do §6.4 |
| `Program.cs` | DI | Registrar repositório e serviço *(requer aprovação)* |
| `Infrastructure/Migrations` | *(ver §7)* | **Requer aprovação antes de criar** |

---

## 7. Notas de Migração

> **Requer aprovação explícita antes de executar.**

**Criar tabela `MilkProductions`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `Date` | datetime2 | not null |
| `Milking` | int | **nullable** |
| `Volume` | decimal(11,2) | not null |
| `Notes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Índices:**
| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(PropertyId)` | Simples | Isolamento de tenant (padrão do projeto). |
| `(PropertyId, Date)` | Composto | Consulta/filtro por período (e base para a agregação futura do 11.2). |

> Migração sugerida: `Spec11_1_MilkProduction`.

---

## 8. Como habilitar offline-first depois (sem retrabalho)

Escolher `Id int` agora **não** impede o offline-first previsto no Spec #11 (D9). Quando a frente de sync entrar, basta:

- Adicionar uma **chave de idempotência do cliente** (ex.: `ClientId Guid` único) em `MilkProduction`, sem remover o `Id int` (PK interna).
- O endpoint de ingestão passa a fazer **upsert idempotente por `ClientId`** — reenvio após falha de rede não duplica.
- Como os lançamentos já são **fatos append-only que somam** (D2/D6), não há status nem saldo a reconciliar; qualquer nó recalcula. Nenhuma mudança na semântica de `Volume`/`Date`/`Milking`.

Ou seja, a evolução é **aditiva** (uma coluna + um caminho de upsert), não uma reescrita.

---

## 9. Fora do Escopo desta Spec

- **Lactação, secagem (`dry-off`) e status "em lactação"** → **Spec 11.2**.
- **Índices/agregações** (total do rebanho por período, média por vaca em lactação, DEL, proporção de vacas em lactação) → Spec 11.2 / spec de indicadores.
- **`MilkYield` / medição individual por animal** (Spec #11, D7) → futuro.
- **Sincronização offline-first / upsert idempotente** (Spec #11, D9) → frente futura (ver §8 para a via de evolução).
- **Cadastro/edição de animais** → Spec #1.
