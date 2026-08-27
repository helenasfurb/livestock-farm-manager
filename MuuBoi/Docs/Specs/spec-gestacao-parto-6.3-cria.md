# Spec 6.3: Cria (`AnimalCalvingCalf`)

**Módulo:** Gestação e Parto — Cria  
**Versão:** 1.0  
**Data:** 24/Ago/2026  
**Fonte:** Ordem de specs.txt; Spec #5 (Eventos Reprodutivos)  
**Status:** Aprovado para implementação  
**Depende de:** Spec 6.2 (Parto)  
**Parte de:** Spec #6 (Gestação e Parto)  
**Ver também:** Spec 6.1 (Gestação), Spec 6.2 (Parto)

---

## 1. Contexto e Objetivo

Ao registrar um parto (`AnimalCalving` — Spec 6.2), o produtor informa os dados de cada cria nascida. Este spec define a entidade `AnimalCalvingCalf`, que compõe o registro de parto e captura sexo, peso e status vital de cada animal nascido.

Partos duplos (gêmeos) são suportados: um `AnimalCalving` pode ter mais de uma `AnimalCalvingCalf`. O mínimo exigido é uma cria por parto.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Mínimo de uma `AnimalCalvingCalf` por `AnimalCalving` | Um parto sempre resulta em pelo menos uma cria (viva ou natimorta). |
| D2 | `AnimalCalvingCalf` não é inativada individualmente — ciclo de vida controlado pelo `AnimalCalving` | Crias são parte integrante de um parto; inativar individualmente causaria inconsistência. Ao inativar o parto (Spec 6.2 CU-02), todas as crias são inativadas automaticamente. |
| D3 | Crias não são vinculadas a uma entidade `Animal` neste spec | O cadastro do animal filho via Spec #2 (nascimento) é um passo separado. A ligação entre `AnimalCalvingCalf` e o `Animal` cadastrado está fora do escopo do Spec #6. |
| D4 | `WeightKg` é opcional | Nem todo produtor pesa os bezerros ao nascimento. |
| D5 | `AnimalCalvingCalf` implementa `ITenantEntity` (PropertyId) | Consistência com o padrão do projeto; isolamento de tenant. |

---

## 3. Histórias de Usuário

### US-01 — Registrar dados das crias no parto
> **Como** produtor,  
> **quero** informar os dados de cada cria nascida ao registrar um parto,  
> **para** ter o histórico de nascimentos da propriedade.

**Critérios de aceite:**
- O produtor informa, para cada cria: sexo (obrigatório) e status vital — nascido vivo ou natimorto (obrigatório).
- Peso ao nascimento e observações são opcionais.
- Ao menos uma cria deve ser informada por parto.
- Partos múltiplos (gêmeos) são suportados: o produtor pode informar N crias.

---

### US-02 — Consultar crias de um parto
> **Como** produtor,  
> **quero** ver as crias registradas de um parto,  
> **para** consultar o histórico de nascimentos de uma fêmea.

**Critérios de aceite:**
- As crias aparecem aninhadas no detalhe da gestação (`AnimalPregnancyDto.Calving.Calves`).
- Exibe: sexo, label do sexo, peso (se informado), status vital, label do status vital e observações.

---

## 4. Casos de Uso

### CU-01 — Criação de Crias ao Registrar Parto (parte de CU-01 do Spec 6.2)

**Ator:** Sistema (dentro de `AnimalCalvingService.CreateAsync`)  
**Pré-condição:** `AnimalCalving` criada com sucesso.

**Fluxo:**
1. Para cada item em `AnimalCalvingCreateDto.Calves`:
   - Sistema cria `AnimalCalvingCalf` com `CalvingId`, `Sex`, `WeightKg`, `VitalStatus`, `Notes` e `PropertyId`.
   - `IsActive = true`.
2. Todos os registros de cria são persistidos na mesma transação do parto.

---

### CU-02 — Inativação Automática de Crias ao Inativar Parto (parte de CU-02 do Spec 6.2)

**Ator:** Sistema (dentro de `AnimalCalvingService.InactivateAsync`)

**Fluxo:**
1. Sistema carrega todas as `AnimalCalvingCalf` com `CalvingId = calvingId` e `IsActive = true`.
2. Seta `IsActive = false` em cada uma.
3. Persistido na mesma transação da inativação do parto.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Entidade `AnimalCalvingCalf`

