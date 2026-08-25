# Spec: Ajustes no Banco de Sêmen — Simplificação e Controle de Doses

**Módulo:** Banco de Sêmen (Revisão)
**Versão:** 3.0
**Data:** 23/Ago/2026
**Fonte:** Ajuste pós-desenvolvimento — revisão interna
**Status:** Aprovado para implementação
**Revisa:** Spec #4 (Banco de Sêmen)
**Impacta:** Spec #5 (Eventos Reprodutivos)

## Changelog

| Versão | Alteração |
|--------|-----------|
| 1.0 | Versão inicial — remoção de campos de data e nova tabela `SemenSampleEntry` |
| 2.0 | `SemenSampleEntry` substituído por `SemenSampleMovement` unificado (entradas e saídas); inseminação gera `Output` automaticamente |
| 3.0 | `BatchNumber` e `BatchDate` movidos de `SemenSampleMovement` para `SemenSample`; histórico de movimentações mantém apenas data de compra, quantidade e tipo |
| 3.1 | `InitialQuantity` e `InitialNotes` adicionados ao `SemenSampleCreateDto`; ao registrar um sêmen com quantidade inicial, o sistema cria automaticamente o primeiro movimento `Input` |
| 3.2 | Campo `BullName` removido de `SemenSample` — desnecessário na prática |

---

## 1. Contexto e Objetivo

