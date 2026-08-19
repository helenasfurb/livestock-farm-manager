# Spec: Gestão de Animais

**Módulo:** Animal  
**Versão:** 1.1  
**Data:** 18/Ago/2026  
**Fonte:** Ata do Primeiro Encontro de Consultoria — Gestão de Animais  
**Status:** Aprovado para implementação

---

## 1. Contexto e Objetivo

O módulo de animais é a fundação de todo o sistema. Todo o restante (reprodução, saúde, índices zootécnicos) depende do cadastro correto dos animais.

O objetivo é permitir que o pequeno produtor de leite **identifique, cadastre e acompanhe individualmente os animais do rebanho**, mantendo o histórico mesmo após a saída de um animal da propriedade.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Raça será enum fixo (3 valores iniciais) | Simplifica o cadastro; o produtor não precisa gerenciar raças. Expandível no futuro. |
| D2 | A entidade `Breed` e sua tabela são descontinuadas | Substituída pelo enum `AnimalBreed` diretamente em `Animal`. |
| D3 | Nome do animal é opcional | O brinco é o identificador principal. Nome é apelido, não obrigatório. |
| D4 | `TagNumber`: 6 dígitos numéricos, obrigatório, único por propriedade | É o identificador oficial do animal. Campo mantém o nome atual. |
| D5 | Classificação sem histórico | Basta o valor atual. Atualizada manualmente pelo produtor. |
| D6 | Origem: apenas enum (Nascido na Propriedade / Adquirido) | Dados extras de compra podem ir em Observações por ora. |
| D7 | `IsPregnant`, `ExpectedBirthDate` e `ReproductiveStatus` removidos da entidade `Animal` | Status reprodutivo é derivado dos eventos reprodutivos e registros de gestação/parto — não é estado persistido no animal. Pertence ao Spec #5. |
| D8 | Soft delete — inativação em vez de exclusão | Histórico do animal é preservado mesmo após saída da propriedade. |
| D9 | `Calf` é classificação neutra em relação ao `Gender` | A distinção Bezerra/Bezerro já é coberta pelo campo `Gender`. |

---

## 3. Histórias de Usuário

### US-01 — Cadastrar animal
> **Como** produtor,  
> **quero** cadastrar um animal informando seus dados básicos,  
> **para** que ele faça parte do meu rebanho e possa ser acompanhado individualmente.

**Critérios de aceite:**
- Deve ser possível cadastrar um animal com apenas o brinco principal (campo mínimo obrigatório).
- O brinco principal (`TagNumber`) deve ter exatamente 6 dígitos numéricos e ser único dentro da propriedade.
- O brinco de propriedade (`PropertyTagNumber`) é opcional e aceita qualquer texto alfanumérico.
- Peso inicial é opcional; se informado, cria automaticamente o primeiro registro no histórico de pesos.
- Ao cadastrar, o animal recebe `IsActive = true`.
- O produtor deve poder informar a origem do animal (nascido na propriedade ou adquirido).

---

### US-02 — Editar dados de um animal
> **Como** produtor,  
> **quero** editar os dados de um animal já cadastrado,  
> **para** corrigir informações ou atualizá-las ao longo do tempo (ex: classificação muda de Bezerro(a) para Novilha).

**Critérios de aceite:**
- Todos os campos editáveis são opcionais no PATCH (envia apenas o que deseja alterar).
- O `TagNumber` pode ser alterado, desde que continue único dentro da propriedade.
- Classificação e finalidade podem ser alterados a qualquer momento.

---

### US-03 — Listar animais do rebanho
> **Como** produtor,  
> **quero** ver a lista de animais da minha propriedade com filtros,  
> **para** localizar rapidamente um animal específico.

**Critérios de aceite:**
- A lista retorna apenas animais da propriedade do usuário autenticado (multi-tenant).
- Filtros disponíveis: `TagNumber`, nome, classificação, raça, status ativo/inativo.
- Por padrão (`IsActive = null`), retorna todos os animais (ativos e inativos).
- É possível filtrar apenas ativos (`IsActive = true`) ou apenas inativos (`IsActive = false`).
- Cada item da lista exibe: `TagNumber`, nome, classificação, raça, último peso registrado.

---

### US-04 — Visualizar perfil de um animal
> **Como** produtor,  
> **quero** abrir o cadastro completo de um animal,  
> **para** ver todas as suas informações de uma vez.

**Critérios de aceite:**
- Exibe todos os campos do animal.
- Inclui o último registro de peso.
- Inclui o histórico completo de pesos (lista).

---

