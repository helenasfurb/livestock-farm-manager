# Spec: Ajustes Pontuais — Classificação em Eventos Reprodutivos e Motivos de Saída

**Módulo:** Ajustes transversais
**Versão:** 1.1
**Data:** 25/Ago/2026
**Fonte:** Ajuste pós-desenvolvimento — revisão interna
**Status:** Aprovado para implementação
**Ajusta:** Spec #2 (Saída de Animais), Spec #5 (Eventos Reprodutivos)

---

## 1. Contexto e Objetivo

Dois ajustes pontuais identificados após o desenvolvimento inicial:

1. **Classificação elegível para eventos reprodutivos (Spec #5):** A regra de qual classificação de animal pode participar de um evento reprodutivo não estava explicitada no Spec #5. Vacas (`Cow`) e novilhas (`Heifer`) são igualmente elegíveis para inseminação artificial e monta natural. Qualquer outra classificação deve ser rejeitada.

2. **Enum `AnimalExitReason` (Spec #2):** Os valores atuais do enum não refletem com precisão os cenários da propriedade:
   - `Death` ("Morte") → renomear para `NaturalDeath` ("Morte Natural"), pois o contexto da propriedade é morte por causas naturais.
   - `Discard` ("Descarte") → renomear para `OwnConsumption` ("Consumo Próprio"), que é o uso real do animal descartado na prática do produtor consultado.
   - `Transfer` ("Transferência") → remover; não é um cenário relevante para o escopo do projeto.

3. **Bloqueio de novo evento reprodutivo com diagnóstico pendente (Spec #5):** Atualmente é possível criar um novo `BreedingEvent` para um animal que já possui um evento reprodutivo ativo aguardando diagnóstico (`AwaitingDiagnosis`). Isso é biologicamente incoerente — um animal só pode ser submetido a um novo serviço após o desfecho (diagnóstico) do serviço anterior ser registrado. Deve-se impedir a criação enquanto houver um evento pendente.

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

## 6. Notas de Migração

> **Estas ações requerem aprovação explícita antes de executar.**

Não há alteração de schema de banco de dados necessária para estes ajustes:
- O enum é armazenado como `int` no SQL Server — renomear membros não afeta o banco.
- Remover `Transfer = 4` não exige remoção de coluna; registros com valor `4` seriam inválidos, mas não existem dados de produção.

A migration deste spec pode ser omitida. Caso seja criada para registro, deverá ser uma migration vazia com comentário documentando a mudança de enum.

> O Ajuste 3 (RN-18) também não requer alteração de schema — é uma validação em memória sobre dados já existentes.

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
