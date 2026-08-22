# Spec: Eventos Reprodutivos

**Módulo:** Eventos Reprodutivos  
**Versão:** 1.0  
**Data:** 20/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais; `spec_modulo_reproducao.md`  
**Status:** Aprovado para implementação  
**Depende de:** Spec #1 (Animal), Spec #4 (Banco de Sêmen)  
**Referenciado por:** Spec #6 (Gestação e Parto), Spec #7 (Dashboards)

---

## 1. Contexto e Objetivo

O módulo de Eventos Reprodutivos registra cada cobertura realizada na propriedade — por inseminação artificial (IA) ou monta natural. Cada evento nasce com status **Pendente** e evolui para **Prenha** ou **Não Prenha** após o diagnóstico do produtor. Ao confirmar prenhez, o sistema cria automaticamente o registro de gestação (Spec #6).

O **status reprodutivo** do animal (Vazia / Aguardando Confirmação / Prenha / Pós-Parto) é derivado on-the-fly a partir dos eventos registrados — sem tabela espelho separada — e exposto no detalhe do animal (`AnimalDto`).

**Fora do escopo deste spec:**
- Gestação, parto e status Pós-Parto → Spec #6
- Secagem (dry-off) e status de lactação → abstraído (fora do TCC)
- Inseminação em lote (múltiplos animais de uma vez)
- IATF e Transferência de Embrião

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Apenas IA e Monta Natural no enum `ReproductionType` | Exatamente o que a ata especifica. IATF e TE são raros em pequenos produtores e podem ser adicionados depois. |
| D2 | Touro pai (monta natural) deve ser um animal cadastrado no sistema | Garante rastreabilidade genealógica. O produtor cadastra o touro antes de registrar a monta. |
| D4 | Status reprodutivo calculado on-the-fly — sem tabela espelho | Simplicidade para o TCC. `AnimalReproStatus` como tabela separada pode ser introduzido depois se o dashboard exigir performance. |
| D5 | `BreedingEvent` não implementa `ITenantEntity` diretamente | Isolamento de tenant garantido via join com `Animal.PropertyId`, igual ao padrão do `BodyConditionRecord` (Spec #3). |
| D6 | Evento é imutável após criação — só o status pode ser atualizado | Mantém a trilha de auditoria. Para corrigir um evento errado, o produtor inativa e cria outro. |
| D7 | Soft delete bloqueado se evento referenciado por gestação (Spec #6) | Preserva a integridade: a gestação deve saber de qual cobertura originou. |
| D8 | `ServiceNumber` calculado automaticamente pelo serviço | Conta coberturas anteriores daquele animal e incrementa. Necessário para o cálculo de "% prenhez ao 1º serviço" no Spec #7 sem contagem retroativa. |
| D9 | A criação da gestação ao confirmar prenhez é efeito colateral definido no Spec #6 | O PATCH de status do Spec #5 atualiza o `BreedingEvent`. A lógica de criação do `AnimalPregnancy` é adicionada ao mesmo método de serviço na implementação do Spec #6. |
| D10 | `ReproductiveStatus` adicionado ao `AnimalDto` como campo computado | Status calculado no `AnimalService.GetByIdAsync`. Para Spec #5 cobre Vazia e AguardandoConfirmação; Prenha e PósParto completados no Spec #6. |
| D11 | "Seca" da ata mapeada para `Open` (Vazia) | A ata usa "Seca" onde o domínio reprodutivo usa "Vazia". "Seca" é status de lactação (abstraído); "Vazia" é a fêmea sem cobertura ativa ou gestação confirmada. |
| D12 | Pós-Parto limitado a 60 dias fixos; futuramente configurável por propriedade | Sem limite de tempo, um animal nunca inseminado após o parto ficaria preso em Pós-Parto indefinidamente. O PEV (Período de Espera Voluntário) padrão para bovinos leiteiros é 60 dias. A constante `PostpartumDaysThreshold = 60` fica em `BreedingEventService` para facilitar a extração futura para um campo de configuração da propriedade (`Property.PostpartumDaysThreshold`). |

---

## 3. Histórias de Usuário

### US-01 — Registrar evento reprodutivo
> **Como** produtor,  
> **quero** registrar uma cobertura (IA ou monta natural) para uma fêmea,  
> **para** acompanhar o histórico reprodutivo do animal.

**Critérios de aceite:**
- O produtor informa: tipo de reprodução, data, e o sêmen (IA) ou o touro pai (monta natural).
- A data não pode ser futura.
- Para IA: `SemenSampleId` obrigatório; `SireAnimalId` proibido.
- Para monta natural: `SireAnimalId` obrigatório; `SemenSampleId` proibido.
- O touro informado deve ter `Classification = Bull`.
- O sêmen informado deve estar ativo.
- O animal submetido deve estar ativo.
- O evento nasce com `Status = Pending`.
- `ServiceNumber` é calculado automaticamente (número da cobertura para aquele animal).

---

### US-02 — Atualizar status do evento (diagnóstico)
> **Como** produtor,  
> **quero** informar o resultado do diagnóstico de prenhez,  
> **para** registrar se a fêmea ficou prenha ou não.

**Critérios de aceite:**
- O produtor informa o novo status (`Pregnant` ou `NotPregnant`) e a data do diagnóstico.
- Apenas eventos com `Status = Pending` podem ser atualizados.
- A data de diagnóstico não pode ser futura.
- Ao confirmar `Pregnant`, o sistema criará um registro de gestação (Spec #6).
- Ao confirmar `NotPregnant`, o animal retorna ao status `Open` (Vazia).

---

### US-03 — Listar eventos reprodutivos de um animal
> **Como** produtor,  
> **quero** ver o histórico de coberturas de uma fêmea,  
> **para** acompanhar sua vida reprodutiva.

**Critérios de aceite:**
- Lista retorna todos os eventos do animal, ordenados por data decrescente.
- Exibe: data, tipo de reprodução, sêmen ou touro pai, status, número do serviço.

---

### US-04 — Listar todos os eventos da propriedade (com filtros)
> **Como** produtor,  
> **quero** consultar os eventos reprodutivos da propriedade com filtros,  
> **para** analisar o desempenho reprodutivo do rebanho (base para Spec #7).

**Critérios de aceite:**
- Filtros disponíveis: animal, tipo de reprodução, status, período da cobertura, status ativo/inativo.
- Retorna somente eventos da propriedade do usuário autenticado.

---

### US-05 — Visualizar status reprodutivo de um animal
> **Como** produtor,  
> **quero** ver o status reprodutivo atual de uma fêmea no perfil do animal,  
> **para** saber se ela está vazia, aguardando diagnóstico ou prenha.

**Critérios de aceite:**
- O status é exibido no detalhe do animal (`GET /api/animals/{id}`).
- Status aplicável apenas a animais com `Classification = Cow` ou `Heifer`; para outros, retorna `null`.
- Os 4 valores possíveis: Vazia, Aguardando Confirmação, Prenha, Pós-Parto.
- Pós-Parto e Prenha tornam-se funcionais após implementação do Spec #6.

---

### US-06 — Inativar evento reprodutivo
> **Como** produtor,  
> **quero** inativar um evento cadastrado incorretamente,  
> **para** corrigi-lo sem perder o histórico.

**Critérios de aceite:**
- Inativação é soft delete (`IsActive = false`).
- Bloqueada se o evento já possuir uma gestação vinculada (Spec #6) → `409 Conflict`.

---

## 4. Casos de Uso

### CU-01 — Registrar Evento Reprodutivo

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe, está ativo e pertence à propriedade. Sêmen ou touro informado conforme tipo de reprodução.

**Fluxo principal:**
1. Produtor envia `POST /api/animals/{animalId}/breeding-events` com `BreedingEventCreateDto`.
2. Sistema carrega o animal e valida que pertence ao tenant.
3. Sistema valida que o animal está ativo — se não, lança `ConflictException`.
4. Validações de DTO (tipo vs. sêmen/touro, data não futura).
5. Se `ReproductionType = ArtificialInsemination`: verifica que `SemenSample` existe e está ativo — se não, lança `NotFoundException` ou `ConflictException`.
6. Se `ReproductionType = NaturalMating`: verifica que o `SireAnimal` existe, está ativo e tem `Classification = Bull` — se não, lança `BusinessRuleException`.
7. Calcula `ServiceNumber` = (count de eventos anteriores do animal) + 1.
8. Cria `BreedingEvent` com `Status = Pending`.
9. Retorna `201 Created` com `BreedingEventDto`.

**Fluxo alternativo — animal não encontrado:** Passo 2 → `NotFoundException` → `404`.  
**Fluxo alternativo — animal inativo:** Passo 3 → `ConflictException` → `409`.  
**Fluxo alternativo — sêmen não encontrado/inativo:** Passo 5 → `NotFoundException`/`ConflictException` → `404`/`409`.  
**Fluxo alternativo — touro inválido (não Bull):** Passo 6 → `BusinessRuleException` → `422`.

---

### CU-02 — Atualizar Status do Evento (Diagnóstico)

**Ator:** Produtor autenticado  
**Pré-condição:** Evento existe, pertence à propriedade e está com `Status = Pending`.

**Fluxo principal:**
1. Produtor envia `PATCH /api/breeding-events/{id}/status` com `BreedingEventStatusUpdateDto`.
2. Sistema carrega o evento e valida o tenant via `Animal.PropertyId`.
3. Sistema valida que o evento está com `Status = Pending` — se não, lança `ConflictException`.
4. Sistema atualiza `Status` e `DiagnosisDate`.
5. Se `Status = Pregnant`: criação de `AnimalPregnancy` será implementada no Spec #6 — neste spec, o serviço apenas atualiza o evento.
6. Retorna `200 OK` com `BreedingEventDto` atualizado.

**Fluxo alternativo — evento não encontrado:** `NotFoundException` → `404`.  
**Fluxo alternativo — status já definido:** Passo 3 → `ConflictException` → `409`.

---

### CU-03 — Listar Eventos de um Animal

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/animals/{animalId}/breeding-events`.
2. Sistema valida que o animal pertence ao tenant.
3. Retorna lista de `BreedingEventListItemDto` ordenada por `BreedingDate` decrescente.

---

### CU-04 — Listar Eventos da Propriedade (com filtros)

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/breeding-events` com `BreedingEventFilterDto` como query params.
2. Repositório filtra por `Animal.PropertyId` do tenant.
3. Aplica filtros adicionais.
4. Retorna lista de `BreedingEventListItemDto`.

**Filtros disponíveis:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `animalId` | int | Filtro por animal |
| `reproductionType` | int | Valor do enum `ReproductionType` |
| `status` | int | Valor do enum `BreedingStatus` |
| `breedingDateFrom` | DateTime | Início do período da cobertura |
| `breedingDateTo` | DateTime | Fim do período da cobertura |
| `isActive` | bool? | `true` = só ativos, `false` = só inativos, `null` = todos |

---

### CU-05 — Inativar Evento Reprodutivo

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/breeding-events/{id}`.
2. Sistema carrega o evento e valida o tenant.
3. Sistema verifica que não há gestação vinculada a este evento (Spec #6 — verificação implementada junto com Spec #6).
4. Sistema seta `IsActive = false`.
5. Retorna `204 No Content`.

**Fluxo alternativo — evento não encontrado:** `NotFoundException` → `404`.  
**Fluxo alternativo — gestação vinculada:** Passo 3 → `ConflictException` → `409`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Nova Entidade `BreedingEvent`

> Localização: `Domain/Models/BreedingEvent.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalId` | `int` | Sim | FK → `Animal` (fêmea submetida). |
| `ReproductionType` | `ReproductionType` (enum) | Sim | IA ou Monta Natural. |
| `BreedingDate` | `DateTime` | Sim | Data da cobertura. Não pode ser futura. |
| `SemenSampleId` | `int?` | Condicional | Obrigatório quando IA. FK → `SemenSample`. |
| `SireAnimalId` | `int?` | Condicional | Obrigatório quando Monta Natural. FK → `Animal` (touro pai). |
| `Status` | `BreedingStatus` (enum) | Sim | Inicia como `Pending`. |
| `DiagnosisDate` | `DateTime?` | Não | Preenchido ao atualizar o status. |
| `ServiceNumber` | `int` | Sim | Auto-calculado pelo serviço (nº da cobertura para esse animal). |
| `Notes` | `string?` (max 500) | Não | Observações livres. |
| `Animal` | navigation | — | Fêmea submetida. |
| `SemenSample` | navigation | — | Sêmen usado (nullable). |
| `SireAnimal` | navigation | — | Touro pai (nullable). |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. |

```csharp
public class BreedingEvent : BaseEntity
{
    public int AnimalId { get; set; }

    public ReproductionType ReproductionType { get; set; }

    public DateTime BreedingDate { get; set; }

    public int? SemenSampleId { get; set; }

    public int? SireAnimalId { get; set; }

    public BreedingStatus Status { get; set; } = BreedingStatus.Pending;

    public DateTime? DiagnosisDate { get; set; }

    public int ServiceNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Animal? Animal { get; set; }
    public SemenSample? SemenSample { get; set; }
    public Animal? SireAnimal { get; set; }
}
```

> **Nota de configuração EF Core:** Como `BreedingEvent` tem duas FKs para `Animals` (`AnimalId` e `SireAnimalId`), é necessário configurar explicitamente o `OnModelCreating` para evitar ambiguidade de convenção:
> ```csharp
> builder.Entity<BreedingEvent>()
>     .HasOne(e => e.Animal)
>     .WithMany(a => a.BreedingEvents)
>     .HasForeignKey(e => e.AnimalId)
>     .OnDelete(DeleteBehavior.Restrict);
>
> builder.Entity<BreedingEvent>()
>     .HasOne(e => e.SireAnimal)
>     .WithMany()
>     .HasForeignKey(e => e.SireAnimalId)
>     .OnDelete(DeleteBehavior.Restrict);
> ```

---

#### Adições à entidade `Animal`

Adicionar em `Animal.cs`:

```csharp
public ICollection<BreedingEvent>? BreedingEvents { get; set; }
```

---

### 5.2 Novos Enums

> Localização: `Domain/Enums/`

#### `ReproductionType.cs`
```csharp
public enum ReproductionType
{
    [Description("Inseminação Artificial")]
    ArtificialInsemination = 1,

    [Description("Monta Natural")]
    NaturalMating = 2
}
```

#### `BreedingStatus.cs`
```csharp
public enum BreedingStatus
{
    [Description("Pendente")]
    Pending = 1,

    [Description("Prenha")]
    Pregnant = 2,

    [Description("Não Prenha")]
    NotPregnant = 3
}
```

#### `ReproductiveStatus.cs`
```csharp
public enum ReproductiveStatus
{
    [Description("Vazia")]
    Open = 1,

    [Description("Aguardando Confirmação de Prenhez")]
    AwaitingConfirmation = 2,

    [Description("Prenha")]
    Pregnant = 3,

    [Description("Pós-Parto")]
    Postpartum = 4
}
```

> **Nota:** `ReproductiveStatus` é um enum de apresentação — não é persistido em `Animal` nem em `BreedingEvent`. É calculado on-the-fly pelo `AnimalService` com base nos eventos mais recentes do animal.

---

### 5.3 DTOs

#### `BreedingEventCreateDto.cs`

```csharp
public class BreedingEventCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "O tipo de reprodução é obrigatório.")]
    public ReproductionType ReproductionType { get; set; }

    [Required(ErrorMessage = "A data da cobertura é obrigatória.")]
    public DateTime BreedingDate { get; set; }

    public int? SemenSampleId { get; set; }

    public int? SireAnimalId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BreedingDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data da cobertura não pode ser futura.",
                new[] { nameof(BreedingDate) });

        if (ReproductionType == ReproductionType.ArtificialInsemination)
        {
            if (!SemenSampleId.HasValue)
                yield return new ValidationResult(
                    "O sêmen é obrigatório para inseminação artificial.",
                    new[] { nameof(SemenSampleId) });

            if (SireAnimalId.HasValue)
                yield return new ValidationResult(
                    "O touro pai não deve ser informado para inseminação artificial.",
                    new[] { nameof(SireAnimalId) });
        }

        if (ReproductionType == ReproductionType.NaturalMating)
        {
            if (!SireAnimalId.HasValue)
                yield return new ValidationResult(
                    "O touro pai é obrigatório para monta natural.",
                    new[] { nameof(SireAnimalId) });

            if (SemenSampleId.HasValue)
                yield return new ValidationResult(
                    "O sêmen não deve ser informado para monta natural.",
                    new[] { nameof(SemenSampleId) });
        }
    }
}
```

---

#### `BreedingEventStatusUpdateDto.cs`

```csharp
public class BreedingEventStatusUpdateDto : IValidatableObject
{
    [Required(ErrorMessage = "O status é obrigatório.")]
    public BreedingStatus Status { get; set; }

    [Required(ErrorMessage = "A data do diagnóstico é obrigatória.")]
    public DateTime DiagnosisDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status == BreedingStatus.Pending)
            yield return new ValidationResult(
                "O status não pode ser alterado para Pendente.",
                new[] { nameof(Status) });

        if (DiagnosisDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data do diagnóstico não pode ser futura.",
                new[] { nameof(DiagnosisDate) });
    }
}
```

---

#### `BreedingEventDto.cs` (response — detalhe)

```csharp
public class BreedingEventDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string AnimalTagNumber { get; set; } = string.Empty;
    public ReproductionType ReproductionType { get; set; }
    public string ReproductionTypeLabel { get; set; } = string.Empty;
    public DateTime BreedingDate { get; set; }
    public int? SemenSampleId { get; set; }
    public string? SemenSampleName { get; set; }
    public int? SireAnimalId { get; set; }
    public string? SireAnimalTagNumber { get; set; }
    public string? SireAnimalName { get; set; }
    public BreedingStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? DiagnosisDate { get; set; }
    public int ServiceNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

