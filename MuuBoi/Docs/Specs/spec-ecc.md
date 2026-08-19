# Spec: Escore de Condição Corporal (ECC)

**Módulo:** Escore de Condição Corporal  
**Versão:** 1.0  
**Data:** 18/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais  
**Status:** Aprovado para implementação  
**Depende de:** Spec #1 — Gestão de Animais

---

## 1. Contexto e Objetivo

O Escore de Condição Corporal (ECC) é um indicador visual do estado nutricional do animal, avaliado em 5 níveis — de magreza severa a obesidade severa. É registrado com histórico ao longo da vida do animal e atualizado manualmente pelo produtor ou automaticamente pelos módulos de eventos reprodutivos (Spec #5 e #6).

O objetivo deste spec é definir o cadastro manual de ECC e a consulta do histórico por animal.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | ECC é uma entidade separada com histórico (`BodyConditionRecord`) | Múltiplos registros ao longo do tempo por animal. Não é um campo simples em `Animal`. |
| D2 | Registros de ECC são imutáveis — apenas adição | Para correção, adiciona-se um novo registro. O histórico nunca é alterado. |
| D3 | Registro manual liberado a qualquer momento | O produtor pode registrar ECC independentemente de eventos reprodutivos. |
| D4 | Os Specs #5 e #6 também criam registros de ECC automaticamente | Quando vinculados a inseminação, gestação ou parto. Usam o mesmo serviço e entidade deste spec. |
| D5 | `BodyConditionRecord` não implementa `ITenantEntity` diretamente | Isolamento de tenant é garantido via join com `Animal` (que possui `PropertyId`). |
| D6 | O produtor pode informar uma data passada | Permite registrar avaliações feitas em campo sem acesso imediato ao sistema. Data não pode ser futura. |
| D7 | Campo de observações opcional por registro | Permite contexto adicional (ex: "avaliado pós-parto", "animal em recuperação"). |

---

## 3. Histórias de Usuário

### US-01 — Registrar ECC de um animal
> **Como** produtor,  
> **quero** registrar o escore de condição corporal de um animal em uma data específica,  
> **para** acompanhar a evolução nutricional do animal ao longo do tempo.

**Critérios de aceite:**
- O produtor informa o escore (enum obrigatório) e a data da avaliação (obrigatória).
- Observações são opcionais.
- A data não pode ser futura.
- O animal deve estar ativo para receber um novo registro.
- O registro é imutável após criado — não há edição ou exclusão.

---

### US-02 — Consultar histórico de ECC de um animal
> **Como** produtor,  
> **quero** visualizar o histórico completo de ECC de um animal,  
> **para** acompanhar a evolução do estado nutricional ao longo do tempo.

**Critérios de aceite:**
- A lista retorna todos os registros de ECC do animal, ordenados do mais recente para o mais antigo.
- Cada registro exibe: escore, label do escore, data e observações.
- O último registro de ECC também aparece no perfil completo do animal (`AnimalDto`).

---

### US-03 — Consultar valores do enum de ECC (autocomplete)
> **Como** frontend,  
> **quero** consultar os valores válidos do escore de condição corporal,  
> **para** preencher o campo de seleção no formulário.

**Critérios de aceite:**
- Rota retorna `[{ value, label }]` com os 5 níveis do enum.
- Rota é protegida por autenticação.

---

## 4. Casos de Uso

### CU-01 — Registrar ECC

**Ator:** Produtor autenticado (ou sistema via Spec #5/#6)  
**Pré-condição:** Animal existe, pertence à propriedade e está ativo.

**Fluxo principal:**
1. Produtor envia `POST /api/animals/{animalId}/body-condition-records` com `BodyConditionRecordCreateDto`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está ativo — se não, lança `ConflictException`.
4. Sistema valida que `RecordedAt` não é futura — se for, retorna `422`.
5. Sistema cria o registro com `IsActive = true`.
6. Retorna `201 Created` com `BodyConditionRecordDto`.

**Fluxo alternativo — animal não encontrado:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

**Fluxo alternativo — animal inativo:**
- Passo 3 falha → lança `ConflictException` → `409 Conflict`.

**Fluxo alternativo — data futura:**
- Passo 4 falha → `422 Unprocessable Entity`.

---

### CU-02 — Listar Histórico de ECC

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe e pertence à propriedade.

**Fluxo principal:**
1. Produtor envia `GET /api/animals/{animalId}/body-condition-records`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Retorna lista de `BodyConditionRecordDto` ordenada por `RecordedAt` decrescente.
4. Se animal não encontrado → lança `NotFoundException` → `404 Not Found`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Nova entidade `BodyConditionRecord`

> Localização: `Domain/Models/BodyConditionRecord.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalId` | `int` | Sim | FK → `Animal`. |
| `Score` | `BodyConditionScore` (enum) | Sim | Escore de 1 a 5. |
| `RecordedAt` | `DateTime` | Sim | Data da avaliação. Não pode ser futura. |
| `Notes` | `string?` (max 500) | Não | Observações livres. |
| `Animal` | `Animal?` | — | Navigation property. |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. `IsActive` gerenciado pelo sistema — não exposto na API. |

```csharp
public class BodyConditionRecord : BaseEntity
{
    public int AnimalId { get; set; }

    public BodyConditionScore Score { get; set; }

    public DateTime RecordedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Animal? Animal { get; set; }
}
```

> **Nota:** `BodyConditionRecord` não implementa `ITenantEntity`. O isolamento de tenant é garantido pelo repositório via join com `Animal.PropertyId`.

---

#### Adição à entidade `Animal`

Adicionar a navigation collection em `Animal.cs`:

```csharp
public ICollection<BodyConditionRecord>? BodyConditionRecords { get; set; }
```

---

### 5.2 Novo Enum

> Localização: `Domain/Enums/BodyConditionScore.cs`

```csharp
public enum BodyConditionScore
{
    [Description("Magreza Severa")]
    SeverelyThin = 1,

    [Description("Estrutura Óssea Visível")]
    Thin = 2,

    [Description("Estrutura Óssea e Cobertura Bem Distribuídas")]
    Ideal = 3,

    [Description("Cobertura Predominante sobre Estrutura Óssea")]
    Fleshy = 4,

    [Description("Obesidade Severa")]
    SeverelyObese = 5
}
```

---

### 5.3 DTOs

#### `BodyConditionRecordCreateDto.cs`

```csharp
public class BodyConditionRecordCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "O escore de condição corporal é obrigatório.")]
    public BodyConditionScore Score { get; set; }

    [Required(ErrorMessage = "A data da avaliação é obrigatória.")]
    public DateTime RecordedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RecordedAt > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data da avaliação não pode ser futura.",
                new[] { nameof(RecordedAt) });
    }
}
```

---

#### `BodyConditionRecordDto.cs` (response)

```csharp
public class BodyConditionRecordDto
{
    public int Id { get; set; }
    public BodyConditionScore Score { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

#### Atualização de `AnimalDto.cs`

Adicionar ao `AnimalDto` existente (Spec #1) o último registro de ECC:

```csharp
public BodyConditionRecordDto? LastBodyConditionRecord { get; set; }
```

---

### 5.4 Endpoints da API

> Base: `api/animals` | Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/animals/{animalId}/body-condition-records` | Registrar ECC | `201 BodyConditionRecordDto` / `404` / `409` / `422` |
| `GET` | `/api/animals/{animalId}/body-condition-records` | Histórico de ECC do animal | `200 BodyConditionRecordDto[]` / `404` |
| `GET` | `/api/animals/body-condition-scores` | Enum de escores | `200 [{value, label}]` |

> **Não há** endpoints de PATCH ou DELETE — registros são imutáveis.

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `Score` é obrigatório. | DTO (DataAnnotations) |
| RN-02 | `RecordedAt` é obrigatório e não pode ser futura. | DTO (IValidatableObject) |
| RN-03 | Animal deve estar ativo para receber novo registro de ECC. | Service → lança `ConflictException` |
| RN-04 | Registros de ECC são imutáveis — sem PATCH ou DELETE na API. | Controller (ausência de endpoints) |
| RN-05 | Histórico retornado ordenado por `RecordedAt` decrescente. | Repository |
| RN-06 | Isolamento de tenant garantido via `Animal.PropertyId` no join. | Repository |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `BodyConditionRecord.cs` | Criar |
| `Domain/Models` | `Animal.cs` | Adicionar navigation `ICollection<BodyConditionRecord>?` |
| `Domain/Enums` | `BodyConditionScore.cs` | Criar |
| `Application/DTOs` | `BodyConditionRecordCreateDto.cs` | Criar |
| `Application/DTOs` | `BodyConditionRecordDto.cs` | Criar |
| `Application/DTOs` | `AnimalDto.cs` | Adicionar `LastBodyConditionRecord` |
| `Application/Mappings` | `BodyConditionRecordProfile.cs` | Criar |
| `Application/Mappings` | `AnimalProfile.cs` | Mapear `LastBodyConditionRecord` |
| `Application/Services` | `BodyConditionRecordService.cs` | Criar |
| `Application/Interfaces` | `IBodyConditionRecordService.cs` | Criar |
| `Application/Interfaces` | `IBodyConditionRecordRepository.cs` | Criar |
| `Infrastructure/Repositories` | `BodyConditionRecordRepository.cs` | Criar |
| `Api/Controllers` | `BodyConditionRecordsController.cs` | Criar (rotas aninhadas em `/animals/{animalId}/`) |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Criar tabela `BodyConditionRecords` com colunas: `Id`, `AnimalId` (FK → Animals, cascade delete), `Score` (int, not null), `RecordedAt` (datetime2, not null), `Notes` (nvarchar(500), nullable), `IsActive` (bit, not null, default 1), `CreatedAt` (datetime2, not null), `UpdatedAt` (datetime2, nullable).
2. Criar índice em `(AnimalId, RecordedAt)` para otimizar a busca do último registro.

---

## 7. Fora do Escopo deste Spec

- **Cadastro do animal** → Spec #1
- **Entrada e saída de animais** → Spec #2
- **Criação automática de ECC durante inseminação** → Spec #5
- **Criação automática de ECC durante gestação e parto** → Spec #6
- **Banco de Sêmen** → Spec #4
- **Dashboards de índices zootécnicos** → Spec #7