### US-05 — Inativar (remover do rebanho) um animal
> **Como** produtor,  
> **quero** inativar um animal (por venda, morte, descarte ou transferência),  
> **para** que ele saia do rebanho ativo mas mantenha seu histórico no sistema.

**Critérios de aceite:**
- Inativar seta `IsActive = false`. O animal não aparece nas listagens padrão.
- O cadastro e histórico do animal são preservados.
- Animais inativos podem ser consultados com filtro explícito.
- A exclusão física (DELETE) não é permitida.

---

### US-06 — Consultar valores de enums (autocomplete)
> **Como** frontend,  
> **quero** consultar os valores válidos de raça, classificação, finalidade, origem e sexo,  
> **para** preencher os campos de seleção nos formulários.

**Critérios de aceite:**
- Cada enum tem uma rota própria que retorna `[{ value, label }]`.
- As rotas são protegidas por autenticação.

---

## 4. Casos de Uso

### CU-01 — Cadastrar Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Usuário autenticado e vinculado a uma propriedade.

**Fluxo principal:**
1. Produtor envia `POST /api/animals` com os dados do animal.
2. Sistema valida que `TagNumber` tem exatamente 6 dígitos numéricos.
3. Sistema verifica que não existe outro animal ativo com o mesmo `TagNumber` na propriedade.
4. Sistema verifica consistência entre `Classification` e `Gender` (ver regras de negócio).
5. Sistema cria o animal com `IsActive = true`.
6. Se `InitialWeight` for informado, cria o primeiro `WeightRecord` vinculado ao animal.
7. Retorna `201 Created` com o `AnimalDto` completo.

**Fluxo alternativo — brinco duplicado:**
- Passo 3 falha → Service lança `ConflictException` → middleware retorna `409 Conflict`.

**Fluxo alternativo — classificação e sexo inconsistentes:**
- Passo 4 falha → retorna `422 Unprocessable Entity` com mensagem explicando a inconsistência.

---

### CU-02 — Editar Animal

**Ator:** Produtor autenticado  
**Pré-condição:** Animal existe e pertence à propriedade do usuário.

**Fluxo principal:**
1. Produtor envia `PATCH /api/animals/{id}` com campos a alterar.
2. Sistema carrega o animal e verifica que pertence à propriedade.
3. Sistema aplica apenas os campos presentes no payload (campos ausentes não são alterados).
4. Se `TagNumber` for alterado, valida formato e unicidade.
5. Se `Classification` ou `Gender` forem alterados, valida consistência entre os dois.
6. Retorna `200 OK` com o `AnimalDto` atualizado.

---

### CU-03 — Listar Animais

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/animals` com parâmetros de filtro opcionais.
2. Repositório filtra por `PropertyId` do tenant atual.
3. Aplica filtros adicionais (se presentes).
4. Por padrão, inclui apenas `IsActive = true`.
5. Retorna lista de `AnimalListItemDto`.

**Filtros disponíveis (query params):**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `tagNumber` | string | Busca parcial no brinco principal |
| `name` | string | Busca parcial no nome |
| `classification` | int | Valor do enum `AnimalClassification` |
| `breed` | int | Valor do enum `AnimalBreed` |
| `isActive` | bool? | `true` = só ativos, `false` = só inativos, `null` = todos |

---

### CU-04 — Visualizar Animal

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `GET /api/animals/{id}`.
2. Sistema carrega animal com `WeightRecords`.
3. Valida que pertence à propriedade do tenant.
4. Retorna `200 OK` com `AnimalDto` completo (inclui último peso e histórico de pesos).
5. Se não encontrado, retorna `404 Not Found`.

---

### CU-05 — Inativar Animal

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/animals/{id}`.
2. Sistema carrega o animal e valida o tenant.
3. Sistema seta `IsActive = false` e `UpdatedAt = UtcNow`.
4. Retorna `204 No Content`.
5. Se não encontrado, retorna `404 Not Found`.

> **Nota:** Nunca realizar exclusão física. O método `DeleteAnimalAsync` no repositório deve implementar soft delete.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Alterações na entidade `Animal`

> Localização: `Domain/Models/Animal.cs`

