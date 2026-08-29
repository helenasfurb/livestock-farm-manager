# Spec 9: Brinco Opcional e Cadastro de Cria como Animal ao Nascimento

**Módulo:** Animais + Gestação e Parto — Nascimento
**Versão:** 1.1
**Data:** 29/Ago/2026
**Fonte:** Reunião de definição (brinco opcional / nascimento de cria); estado atual do sistema
**Status:** Aprovado para implementação
**Histórico:** v1.1 — adiciona a edição de cria já registrada (§8), com sincronização de sexo cria ↔ Animal e de peso cria → registro de peso do bezerro.
**Depende de:** Spec #1 (Animais), Spec 6.2 (Parto), Spec 6.3 (Cria)
**Fecha:** Spec 6.3 §7 e D3 — "Vinculação de cria cadastrada como Animal" (antes fora de escopo)
**Ver também:** Spec 6.1 (Gestação), Spec #3 (ECC)

---

## 1. Contexto e Objetivo

Hoje o brinco (`Animal.TagNumber`) é obrigatório para **todo** animal, inclusive no nível da entidade de domínio. Isso impede registrar um animal recém-nascido, que ainda não recebeu brinco no momento do parto.

Este spec faz três mudanças complementares:

1. **Brinco deixa de ser obrigatório no domínio.** `Animal.TagNumber` passa a ser nulável. A obrigatoriedade permanece **apenas na rota de cadastro manual** (`POST /api/animals`): não é possível cadastrar manualmente um animal sem brinco.

2. **Cria viva vira Animal automaticamente.** Ao registrar um parto (`POST /api/pregnancies/{id}/calvings` — Spec 6.2), para cada cria com `VitalStatus = Live`, o sistema cadastra automaticamente um novo `Animal` na base, como **bezerro nascido na propriedade**, sem brinco. Como o animal ainda não tem brinco, o **nome passa a ser obrigatório** para cada cria viva, servindo como identificador temporário. Natimortos (`Stillborn`) **não** geram `Animal`.

3. **Cria já registrada pode ser editada** (§8 — adendo v1.1). Depois de registrada, uma cria (viva ou natimorta) pode ter **sexo**, **observações** e **peso** corrigidos. Como cria viva e `Animal` compartilham o mesmo sexo (via FK `AnimalId`), a edição do sexo é **sincronizada** nos dois sentidos (cria ↔ Animal). O peso da cria, ao ser editado, **reflete no registro de peso de nascimento** do bezerro (não há FK, mas há regra de negócio que mantém os valores coerentes).

Isso encerra o trabalho futuro previsto no Spec 6.3 (§7 e decisão D3), que deixou a vinculação `AnimalCalvingCalf → Animal` para depois.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | `Animal.TagNumber` passa a ser nulável no domínio e no banco. | Recém-nascidos não têm brinco no momento do parto. |
| D2 | Brinco continua **obrigatório** no cadastro manual (`AnimalCreateDto`), com o mesmo formato de 6 dígitos. | Cadastro manual pressupõe um animal já identificado; regra de negócio inalterada nesse fluxo. |
| D3 | Cada cria com `VitalStatus = Live` gera automaticamente um `Animal`; natimortos não. | Só faz sentido cadastrar na base animais que continuarão vivos na propriedade. |
| D4 | `Name` passa a ser **obrigatório por cria viva** no DTO do parto. | Sem brinco, o nome é o único identificador humano da cria recém-nascida. |
| D5 | O `Animal`-bezerro criado recebe: `Classification = Calf`, `Origin = BornOnFarm`, `BirthDate = CalvingDate`, `Gender = Sex` da cria, `Name` da cria, `TagNumber = null`. | Regras fixas do nascimento; demais campos ficam a cargo de edição futura. |
| D6 | `Breed` e `Purpose` do bezerro ficam **nulos** no nascimento. | Produtor completa depois via `PATCH /api/animals/{id}` quando o animal receber o brinco. |
| D7 | Se `WeightKg` da cria for informado, cria um `WeightRecord` no `Animal` (`RecordedAt = CalvingDate`, `Observations = Notes` da cria). Peso é opcional. | Aproveita o peso já capturado no parto sem introduzir campos novos. |
| D8 | `AnimalCalvingCalf` ganha FK opcional `AnimalId` apontando para o `Animal` gerado. | Rastreabilidade cria → animal; nulo para natimortos. Fecha Spec 6.3 §7. |
| D9 | Ao inativar o parto, os `Animal`-bezerros gerados são inativados junto (soft delete). | Consistência com a inativação em cascata das crias (Spec 6.2 CU-02 / Spec 6.3 CU-02). |
| D10 | A unicidade de brinco (`TagNumberExistsAsync`) passa a ignorar brincos nulos. | Vários bezerros sem brinco coexistem; unicidade só se aplica a brincos preenchidos. |

