# Spec: Banco de Sêmen

**Módulo:** Banco de Sêmen  
**Versão:** 1.0  
**Data:** 20/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais  
**Status:** Aprovado para implementação  
**Depende de:** Nenhum — entidade independente.  
**Referenciado por:** Spec #5 — Eventos Reprodutivos (inseminação artificial)

---

## 1. Contexto e Objetivo

O Banco de Sêmen é um catálogo de amostras de sêmen disponíveis na propriedade para uso em inseminação artificial (Spec #5). Cada registro representa um tipo de sêmen identificado pelo produtor — geralmente por nome/apelido interno — com informações opcionais sobre o touro doador, a central genética fornecedora e as datas relevantes (coleta, fabricação, entrada na propriedade e validade).

**Não há controle de estoque (quantidade de doses):** o módulo é um registro de referência, não um inventário. A quantidade de doses disponíveis não é gerenciada pelo sistema.

O objetivo é permitir que o produtor cadastre e consulte as amostras de sêmen e selecione uma delas ao registrar um evento reprodutivo por inseminação artificial.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Entidade `SemenSample` — CRUD independente de `Animal` | A ata define explicitamente o banco de sêmen como separado do cadastro de animais. |
| D2 | Sem controle de estoque (quantidade de doses) | Produtor confirmou: o banco é só catálogo de referência. Complexidade de inventário está fora do escopo deste TCC. |
| D3 | `Name` (apelido interno) é o único campo obrigatório | O produtor nem sempre tem todos os dados do touro ou da central. O apelido interno é suficiente para identificar e selecionar o sêmen. |
| D4 | `CollectedAt` e `ManufacturedAt` são campos **distintos e opcionais** | Em coleta local (touro próprio) o produtor conhece a data de coleta. Em sêmen de central genética, conhece a data de fabricação do produto. Na prática são a mesma data, mas manter os dois campos separados evita ambiguidade e não há custo de modelagem. |
| D5 | `BullBreed` reutiliza o enum `AnimalBreed` (Spec #1) | As raças leiteiras relevantes são as mesmas (Holandesa, Jersey, Híbrida/Mestiça). Não há necessidade de enum separado. |
| D6 | Inativação permitida mesmo se sêmen referenciado em eventos reprodutivos | O soft delete (`IsActive = false`) não remove o registro nem a FK — a rastreabilidade histórica é preservada. Inativar apenas impede que a amostra apareça em novas seleções, que é o comportamento desejado quando o sêmen não está mais disponível. |
| D7 | `SemenSample` implementa `ITenantEntity` (PropertyId) | Cada propriedade gerencia seu próprio banco de sêmen. |
| D8 | Soft delete — inativação em vez de exclusão | Consistência com o padrão do projeto. Um sêmen inativo não aparece nas seleções de novos eventos reprodutivos. |

---

## 3. Histórias de Usuário

### US-01 — Cadastrar amostra de sêmen
> **Como** produtor,  
> **quero** cadastrar uma amostra de sêmen no banco,  
> **para** poder selecioná-la ao registrar uma inseminação artificial.

**Critérios de aceite:**
- O campo `Name` (apelido interno) é obrigatório.
- Todos os demais campos são opcionais.
- Ao cadastrar, a amostra recebe `IsActive = true`.

---

### US-02 — Editar amostra de sêmen
> **Como** produtor,  
> **quero** editar os dados de uma amostra de sêmen,  
> **para** corrigir informações ou complementar dados inseridos previamente.

**Critérios de aceite:**
- Todos os campos são opcionais no PATCH — apenas o que for enviado é alterado.

---

### US-03 — Listar banco de sêmen
> **Como** produtor,  
> **quero** ver a lista de amostras de sêmen disponíveis,  
> **para** consultar o catálogo e selecionar ao registrar inseminações.

**Critérios de aceite:**
- A lista retorna apenas amostras da propriedade do usuário autenticado (multi-tenant).
- Filtros disponíveis: nome (busca parcial), raça do touro, status ativo/inativo.
- Por padrão (`IsActive = null`), retorna todas; `true` = só ativas, `false` = só inativas.

---

### US-04 — Visualizar detalhe de uma amostra
> **Como** produtor,  
> **quero** abrir o cadastro completo de uma amostra de sêmen,  
> **para** consultar todos os dados registrados.

**Critérios de aceite:**
- Exibe todos os campos da amostra.

---

### US-06 — Buscar amostras para autocomplete
> **Como** produtor,  
> **quero** buscar amostras de sêmen pelo nome enquanto digito,  
> **para** selecionar rapidamente a amostra ao registrar uma inseminação artificial.

**Critérios de aceite:**
- Retorna apenas amostras **ativas** da propriedade do usuário autenticado.
- Filtro por `name` (busca parcial, case-insensitive) é opcional — sem filtro retorna todas as ativas.
- A resposta contém apenas `Id` e `Name`.

---

### US-05 — Inativar amostra de sêmen
> **Como** produtor,  
> **quero** inativar uma amostra de sêmen que não está mais disponível,  
> **para** que ela não apareça como opção em novas inseminações.

**Critérios de aceite:**
- Inativar seta `IsActive = false`. A amostra não aparece na listagem padrão nem como opção em novas inseminações.
- A inativação é permitida mesmo que a amostra já tenha sido utilizada em eventos reprodutivos — o histórico é preservado pela FK existente.
- A exclusão física não é permitida.

---

## 4. Casos de Uso

### CU-01 — Cadastrar Amostra de Sêmen

**Ator:** Produtor autenticado  
**Pré-condição:** Usuário autenticado e vinculado a uma propriedade.

**Fluxo principal:**
1. Produtor envia `POST /api/semen-samples` com os dados da amostra.
2. Sistema valida que `Name` está presente.
3. Sistema cria a amostra com `IsActive = true`.
4. Retorna `201 Created` com `SemenSampleDto`.

---

### CU-02 — Editar Amostra de Sêmen

**Ator:** Produtor autenticado  
**Pré-condição:** Amostra existe e pertence à propriedade do usuário.

**Fluxo principal:**
1. Produtor envia `PATCH /api/semen-samples/{id}` com os campos a alterar.
2. Sistema carrega a amostra e valida que pertence ao tenant.
3. Sistema aplica apenas os campos presentes no payload.
4. Retorna `200 OK` com `SemenSampleDto` atualizado.

**Fluxo alternativo — amostra não encontrada:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

---

### CU-03 — Listar Amostras de Sêmen

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/semen-samples` com filtros opcionais.
2. Repositório filtra por `PropertyId` do tenant atual.
3. Aplica filtros adicionais se presentes.
4. Retorna lista de `SemenSampleListItemDto`.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `name` | string | Busca parcial no apelido interno |
| `bullBreed` | int | Valor do enum `AnimalBreed` |
| `isActive` | bool? | `true` = só ativas, `false` = só inativas, `null` = todas |

---

### CU-04 — Visualizar Amostra de Sêmen

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/semen-samples/{id}`.
2. Sistema carrega a amostra e valida o tenant.
3. Retorna `200 OK` com `SemenSampleDto`.
4. Se não encontrada → `404 Not Found`.

---

### CU-06 — Autocomplete de Amostras de Sêmen

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/semen-samples/autocomplete` com `name` opcional.
2. Repositório filtra por `PropertyId` do tenant atual e `IsActive = true`.
3. Aplica busca parcial no `Name` se o parâmetro estiver presente.
4. Retorna lista de `SemenSampleAutocompleteItemDto`.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `name` | string | Busca parcial no apelido interno (opcional) |

---

### CU-05 — Inativar Amostra de Sêmen

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/semen-samples/{id}`.
2. Sistema carrega a amostra e valida o tenant.
3. Sistema seta `IsActive = false`.
4. Retorna `204 No Content`.

**Fluxo alternativo — amostra não encontrada:**
- Passo 2 falha → lança `NotFoundException` → `404 Not Found`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Nova Entidade `SemenSample`

> Localização: `Domain/Models/SemenSample.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `Name` | `string` (max 100) | Sim | Apelido interno da amostra. Identificador principal para o produtor. |
| `BullName` | `string?` (max 200) | Não | Nome do touro doador. |
| `BullRegistration` | `string?` (max 100) | Não | Registro ANCP ou código da central genética. |
| `GeneticsCompany` | `string?` (max 200) | Não | Central/empresa genética fornecedora. |
| `BullBreed` | `AnimalBreed?` (enum) | Não | Raça do touro doador. Reutiliza enum do Spec #1. |
| `CollectedAt` | `DateTime?` | Não | Data de coleta do touro (uso em coleta local). |
| `ManufacturedAt` | `DateTime?` | Não | Data de fabricação do produto (uso em sêmen de central). |
| `ReceivedAt` | `DateTime?` | Não | Data de entrada na propriedade. |
| `ExpiresAt` | `DateTime?` | Não | Data de validade. |
| `Notes` | `string?` (max 500) | Não | Observações livres. |
| `PropertyId` | `Guid` | Sim | FK tenant. |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. |

```csharp
public class SemenSample : BaseEntity, ITenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BullName { get; set; }

    [MaxLength(100)]
    public string? BullRegistration { get; set; }

    [MaxLength(200)]
    public string? GeneticsCompany { get; set; }

    public AnimalBreed? BullBreed { get; set; }

    public DateTime? CollectedAt { get; set; }

    public DateTime? ManufacturedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }
}
```

---

### 5.2 Enums

Nenhum enum novo. Este módulo reutiliza `AnimalBreed` (Spec #1) para a raça do touro doador.

---

### 5.3 DTOs

#### `SemenSampleCreateDto.cs`

```csharp
public class SemenSampleCreateDto
{
    [Required(ErrorMessage = "O nome/apelido interno é obrigatório.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BullName { get; set; }

    [MaxLength(100)]
    public string? BullRegistration { get; set; }

    [MaxLength(200)]
    public string? GeneticsCompany { get; set; }

    public AnimalBreed? BullBreed { get; set; }

    public DateTime? CollectedAt { get; set; }

    public DateTime? ManufacturedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

---

#### `SemenSampleUpdateDto.cs`

```csharp
public class SemenSampleUpdateDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? BullName { get; set; }

    [MaxLength(100)]
    public string? BullRegistration { get; set; }

    [MaxLength(200)]
    public string? GeneticsCompany { get; set; }

    public AnimalBreed? BullBreed { get; set; }

    public DateTime? CollectedAt { get; set; }

    public DateTime? ManufacturedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

---

#### `SemenSampleDto.cs` (response — detalhe)

```csharp
public class SemenSampleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BullName { get; set; }
    public string? BullRegistration { get; set; }
    public string? GeneticsCompany { get; set; }
    public AnimalBreed? BullBreed { get; set; }
    public string? BullBreedLabel { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? ManufacturedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

---

#### `SemenSampleListItemDto.cs` (response — listagem)

```csharp
public class SemenSampleListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BullName { get; set; }
    public string? GeneticsCompany { get; set; }
    public AnimalBreed? BullBreed { get; set; }
    public string? BullBreedLabel { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
```

---

#### `SemenSampleAutocompleteItemDto.cs` (response — autocomplete)

```csharp
public class SemenSampleAutocompleteItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

---

#### `SemenSampleFilterDto.cs` (query params)

```csharp
public class SemenSampleFilterDto
{
    public string? Name { get; set; }
    public AnimalBreed? BullBreed { get; set; }
    public bool? IsActive { get; set; } // null = todas, true = só ativas, false = só inativas
}
```

---

### 5.4 Endpoints da API

> Base: `/api/semen-samples` | Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/semen-samples` | Lista amostras com filtros | `200 SemenSampleListItemDto[]` |
| `GET` | `/api/semen-samples/autocomplete` | Autocomplete por nome (só ativas) | `200 SemenSampleAutocompleteItemDto[]` |
| `GET` | `/api/semen-samples/{id}` | Detalhe de uma amostra | `200 SemenSampleDto` / `404` |
| `POST` | `/api/semen-samples` | Cadastrar amostra | `201 SemenSampleDto` |
| `PATCH` | `/api/semen-samples/{id}` | Editar amostra | `200 SemenSampleDto` / `404` |
| `DELETE` | `/api/semen-samples/{id}` | Inativar amostra (soft delete) | `204` / `404` / `409` |

> **Nota:** Não há endpoint de enum de raças — reutilizar `/api/animals/breeds` definido no Spec #1.

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `Name` é obrigatório. | DTO (DataAnnotations) |
| RN-02 | Inativação sempre permitida — o soft delete preserva a FK nos eventos históricos. | Service |
| RN-03 | Inativação é sempre soft delete (`IsActive = false`). Nunca exclusão física. | Repository |
| RN-04 | Listagem sem filtro `IsActive` retorna todas as amostras. `true` filtra só ativas; `false` filtra só inativas. | Repository |
| RN-05 | Isolamento de tenant: repositório filtra por `PropertyId` em todas as operações. | Repository |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `SemenSample.cs` | Criar |
| `Application/DTOs` | `SemenSampleCreateDto.cs` | Criar |
| `Application/DTOs` | `SemenSampleUpdateDto.cs` | Criar |
| `Application/DTOs` | `SemenSampleDto.cs` | Criar |
| `Application/DTOs` | `SemenSampleListItemDto.cs` | Criar |
| `Application/DTOs` | `SemenSampleAutocompleteItemDto.cs` | Criar |
| `Application/DTOs` | `SemenSampleFilterDto.cs` | Criar |
| `Application/Mappings` | `SemenSampleProfile.cs` | Criar |
| `Application/Services` | `SemenSampleService.cs` | Criar |
| `Application/Interfaces` | `ISemenSampleService.cs` | Criar |
| `Application/Interfaces` | `ISemenSampleRepository.cs` | Criar |
| `Infrastructure/Repositories` | `SemenSampleRepository.cs` | Criar |
| `Api/Controllers` | `SemenSamplesController.cs` | Criar |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Criar tabela `SemenSamples` com colunas: `Id` (int, PK), `Name` (nvarchar(100), not null), `BullName` (nvarchar(200), nullable), `BullRegistration` (nvarchar(100), nullable), `GeneticsCompany` (nvarchar(200), nullable), `BullBreed` (int, nullable), `CollectedAt` (datetime2, nullable), `ManufacturedAt` (datetime2, nullable), `ReceivedAt` (datetime2, nullable), `ExpiresAt` (datetime2, nullable), `Notes` (nvarchar(500), nullable), `PropertyId` (uniqueidentifier, not null), `IsActive` (bit, not null, default 1), `CreatedAt` (datetime2, not null), `UpdatedAt` (datetime2, nullable).
2. Criar índice em `(PropertyId, IsActive)` para otimizar a listagem por tenant.

---

## 7. Fora do Escopo deste Spec

- **Cadastro de animais** → Spec #1
- **Entrada e saída de animais** → Spec #2
- **Escore de Condição Corporal** → Spec #3
- **Eventos Reprodutivos** (uso do sêmen em inseminação) → Spec #5
- **Gestação e Parto** → Spec #6
- **Dashboards de índices zootécnicos** → Spec #7
