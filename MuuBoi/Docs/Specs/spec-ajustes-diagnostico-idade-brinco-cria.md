# Spec #15: Ajustes Pontuais — Método de Diagnóstico, Idade Gestacional e Brinco da Cria no Parto

**Módulo:** Reprodução (Cobertura/Diagnóstico) + Gestação + Parto/Cria
**Versão:** 1.0
**Data:** 05/Set/2026
**Fonte:** Reunião de ajustes pontuais — (1) método de diagnóstico na cobertura; (2) idade gestacional na confirmação; (3) brinco e brinco de fazenda opcionais por cria no parto.
**Status:** Aprovado para implementação
**Depende de:** Spec #5 (Eventos Reprodutivos), Spec 6.1 (Gestação), Spec 6.2 (Parto), Spec 6.3 (Cria), Spec #9 (Brinco Opcional e Cadastro de Cria como Animal)
**Relaciona-se com:** Spec #13 (Cadastro Retroativo de Gestação — mantém seu próprio cálculo de datas; a idade gestacional deste spec é do fluxo de diagnóstico), Spec #7 (Índices Zootécnicos — o método de diagnóstico passa a ser dado disponível).

---

## 1. Contexto e Objetivo

Três ajustes pontuais, independentes entre si, sobre fluxos já existentes:

1. **Método de diagnóstico na cobertura.** Hoje o diagnóstico (`PATCH /api/breeding-events/{id}/status`) registra apenas `Status` e `DiagnosisDate`. Não há como saber **como** o diagnóstico foi feito. Passa a ser obrigatório informar o método: **palpação** ou **ultrassom**.

2. **Idade gestacional na confirmação.** Ao confirmar a gestação (`Status = Successful`), hoje a `ExpectedCalvingDate` é sempre `BreedingDate + 280`. O veterinário, porém, estima no exame a **idade gestacional** (em dias), que dá uma data prevista de parto mais precisa que a derivada da data da cobertura. Passa a existir um campo opcional de idade gestacional que, **quando informado**, define a data prevista de parto; quando ausente, mantém o cálculo atual.