> Localização: `Domain/Models/AnimalCalvingCalf.cs`

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `CalvingId` | `int` | Sim | FK → `AnimalCalving`. |
| `Sex` | `AnimalSex` | Sim | Sexo da cria (enum existente de Spec #1). |
| `WeightKg` | `decimal?` | Não | Peso ao nascimento (kg). |
| `VitalStatus` | `CalfVitalStatus` | Sim | `Live` ou `Stillborn`. |
| `Notes` | `string?` (max 500) | Não | Observações individuais da cria. |
| `PropertyId` | `Guid` | Sim | FK tenant. |
| `Calving` | navigation | — | Parto ao qual pertence. |
| *(BaseEntity)* | `Id`, `IsActive`, `CreatedAt`, `UpdatedAt` | — | Herdados. `IsActive` gerenciado pelo sistema — não exposto individualmente na API. |

```csharp
public class AnimalCalvingCalf : BaseEntity, ITenantEntity
{
    public int CalvingId { get; set; }

    public AnimalSex Sex { get; set; }

    [Range(0.01, 999.99)]
    public decimal? WeightKg { get; set; }

    public CalfVitalStatus VitalStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public AnimalCalving? Calving { get; set; }
}
```

---

### 5.2 Novo Enum `CalfVitalStatus`

> Localização: `Domain/Enums/CalfVitalStatus.cs`

```csharp
public enum CalfVitalStatus
{
    [Description("Nascido Vivo")]
    Live = 1,

    [Description("Natimorto")]
    Stillborn = 2
}
```

---

### 5.3 DTOs

#### `AnimalCalvingCalfCreateDto.cs`

```csharp
public class AnimalCalvingCalfCreateDto
{
    [Required(ErrorMessage = "O sexo da cria é obrigatório.")]
    public AnimalSex Sex { get; set; }

    [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
    public decimal? WeightKg { get; set; }

    [Required(ErrorMessage = "O status vital da cria é obrigatório.")]
    public CalfVitalStatus VitalStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

---

#### `AnimalCalvingCalfDto.cs` (response)

```csharp
public class AnimalCalvingCalfDto
{
    public int Id { get; set; }
    public AnimalSex Sex { get; set; }
    public string SexLabel { get; set; } = string.Empty;
    public decimal? WeightKg { get; set; }
    public CalfVitalStatus VitalStatus { get; set; }
    public string VitalStatusLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
```

---

### 5.4 Endpoints da API

> Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/calvings/calf-vital-statuses` | Enum de status vital de cria | `200 [{value, label}]` |

> Crias não possuem endpoints próprios de CRUD. Elas são criadas como parte do `POST /api/pregnancies/{id}/calvings` (Spec 6.2) e consultadas via `GET /api/pregnancies/{id}` (Spec 6.1), aninhadas em `AnimalCalvingDto.Calves`.

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | Ao menos uma `AnimalCalvingCalf` por parto. | DTO (`MinLength` em `AnimalCalvingCreateDto.Calves`) |
| RN-02 | `WeightKg`, se informado, deve ser > 0. | DTO (DataAnnotations — `Range`) |
| RN-03 | `AnimalCalvingCalf` não é inativada individualmente — ciclo de vida atrelado ao `AnimalCalving`. | Service (ausência de endpoint de DELETE individual) |
| RN-04 | Ao inativar o parto, todas as crias são inativadas automaticamente pelo sistema. | `AnimalCalvingService.InactivateAsync` |
| RN-05 | Isolamento de tenant: repositório filtra por `PropertyId`. | Repository |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalCalvingCalf.cs` | **Criar** |
| `Domain/Enums` | `CalfVitalStatus.cs` | **Criar** |
| `Application/DTOs` | `AnimalCalvingCalfCreateDto.cs` | **Criar** |
| `Application/DTOs` | `AnimalCalvingCalfDto.cs` | **Criar** |
| `Application/Mappings` | `AnimalCalvingProfile.cs` | Incluir mapeamento de `AnimalCalvingCalf → AnimalCalvingCalfDto` |
| `Application/Services` | `AnimalCalvingService.cs` | Criar crias em `CreateAsync`; inativar crias em `InactivateAsync` |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Adicionar `DbSet<AnimalCalvingCalf>`; configurar FK e índice em `OnModelCreating` |
| `Api/Controllers` | `CalvingsController.cs` | Adicionar rota `GET /api/calvings/calf-vital-statuses` |
| `Infrastructure/Migrations` | *(ver seção 6)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

**Criar tabela `AnimalCalvingCalves`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `CalvingId` | int | not null, FK → `AnimalCalvings`, `ON DELETE RESTRICT` |
| `Sex` | int | not null |
| `WeightKg` | decimal(6,2) | nullable |
| `VitalStatus` | int | not null |
| `Notes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Índices:**

| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(CalvingId)` | Composto | Otimiza listagem e inativação em massa de crias de um parto. |

---

## 7. Fora do Escopo deste Spec

- **Vinculação de cria cadastrada como Animal** — `AnimalCalvingCalf` não possui FK para `Animal`. O produtor cadastra o animal filho separadamente via Spec #2. A ligação entre os dois registros fica para implementação futura.
- **Registro do parto** → Spec 6.2
- **Gestação** → Spec 6.1
- **Cadastro de animais** → Spec #1
- **Entrada de animais por nascimento** → Spec #2