---

## 3. Histórias de Usuário

### US-01 — Cadastro manual continua exigindo brinco
> **Como** produtor,
> **quero** que o cadastro manual de um animal continue exigindo o brinco,
> **para** garantir que todo animal cadastrado manualmente esteja identificado.

**Critérios de aceite:**
- `POST /api/animals` sem `TagNumber` retorna `400 Bad Request` (validação de DTO).
- O formato do brinco permanece 6 dígitos numéricos.
- Brinco duplicado (entre animais que possuem brinco) continua retornando `409 Conflict`.

---

### US-02 — Registrar cria viva já cadastra o bezerro
> **Como** produtor,
> **quero** que, ao registrar um parto com cria viva, o bezerro seja cadastrado automaticamente na base,
> **para** não precisar recadastrar o animal manualmente depois.

**Critérios de aceite:**
- Ao registrar o parto, informo, para cada cria viva, um **nome obrigatório** e o sexo (já obrigatório).
- Para cada cria `Live`, o sistema cria um `Animal` com classificação Bezerro(a), origem Nascido na propriedade, data de nascimento igual à data do parto, sexo igual ao da cria e sem brinco.
- Se eu informar o peso da cria, o bezerro nasce com um registro de peso (na data do parto), usando as observações da cria.
- Crias natimortas **não** geram animal e **não** exigem nome.
- O bezerro cadastrado aparece normalmente na listagem/detalhe de animais e pode receber o brinco depois via edição.

---

### US-03 — Completar o brinco do bezerro depois
> **Como** produtor,
> **quero** editar o bezerro para informar o brinco quando ele for aplicado,
> **para** identificar definitivamente o animal.

**Critérios de aceite:**
- `PATCH /api/animals/{id}` aceita `TagNumber` (6 dígitos), validando unicidade.
- Enquanto o brinco não é informado, o animal permanece sem brinco, identificado pelo nome.

---

## 4. Casos de Uso

### CU-01 — Cadastro Manual de Animal (fluxo existente, brinco obrigatório)

**Ator:** Produtor autenticado
**Pré-condição:** Usuário autenticado com propriedade (tenant).

**Fluxo principal:**
1. Produtor envia `POST /api/animals` com `AnimalCreateDto`.
2. DTO valida `TagNumber` obrigatório e no formato de 6 dígitos — se ausente/ inválido, `400`.
3. Serviço valida unicidade do brinco — se duplicado, `ConflictException` → `409`.
4. Cria o `Animal` e retorna `201 Created`.

> Inalterado em relação ao comportamento atual — apenas reforçado por este spec.

---

### CU-02 — Registrar Parto com Cadastro Automático das Crias Vivas

**Ator:** Produtor autenticado
**Pré-condição:** `AnimalPregnancy` existe, pertence ao tenant e tem `Status = Confirmed`.

**Fluxo principal:**
1. Produtor envia `POST /api/pregnancies/{pregnancyId}/calvings` com `AnimalCalvingCreateDto` (cada cria agora inclui `Name` quando viva).
2. Validações do Spec 6.2 (status, parto único, datas) são aplicadas normalmente.
3. Para cada cria com `VitalStatus = Live`, o DTO exige `Name` — se ausente/vazio, `400`.
4. Sistema cria a `AnimalCalving` (Spec 6.2).
5. Para cada cria **viva**, o sistema:
   1. Cria um `Animal` com:
      - `Name` = `Calf.Name`
      - `Gender` = `Calf.Sex`
      - `Classification` = `Calf`
      - `Origin` = `BornOnFarm`
      - `BirthDate` = `AnimalCalving.CalvingDate`
      - `TagNumber` = `null`, `Breed` = `null`, `Purpose` = `null`
      - `PropertyId` = `pregnancy.PropertyId`, `IsActive = true`
   2. Se `Calf.WeightKg` informado, adiciona um `WeightRecord` (`Weight = WeightKg`, `RecordedAt = CalvingDate`, `Observations = Calf.Notes`).
   3. Persiste o `Animal` e grava o `Id` gerado em `AnimalCalvingCalf.AnimalId`.
