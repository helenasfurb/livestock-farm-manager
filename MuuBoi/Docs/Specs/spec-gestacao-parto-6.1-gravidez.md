# Spec 6.1: Gestação (`AnimalPregnancy`)

**Módulo:** Gestação e Parto — Gestação  
**Versão:** 1.0  
**Data:** 24/Ago/2026  
**Fonte:** Ordem de specs.txt; Spec #5 (Eventos Reprodutivos)  
**Status:** Aprovado para implementação  
**Depende de:** Spec #5 (Eventos Reprodutivos)  
**Parte de:** Spec #6 (Gestação e Parto)  
**Ver também:** Spec 6.2 (Parto), Spec 6.3 (Cria)  
**Referenciado por:** Spec #7 (Dashboards)

---

## 1. Contexto e Objetivo

Ao marcar um `BreedingEvent` como `Successful` no Spec #5, o sistema deve criar automaticamente um registro de `AnimalPregnancy`. Este spec define o ciclo de vida completo da gestação: acompanhamento, registro de perda gestacional e inativação para correção de erro.

Este spec também **completa implementações pendentes do Spec #5**:
- Criação automática de `AnimalPregnancy` ao confirmar prenhez (D9 do Spec #5).
- Verificação de gestação vinculada ao inativar `BreedingEvent` (CU-05 passo 3 do Spec #5).
- Estado `Pregnant` do `ReproductiveStatus` torna-se funcional (D10 do Spec #5).

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | `AnimalPregnancy` é criada automaticamente pelo sistema; o produtor não a cria diretamente | A gestação é consequência direta da confirmação. Criar manualmente geraria inconsistência com o `BreedingEvent`. |
| D2 | `ExpectedCalvingDate = BreedingEvent.BreedingDate + 280 dias` | 280 dias é a gestação padrão de bovinos. Calculado a partir da data da cobertura, não da confirmação — a confirmação ocorre semanas depois. |
| D3 | `AnimalPregnancy` tem três estados: `Confirmed`, `Calved`, `LostPregnancy` | Cobre os desfechos reais: gestação em curso, parto realizado e aborto/perda. |
| D4 | Status `LostPregnancy` é imutável após definido | Para correção de erro, o produtor inativa a gestação (soft delete). |
| D5 | Inativar `AnimalPregnancy` com `AnimalCalving` ativa é bloqueado | A ordem correta para correção é inativar o parto primeiro, depois a gestação (Spec 6.2). |
| D6 | Inativar `AnimalPregnancy` desvincula a gestação do `BreedingEvent` | Permite ao produtor inativar o `BreedingEvent` em seguida, para correção de erros de cadastro. |
| D7 | `AnimalPregnancy` implementa `ITenantEntity` (PropertyId) | Consistência com o padrão do projeto; isolamento de tenant. |

---

## 3. Histórias de Usuário

### US-01 — Acompanhar gestação
> **Como** produtor,  
> **quero** ver as gestações ativas e o histórico de gestações de uma fêmea,  
> **para** acompanhar a vida reprodutiva do animal.

**Critérios de aceite:**
- A listagem exibe: data de confirmação, data prevista do parto, status e se está ativa.
- O detalhe inclui os dados do parto e das crias, se já registrado (Spec 6.2 / 6.3).
- O status reprodutivo `Prenha` é exibido no perfil do animal enquanto a gestação estiver `Confirmed` e ativa.

---

### US-02 — Registrar perda de gestação (aborto)
> **Como** produtor,  
> **quero** registrar que uma fêmea perdeu a gestação,  
> **para** atualizar o status reprodutivo e manter o histórico correto.

**Critérios de aceite:**
- O produtor informa a data da perda. Observações são opcionais.
- `AnimalPregnancy.Status` passa a `LostPregnancy`.
- O animal retorna ao status `Open` (Vazia).
- Só é possível para gestações com `Status = Confirmed`.

---

### US-03 — Inativar gestação (correção de erro)
> **Como** produtor,  
> **quero** inativar uma gestação cadastrada incorretamente,  
> **para** corrigir o histórico sem apagar os dados.

**Critérios de aceite:**
- Só é possível inativar se não houver `AnimalCalving` ativa vinculada.
- Soft delete: `IsActive = false`. A gestação não aparece mais nos cálculos de status reprodutivo.
- Após inativar a gestação, o `BreedingEvent` associado pode ser inativado se necessário.

---

## 4. Casos de Uso

### CU-01 — Criação Automática de Gestação ao Confirmar Prenhez (complemento de CU-02 do Spec #5)

**Ator:** Sistema (efeito colateral de `BreedingEventService.UpdateStatusAsync`)  
**Pré-condição:** `BreedingEventStatusUpdateDto.Status = Successful`.

**Fluxo:**
1. `BreedingEventService.UpdateStatusAsync` atualiza `BreedingEvent.Status = Successful` e `DiagnosisDate`.
2. Chama `IAnimalPregnancyService.CreateForBreedingEventAsync(breedingEvent, diagnosisDate)`.
3. O serviço cria `AnimalPregnancy` com:
   - `AnimalId` = `breedingEvent.AnimalId`
   - `BreedingEventId` = `breedingEvent.Id`
   - `ConfirmationDate` = `diagnosisDate`
   - `ExpectedCalvingDate` = `breedingEvent.BreedingDate.AddDays(280)`
   - `Status` = `Confirmed`
   - `PropertyId` = `breedingEvent.PropertyId`
   - `IsActive` = `true`

---

### CU-02 — Registrar Perda de Gestação

**Ator:** Produtor autenticado  
**Pré-condição:** `AnimalPregnancy` existe, pertence ao tenant e tem `Status = Confirmed`.

**Fluxo principal:**
1. Produtor envia `PATCH /api/pregnancies/{pregnancyId}/status` com `AnimalPregnancyStatusUpdateDto`.
2. Sistema carrega a gestação e valida que pertence ao tenant.
3. Sistema valida que `Status = Confirmed` — se não, lança `ConflictException`.
4. Sistema atualiza `Status = LostPregnancy`, `LossDate` e `Notes` (se informado).
5. Retorna `200 OK` com `AnimalPregnancyDto`.

**Fluxo alternativo — não encontrada:** `NotFoundException` → `404`.  
**Fluxo alternativo — status inválido:** Passo 3 → `ConflictException` → `409`.

---

### CU-03 — Listar Gestações de um Animal

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/animals/{animalId}/pregnancies`.
2. Sistema valida que o animal pertence ao tenant — se não, `NotFoundException` → `404`.
3. Retorna lista de `AnimalPregnancyListItemDto` ordenada por `ConfirmationDate` decrescente.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `isActive` | bool? | `true` = só ativas, `false` = só inativas, `null` = todas |

---

### CU-04 — Detalhe de Gestação

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/pregnancies/{id}`.
2. Sistema valida que a gestação pertence ao tenant.
3. Retorna `AnimalPregnancyDto` com `AnimalCalvingDto` aninhado (se existir).

**Fluxo alternativo — não encontrada:** `NotFoundException` → `404`.

---

### CU-05 — Inativar Gestação

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/pregnancies/{pregnancyId}`.
2. Sistema carrega a gestação e valida que pertence ao tenant.
3. Sistema verifica que não há `AnimalCalving` ativa vinculada — se houver, lança `ConflictException`.
4. Sistema seta `AnimalPregnancy.IsActive = false`.
5. Retorna `204 No Content`.

**Fluxo alternativo — não encontrada:** `NotFoundException` → `404`.  
**Fluxo alternativo — parto ativo vinculado:** Passo 3 → `ConflictException` → `409`.

---

### CU-06 — Verificação de Gestação ao Inativar BreedingEvent (complemento de CU-05 do Spec #5)

**Ator:** Sistema (executado dentro de `BreedingEventService.InactivateAsync`)

**Fluxo:**
1. `BreedingEventService.InactivateAsync` chama `IAnimalPregnancyRepository.ExistsActiveForBreedingEventAsync(breedingEventId)`.
2. Se retornar `true` → lança `ConflictException` ("Este evento possui uma gestação ativa vinculada. Inative a gestação primeiro.").
3. Se retornar `false` → prossegue com a inativação normalmente.

---

### CU-07 — Listar Gestações da Propriedade (com filtros)

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/pregnancies` com filtros opcionais (query params).
2. Sistema retorna lista de `AnimalPregnancyListItemDto` (com `AnimalId`/`AnimalTagNumber`) ordenada por `ConfirmationDate` decrescente, isolada por tenant.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `animalId` | int? | Filtra por animal. |
| `status` | AnimalPregnancyStatus? | Filtra por status da gestação. |
| `isActive` | bool? | `true` = só ativas, `false` = só inativas, `null` = todas. |

---

## 5. Especificação Técnica de Modelagem

### 5.1 Entidade `AnimalPregnancy`

> Localização: `Domain/Models/AnimalPregnancy.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalId` | `int` | Sim | FK → `Animal`. |
| `BreedingEventId` | `int` | Sim | FK → `BreedingEvent`. Unique — um evento gera no máximo uma gestação. |
| `ConfirmationDate` | `DateTime` | Sim | Data do diagnóstico de prenhez (= `BreedingEvent.DiagnosisDate`). |
| `ExpectedCalvingDate` | `DateTime` | Sim | `BreedingDate + 280 dias`. Calculado no serviço. |
| `LossDate` | `DateTime?` | Não | Preenchido ao registrar perda gestacional. |
| `Status` | `AnimalPregnancyStatus` | Sim | Inicia como `Confirmed`. |
| `Notes` | `string?` (max 500) | Não | Observações; preenchível ao registrar perda. |
| `PropertyId` | `Guid` | Sim | FK tenant. |
| `Animal` | navigation | — | Fêmea gestante. |
| `BreedingEvent` | navigation | — | Evento de cobertura originador. |
| `Calving` | navigation | — | Parto vinculado (nullable). |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. |

```csharp
public class AnimalPregnancy : BaseEntity, ITenantEntity
{
    public int AnimalId { get; set; }

    public int BreedingEventId { get; set; }

    public DateTime ConfirmationDate { get; set; }

    public DateTime ExpectedCalvingDate { get; set; }

    public DateTime? LossDate { get; set; }

    public AnimalPregnancyStatus Status { get; set; } = AnimalPregnancyStatus.Confirmed;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public Animal? Animal { get; set; }
    public BreedingEvent? BreedingEvent { get; set; }
    public AnimalCalving? Calving { get; set; }
}
```

> **Nota EF Core:** `BreedingEventId` deve ter índice único para garantir que um `BreedingEvent` resulte em no máximo uma `AnimalPregnancy`.

---

### 5.2 Adições a entidades existentes

`Animal.cs`:
```csharp
public ICollection<AnimalPregnancy>? Pregnancies { get; set; }
```

`BreedingEvent.cs`:
```csharp
public AnimalPregnancy? Pregnancy { get; set; }
```

---

### 5.3 Novo Enum `AnimalPregnancyStatus`

> Localização: `Domain/Enums/AnimalPregnancyStatus.cs`

```csharp
public enum AnimalPregnancyStatus
{
    [Description("Gestação Confirmada")]
    Confirmed = 1,

    [Description("Parto Realizado")]
    Calved = 2,

    [Description("Perda Gestacional")]
    LostPregnancy = 3
}
```

---

### 5.4 DTOs

#### `AnimalPregnancyDto.cs` (response — detalhe)

```csharp
public class AnimalPregnancyDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string AnimalTagNumber { get; set; } = string.Empty;
    public int BreedingEventId { get; set; }
    public DateTime ConfirmationDate { get; set; }
    public DateTime ExpectedCalvingDate { get; set; }
    public DateTime? LossDate { get; set; }
    public AnimalPregnancyStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public AnimalCalvingDto? Calving { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

#### `AnimalPregnancyListItemDto.cs` (response — listagem)

```csharp
public class AnimalPregnancyListItemDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string AnimalTagNumber { get; set; } = string.Empty;
    public int BreedingEventId { get; set; }
    public DateTime ConfirmationDate { get; set; }
    public DateTime ExpectedCalvingDate { get; set; }
    public DateTime? LossDate { get; set; }
    public EnumValueDto? Status { get; set; }
    public bool IsActive { get; set; }
}
```

> **Convenção de enum (implementação):** os campos de enum de resposta são serializados como `EnumValueDto { value, label }`, seguindo o padrão do projeto (idem `BreedingEventDto`/`AnimalDto`). Vale para `Status` aqui, em `AnimalPregnancyDto` e para `Sex`/`VitalStatus` em `AnimalCalvingCalfDto`. `AnimalId`/`AnimalTagNumber` foram adicionados ao item de lista para dar sentido à listagem global (CU-07).

---

#### `AnimalPregnancyStatusUpdateDto.cs`

```csharp
public class AnimalPregnancyStatusUpdateDto : IValidatableObject
{
    [Required(ErrorMessage = "O status é obrigatório.")]
    public AnimalPregnancyStatus Status { get; set; }

    [Required(ErrorMessage = "A data da perda é obrigatória.")]
    public DateTime LossDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status != AnimalPregnancyStatus.LostPregnancy)
            yield return new ValidationResult(
                "Apenas o status 'Perda Gestacional' pode ser definido manualmente. 'Parto Realizado' é gerado automaticamente pelo sistema.",
                new[] { nameof(Status) });

        if (LossDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data da perda não pode ser futura.",
                new[] { nameof(LossDate) });
    }
}
```

---

### 5.5 Derivação do `ReproductiveStatus` — estado `Pregnant`

Complementa a seção 5.4 do Spec #5. No `AnimalService.GetByIdAsync`, o passo 1 da derivação:

```
1. Se há AnimalPregnancy ativa com Status = Confirmed
   → ReproductiveStatus.Pregnant