Este spec revisa o módulo Banco de Sêmen (Spec #4) com dois objetivos:

**1. Simplificação do cadastro:**
O campo `Name` deixa de ser descrito como "apelido interno" e passa a ser simplesmente "nome", livre para o produtor usar como quiser (nome do touro, nome comercial do produto, código interno etc.). Os campos de datas (`CollectedAt`, `ManufacturedAt`, `ReceivedAt`, `ExpiresAt`) são removidos por serem desnecessários na prática. Número de partida (`BatchNumber`) e data de partida (`BatchDate`) passam a compor o cadastro do sêmen, não o histórico de movimentações.

**2. Controle de estoque de doses por movimentação:**
Introduz a entidade `SemenSampleMovement` para registrar toda entrada e saída de palhetas — compras (entrada manual), descartes/perdas (saída manual) e inseminações artificiais (saída automática gerada pelo sistema ao registrar um `BreedingEvent`). O histórico de movimentação é intencionalmente simples: registra apenas data, tipo e quantidade. O saldo disponível resulta de uma única fórmula:

```
AvailableDoses = SUM(Quantity | MovementType = Input, IsActive = true)
               - SUM(Quantity | MovementType = Output, IsActive = true)
```

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Campo `Name` de `SemenSample` mantido sem alteração de código — apenas a descrição semântica muda de "apelido interno" para "nome livre" | O campo já existe na entidade; a mudança é conceitual para torná-lo mais genérico. |
| D2 | Remover `CollectedAt`, `ManufacturedAt`, `ReceivedAt` e `ExpiresAt` de `SemenSample` | Esses campos foram definidos no Spec #4 mas avaliados como desnecessários após o desenvolvimento inicial. |
| D3 | Tabela única `SemenSampleMovement` para entradas e saídas, com campo `MovementType` | Todas as movimentações (compra, descarte, inseminação) são efeitos do mesmo fenômeno — alteração de estoque. Uma tabela unificada resulta em fórmula de saldo simples e fonte única de verdade. |
| D4 | Inseminação artificial gera automaticamente um `SemenSampleMovement` do tipo `Output` | A dose é fisicamente consumida na inseminação. Registrar como movimento mantém o saldo consistente sem contar duas fontes distintas. |
| D5 | Inativar `BreedingEvent` inativa automaticamente o movimento `Output` vinculado | Se o evento é cancelado, o consumo da dose também é cancelado — a dose retorna ao saldo disponível. |
| D6 | Movimentos gerados pelo sistema (`BreedingEventId != null`) **não podem ser editados nem inativados diretamente** | O ciclo de vida desses movimentos é controlado pelo `BreedingEvent`. Lança `ConflictException` se tentado. |
| D7 | `Quantity` é sempre positivo; a direção do estoque é determinada pelo `MovementType` | Quantidades negativas são confusas. `MovementType = Output` com `Quantity = 2` é mais legível que `Quantity = -2`. |
| D8 | Saldo negativo é permitido — não bloqueia inseminações | O produtor pode aplicar sêmen sem ter registrado todas as entradas. Bloquear causaria fricção desnecessária. |
| D9 | `BatchNumber` e `BatchDate` pertencem ao cadastro de `SemenSample`, não ao histórico de movimentações | Se colocados no histórico, o produtor precisaria escolher entre múltiplos registros de movimentação para identificar o lote, o que dificulta o rastreamento quando há muitas compras de datas e lotes diferentes para o mesmo catálogo. Com os campos no cadastro, cada `SemenSample` representa um lote específico. |
| D10 | O histórico de movimentações registra apenas data, tipo e quantidade | Simplicidade intencional: a procedência do lote está no cadastro do sêmen; o histórico só precisa responder "quando e quanto entrou ou saiu". |
| D11 | `SemenSampleMovement` implementa `ITenantEntity` (PropertyId) | Consistência com o padrão do projeto; isolamento de tenant em todas as entidades com escopo de propriedade. |
| D12 | Soft delete em `SemenSampleMovement` | Movimentos inativados não entram no cálculo do saldo. Permite desfazer registros incorretos sem perder o histórico. |
| D13 | `InitialQuantity` e `InitialNotes` em `SemenSampleCreateDto` permitem criar o primeiro movimento `Input` junto com o cadastro | Evita que o produtor precise fazer dois POSTs separados quando o sêmen já existe em estoque no momento do registro. O movimento gerado é tratado como manual (`BreedingEventId = null`) e pode ser editado ou inativado normalmente. `MovementDate` é definido como `DateTime.UtcNow`. |

---

## 3. Histórias de Usuário

### US-01 — Registrar entrada de doses (compra)
> **Como** produtor,
> **quero** registrar uma compra de palhetas de sêmen,
> **para** controlar o estoque disponível de cada tipo de sêmen.

**Critérios de aceite:**
- O produtor informa: data da compra e quantidade de palhetas.
- A movimentação é do tipo `Input`.
- A quantidade deve ser maior que zero.
- O saldo disponível é atualizado automaticamente.

---

### US-02 — Registrar saída manual de doses (descarte ou perda)
> **Como** produtor,
> **quero** registrar a saída de doses por descarte ou perda,
> **para** manter o saldo de palhetas preciso sem depender apenas das inseminações cadastradas.

**Critérios de aceite:**
- O produtor informa: data e quantidade. Observações são opcionais.
- A movimentação é do tipo `Output`.
- A quantidade deve ser maior que zero.
- O saldo disponível é atualizado automaticamente.

---

### US-03 — Consultar saldo de doses disponíveis
> **Como** produtor,
> **quero** ver quantas palhetas estão disponíveis para cada sêmen,
> **para** saber se tenho material suficiente antes de programar inseminações.

**Critérios de aceite:**
- O saldo é exibido no detalhe e na listagem de cada amostra de sêmen.
- Saldo = soma das entradas ativas − soma das saídas ativas (inclui inseminações).
- Saldo negativo é permitido.

---

### US-04 — Consultar histórico de movimentações
> **Como** produtor,
> **quero** ver o histórico de entradas e saídas de um tipo de sêmen,
> **para** auditar o consumo de doses.

**Critérios de aceite:**
- A lista retorna todas as movimentações ativas, ordenadas por data decrescente.
- Exibe: data, tipo (entrada/saída), quantidade e origem (manual ou inseminação).

---

### US-05 — Editar movimentação manual (correção)
> **Como** produtor,
> **quero** corrigir os dados de uma movimentação cadastrada incorretamente,
> **para** manter o histórico preciso.

**Critérios de aceite:**
- Apenas movimentações manuais (`BreedingEventId = null`) podem ser editadas.
- Todos os campos são opcionais no PATCH.
- A quantidade, se informada, deve ser maior que zero.

---

### US-06 — Inativar movimentação manual (exclusão lógica)
> **Como** produtor,
> **quero** inativar uma movimentação registrada por engano,
> **para** corrigir o saldo sem apagar o histórico.

**Critérios de aceite:**
- Apenas movimentações manuais (`BreedingEventId = null`) podem ser inativadas diretamente.
- A inativação seta `IsActive = false` — o movimento não entra mais no cálculo do saldo.

---

## 4. Casos de Uso

### CU-01 — Registrar Movimentação Manual

**Ator:** Produtor autenticado
**Pré-condição:** Registro de sêmen existe e pertence à propriedade do usuário.

**Fluxo principal:**
1. Produtor envia `POST /api/semen-samples/{semenSampleId}/movements` com `SemenSampleMovementCreateDto`.
2. Sistema carrega o `SemenSample` e valida que pertence ao tenant.
3. Sistema valida `Quantity > 0`.
4. Sistema cria `SemenSampleMovement` com `IsActive = true` e `BreedingEventId = null`.
5. Retorna `201 Created` com `SemenSampleMovementDto`.

**Fluxo alternativo — sêmen não encontrado:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

---

### CU-02 — Listar Histórico de Movimentações

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/semen-samples/{semenSampleId}/movements`.
2. Sistema carrega o `SemenSample` e valida que pertence ao tenant.
3. Repositório retorna movimentações ativas, ordenadas por `MovementDate` decrescente.
4. Retorna `200 OK` com `SemenSampleMovementListItemDto[]`.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `movementType` | int? | `1` = só entradas, `2` = só saídas, `null` = todas |

**Fluxo alternativo — sêmen não encontrado:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

---

### CU-03 — Editar Movimentação Manual

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `PATCH /api/semen-samples/{semenSampleId}/movements/{movementId}` com `SemenSampleMovementUpdateDto`.
2. Sistema carrega a movimentação e valida que pertence ao tenant.
3. Sistema valida que `BreedingEventId == null` — se não, lança `ConflictException`.
4. Sistema aplica apenas os campos presentes no payload.
5. Retorna `200 OK` com `SemenSampleMovementDto` atualizado.

**Fluxo alternativo — movimentação não encontrada:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

**Fluxo alternativo — movimentação gerada pelo sistema:**
- Passo 3 → lança `ConflictException` → `409 Conflict`.

---

### CU-04 — Inativar Movimentação Manual

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/semen-samples/{semenSampleId}/movements/{movementId}`.
2. Sistema carrega a movimentação e valida que pertence ao tenant.
3. Sistema valida que `BreedingEventId == null` — se não, lança `ConflictException`.
4. Sistema seta `IsActive = false`.
5. Retorna `204 No Content`.

**Fluxo alternativo — movimentação não encontrada:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

**Fluxo alternativo — movimentação gerada pelo sistema:**
- Passo 3 → lança `ConflictException` → `409 Conflict`.

---

### CU-05 — Criação Automática de Entrada ao Registrar Sêmen com Estoque Inicial

**Ator:** Produtor autenticado
**Pré-condição:** Nenhuma.

**Fluxo principal:**
1. Produtor envia `POST /api/semen-samples` com `SemenSampleCreateDto` contendo `InitialQuantity` preenchido.
2. `SemenSampleService.CreateAsync` cria o `SemenSample`.
3. Ao final do mesmo método, chama `ISemenSampleMovementService.CreateForSemenSampleAsync(semenSampleId, initialQuantity, initialNotes)`.
4. O serviço cria um `SemenSampleMovement` com:
   - `SemenSampleId` = id do sêmen recém-criado
   - `MovementType` = `Input`
   - `MovementDate` = `DateTime.UtcNow`
   - `Quantity` = `initialQuantity`
   - `Notes` = `initialNotes` (pode ser `null`)
   - `BreedingEventId` = `null`
   - `IsActive` = `true`
5. Retorna `201 Created` com `SemenSampleDto` (incluindo `AvailableDoses` já atualizado).

**Fluxo alternativo — `InitialQuantity` não informado:**
- Passo 3 é ignorado; nenhum movimento é criado.

---

### CU-06 — Criação Automática de Saída ao Registrar Inseminação (ajuste no Spec #5, CU-01)

**Ator:** Sistema (efeito colateral de CU-01 do Spec #5)
**Pré-condição:** `BreedingEvent` criado com `ReproductionType = ArtificialInsemination`.

**Fluxo:**
1. `BreedingEventService.CreateAsync` cria o `BreedingEvent`.
2. Ao final do mesmo método, chama `ISemenSampleMovementService.CreateForBreedingEventAsync(breedingEvent)`.
3. O serviço cria um `SemenSampleMovement` com:
   - `SemenSampleId` = `breedingEvent.SemenSampleId`
   - `MovementType` = `Output`
   - `MovementDate` = `breedingEvent.BreedingDate`
   - `Quantity` = `1`
   - `BreedingEventId` = `breedingEvent.Id`
   - `IsActive` = `true`

---

### CU-06 — Inativação Automática de Saída ao Inativar Evento (ajuste no Spec #5, CU-05)

**Ator:** Sistema (efeito colateral de CU-05 do Spec #5)
**Pré-condição:** `BreedingEvent` com `ReproductionType = ArtificialInsemination` sendo inativado.

**Fluxo:**
1. `BreedingEventService.InactivateAsync` seta `BreedingEvent.IsActive = false`.
2. Chama `ISemenSampleMovementService.InactivateForBreedingEventAsync(breedingEventId)`.
3. O serviço busca o `SemenSampleMovement` com `BreedingEventId = breedingEventId` e seta `IsActive = false`.
4. A dose retorna automaticamente ao saldo disponível.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Alterações na entidade `SemenSample`

> Localização: `Domain/Models/SemenSample.cs`

**Campos removidos:**

| Campo | Motivo |
|-------|--------|
| `CollectedAt` | Desnecessário (D2) |
| `ManufacturedAt` | Desnecessário (D2) |
| `ReceivedAt` | Desnecessário (D2) |
| `ExpiresAt` | Desnecessário (D2) |

**Campos adicionados:**

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `BatchNumber` | `string?` (max 100) | Não | Número de partida / lote. Identifica o batch ao qual este registro pertence. |
| `BatchDate` | `DateTime?` | Não | Data de partida (fabricação ou emissão do lote). |

**Navigation adicionada:**
```csharp
public ICollection<SemenSampleMovement>? Movements { get; set; }
```

**Entidade resultante:**

```csharp
public class SemenSample : BaseEntity, ITenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BullRegistration { get; set; }

    [MaxLength(200)]
    public string? GeneticsCompany { get; set; }

    public AnimalBreed? BullBreed { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? BatchDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public ICollection<SemenSampleMovement>? Movements { get; set; }
}
```

---

### 5.2 Nova entidade `SemenSampleMovement`

> Localização: `Domain/Models/SemenSampleMovement.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `SemenSampleId` | `int` | Sim | FK → `SemenSample`. |
| `MovementType` | `SemenMovementType` (enum) | Sim | `Input` (entrada) ou `Output` (saída). |
| `MovementDate` | `DateTime` | Sim | Data da compra, do descarte ou da inseminação. |
| `Quantity` | `int` | Sim | Número de palhetas. Sempre positivo; direção determinada por `MovementType`. |
| `Notes` | `string?` (max 500) | Não | Observações livres (ex: motivo do descarte). |
| `BreedingEventId` | `int?` | Não | Preenchido automaticamente quando a saída origina de uma inseminação. `null` = movimentação manual. |
| `PropertyId` | `Guid` | Sim | FK tenant. |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. |

```csharp
public class SemenSampleMovement : BaseEntity, ITenantEntity
{
    public int SemenSampleId { get; set; }

    public SemenMovementType MovementType { get; set; }

    public DateTime MovementDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? BreedingEventId { get; set; }

    public Guid PropertyId { get; set; }

    public SemenSample? SemenSample { get; set; }
    public BreedingEvent? BreedingEvent { get; set; }
}
```

---

### 5.3 Novo Enum `SemenMovementType`

> Localização: `Domain/Enums/SemenMovementType.cs`

```csharp
public enum SemenMovementType
{
    [Description("Entrada")]
    Input = 1,

    [Description("Saída")]
    Output = 2
}
```

---

### 5.4 Cálculo de `AvailableDoses`

Calculado no `SemenSampleService` ao montar `SemenSampleDto` e `SemenSampleListItemDto`. O repositório expõe `GetAvailableDosesAsync(int semenSampleId)`:

```
AvailableDoses =
    SUM(Quantity WHERE SemenSampleId = X AND MovementType = Input  AND IsActive = true)
  - SUM(Quantity WHERE SemenSampleId = X AND MovementType = Output AND IsActive = true)
```

> Inseminações inativas não reduzem o saldo (movimento `Output` vinculado também é inativado via CU-06).

---

### 5.5 DTOs

#### DTOs atualizados de `SemenSample`

**Remover** dos DTOs existentes (`SemenSampleCreateDto`, `SemenSampleUpdateDto`, `SemenSampleDto`, `SemenSampleListItemDto`):
- `CollectedAt`, `ManufacturedAt`, `ReceivedAt`, `ExpiresAt`

**Adicionar** aos DTOs de `SemenSample`:

`SemenSampleCreateDto` e `SemenSampleUpdateDto`:
```csharp
[MaxLength(100)]
public string? BatchNumber { get; set; }

public DateTime? BatchDate { get; set; }
```

`SemenSampleDto` (detalhe) e `SemenSampleListItemDto` (listagem):
```csharp
public string? BatchNumber { get; set; }
public DateTime? BatchDate { get; set; }
public int AvailableDoses { get; set; }
```

`SemenSampleAutocompleteItemDto` (autocomplete):
```csharp
public string? BatchNumber { get; set; }
public DateTime? BatchDate { get; set; }
```

> **Motivo:** Quando dois registros de sêmen têm o mesmo nome (ex.: mesmo touro comprado em lotes distintos), o front-end pode exibir `BatchNumber` e `BatchDate` para o produtor diferenciar as opções no autocomplete.

---

#### Novos DTOs de `SemenSampleMovement`

**`SemenSampleCreateDto.cs`** — campos adicionados (além dos já descritos em 5.5):

```csharp
[Range(1, int.MaxValue, ErrorMessage = "A quantidade inicial deve ser maior que zero.")]
public int? InitialQuantity { get; set; }

[MaxLength(500)]
public string? InitialNotes { get; set; }
```

> `InitialNotes` só é utilizado se `InitialQuantity` for informado; caso contrário, é ignorado.

---

**`SemenSampleMovementCreateDto.cs`**

```csharp
public class SemenSampleMovementCreateDto
{
    [Required(ErrorMessage = "O tipo de movimentação é obrigatório.")]
    public SemenMovementType MovementType { get; set; }

    [Required(ErrorMessage = "A data da movimentação é obrigatória.")]
    public DateTime MovementDate { get; set; }

    [Required(ErrorMessage = "A quantidade é obrigatória.")]
    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

---

**`SemenSampleMovementUpdateDto.cs`**

```csharp
public class SemenSampleMovementUpdateDto
{
    public DateTime? MovementDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int? Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

> **Nota:** `MovementType` não é editável via PATCH — alterar a direção de um movimento representa um erro de modelagem; o correto é inativar e criar outro.

---

**`SemenSampleMovementDto.cs`** (response — detalhe e criação)

```csharp
public class SemenSampleMovementDto
{
    public int Id { get; set; }
    public int SemenSampleId { get; set; }
    public string SemenSampleName { get; set; } = string.Empty;
    public SemenMovementType MovementType { get; set; }
    public string MovementTypeLabel { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public int? BreedingEventId { get; set; }
    public bool IsSystemGenerated => BreedingEventId.HasValue;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

**`SemenSampleMovementListItemDto.cs`** (response — listagem)

```csharp
public class SemenSampleMovementListItemDto
{
    public int Id { get; set; }
    public SemenMovementType MovementType { get; set; }
    public string MovementTypeLabel { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public int Quantity { get; set; }
    public int? BreedingEventId { get; set; }
    public bool IsActive { get; set; }
}
```

---

### 5.6 Endpoints da API

> Auth: Bearer Token obrigatório em todos os endpoints.

**Novos endpoints (`SemenSampleMovement`):**

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/semen-samples/{semenSampleId}/movements` | Registrar movimentação manual (entrada ou saída) | `201 SemenSampleMovementDto` / `404` |
| `GET` | `/api/semen-samples/{semenSampleId}/movements` | Listar histórico de movimentações | `200 SemenSampleMovementListItemDto[]` / `404` |
| `PATCH` | `/api/semen-samples/{semenSampleId}/movements/{movementId}` | Editar movimentação manual | `200 SemenSampleMovementDto` / `404` / `409` |
| `DELETE` | `/api/semen-samples/{semenSampleId}/movements/{movementId}` | Inativar movimentação manual | `204` / `404` / `409` |

**Endpoints existentes com comportamento alterado:**

| Método | Rota | Mudança |
|--------|------|---------|
| `GET` | `/api/semen-samples/{id}` | Resposta inclui `BatchNumber`, `BatchDate` e `AvailableDoses` |
| `GET` | `/api/semen-samples` | Resposta inclui `BatchNumber`, `BatchDate` e `AvailableDoses` por item |

---

### 5.7 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `MovementType`, `MovementDate` e `Quantity` são obrigatórios para movimentação manual. | DTO (DataAnnotations) |
| RN-02 | `Quantity` deve ser maior que zero. | DTO (DataAnnotations) |
| RN-03 | Saldo negativo é permitido — não bloqueia inseminações. | Informativo; sem validação ativa. |
| RN-04 | Movimentos inativos (`IsActive = false`) não entram no cálculo do saldo. | Repository (filtro `IsActive = true` na agregação) |
| RN-05 | Movimentos com `BreedingEventId != null` não podem ser editados ou inativados diretamente. | Service → lança `ConflictException` |
| RN-06 | Ao criar `BreedingEvent` com IA, criar automaticamente um `SemenSampleMovement` do tipo `Output` com `Quantity = 1`. | `BreedingEventService.CreateAsync` → chama `ISemenSampleMovementService` |
| RN-07 | Ao inativar `BreedingEvent` com IA, inativar automaticamente o `SemenSampleMovement` vinculado (`BreedingEventId`). | `BreedingEventService.InactivateAsync` → chama `ISemenSampleMovementService` |
| RN-08 | `MovementType` não é editável via PATCH — deve-se inativar e criar novo movimento. | Service → campo ignorado no UpdateDto |
| RN-09 | Isolamento de tenant: repositório filtra `SemenSampleMovement` por `PropertyId`. | Repository |
| RN-10 | Se `InitialQuantity` for informado no cadastro de `SemenSample`, criar automaticamente um `Input` com `MovementDate = DateTime.UtcNow`, `Quantity = InitialQuantity`, `Notes = InitialNotes` e `BreedingEventId = null`. | `SemenSampleService.CreateAsync` → chama `ISemenSampleMovementService.CreateForSemenSampleAsync` |

---

### 5.8 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `SemenSample.cs` | Remover campos de data; adicionar `BatchNumber`, `BatchDate` e navigation `Movements` |
| `Domain/Models` | `SemenSampleMovement.cs` | **Criar** |
| `Domain/Enums` | `SemenMovementType.cs` | **Criar** |
| `Application/DTOs` | `SemenSampleCreateDto.cs` | Remover campos de data; adicionar `BatchNumber`, `BatchDate`, `InitialQuantity`, `InitialNotes` |
| `Application/DTOs` | `SemenSampleUpdateDto.cs` | Remover campos de data; adicionar `BatchNumber`, `BatchDate` |
| `Application/DTOs` | `SemenSampleDto.cs` | Remover campos de data; adicionar `BatchNumber`, `BatchDate`, `AvailableDoses` |
| `Application/DTOs` | `SemenSampleListItemDto.cs` | Remover `ExpiresAt`; adicionar `BatchNumber`, `BatchDate`, `AvailableDoses` |
| `Application/DTOs` | `SemenSampleAutocompleteItemDto.cs` | Adicionar `BatchNumber` e `BatchDate` |
| `Application/DTOs` | `SemenSampleMovementCreateDto.cs` | **Criar** |
| `Application/DTOs` | `SemenSampleMovementUpdateDto.cs` | **Criar** |
| `Application/DTOs` | `SemenSampleMovementDto.cs` | **Criar** |
| `Application/DTOs` | `SemenSampleMovementListItemDto.cs` | **Criar** |
| `Application/Mappings` | `SemenSampleProfile.cs` | Remover mapeamento dos campos removidos; adicionar `BatchNumber`, `BatchDate`; mapear `AvailableDoses` |
| `Application/Mappings` | `SemenSampleMovementProfile.cs` | **Criar** |
| `Application/Services` | `SemenSampleService.cs` | Remover lógica dos campos removidos; calcular e incluir `AvailableDoses`; chamar `ISemenSampleMovementService` quando `InitialQuantity` for informado |
| `Application/Services` | `SemenSampleMovementService.cs` | **Criar** (inclui `CreateForSemenSampleAsync`, `CreateForBreedingEventAsync` e `InactivateForBreedingEventAsync`) |
| `Application/Services` | `BreedingEventService.cs` | Chamar `ISemenSampleMovementService` ao criar e inativar evento com IA |
| `Application/Interfaces` | `ISemenSampleService.cs` | Atualizar assinaturas |
| `Application/Interfaces` | `ISemenSampleRepository.cs` | Adicionar `GetAvailableDosesAsync(int semenSampleId)` |
| `Application/Interfaces` | `ISemenSampleMovementService.cs` | **Criar** (inclui assinatura `CreateForSemenSampleAsync`) |
| `Application/Interfaces` | `ISemenSampleMovementRepository.cs` | **Criar** |
| `Infrastructure/Repositories` | `SemenSampleRepository.cs` | Implementar `GetAvailableDosesAsync` |
| `Infrastructure/Repositories` | `SemenSampleMovementRepository.cs` | **Criar** |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Adicionar `DbSet<SemenSampleMovement>`; configurar FKs e índices em `OnModelCreating` |
| `Api/Controllers` | `SemenSamplesController.cs` | Adicionar rotas aninhadas de `movements` |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:

1. **Alterar tabela `SemenSamples`:**
   - Remover colunas: `CollectedAt`, `ManufacturedAt`, `ReceivedAt`, `ExpiresAt`
   - Adicionar coluna: `BatchNumber` (nvarchar(100), nullable)
   - Adicionar coluna: `BatchDate` (datetime2, nullable)

2. **Criar tabela `SemenSampleMovements`** com colunas:
   - `Id` (int, PK, identity)
   - `SemenSampleId` (int, not null, FK → `SemenSamples`)
   - `MovementType` (int, not null)
   - `MovementDate` (datetime2, not null)
   - `Quantity` (int, not null)
   - `Notes` (nvarchar(500), nullable)
   - `BreedingEventId` (int, nullable, FK → `BreedingEvents`)
   - `PropertyId` (uniqueidentifier, not null)
   - `IsActive` (bit, not null, default 1)
   - `CreatedAt` (datetime2, not null)
   - `UpdatedAt` (datetime2, nullable)

3. **Criar índices** em `SemenSampleMovements`:
   - `(SemenSampleId, MovementType, IsActive)` — otimiza a agregação de saldo.
   - `(BreedingEventId)` — otimiza a busca do movimento ao inativar um `BreedingEvent`.
   - `(PropertyId, IsActive)` — isolamento de tenant.

4. **Configurar FKs:**
   - `SemenSampleMovements.SemenSampleId → SemenSamples.Id` com `ON DELETE RESTRICT`.
   - `SemenSampleMovements.BreedingEventId → BreedingEvents.Id` com `ON DELETE RESTRICT`.

---

## 7. Fora do Escopo deste Spec

- **Cadastro de animais** → Spec #1
- **Entrada e saída de animais** → Spec #2
- **Escore de Condição Corporal** → Spec #3
- **Banco de Sêmen (versão original)** → Spec #4
- **Eventos Reprodutivos** → Spec #5
- **Gestação e Parto** → Spec #6
- **Dashboards de índices zootécnicos** → Spec #7
- **Bloqueio de inseminação por saldo zero** — informativo neste spec; pode ser introduzido futuramente como regra configurável por propriedade.
- **Devolução de dose por monta natural inativada** — não se aplica (monta natural não gera movimento de estoque).