6. Para cada cria **natimorta**, `AnimalId` permanece nulo e nenhum `Animal` é criado.
7. Sistema persiste as crias (com `AnimalId` preenchido nas vivas) junto ao parto.
8. Atualiza `AnimalPregnancy.Status = Calved` (Spec 6.2) e, se houver ECC, cria `BodyConditionRecord`.
9. Retorna `201 Created` com `AnimalCalvingDto`.

**Fluxo alternativo — cria viva sem nome:** Passo 3 → `400 Bad Request`.
**Demais fluxos alternativos:** herdados do Spec 6.2 (`404` / `409` / `422`).

---

### CU-03 — Inativar Parto com Inativação em Cascata dos Bezerros

**Ator:** Produtor autenticado

**Fluxo principal:**
1. Produtor envia `DELETE /api/calvings/{calvingId}`.
2. Sistema seta `AnimalCalving.IsActive = false` e todas as `AnimalCalvingCalf` como inativas (Spec 6.2).
3. Para cada cria com `AnimalId != null`, o sistema carrega o `Animal` correspondente e seta `IsActive = false`.
4. `AnimalPregnancy.Status` reverte para `Confirmed` (Spec 6.2).
5. Retorna `204 No Content`.

> Reativação do parto **não** é escopo deste spec; se implementada no futuro, deve reativar os bezerros vinculados de forma simétrica.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Alterações em entidades existentes

#### `Animal.cs` — brinco nulável

```csharp
// Antes:
[Required]
[MaxLength(6)]
public string TagNumber { get; set; } = string.Empty;

// Depois:
[MaxLength(6)]
public string? TagNumber { get; set; }
```

> A obrigatoriedade sai do domínio e passa a existir **somente** no `AnimalCreateDto`.

#### `AnimalCalvingCalf.cs` — vínculo com o Animal gerado

| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `AnimalId` | `int?` | Não | FK → `Animal`. Preenchido para crias vivas; nulo para natimortas. |
| `Animal` | navigation | — | Animal cadastrado a partir da cria. |

```csharp
public class AnimalCalvingCalf : BaseEntity, ITenantEntity
{
    public int CalvingId { get; set; }

    public int? AnimalId { get; set; }          // novo

    public AnimalGender Sex { get; set; }

    [Range(0.01, 999.99)]
    public decimal? WeightKg { get; set; }

    public CalfVitalStatus VitalStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public AnimalCalving? Calving { get; set; }
    public Animal? Animal { get; set; }          // novo
}
```

---

### 5.2 Enums

Nenhum enum novo. São reutilizados:
- `AnimalGender` (`M` / `F`) — sexo da cria e do bezerro.
- `AnimalClassification.Calf` — classificação fixa do bezerro.
- `AnimalOrigin.BornOnFarm` — origem fixa do bezerro.
- `CalfVitalStatus` (`Live` / `Stillborn`) — decide se há cadastro de animal.

---

### 5.3 DTOs

#### `AnimalCreateDto.cs` — **inalterado**
`TagNumber` permanece `[Required]` com regex de 6 dígitos. Nenhuma mudança.

#### `AnimalCalvingCalfCreateDto.cs` — adicionar `Name` com obrigatoriedade condicional

```csharp
public class AnimalCalvingCalfCreateDto : IValidatableObject
{
    [MaxLength(100)]
    public string? Name { get; set; }          // obrigatório quando VitalStatus == Live

    [Required(ErrorMessage = "O sexo da cria é obrigatório.")]
    public AnimalGender Sex { get; set; }

    [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
    public decimal? WeightKg { get; set; }

    [Required(ErrorMessage = "O status vital da cria é obrigatório.")]
    public CalfVitalStatus VitalStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (VitalStatus == CalfVitalStatus.Live && string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult(
                "O nome é obrigatório para crias nascidas vivas.",
                new[] { nameof(Name) });
    }
}
```