| Campo | Situação | Tipo Antes | Tipo Depois | Notas |
|-------|----------|-----------|------------|-------|
| `Name` | **Alterado** | `string` (required) | `string?` (opcional, max 100) | Nome passa a ser apelido opcional. |
| `TagNumber` | **Alterado** | `string?` (max 50) | `string` (required, max 6) | 6 dígitos numéricos. Obrigatório. Único por propriedade. |
| `Gender` | Mantido | `AnimalGender?` | `AnimalGender?` | Sem mudança. |
| `BirthDate` | Mantido | `DateTime?` | `DateTime?` | Sem mudança. |
| `BreedId` | **Removido** | `int?` (FK → Breed) | — | Substituído por `Breed` enum abaixo. |
| `Breed` (navigation) | **Removido** | `Breed?` (navigation) | — | Substituído por `Breed` enum abaixo. |
| `IsPregnant` | **Removido** | `bool` | — | Estado reprodutivo é derivado dos eventos reprodutivos (Spec #5). |
| `ExpectedBirthDate` | **Removido** | `DateTime?` | — | Idem acima. |
| `Breed` **(novo)** | **Adicionado** | — | `AnimalBreed?` (enum) | Raça como enum. |
| `PropertyTagNumber` **(novo)** | **Adicionado** | — | `string?` (max 100) | Brinco de propriedade. Alfanumérico. Opcional. |
| `Classification` **(novo)** | **Adicionado** | — | `AnimalClassification?` (enum) | Classificação etária/funcional do animal. |
| `Purpose` **(novo)** | **Adicionado** | — | `AnimalPurpose?` (enum) | Finalidade do animal na propriedade. |
| `Origin` **(novo)** | **Adicionado** | — | `AnimalOrigin?` (enum) | Nascido na propriedade ou adquirido. |
| `Notes` **(novo)** | **Adicionado** | — | `string?` (max 1000) | Observações livres. |

**Entidade resultante (`Animal.cs`):**

```csharp
public class Animal : BaseEntity, ITenantEntity
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [Required]
    [MaxLength(6)]
    public string TagNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PropertyTagNumber { get; set; }

    public AnimalGender? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    public AnimalBreed? Breed { get; set; }

    public AnimalClassification? Classification { get; set; }

    public AnimalPurpose? Purpose { get; set; }

    public AnimalOrigin? Origin { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public ICollection<WeightRecord>? WeightRecords { get; set; }
    public ICollection<AnimalVaccination>? AnimalVaccinations { get; set; }
    public ICollection<AnimalMedication>? AnimalMedications { get; set; }
}
```

---

### 5.2 Novos Enums

> Localização: `Domain/Enums/`

#### `AnimalBreed.cs`
```csharp
public enum AnimalBreed
{
    [Description("Holandesa")]
    Holstein = 1,

    [Description("Jersey")]
    Jersey = 2,

    [Description("Híbrida/Mestiça")]
    Crossbred = 3
}
```

#### `AnimalClassification.cs`
```csharp
public enum AnimalClassification
{
    [Description("Bezerro(a)")]
    Calf = 1,

    [Description("Novilha")]
    Heifer = 2,

    [Description("Boi")]
    Steer = 3,

    [Description("Touro")]
    Bull = 4,

    [Description("Vaca")]
    Cow = 5
}
```

#### `AnimalPurpose.cs`
```csharp
public enum AnimalPurpose
{
    [Description("Matriz")]
    Breeder = 1,

    [Description("Novilha de Reposição")]
    ReplacementHeifer = 2,

    [Description("Vaca de Descarte")]
    CullCow = 3,

    [Description("Novilha para Venda")]
    HeiferForSale = 4
}
```

#### `AnimalOrigin.cs`
```csharp
public enum AnimalOrigin
{
    [Description("Nascido na Propriedade")]
    BornOnFarm = 1,

    [Description("Adquirido")]
    Purchased = 2
}
```

> **Nota:** O enum `ReproductiveStatus` será criado no Spec #5 (Eventos Reprodutivos), pois é calculado a partir dos registros de inseminação, gestação e parto — não é persistido na entidade `Animal`.

---

### 5.3 Entidade `Breed` — Descontinuação

A entidade `Breed` (`Domain/Models/Breed.cs`) e sua tabela no banco são descontinuadas.

**Impacto:**
- Remover `Breed.cs` de `Domain/Models/`.
- Remover `BreedRepository.cs` e sua interface.
- Remover o endpoint de CRUD de raças (se existir).
- A coluna `BreedId` em `Animals` será removida; uma nova coluna `Breed` (int, nullable — armazena o valor do enum) será adicionada.
- Migration necessária (aguarda aprovação).

---

### 5.4 DTOs

#### `AnimalCreateDto.cs`

```csharp
public class AnimalCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "O brinco principal é obrigatório.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
    public string TagNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PropertyTagNumber { get; set; }

    [MaxLength(100)]
    public string? Name { get; set; }

    public AnimalGender? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    public AnimalBreed? Breed { get; set; }

    public AnimalClassification? Classification { get; set; }

    public AnimalPurpose? Purpose { get; set; }

    public AnimalOrigin? Origin { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Peso inicial (cria WeightRecord automaticamente)
    public decimal? InitialWeight { get; set; }
    public DateTime? InitialWeightDate { get; set; }

    [MaxLength(500)]
    public string? InitialWeightObservations { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Classification.HasValue && Gender.HasValue)
        {
            var femaleOnly = new[] { AnimalClassification.Heifer, AnimalClassification.Cow };
            var maleOnly = new[] { AnimalClassification.Steer, AnimalClassification.Bull };
            // Calf é neutro — o Gender já distingue Bezerra de Bezerro

            if (femaleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.F)
                yield return new ValidationResult(
                    $"A classificação '{Classification}' é exclusiva de fêmeas.",
                    new[] { nameof(Classification) });

            if (maleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.M)
                yield return new ValidationResult(
                    $"A classificação '{Classification}' é exclusiva de machos.",
                    new[] { nameof(Classification) });
        }
    }
}
```

---

#### `AnimalUpdateDto.cs`

```csharp
public class AnimalUpdateDto : IValidatableObject
{
    [RegularExpression(@"^\d{6}$", ErrorMessage = "O brinco principal deve ter exatamente 6 dígitos numéricos.")]
    public string? TagNumber { get; set; }

    [MaxLength(100)]
    public string? PropertyTagNumber { get; set; }

    [MaxLength(100)]
    public string? Name { get; set; }

    public AnimalGender? Gender { get; set; }

    public DateTime? BirthDate { get; set; }

    public AnimalBreed? Breed { get; set; }

    public AnimalClassification? Classification { get; set; }

    public AnimalPurpose? Purpose { get; set; }

    public AnimalOrigin? Origin { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Classification.HasValue && Gender.HasValue)
        {
            var femaleOnly = new[] { AnimalClassification.Heifer, AnimalClassification.Cow };
            var maleOnly = new[] { AnimalClassification.Steer, AnimalClassification.Bull };
            // Calf é neutro — o Gender já distingue Bezerra de Bezerro

            if (femaleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.F)
                yield return new ValidationResult(
                    $"A classificação '{Classification}' é exclusiva de fêmeas.",
                    new[] { nameof(Classification) });

            if (maleOnly.Contains(Classification.Value) && Gender.Value != AnimalGender.M)
                yield return new ValidationResult(
                    $"A classificação '{Classification}' é exclusiva de machos.",
                    new[] { nameof(Classification) });
        }
    }
}
```

---

#### `AnimalDto.cs` (response — detalhe)

```csharp
public class AnimalDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? PropertyTagNumber { get; set; }
    public AnimalGender? Gender { get; set; }
    public string? GenderLabel { get; set; }
    public DateTime? BirthDate { get; set; }
    public AnimalBreed? Breed { get; set; }
    public string? BreedLabel { get; set; }
    public AnimalClassification? Classification { get; set; }
    public string? ClassificationLabel { get; set; }
    public AnimalPurpose? Purpose { get; set; }
    public string? PurposeLabel { get; set; }
    public AnimalOrigin? Origin { get; set; }
    public string? OriginLabel { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public WeightRecordDto? LastWeightRecord { get; set; }
    public IEnumerable<WeightRecordDto>? WeightRecords { get; set; }
}
```

> **Nota:** Os campos `*Label` retornam o texto legível do enum via atributo `[Description]`.

---

#### `AnimalListItemDto.cs` (response — listagem, mais leve)

```csharp
public class AnimalListItemDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? PropertyTagNumber { get; set; }
    public AnimalClassification? Classification { get; set; }
    public string? ClassificationLabel { get; set; }
    public AnimalBreed? Breed { get; set; }
    public string? BreedLabel { get; set; }
    public bool IsActive { get; set; }
    public WeightRecordDto? LastWeightRecord { get; set; }
}
```

---

#### `AnimalFilterDto.cs` (query params da listagem)

```csharp
public class AnimalFilterDto
{
    public string? TagNumber { get; set; }
    public string? Name { get; set; }
    public AnimalClassification? Classification { get; set; }
    public AnimalBreed? Breed { get; set; }
    public bool? IsActive { get; set; } // null = todos, true = só ativos, false = só inativos
}
```

---

### 5.5 Endpoints da API

> Base: `api/animals` | Auth: Bearer Token obrigatório em todos os endpoints.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/animals` | Lista animais com filtros | `200 AnimalListItemDto[]` |
| `GET` | `/api/animals/{id}` | Detalhe de um animal | `200 AnimalDto` / `404` |
| `POST` | `/api/animals` | Cadastrar animal | `201 AnimalDto` / `409` / `422` |
| `PATCH` | `/api/animals/{id}` | Editar animal | `200 AnimalDto` / `404` / `409` / `422` |
| `DELETE` | `/api/animals/{id}` | Inativar animal (soft delete) | `204` / `404` |
| `GET` | `/api/animals/breeds` | Enum de raças | `200 [{value, label}]` |
| `GET` | `/api/animals/classifications` | Enum de classificações | `200 [{value, label}]` |
| `GET` | `/api/animals/purposes` | Enum de finalidades | `200 [{value, label}]` |
| `GET` | `/api/animals/origins` | Enum de origens | `200 [{value, label}]` |
| `GET` | `/api/animals/genders` | Enum de sexo (já existe) | `200 [{value, label}]` |

---

### 5.6 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `TagNumber` deve ter exatamente 6 dígitos numéricos. | DTO (DataAnnotations) |
| RN-02 | `TagNumber` deve ser único dentro da propriedade (tenant). | Service → lança `ConflictException` |
| RN-03 | Classificações femininas (`Heifer`, `Cow`) exigem `Gender = F`. | DTO (IValidatableObject) |
| RN-04 | Classificações masculinas (`Steer`, `Bull`) exigem `Gender = M`. | DTO (IValidatableObject) |
| RN-05 | `Calf` é neutro — não há restrição de `Gender` para essa classificação. | DTO (IValidatableObject) |
| RN-06 | Inativação é sempre soft delete (`IsActive = false`). Nunca exclusão física. | Repository |
| RN-07 | Listagem sem filtro `IsActive` retorna todos os animais. `IsActive = true` filtra só ativos; `IsActive = false` filtra só inativos. | Repository |

---

### 5.7 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `Animal.cs` | Alterar campos conforme §5.1 |
| `Domain/Models` | `Breed.cs` | Remover (descontinuado) |
| `Domain/Enums` | `AnimalBreed.cs` | Criar |
| `Domain/Enums` | `AnimalClassification.cs` | Criar |
| `Domain/Enums` | `AnimalPurpose.cs` | Criar |
| `Domain/Enums` | `AnimalOrigin.cs` | Criar |
| `Application/DTOs` | `AnimalCreateDto.cs` | Reescrever |
| `Application/DTOs` | `AnimalUpdateDto.cs` | Reescrever |
| `Application/DTOs` | `AnimalDto.cs` | Reescrever |
| `Application/DTOs` | `AnimalListItemDto.cs` | Criar |
| `Application/DTOs` | `AnimalFilterDto.cs` | Criar |
| `Application/Mappings` | `AnimalProfile.cs` | Atualizar mapeamentos |
| `Application/Services` | `AnimalService.cs` | Adicionar validação de `TagNumber` único; ajustar `GetAll` para aceitar filtros |
| `Application/Interfaces` | `IAnimalService.cs` | Atualizar assinaturas |
| `Application/Interfaces` | `IAnimalRepository.cs` | Atualizar assinatura do `GetAll` para receber filtros |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | Implementar filtros; garantir soft delete |
| `Api/Controllers` | `AnimalsController.cs` | Adicionar rotas de enums; atualizar `GetAll` para aceitar `[FromQuery] AnimalFilterDto` |
| `Infrastructure/Migrations` | *(nova migration)* | **Requer aprovação antes de criar** |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

A migration deverá:
1. Alterar coluna `TagNumber` em `Animals`: tornar obrigatória e reduzir `MaxLength` para 6.
2. Adicionar coluna `PropertyTagNumber` (nvarchar(100), nullable).
3. Adicionar coluna `Breed` (int, nullable) — armazena o valor do enum `AnimalBreed`.
4. Remover FK `BreedId` e coluna `BreedId` de `Animals`.
5. Adicionar coluna `Classification` (int, nullable).
6. Adicionar coluna `Purpose` (int, nullable).
7. Adicionar coluna `Origin` (int, nullable).
8. Adicionar coluna `Notes` (nvarchar(1000), nullable).
9. Tornar coluna `Name` nullable.
10. Remover colunas `IsPregnant` e `ExpectedBirthDate` de `Animals`.
11. (Opcional) Dropar tabela `Breeds` após confirmar que não há dados em uso.

---

## 7. Fora do Escopo deste Spec

Os itens abaixo são mencionados na ata mas pertencem a specs separados:

- **Entrada e Saída de animais** → Spec #2
- **Escore de Condição Corporal (ECC)** → Spec #3
- **Banco de Sêmen** → Spec #4
- **Eventos Reprodutivos, Gestação e Parto (inclui `ReproductiveStatus`)** → Spec #5 e #6
- **Dashboards de índices zootécnicos** → Spec #7
