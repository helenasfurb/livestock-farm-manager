# Spec: Entrada e Saída de Animais

**Módulo:** Animal — Entrada e Saída  
**Versão:** 1.0  
**Data:** 18/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais  
**Status:** Aprovado para implementação  
**Depende de:** Spec #1 — Gestão de Animais

---

## 1. Contexto e Objetivo

Todo animal do rebanho tem um ciclo de vida na propriedade: entra (por nascimento ou compra) e pode sair (por venda, morte, descarte ou transferência). Este spec define como registrar esses eventos de forma que o histórico do animal seja preservado e o rebanho ativo seja gerenciado corretamente.

A entrada já é coberta pelo cadastro do animal (Spec #1): o campo `Origin` (`BornOnFarm` ou `Purchased`) e a data de nascimento/aquisição são suficientes. Este spec foca exclusivamente no **registro de saída e na reativação**.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Saída modelada como campos diretos no animal, sem tabela separada | Não há necessidade de histórico de múltiplas movimentações no escopo atual. Simplicidade preferida. |
| D2 | Entrada por compra é o próprio cadastro do animal (Spec #1) | O campo `Origin = Purchased` com a data de nascimento/aquisição já serve como registro de entrada. Não há entidade separada de entrada. |
| D3 | Saída exige `ExitReason` e `ExitDate` obrigatórios | Sem motivo e data, o registro de saída não tem valor para gestão. |
| D4 | Campo `ExitNotes` como texto livre para todos os tipos de saída | Permite registrar comprador, destino de transferência, detalhes do descarte etc. sem campos extras por tipo. |
| D5 | `DeathCause` é obrigatório apenas quando `ExitReason = Death` | Causa de morte é a única informação específica por tipo que justifica um campo estruturado (para uso nos dashboards de mortalidade). |
| D6 | Animal pode ser reativado | Erros de registro ou retorno de animal transferido devem ser suportados. Reativação limpa todos os campos de saída. |
| D7 | `PATCH /api/animals/{id}/exit` substitui `DELETE /api/animals/{id}` do Spec #1 | Saída agora requer dados (motivo, data) que não cabem em um DELETE sem body. O endpoint DELETE é descontinuado. |

---

## 3. Histórias de Usuário

### US-01 — Registrar saída de um animal
> **Como** produtor,  
> **quero** registrar que um animal saiu do rebanho informando o motivo e a data,  
> **para** que ele deixe de aparecer no rebanho ativo e o histórico fique registrado.

**Critérios de aceite:**
- O produtor informa: motivo da saída (enum), data da saída e observações opcionais.
- Se o motivo for Morte, o produtor deve informar também a causa da morte (enum).
- Após o registro, o animal aparece como inativo (`IsActive = false`).
- O animal inativo continua acessível via consulta com filtro `IsActive = false`.
- Não é possível registrar saída de um animal já inativo.
- A data de saída não pode ser futura.

---

### US-02 — Reativar um animal
> **Como** produtor,  
> **quero** reativar um animal inativo,  
> **para** corrigir um registro de saída feito por engano ou registrar o retorno de um animal transferido.

**Critérios de aceite:**
- Ao reativar, o animal volta a aparecer no rebanho ativo (`IsActive = true`).
- Os campos de saída (`ExitDate`, `ExitReason`, `ExitNotes`, `DeathCause`) são limpos.
- Não é possível reativar um animal já ativo.

---

### US-03 — Consultar motivos de saída e causas de morte (autocomplete)
> **Como** frontend,  
> **quero** consultar os valores válidos de motivo de saída e causa de morte,  
> **para** preencher os campos de seleção nos formulários.

**Critérios de aceite:**
- Cada enum tem uma rota própria que retorna `[{ value, label }]`.
- As rotas são protegidas por autenticação.

---

## 4. Casos de Uso

### CU-01 — Registrar Saída de Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe, pertence à propriedade e está ativo (`IsActive = true`).

**Fluxo principal:**
1. Produtor envia `PATCH /api/animals/{id}/exit` com `AnimalExitDto`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está ativo — se não, retorna `409 Conflict`.
4. Sistema valida que `ExitDate` não é futura — se for, retorna `422`.
5. Se `ExitReason = Death`, valida que `DeathCause` foi informado — se não, retorna `422`.
6. Se `ExitReason ≠ Death`, valida que `DeathCause` não foi informado — se foi, retorna `422`.
7. Sistema aplica: `IsActive = false`, `ExitDate`, `ExitReason`, `ExitNotes`, `DeathCause`, `UpdatedAt = UtcNow`.
8. Retorna `200 OK` com `AnimalDto` atualizado.

**Fluxo alternativo — animal já inativo:**
- Passo 3 falha → Service lança `ConflictException` → middleware retorna `409 Conflict`.

**Fluxo alternativo — data futura:**
- Passo 4 falha → `422 Unprocessable Entity`.

**Fluxo alternativo — causa de morte ausente:**
- Passo 5 falha → `422 Unprocessable Entity` com mensagem "Causa da morte é obrigatória para saída por óbito."

---

### CU-02 — Reativar Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe, pertence à propriedade e está inativo (`IsActive = false`).

**Fluxo principal:**
1. Produtor envia `PATCH /api/animals/{id}/reactivate`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está inativo — se não, retorna `409 Conflict`.
4. Sistema aplica: `IsActive = true`, `ExitDate = null`, `ExitReason = null`, `ExitNotes = null`, `DeathCause = null`, `UpdatedAt = UtcNow`.
5. Retorna `200 OK` com `AnimalDto` atualizado.

**Fluxo alternativo — animal já ativo:**
- Passo 3 falha → Service lança `ConflictException` → middleware retorna `409 Conflict`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Alterações na entidade `Animal`

> Localização: `Domain/Models/Animal.cs`  
> Apenas adições — nenhum campo existente é removido ou alterado neste spec.

| Campo | Situação | Tipo | Notas |
|-------|----------|------|-------|
| `ExitDate` **(novo)** | Adicionado | `DateTime?` | Data de saída do rebanho. Nulo enquanto o animal está ativo. |
| `ExitReason` **(novo)** | Adicionado | `AnimalExitReason?` | Motivo da saída. Nulo enquanto ativo. |
| `ExitNotes` **(novo)** | Adicionado | `string?` (max 1000) | Observações livres sobre a saída (comprador, destino, etc.). |
| `DeathCause` **(novo)** | Adicionado | `AnimalDeathCause?` | Causa da morte. Preenchido apenas quando `ExitReason = Death`. |

**Trecho a adicionar em `Animal.cs`:**

```csharp
public DateTime? ExitDate { get; set; }

public AnimalExitReason? ExitReason { get; set; }

[MaxLength(1000)]
public string? ExitNotes { get; set; }

public AnimalDeathCause? DeathCause { get; set; }
```

---

### 5.2 Novos Enums

> Localização: `Domain/Enums/`

#### `AnimalExitReason.cs`
```csharp
public enum AnimalExitReason
{
    [Description("Venda")]
    Sale = 1,

    [Description("Morte")]
    Death = 2,

    [Description("Descarte")]
    Discard = 3,

    [Description("Transferência")]
    Transfer = 4
}
```

#### `AnimalDeathCause.cs`
```csharp
public enum AnimalDeathCause
{
    [Description("Doença")]
    Disease = 1,

    [Description("Acidente")]
    Accident = 2,

    [Description("Complicação Reprodutiva")]
    ReproductiveComplication = 3,

    [Description("Problema Digestivo")]
    DigestiveIssue = 4,

    [Description("Outros")]
    Other = 5
}
```

---

### 5.3 DTOs

#### `AnimalExitDto.cs` (input — saída)

```csharp
public class AnimalExitDto : IValidatableObject
{
    [Required(ErrorMessage = "O motivo de saída é obrigatório.")]
    public AnimalExitReason ExitReason { get; set; }

    [Required(ErrorMessage = "A data de saída é obrigatória.")]
    public DateTime ExitDate { get; set; }

    [MaxLength(1000)]
    public string? ExitNotes { get; set; }

    public AnimalDeathCause? DeathCause { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExitDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data de saída não pode ser futura.",
                new[] { nameof(ExitDate) });

        if (ExitReason == AnimalExitReason.Death && !DeathCause.HasValue)
            yield return new ValidationResult(
                "A causa da morte é obrigatória para saída por óbito.",
                new[] { nameof(DeathCause) });

        if (ExitReason != AnimalExitReason.Death && DeathCause.HasValue)
            yield return new ValidationResult(
                "A causa da morte só pode ser informada quando o motivo de saída for óbito.",
                new[] { nameof(DeathCause) });
    }
}
```

---

#### Atualização de `AnimalDto.cs` (adicionar campos de saída ao response)

> Os campos abaixo devem ser adicionados ao `AnimalDto` existente (Spec #1).

```csharp
public DateTime? ExitDate { get; set; }
public AnimalExitReason? ExitReason { get; set; }
public string? ExitReasonLabel { get; set; }
public string? ExitNotes { get; set; }
public AnimalDeathCause? DeathCause { get; set; }
public string? DeathCauseLabel { get; set; }
```

---

#### Atualização de `AnimalListItemDto.cs` (adicionar campo de saída à listagem)

> Adicionar ao `AnimalListItemDto` existente (Spec #1):

```csharp
public AnimalExitReason? ExitReason { get; set; }
public string? ExitReasonLabel { get; set; }
```

---

### 5.4 Endpoints da API

> Base: `api/animals` | Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `PATCH` | `/api/animals/{id}/exit` | Registrar saída do animal | `200 AnimalDto` / `404` / `409` / `422` |
| `PATCH` | `/api/animals/{id}/reactivate` | Reativar animal inativo | `200 AnimalDto` / `404` / `409` |
| `GET` | `/api/animals/exit-reasons` | Enum de motivos de saída | `200 [{value, label}]` |
| `GET` | `/api/animals/death-causes` | Enum de causas de morte | `200 [{value, label}]` |

> **Nota:** O endpoint `DELETE /api/animals/{id}` definido no Spec #1 é **descontinuado** e substituído por `PATCH /api/animals/{id}/exit`.

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `ExitReason` e `ExitDate` são obrigatórios para registrar saída. | DTO (DataAnnotations) |
| RN-02 | `ExitDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-03 | `DeathCause` é obrigatório quando `ExitReason = Death`. | DTO (IValidatableObject) |
| RN-04 | `DeathCause` não pode ser informado quando `ExitReason ≠ Death`. | DTO (IValidatableObject) |
| RN-05 | Não é possível registrar saída de um animal já inativo. | Service → lança `ConflictException` |
| RN-06 | Não é possível reativar um animal já ativo. | Service → lança `ConflictException` |
| RN-07 | Reativação limpa `ExitDate`, `ExitReason`, `ExitNotes` e `DeathCause`. | Service |
| RN-08 | Saída nunca exclui fisicamente o animal — sempre soft delete. | Service/Repository |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `Animal.cs` | Adicionar campos de saída conforme §5.1 |
| `Domain/Enums` | `AnimalExitReason.cs` | Criar |
| `Domain/Enums` | `AnimalDeathCause.cs` | Criar |
| `Application/DTOs` | `AnimalExitDto.cs` | Criar |
| `Application/DTOs` | `AnimalDto.cs` | Adicionar campos de saída |
| `Application/DTOs` | `AnimalListItemDto.cs` | Adicionar `ExitReason` e `ExitReasonLabel` |
| `Application/Mappings` | `AnimalProfile.cs` | Mapear novos campos de saída |
| `Application/Services` | `AnimalService.cs` | Implementar `ExitAnimalAsync` e `ReactivateAnimalAsync` |
| `Application/Interfaces` | `IAnimalService.cs` | Adicionar assinaturas dos dois novos métodos |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | Nenhuma mudança necessária (usa `UpdateAnimalAsync` existente) |
| `Api/Controllers` | `AnimalsController.cs` | Adicionar rotas `exit`, `reactivate`, `exit-reasons`, `death-causes`; remover `DELETE` |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Adicionar coluna `ExitDate` (datetime2, nullable) em `Animals`.
2. Adicionar coluna `ExitReason` (int, nullable) em `Animals`.
3. Adicionar coluna `ExitNotes` (nvarchar(1000), nullable) em `Animals`.
4. Adicionar coluna `DeathCause` (int, nullable) em `Animals`.

---

## 7. Fora do Escopo deste Spec

- **Cadastro inicial do animal (incluindo `Origin`)** → Spec #1
- **Escore de Condição Corporal (ECC)** → Spec #3
- **Banco de Sêmen** → Spec #4
- **Eventos Reprodutivos, Gestação e Parto** → Spec #5 e #6
- **Dashboards de mortalidade** (que usarão `DeathCause`) → Spec #7