> `AnimalCalvingCreateDto` permanece como no Spec 6.2 (a validação por-item é feita no DTO da cria acima).

#### `AnimalCalvingCalfDto.cs` (response) — expor o vínculo

```csharp
public class AnimalCalvingCalfDto
{
    public int Id { get; set; }
    public int? AnimalId { get; set; }          // novo — animal gerado (null p/ natimorto)
    public string? Name { get; set; }           // novo
    public EnumValueDto? Sex { get; set; }
    public decimal? WeightKg { get; set; }
    public EnumValueDto? VitalStatus { get; set; }
    public string? Notes { get; set; }
}
```

---

### 5.4 Endpoints da API

> Auth: Bearer Token obrigatório. **Nenhum endpoint novo.** O comportamento é adicionado aos endpoints existentes.

| Método | Rota | Mudança | Retorno |
|--------|------|---------|---------|
| `POST` | `/api/animals` | Brinco continua obrigatório (comportamento reforçado, sem mudança de contrato) | `201` / `400` / `409` |
| `POST` | `/api/pregnancies/{id}/calvings` | Passa a exigir `Name` por cria viva e a cadastrar o `Animal` de cada cria viva | `201 AnimalCalvingDto` / `400` / `404` / `409` / `422` |
| `DELETE` | `/api/calvings/{id}` | Passa a inativar em cascata os bezerros gerados | `204` / `404` |

---

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `Animal.TagNumber` é nulável no domínio. | Entidade `Animal` |
| RN-02 | No cadastro manual, `TagNumber` é obrigatório e deve ter 6 dígitos numéricos. | `AnimalCreateDto` (DataAnnotations) |
| RN-03 | Unicidade de brinco só se aplica a brincos preenchidos (ignora nulos). | `AnimalRepository.TagNumberExistsAsync` |
| RN-04 | `Name` é obrigatório para cada cria com `VitalStatus = Live`. | `AnimalCalvingCalfCreateDto` (IValidatableObject) |
| RN-05 | Para cada cria viva, criar um `Animal` (`Calf`, `BornOnFarm`, `BirthDate = CalvingDate`, `Gender = Sex`, `Name`, `TagNumber = null`). | `AnimalCalvingService.CreateAsync` |
| RN-06 | Crias natimortas não geram `Animal`; `AnimalId` fica nulo. | `AnimalCalvingService.CreateAsync` |
| RN-07 | Se `WeightKg` informado, criar `WeightRecord` no bezerro (`RecordedAt = CalvingDate`, `Observations = Calf.Notes`). | `AnimalCalvingService.CreateAsync` |
| RN-08 | Gravar o `Id` do `Animal` gerado em `AnimalCalvingCalf.AnimalId`. | `AnimalCalvingService.CreateAsync` |
| RN-09 | Ao inativar o parto, inativar (soft delete) todos os `Animal` com `AnimalId` vinculado às crias. | `AnimalCalvingService.InactivateAsync` |
| RN-10 | O `Animal`-bezerro é criado no mesmo `PropertyId` da gestação/parto (isolamento de tenant). | `AnimalCalvingService.CreateAsync` |
| RN-11 | Isolamento de tenant: repositórios filtram por `PropertyId`. | Repositories |

---

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `Animal.cs` | Tornar `TagNumber` nulável (remover `[Required]`, tipo `string?`) |
| `Domain/Models` | `AnimalCalvingCalf.cs` | Adicionar `int? AnimalId` + navigation `Animal` |
| `Application/DTOs` | `AnimalCreateDto.cs` | **Sem mudança** (brinco continua obrigatório) |
| `Application/DTOs` | `AnimalCalvingCalfCreateDto.cs` | Adicionar `Name`; implementar `IValidatableObject` (nome obrigatório se `Live`) |
| `Application/DTOs` | `AnimalCalvingCalfDto.cs` | Adicionar `AnimalId` e `Name` |
| `Application/Mappings` | `AnimalCalvingProfile.cs` | Mapear `Name`/`AnimalId` no `AnimalCalvingCalf ↔ DTO`; ignorar `Animal`/`AnimalId` no map de create |
| `Application/Services` | `AnimalCalvingService.cs` | Criar `Animal` (+ `WeightRecord`) por cria viva em `CreateAsync` pela navegação `AnimalCalvingCalf.Animal` (persistido no grafo do parto); inativar bezerros em cascata em `InactivateAsync` sobre entidades rastreadas. Sem nova dependência de repositório |
| `Application/Interfaces` | `IAnimalRepository.cs` | Sem mudança para o fluxo de parto (o bezerro é escrito via agregado de parto) |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | `TagNumberExistsAsync` ignora nulos (naturalmente, pois só é chamado com brinco informado) |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Configurar FK `AnimalCalvingCalf.AnimalId → Animal` (opcional, `ON DELETE RESTRICT`); ajustar `TagNumber` para nulável |
| `Infrastructure/Migrations` | *(ver seção 6)* | **Requer aprovação antes de criar** |