#### `BreedingEventListItemDto.cs` (response — listagem)

```csharp
public class BreedingEventListItemDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string AnimalTagNumber { get; set; } = string.Empty;
    public ReproductionType ReproductionType { get; set; }
    public string ReproductionTypeLabel { get; set; } = string.Empty;
    public DateTime BreedingDate { get; set; }
    public BreedingStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public int ServiceNumber { get; set; }
}
```

---

#### `BreedingEventFilterDto.cs` (query params)

```csharp
public class BreedingEventFilterDto
{
    public int? AnimalId { get; set; }
    public ReproductionType? ReproductionType { get; set; }
    public BreedingStatus? Status { get; set; }
    public DateTime? BreedingDateFrom { get; set; }
    public DateTime? BreedingDateTo { get; set; }
    public bool? IsActive { get; set; }
}
```

---

#### Atualização de `AnimalDto.cs`

Adicionar ao `AnimalDto` existente (Spec #1 + Spec #3):

```csharp
public ReproductiveStatus? ReproductiveStatus { get; set; }
public string? ReproductiveStatusLabel { get; set; }
```

> **Nota:** `ReproductiveStatus` é `null` para animais com `Classification` diferente de `Cow` ou `Heifer`. Calculado no `AnimalService.GetByIdAsync`.

---

### 5.4 Lógica de Derivação do Status Reprodutivo (on-the-fly)

Chamada no `AnimalService` ao montar o `AnimalDto`. Aplicável apenas a animais com `Classification = Cow` ou `Heifer`.

```
1. Se há gestação ativa (AnimalPregnancy com Status = Confirmed, implementado no Spec #6)
   → ReproductiveStatus.Pregnant

2. Senão, se há BreedingEvent ativo com Status = Pending
   → ReproductiveStatus.AwaitingConfirmation

3. Senão, se há parto registrado há menos de 60 dias E sem cobertura ativa após ele (Spec #6)
   → ReproductiveStatus.Postpartum

4. Caso contrário
   → ReproductiveStatus.Open
```

> **PEV (Período de Espera Voluntário):** o limite de 60 dias do passo 3 é definido pela constante `PostpartumDaysThreshold = 60` em `BreedingEventService`. Após esse prazo sem nova cobertura, o animal retorna a `Open`. Futuramente, esse valor será movido para `Property.PostpartumDaysThreshold` (configurável por propriedade).

> Para a implementação do Spec #5 (antes do Spec #6): apenas os estados `AwaitingConfirmation` e `Open` são funcionais. Os demais são retornados corretamente após a implementação do Spec #6.

---

### 5.5 Endpoints da API

> Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/animals/{animalId}/breeding-events` | Registrar evento reprodutivo | `201 BreedingEventDto` / `404` / `409` / `422` |
| `GET` | `/api/animals/{animalId}/breeding-events` | Histórico de eventos de um animal | `200 BreedingEventListItemDto[]` / `404` |
| `GET` | `/api/breeding-events` | Listar todos com filtros (base para Spec #7) | `200 BreedingEventListItemDto[]` |
| `GET` | `/api/breeding-events/{id}` | Detalhe de um evento | `200 BreedingEventDto` / `404` |
| `PATCH` | `/api/breeding-events/{id}/status` | Atualizar resultado do diagnóstico | `200 BreedingEventDto` / `404` / `409` / `422` |
| `DELETE` | `/api/breeding-events/{id}` | Inativar evento (soft delete) | `204` / `404` / `409` |
| `GET` | `/api/breeding-events/reproduction-types` | Enum de tipos de reprodução | `200 [{value, label}]` |
| `GET` | `/api/breeding-events/statuses` | Enum de status de cobertura | `200 [{value, label}]` |
| `GET` | `/api/animals/reproductive-statuses` | Enum de status reprodutivo | `200 [{value, label}]` |

---

### 5.6 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `ReproductionType` e `BreedingDate` obrigatórios. | DTO (DataAnnotations) |
| RN-02 | `BreedingDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-03 | IA exige `SemenSampleId`; proíbe `SireAnimalId`. | DTO (IValidatableObject) |
| RN-04 | Monta Natural exige `SireAnimalId`; proíbe `SemenSampleId`. | DTO (IValidatableObject) |
| RN-05 | Animal submetido deve estar ativo. | Service → lança `ConflictException` |
| RN-06 | `SemenSample` referenciado deve estar ativo. | Service → lança `ConflictException` |
| RN-07 | `SireAnimal` deve ter `Classification = Bull`. | Service → lança `BusinessRuleException` |
| RN-08 | `SireAnimal` deve estar ativo. | Service → lança `ConflictException` |
| RN-09 | `ServiceNumber` calculado automaticamente (count eventos ativos do animal + 1). | Service |
| RN-11 | Atualização de status permitida apenas quando `Status = Pending`. | Service → lança `ConflictException` |
| RN-12 | `DiagnosisDate` não pode ser futura. | DTO (IValidatableObject) |
| RN-13 | Status não pode regredir para `Pending` via PATCH. | DTO (IValidatableObject) |
| RN-14 | Inativação bloqueada se existir gestação vinculada (Spec #6). | Service → lança `ConflictException` |
| RN-15 | `ReproductiveStatus` calculado apenas para animais `Cow` ou `Heifer`; retorna `null` para demais. | Service |
| RN-16 | Isolamento de tenant via `Animal.PropertyId` em todas as queries. | Repository |

---

### 5.7 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `BreedingEvent.cs` | Criar |
| `Domain/Models` | `Animal.cs` | Adicionar navigation `ICollection<BreedingEvent>? BreedingEvents` |
| `Domain/Enums` | `ReproductionType.cs` | Criar |
| `Domain/Enums` | `BreedingStatus.cs` | Criar |
| `Domain/Enums` | `ReproductiveStatus.cs` | Criar |
| `Application/DTOs` | `BreedingEventCreateDto.cs` | Criar |
| `Application/DTOs` | `BreedingEventStatusUpdateDto.cs` | Criar |
| `Application/DTOs` | `BreedingEventDto.cs` | Criar |
| `Application/DTOs` | `BreedingEventListItemDto.cs` | Criar |
| `Application/DTOs` | `BreedingEventFilterDto.cs` | Criar |
| `Application/DTOs` | `AnimalDto.cs` | Adicionar `ReproductiveStatus` e `ReproductiveStatusLabel` |
| `Application/Mappings` | `BreedingEventProfile.cs` | Criar |
| `Application/Services` | `BreedingEventService.cs` | Criar |
| `Application/Services` | `AnimalService.cs` | Adicionar lógica de derivação de `ReproductiveStatus` no `GetByIdAsync` |
| `Application/Interfaces` | `IBreedingEventService.cs` | Criar |
| `Application/Interfaces` | `IBreedingEventRepository.cs` | Criar |
| `Infrastructure/Repositories` | `BreedingEventRepository.cs` | Criar |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Adicionar `DbSet<BreedingEvent>` e configurar FKs duplicadas em `OnModelCreating` |
| `Api/Controllers` | `BreedingEventsController.cs` | Criar (rotas planas e aninhadas em `/animals/{animalId}/`) |
| `Api/Controllers` | `AnimalsController.cs` | Adicionar rota `GET /api/animals/reproductive-statuses` |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Criar tabela `BreedingEvents` com colunas: `Id` (int, PK), `AnimalId` (int, FK → Animals, not null), `ReproductionType` (int, not null), `BreedingDate` (datetime2, not null), `SemenSampleId` (int, nullable, FK → SemenSamples), `SireAnimalId` (int, nullable, FK → Animals), `Status` (int, not null, default 1), `DiagnosisDate` (datetime2, nullable), `ServiceNumber` (int, not null), `Notes` (nvarchar(500), nullable), `IsActive` (bit, not null, default 1), `CreatedAt` (datetime2, not null), `UpdatedAt` (datetime2, nullable).
2. Ambas as FKs para `Animals` com `ON DELETE RESTRICT` (sem cascade) para evitar conflito.
3. Criar índice em `(AnimalId, BreedingDate DESC)` — otimiza histórico por animal.
4. Criar índice em `(AnimalId, Status, IsActive)` — otimiza derivação do status reprodutivo on-the-fly.
5. Criar índice em `(SemenSampleId)` — otimiza verificação de referências (RN-14 do Spec #4).

---

## 7. Fora do Escopo deste Spec

- **Gestação e Parto** (entidade `AnimalPregnancy`, `AnimalCalving`, status `Pregnant` e `Postpartum` funcionais) → Spec #6
- **Secagem e status de lactação** (`AnimalDryOff`, `LactationStatus`) → abstraído / fora do TCC
- **Inseminação em lote** → fora do escopo
- **IATF e Transferência de Embrião** → fora do escopo
- **Dashboards reprodutivos** (taxa de prenhez, taxa de concepção, % prenhez ao 1º serviço) → Spec #7
- **Cadastro de Animal** → Spec #1
- **Banco de Sêmen** → Spec #4
