# Spec: Ajustes Pontuais — Dashboard Geral (Home) e recepção ao offline-first

**Módulo:** Ajustes transversais / Dashboards
**Versão:** 1.1
**Data:** 15/Set/2026
**Fonte:** Ajuste pós-desenvolvimento — revisão interna
**Status:** Aprovado para implementação
**Ajusta:** Spec #7 / #18 (Dashboards) — rota `GET /api/dashboard` (dashboard geral / home) e `GET /api/dashboard/reproductive` (previsão de partos, Ajuste 6)
**Depende de:** #14 (Vacinação), #16 (Tratamentos/Mastite), #6.1 (Gestação) — resolvers/repositórios já implementados (`VaccinationEventStatusResolver`, `AnimalSanitaryStatusResolver`, `IAnimalPregnancyRepository`)
**Referenciado por:** —
**Histórico:** v1.1 — adiciona a previsão de partos ao dashboard reprodutivo (Ajuste 6, cumpre a "estimativa de parto" prevista no #18 US-02/CU-02, nunca implementada) e retira o *stub* vazio de `BirthForecast` da home, que passa a viver no reprodutivo (D9 revisada).

---

## 1. Contexto e Objetivo

A rota `GET /api/dashboard` é a **porta de entrada do app** — a home com os dados gerais da propriedade, ao lado dos dashboards produtivo (`/productive`), reprodutivo (`/reproductive`) e de estoque (`/api/stock/dashboard`). Ela já existe e devolve:

- `Cards` — total de animais e animais em tratamento;
- distribuição por gênero e por raça;
- vacinas por mês (série dos últimos 12 meses);
- previsão de partos (`BirthForecast`, hoje um *stub* vazio).

Dois problemas motivam este ajuste:

1. **Não é amigável ao offline-first.** Diferente do que o #18 fez com os dashboards produtivo e reprodutivo (D2/D17: derivação na leitura por resolver puro sobre fatos brutos, set-based, endpoint autossuficiente), a home ainda **agrega no SQL** (`GROUP BY` de gênero/raça/vacinas dentro do `DashboardRepository`). Número agregado no banco **não** é recomputável no cliente com o mesmo código, então quebra a via aditiva do offline-first do #18 §8 (o mesmo resolver rodar sobre o cache local Room quando não há rede).

2. **Dois indicadores estão defasados dos eixos atuais.** "Animais em tratamento" ainda conta a tabela antiga `AnimalMedication` por `EndDate`, em vez do eixo sanitário do #16 (que é a fonte de verdade do estado de tratamento exibido na ficha e na lista). "Vacinas por mês" precisa ser recomputada consistentemente sobre os eventos de vacinação do #14.

**Objetivo:** reestruturar `GET /api/dashboard` para o padrão offline-first do #18 (mesmos dados, produzidos por resolvers puros sobre fatos brutos, resposta autossuficiente), corrigir os dois indicadores defasados e adicionar dois índices de nível-propriedade que **não** pertencem aos dashboards produtivo/reprodutivo/estoque: **distribuição por classificação** e **vacinas atrasadas**. Adicionalmente, implementar a **previsão de partos** — um índice reprodutivo de rebanho — no **dashboard reprodutivo** (Ajuste 6), cumprindo o que o #18 US-02/CU-02 previu ("estimativa de parto") e que nunca foi implementado.

> **Relação com o dashboard sanitário do #18.** Os dois índices sanitários desta home — **animais em tratamento** (`UnderTreatment`) e **vacinas atrasadas** (`Overdue`) — são um **recorte de nível-propriedade** do dashboard sanitário que o #18 especifica como **opcional e não priorizado** (D11 / §6.4 / CU-06 `GET /api/dashboard/sanitary`). Aqui eles entram como cards de atenção da porta de entrada, reusando exatamente os mesmos resolvers do eixo sanitário (`AnimalSanitaryStatusResolver`, `VaccinationEventStatusResolver`). Se o dashboard sanitário completo do #18 for priorizado depois, estes cards reusam o mesmo caminho de derivação — sem retrabalho nem lógica divergente.

**Princípio herdado (do #18):** nunca armazenar estado derivável; tudo calculado na leitura sobre fatos brutos; set-based sem N+1; escopo por `PropertyId`; a home é somente-leitura.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Reestruturar a resposta para **derivação na leitura por resolvers puros sobre fatos brutos set-based**, em vez de `GROUP BY` no SQL. O repositório entrega os fatos brutos; um resolver/serviço projeta os números. | É o padrão do #18 (D2/D17). O mesmo resolver roda depois sobre o cache local (Room) no offline-first (#18 §8) — sem reescrita. Agregar no SQL trava esse caminho. |
| D2 | Organizar a resposta em uma **zona "Agora"** (estado atual, *clock-sensitive*) mais a **série histórica de vacinas**. A home **não** tem seletor de período. | #18 D13: os índices de estado atual recomputam na virada do dia / ao abrir o app. A home é um pulso da propriedade; janela por período (`dateFrom`/`dateTo`) fica nos dashboards especializados (#18 D4). |
| D3 | Endpoint **autossuficiente**: cada card de atenção (em tratamento, vacinas atrasadas) traz a **contagem + a lista de drill-down embutida** numa única resposta. | #18 D17 + ambiente instável: evita requisições de follow-up com internet ruim; o cliente já tem a lista para abrir offline. |
| D4 | **"Animais em tratamento"** passa a derivar do eixo sanitário do #16: animais com `SanitaryStatus = UnderTreatment`, via `AnimalSanitaryStatusResolver` sobre `GetSanitaryFactsMapAsync`. Deixa de contar `AnimalMedication.EndDate`. | O #16 é a fonte de verdade atual do estado de tratamento (mesma derivação da ficha e da lista de animais). A contagem antiga por `EndDate` diverge do status sanitário exibido no resto do app. |
| D5 | **"Vacinas por mês"** passa a contar eventos **`Applied`** (`VaccinationEventStatusResolver`) sobre `VaccinationEvent` (#14), por mês de `ApplicationDate`, nos últimos 12 meses. | Mesma fonte de verdade do eixo de vacinação; classificação idêntica à do resto do #14; derivável dos fatos brutos no cliente. |
| D6 | Adicionar **distribuição por classificação** do rebanho (`Calf`/`Heifer`/`Steer`/`Bull`/`Cow`). | Composição real do rebanho leiteiro; complementa gênero/raça já existentes; é nível-propriedade e não vive em nenhum dos dashboards produtivo/reprodutivo/estoque. |
| D7 | Adicionar **vacinas atrasadas** (`Overdue`) com contagem + lista embutida. | Pendência sanitária de propriedade; complementa "vacinas por mês" (aplicadas) com o que está pendente. Não há dashboard sanitário implementado que já cubra isso. |
| D8 | **Sem alteração de schema e sem migração.** | Todos os índices são derivados de dados já existentes (`Animal.Classification`, `VaccinationEvent`, `HealthCase`/`AnimalMedication`). Coerente com a natureza "ajuste pontual". |
| D9 | **`BirthForecast` sai da home e é implementado no dashboard reprodutivo** como previsão de partos de rebanho (Ajuste 6). O *stub* vazio da home é removido. | A previsão de partos é um índice reprodutivo de rebanho (fonte `AnimalPregnancy.ExpectedCalvingDate`, #6.1) — pertence ao dashboard reprodutivo (#18 US-02/CU-02), não à home. Manter um *stub* sempre vazio na home seria duplicar o conceito e deixar código morto. Nenhum dado real é removido da home (o campo sempre devolvia `[]`). |
| D10 | A reestruturação **preserva todos os dados hoje apresentados** — só muda como são produzidos e como são agrupados na resposta. | O pedido é reestruturar para offline-first e acrescentar, não remover conteúdo. |
| D11 | Os índices sanitários da home (tratamentos, vacinas atrasadas) são um **recorte de propriedade** do dashboard sanitário opcional do #18 (D11/§6.4), reusando os mesmos resolvers. Se o dashboard sanitário completo for priorizado, estes cards reusam o mesmo caminho. | Evita lógica divergente e dívida técnica: um único caminho de derivação sanitária serve à home agora e ao dashboard sanitário depois (mesma via aditiva do offline-first, #18 §8). |

---

## 3. Ajuste 1 — Reestruturação offline-first da `GET /api/dashboard`

### 3.1 O que muda

Não muda o *conteúdo*, muda a *produção* e a *organização*:

1. **Agregação sai do SQL e vira derivação na leitura sobre fatos brutos set-based** (D1). O `DashboardRepository` deixa de fazer `GROUP BY` e passa a entregar os fatos (animais ativos com seus campos de classificação/gênero/raça; eventos de vacinação; fatos sanitários), e o `DashboardService` projeta as distribuições e contagens — o mesmo padrão dos resolvers do #18, apto a rodar sobre o cache local no offline (#18 §8).
2. **Zona "Agora"** (D2): os índices de estado atual ficam agrupados e são *clock-sensitive* (recomputam na virada do dia). A série de vacinas por mês fica à parte como histórico.
3. **Autossuficiência** (D3): tratamentos e vacinas atrasadas trazem as listas de drill-down embutidas.

### 3.2 Contrato da rota

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/dashboard` | Home da propriedade: composição do rebanho, pulso sanitário (tratamentos, vacinas atrasadas), previsão de partos e série de vacinas por mês. Sem parâmetros. Escopo por `PropertyId`. | `200 DashboardDto` |

### 3.3 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `DashboardDto.cs` | Reestruturar (ver §8): agrupar composição do rebanho e pulso sanitário; adicionar classificação e vacinas atrasadas. |
| `Application/Interfaces` | `IDashboardRepository.cs` | Trocar os métodos de `GROUP BY` por métodos que entregam **fatos brutos set-based** (animais ativos; eventos de vacinação da janela; ids para os fatos sanitários). |
| `Infrastructure/Repositories` | `DashboardRepository.cs` | Remover as agregações SQL; entregar os fatos brutos (mantendo o escopo por `PropertyId`). |
| `Application/Services` | `DashboardService.cs` | Compor as distribuições e contagens em memória a partir dos fatos (reusando `AnimalSanitaryStatusResolver` e `VaccinationEventStatusResolver`); montar o `DashboardDto`. |
| `Api/Controllers` | `DashboardController.cs` | Manter `GET /api/dashboard`; nenhuma mudança de assinatura. |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | Cobrir as novas projeções (ver cada ajuste abaixo). |

---

## 4. Ajuste 2 — Distribuição por classificação (novo)

### 4.1 Regra

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | Contar os animais **ativos** por `Classification` (`Calf`/`Heifer`/`Steer`/`Bull`/`Cow`), com rótulo em português via `[Description]`. Escopo por `PropertyId`. | `DashboardService` (projeção sobre os animais ativos) |

Segue o mesmo formato das distribuições existentes (gênero/raça): membro do enum + label + contagem.

### 4.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `DashboardDto.cs` | Adicionar `ClassificationDistributionDto` (`Classification`, `Label`, `Count`) e o campo na composição do rebanho. |
| `Application/Services` | `DashboardService.cs` | Projetar a distribuição a partir dos animais ativos (fato bruto). |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | `GetDashboardAsync_GroupsActiveAnimalsByClassification`. |

---

## 5. Ajuste 3 — "Animais em tratamento" refeito sobre o eixo sanitário (#16)

### 5.1 Regra

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-02 | "Animais em tratamento" = animais **ativos** cujo `SanitaryStatus` resolvido é `UnderTreatment`, via `AnimalSanitaryStatusResolver` sobre os fatos de `IHealthCaseRepository.GetSanitaryFactsMapAsync(animalIds, utcNow)`. Deixa de contar `AnimalMedication.EndDate`. Retornar **contagem + lista embutida** de drill-down (`AnimalListItemDto`). | `DashboardService` (reusa o caminho sanitário do #16) |

> **Nota:** é o mesmo caminho de derivação usado na lista de animais e na ficha — garante que o número da home bata com o status sanitário exibido no resto do app. `utcNow` (relógio do servidor) é o "hoje" *clock-sensitive* (recomputa na virada do dia).

### 5.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `DashboardDto.cs` | Substituir o `int ActiveTreatments` por `AnimalsUnderTreatmentDto` (`Count`, `Animals: IEnumerable<AnimalListItemDto>`) dentro do pulso sanitário. |
| `Application/Services` | `DashboardService.cs` | Injetar `IHealthCaseRepository`; reunir os ids dos animais ativos; chamar `GetSanitaryFactsMapAsync`; resolver `UnderTreatment` via `AnimalSanitaryStatusResolver`; montar contagem + lista. |
| `Infrastructure/Repositories` | `DashboardRepository.cs` | Remover a contagem por `AnimalMedication.EndDate`. |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | `GetDashboardAsync_CountsAnimalsUnderTreatmentFromSanitaryAxis`, incluindo a lista embutida. |

---

## 6. Ajuste 4 — "Vacinas por mês" refeito sobre os eventos de vacinação (#14)

### 6.1 Regra

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-03 | "Vacinas por mês" = nº de **doses aplicadas** (uma por animal **ativo** em cada evento `Applied`, classificado por `VaccinationEventStatusResolver`), agrupadas pelo mês de `ApplicationDate`, nos **últimos 12 meses**, escopo por `PropertyId`. Preserva a métrica atual (doses/mês), mas ancorada nos eventos brutos + resolver (não `GROUP BY` no SQL). | `DashboardService` (projeção sobre os eventos de vacinação) |

> Mantém a métrica hoje apresentada (doses aplicadas por mês), mas ancorada na mesma fonte de verdade e classificação do eixo de vacinação (#14), e derivável dos fatos brutos no cliente.

### 6.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/Interfaces` | `IDashboardRepository.cs` | Adicionar leitura dos eventos de vacinação (`VaccinationEvent` + `EventAnimals`) dos últimos 12 meses como fato bruto. |
| `Infrastructure/Repositories` | `DashboardRepository.cs` | Substituir o `GROUP BY` de `VaccinationEventAnimals` pela entrega dos eventos brutos da janela. |
| `Application/Services` | `DashboardService.cs` | Classificar via `VaccinationEventStatusResolver` (contar só `Applied`) e agrupar por mês. |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | `GetDashboardAsync_CountsAppliedVaccinationsPerMonth`. |

---

## 7. Ajuste 5 — Vacinas atrasadas (novo)

### 7.1 Regra

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-04 | "Vacinas atrasadas" = eventos de vacinação **ativos** classificados como **`Overdue`** por `VaccinationEventStatusResolver` (`PredictedDate` no passado, sem `ApplicationDate`). Escopo por `PropertyId`. Retornar **contagem + lista embutida** de drill-down por evento. | `DashboardService` (projeção sobre os eventos de vacinação) |

> É *clock-sensitive* (o mesmo evento vira `Overdue` na virada do dia sem dado novo) — recomputa por data no cache local (#18 §8/D13).

### 7.2 DTO de item

```csharp
public class OverdueVaccinationItemDto
{
    public int VaccinationEventId { get; set; }
    public string VaccineName { get; set; } = string.Empty;
    public DateTime PredictedDate { get; set; }
    public int AnimalCount { get; set; }
}
```

### 7.3 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `DashboardDto.cs` | Adicionar `OverdueVaccinationsDto` (`Count`, `Events: IEnumerable<OverdueVaccinationItemDto>`) e `OverdueVaccinationItemDto`. |
| `Application/Services` | `DashboardService.cs` | Reusar os eventos brutos de vacinação já lidos (RN-03); filtrar `Overdue` via `VaccinationEventStatusResolver`; montar contagem + lista. |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | `GetDashboardAsync_ListsOverdueVaccinations`. |

---

## 7b. Ajuste 6 — Previsão de partos no dashboard reprodutivo (#18 US-02/CU-02)

O #18 previu "estimativa de parto" como critério do dashboard reprodutivo (US-02/CU-02), mas o `ReproductiveDashboardDto` foi entregue sem esse índice. A previsão de partos **de rebanho** não existe em nenhum dashboard hoje; só a versão **por animal** (`nextCalving` na ficha, #18 §6.2.1) e a data na listagem de gestações. Este ajuste materializa a versão agregada, no `GET /api/dashboard/reproductive`.

### 7b.1 Regra

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-05 | "Previsão de partos" = lista das gestações **ativas e confirmadas** (`AnimalPregnancy.IsActive && Status = Confirmed`) de **animais ativos**, ordenadas por `ExpectedCalvingDate` ascendente. A data reusa `AnimalPregnancy.ExpectedCalvingDate` já persistida (a gestação é a fonte de verdade — #6.1 D2; não recalcula `BreedingDate + 280`). Cada item embute o `PregnancyId` para drill-down da gestação (mesmo racional do `nextCalving`, #18 §6.2.1). Escopo por `PropertyId`. | `DashboardService.GetReproductiveDashboardAsync` |

> Lista completa (sem janela), no mesmo padrão das demais listas do dashboard reprodutivo (ex.: elegíveis para IA). Set-based: uma query com `Include(Animal)`. Índice *forward-looking* — não obedece ao período da aba (é estado atual, como os demais "Agora" do #18 D13).

### 7b.2 DTO de item

```csharp
public class CalvingForecastItemDto
{
    public int AnimalId { get; set; }
    public string? Name { get; set; }
    public string? TagNumber { get; set; }
    public int PregnancyId { get; set; }
    public DateTime ExpectedCalvingDate { get; set; }
}
```

### 7b.3 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `ReproductiveDashboardDto.cs` | Adicionar `CalvingForecastItemDto` e o campo `List<CalvingForecastItemDto> CalvingForecast` no `ReproductiveDashboardDto`. |
| `Application/Interfaces` | `IAnimalPregnancyRepository.cs` | Adicionar `Task<IEnumerable<AnimalPregnancy>> GetActiveConfirmedForForecastAsync()`. |
| `Infrastructure/Repositories` | `AnimalPregnancyRepository.cs` | Implementar a consulta set-based (`Include(Animal)`, ativas + confirmadas + animal ativo, ordenadas por `ExpectedCalvingDate`). |
| `Application/Services` | `DashboardService.cs` | Em `GetReproductiveDashboardAsync`, buscar as gestações e projetar `CalvingForecast`. |
| `MuuBoi.Tests` | `DashboardServiceTests.cs` | `GetReproductiveDashboardAsync_ListsActiveConfirmedPregnanciesAsCalvingForecast`. |

---

## 8. DTO resultante (visão consolidada)

Estrutura-alvo do `DashboardDto` (mesmos dados de hoje + os dois acréscimos, reagrupados para a zona "Agora"):

```csharp
public class DashboardDto
{
    // Zona "Agora" — estado atual, clock-sensitive
    public HerdCompositionDto Herd { get; set; } = new();
    public SanitaryPulseDto Sanitary { get; set; } = new();

    // Série histórica (últimos 12 meses)
    public IEnumerable<VaccinePerMonthDto> VaccinesPerMonth { get; set; } = [];   // refeito — RN-03
}
// BirthForecast removido da home (D9); a previsão de partos vive no ReproductiveDashboardDto (Ajuste 6).

public class HerdCompositionDto
{
    public int TotalAnimals { get; set; }
    public IEnumerable<ClassificationDistributionDto> ClassificationDistribution { get; set; } = [];  // novo — RN-01
    public IEnumerable<GenderDistributionDto> GenderDistribution { get; set; } = [];   // preservado
    public IEnumerable<BreedDistributionDto> BreedDistribution { get; set; } = [];     // preservado
}

public class SanitaryPulseDto
{
    public AnimalsUnderTreatmentDto UnderTreatment { get; set; } = new();       // refeito — RN-02
    public OverdueVaccinationsDto OverdueVaccinations { get; set; } = new();    // novo — RN-04
}

public class AnimalsUnderTreatmentDto
{
    public int Count { get; set; }
    public IEnumerable<AnimalListItemDto> Animals { get; set; } = [];   // drill-down embutido
}

public class OverdueVaccinationsDto
{
    public int Count { get; set; }
    public IEnumerable<OverdueVaccinationItemDto> Events { get; set; } = [];   // drill-down embutido
}
```

E no `ReproductiveDashboardDto` (Ajuste 6):

```csharp
public class ReproductiveDashboardDto
{
    // ... campos existentes ...
    public List<CalvingForecastItemDto> CalvingForecast { get; set; } = new();   // novo — RN-05
}
```

`ClassificationDistributionDto`, `GenderDistributionDto`, `BreedDistributionDto`, `VaccinePerMonthDto`, `OverdueVaccinationItemDto` e `CalvingForecastItemDto` seguem o padrão de item do arquivo (membro do enum quando houver + label + contagem/campos).

> Nota de compatibilidade: o reagrupamento (`Cards` → `Herd`/`Sanitary`) e a remoção de `BirthForecast` mudam o formato da resposta da home; o `ReproductiveDashboardDto` ganha `CalvingForecast`. Como o front mobile ainda consome estes dashboards, alinhar as mudanças com o cliente antes de publicar.

---

## 9. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

**Nenhuma alteração de schema é necessária** (D8). Todos os índices são derivados de dados já existentes:

- distribuição por classificação lê `Animal.Classification`;
- "animais em tratamento" e vacinas (aplicadas/atrasadas) derivam de `HealthCase`/`AnimalMedication` e `VaccinationEvent`, já persistidos;
- a previsão de partos (Ajuste 6) lê `AnimalPregnancy.ExpectedCalvingDate`, já persistida (#6.1);
- a reestruturação move a agregação do SQL para a camada de serviço — não toca no banco.

Nenhuma migration é necessária.

---

## 10. Fora do Escopo deste Spec

- **Dashboards produtivo, reprodutivo e sanitário** → Spec #18 (a home apenas complementa, não os duplica). O **dashboard sanitário completo** (`GET /api/dashboard/sanitary`: distribuição por `SanitaryStatus`, mastite por período/quarto, próximos reforços, métricas de #16 §9) permanece opcional e **não priorizado** no #18 (D11/§6.4); este spec só traz o recorte de propriedade (tratamentos + vacinas atrasadas), reusando os mesmos resolvers (D11).
- **Dashboard de estoque** → Spec #17 (`/api/stock/dashboard`).
- **Seletor de período na home** → decidido fora por D2/D8 (a home é snapshot "Agora"; janela por período vive nos dashboards especializados, #18 D4).
- **Janela na previsão de partos** → o Ajuste 6 lista todas as gestações ativas confirmadas (sem janela), no padrão das demais listas do reprodutivo; limitar a "próximos N dias" fica como melhoria futura se a lista crescer demais.
- **Habilitar de fato o offline-first (cache Room, sync)** → evolução futura aditiva (#18 §8); aqui só se garante que a rota seja *recepcionável* a ele (derivação na leitura por resolver puro sobre fatos brutos).
- **Movimentação do rebanho, leite retido, faixa etária** → sugeridos e não priorizados para este spec; ficam como candidatos futuros.