> **Nota de layering:** `AnimalCalvingService` passa a depender de `IAnimalRepository` para criar/inativar o bezerro. Isso mantém a regra do CLAUDE.md (serviço depende de interfaces de repositório, nunca de repositórios concretos nem de outro serviço). A criação do `WeightRecord` do bezerro é montada como coleção do próprio `Animal` (mesmo padrão de `AnimalService.CreateAnimalAsync`), persistida junto ao animal.

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

**Alterar tabela `Animals`:**

| Coluna | Mudança |
|--------|---------|
| `TagNumber` | `nvarchar(6) NOT NULL` → `nvarchar(6) NULL` |

> Não existe índice único em `TagNumber` (unicidade é garantida em aplicação via `TagNumberExistsAsync`), portanto não há índice a ajustar. Caso um índice único venha a ser adicionado no futuro, deve ser filtrado (`WHERE TagNumber IS NOT NULL`).

**Alterar tabela `AnimalCalvingCalves`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `AnimalId` | int | **nullable**, FK → `Animals(Id)`, `ON DELETE RESTRICT` |

**Índices:**

| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(AnimalId)` | Simples | Otimiza a navegação cria → animal e a inativação em cascata. |

> Migração sugerida: `Spec9_OptionalTagAndCalfAnimalLink`.

---

## 7. Fora do Escopo deste Spec

- **Reativação de parto** — não há reativação de parto no sistema; se criada, deverá reativar os bezerros vinculados simetricamente.
- **Preenchimento posterior de brinco, raça e finalidade do bezerro** — feito via `PATCH /api/animals/{id}` (Spec #1), sem regra nova.
- **Modelagem base da cria (campos, criação no parto)** → Spec 6.3. A **edição** de sexo/peso/observações de uma cria já registrada é tratada no §8 deste spec.
- **Alteração de `VitalStatus` de uma cria já registrada** (Live ↔ Stillborn) — **fora de escopo por decisão (29/Ago/2026):** por ora não se altera o status vital de uma cria. §8 mantém `VitalStatus` imutável (D11/RN-16).
  - *Consideração futura registrada:* existe a convenção zootécnica de que um recém-nascido pode ser **considerado natimorto até 24h após o parto**. Se essa reclassificação (Live → Stillborn dentro de 24h) vier a ser implementada, o `Animal`-bezerro gerado deverá ser inativado. Fica em aberto o **motivo** da inativação — as opções levantadas foram: (a) *soft-delete* sem `AnimalExitRecord` (como a cascata do parto, D9); (b) saída com `NaturalDeath`; (c) novo motivo "Morte perinatal". A decisão do motivo depende de como natimortalidade e mortalidade serão separadas nos índices (Spec #7) e fica para spec futuro.
- **Registro do parto (validações de data/status)** → Spec 6.2.
- **Gestação** → Spec 6.1.
- **Cadastro/edição geral de animais e entrada/saída** → Spec #1 e Spec #2.
- **Dashboards de índices zootécnicos** → Spec #7.

---

## 8. Adendo (v1.1) — Edição de Cria Registrada

Complementa este spec permitindo **corrigir uma cria já registrada** sem refazer o parto. Vale para cria **viva** e **natimorta**. Os campos editáveis são **sexo**, **observações** e **peso**. `VitalStatus` **não** é editável (ver §7).

### 8.1 Contexto e Objetivo

Após registrar o parto, o produtor pode ter informado um dado errado de uma cria (ex.: sexo trocado, peso digitado errado, observação incompleta). Este adendo permite editar esses campos individualmente.

A particularidade está na **coerência com o `Animal` gerado** (§2 D8): para cria **viva**, existe um `Animal` vinculado por `AnimalCalvingCalf.AnimalId`. Sexo e peso da cria têm reflexo no bezerro:

- **Sexo** é o mesmo dado nas duas entidades (a cria e o `Animal` compartilham conceitualmente o sexo). Editar em um lado **sincroniza** no outro — em **ambos os sentidos**.
- **Peso** não é o mesmo campo: a cria tem `WeightKg` e o bezerro tem uma coleção de `WeightRecord`. Mas o registro de peso de nascimento do bezerro **nasceu** do `WeightKg` da cria (§2 D7). Por isso, editar o peso da cria **atualiza** esse registro de peso de nascimento. A relação é por **regra de negócio**, não por FK.

Para cria **natimorta** não há `Animal`; a edição altera apenas a linha da cria.

### 8.2 Decisões Registradas (adendo)

| # | Decisão | Motivo |
|---|---------|--------|
| D11 | A edição de cria expõe **apenas** `Sex`, `Notes` e `WeightKg`. `VitalStatus`, `CalvingId`, `AnimalId` e `PropertyId` são imutáveis pela edição. | Correção de dados sem reescrever o vínculo nem o ciclo de vida. Trocar `VitalStatus` está fora de escopo (§7). |
| D12 | Editar `Sex` da cria **viva** sincroniza `Animal.Gender` do bezerro vinculado (`AnimalId`). | Cria e bezerro representam o mesmo animal; sexo deve permanecer coerente. |
| D13 | Editar `Gender` do `Animal`-bezerro (via `PATCH /api/animals/{id}`) **sincroniza de volta** o `Sex` da cria vinculada. | Sincronização **bidirecional** (D12 + D13): qualquer lado que mude o sexo mantém o outro coerente. |
| D14 | Editar `WeightKg` da cria **viva** com um valor: atualiza o registro de peso de nascimento do bezerro (o `WeightRecord` com `RecordedAt = CalvingDate`); se não existia, é **criado**. Seguindo o padrão de PATCH do projeto, `WeightKg = null` significa **campo não enviado** → **não altera nada** (nem a cria, nem o bezerro). Ou seja, o histórico de peso do bezerro **nunca** é removido por uma edição de cria. | Mantém o peso de nascimento coerente quando informado (§2 D7); o histórico de peso é dado próprio do bezerro e não deve ser apagado por edição de cria. Consistência com `AnimalUpdateDto` (null = não altera). O produtor ajusta pelo próprio animal se necessário. |
| D15 | Editar peso/sexo **pelo lado do `Animal`** não é obrigado a refletir em outros campos da cria além do sexo. Em especial, editar diretamente um `WeightRecord` do bezerro **não** altera `WeightKg` da cria (não há FK; o vínculo peso é unidirecional cria → registro). | O produtor pode registrar pesagens posteriores no bezerro sem “contaminar” o dado histórico do parto. |
| D16 | A edição é **individual por cria**, identificada pelo seu `Id`, e só afeta cria **ativa** de parto **ativo**. | Consistência com o ciclo de vida do parto (Spec 6.3 D2); cria de parto inativo não é editável. |

### 8.3 Histórias de Usuário (adendo)

#### US-04 — Corrigir dados de uma cria registrada
> **Como** produtor,
> **quero** editar o sexo, as observações e o peso de uma cria já registrada,
> **para** corrigir dados informados errado no momento do parto.

**Critérios de aceite:**
- Posso editar **sexo**, **observações** e **peso** de qualquer cria (viva ou natimorta) de um parto ativo.
- Ao corrigir o **sexo** de uma cria **viva**, o sexo do bezerro cadastrado é atualizado automaticamente.
- Ao corrigir o **sexo do bezerro** pela edição do animal, o sexo da cria vinculada é atualizado automaticamente.
- Ao corrigir o **peso** de uma cria **viva**, o registro de peso de nascimento do bezerro é atualizado (ou criado, se não existia).
- Não consigo alterar o **status vital** da cria por esta operação.
- Editar uma cria de parto inativo (ou uma cria inativa) retorna erro.

### 8.4 Casos de Uso (adendo)

#### CU-04 — Editar Cria

**Ator:** Produtor autenticado
**Pré-condição:** A cria existe, pertence ao tenant, está ativa e seu parto está ativo.

**Fluxo principal:**
1. Produtor envia `PATCH /api/calvings/{calvingId}/calves/{calfId}` com `AnimalCalvingCalfUpdateDto` (todos os campos opcionais).
2. Sistema carrega a cria por `calfId` dentro do parto `calvingId` — se não existir, `404`.
3. Se a cria ou o parto estiver inativo, `409 Conflict`.
4. Para cada campo informado no DTO, o sistema aplica:
   1. `Notes` → atualiza `AnimalCalvingCalf.Notes`.
   2. `Sex` → atualiza `AnimalCalvingCalf.Sex`; **se** `AnimalId != null`, carrega o `Animal` e seta `Animal.Gender = Sex` (RN-13).
   3. `WeightKg` **informado (não null)** → atualiza `AnimalCalvingCalf.WeightKg` e, **se** `AnimalId != null`, ajusta o `WeightRecord` de nascimento do bezerro (RN-15):
      - existe registro de nascimento (`RecordedAt = CalvingDate`) → **atualiza** `Weight`;
      - não existe → **cria** `WeightRecord` (`Weight = WeightKg`, `RecordedAt = CalvingDate`, `Observations = Notes` atual da cria);
      - `WeightKg = null` (campo ausente) → **não altera nada** (nem cria, nem bezerro).
5. Persiste cria e, quando aplicável, o `Animal` na mesma transação.
6. Retorna `200 OK` com `AnimalCalvingCalfDto` atualizado.

**Fluxos alternativos:**
- Cria/parto inexistente → `404 Not Found`.
- Cria ou parto inativo → `409 Conflict`.
- Peso fora de faixa (`0,01–999,99`) → `400 Bad Request` (validação de DTO).

#### CU-05 — Sincronizar Sexo pela Edição do Animal (bidirecional)

**Ator:** Produtor autenticado (dentro de `AnimalService.UpdateAnimalAsync`)
**Pré-condição:** `Animal` existe e pertence ao tenant.

**Fluxo:**
1. Produtor envia `PATCH /api/animals/{id}` com `Gender` diferente do atual.
2. Sistema atualiza `Animal.Gender`.
3. Sistema busca a cria com `AnimalCalvingCalf.AnimalId = id` **e ativa**.
4. Se existir, seta `AnimalCalvingCalf.Sex = Animal.Gender` e persiste (RN-14).
5. Retorna `200 OK` com o animal atualizado (fluxo do Spec #1, apenas acrescido do passo 3–4).

### 8.5 DTOs (adendo)

#### `AnimalCalvingCalfUpdateDto.cs` — **criar**

Padrão de PATCH: todos os campos anuláveis/opcionais; só o que vier é aplicado.

```csharp
public class AnimalCalvingCalfUpdateDto
{
    public AnimalGender? Sex { get; set; }