```

> Query: `AnimalPregnancy WHERE AnimalId = X AND Status = Confirmed AND IsActive = true`.

---

### 5.5.1 Exposição e filtro do `ReproductiveStatus` na listagem de animais

O status reprodutivo passa a ser **exibido e filtrável na listagem de animais** (`GET /api/animals`), não só no detalhe.

- `AnimalListItemDto.ReproductiveStatus` (`EnumValueDto?`) — preenchido apenas para fêmeas (`Cow`/`Heifer`); `null` para os demais.
- Novo filtro `AnimalFilterDto.ReproductiveStatus` (`ReproductiveStatus?`) — permite buscar animais `Prenhe`, `Pós-parto`, `Aguardando confirmação` ou `Vazia`.
- Lookup para o dropdown do filtro: `GET /api/animals/reproductive-statuses` (já existente).

**Fonte única de derivação.** A lógica de prioridade (`Pregnant` > `Postpartum` > `AwaitingConfirmation` > `Open`) e o limiar de pós-parto (`PostpartumDaysThreshold = 60`) vivem em `ReproductiveStatusResolver` (`Application/Helpers`), usado **tanto** pelo detalhe (um animal) **quanto** pela listagem (conjunto), garantindo classificação idêntica nos dois caminhos.

**Derivação on-read (decisão de arquitetura).** O status **não é persistido**: o pós-parto expira pela passagem do tempo e o limiar é variável por propriedade — persistir geraria valor obsoleto. Considerando servidor limitado e internet ruim, a listagem usa duas consultas enxutas em vez de N+1:

1. `AnimalRepository.GetAllAnimalsAsync(filter)` — carrega os animais da página (aplica os filtros).
2. `AnimalRepository.GetReproductiveStatusMapAsync(femaleIds)` — **uma** consulta set-based, restrita por PK aos IDs das fêmeas já carregadas (seek, sem re-scan nem reaplicar filtros). É **pulada** quando a página não tem fêmeas.

O filtro por `ReproductiveStatus` é aplicado no serviço sobre o mapa resultante. Complementa a derivação de `Postpartum` da seção 5.4 do Spec 6.2.

---

### 5.6 Endpoints da API

> Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/pregnancies` | Listagem global de gestações da propriedade (filtros: `animalId`, `status`, `isActive`) | `200 AnimalPregnancyListItemDto[]` |
| `GET` | `/api/animals/{animalId}/pregnancies` | Histórico de gestações de um animal (filtro: `isActive`) | `200 AnimalPregnancyListItemDto[]` / `404` |
| `GET` | `/api/pregnancies/{id}` | Detalhe de uma gestação (com parto e crias) | `200 AnimalPregnancyDto` / `404` |
| `PATCH` | `/api/pregnancies/{id}/status` | Registrar perda gestacional | `200 AnimalPregnancyDto` / `404` / `409` / `422` |
| `DELETE` | `/api/pregnancies/{id}` | Inativar gestação | `204` / `404` / `409` |
| `GET` | `/api/pregnancies/statuses` | Enum de status de gestação | `200 [{value, label}]` |

