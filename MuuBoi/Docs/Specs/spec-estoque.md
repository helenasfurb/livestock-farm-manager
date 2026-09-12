# Spec: Estoque de Insumos (`StockItem` / `StockMovement`)

**Módulo:** Estoque — insumos e movimentações (Spec #17)
**Versão:** 1.0
**Data:** 11/Set/2026
**Fonte:** Spec de análise "Estoque e dieta" (offline-first) — reenquadrada para o padrão server-side do MuuBoi.
**Status:** Especificada (não implementada)
**Depende de:** Nenhum eixo — módulo independente. Reutiliza infra transversal (`BaseEntity`, `ITenantEntity`, tenant no repositório, `ExceptionMiddleware`, helpers estáticos em `Application/Helpers/`, `EnumValueDto`/`DateFormatConverter`).
**Referência de padrão:** Controle de doses do Banco de Sêmen (`SemenSampleMovement`) — o ledger de estoque segue o mesmo desenho (saldo = Σ entradas − Σ saídas, soft delete, FK `Restrict`, índices de agregação).

> **Decisões de escopo desta spec (11/Set):**
> - **Server-side agora** (`Id int`, `PropertyId`, `BaseEntity`, soft delete, resolvers como **helpers estáticos**). Offline-first fica como **evolução futura aditiva** (§8) — mesmo racional das specs de Vacinação, 11.1 e Tratamentos.
> - **Categoria e Unidade são tabelas de referência semeadas, sem CRUD** (globais, read-only) — não enums (D3). Editabilidade por produtor é evolução futura (§8, §9).
> - **Controle financeiro completo** (D7 + D9): compras gravam `TotalValue`; saídas congelam `UnitCostSnapshot` por **média ponderada móvel**; saldo valorizado em R$.
> - **Alerta por ponto crítico agora** (D12); **previsão de ruptura** (R5) enriquece a leitura quando há histórico.
> - **Só insumos** (o produtor decide o que cadastrar); **sem acoplamento** com os eixos produtivo/reprodutivo/sanitário (D11). Nenhuma baixa automática — toda saída é lançada à mão.

---

## 1. Contexto e Objetivo

O eixo de estoque controla **insumos e suprimentos da propriedade** — de nutrição animal (ração, feno, sal mineral) a operacionais (combustível, produtos de limpeza, peças de manutenção) — e suas **movimentações** (entradas e saídas, com valor). O produtor quer, por período, saber **quanto consumiu de cada insumo**, **quanto gastou no total** e **quanto ainda resta** — além de prever quando o estoque acaba e ser avisado quando fica baixo.

O módulo é um **ledger append-friendly**: cada movimento é um fato (compra, saldo inicial, consumo, perda, ajuste). Saldo, valor em estoque, ritmo de consumo e previsão de ruptura são **derivados na leitura** por helper estático — nunca colunas persistidas. Dashboard e avisos são requisito upfront, não extra.

Princípios (herdados do MuuBoi e da análise):

- **Nunca armazenar estado derivável** — saldo/valor/previsão/alerta resolvidos na leitura, set-based (padrão `AnimalSanitaryStatusResolver` / `ReproductiveStatusResolver`).
- **Sem jobs agendados** — a virada "acima → abaixo do ponto crítico" é derivada comparando o saldo do relógio com o ponto crítico cadastrado.
- **Só materializar fatos imutáveis** — o `TotalValue` da compra e o `UnitCostSnapshot` da saída são fatos congelados no lançamento.
- **Ortogonalidade estrita** (D11) — o eixo não escreve em produção/reprodução/sanitário; cruzamentos (ex.: custo por litro de leite) são JOIN de leitura, fora do v1.
- **Retroativo é normal** — onboarding entra como `SaldoInicial`, não como compra fabricada.

**Convenção de tempo:** granularidade de **dia**. `MovementDate` é `DateTime` (datetime2) comparado por `.Date`; deve ser `<= hoje`.

---

## 2. Escopo

**Dentro:**
- Entidade **`StockItem`** (o insumo/suprimento): nome, categoria (FK), unidade (FK), ponto crítico e tempo de reposição opcionais. **Sem** coluna de saldo nem de valor (ambos derivados). O que cadastrar é decisão do produtor (nutrição, limpeza, combustível, manutenção, etc.).
- Entidade **`StockMovement`** (o ledger): tipo (`Input`/`Output`), motivo (`Purchase`/`OpeningBalance`/`Consumption`/`Loss`/`Adjustment`), quantidade `decimal`, data, valor total (entradas) e custo unitário congelado (saídas).
- Tabelas de referência **`StockCategory`** e **`UnitOfMeasure`** — globais, semeadas por migration, expostas só como **lookup de leitura** (sem CRUD).
- **Saldo, valor em estoque, ritmo, cobertura, data de ruptura e severidade de alerta derivados na leitura** por `StockForecastResolver` (helper estático), set-based (sem N+1).
- **Dashboard** (3 números em R$ + tabela por insumo com filtro por categoria) e **tela de avisos** (insumos no/abaixo do ponto crítico).
- CRUD de `StockItem` e de `StockMovement` com **soft delete** e isolamento de tenant no repositório.

**Fora (→ §9 / futuro):**
- **Eixo de dieta / receitas** que gerariam saídas `Consumption` automaticamente — dependência unidirecional dieta → estoque; este spec é autossuficiente (D11).
- **Baixa automática** ao aplicar medicação/vacina — sem acoplamento (Q4). Medicamentos/vacinas seguem nos catálogos existentes.
- **Edição/desativação de categoria/unidade pelo produtor** (CRUD) — read-only no v1 (D3).
- **Conversão entre unidades** — nunca converte; formas físicas distintas são insumos distintos (D4).
- **FIFO / rastreio lote a lote** na valoração — usa média ponderada móvel (D9).
- **Notificação com app fechado** e **sincronização offline-first** — evolução futura aditiva (§8).

---

## 3. Decisões

| # | Decisão | Origem / Motivo |
|---|---------|-----------------|
| D1 | **Estoque é um ledger** — cada movimento é uma linha em `StockMovement`. O saldo nunca é coluna; deriva de `Σ entradas − Σ saídas` sobre linhas **ativas**. | Espelha o controle de doses do sêmen (`SemenSampleMovement`). Fonte única de verdade, sem contador mutável a reconciliar; converge trivialmente quando o offline entrar (§8). |
| D2 | **Saldo, valor, ritmo, cobertura e previsão são derivados na leitura** por `StockForecastResolver` (helper estático puro); o serviço reúne os fatos **set-based** (bulk), sem N+1. | Padrão real do projeto ("never store derived", "no scheduled jobs", "bulk over N+1"). A previsão usa `hoje` (relógio) contra as datas do ledger. |
| D3 | **`StockCategory` e `UnitOfMeasure` são tabelas de referência globais, semeadas por migration, sem CRUD** (expostas só como lookup de leitura). `StockItem` referencia ambas por FK. | Unidade/categoria variam por produtor, mas o v1 não precisa de edição — tabela (não enum) evita redeploy para valores novos e já deixa o caminho para editabilidade futura (§8). Global + read-only dispensa a infra de seed por-tenant que o projeto ainda não tem, sem caso especial no query filter. |
| D4 | **Unidade canônica por insumo; formas físicas distintas são insumos distintos.** Nunca converte entre unidades. | Peso de "bola" é variável — converter bola→kg inventaria número que não existe ("explicit over inferred"). "Feno (Bola)" e "Feno a granel (Kg)" são dois cadastros. |
| D5 | **Dinheiro soma sempre; quantidade só soma dentro da mesma unidade.** Relatórios em R$ atravessam qualquer mistura de insumos; relatórios de quantidade são por insumo, na sua unidade. | `16 bolas + 5 kg` não é operação válida; `R$40 + R$40` sempre é. O R$ é o único número que unifica insumos heterogêneos. |
| D6 | **Nunca persistir valor unitário e valor total como colunas independentes.** Com `Quantity` no movimento, um deriva do outro. | Guardar os dois abre divergência por arredondamento (`33,33 × 3 ≠ 100`). "Never store derivable state". |
| D7 | **Valor da compra normalizado para `TotalValue`; `ValueEntryMode` guarda só a proveniência** (como o usuário digitou: unitário ou total). O unitário é derivado (`TotalValue / Quantity`). | O relatório de gasto soma sempre um único fato imutável (`SUM(TotalValue)`) — determinístico e imune a arredondamento. A UI mantém o toggle e mostra o outro valor ao vivo. |
| D8 | **Valoração de saída: `UnitCostSnapshot` congelado no lançamento, por média ponderada móvel** (`valor em estoque / quantidade em estoque` no instante). | Torna consumo/saldo em R$ estáveis no tempo (mês passado não muda quando o preço sobe depois). Mesma tática de "materialize only immutable facts" usada ao copiar carência no `AnimalMedication`. |
| D9 | **`Ajuste` de entrada (contagem "encontrou" estoque) é valorado ao custo médio vigente** (mesmo `UnitCostSnapshot`, gravado como `TotalValue`); `Compra` e `SaldoInicial` levam o valor informado pelo usuário. | Resolve a pergunta em aberto #3 da análise: item "achado" entra ao custo médio corrente, mantendo o valor em estoque coerente sem inventar preço de compra. |
| D10 | **Saldo inicial via `MovementReason = OpeningBalance`.** Estoque pré-existente no onboarding entra como `Input`/`OpeningBalance`, com valor de inventário — nunca como compra fabricada. | Não corromper o relatório de desembolso (R2 conta só `Purchase`). "Onboarding as opening balance". |
| D11 | **Saídas são lançadas manualmente**; `Consumption`, `Loss` e `Adjustment` são motivos próprios. O sistema **nunca** dá baixa sozinho. | Requisito do produtor. O relatório de **consumo** (R1) conta só `Consumption`; o **saldo** desconta toda saída. Sem baixa automática/agendada (viola "no scheduled jobs"). |
| D12 | **Alerta primário = ponto crítico por insumo** (`ReorderPoint`, na unidade do item), avaliado na leitura (`saldo <= ponto crítico`). Não existe entidade `Alert` persistida. **Cobertura/ruptura** (R5) enriquecem, mas não são o gatilho. | Funciona no dia 1 sem histórico e sem divisão por zero; casa com o mental model do produtor (a marca da reserva). Ponto crítico é config do usuário → estado legítimo de guardar. |
| D13 | **Movimentos suportam soft delete e PATCH de correção** (data, quantidade, valor, notas); `MovementType`/`MovementReason` **não** são editáveis (mudar a natureza = inativar e recriar). O `UnitCostSnapshot` das saídas **não é recalculado** por correções posteriores — é estimativa histórica congelada (D8). | Precedente `SemenSampleMovement` (PATCH de manual + soft delete; tipo imutável). Não recomputar snapshot é o risco documentado em D8 (não corrompe retroativamente). |
| D14 | **`StockItem`/`StockMovement` implementam `ITenantEntity`** (PropertyId, query filter no repositório). **`StockCategory`/`UnitOfMeasure` são globais** (sem PropertyId, sem query filter). | Insumos e movimentos são do tenant; as tabelas de referência são compartilhadas e read-only (D3). |
| D15 | **Criar `StockItem` com `InitialQuantity` gera automaticamente um movimento `OpeningBalance`** (com `InitialTotalValue`/`InitialNotes`), no mesmo POST. | Evita dois requests quando o insumo já existe em estoque no cadastro. Espelha `InitialQuantity` do `SemenSampleCreateDto`. |

---

## 4. Histórias de Usuário

### US-01 — Cadastrar um insumo
> **Como** produtor, **quero** cadastrar um insumo (nome, categoria, unidade), **para** controlar seu estoque.

**Critérios de aceite:**
- Informo `name`, `stockCategoryId` e `unitOfMeasureId` (obrigatórios); `reorderPoint` e `replenishmentLeadDays` opcionais.
- Opcionalmente informo `initialQuantity` (+ `initialTotalValue`, `initialNotes`) e o sistema cria um movimento `OpeningBalance` junto (D15).
- Categoria e unidade vêm dos lookups semeados; a unidade é **fixa** para o insumo (D4).

### US-02 — Registrar entrada (compra ou saldo inicial)
> **Como** produtor, **quero** registrar uma compra de insumo (data, quantidade, valor pago), **para** atualizar meu estoque e meu gasto.

**Critérios de aceite:**
- Informo `movementReason` (`Purchase` ou `OpeningBalance`), `movementDate` (`<= hoje`), `quantity` (`> 0`) e o valor: `totalValue` **ou** `unitPrice`, escolhendo `valueEntryMode`; o sistema normaliza para `TotalValue` (D7).
- A movimentação é `Input`; o saldo e o valor em estoque sobem automaticamente (derivados).

### US-03 — Registrar saída (consumo, perda ou ajuste)
> **Como** produtor, **quero** registrar a saída de insumo (consumo dos animais, perda ou acerto de contagem), **para** manter o saldo preciso.

**Critérios de aceite:**
- Informo `movementReason` (`Consumption`, `Loss` ou `Adjustment`), `movementDate` (`<= hoje`) e `quantity` (`> 0`). Notas opcionais.
- A movimentação é `Output`; o sistema **congela** o `UnitCostSnapshot` pelo custo médio vigente (D8). Saldo negativo é permitido (não bloqueia).

### US-04 — Consultar saldo, valor e histórico do insumo
> **Como** produtor, **quero** ver o saldo atual, o valor em estoque e o histórico de um insumo, **para** auditar consumo e planejar compras.

**Critérios de aceite:**
- O detalhe do insumo mostra `currentBalance` (quantidade), `stockValue` (R$), `averageUnitCost` e — quando há histórico — `daysOfCoverage`/`estimatedRunOutDate`.
- O histórico lista as movimentações ativas por `movementDate` decrescente, com tipo, motivo, quantidade e valor.

### US-05 — Dashboard e relatórios por período
> **Como** produtor, **quero** ver quanto gastei, quanto consumi e quanto tenho em estoque num período, **para** entender meus custos.

**Critérios de aceite:**
- Topo: três números em R$ — gasto do período (compras), consumo valorizado, saldo valorizado.
- Tabela por insumo (filtro por categoria): consumido e saldo, cada linha na sua unidade (D5).

### US-06 — Ser avisado de estoque baixo
> **Como** produtor, **quero** ver quais insumos estão no ponto crítico ou zerados, **para** repor antes de faltar.

**Critérios de aceite:**
- A tela de avisos lista insumos com `saldo <= reorderPoint` (D12), com severidade `Attention`/`Critical` e, quando há histórico, a data de ruptura estimada.
- O aviso some sozinho quando uma entrada recupera o saldo (recalculado na leitura).

### US-07 — Corrigir/excluir
> **Como** produtor, **quero** editar ou inativar um movimento lançado errado, **para** manter os dados corretos.

**Critérios de aceite:**
- `PATCH` altera `movementDate`, `quantity`, `totalValue` e `notes` (não altera tipo/motivo — D13). `DELETE` inativa (soft delete) e o movimento sai do cálculo do saldo.

---

## 5. Casos de Uso

### CU-01 — Cadastrar insumo
1. `POST /api/stock-items` com `StockItemCreateDto`.
2. Valida DTO: `name`, `stockCategoryId`, `unitOfMeasureId` obrigatórios. Inválido → `400`.
3. Valida existência de `StockCategory` e `UnitOfMeasure` (globais) → inexistente → `404`.
4. Cria `StockItem` (`IsActive = true`). Se `initialQuantity` presente, cria movimento `OpeningBalance` (D15). Retorna `201` com saldo/valor derivados.

### CU-02 — Registrar movimentação
1. `POST /api/stock-items/{id}/movements` com `StockMovementCreateDto`.
2. Carrega o `StockItem` (tenant). Inexistente → `404`.
3. Valida `quantity > 0`, `movementDate <= hoje`, coerência tipo×motivo (RN-04). Inválido → `422`.
4. Para saídas (e `Adjustment` de entrada), calcula e congela `UnitCostSnapshot`/`TotalValue` pelo custo médio vigente (D8/D9). Para `Purchase`/`OpeningBalance`, normaliza `TotalValue` a partir de `valueEntryMode` (D7).
5. Cria `StockMovement` (`IsActive = true`). Retorna `201` com o movimento e o saldo do item atualizado.

### CU-03 — Editar movimentação
1. `PATCH /api/stock-items/{id}/movements/{movId}` com `StockMovementUpdateDto`.
2. Carrega o movimento (tenant). Inexistente → `404`.
3. Aplica só os campos enviados (`movementDate`, `quantity`, `totalValue`, `notes`); reaplica validações. `MovementType`/`MovementReason` ignorados (D13).
4. Retorna `200` com o movimento atualizado.

### CU-04 — Listar histórico de um insumo
- `GET /api/stock-items/{id}/movements` (filtros: `movementType`, `movementReason`, `dateFrom`, `dateTo`) → `200 [StockMovementListItemDto]` (`movementDate` desc). Item inexistente → `404`.

### CU-05 — Consultar / listar insumos
- `GET /api/stock-items` (filtros: `name`, `stockCategoryId`, `isActive`) → `200 [StockItemListItemDto]` com `currentBalance`, `stockValue`, `alertSeverity` resolvidos **em bloco** (set-based).
- `GET /api/stock-items/{id}` → `200 StockItemDto` (detalhe com saldo, valor, custo médio, cobertura/ruptura). Inexistente → `404`.

### CU-06 — Dashboard / relatórios
- `GET /api/stock/dashboard?dateFrom=&dateTo=&stockCategoryId=&stockItemId=` → `200 StockDashboardDto` (3 números em R$ + tabela por insumo). Sem datas → mês corrente. Os filtros `stockCategoryId` e `stockItemId` restringem o escopo: os três números em R$ e a tabela passam a refletir só os insumos filtrados.
- `GET /api/stock/alerts` → `200 [StockAlertDto]` (insumos com `saldo <= reorderPoint`, ordenados por severidade).

### CU-07 — Inativar
- `DELETE /api/stock-items/{id}/movements/{movId}` → soft delete → `204`. Já inativo → `409`.
- `DELETE /api/stock-items/{id}` → soft delete do insumo → `204`. Já inativo → `409`.

### CU-08 — Lookups de referência
- `GET /api/stock-categories` → `200 [StockCategoryDto]` (ativas).
- `GET /api/units-of-measure` → `200 [UnitOfMeasureDto]` (ativas).
- `GET /api/stock/movement-reasons`, `/movement-types` → `200 [{value,label}]` (enums).

---

## 6. Especificação Técnica

### 6.1 Entidade `StockItem`
> `Domain/Models/StockItem.cs` — o insumo. **Sem** coluna de saldo/valor (D1/D2).

```csharp
public class StockItem : BaseEntity, ITenantEntity
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int StockCategoryId { get; set; }        // FK → StockCategory (global)

    [Required]
    public int UnitOfMeasureId { get; set; }         // FK → UnitOfMeasure (global); fixa por item (D4)

    public decimal? ReorderPoint { get; set; }       // ponto crítico, na unidade do item; null = sem alerta (D12)

    public int? ReplenishmentLeadDays { get; set; }  // dias até repor; enriquece a severidade (D12)

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public StockCategory? StockCategory { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public ICollection<StockMovement>? Movements { get; set; }
}
```

### 6.2 Entidade `StockMovement`
> `Domain/Models/StockMovement.cs` — o ledger (átomo de escrita). Espelha `SemenSampleMovement`.

```csharp
public class StockMovement : BaseEntity, ITenantEntity
{
    [Required]
    public int StockItemId { get; set; }

    public StockMovementType MovementType { get; set; }      // Input | Output
    public StockMovementReason MovementReason { get; set; }  // Purchase | OpeningBalance | Consumption | Loss | Adjustment

    public DateTime MovementDate { get; set; }               // <= hoje

    [Range(0.001, 9999999.999, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public decimal Quantity { get; set; }                    // sempre positiva; direção vem de MovementType (D6)

    public decimal? TotalValue { get; set; }                 // R$ da entrada (Purchase/OpeningBalance/Adjustment-in) (D7/D9)
    public ValueEntryMode? ValueEntryMode { get; set; }      // proveniência do valor digitado (só entradas de compra) (D7)
    public decimal? UnitCostSnapshot { get; set; }           // custo médio congelado nas saídas (D8)

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid PropertyId { get; set; }

    public StockItem? StockItem { get; set; }
}
```

> **Valoração (D7/D8/D9):**
> - `Purchase` / `OpeningBalance` (Input): `TotalValue` = valor informado pelo usuário (normalizado de `unitPrice × quantity` quando `ValueEntryMode = UnitPrice`); `ValueEntryMode` guarda como foi digitado.
> - `Adjustment` de **entrada** (contagem encontrou estoque): `TotalValue` = `averageUnitCost vigente × quantity` (D9); `ValueEntryMode` nulo.
> - `Consumption` / `Loss` / `Adjustment` de **saída** (Output): `UnitCostSnapshot` = `averageUnitCost vigente`, congelado no lançamento (D8); `TotalValue` nulo.

### 6.3 Tabelas de referência `StockCategory` e `UnitOfMeasure`
> `Domain/Models/` — globais (sem `PropertyId`), semeadas por migration, sem CRUD (D3).

```csharp
public class StockCategory : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;   // dado (PT): ver seed em §7 (Concentrado, Volumoso, ... , Outro)
    public ICollection<StockItem>? Items { get; set; }
}

public class UnitOfMeasure : BaseEntity
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;    // ex.: "Quilograma"
    [MaxLength(10)]
    public string? Abbreviation { get; set; }            // ex.: "kg" — exibição "80 kg"
    public ICollection<StockItem>? Items { get; set; }
}
```

> Não implementam `ITenantEntity` (D14). `IsActive` (de `BaseEntity`) permite desativar um valor de referência no futuro sem removê-lo; o v1 só lê os ativos.

### 6.4 Enums
> `Domain/Enums/` — nomes em **inglês**, `[Description]` em **português**.

```csharp
public enum StockMovementType
{
    [Description("Entrada")] Input = 1,
    [Description("Saída")]   Output = 2
}

public enum StockMovementReason
{
    [Description("Compra")]        Purchase = 1,
    [Description("Saldo inicial")] OpeningBalance = 2,
    [Description("Consumo")]       Consumption = 3,
    [Description("Perda")]         Loss = 4,
    [Description("Ajuste")]        Adjustment = 5
}

public enum ValueEntryMode
{
    [Description("Unitário")] UnitPrice = 1,
    [Description("Total")]    TotalPrice = 2
}

// Derivado — NÃO gravado (produzido pelo resolver na leitura):
public enum StockAlertSeverity
{
    [Description("OK")]       Ok = 1,
    [Description("Atenção")]  Attention = 2,
    [Description("Crítico")]  Critical = 3
}
```

### 6.5 Resolver (helper estático)
> `Application/Helpers/StockForecastResolver.cs` — mesma fonte de verdade para 1 e para N. O serviço reúne os fatos set-based (padrão `GetReproductiveStatusMapAsync`); o resolver é função pura.

**Fatos por insumo** (agregados no repositório, sobre movimentos **ativos**):
- `inQty = Σ Quantity (MovementType = Input)`, `outQty = Σ Quantity (MovementType = Output)`.
- `inValue = Σ TotalValue (Input)`, `outValue = Σ (UnitCostSnapshot × Quantity) (Output)`.
- `recentConsumption` = saídas `Consumption` numa janela (default **30 dias**) → para o ritmo.

```csharp
public readonly record struct StockLevels(
    decimal CurrentBalance,   // inQty - outQty
    decimal StockValue,       // inValue - outValue (>= 0 clamp p/ exibição)
    decimal AverageUnitCost); // StockValue / CurrentBalance (0 se saldo <= 0)

public static class StockForecastResolver
{
    // Custo médio vigente usado para congelar UnitCostSnapshot no lançamento de uma saída (D8).
    public static decimal CurrentAverageUnitCost(decimal onHandQty, decimal onHandValue)
        => onHandQty > 0 ? onHandValue / onHandQty : 0m;

    // Ritmo diário de consumo pela janela recente (null se não houver consumo).
    public static decimal? DailyConsumptionRate(decimal consumedInWindow, int windowDays)
        => windowDays > 0 && consumedInWindow > 0 ? consumedInWindow / windowDays : (decimal?)null;

    // Dias de cobertura e data de ruptura (null quando não há ritmo — cold-start).
    public static (int? DaysOfCoverage, DateTime? RunOutDate) Forecast(
        decimal currentBalance, decimal? dailyRate, DateTime today) { /* ... */ }

    // Severidade do alerta (D12). Gatilho primário = ponto crítico; ruptura enriquece.
    public static StockAlertSeverity ResolveSeverity(
        decimal currentBalance,
        decimal? reorderPoint,
        DateTime? runOutDate,
        int? replenishmentLeadDays,
        DateTime today) { /* ... */ }
}
```

> **Severidade (D12):**
> - `Ok` — `reorderPoint` nulo **ou** `saldo > reorderPoint` (fora da tela de avisos).
> - `Attention` — `saldo <= reorderPoint` (chegou na reserva).
> - `Critical` — `saldo <= 0`, **ou** (havendo histórico) `runOutDate < hoje + replenishmentLeadDays` (rompe antes de repor).
> - Sem histórico de consumo, o item aparece no máximo como `Attention` (por quantidade), com "sem previsão" no lugar da data — nunca vira falso `Critical`.

### 6.6 DTOs
> `Application/DTOs/` — enums de resposta como `EnumValueDto { Value, Label }` (via `GetDescription()`); datas com `DateFormatConverter`.

**`StockItem`:**
- **`StockItemCreateDto`** — `Name` (`[Required]`), `StockCategoryId` (`[Required]`), `UnitOfMeasureId` (`[Required]`), `ReorderPoint?`, `ReplenishmentLeadDays?`, `Notes?`, e (D15) `InitialQuantity?` (`> 0`), `InitialTotalValue?`, `InitialNotes?`.
- **`StockItemUpdateDto`** (PATCH) — `Name?`, `StockCategoryId?`, `UnitOfMeasureId?`, `ReorderPoint?`, `ReplenishmentLeadDays?`, `Notes?` (null = não altera).
- **`StockItemDto`** (detalhe) — `Id`, `Name`, `StockCategory` (`{id,name}`), `UnitOfMeasure` (`{id,name,abbreviation}`), `ReorderPoint?`, `ReplenishmentLeadDays?`, `Notes?`, **derivados:** `CurrentBalance`, `StockValue`, `AverageUnitCost`, `DaysOfCoverage?`, `EstimatedRunOutDate?`, `AlertSeverity` (`EnumValueDto`), `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`StockItemListItemDto`** — `Id`, `Name`, `CategoryName`, `UnitAbbreviation`, `CurrentBalance`, `StockValue`, `ReorderPoint?`, `AlertSeverity` (`EnumValueDto`), `IsActive`.
- **`StockItemFilterDto`** — `Name?`, `StockCategoryId?`, `IsActive?` (`null`=todos, `true`=ativos, `false`=inativos).

**`StockMovement`:**
- **`StockMovementCreateDto`** — `MovementType` (`[Required]`), `MovementReason` (`[Required]`), `MovementDate` (`[Required]`, `<= hoje`), `Quantity` (`[Required]`, `> 0`), `TotalValue?`, `UnitPrice?`, `ValueEntryMode?`, `Notes?`. `IValidatableObject`: coerência tipo×motivo (RN-04); valor obrigatório sse `Purchase`/`OpeningBalance` (informar `totalValue` **ou** `unitPrice` conforme `valueEntryMode`).
- **`StockMovementUpdateDto`** (PATCH) — `MovementDate?`, `Quantity?` (`> 0`), `TotalValue?`, `Notes?`. (Tipo/motivo não editáveis — D13.)
- **`StockMovementDto`** (resposta) — `Id`, `StockItemId`, `StockItemName`, `MovementType`/`MovementReason` (`EnumValueDto`), `MovementDate`, `Quantity`, `TotalValue?`, `UnitCost` (derivado: `TotalValue/Quantity` ou `UnitCostSnapshot`), `Notes?`, `IsActive`, `CreatedAt`, `UpdatedAt`.
- **`StockMovementListItemDto`** — `Id`, `MovementType`/`MovementReason` (`EnumValueDto`), `MovementDate`, `Quantity`, `TotalValue?`, `IsActive`.
- **`StockMovementFilterDto`** — `MovementType?`, `MovementReason?`, `DateFrom?`, `DateTo?`.

**Referência / dashboard:**
- **`StockCategoryDto`** — `Id`, `Name`. **`UnitOfMeasureDto`** — `Id`, `Name`, `Abbreviation`.
- **`StockDashboardFilterDto`** (query params) — `DateFrom?`, `DateTo?`, `StockCategoryId?`, `StockItemId?`. Sem datas → mês corrente; com categoria/item → escopo restrito (os R$ e a tabela seguem o filtro).
- **`StockDashboardDto`** — `DateFrom`, `DateTo`, `PeriodSpent` (R$ compras), `PeriodConsumedValue` (R$ consumo valorizado), `StockValue` (R$ saldo valorizado), `Items` (`[StockDashboardItemDto]` = `StockItemId`, `Name`, `UnitAbbreviation`, `ConsumedQuantity`, `CurrentBalance`).
- **`StockAlertDto`** — `StockItemId`, `Name`, `UnitAbbreviation`, `CurrentBalance`, `ReorderPoint?`, `EstimatedRunOutDate?`, `Severity` (`EnumValueDto`).

Mapeamento em `Application/Mappings/StockProfile.cs`. Os campos derivados (`CurrentBalance`, `StockValue`, `AlertSeverity`, dashboard) são preenchidos no service (resolver + memória, sobre queries set-based); DTOs simples passam pelo AutoMapper.

#### 6.6.1 Convenção de enums no payload (front)
A API **não** usa `JsonStringEnumConverter` — nos corpos de requisição, todo enum é enviado pelo **valor numérico**. `MovementType`: `Input=1`, `Output=2`. `MovementReason`: `Purchase=1`, `OpeningBalance=2`, `Consumption=3`, `Loss=4`, `Adjustment=5`. `ValueEntryMode`: `UnitPrice=1`, `TotalPrice=2`. As respostas devolvem enums como `EnumValueDto { value, label }`; os lookups (`/movement-reasons`, `/movement-types`, `/stock-categories`, `/units-of-measure`) montam os selects.

**Body — compra** (`POST /api/stock-items/{id}/movements`):
```json
{ "movementType": 1, "movementReason": 1, "movementDate": "2026-09-11",
  "quantity": 16, "valueEntryMode": 2, "totalValue": 640.00, "notes": "Feno em bola" }
```
**Body — consumo:**
```json
{ "movementType": 2, "movementReason": 3, "movementDate": "2026-09-11", "quantity": 3 }
```

### 6.7 Endpoints da API
> Auth: Bearer Token obrigatório.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `POST` | `/api/stock-items` | Criar insumo (+ saldo inicial opcional) | `201 StockItemDto` / `400` / `404` |
| `GET` | `/api/stock-items` | Listar (filtro: `name`, `stockCategoryId`, `isActive`) | `200 [StockItemListItemDto]` |
| `GET` | `/api/stock-items/{id}` | Detalhe (saldo, valor, custo médio, cobertura) | `200 StockItemDto` / `404` |
| `PATCH` | `/api/stock-items/{id}` | Editar insumo | `200 StockItemDto` / `400` / `404` |
| `DELETE` | `/api/stock-items/{id}` | Inativar insumo (soft delete) | `204` / `404` / `409` |
| `POST` | `/api/stock-items/{id}/movements` | Registrar movimentação | `201 StockMovementDto` / `404` / `422` |
| `GET` | `/api/stock-items/{id}/movements` | Histórico (filtros de tipo/motivo/período) | `200 [StockMovementListItemDto]` / `404` |
| `PATCH` | `/api/stock-items/{id}/movements/{movId}` | Editar movimentação | `200 StockMovementDto` / `404` / `422` |
| `DELETE` | `/api/stock-items/{id}/movements/{movId}` | Inativar movimentação | `204` / `404` / `409` |
| `GET` | `/api/stock/dashboard` | Dashboard por período (3 R$ + tabela); filtros `stockCategoryId`/`stockItemId` | `200 StockDashboardDto` |
| `GET` | `/api/stock/alerts` | Insumos no/abaixo do ponto crítico | `200 [StockAlertDto]` |
| `GET` | `/api/stock-categories` | Lookup de categorias (ativas) | `200 [StockCategoryDto]` |
| `GET` | `/api/units-of-measure` | Lookup de unidades (ativas) | `200 [UnitOfMeasureDto]` |
| `GET` | `/api/stock/movement-reasons` | Lookup `StockMovementReason` | `200 [{value,label}]` |
| `GET` | `/api/stock/movement-types` | Lookup `StockMovementType` | `200 [{value,label}]` |

### 6.8 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | `Name`, `StockCategoryId`, `UnitOfMeasureId` obrigatórios no insumo. | DTO (DataAnnotations) |
| RN-02 | `StockCategory`/`UnitOfMeasure` referenciados devem existir (ativos). | Service → `NotFoundException` |
| RN-03 | `Quantity > 0`; `MovementDate <= hoje`. | DTO + Service (`BusinessRuleException`) |
| RN-04 | Coerência tipo×motivo: `Purchase`/`OpeningBalance` ⇒ `Input`; `Consumption`/`Loss` ⇒ `Output`; `Adjustment` ⇒ `Input` **ou** `Output`. | DTO (`IValidatableObject`) + Service |
| RN-05 | Entrada `Purchase`/`OpeningBalance` exige valor (`totalValue` ou `unitPrice` conforme `valueEntryMode`); normalizado para `TotalValue` (D7). | DTO + Service |
| RN-06 | Saída (e `Adjustment`-in) congela `UnitCostSnapshot`/`TotalValue` pelo custo médio vigente no lançamento (D8/D9). | Service (leitura set-based do saldo/valor até a data) |
| RN-07 | `CurrentBalance = Σ Input.Quantity − Σ Output.Quantity` sobre movimentos **ativos**. | Repository (agregação, `IsActive = true`) |
| RN-08 | `StockValue = Σ Input.TotalValue − Σ (Output.UnitCostSnapshot × Quantity)` sobre movimentos **ativos**. | Repository / Resolver |
| RN-09 | Saldo negativo é permitido — não bloqueia saídas. | Informativo; sem validação ativa |
| RN-10 | `MovementType`/`MovementReason` não editáveis via PATCH (D13). | Service (campos ignorados) |
| RN-11 | Movimento inativo (`IsActive = false`) não entra em nenhum cálculo. | Repository (filtro `IsActive` nas agregações) |
| RN-12 | Alerta: `saldo <= ReorderPoint` (D12); severidade pelo resolver. Sem entidade persistida. | Resolver (leitura) |
| RN-13 | Relatório de consumo (R1) conta só `Consumption`; gasto (R2) conta só `Purchase`. | Repository / Service (dashboard) |
| RN-14 | Isolamento de tenant: repositório filtra `StockItem`/`StockMovement` por `PropertyId`. Referência é global. | Repository (D14) |

### 6.9 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `StockItem.cs`, `StockMovement.cs`, `StockCategory.cs`, `UnitOfMeasure.cs` | **Criar** |
| `Domain/Enums` | `StockMovementType.cs`, `StockMovementReason.cs`, `ValueEntryMode.cs`, `StockAlertSeverity.cs` | **Criar** |
| `Application/Helpers` | `StockForecastResolver.cs` | **Criar** |
| `Application/DTOs` | DTOs do §6.6 | **Criar** |
| `Application/Mappings` | `StockProfile.cs` | **Criar** |
| `Application/Interfaces` | `IStockItemService`, `IStockItemRepository`, `IStockMovementService`, `IStockMovementRepository`, `IStockReferenceRepository` | **Criar** |
| `Application/Services` | `StockItemService.cs`, `StockMovementService.cs` | **Criar** (movimento gera custo congelado; item compõe derivados set-based) |
| `Infrastructure/Repositories` | `StockItemRepository.cs`, `StockMovementRepository.cs`, `StockReferenceRepository.cs` | **Criar** (filtro tenant; agregações de saldo/valor) |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | `DbSet`s, query filters (item/movimento), FKs, `HasPrecision`, índices, seed (`HasData`) da referência |
| `Api/Controllers` | `StockItemsController.cs`, `StockController.cs` (dashboard/alerts/lookups de enum), `StockCategoriesController.cs`, `UnitsOfMeasureController.cs` | **Criar** |
| `Program.cs` | DI | Registrar repositórios e serviços *(requer aprovação)* |
| `Infrastructure/Migrations` | *(ver §7)* | **Requer aprovação antes de criar** |

---

## 7. Notas de Migração
> **Requer aprovação explícita antes de executar** (criação de migração e mudança de DI).

**Criar tabela `StockCategories`** (global, sem `PropertyId`): `Id` (PK), `Name` nvarchar(100), `IsActive` bit default 1, `CreatedAt`, `UpdatedAt`.
**Seed (`HasData`)** — Ids fixos 1..8:

| Id | Categoria | Cobre |
|----|-----------|-------|
| 1 | Concentrado | Ração, farelos, concentrados energéticos/proteicos |
| 2 | Volumoso | Feno, silagem, pré-secado |
| 3 | Minerais e Suplementos | Sal mineral, núcleos, aditivos nutricionais |
| 4 | Higiene e Limpeza | Detergentes, desinfetantes, produtos de limpeza da ordenha e das instalações |
| 5 | Combustível e Lubrificantes | Diesel, gasolina, óleos, graxas |
| 6 | Manutenção e Ferramentas | Peças, materiais de reparo, ferramentas, EPIs |
| 7 | Insumos Agrícolas | Adubos, fertilizantes, sementes, defensivos (plantio de pasto/silagem) |
| 8 | Outro | Insumo fora das categorias acima (escape) |

**Criar tabela `UnitsOfMeasure`** (global, sem `PropertyId`): `Id` (PK), `Name` nvarchar(50), `Abbreviation` nvarchar(10) null, `IsActive`, `CreatedAt`, `UpdatedAt`.
**Seed (`HasData`):** Quilograma (kg), Litro (L), Bola, Fardo, Saco, Tonelada (t), Unidade (un) — Ids fixos 1..7.

**Criar tabela `StockItems`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | int | PK, identity |
| `Name` | nvarchar(150) | not null |
| `StockCategoryId` | int | not null, FK → `StockCategories` (`Restrict`) |
| `UnitOfMeasureId` | int | not null, FK → `UnitsOfMeasure` (`Restrict`) |
| `ReorderPoint` | decimal(12,3) | nullable |
| `ReplenishmentLeadDays` | int | nullable |
| `Notes` | nvarchar(500) | nullable |
| `PropertyId` | uniqueidentifier | not null |
| `IsActive` | bit | not null, default 1 |
| `CreatedAt` / `UpdatedAt` | datetime2 | not null / nullable |

**Criar tabela `StockMovements`:** `Id` (PK), `StockItemId` (FK → `StockItems`, `Restrict`), `MovementType` int, `MovementReason` int, `MovementDate` datetime2, `Quantity` decimal(12,3), `TotalValue` decimal(12,2) null, `ValueEntryMode` int null, `UnitCostSnapshot` decimal(12,4) null, `Notes` nvarchar(500) null, `PropertyId` uniqueidentifier, `IsActive`/`CreatedAt`/`UpdatedAt`.

**Índices:**
| Colunas | Motivo |
|---------|--------|
| `StockItems (PropertyId, IsActive)` | Listagem por tenant. |
| `StockItems (PropertyId, StockCategoryId)` | Filtro por categoria. |
| `StockMovements (StockItemId, MovementType, IsActive)` | Agregação de saldo/valor. |
| `StockMovements (PropertyId, IsActive)` | Isolamento de tenant. |
| `StockMovements (StockItemId, MovementDate)` | Histórico / relatórios por período. |
| `StockMovements (PropertyId, MovementReason, MovementDate)` | Dashboard (R1 consumo / R2 gasto). |

**DbContext:** `DbSet<StockItem>`, `DbSet<StockMovement>`, `DbSet<StockCategory>`, `DbSet<UnitOfMeasure>`; `HasQueryFilter(x => x.PropertyId == _propertyId)` em `StockItem` e `StockMovement` (referência **não** tem filtro); `HasPrecision` nas colunas decimais; FKs `Restrict`; `HasData` da referência.

> Migração sugerida: `Spec17_Estoque`.

---

## 8. Como habilitar offline-first depois (sem retrabalho)
A análise original é offline-first (Id GUID de cliente, `FarmId`, resolvers no device, Room/outbox). Server-side agora **não** impede a evolução — a via é **aditiva** (mesmo racional de Vacinação, 11.1 e Tratamentos):

- Adicionar **`ClientId Guid` único** em `StockItem` e `StockMovement` (chave de idempotência), mantendo o `Id int` (PK interna). Ingestão vira **upsert idempotente por `ClientId`**.
- O **ledger é append por natureza** (D1) → movimentos criados offline convergem sem conflito; correções entram como `Adjustment` (append) ou como tombstone de soft delete.
- O **resolver** (`StockForecastResolver`) é função pura sobre os fatos — roda idêntico sobre o cache local (Room) ou o banco. "Barracão sem sinal: a lista mostra 'feno — 4 bolas, rompe em 05/09' sem servidor."
- `PropertyId` continua o tenant; se/quando `FarmId` entrar como raiz, é renomeação/alias, não reescrita de regra.
- **Categoria/Unidade editáveis por produtor** (CRUD + escopo por tenant) é a evolução natural de D3, sem tocar o ledger.

---

## 9. Fora do Escopo / Questões Futuras
- **Eixo de dieta / receitas** que pré-preenchem e geram saídas `Consumption` — dependência unidirecional dieta → estoque; nenhuma FK aqui (D11).
- **Baixa automática** por aplicação de medicação/vacina — sem acoplamento no v1 (Q4).
- **CRUD de categoria/unidade pelo produtor** — read-only agora; editável no futuro (§8; pergunta em aberto #4 da análise: soft-delete via `IsActive` e bloqueio de nova seleção, preservando itens existentes).
- **FIFO / rastreio lote a lote** na valoração — usa média ponderada móvel (D8); risco de compra sincronizada em atraso documentado (não corrompe retroativo).
- **Notificação com app fechado** ("ponto crítico atingido") — client-side/offline lendo o resolver; sem job de servidor.
- **Cruzamentos de custo com outros eixos** (ex.: custo de insumo por litro de leite) — JOIN de leitura com o eixo produtivo, mantendo a escrita desacoplada (D11). Fora do v1.
- **Sincronização offline-first / upsert idempotente** — evolução futura aditiva (§8).

**Resolvidas nesta iteração:** categoria/unidade como tabela semeada read-only (D3); financeiro completo com média ponderada móvel (D7/D8) e valoração de `Ajuste`-in ao custo vigente (D9, pergunta em aberto #3); ponto crítico como gatilho + ruptura como enriquecimento (D12); saldo inicial via `OpeningBalance` (D10); saídas sempre manuais, sem acoplamento (D11); ledger com soft delete + correção sem recomputar snapshot (D13).