    [Range(0.01, 999.99, ErrorMessage = "O peso deve ser entre 0,01 e 999,99 kg.")]
    public decimal? WeightKg { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
```

> `Name`, `CalvingId` e `AnimalId` **não** aparecem no DTO de edição (D11). `VitalStatus` é imutável (§7).
> **Semântica de `null` (padrão do projeto):** cada campo nulo é tratado como **não enviado** e **não altera** o valor atual — igual ao `AnimalUpdateDto`, que aplica só membros não nulos. Não há remoção de peso por esta rota; para zerar/ajustar peso o produtor usa o próprio animal. Isso elimina a necessidade de sentinela/JsonPatch.

#### `AnimalUpdateDto.cs` — **sem novo campo**
`Gender` já existe/permanece; apenas o **serviço** ganha o passo de sincronização (CU-05). Nenhuma mudança de contrato.

### 8.6 Endpoints da API (adendo)

> Auth: Bearer Token obrigatório.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `PATCH` | `/api/calvings/{calvingId}/calves/{calfId}` | Edita sexo/observações/peso de uma cria; sincroniza bezerro quando aplicável | `200 AnimalCalvingCalfDto` / `400` / `404` / `409` |
| `PATCH` | `/api/animals/{id}` | **Comportamento acrescido:** ao mudar `Gender`, sincroniza o `Sex` da cria vinculada | `200 AnimalDto` / `400` / `404` / `409` |

### 8.7 Regras de Negócio (adendo)

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-12 | Só é editável cria **ativa** de parto **ativo**; caso contrário `ConflictException` → `409`. | `AnimalCalvingService.UpdateCalfAsync` |
| RN-13 | Editar `Sex` de cria viva sincroniza `Animal.Gender` do bezerro vinculado (`AnimalId`). | `AnimalCalvingService.UpdateCalfAsync` |
| RN-14 | Editar `Gender` de um bezerro sincroniza `Sex` da cria vinculada e ativa (`AnimalId`). | `AnimalService.UpdateAnimalAsync` |
| RN-15 | Editar `WeightKg` de cria viva com valor **atualiza ou cria** o `WeightRecord` de nascimento do bezerro (`RecordedAt = CalvingDate`); `WeightKg = null` é campo não enviado e **não altera nada** (histórico do bezerro nunca é removido). | `AnimalCalvingService.UpdateCalfAsync` |
| RN-16 | `VitalStatus` da cria é imutável na edição. | `AnimalCalvingCalfUpdateDto` (campo ausente) + serviço |
| RN-17 | Editar diretamente um `WeightRecord` do bezerro **não** reflete em `WeightKg` da cria (vínculo peso é unidirecional cria → registro). | `WeightRecordService` (sem mudança; documental) |
| RN-18 | Isolamento de tenant: toda busca de cria/animal/registro filtra por `PropertyId`. | Repositories |

### 8.8 Camadas Impactadas (adendo)

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `AnimalCalvingCalfUpdateDto.cs` | **Criar** (Sex/WeightKg/Notes opcionais) |
| `Application/Mappings` | `AnimalCalvingProfile.cs` | Mapear `AnimalCalvingCalfUpdateDto → AnimalCalvingCalf` (aplicar só campos não nulos; ignorar imutáveis) |
| `Application/Interfaces` | `IAnimalCalvingService.cs` | Adicionar `Task<AnimalCalvingCalfDto> UpdateCalfAsync(int calvingId, int calfId, AnimalCalvingCalfUpdateDto dto)` |
| `Application/Services` | `AnimalCalvingService.cs` | Implementar `UpdateCalfAsync` (RN-12, RN-13, RN-15). Opera sobre o `Animal` e seus `WeightRecord` **rastreados** (carregados via include no `IAnimalCalvingRepository`), sem nova dependência de repositório |
| `Application/Interfaces` | `IAnimalCalvingRepository.cs` | `GetCalfByIdAsync` (inclui `Calving`, `Animal` e `Animal.WeightRecords`), `GetActiveCalfByAnimalIdAsync`, `UpdateCalfAsync` |
| `Infrastructure/Repositories` | `AnimalCalvingRepository.cs` | Implementar as buscas acima; `GetByIdAsync` passa a incluir `Calves.Animal` para a inativação em cascata (RN-09 / §2 D9) |
| `Application/Services` | `AnimalService.cs` | No `UpdateAnimalAsync`, ao mudar `Gender`, sincronizar `Sex` da cria vinculada (RN-14) via `IAnimalCalvingRepository.GetActiveCalfByAnimalIdAsync` |
| `Api/Controllers` | `CalvingsController.cs` | Adicionar `PATCH /api/calvings/{calvingId}/calves/{calfId}` |

> **Nota de layering:** o `Animal`-bezerro é criado e inativado como parte do **agregado de parto** — a `AnimalCalvingService` monta o `Animal` (e seu `WeightRecord` de nascimento) pela navegação `AnimalCalvingCalf.Animal` e persiste tudo numa única transação via `IAnimalCalvingRepository`, sem precisar injetar `IAnimalRepository`. A edição e a inativação operam sobre entidades já **rastreadas** (carregadas por include). A sincronização reversa em `AnimalService` busca a cria por interface de repositório (`IAnimalCalvingRepository`, já injetado). Nenhum serviço chama outro serviço nem repositório concreto.

### 8.9 Notas de Migração (adendo)

**Nenhuma migração nova.** A edição usa apenas colunas já criadas pelo spec base (`Sex`, `Notes`, `WeightKg` em `AnimalCalvingCalves`; `AnimalId` FK; `Gender` em `Animals`; tabela `WeightRecords`). Não há novas colunas, índices ou tabelas.