> A **listagem de animais** (`GET /api/animals`) passa a aceitar o filtro `reproductiveStatus` e a expor `ReproductiveStatus` em cada item — ver seção 5.5.1.

---

### 5.7 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `AnimalPregnancy` é criada pelo sistema ao confirmar prenhez; o produtor não a cria diretamente. | `BreedingEventService.UpdateStatusAsync` → chama `IAnimalPregnancyService.CreateForBreedingEventAsync` |
| RN-02 | `ExpectedCalvingDate = BreedingDate + 280 dias`. | `AnimalPregnancyService.CreateForBreedingEventAsync` |
| RN-03 | `LostPregnancy` só pode ser aplicado quando `Status = Confirmed`. | Service → lança `ConflictException` |
| RN-04 | `LossDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-05 | Status `LostPregnancy` é imutável após definido — inativar é o caminho de correção. | Service (ausência de transição de volta) |
| RN-06 | Inativar `AnimalPregnancy` com `AnimalCalving` ativa é bloqueado. | Service → lança `ConflictException` |
| RN-07 | Ao inativar `BreedingEvent`, verificar se há `AnimalPregnancy` ativa vinculada. Se sim, lança `ConflictException`. | `BreedingEventService.InactivateAsync` → chama `IAnimalPregnancyRepository.ExistsActiveForBreedingEventAsync` |
| RN-08 | `ReproductiveStatus.Pregnant` ativo enquanto `AnimalPregnancy.Status = Confirmed AND IsActive = true`. | `AnimalService.GetByIdAsync` |
| RN-09 | Isolamento de tenant: repositório filtra por `PropertyId`. | Repository |

---

### 5.8 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalPregnancy.cs` | **Criar** |
| `Domain/Models` | `Animal.cs` | Adicionar navigation `ICollection<AnimalPregnancy>? Pregnancies` |
| `Domain/Models` | `BreedingEvent.cs` | Adicionar navigation `AnimalPregnancy? Pregnancy` |
| `Domain/Enums` | `AnimalPregnancyStatus.cs` | **Criar** |
| `Application/DTOs` | `AnimalPregnancyDto.cs` | **Criar** |
| `Application/DTOs` | `AnimalPregnancyListItemDto.cs` | **Criar** |
| `Application/DTOs` | `AnimalPregnancyStatusUpdateDto.cs` | **Criar** |
| `Application/Mappings` | `AnimalPregnancyProfile.cs` | **Criar** |
| `Application/Services` | `AnimalPregnancyService.cs` | **Criar** (inclui `CreateForBreedingEventAsync`) |
| `Application/Services` | `BreedingEventService.cs` | Adicionar chamada a `IAnimalPregnancyService.CreateForBreedingEventAsync` em `UpdateStatusAsync`; adicionar verificação `IAnimalPregnancyRepository.ExistsActiveForBreedingEventAsync` em `InactivateAsync` |
| `Application/Services` | `AnimalService.cs` | Adicionar estado `Pregnant`; usar `ReproductiveStatusResolver`; expor e filtrar `ReproductiveStatus` na listagem |
| `Application/Helpers` | `ReproductiveStatusResolver.cs` | **Criar** — fonte única da derivação (prioridade + limiar de 60 dias) |
| `Application/DTOs` | `AnimalFilterDto.cs` | Adicionar filtro `ReproductiveStatus?` |
| `Application/DTOs` | `AnimalListItemDto.cs` | Adicionar `ReproductiveStatus` (`EnumValueDto?`) |
| `Application/DTOs` | `AnimalPregnancyFilterDto.cs` | **Criar** — filtros `AnimalId`/`Status`/`IsActive` da listagem global |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | Adicionar `GetReproductiveStatusMapAsync` (query set-based por PK) |
| `Application/Interfaces` | `IAnimalPregnancyService.cs` | **Criar** |
| `Application/Interfaces` | `IAnimalPregnancyRepository.cs` | **Criar** (inclui `ExistsActiveForBreedingEventAsync`) |
| `Infrastructure/Repositories` | `AnimalPregnancyRepository.cs` | **Criar** |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Adicionar `DbSet<AnimalPregnancy>`; configurar FK única em `BreedingEventId` e demais índices em `OnModelCreating` |
| `Api/Controllers` | `PregnanciesController.cs` | **Criar** (inclui `GET /api/pregnancies` — listagem global com filtros) |
| `Infrastructure/Migrations` | *(ver seção 6)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

**Criar tabela `AnimalPregnancies`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `AnimalId` | int | not null, FK → `Animals`, `ON DELETE RESTRICT` |
| `BreedingEventId` | int | not null, FK → `BreedingEvents`, `ON DELETE RESTRICT` |
| `ConfirmationDate` | datetime2 | not null |
| `ExpectedCalvingDate` | datetime2 | not null |
| `LossDate` | datetime2 | nullable |
| `Status` | int | not null |
| `Notes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Índices:**

| Colunas | Tipo | Motivo |
|---------|------|--------|
| `BreedingEventId` | **Único** | Garante máximo uma gestação por evento reprodutivo. |
| `(AnimalId, Status, IsActive)` | Composto | Otimiza derivação de `ReproductiveStatus` no `AnimalService`. |
| `(PropertyId, IsActive)` | Composto | Isolamento de tenant. |

---

## 7. Fora do Escopo deste Spec

- **Registro de parto** → Spec 6.2
- **Dados das crias** → Spec 6.3
- **Cadastro de animais** → Spec #1
- **Eventos Reprodutivos** → Spec #5
- **Dashboards de índices zootécnicos** → Spec #7
- **Estado `Postpartum` do `ReproductiveStatus`** → Spec 6.2