3. **Brinco e brinco de fazenda opcionais por cria no parto.** Pela Spec #9, cada cria **viva** já gera um `Animal` (bezerro) sem brinco. Passa a ser possível informar, **opcionalmente**, o **brinco oficial** (`TagNumber`) e o **brinco de fazenda** (`PropertyTagNumber`) de cada cria viva já no parto, alimentando o `Animal` gerado — sem precisar editar o animal depois.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Novo enum `DiagnosisMethod` (`Palpation` / `Ultrasound`), membros em inglês com `[Description]` em português. | Padroniza o método do diagnóstico como dado estruturado. |
| D2 | `DiagnosisMethod` é **obrigatório** ao registrar o diagnóstico, **tanto** para `Successful` quanto para `Unsuccessful`. Não se aplica a `AwaitingDiagnosis`. | Todo diagnóstico é feito por um método; registrar isso vale para os dois desfechos (inclusive "sem gestação"). |
| D3 | O método é persistido em `BreedingEvent.DiagnosisMethod` (nulável no domínio; preenchido no diagnóstico). | O método é dado do evento de cobertura/diagnóstico, existindo mesmo quando não há gestação (`Unsuccessful`). |
| D4 | Novo campo opcional `GestationalAge` (inteiro, **em dias** — nome sem sufixo de unidade) informado na confirmação. | Idade gestacional estimada no exame. Nome sem unidade embutida por convenção do projeto. |
| D5 | Quando `GestationalAge` é informado, `ExpectedCalvingDate = DiagnosisDate + (280 − GestationalAge)`. Quando ausente, mantém `BreedingDate + 280` (comportamento atual). | Idade medida no exame é mais precisa que a derivada da data da cobertura; sem ela, nada muda. |
| D6 | `GestationalAge` é persistido em `AnimalPregnancy.GestationalAge` (nulável). Só é informável quando `Status = Successful`. | Rastreabilidade do laudo junto à gestação; idade só existe quando há gestação confirmada. |
| D7 | `GestationalAge` válido é `1..279` (dias). | Garante `280 − GestationalAge ≥ 1` → `ExpectedCalvingDate > DiagnosisDate`. |
| D8 | `AnimalCalvingCalfCreateDto` ganha `TagNumber` e `PropertyTagNumber` **opcionais**, aplicados ao `Animal` gerado da cria **viva**. | Permite identificar o bezerro já no parto, sem edição posterior (Spec #9 US-03). |
| D9 | `TagNumber` da cria, quando informado, é **alfanumérico sem padrão definido** (até 6 caracteres — limite da coluna `Animals.TagNumber`); **não** segue o formato de 6 dígitos numéricos do cadastro manual. A **unicidade** entre brincos não nulos (`TagNumberExistsAsync` — Spec #9 RN-03) continua valendo, inclusive **dentro do mesmo parto**. | Brinco da cria no parto é mais flexível que o cadastro manual; ainda assim não pode duplicar. |
| D10 | `PropertyTagNumber` da cria é **texto livre** (máx. 100), **sem** unicidade — igual ao cadastro manual de animal. | Brinco de fazenda é identificador interno informal, sem regra de unicidade no sistema. |
| D11 | `TagNumber`/`PropertyTagNumber` **só** se aplicam a crias **vivas** (que geram `Animal`). Informá-los para natimorta é erro de validação. | Natimorta não gera `Animal` (Spec #9 D3); brinco não teria onde morar. |

---

## 3. Histórias de Usuário

### US-01 — Registrar o método do diagnóstico
> **Como** produtor, **quero** informar se o diagnóstico foi por palpação ou ultrassom, **para** registrar como a gestação foi verificada.

**Critérios de aceite:**
- Ao registrar o diagnóstico (confirmado ou sem gestação), o método é **obrigatório**.
- Omissão do método retorna `400 Bad Request`.
- O método fica visível no detalhe da cobertura.

### US-02 — Informar a idade gestacional na confirmação
> **Como** produtor, **quero** informar a idade gestacional estimada no exame ao confirmar a gestação, **para** obter uma data prevista de parto mais precisa.

**Critérios de aceite:**
- Ao confirmar (`Successful`), posso informar a idade gestacional em dias (opcional).
- Se informada, a data prevista de parto passa a ser `data do diagnóstico + (280 − idade)`.
- Se não informada, a data prevista de parto continua sendo `data da cobertura + 280`.
- Informar idade gestacional junto de um resultado "sem gestação" retorna `400 Bad Request`.

### US-03 — Informar o brinco da cria já no parto
> **Como** produtor, **quero** informar o brinco e/ou o brinco de fazenda de uma cria viva no momento do parto, **para** já identificar o bezerro sem editar o animal depois.

**Critérios de aceite:**
- Para cada cria **viva**, posso informar `TagNumber` (alfanumérico, sem padrão, até 6 caracteres) e/ou `PropertyTagNumber` (texto livre), ambos opcionais.
- O `Animal`-bezerro é criado com esses brincos.
- Brinco oficial duplicado (com outro animal ou com outra cria do mesmo parto) retorna `409 Conflict`.
- Informar brinco para cria **natimorta** retorna `400 Bad Request`.

---

## 4. Casos de Uso

### CU-01 — Diagnóstico da cobertura com método (e idade gestacional na confirmação)
**Ator:** Produtor autenticado
**Pré-condição:** Cobertura existe, pertence ao tenant, `Status = AwaitingDiagnosis`.

**Fluxo principal:**
1. Produtor envia `PATCH /api/breeding-events/{id}/status` com `Status`, `DiagnosisDate`, `DiagnosisMethod` e, opcionalmente, `GestationalAge`.
2. DTO valida: `DiagnosisMethod` obrigatório; `DiagnosisDate` não futura e ≥ `BreedingDate`; `GestationalAge` (se enviado) em `1..279` e **apenas** com `Status = Successful`.
3. Sistema grava `Status`, `DiagnosisDate` e `DiagnosisMethod` na cobertura.
4. Se `Status = Successful`, cria a `AnimalPregnancy`:
   - Se `GestationalAge` informado → `ExpectedCalvingDate = DiagnosisDate + (280 − GestationalAge)` e grava `GestationalAge`.
   - Senão → `ExpectedCalvingDate = BreedingDate + 280` e `GestationalAge = null`.
5. Retorna `200 OK` com a cobertura atualizada.

**Alternativos:** método ausente → `400`; `GestationalAge` com `Unsuccessful` ou fora de faixa → `400`; cobertura já diagnosticada → `409` (inalterado); `DiagnosisDate < BreedingDate` → `422` (inalterado).

### CU-02 — Registrar parto informando brinco das crias vivas
**Ator:** Produtor autenticado
**Pré-condição:** Gestação existe, pertence ao tenant, `Status = Confirmed`.

**Fluxo principal:**
1. Produtor envia `POST /api/pregnancies/{pregnancyId}/calvings`; cada cria viva pode incluir `TagNumber` e/ou `PropertyTagNumber`.
2. Validações da Spec #9 (nome e raça por cria viva) aplicadas normalmente.
3. Para cada cria viva com `TagNumber` (alfanumérico, até 6 caracteres, sem padrão): o serviço valida unicidade contra a base **e** contra as demais crias do lote — duplicado → `409`.
4. Para cada cria viva, o `Animal` gerado (Spec #9 RN-05) recebe também `TagNumber` e `PropertyTagNumber` informados.
5. Cria natimorta com `TagNumber`/`PropertyTagNumber` → `400` (passo 2/validação).
6. Retorna `201 Created` com `AnimalCalvingDto`.

**Alternativos:** herdados da Spec #9 / 6.2 (`400` / `404` / `409` / `422`).

---

## 5. Especificação Técnica de Modelagem

### 5.1 Entidades

#### `BreedingEvent.cs` — método de diagnóstico
| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `DiagnosisMethod` | `DiagnosisMethod?` | Não (domínio) | Preenchido no diagnóstico; nulo enquanto `AwaitingDiagnosis` e em registros antigos. |

```csharp
public DiagnosisMethod? DiagnosisMethod { get; set; }
```

#### `AnimalPregnancy.cs` — idade gestacional
| Campo | Tipo | Obrigatório | Notas |
|-------|------|-------------|-------|
| `GestationalAge` | `int?` | Não | Idade gestacional **em dias** no momento da confirmação. Nulo quando não informada (e nas gestações retroativas da Spec #13). |

```csharp
public int? GestationalAge { get; set; }
```

#### `AnimalCalvingCalf.cs` — sem mudança de entidade
`TagNumber`/`PropertyTagNumber` **não** viram colunas da cria: são entrada que alimenta o `Animal` gerado (mesmo padrão de `Name`/`Breed` na Spec #9). O `Animal` já possui `TagNumber` e `PropertyTagNumber`.

### 5.2 Enums

#### `DiagnosisMethod.cs` — **novo** (`Domain/Enums/`)
```csharp
using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    public enum DiagnosisMethod
    {
        [Description("Palpação")]
        Palpation = 1,

        [Description("Ultrassom")]
        Ultrasound = 2
    }
}
```
Demais enums reutilizados sem mudança.

### 5.3 DTOs

#### `BreedingEventStatusUpdateDto.cs` — adicionar método e idade
```csharp
public class BreedingEventStatusUpdateDto : IValidatableObject
{
    [Required(ErrorMessage = "O status é obrigatório.")]
    public ReproductiveEventStatus Status { get; set; }

    [Required(ErrorMessage = "A data do diagnóstico é obrigatória.")]
    public DateTime DiagnosisDate { get; set; }

    [Required(ErrorMessage = "O método de diagnóstico é obrigatório.")]
    public DiagnosisMethod? DiagnosisMethod { get; set; }

    [Range(1, 279, ErrorMessage = "A idade gestacional deve estar entre 1 e 279 dias.")]
    public int? GestationalAge { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status == ReproductiveEventStatus.AwaitingDiagnosis)
            yield return new ValidationResult(
                "O status não pode ser alterado para 'Aguardando diagnóstico'.",
                new[] { nameof(Status) });

        if (DiagnosisDate > DateTime.UtcNow)
            yield return new ValidationResult(
                "A data do diagnóstico não pode ser futura.",
                new[] { nameof(DiagnosisDate) });

        if (GestationalAge.HasValue && Status != ReproductiveEventStatus.Successful)
            yield return new ValidationResult(
                "A idade gestacional só pode ser informada quando a gestação é confirmada.",
                new[] { nameof(GestationalAge) });
    }
}
```

#### `BreedingEventDto.cs` — expor o método (saída)
Adicionar `EnumValueDto? DiagnosisMethod`.

#### `AnimalPregnancyDto.cs` / `AnimalPregnancyListItemDto.cs` — expor idade (saída)
Adicionar `int? GestationalAge` (detalhe; opcional na listagem).

#### `AnimalCalvingCalfCreateDto.cs` — adicionar brincos opcionais
```csharp
// Alfanumérico, sem padrão definido; limite de 6 caracteres da coluna Animals.TagNumber.
[MaxLength(6, ErrorMessage = "O brinco deve ter no máximo 6 caracteres.")]
public string? TagNumber { get; set; }

[MaxLength(100)]
public string? PropertyTagNumber { get; set; }
```
Validação adicional no `IValidatableObject`: se `VitalStatus != Live` e (`TagNumber` ou `PropertyTagNumber` informado) → erro (`brinco só se aplica a cria viva`).

#### `AnimalCalvingCalfDto.cs` — expor brincos do animal gerado (saída)
Adicionar `string? TagNumber` e `string? PropertyTagNumber` (do `Animal` vinculado).

### 5.4 Endpoints da API
> Nenhum endpoint novo. Comportamento acrescido aos existentes.

| Método | Rota | Mudança | Retorno |
|--------|------|---------|---------|
| `PATCH` | `/api/breeding-events/{id}/status` | Exige `DiagnosisMethod`; aceita `GestationalAge` opcional (só em `Successful`) que define `ExpectedCalvingDate` | `200` / `400` / `404` / `409` / `422` |
| `POST` | `/api/pregnancies/{pregnancyId}/calvings` | Aceita `TagNumber`/`PropertyTagNumber` por cria viva; brinco alfanumérico (máx. 6), valida unicidade | `201` / `400` / `404` / `409` / `422` |
| `GET` | `/api/breeding-events/diagnosis-methods` | **Novo (lookup)** — lista de métodos para popular seleção no app | `200 IEnumerable<LookupDto>` |

### 5.5 Regras de Negócio

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `DiagnosisMethod` é obrigatório no diagnóstico (`Successful` e `Unsuccessful`). | `BreedingEventStatusUpdateDto` (`[Required]`) |
| RN-02 | Persistir `DiagnosisMethod` em `BreedingEvent` ao registrar o diagnóstico. | `BreedingEventService.UpdateStatusAsync` |
| RN-03 | `GestationalAge`, se enviado, deve estar em `1..279` e só com `Status = Successful`. | `BreedingEventStatusUpdateDto` (`[Range]` + `IValidatableObject`) |
| RN-04 | Na confirmação: se `GestationalAge` informado → `ExpectedCalvingDate = DiagnosisDate + (280 − GestationalAge)` e grava `GestationalAge`; senão → `BreedingDate + 280` e `GestationalAge = null`. | `AnimalPregnancyService.CreateForBreedingEventAsync` |
| RN-05 | `TagNumber` de cria (se informado) é alfanumérico sem padrão, com no máximo 6 caracteres (limite da coluna). | `AnimalCalvingCalfCreateDto` (`[MaxLength(6)]`) |
| RN-06 | `TagNumber` de cria deve ser único entre animais com brinco (base) **e** entre as crias do próprio parto; duplicado → `ConflictException` (409). | `AnimalCalvingService.CreateAsync` (via `IAnimalRepository.TagNumberExistsAsync` + checagem do lote) |
| RN-07 | `TagNumber`/`PropertyTagNumber` só são aceitos para cria **viva**; informá-los para natimorta → `400`. | `AnimalCalvingCalfCreateDto` (`IValidatableObject`) |
| RN-08 | Para cada cria viva, o `Animal` gerado recebe `TagNumber` e `PropertyTagNumber` informados (ou `null`). | `AnimalCalvingService.CreateAsync` |
| RN-09 | Isolamento de tenant: buscas de unicidade/animais filtram por `PropertyId`. | Repositories |

### 5.6 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Enums` | `DiagnosisMethod.cs` | **Criar** enum (inglês + `[Description]` PT). |
| `Domain/Models` | `BreedingEvent.cs` | Adicionar `DiagnosisMethod? DiagnosisMethod`. |
| `Domain/Models` | `AnimalPregnancy.cs` | Adicionar `int? GestationalAge`. |
| `Application/DTOs` | `BreedingEventStatusUpdateDto.cs` | Adicionar `DiagnosisMethod` (obrigatório) e `GestationalAge` (opcional) + validações. |
| `Application/DTOs` | `BreedingEventDto.cs` | Expor `DiagnosisMethod`. |
| `Application/DTOs` | `AnimalPregnancyDto.cs` / `AnimalPregnancyListItemDto.cs` | Expor `GestationalAge`. |
| `Application/DTOs` | `AnimalCalvingCalfCreateDto.cs` | Adicionar `TagNumber`/`PropertyTagNumber` + validação de cria viva. |
| `Application/DTOs` | `AnimalCalvingCalfDto.cs` | Expor `TagNumber`/`PropertyTagNumber` do animal gerado. |
| `Application/Mappings` | `BreedingEventProfile.cs` | Mapear `DiagnosisMethod` → `EnumValueDto`. |
| `Application/Mappings` | `AnimalPregnancyProfile.cs` | Mapear `GestationalAge`. |
| `Application/Mappings` | `AnimalCalvingProfile.cs` | Mapear `TagNumber`/`PropertyTagNumber` do `Animal` vinculado na saída. |
| `Application/Interfaces` | `IAnimalPregnancyService.cs` | `CreateForBreedingEventAsync` ganha parâmetro `int? gestationalAge`. |
| `Application/Services` | `AnimalPregnancyService.cs` | Aplicar RN-04. |
| `Application/Services` | `BreedingEventService.cs` | Gravar `DiagnosisMethod`; repassar `GestationalAge` na confirmação (RN-02). |
| `Application/Services` | `AnimalCalvingService.cs` | Aplicar brincos ao `Animal` gerado + unicidade (RN-06/08); **injetar `IAnimalRepository`** para `TagNumberExistsAsync`. |
| `Api/Controllers` | `BreedingEventsController.cs` | Novo lookup `GET /api/breeding-events/diagnosis-methods`. |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Sem novas relações; apenas as colunas novas (mapeadas por convenção). |
| `Infrastructure/Migrations` | *(ver §6)* | **Requer aprovação antes de criar.** |

> **Nota de layering:** `AnimalCalvingService` passa a injetar `IAnimalRepository` (interface) apenas para validar unicidade de brinco — sem depender de repositório concreto nem de outro serviço, mantendo a regra do CLAUDE.md. O `Animal`-bezerro continua sendo montado/persistido pelo agregado de parto (Spec #9), agora também com os brincos.

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

Migração sugerida: `Spec15_DiagnosisMethodAndGestationalAge`.

**Alterar tabela `BreedingEvents`:**
| Coluna | Tipo | Restrições |
|--------|------|------------|
| `DiagnosisMethod` | int | **nullable** (registros existentes ficam nulos) |

**Alterar tabela `AnimalPregnancies`:**
| Coluna | Tipo | Restrições |
|--------|------|------------|
| `GestationalAge` | int | **nullable** |

> Ambas aditivas e nuláveis — sem risco a dados existentes. O brinco da cria **não** gera coluna nova (usa `Animals.TagNumber`/`Animals.PropertyTagNumber`, já existentes).

---

## 7. Fora do Escopo deste Spec

- **Idade gestacional no cadastro retroativo** (Spec #13) — o retroativo mantém `EstimatedConceptionDate`/`ExpectedCalvingDate` próprios; `GestationalAge` fica nulo nele.
- **Edição do método de diagnóstico / idade gestacional após registrados** — não previsto aqui (diagnóstico é imutável após registrado — regra atual).
- **Conversão de unidade da idade gestacional** (meses/semanas) — decidido usar **dias**; conversões de entrada ficam a cargo do cliente.
- **Índices zootécnicos que usem o método de diagnóstico** → Spec #7 (aqui só se disponibiliza o dado).
- **Modelagem base de cobertura/gestação/parto/cria** → Specs #5, 6.1, 6.2, 6.3, #9.
- **Unicidade/formato do brinco de fazenda** — decidido: texto livre, sem unicidade (D10).
