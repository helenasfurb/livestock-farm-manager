# Spec: Ajustes Pontuais — Classificação em Eventos Reprodutivos e Motivos de Saída

**Módulo:** Ajustes transversais
**Versão:** 1.2
**Data:** 29/Ago/2026
**Fonte:** Ajuste pós-desenvolvimento — revisão interna
**Status:** Aprovado para implementação
**Ajusta:** Spec #2 (Saída de Animais), Spec #5 (Eventos Reprodutivos)
**Histórico:** v1.2 — adiciona o bloqueio de cobertura para animal com gestação ativa (Ajuste 4) e a rota de autocomplete de animais elegíveis à cobertura (Ajuste 5).

---

## 1. Contexto e Objetivo

Dois ajustes pontuais identificados após o desenvolvimento inicial:

1. **Classificação elegível para eventos reprodutivos (Spec #5):** A regra de qual classificação de animal pode participar de um evento reprodutivo não estava explicitada no Spec #5. Vacas (`Cow`) e novilhas (`Heifer`) são igualmente elegíveis para inseminação artificial e monta natural. Qualquer outra classificação deve ser rejeitada.

2. **Enum `AnimalExitReason` (Spec #2):** Os valores atuais do enum não refletem com precisão os cenários da propriedade:
   - `Death` ("Morte") → renomear para `NaturalDeath` ("Morte Natural"), pois o contexto da propriedade é morte por causas naturais.
   - `Discard` ("Descarte") → renomear para `OwnConsumption` ("Consumo Próprio"), que é o uso real do animal descartado na prática do produtor consultado.
   - `Transfer` ("Transferência") → remover; não é um cenário relevante para o escopo do projeto.

3. **Bloqueio de novo evento reprodutivo com diagnóstico pendente (Spec #5):** Atualmente é possível criar um novo `BreedingEvent` para um animal que já possui um evento reprodutivo ativo aguardando diagnóstico (`AwaitingDiagnosis`). Isso é biologicamente incoerente — um animal só pode ser submetido a um novo serviço após o desfecho (diagnóstico) do serviço anterior ser registrado. Deve-se impedir a criação enquanto houver um evento pendente.

4. **Bloqueio de cobertura para animal com gestação ativa (Spec #5):** Também é possível criar um `BreedingEvent` para um animal que já possui uma **gestação confirmada ativa**. Um animal prenhe não pode ser coberto novamente; a criação deve ser impedida enquanto houver gestação confirmada ativa.

5. **Autocomplete de animais elegíveis à cobertura (Spec #5):** O cadastro de cobertura precisa de uma rota de autocomplete que liste apenas os animais aptos a receber um novo serviço — fêmeas com classificação `Cow`/`Heifer`, ativas, **sem** cobertura aguardando diagnóstico e **sem** gestação confirmada ativa. Assim a UI só oferece animais válidos, espelhando as regras de bloqueio da criação (Ajustes 1, 3 e 4).

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Validar que o animal submetido a um `BreedingEvent` tem `Classification = Cow` ou `Heifer` | Biologicamente, apenas fêmeas adultas e jovens são submetidas a cobertura. A regra estava implícita no Spec #5 (status reprodutivo já era derivado apenas para essas classificações) mas não era validada na criação do evento. |
| D2 | `Death → NaturalDeath` mantém o valor numérico `2` | Preserva registros existentes no banco. Apenas o nome do membro e a descrição mudam. |
| D3 | `Discard → OwnConsumption` mantém o valor numérico `3` | Mesma razão de D2. O número armazenado não muda. |
| D4 | `Transfer = 4` removido sem reatribuição do valor | Nenhum outro membro ocupa o valor `4` para evitar ambiguidade com dados eventualmente persistidos. Como o projeto está em desenvolvimento (sem dados de produção), a remoção é segura. |
| D5 | A remoção de `Transfer` não exige migration de dados | Projeto acadêmico sem dados de produção. A migration apenas remove a validação de enum onde aplicável; nenhum dado precisa ser migrado. |
| D6 | Bloquear a criação de um `BreedingEvent` quando o animal já possui um evento ativo com `Status = AwaitingDiagnosis` | Um animal aguardando o diagnóstico de um serviço não pode receber um novo serviço antes do desfecho do anterior. Sem essa regra, seria possível empilhar múltiplos eventos pendentes para o mesmo animal, gerando inconsistência no controle reprodutivo. |
| D7 | A verificação lança `ConflictException` (`409`) | É um conflito de estado — o animal já se encontra em um ciclo reprodutivo aberto (pendente). Consistente com os demais conflitos de estado já sinalizados por `ConflictException` em `BreedingEventService` (animal inativo, diagnóstico já registrado). |
| D8 | Bloquear a criação de um `BreedingEvent` quando o animal já possui uma gestação **ativa** e **confirmada** (`AnimalPregnancy.IsActive && Status = Confirmed`) | Um animal prenhe não pode ser coberto novamente enquanto a gestação estiver em curso. Reaproveita `IAnimalPregnancyRepository.HasActiveConfirmedByAnimalIdAsync`, já existente. |
| D9 | A verificação de gestação ativa lança `BusinessRuleException` (`422`) | Segue o mesmo tratamento do bloqueio por diagnóstico pendente **conforme implementado** em `BreedingEventService.CreateAsync` (ambos como regra de negócio). Os dois bloqueios ficam consistentes entre si no código. |
| D10 | Expor uma rota de autocomplete de animais elegíveis à cobertura, retornando `Id`, `Name` e `TagNumber` | A elegibilidade é exatamente o inverso dos bloqueios de criação (classificação `Cow`/`Heifer` + fêmea ativa, sem diagnóstico pendente, sem gestação confirmada). Manter a rota junto ao recurso de cobertura evita expor regra reprodutiva na listagem geral de animais. |

---

## 3. Ajuste 1 — Classificação elegível para `BreedingEvent`

### 3.1 Regra de negócio adicionada ao Spec #5

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-17 | O animal submetido deve ter `Classification = Cow` ou `Heifer`. Se não, lança `BusinessRuleException`. | `BreedingEventService.CreateAsync` |

**Posição no fluxo de CU-01 (Spec #5):**
Inserir entre os passos 3 e 4 atuais:

> **3a.** Sistema valida que `Animal.Classification` é `Cow` ou `Heifer` — se não, lança `BusinessRuleException` → `422 Unprocessable Entity`.

**Mensagem de erro sugerida:**
> "Apenas vacas e novilhas podem ser submetidas a eventos reprodutivos."

### 3.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/Services` | `BreedingEventService.cs` | Adicionar validação de `Classification` em `CreateAsync` |
| `MuuBoi.Tests` | `BreedingEventServiceTests.cs` | Adicionar casos: `CreateAsync_WithNonFemaleClassification_ThrowsBusinessRuleException` |

---

## 4. Ajuste 2 — Enum `AnimalExitReason`

### 4.1 Alterações no enum

> Localização: `Domain/Enums/AnimalExitReason.cs`

**Antes:**
```csharp
public enum AnimalExitReason
{
    [Description("Venda")]
    Sale = 1,

    [Description("Morte")]
    Death = 2,

    [Description("Descarte")]
    Discard = 3,

    [Description("Transferência")]
    Transfer = 4
}
```

**Depois:**
```csharp
public enum AnimalExitReason
{
    [Description("Venda")]
    Sale = 1,

    [Description("Morte Natural")]
    NaturalDeath = 2,

    [Description("Consumo Próprio")]
    OwnConsumption = 3
}
```

### 4.2 Impacto em cascata

Todos os arquivos que referenciam `Death`, `Discard` ou `Transfer` de `AnimalExitReason` devem ser atualizados:

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Enums` | `AnimalExitReason.cs` | Renomear membros; remover `Transfer` |
| `Application/DTOs` | `AnimalExitDto.cs` | Nenhuma mudança de código — o enum é referenciado por valor, não por nome |
| `Application/DTOs` | `AnimalExitRecordDto.cs` | Nenhuma mudança de código |
| `Application/Mappings` | `AnimalExitRecordProfile.cs` | Verificar se há referência a `Death`, `Discard` ou `Transfer` por nome e atualizar |
| `MuuBoi.Tests` | *(testes de saída de animal)* | Atualizar `AnimalExitReason.Death` → `NaturalDeath`, `Discard` → `OwnConsumption`; remover casos com `Transfer` |

> **Nota:** O valor numérico armazenado no banco (`2` e `3`) não muda. Nenhuma migration de dados é necessária. A migration registra apenas a mudança de schema se houver constraint de enum no banco (SQL Server não tem — o valor é armazenado como `int`).

---

## 5. Ajuste 3 — Bloqueio de novo `BreedingEvent` com diagnóstico pendente

### 5.1 Regra de negócio adicionada ao Spec #5

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-18 | Não é possível criar um novo `BreedingEvent` para um animal que já possui um evento reprodutivo **ativo** com `Status = AwaitingDiagnosis`. Se houver, lança `ConflictException`. | `BreedingEventService.CreateAsync` |

**Posição no fluxo de CU-01 (Spec #5):**
Inserir logo após a validação do animal (e após a RN-17 do Ajuste 1), antes das validações específicas de tipo de reprodução (sêmen / touro):

> **3b.** Sistema valida que o animal **não** possui um `BreedingEvent` ativo com `Status = AwaitingDiagnosis` — se possuir, lança `ConflictException` → `409 Conflict`.

**Mensagem de erro sugerida:**
> "O animal já possui um evento reprodutivo aguardando diagnóstico. Registre o diagnóstico do serviço anterior antes de criar um novo."

### 5.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/Interfaces` | `IBreedingEventRepository.cs` | Adicionar método `Task<bool> HasPendingByAnimalIdAsync(int animalId)` — retorna `true` se existir evento ativo com `Status = AwaitingDiagnosis` para o animal. O método existente `HasActiveByAnimalIdAsync` **não** serve, pois não filtra por status. |
| `Infrastructure/Repositories` | `BreedingEventRepository.cs` | Implementar `HasPendingByAnimalIdAsync` filtrando por `IsActive == true && Status == AwaitingDiagnosis` (mantendo o isolamento por `PropertyId` já aplicado no repositório). |
| `Application/Services` | `BreedingEventService.cs` | Em `CreateAsync`, após validar o animal, chamar `HasPendingByAnimalIdAsync` e lançar `ConflictException` se `true`. |
| `MuuBoi.Tests` | `BreedingEventServiceTests.cs` | Adicionar caso: `CreateAsync_WhenAnimalHasPendingEvent_ThrowsConflictException`. |

> **Nota:** A regra considera apenas eventos **ativos** (`IsActive == true`). Um evento inativado (soft delete) não bloqueia novos serviços. Eventos com `Status = Successful` ou `Unsuccessful` também não bloqueiam — o desfecho já foi registrado.

---

## 5b. Ajuste 4 — Bloqueio de cobertura para animal com gestação ativa

### 5b.1 Regra de negócio adicionada ao Spec #5

| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-19 | Não é possível criar um novo `BreedingEvent` para um animal que já possui uma gestação **ativa e confirmada** (`AnimalPregnancy.IsActive && Status = Confirmed`). Se houver, lança `BusinessRuleException`. | `BreedingEventService.CreateAsync` |

**Posição no fluxo de CU-01 (Spec #5):**
Inserir logo após o bloqueio por diagnóstico pendente (RN-18 / passo 3b), antes das validações específicas de tipo de reprodução:

> **3c.** Sistema valida que o animal **não** possui gestação ativa confirmada — se possuir, lança `BusinessRuleException` → `422 Unprocessable Entity`.

**Mensagem de erro sugerida:**
> "O animal possui uma gestação ativa. Não é possível registrar uma nova cobertura."

### 5b.2 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/Services` | `BreedingEventService.cs` | Em `CreateAsync`, após os bloqueios anteriores, chamar `IAnimalPregnancyRepository.HasActiveConfirmedByAnimalIdAsync(animalId)` e lançar `BusinessRuleException` se `true`. |
| `Application/Interfaces` / `Infrastructure/Repositories` | `IAnimalPregnancyRepository` / `AnimalPregnancyRepository` | **Sem mudança** — `HasActiveConfirmedByAnimalIdAsync` já existe (usado na derivação de status reprodutivo). |
| `MuuBoi.Tests` | `BreedingEventServiceTests.cs` | Adicionar caso: `CreateAsync_WhenAnimalHasActivePregnancy_ThrowsBusinessRuleException`. |

> **Nota:** Gestações com `Status = Calved` (parto já registrado) ou inativas **não** bloqueiam — o ciclo já se encerrou.

---

## 5c. Ajuste 5 — Rota de autocomplete de animais elegíveis à cobertura

### 5c.1 Endpoint

> Auth: Bearer Token obrigatório.

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/breeding-events/animals-autocomplete?search={termo}` | Lista os animais aptos a receber uma nova cobertura. `search` (opcional) filtra por nome ou brinco. | `200 [{ id, name, tagNumber }]` |

**Critérios de elegibilidade (todos obrigatórios):**
- `IsActive == true`
- `Gender == F`
- `Classification == Cow` **ou** `Heifer`
- **Não** possui `BreedingEvent` ativo com `Status = AwaitingDiagnosis`
- **Não** possui `AnimalPregnancy` ativa com `Status = Confirmed`

> É exatamente o complemento das regras RN-17 (classificação), RN-18 (diagnóstico pendente) e RN-19 (gestação ativa): um animal que apareça no autocomplete passa nos três bloqueios de criação.

### 5c.2 DTO de resposta

```csharp
public class AnimalAutocompleteItemDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? TagNumber { get; set; }
}
```

### 5c.3 Camadas impactadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `AnimalAutocompleteItemDto.cs` | **Criar** (`Id`, `Name`, `TagNumber`). |
| `Application/Mappings` | `AnimalProfile.cs` | Adicionar `CreateMap<Animal, AnimalAutocompleteItemDto>()` (campos batem, sem config extra). |
| `Application/Interfaces` | `IAnimalRepository.cs` | Adicionar `Task<IEnumerable<Animal>> GetBreedingEligibleAnimalsAsync(string? search)`. |
| `Infrastructure/Repositories` | `AnimalRepository.cs` | Implementar a consulta com os critérios acima (filtros de tenant aplicados automaticamente nas navegações); busca opcional por `Name`/`TagNumber`. |
| `Application/Interfaces` | `IBreedingEventService.cs` | Adicionar `Task<IEnumerable<AnimalAutocompleteItemDto>> GetEligibleAnimalsAsync(string? search)`. |
| `Application/Services` | `BreedingEventService.cs` | Implementar delegando a `IAnimalRepository.GetBreedingEligibleAnimalsAsync` e mapeando o resultado. |
| `Api/Controllers` | `BreedingEventsController.cs` | Adicionar rota `GET /api/breeding-events/animals-autocomplete`. |

---

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

Não há alteração de schema de banco de dados necessária para estes ajustes:
- O enum é armazenado como `int` no SQL Server — renomear membros não afeta o banco.
- Remover `Transfer = 4` não exige remoção de coluna; registros com valor `4` seriam inválidos, mas não existem dados de produção.

A migration deste spec pode ser omitida. Caso seja criada para registro, deverá ser uma migration vazia com comentário documentando a mudança de enum.

> Os Ajustes 3 (RN-18), 4 (RN-19) e 5 (autocomplete) também **não** requerem alteração de schema — são validações/consultas sobre dados já existentes. Nenhuma migration é necessária.

---

## 7. Fora do Escopo deste Spec

- **Cadastro de animais** → Spec #1
- **Entrada e saída de animais (versão original)** → Spec #2
- **Escore de Condição Corporal** → Spec #3
- **Banco de Sêmen e controle de doses** → Spec #4 e Spec #8
- **Eventos Reprodutivos (versão original)** → Spec #5
- **Gestação e Parto** → Spec #6
- **Dashboards** → Spec #7
- **Validação de outros tipos de classificação** (ex: impedir que um `Bull` seja submetido a monta natural como fêmea) — já coberto indiretamente por RN-17; se necessário, detalhar em spec futuro.
