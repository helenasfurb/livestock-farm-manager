# Spec #10: Registro Genealógico de Animal Nascido na Propriedade (PROPOSTA)

**Módulo:** Genealogia / Rastreabilidade
**Versão:** 0.1 (rascunho para discussão)
**Data:** 30/Ago/2026
**Fonte:** Definição de escopo do TCC — genealogia de nascidos na propriedade
**Status:** 🟡 **Em discussão — NÃO aprovado para implementação.** Este documento apresenta **duas alternativas** para decisão conjunta.
**Depende de:** Spec #5 (Eventos Reprodutivos), Spec #6 (6.1 Gestação / 6.2 Parto / 6.3 Cria), Spec 6.4 (Brinco opcional e cria → Animal), Spec #4/#8 (Banco de Sêmen)

---

## 1. Contexto e Objetivo

Um animal **nascido na propriedade** deve ter um **registro genealógico** que identifique sua **mãe** e seu **pai**. O pai pode ser:

- um **touro do rebanho** (quando a cobertura foi por **monta natural**), ou
- um **sêmen** (quando a cobertura foi por **inseminação artificial**), representando um touro externo já cadastrado no banco de sêmen.

O objetivo é expor essa filiação de forma consultável (e, opcionalmente, recursiva — avós, bisavós…).

> **Ponto-chave:** graças ao encadeamento **cobertura → gestação → parto → cria → animal** (fechado no Spec 6.4), **a filiação já está registrada no sistema**. A discussão não é *se* temos o dado, e sim **como materializá-lo**.

---

## 2. O dado já existente — a cadeia de parentesco

Para todo bezerro cadastrado a partir de uma cria viva (Spec 6.4), existe o elo `AnimalCalvingCalf.AnimalId → Animal`. Invertendo esse elo chega-se a toda a filiação:

```
Animal (bezerro, id = X)
  └─ AnimalCalvingCalf  (onde AnimalId == X)          ← elo reverso criado no Spec 6.4
       └─ AnimalCalving
            ├─ AnimalId ─────────────────────────────► MÃE  (Animal vaca/novilha do rebanho)
            └─ AnimalPregnancy → BreedingEvent
                 ├─ ReproductionType = NaturalMating
                 │     └─ SireAnimalId ───────────────► PAI  (Animal touro do rebanho)
                 └─ ReproductionType = ArtificialInsemination
                       └─ SemenSampleId ──────────────► PAI  (SemenSample: Name,
                                                              BullRegistration, BullBreed,
                                                              GeneticsCompany)
```

| Elo | Origem do dado | Observação |
|-----|----------------|------------|
| **Mãe** | `AnimalCalving.AnimalId` | Sempre um `Animal` do rebanho. Igual a `BreedingEvent.AnimalId`. |
| **Pai (touro)** | `BreedingEvent.SireAnimalId` | Presente quando `ReproductionType = NaturalMating`. É um `Animal` classificado como `Bull`. |
| **Pai (sêmen)** | `BreedingEvent.SemenSampleId` | Presente quando `ReproductionType = ArtificialInsemination`. Aponta para `SemenSample` (touro externo). |

> Todas essas linhas **persistem** no banco mesmo que a cobertura/gestação seja inativada (soft delete) — o `IsActive` não é filtro global, então a leitura genealógica pode recuperar o histórico independentemente do estado atual.

---

## 3. Representação do "pai" (comum às duas alternativas)

O pai é uma **união** de dois tipos. Sugestão de contrato único (independe da alternativa escolhida):

```csharp
public class GenealogyFatherDto
{
    public string Type { get; set; }          // "Bull" (monta natural) | "Semen" (IA)

    // Preenchido quando Type == "Bull"
    public AnimalRefDto? Bull { get; set; }    // { Id, Name, TagNumber }

    // Preenchido quando Type == "Semen"
    public SemenSireRefDto? Semen { get; set; } // { SemenSampleId, Name, BullRegistration,
                                                //   BullBreed (EnumValueDto), GeneticsCompany }
}
```

**A mãe** é sempre um `Animal` do rebanho → representada como nó genealógico (ver profundidade, §7).

---

## 4. Alternativa A — Derivação na leitura (sem schema novo)

Monta a genealogia **em tempo de consulta**, percorrendo a cadeia do §2. Nada é duplicado.

### 4.1 Modelagem
Nenhuma mudança em entidades. Nenhuma migração.

### 4.2 Endpoint + DTO
```
GET /api/animals/{id}/genealogy   [?depth=N]
```
```jsonc
{
  "id": 10, "name": "Mimosa", "tagNumber": null,
  "mother": {
    "id": 3, "name": "Estrela", "tagNumber": "000123",
    "mother": { "...": "recursivo se depth > 1" },
    "father": { "...": "recursivo" }
  },
  "father": {
    "type": "Semen",
    "semen": { "semenSampleId": 7, "name": "Touro X",
               "bullRegistration": "BR123", "bullBreed": { "value": 1, "label": "Nelore" },
               "geneticsCompany": "Central Y" }
  }
}
```

### 4.3 Camadas impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Application/DTOs` | `AnimalGenealogyDto`, `GenealogyFatherDto`, `SemenSireRefDto`, `AnimalRefDto` | Criar |
| `Application/Interfaces` + `Infrastructure/Repositories` | `IAnimalCalvingRepository` (ou um `IGenealogyRepository`) | Método que, dado um `animalId`, retorna a cria + parto + gestação + cobertura **sem** filtrar por `IsActive` (histórico) |
| `Application/Services` | `GenealogyService` (novo) ou método em `AnimalService` | Percorre a cadeia; monta o DTO; recursão limitada por `depth` |
| `Api/Controllers` | `AnimalsController` | `GET /api/animals/{id}/genealogy` |
| `Program.cs` | DI | Registrar o novo serviço/repositório (requer aprovação) |

