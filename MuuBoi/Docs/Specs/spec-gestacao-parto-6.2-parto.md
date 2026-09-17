# Spec 6.2: Parto (`AnimalCalving`)

**Módulo:** Gestação e Parto — Parto  
**Versão:** 1.0  
**Data:** 24/Ago/2026  
**Fonte:** Ordem de specs.txt; Spec #5 (Eventos Reprodutivos)  
**Status:** Aprovado para implementação  
**Depende de:** Spec 6.1 (Gestação), Spec #3 (ECC)  
**Parte de:** Spec #6 (Gestação e Parto)  
**Ver também:** Spec 6.1 (Gestação), Spec 6.3 (Cria)  
**Referenciado por:** Spec #7 (Dashboards)

---

## 1. Contexto e Objetivo

Este spec define o registro do parto (`AnimalCalving`) vinculado a uma gestação confirmada (`AnimalPregnancy.Status = Confirmed`). Ao registrar o parto, o sistema atualiza o status da gestação para `Calved`, cria os registros das crias (Spec 6.3) e, opcionalmente, gera um registro de ECC (Spec #3).

Este spec também **completa o estado `Postpartum` do `ReproductiveStatus`**, tornando-o funcional conforme previsto em D10 do Spec #5.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Apenas uma `AnimalCalving` ativa por `AnimalPregnancy` | Uma gestação resulta em um único evento de parto (que pode ter múltiplas crias). |
| D2 | Parto só pode ser registrado quando `AnimalPregnancy.Status = Confirmed` | Não faz sentido registrar parto de uma gestação já perdida ou já parida. |
| D3 | ECC é opcional no registro do parto; se informado, cria automaticamente um `BodyConditionRecord` | Parto é momento natural de avaliação corporal, mas nem todo produtor registra. |
| D4 | Ao inativar uma `AnimalCalving`, `AnimalPregnancy.Status` reverte para `Confirmed` | Permite correção sem apagar o histórico; a gestação volta ao estado aguardando parto. |
| D5 | `AnimalCalving` armazena `AnimalId` além de `AnimalPregnancyId` | Redundante mas otimiza queries de histórico por animal sem precisar de join com `AnimalPregnancy`. |
| D6 | `AnimalCalving` implementa `ITenantEntity` (PropertyId) | Consistência com o padrão do projeto; isolamento de tenant. |

---

## 3. Histórias de Usuário

### US-01 — Registrar parto
> **Como** produtor,  
> **quero** registrar o parto de uma vaca prenha,  
> **para** atualizar o status reprodutivo e registrar as informações das crias.

**Critérios de aceite:**
- O produtor informa: data do parto e ao menos uma cria (sexo e status vital — ver Spec 6.3).
- Peso da cria e observações são opcionais.
- ECC no parto é opcional; se informado, cria automaticamente um registro de ECC.
- Após o registro, `AnimalPregnancy.Status` passa a `Calved`.
- O status reprodutivo da vaca muda para `Postpartum` por 60 dias após o parto.
- A data do parto não pode ser futura nem anterior à data de confirmação da gestação.

---

### US-02 — Inativar parto (correção de erro)
> **Como** produtor,  
> **quero** inativar um parto registrado incorretamente,  
> **para** corrigir o cadastro e restaurar a gestação ao estado anterior.

**Critérios de aceite:**
- Soft delete: `AnimalCalving.IsActive = false`; todas as `AnimalCalvingCalf` também são inativadas.
- `AnimalPregnancy.Status` reverte para `Confirmed`.

---

## 4. Casos de Uso

### CU-01 — Registrar Parto

**Ator:** Produtor autenticado  
**Pré-condição:** `AnimalPregnancy` existe, pertence ao tenant e tem `Status = Confirmed`.

**Fluxo principal:**
1. Produtor envia `POST /api/pregnancies/{pregnancyId}/calvings` com `AnimalCalvingCreateDto`.
2. Sistema carrega a gestação e valida que pertence ao tenant.
3. Sistema valida que `Status = Confirmed` — se não, lança `ConflictException`.
4. Sistema valida que não há `AnimalCalving` ativa vinculada — se houver, lança `ConflictException`.
5. Sistema valida que `CalvingDate` não é futura — se for, lança `BusinessRuleException`.
6. Sistema valida que `CalvingDate >= AnimalPregnancy.ConfirmationDate` — se não, lança `BusinessRuleException`.
7. Sistema cria `AnimalCalving` com `IsActive = true` e `AnimalId` copiado da gestação.
8. Sistema cria uma `AnimalCalvingCalf` para cada cria informada no DTO (ver Spec 6.3).
9. Sistema atualiza `AnimalPregnancy.Status = Calved`.
10. Se `BodyConditionScore` informado, chama `IBodyConditionRecordService.CreateAsync` com `RecordedAt = CalvingDate`.
11. Retorna `201 Created` com `AnimalCalvingDto`.

**Fluxo alternativo — gestação não encontrada:** `NotFoundException` → `404`.  
**Fluxo alternativo — status inválido (não Confirmed):** Passo 3 → `ConflictException` → `409`.  
**Fluxo alternativo — parto já registrado:** Passo 4 → `ConflictException` → `409`.  
**Fluxo alternativo — data inválida:** Passos 5–6 → `BusinessRuleException` → `422`.

---

### CU-02 — Inativar Parto

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/calvings/{calvingId}`.
2. Sistema carrega o parto e valida que pertence ao tenant.
3. Sistema seta `AnimalCalving.IsActive = false`.
4. Sistema seta `IsActive = false` em todas as `AnimalCalvingCalf` vinculadas.
5. Sistema atualiza `AnimalPregnancy.Status = Confirmed`.
6. Retorna `204 No Content`.

**Fluxo alternativo — não encontrado:** `NotFoundException` → `404`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Entidade `AnimalCalving`

> Localização: `Domain/Models/AnimalCalving.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalPregnancyId` | `int` | Sim | FK → `AnimalPregnancy`. |
| `AnimalId` | `int` | Sim | FK → `Animal` (fêmea). Redundante, otimiza queries. |
| `CalvingDate` | `DateTime` | Sim | Data do parto. |
| `Notes` | `string?` (max 500) | Não | Observações do parto. |
| `PropertyId` | `Guid` | Sim | FK tenant. |
| `AnimalPregnancy` | navigation | — | Gestação de origem. |
| `Animal` | navigation | — | Fêmea que pariu. |
| `Calves` | navigation | — | Crias nascidas (Spec 6.3). |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. |

```csharp
public class AnimalCalving : BaseEntity, ITenantEntity
{
    public int AnimalPregnancyId { get; set; }

    public int AnimalId { get; set; }

    public DateTime CalvingDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public AnimalPregnancy? AnimalPregnancy { get; set; }
    public Animal? Animal { get; set; }
    public ICollection<AnimalCalvingCalf>? Calves { get; set; }
}
```

---

### 5.2 Adição a entidades existentes

`Animal.cs`:
```csharp
public ICollection<AnimalCalving>? Calvings { get; set; }
```

---

### 5.3 DTOs

#### `AnimalCalvingCreateDto.cs`

```csharp
public class AnimalCalvingCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "A data do parto é obrigatória.")]
    public DateTime CalvingDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public BodyConditionScore? BodyConditionScore { get; set; }

    [Required(ErrorMessage = "Informe ao menos uma cria.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma cria.")]
    public List<AnimalCalvingCalfCreateDto> Calves { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CalvingDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data do parto não pode ser futura.",
                new[] { nameof(CalvingDate) });
    }
}
```

---

#### `AnimalCalvingDto.cs` (response)

```csharp
public class AnimalCalvingDto
{
    public int Id { get; set; }
    public int AnimalPregnancyId { get; set; }
    public DateTime CalvingDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public List<AnimalCalvingCalfDto> Calves { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

### 5.4 Derivação do `ReproductiveStatus` — estado `Postpartum`

Complementa a seção 5.4 do Spec #5 e o Spec 6.1. No `AnimalService.GetByIdAsync`, o passo 3 da derivação:

```
3. Senão, se o último AnimalCalving ativo foi há menos de 60 dias (PostpartumDaysThreshold)
   AND não há BreedingEvent ativo com Status = AwaitingDiagnosis após essa data
   → ReproductiveStatus.Postpartum
```

> Query: `AnimalCalving WHERE AnimalId = X AND IsActive = true ORDER BY CalvingDate DESC TAKE 1`.

> **Implementação:** a lógica de prioridade e a constante `PostpartumDaysThreshold = 60` ficam em `ReproductiveStatusResolver` (`Application/Helpers`) — fonte única usada pelo detalhe e pela listagem de animais. (Substitui a nota original que a colocava em `BreedingEventService`.)

> O estado `Postpartum` também é **exibido e filtrável na listagem de animais** (`GET /api/animals?reproductiveStatus=`) — ver Spec 6.1, seção 5.5.1.

---

### 5.5 Endpoints da API

> Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/pregnancies/{id}/calvings` | Registrar parto | `201 AnimalCalvingDto` / `404` / `409` / `422` |
| `DELETE` | `/api/calvings/{id}` | Inativar parto | `204` / `404` |

---

### 5.6 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | Parto só pode ser registrado quando `AnimalPregnancy.Status = Confirmed`. | Service → lança `ConflictException` |
| RN-02 | Apenas uma `AnimalCalving` ativa por `AnimalPregnancy`. | Service → lança `ConflictException` |
| RN-03 | `CalvingDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-04 | `CalvingDate` deve ser >= `AnimalPregnancy.ConfirmationDate`. | Service → lança `BusinessRuleException` |
| RN-05 | Ao registrar parto, `AnimalPregnancy.Status` é atualizado para `Calved`. | `AnimalCalvingService.CreateAsync` |
| RN-06 | Ao registrar parto com `BodyConditionScore`, cria automaticamente `BodyConditionRecord`. | `AnimalCalvingService.CreateAsync` → chama `IBodyConditionRecordService` |
| RN-07 | Ao inativar parto, `AnimalPregnancy.Status` reverte para `Confirmed`. | `AnimalCalvingService.InactivateAsync` |
| RN-08 | Ao inativar parto, todas as `AnimalCalvingCalf` vinculadas são inativadas automaticamente. | `AnimalCalvingService.InactivateAsync` |
| RN-09 | `ReproductiveStatus.Postpartum` ativo nos primeiros 60 dias após o último parto ativo, sem cobertura pendente após o parto. | `AnimalService.GetByIdAsync` |
| RN-10 | `AnimalCalvingCreateDto.Calves` deve ter ao menos um item. | DTO (`MinLength`) |
| RN-11 | Isolamento de tenant: repositório filtra por `PropertyId`. | Repository |

---

### 5.7 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalCalving.cs` | **Criar** |
| `Domain/Models` | `Animal.cs` | Adicionar navigation `ICollection<AnimalCalving>? Calvings` |
| `Application/DTOs` | `AnimalCalvingCreateDto.cs` | **Criar** |
| `Application/DTOs` | `AnimalCalvingDto.cs` | **Criar** |
| `Application/Mappings` | `AnimalCalvingProfile.cs` | **Criar** |
| `Application/Services` | `AnimalCalvingService.cs` | **Criar** |
| `Application/Services` | `AnimalService.cs` | Adicionar estado `Postpartum` na derivação de `ReproductiveStatus` |
| `Application/Interfaces` | `IAnimalCalvingService.cs` | **Criar** |
| `Application/Interfaces` | `IAnimalCalvingRepository.cs` | **Criar** |
| `Infrastructure/Repositories` | `AnimalCalvingRepository.cs` | **Criar** |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Adicionar `DbSet<AnimalCalving>`; configurar FKs e índices em `OnModelCreating` |
| `Api/Controllers` | `CalvingsController.cs` | **Criar** |
| `Infrastructure/Migrations` | *(ver seção 6)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

**Criar tabela `AnimalCalvings`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `AnimalPregnancyId` | int | not null, FK → `AnimalPregnancies`, `ON DELETE RESTRICT` |
| `AnimalId` | int | not null, FK → `Animals`, `ON DELETE RESTRICT` |
| `CalvingDate` | datetime2 | not null |
| `Notes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Índices:**

| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(AnimalPregnancyId)` | Composto | Otimiza busca do parto de uma gestação. |
| `(AnimalId, CalvingDate DESC)` | Composto | Otimiza derivação do status `Postpartum` no `AnimalService`. |

---

## 7. Fora do Escopo deste Spec

- **Dados das crias** (sexo, peso, status vital) → Spec 6.3
- **Gestação** (criação automática, perda gestacional) → Spec 6.1
- **Cadastro de animais** → Spec #1
- **Escore de Condição Corporal** → Spec #3
- **Eventos Reprodutivos** → Spec #5
- **Dashboards de índices zootécnicos** (taxa de natalidade, intervalo entre partos) → Spec #7
