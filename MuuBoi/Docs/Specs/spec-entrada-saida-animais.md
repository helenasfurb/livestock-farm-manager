# Spec: Entrada e Saída de Animais

**Módulo:** Animal — Entrada e Saída  

**Versão:** 1.1  
**Data:** 21/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais  
**Status:** Aprovado para implementação  
**Depende de:** Spec #1 — Gestão de Animais

---

## Changelog

| Versão | Data | Alteração |
|--------|------|-----------|
| 1.0 | 18/Ago/2026 | Versão inicial |
| 1.1 | 21/Ago/2026 | Saída migrada para entidade separada `AnimalExitRecord` (histórico preservado entre reativações). Enum `AnimalDeathCause` e campo `DeathCause` removidos — substituídos por `ExitNotes` (texto livre). |

---

## 1. Contexto e Objetivo

Todo animal do rebanho tem um ciclo de vida na propriedade: entra (por nascimento ou compra) e pode sair (por venda, morte, descarte ou transferência). Este spec define como registrar esses eventos de forma que o histórico do animal seja preservado e o rebanho ativo seja gerenciado corretamente.

A entrada já é coberta pelo cadastro do animal (Spec #1): o campo `Origin` (`BornOnFarm` ou `Purchased`) e a data de nascimento/aquisição são suficientes. Este spec foca exclusivamente no **registro de saída e na reativação**.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Saída modelada como entidade separada `AnimalExitRecord` com histórico | Necessário preservar o histórico completo de saídas entre reativações. O dashboard de taxa de mortalidade (Spec #7) precisa consultar datas e motivos de saída mesmo após o animal ser reativado. |
| D2 | Entrada por compra é o próprio cadastro do animal (Spec #1) | O campo `Origin = Purchased` com a data de nascimento/aquisição já serve como registro de entrada. Não há entidade separada de entrada. |
| D3 | Saída exige `ExitReason` e `ExitDate` obrigatórios | Sem motivo e data, o registro de saída não tem valor para gestão nem para os dashboards. |
| D4 | `DeathCause` (enum) removido — substituído por `ExitNotes` (texto livre) | Campo de texto único (`ExitNotes`, max 1000) cobre todos os tipos de saída sem necessidade de enum separado para causa de morte. O produtor descreve livremente o motivo. |
| D5 | Animal pode ser reativado | Erros de registro ou retorno de animal transferido devem ser suportados. Reativação apenas seta `IsActive = true` — o histórico de saídas em `AnimalExitRecord` é preservado intacto. |
| D6 | `PATCH /api/animals/{id}/exit` substitui `DELETE /api/animals/{id}` do Spec #1 | Saída agora requer dados (motivo, data) que não cabem em um DELETE sem body. O endpoint DELETE é descontinuado. |
| D7 | `AnimalExitRecord` não implementa `ITenantEntity` diretamente | Isolamento de tenant garantido via join com `Animal.PropertyId`, igual ao padrão de `BodyConditionRecord` (Spec #3). |

---

## 3. Histórias de Usuário

### US-01 — Registrar saída de um animal
> **Como** produtor,  
> **quero** registrar que um animal saiu do rebanho informando o motivo e a data,  
> **para** que ele deixe de aparecer no rebanho ativo e o evento fique no histórico.

**Critérios de aceite:**
- O produtor informa: motivo da saída (enum `AnimalExitReason`), data da saída e observações opcionais (texto livre).
- Após o registro, o animal aparece como inativo (`IsActive = false`).
- Um registro em `AnimalExitRecord` é criado com os dados da saída.
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
- O histórico de saídas (`AnimalExitRecord`) é preservado — não é apagado.
- Não é possível reativar um animal já ativo.

---

### US-03 — Consultar histórico de saídas de um animal
> **Como** produtor,  
> **quero** visualizar o histórico de saídas de um animal,  
> **para** acompanhar todos os eventos de saída e reativação ao longo do tempo.

**Critérios de aceite:**
- A lista retorna todos os registros de saída do animal, ordenados do mais recente para o mais antigo.
- Cada registro exibe: motivo, data, observações e data de criação do registro.

---

### US-04 — Consultar motivos de saída (autocomplete)
> **Como** frontend,  
> **quero** consultar os valores válidos de motivo de saída,  
> **para** preencher o campo de seleção nos formulários.

**Critérios de aceite:**
- Rota retorna `[{ value, label }]`.
- Rota é protegida por autenticação.

---

## 4. Casos de Uso

### CU-01 — Registrar Saída de Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe, pertence à propriedade e está ativo (`IsActive = true`).

**Fluxo principal:**
1. Produtor envia `PATCH /api/animals/{id}/exit` com `AnimalExitDto`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está ativo — se não, lança `ConflictException`.
4. Sistema valida que `ExitDate` não é futura — se for, retorna `422`.
5. Sistema cria um registro em `AnimalExitRecord` com os dados fornecidos.
6. Sistema seta `IsActive = false` e `UpdatedAt = UtcNow` no animal.
7. Retorna `200 OK` com `AnimalDto` atualizado (inclui `LastExitRecord`).

**Fluxo alternativo — animal não encontrado:** `NotFoundException` → `404 Not Found`.  
**Fluxo alternativo — animal já inativo:** Passo 3 → `ConflictException` → `409 Conflict`.  
**Fluxo alternativo — data futura:** Passo 4 → `422 Unprocessable Entity`.

---

### CU-02 — Reativar Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe, pertence à propriedade e está inativo (`IsActive = false`).

**Fluxo principal:**
1. Produtor envia `PATCH /api/animals/{id}/reactivate`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está inativo — se não, lança `ConflictException`.
4. Sistema seta `IsActive = true` e `UpdatedAt = UtcNow`.
5. Retorna `200 OK` com `AnimalDto` atualizado.

> **Nota:** O histórico de saídas (`AnimalExitRecord`) é mantido integralmente. Nenhum registro é apagado ou alterado na reativação.

**Fluxo alternativo — animal não encontrado:** `NotFoundException` → `404 Not Found`.  
**Fluxo alternativo — animal já ativo:** Passo 3 → `ConflictException` → `409 Conflict`.

---

### CU-03 — Listar Histórico de Saídas de um Animal

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/animals/{id}/exit-records`.
2. Sistema valida que o animal pertence ao tenant.
3. Retorna lista de `AnimalExitRecordDto` ordenada por `ExitDate` decrescente.
4. Se animal não encontrado → `NotFoundException` → `404 Not Found`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Nova entidade `AnimalExitRecord`

> Localização: `Domain/Models/AnimalExitRecord.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalId` | `int` | Sim | FK → `Animal`. |
| `ExitReason` | `AnimalExitReason` (enum) | Sim | Motivo da saída. |
| `ExitDate` | `DateTime` | Sim | Data da saída. Não pode ser futura. |
| `ExitNotes` | `string?` (max 1000) | Não | Observações livres (comprador, destino, detalhes do óbito etc.). |
| `Animal` | navigation | — | Navigation property. |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. `IsActive` gerenciado pelo sistema. |

```csharp
public class AnimalExitRecord : BaseEntity
{
    public int AnimalId { get; set; }

    public AnimalExitReason ExitReason { get; set; }

    public DateTime ExitDate { get; set; }

    [MaxLength(1000)]
    public string? ExitNotes { get; set; }

    public Animal? Animal { get; set; }
}
```

> **Nota:** `AnimalExitRecord` não implementa `ITenantEntity`. O isolamento de tenant é garantido pelo repositório via join com `Animal.PropertyId`.

---

#### Adições à entidade `Animal`

Adicionar navigation collection em `Animal.cs`:

```csharp
public ICollection<AnimalExitRecord>? ExitRecords { get; set; }
```

> **Importante:** Os campos `ExitDate`, `ExitReason`, `ExitNotes` e `DeathCause` que seriam adicionados à entidade `Animal` na versão 1.0 deste spec **não existem mais**. Se já foram adicionados ao modelo, devem ser removidos. O estado de saída do animal é derivado do `AnimalExitRecord` mais recente.

---

### 5.2 Enum mantido e enum removido

> Localização: `Domain/Enums/`

#### `AnimalExitReason.cs` — mantido sem alteração
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

#### `AnimalDeathCause.cs` — **removido**
O enum `AnimalDeathCause` não existe mais. Se já foi criado, deve ser deletado de `Domain/Enums/`.

---

### 5.3 DTOs

#### `AnimalExitDto.cs` (input — registrar saída)

```csharp
public class AnimalExitDto : IValidatableObject
{
    [Required(ErrorMessage = "O motivo de saída é obrigatório.")]
    public AnimalExitReason ExitReason { get; set; }

    [Required(ErrorMessage = "A data de saída é obrigatória.")]
    public DateTime ExitDate { get; set; }

    [MaxLength(1000)]
    public string? ExitNotes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExitDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data de saída não pode ser futura.",
                new[] { nameof(ExitDate) });
    }
}
```

---

#### `AnimalExitRecordDto.cs` (response — histórico de saída)

```csharp
public class AnimalExitRecordDto
{
    public int Id { get; set; }
    public AnimalExitReason ExitReason { get; set; }
    public string ExitReasonLabel { get; set; } = string.Empty;
    public DateTime ExitDate { get; set; }
    public string? ExitNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

#### Atualização de `AnimalDto.cs`

Substituir os campos de saída individuais (versão 1.0) pelo último registro de saída:

```csharp
// Substitui: ExitDate, ExitReason, ExitReasonLabel, ExitNotes, DeathCause, DeathCauseLabel
public AnimalExitRecordDto? LastExitRecord { get; set; }
```

---

#### Atualização de `AnimalListItemDto.cs`

Substituir campos de saída individuais (versão 1.0) pelo motivo do último registro:

```csharp
// Substitui: ExitReason, ExitReasonLabel
public AnimalExitReason? LastExitReason { get; set; }
public string? LastExitReasonLabel { get; set; }
```

> **Nota de implementação:** `LastExitReason` é derivado pelo repositório via LEFT JOIN para o `AnimalExitRecord` mais recente (`ORDER BY ExitDate DESC`, `TAKE 1`).

---

### 5.4 Endpoints da API

> Base: `api/animals` | Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `PATCH` | `/api/animals/{id}/exit` | Registrar saída do animal | `200 AnimalDto` / `404` / `409` / `422` |
| `PATCH` | `/api/animals/{id}/reactivate` | Reativar animal inativo | `200 AnimalDto` / `404` / `409` |
| `GET` | `/api/animals/{id}/exit-records` | Histórico de saídas do animal | `200 AnimalExitRecordDto[]` / `404` |
| `GET` | `/api/animals/exit-reasons` | Enum de motivos de saída | `200 [{value, label}]` |

> **Endpoints removidos em relação à versão 1.0:**
> - `GET /api/animals/death-causes` — removido junto com o enum `AnimalDeathCause`.
>
> **Endpoint descontinuado (Spec #1):**
> - `DELETE /api/animals/{id}` — substituído por `PATCH /api/animals/{id}/exit`.

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `ExitReason` e `ExitDate` são obrigatórios para registrar saída. | DTO (DataAnnotations) |
| RN-02 | `ExitDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-03 | Não é possível registrar saída de um animal já inativo. | Service → lança `ConflictException` |
| RN-04 | Não é possível reativar um animal já ativo. | Service → lança `ConflictException` |
| RN-05 | Reativação preserva todos os registros em `AnimalExitRecord` — nenhum é apagado ou alterado. | Service |
| RN-06 | Saída nunca exclui fisicamente o animal — sempre soft delete via `IsActive = false`. | Service |
| RN-07 | Isolamento de tenant nos registros de saída garantido via `Animal.PropertyId` no join. | Repository |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalExitRecord.cs` | Criar |
| `Domain/Models` | `Animal.cs` | Adicionar navigation `ICollection<AnimalExitRecord>? ExitRecords`; **remover** `ExitDate`, `ExitReason`, `ExitNotes`, `DeathCause` se já adicionados |
| `Domain/Enums` | `AnimalExitReason.cs` | Manter sem alteração |
| `Domain/Enums` | `AnimalDeathCause.cs` | **Deletar** (se existir) |
| `Application/DTOs` | `AnimalExitDto.cs` | Criar (sem campo `DeathCause`) |
| `Application/DTOs` | `AnimalExitRecordDto.cs` | Criar |
| `Application/DTOs` | `AnimalDto.cs` | Substituir campos de saída individuais por `LastExitRecord` |
| `Application/DTOs` | `AnimalListItemDto.cs` | Substituir por `LastExitReason` e `LastExitReasonLabel` |
| `Application/Mappings` | `AnimalExitRecordProfile.cs` | Criar |
| `Application/Mappings` | `AnimalProfile.cs` | Mapear `LastExitRecord` e `LastExitReason` |
| `Application/Services` | `AnimalService.cs` | Implementar `ExitAnimalAsync` (cria `AnimalExitRecord` + seta `IsActive`) e `ReactivateAnimalAsync` (só seta `IsActive`) |
| `Application/Interfaces` | `IAnimalService.cs` | Adicionar assinaturas dos dois métodos |
| `Application/Interfaces` | `IAnimalExitRecordRepository.cs` | Criar |
| `Infrastructure/Repositories` | `AnimalExitRecordRepository.cs` | Criar |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | Ajustar `GetAll` e `GetById` para LEFT JOIN com `AnimalExitRecord` mais recente |
| `Api/Controllers` | `AnimalsController.cs` | Adicionar rotas `exit`, `reactivate`, `exit-records/{animalId}`, `exit-reasons`; remover `death-causes`; remover `DELETE` |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Criar tabela `AnimalExitRecords` com colunas: `Id` (int, PK), `AnimalId` (int, FK → Animals, not null), `ExitReason` (int, not null), `ExitDate` (datetime2, not null), `ExitNotes` (nvarchar(1000), nullable), `IsActive` (bit, not null, default 1), `CreatedAt` (datetime2, not null), `UpdatedAt` (datetime2, nullable).
2. FK `AnimalId → Animals.Id` com `ON DELETE RESTRICT` (sem cascade — o animal deve existir).
3. Criar índice em `(AnimalId, ExitDate DESC)` — otimiza a busca pelo último registro de saída e o histórico.
4. Criar índice em `(ExitDate, ExitReason)` — otimiza o dashboard de mortalidade por período e motivo (Spec #7).
5. Se os campos `ExitDate`, `ExitReason`, `ExitNotes`, `DeathCause` já existirem na tabela `Animals` (adicionados pela migration v1.0), devem ser removidos nesta migration.

---

## 7. Fora do Escopo deste Spec

- **Cadastro inicial do animal (incluindo `Origin`)** → Spec #1
- **Escore de Condição Corporal (ECC)** → Spec #3
- **Banco de Sêmen** → Spec #4
- **Eventos Reprodutivos, Gestação e Parto** → Spec #5 e #6
- **Dashboard de taxa de mortalidade** (que consultará `AnimalExitRecords` por `ExitDate` e `ExitReason = Death`) → Spec #7