### 4.4 Prós e contras
**Prós:** sem migração; **fonte única de verdade** (nunca dessincroniza); histórico robusto (lê linhas mesmo inativadas); alinhado ao escopo "nascido na propriedade".
**Contras:** consulta percorre 4–5 tabelas por geração (custo maior com recursão); **só** cobre nascidos na propriedade (adquirido não tem cadeia); a lógica de montagem fica no serviço.

---

## 5. Alternativa B — Snapshot desnormalizado no nascimento

Grava a filiação **direto ao criar o bezerro** (Spec 6.4), em colunas próprias.

### 5.1 Modelagem
Duas opções de onde guardar:

**B1 — Colunas no próprio `Animal`:**
| Coluna | Tipo | Notas |
|--------|------|-------|
| `MotherAnimalId` | `int?` | FK → `Animal` |
| `SireAnimalId` | `int?` | FK → `Animal` (touro); nulo se IA |
| `SemenSampleId` | `int?` | FK → `SemenSample`; nulo se monta natural |

**B2 — Entidade dedicada `AnimalParentage` (1:1 com `Animal`):** mesmos campos, isolando genealogia do agregado `Animal`.

Preenchimento em `AnimalCalvingService` ao montar o `Animal` da cria viva (já temos a cobertura via `pregnancy.BreedingEvent`).

### 5.2 Endpoint + DTO
**Mesmo contrato da Alternativa A** (§4.2) — a diferença é só a origem do dado (FKs diretas em vez de percorrer a cadeia).

### 5.3 Camadas impactadas + migração
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `Animal` (B1) ou novo `AnimalParentage` (B2) | Adicionar FKs de filiação |
| `Infrastructure/Data` | `ApplicationDbContext` | Configurar 3 FKs (`Restrict`), índices |
| `Infrastructure/Migrations` | *(nova)* | **Requer aprovação** — adiciona colunas/tabela + FKs |
| `Application/Services` | `AnimalCalvingService.CreateAsync` | Preencher filiação ao criar o bezerro |
| DTOs / Controller / Service | igual à Alternativa A | Consulta lê as FKs diretamente |

### 5.4 Prós e contras
**Prós:** consulta **direta** (1 join por geração, mais barato); abre caminho para **genealogia manual de animais adquiridos** no futuro; independe da integridade da cadeia de parto.
**Contras:** **duplica** dado (risco de dessincronizar do parto se algo for editado); exige **migração**; precisa backfill se quiser cobrir bezerros já nascidos antes do spec.

---

## 6. Comparação lado a lado

| Critério | A — Derivação | B — Snapshot |
|----------|---------------|--------------|
| Migração / schema | Não | **Sim** |
| Duplicação de dado | Não (fonte única) | Sim |
| Custo de consulta | Maior (cadeia) | Menor (FK direta) |
| Robustez a edições no parto | Reflete sempre o atual | Congela no nascimento |
| Cobre animal adquirido | Não | Sim (com entrada manual futura) |
| Backfill de nascidos antigos | Automático | Necessário script |
| Esforço inicial | Menor | Maior |

---

## 7. Questões em aberto (para discutir com o parceiro de TCC)

1. **Profundidade:** só pais (1 geração) ou **pedigree recursivo** (avós, bisavós…) com limite de gerações (ex.: `depth = 3`)? A recursão para quando o ancestral **não** nasceu na propriedade (folha).
2. **Animal adquirido:** genealogia fica vazia? Ou o produtor poderá informar pais manualmente (empurra para a Alternativa B)?
3. **Coberturas/gestações inativadas:** a genealogia deve considerar o histórico mesmo se a cobertura foi inativada? (Alternativa A precisa ignorar `IsActive`; Alternativa B já congela.)
4. **Pai por sêmen:** quanto expor? (`Name` + `BullRegistration` + `BullBreed` + `GeneticsCompany`?) O sêmen representa um touro **externo**, então não recursa.
5. **Proteção contra ciclos:** improvável, mas o montador recursivo deve ter guarda de profundidade/visitados.
6. **Partos múltiplos (gêmeos):** cada cria vira um `Animal` independente; a genealogia é por animal, sem impacto — apenas confirmar.
7. **Exibição:** endpoint dedicado (`/genealogy`) e/ou aninhar um resumo (`mother`/`father`) no `AnimalDto`?

---

## 8. Recomendação preliminar (não vinculante)

Para o escopo atual ("**nascido na propriedade**"), a **Alternativa A (derivação)** tende a ser mais adequada: zero migração, sem risco de dessincronização e histórico já íntegro. A **Alternativa B** passa a valer a pena **se** e **quando** entrar no escopo registrar genealogia de **animais adquiridos** (entrada manual), pois aí não há cadeia de parto para derivar.

> Uma via intermediária: começar por **A** (contrato do endpoint já definido) e migrar para **B** depois, sem quebrar o contrato — o DTO de resposta é o mesmo nas duas.

---

## 9. Fora do Escopo deste Spec (proposta)

- **Genealogia de animais adquiridos** (entrada manual de pais) — depende da decisão da Questão 2.
- **Índices zootécnicos derivados da genealogia** (consanguinidade etc.) → Spec futuro.
- **Cobertura, gestação, parto e cria** → Specs #5 e #6.
- **Banco de sêmen** → Specs #4 e #8.
