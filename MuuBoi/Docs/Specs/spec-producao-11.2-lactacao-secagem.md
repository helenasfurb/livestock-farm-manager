# Spec 11.2: Lactação e Secagem (entidade `Lactation`)

**Módulo:** Produção — Lactação / Secagem
**Versão:** 1.0
**Data:** 31/Ago/2026
**Fonte:** Divisão do Spec #11 (Produção Leiteira e Status de Lactação) em 11.1 (produção) e 11.2 (esta).
**Status:** Rascunho para revisão
**Depende de:** Spec #6 (Gestação/Parto) — o parto abre a lactação (D10). Spec 11.1 — paradigma de tenant/`BaseEntity` e alinhamento ao backend atual.
**Parte de:** Spec #11 (Produção). **Antecede:** Spec de Indicadores (média por vaca em lactação, proporção de vacas em lactação, produção do rebanho por período).

---

## 1. Contexto e Objetivo

O ciclo produtivo de uma vaca leiteira é **parto → lactação → secagem → período seco → novo parto**. Esta spec modela esse ciclo como um **fato de vigência** — a entidade `Lactation` (um período com início no parto e fim na secagem) — e o expõe pelo seu ciclo de vida: **abertura** (pelo parto ou por cadastro inicial), **secagem** (fecha a lactação) e **consulta** (histórico, lactação atual e **DEL — dias em lactação**).

Dois princípios herdados do Spec #11 guiam tudo:

- **Status derivado, nunca armazenado (D2):** não existe flag `IsLactating` persistida. "Em lactação" é sempre recomputado a partir da existência de uma `Lactation` aberta — exatamente como o `ReproductiveStatus` já é derivado hoje pelo `ReproductiveStatusResolver`.
- **Secagem dirigida por evento (D4):** a secagem é uma **ação explícita** do produtor (secagem medicada, pontual), não um decurso de tempo. Não há job agendado.

> **Ortogonalidade:** status de lactação e status reprodutivo são **dimensões independentes**. Uma vaca pode estar **prenhe e em lactação** ao mesmo tempo. Esta spec não altera o status reprodutivo.

---

## 2. Escopo

**Dentro (11.2):**
- Entidade `Lactation` = ciclo real (`StartDate` no parto, `EndDate` na secagem, `null` = aberta).
- **Abertura pelo parto (D10):** o registro de parto (Spec #6) abre a lactação na mesma ação.
- **Onboarding de lactações pré-existentes (D11), embutido no cadastro do animal (D17):** ao cadastrar um animal **Vaca** ou **Novilha**, é possível informar a data da última lactação e, opcionalmente, a data da secagem, criando o histórico correspondente — **sem parto vinculado**. Com `EndDate` → vaca **seca** (lactação fechada); sem `EndDate` → **em lactação** (lactação aberta). Também disponível de forma avulsa pelo endpoint de lactação.
- **Secagem (D4):** ação que fecha a lactação (`EndDate` + observações). **Ação simples** — sem detalhe de terapia medicada.
- **Status derivado (D2)** "em lactação: sim/não" e **DEL (D5)** calculados on-the-fly.
- Consulta: histórico por animal, lactação atual, detalhe.
- Correção: editar `StartDate`/estimativa; desfazer secagem (reabrir); inativar (soft delete).
- Enum `LactationOrigin`.

**Fora (→ spec de indicadores / futuro):**
- **Índices agregados de rebanho:** média por vaca em lactação, proporção de vacas em lactação, produção do rebanho por período (§5 do Spec #11). Vão para a **spec de indicadores**. Esta spec entrega só o fato/DEL por animal que sustenta esses índices.
- **Terapia medicada da secagem** (qual antibiótico/selante) — se necessário registrar, reutiliza o módulo de medicação (`AnimalMedication`) de forma independente.
- `MilkYield` / medição individual por animal (Spec #11, D7).
- Distinção semântica **novilha nulípara × vaca seca** (Spec #11, O4) — para efeito de "não lactante", ambas são iguais.
- Sincronização offline-first / `Id` GUID de cliente / upsert idempotente (Spec #11, D9) — evolução aditiva futura, como na 11.1.
- Curva de lactação, pico de produção, persistência.

---

## 3. Decisões

Herdadas do Spec #11 (não reabertas) e as **resolvidas** nesta spec (O1, O3, escopo, realinhamento).

| # | Decisão | Origem / Motivo |
|---|---------|-----------------|
| D2 | Status "em lactação" é **derivado**, nunca uma flag persistida. | Spec #11 D2: coerente com o `ReproductiveStatusResolver`; evita dado velho e job. |
| D3 | `Lactation` é o **fato do ciclo**: parto abre (`StartDate`), secagem fecha (`EndDate`). Invariante: **no máximo uma aberta por animal**. | Spec #11 D3: torna trivial a query de "quantas em lactação" e o denominador dos índices futuros. |
| D4 | Secagem **event-driven**, sem job. | Spec #11 D4: a secagem real é pontual e medicada. |
| D5 | **DEL** calculado on-the-fly; congela em `EndDate` quando fechada. | Spec #11 D5: aritmética de data pura, sem contador. |
| D10 | Parto (Spec #6) **abre** a `Lactation` na mesma ação; elo opcional `CalvingId`. | Spec #11 D10: rastreabilidade sem acoplar reprodução e produção. |
| D11 | Onboarding: lactação **aberta, sem parto** (`CalvingId == null`, `Origin = InitialSeed`), com `StartDate` **obrigatória** (pode ser um palpite do produtor). O **marcador explícito de data estimada** (`StartDateEstimated`) fica **adiado para decisão futura** — não modelado agora. | Spec #11 D11: os índices dependem da **data**, não do objeto Parto; equivale a um saldo de abertura. |
| **D12** *(nova)* | **Alinhar ao backend atual**: `Id int`, `PropertyId (Guid)`, `BaseEntity`, soft delete, tenant por query filter global, `ExceptionMiddleware`. **Sem GUID de cliente/upsert** nesta fase. | Mesmo realinhamento da 11.1 (D5): o Spec #11 pai foi escrito com `Guid`/`FarmId`/offline; a implementação segue o padrão do projeto. Offline-first fica como evolução aditiva. |
| **D13** *(nova, escopo)* | **11.2 = ciclo + DEL por animal.** Índices agregados de rebanho vão para a **spec de indicadores**. | Decisão do TCC (31/Ago): manter a spec enxuta, como na 11.1. |
| **D14** *(nova, resolve O3)* | Registrar parto com uma lactação **aberta** → **erro `422`**. Exige secar a anterior antes. | Decisão do TCC (31/Ago): protege a qualidade do dado; recomendação do Spec #11 (O3). |
| **D15** *(nova, resolve O1)* | Fronteira do dia da secagem: **`[StartDate, EndDate]`** (intervalo **fechado**). No **dia da secagem a vaca ainda conta como em lactação** (ainda produziu); fica seca a partir de `EndDate + 1`. | Decisão do TCC (31/Ago): reflete a operação real — no dia do registro final da secagem ela ainda foi ordenhada. **Ajusta a recomendação original do Spec #11 (O1), que era meio-aberto.** |
| **D16** *(nova)* | Secagem = **ação simples** (data + observações). Terapia medicada fica fora, reutilizável via `AnimalMedication`. | Decisão do TCC (31/Ago): menor superfície agora, sem acoplar produção a medicação. |
| **D17** *(nova, estende D11)* | No **cadastro do animal** (Spec #1), quando a classificação é **Vaca** ou **Novilha**, aceitar **opcionalmente** os dados da última lactação (`StartDate` obrigatória **se** o bloco for informado; `EndDate` opcional). Se informado, cria uma `Lactation` `InitialSeed` no mesmo request: `EndDate` presente → **fechada/seca**; ausente → **aberta/em lactação**. Sem os dados, nenhuma lactação é criada. No frontend, uma flag **"já lactou?"** governa a exibição do bloco; quando marcada, **sempre haverá `StartDate`**. | Decisão do TCC (31/Ago): captura o estado de lactação já na entrada do animal e monta o histórico. Embutir no create reduz requisições (ambiente com internet fraca). **Estende a D11**, que previa só lactação *aberta* semeada — agora `InitialSeed` também pode nascer *fechada*. |
| **D18** *(nova, resolve parte de O4)* | **Status produtivo derivado** do histórico, com **3 valores**: `Em lactação` (existe lactação aberta), `Seca` (há lactação encerrada, nenhuma aberta) e `Nunca lactou` (sem histórico). Exposto no animal ao lado do `ReproductiveStatus`. Nunca persistido. | Decisão do TCC (31/Ago): o produtor precisa enxergar o estado produtivo direto no animal. **Traz para o escopo** a distinção *nunca lactou × seca* que o Spec #11 (O4) havia deixado de fora. |

> **Impacto de D15 na futura spec de indicadores:** o limite superior da sobreposição em `cowLactationDays` passa a ser **`EndDate + 1`** (inclui o dia da secagem), e `vacasEmLactacao(@date)` usa `EndDate IS NULL OR @date <= EndDate`.

---

## 4. Histórias de Usuário

### US-01 — Registrar a lactação a partir do parto (automático)
> **Como** produtor, **quando** registro um parto, **quero** que a vaca passe a constar "em lactação" automaticamente, **para** não ter um passo manual a mais.

**Critérios de aceite:**
- Ao criar o parto, abre-se uma `Lactation` com `StartDate` = data do parto, `Origin = Calving`, vinculada ao parto.
- Se a vaca já tiver uma lactação **aberta**, o parto é **recusado** (`422`) até que a anterior seja secada (D14).

### US-02 — Cadastrar o estado de lactação ao registrar Vaca/Novilha (onboarding — D11/D17)
> **Como** produtor, ao cadastrar uma vaca ou novilha, **quero** informar a data da última lactação (e a secagem, se já secou), **para** que o histórico e os indicadores valham desde o dia um.

**Critérios de aceite:**
- No cadastro de animal **Vaca** ou **Novilha**, os dados de última lactação são **opcionais**; se eu não informar, nenhuma lactação é criada.
- Se informo, `StartDate` é obrigatória; `EndDate` é opcional.
  - **Com** `EndDate` → vaca **seca** (lactação `InitialSeed` **fechada**).
  - **Sem** `EndDate` → **em lactação** (lactação `InitialSeed` **aberta**).
- O bloco de última lactação só se aplica a **Vaca/Novilha**; para outras classificações é recusado.
- Não é possível semear uma segunda lactação **aberta** para o mesmo animal (invariante D3).
- Também é possível semear/adicionar avulso depois via `POST /api/animals/{animalId}/lactations`.

### US-03 — Secar uma vaca
> **Como** produtor, **quero** registrar a secagem de uma vaca, **para** encerrar a lactação atual.

**Critérios de aceite:**
- Informo a **data da secagem** (`EndDate`) e, opcionalmente, observações.
- `EndDate` não pode ser futura nem anterior à `StartDate`.
- No **dia da secagem** a vaca ainda conta como em lactação; fica seca no dia seguinte (D15).
- Secar uma vaca que já está seca → `409`.

### US-04 — Acompanhar DEL e histórico
> **Como** produtor, **quero** ver há quantos dias a vaca está em lactação (DEL) e seu histórico de lactações, **para** planejar manejo e secagem.

**Critérios de aceite:**
- Consigo a **lactação atual** com o DEL calculado, e o **histórico** de lactações do animal.
- DEL da lactação aberta = hoje − `StartDate`; da fechada = `EndDate` − `StartDate` (congelado).

### US-05 — Corrigir um registro
> **Como** produtor, **quero** corrigir a data de início (ou desfazer uma secagem lançada errada), **para** manter os dados corretos.

**Critérios de aceite:**
- `PATCH` corrige `StartDate`/estimativa (reaplica validações e invariantes).
- Posso **desfazer a secagem** (reabrir), desde que não haja outra lactação aberta.
- `DELETE` inativa a lactação (soft delete).

---

## 5. Casos de Uso

### CU-01 — Abertura por parto (D10)
1. `POST /api/pregnancies/{pregnancyId}/calvings` (Spec #6) cria o parto.
2. **Antes** de concluir: se o animal tem lactação aberta → `422` (D14).
3. Cria o parto e, na mesma transação, abre `Lactation` (`StartDate` = `CalvingDate`, `CalvingId`, `Origin = Calving`).

### CU-02 — Onboarding (D11/D17)
- **No cadastro do animal (Spec #1):** `POST /api/animals` com bloco opcional `InitialLactation` (`LactationSeedDto`) quando `Classification` for `Cow`/`Heifer`. Cria a `Lactation` `InitialSeed` na mesma transação (fechada se `EndDate`, aberta se não).
  - Bloco informado para classificação que não seja Vaca/Novilha → `422`.
  - `StartDate` futura → `400`; `EndDate` fora de `[StartDate, hoje]` → `400`/`422`.
- **Avulso:** `POST /api/animals/{animalId}/lactations` com `LactationCreateDto` (`StartDate` obrigatória).
- Recusa (`409`) se a operação resultar em **duas lactações abertas** para o mesmo animal (invariante D3).

### CU-03 — Secagem (D4)
- `POST /api/lactations/{id}/dry-off` com `LactationDryOffDto` (`EndDate`, `DryOffNotes?`).
- Fecha a lactação. `409` se já fechada/inativa; `422` se `EndDate` < `StartDate`; `400` se futura.

### CU-04 — Consulta
- `GET /api/animals/{animalId}/lactations` — histórico (mais recente primeiro).
- `GET /api/animals/{animalId}/lactations/current` — lactação aberta + DEL; `204` se não há aberta.
- `GET /api/lactations/{id}` — detalhe; inexistente → `404`.

### CU-05 — Correção
- `PATCH /api/lactations/{id}` — corrige `StartDate`.
- `DELETE /api/lactations/{id}/dry-off` — reabre (limpa `EndDate`); `409` se já aberta ou se houver outra aberta.
- `DELETE /api/lactations/{id}` — soft delete → `204`; já inativa → `409`.

### CU-06 — Reversão de parto
- `DELETE /api/calvings/{id}` (Spec #6) inativa o parto **e** a lactação vinculada (`CalvingId`).

---

## 6. Especificação Técnica

### 6.1 Entidade `Lactation`
> `Domain/Models/Lactation.cs`

```csharp
public class Lactation : BaseEntity, ITenantEntity
{
    public int AnimalId { get; set; }

    public DateTime StartDate { get; set; }          // data do parto — abre a lactação

    public DateTime? EndDate { get; set; }           // data da secagem; null = em lactação

    public int? CalvingId { get; set; }              // elo ao parto (D10); null em InitialSeed (D11)

    public LactationOrigin Origin { get; set; }      // Calving | InitialSeed

    [MaxLength(500)]
    public string? DryOffNotes { get; set; }         // observações da secagem (D16)

    public Guid PropertyId { get; set; }

    public Animal? Animal { get; set; }
    public AnimalCalving? Calving { get; set; }
}
```

### 6.2 Enums
> `Domain/Enums/` — nomes em inglês, `[Description]` em português (convenção do projeto).

```csharp
public enum LactationOrigin
{
    [Description("Parto")]
    Calving = 1,

    [Description("Cadastro inicial")]
    InitialSeed = 2
}

// Status produtivo do animal — DERIVADO do histórico de lactação (D2/D18). Nunca persistido.
public enum ProductiveStatus
{
    [Description("Nunca lactou")]
    NeverLactated = 1,     // sem nenhum registro de lactação ativo

    [Description("Em lactação")]
    Lactating = 2,         // existe lactação ativa em aberto (EndDate == null)

    [Description("Seca")]
    Dry = 3                // há lactação(ões) encerrada(s), mas nenhuma em aberto
}
```

### 6.3 Status produtivo derivado e DEL (D2/D5/D15/D18)
> `Application/Helpers/ProductiveStatusResolver.cs` — fonte única, espelhando o `ReproductiveStatusResolver` (usável tanto no detalhe de um animal quanto na lista, set-based).

**Status produtivo do animal** (`ProductiveStatus`) — derivado do conjunto de lactações **ativas** do animal, com `ref` = data de referência (`DateTime.UtcNow.Date`):

```
1. existe lactação ativa com IsLactating(ref) == true   → Lactating
2. senão, existe ao menos uma lactação ativa encerrada  → Dry
3. senão (nenhum registro)                              → NeverLactated
```

**Por lactação** (usado nos DTOs de lactação):

```
IsLactating(lact, ref) =  lact.StartDate <= ref
                          AND (lact.EndDate == null OR ref <= lact.EndDate)   // intervalo fechado (D15)

DaysInMilk(lact, ref)  =  lact.EndDate == null
                          ? (ref - lact.StartDate).Days                       // aberta: até hoje
                          : (lact.EndDate - lact.StartDate).Days              // fechada: congelado
```

Nada disso é persistido (D2). O `ProductiveStatus` e o `DaysInMilk` da lactação aberta são expostos no animal (§6.7), ao lado do `ReproductiveStatus` e pela mesma audiência (Vaca/Novilha); para as demais classificações ficam nulos. `DaysInMilk` só é significativo para a **lactação aberta**; no histórico fechado representa a duração total do ciclo.

### 6.4 DTOs
> `Application/DTOs/`

- **`LactationCreateDto`** (onboarding avulso — D11) — `StartDate` (`[Required]`; não futura via `IValidatableObject`).
- **`LactationSeedDto`** (bloco embutido no cadastro do animal — D17) — `StartDate` (`[Required]` **dentro do bloco**; não futura), `EndDate?` (opcional; se presente, em `[StartDate, hoje]`). Adicionado ao **`AnimalCreateDto`** como propriedade **opcional** `InitialLactation` (`LactationSeedDto?`). Regras cruzadas validadas no `AnimalService` (só Vaca/Novilha; ver RN-11/RN-12).
- **`LactationDryOffDto`** (secagem) — `EndDate` (`[Required]`; não futura via `IValidatableObject`), `DryOffNotes?` (`[MaxLength(500)]`).
- **`LactationUpdateDto`** (correção, PATCH parcial) — `StartDate?` (não futura).
- **`LactationDto`** (resposta/detalhe) — `Id`, `AnimalId`, `AnimalTagNumber`, `StartDate`, `EndDate`, `Origin` (`EnumValueDto`), `CalvingId`, `DryOffNotes`, `IsLactating` (derivado), `DaysInMilk` (derivado), `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`LactationListItemDto`** — `Id`, `StartDate`, `EndDate`, `Origin` (`EnumValueDto`), `IsLactating`, `DaysInMilk`.

Mapeamento em `Application/Mappings/LactationProfile.cs` (origem como `EnumValueDto` com `GetDescription()`; `IsLactating`/`DaysInMilk` preenchidos no service, à semelhança de como o `ReproductiveStatus` é resolvido). **PATCH aplicado campo a campo no service** — não via AutoMapper — para não repetir a armadilha `DateTime?` → `DateTime` (ver 11.1).

### 6.5 Endpoints da API
> Auth: Bearer Token obrigatório. Padrão do projeto: recursos **por animal** aninhados sob `/api/animals/{animalId}`; ações sobre um registro específico em `/api/lactations/{id}` (mesmo padrão do `BreedingEventsController`).

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/animals/{animalId}/lactations` | Onboarding: abrir lactação semeada (D11) | `201 LactationDto` / `400` / `404` / `409` |
| `GET` | `/api/animals/{animalId}/lactations` | Histórico de lactações do animal | `200 [LactationListItemDto]` / `404` |
| `GET` | `/api/animals/{animalId}/lactations/current` | Lactação aberta + DEL | `200 LactationDto` / `204` (sem aberta) / `404` |
| `GET` | `/api/lactations/{id}` | Detalhe | `200 LactationDto` / `404` |
| `PATCH` | `/api/lactations/{id}` | Corrigir início/estimativa | `200 LactationDto` / `400` / `404` / `409` |
| `POST` | `/api/lactations/{id}/dry-off` | Secagem (fecha a lactação) | `200 LactationDto` / `400` / `404` / `409` |
| `DELETE` | `/api/lactations/{id}/dry-off` | Desfazer secagem (reabrir) | `200 LactationDto` / `404` / `409` |
| `DELETE` | `/api/lactations/{id}` | Inativar (soft delete) | `204` / `404` / `409` |
| `GET` | `/api/lactations/origins` | Lookup do enum de origem | `200 [{value, label}]` |

> **Além destes**, o onboarding também ocorre embutido no **`POST /api/animals`** (Spec #1) via o bloco opcional `InitialLactation` — ver §8. Não há endpoint novo para isso; é uma extensão do payload de criação de animal.

### 6.6 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | **Invariante:** no máximo uma lactação **aberta** (`EndDate == null` e `IsActive`) por animal (D3). Validar na abertura por parto, no onboarding e ao reabrir. | Service |
| RN-02 | `StartDate` não pode ser futura. | DTO (`IValidatableObject`) |
| RN-03 | Onboarding → `Origin = InitialSeed`, `CalvingId = null`, `StartDate` obrigatória (D11). | Service / mapping |
| RN-04 | Secagem: a lactação deve estar **aberta e ativa**; `EndDate` ∈ `[StartDate, hoje]`. Futura → `400`; anterior à `StartDate` → `422`; já fechada/inativa → `409`. | DTO + Service |
| RN-05 | **Parto com lactação aberta → `422`** (D14). Exige secar a anterior antes. | `AnimalCalvingService.CreateAsync` |
| RN-06 | Reabrir secagem só se a lactação estiver **fechada e ativa** e **não** houver outra aberta para o animal (RN-01). | Service |
| RN-07 | Inativar o parto (`DELETE /api/calvings/{id}`) **inativa a lactação vinculada** (`CalvingId`). | `AnimalCalvingService.InactivateAsync` |
| RN-08 | `ProductiveStatus`, `IsLactating` e `DaysInMilk` são **sempre derivados** (D2/D5/D18), nunca persistidos. | Application (resolvers) |
| RN-09 | Correção de `StartDate` (PATCH) não pode torná-la futura nem posterior à `EndDate` (se fechada). | DTO + Service |
| RN-10 | Isolamento de tenant: repositório sob global query filter por `PropertyId`. | Repository / DbContext |
| RN-11 | Bloco `InitialLactation` no cadastro do animal só é aceito para `Classification` ∈ {`Cow`, `Heifer`}; caso contrário → `422` (D17). | `AnimalService.CreateAsync` |
| RN-12 | Seed embutido (D17): `StartDate` obrigatória dentro do bloco e não futura; `EndDate` opcional em `[StartDate, hoje]`. `EndDate` presente → lactação **fechada** (`Origin = InitialSeed`); ausente → **aberta**. `CalvingId = null`. | DTO + `AnimalService` |

### 6.7 Integração com Parto e com o Animal

**Abertura por parto (D10) — `AnimalCalvingService`:**
- `CreateAsync(pregnancyId, dto)`: **antes** de criar o parto, aplicar RN-05 (bloquear se houver lactação aberta). Após criar o parto, abrir `Lactation { StartDate = CalvingDate, CalvingId = calving.Id, Origin = Calving }`.
- `InactivateAsync(id)`: ao inativar o parto, localizar a lactação por `CalvingId` e inativá-la (RN-07).
- Consequência: `AnimalCalvingService` passa a depender de `ILactationRepository` (e/ou `ILactationService`). Ajuste de construtor/serviço e **registro em `Program.cs`** — *requer aprovação na implementação*.

**Status produtivo no Animal (D18 — em escopo):** espelhar o `ReproductiveStatus`. Expor no `AnimalDto` (detalhe) e no `AnimalListItemDto` (lista, set-based via `ProductiveStatusResolver`):
- `ProductiveStatus` (`EnumValueDto?`) — `Nunca lactou` / `Em lactação` / `Seca`, derivado do histórico (§6.3);
- `DaysInMilk` (`int?`) — DEL da lactação **aberta**, quando `Lactating`; `null` caso contrário.

Ambos calculados na mesma passada que já resolve o `ReproductiveStatus` no `AnimalService`, para lista e detalhe classificarem igual. Surgem apenas para a audiência de fêmeas (Vaca/Novilha); `null` para as demais classificações.

### 6.8 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `Lactation.cs` | **Criar** |
| `Domain/Enums` | `LactationOrigin.cs`, `ProductiveStatus.cs` | **Criar** |
| `Application/DTOs` | `LactationCreateDto`, `LactationSeedDto`, `LactationDryOffDto`, `LactationUpdateDto`, `LactationDto`, `LactationListItemDto` | **Criar** |
| `Application/DTOs` | `AnimalCreateDto` | **Editar** (add `InitialLactation` opcional — D17) |
| `Application/DTOs` | `AnimalDto`, `AnimalListItemDto` | **Editar** (add `ProductiveStatus` + `DaysInMilk` — D18) |
| `Application/Services` | `AnimalService.cs` | **Editar** (semear lactação no create — D17/RN-11/RN-12; resolver `ProductiveStatus` — D18) |
| `Application/Mappings` | `AnimalProfile.cs` | **Editar** (mapear `ProductiveStatus` como `EnumValueDto` — D18) |
| `Application/Helpers` | `ProductiveStatusResolver.cs` | **Criar** |
| `Application/Mappings` | `LactationProfile.cs` | **Criar** |
| `Application/Interfaces` | `ILactationService`, `ILactationRepository` | **Criar** |
| `Application/Services` | `LactationService.cs` | **Criar** |
| `Application/Services` | `AnimalCalvingService.cs` | **Editar** (abrir/inativar lactação — D10/RN-05/RN-07) |
| `Infrastructure/Repositories` | `LactationRepository.cs` | **Criar** (inclui "buscar lactação aberta por animal") |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | `DbSet<Lactation>`, query filter por `PropertyId`, índices |
| `Api/Controllers` | `LactationsController.cs` | **Criar** rotas do §6.5 |
| `Program.cs` | DI | Registrar repositório e serviço; injetar em `AnimalCalvingService` *(requer aprovação)* |
| `Infrastructure/Migrations` | *(ver §7)* | **Requer aprovação antes de criar** |

---

## 7. Notas de Migração

> **Requer aprovação explícita antes de executar.**

**Criar tabela `Lactations`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `AnimalId` | int | FK → `Animals`, not null |
| `StartDate` | datetime2 | not null |
| `EndDate` | datetime2 | **nullable** (null = em lactação) |
| `CalvingId` | int | **nullable**, FK → `AnimalCalvings` |
| `Origin` | int | not null |
| `DryOffNotes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` | datetime2 | not null |
| `UpdatedAt` | datetime2 | nullable |

**Índices:**
| Colunas | Tipo | Motivo |
|---------|------|--------|
| `(PropertyId)` | Simples | Isolamento de tenant (padrão do projeto). |
| `(PropertyId, AnimalId, EndDate)` | Composto | Localizar rapidamente a lactação aberta (`EndDate IS NULL`) e o histórico por animal. |
| `(CalvingId)` | Simples | Elo ao parto (D10); usado na inativação (RN-07). |

> FKs com `OnDelete(DeleteBehavior.Restrict)` (padrão do projeto; soft delete preferido).
> Migração sugerida: `Spec11_2_Lactation`.

---

## 8. Onboarding de lactações pré-existentes (D11/D17)

**Caminho primário — embutido no cadastro do animal (D17).** Ao registrar uma **Vaca** ou **Novilha**, a UI exibe a flag **"já lactou?"**. Marcada, revela os campos: "Desde quando? (data de início — obrigatória) Já secou? (data da secagem — opcional)". Ou seja, **se a flag está marcada, sempre haverá `StartDate`**; o backend exige `StartDate` sempre que o bloco `InitialLactation` chegar (a flag é o gatilho de UI, não um campo persistido — o "já lactou" é reconstituível do próprio `ProductiveStatus`).

1. `POST /api/animals` com `Classification = Cow`/`Heifer` e o bloco opcional:
   ```jsonc
   "initialLactation": {
     "startDate": "2026-02-10",          // data da última lactação (parto) — obrigatória no bloco
     "endDate": "2026-08-20"             // opcional: data da secagem
   }
   ```
2. Na mesma transação nasce uma `Lactation` `InitialSeed`, `CalvingId = null`:
   - **com** `endDate` → **fechada** ⇒ a vaca está **seca** (histórico de ciclo encerrado, nenhuma lactação aberta);
   - **sem** `endDate` → **aberta** ⇒ a vaca está **em lactação**.
3. Se o bloco não for informado, **nenhuma** lactação é criada (opcional para ambas as classificações).

**Caminho avulso.** `POST /api/animals/{animalId}/lactations` (`LactationCreateDto`) para semear/adicionar depois — abre uma lactação (respeitando a invariante de uma aberta por animal). Onboarding em **lote** é conveniência futura opcional.

**Por que `StartDate` é obrigatória quando o bloco existe (D11):** os índices futuros usam a **data**, não o objeto Parto. Uma vaca marcada como em lactação **sem** `StartDate` seria contada na proporção mas sumiria de `cowLactationDays` (a fórmula de sobreposição exige um início), inflando artificialmente a média por vaca. Sem `StartDate`, não se cria a lactação.

**Auto-cura:** o efeito do onboarding afeta **só o primeiro ciclo** de cada vaca. Ao ser secada (fecha) e parir de novo (nova lactação, agora com `CalvingId`), o animal passa a ser 100% event-driven.

> **Novilha primípara:** uma novilha semeada com lactação passa, na prática, a ter histórico de lactação; a classificação em si não muda automaticamente (fora de escopo).

---

## 9. Fora do Escopo desta Spec

- **Índices agregados de rebanho** (média por vaca em lactação, proporção de vacas em lactação, produção do rebanho por período) → **spec de indicadores**. (D15 já fixa a fronteira `[StartDate, EndDate]` que a fórmula usará.)
- **Terapia medicada da secagem** (antibiótico/selante) → reutiliza `AnimalMedication` se necessário (D16).
- **`MilkYield` / medição individual** (Spec #11, D7).
- **Ordem de lactação / paridade** (quantas vezes pariu) — não modelada aqui.
- **Marcador de data estimada** (`StartDateEstimated`) e a sinalização de "DEL aproximado" na UI → **decisão futura** (removido desta versão a pedido).
- **Offline-first / GUID de cliente / upsert idempotente** (Spec #11, D9) → evolução aditiva futura.
- **Curva de lactação / pico de produção / persistência.**

---

## 10. Decisões em aberto remanescentes

- **O2 — Colostro (Spec #11).** A vaca é "em lactação" desde o parto. Se o colostro dos primeiros dias entra ou não no **total registrado** de leite é decisão de **lançamento do produtor** (11.1), não do modelo de lactação. *Sem impacto no modelo desta spec.*
- **O4 — Nulípara × seca (Spec #11).** **Resolvido nesta spec (D18):** o `ProductiveStatus` distingue `Nunca lactou` (sem histórico) de `Seca` (histórico encerrado). Para as **contagens/denominadores** dos índices futuros, porém, ambas continuam valendo como "não lactante" — a distinção é de **exibição/status**, não muda `cowLactationDays`.

---

## 11. Referências

Herdadas do Spec #11 (§9): Embrapa Gado de Leite (Período Seco; terço médio/final da lactação), MilkPoint/EducaPoint (período de lactação e DEL; curva de lactação), JA Saúde Animal (terapia da vaca seca), UFPel/SIEPE (avaliação de DEL).
