# Spec #13: Cadastro Retroativo de Gestação (sem Cobertura Vinculada) — PROPOSTA

**Módulo:** Gestação e Parto — Onboarding / Rastreabilidade
**Versão:** 0.2 (rascunho para discussão)
**Data:** 05/Set/2026
**Fonte:** Definição de escopo do TCC — entrada em produção com animais já gestantes; compra de vaca gestante. Refino: recorte para **cadastro retroativo de gestação** + análise de **funcionamento offline/sincronização**.
**Status:** 🟡 **Em discussão — NÃO aprovado para implementação.**
**Depende de:** Spec #2 (Entrada de Animais), Spec #5 (Eventos Reprodutivos), Spec #6 (6.1 Gestação / 6.2 Parto / 6.3 Cria)
**Relaciona-se com:** Spec #10 (Registro Genealógico) — só alimenta os campos de pai; a modelagem final da genealogia é lá. Sincronização offline → **Spec #14** (contrato de servidor, transversal) — aqui só mapeamos o impacto e sugerimos caminhos.

---

## 1. Contexto e Objetivo

Hoje uma `AnimalPregnancy` só existe como **efeito de uma cobertura** (`BreedingEventService.UpdateStatusAsync` com diagnóstico = sucesso → `CreateForBreedingEventAsync`). O FK `AnimalPregnancy.BreedingEventId` é **obrigatório** (1:1 único). Não há endpoint para cadastrar gestação diretamente.

Isso impede dois cenários reais de **onboarding em produção**:

- Uma vaca **da propriedade** já está prenhe quando o sistema entra no ar — a cobertura ocorreu antes de existir registro.
- Uma vaca **comprada já gestante**, muitas vezes **sem informação do touro/sêmen**.

O objetivo é permitir registrar essa gestação **retroativamente**, decidindo como tratar a **ausência da cobertura e do pai** — e fazê-lo de um jeito que **não atrapalhe** o futuro funcionamento offline com sincronização (§6).

---

## 2. Casos de Uso Motivadores

| # | Cenário | Cobertura conhecida? | Pai (touro/sêmen) conhecido? |
|---|---------|----------------------|------------------------------|
| CU-A | Go-live: vaca da propriedade já prenhe | Não (foi antes do sistema) | Às vezes (produtor pode saber) |
| CU-B | **Compra de vaca já gestante, sem info do touro/sêmen** | Não | **Não** |
| CU-C | Compra de vaca gestante **com** info do pai | Não | Sim (o vendedor informou) |

> CU-B é o caso mais restritivo e o que guia a modelagem: **precisamos representar "pai desconhecido" de forma honesta**, sem impedir o cadastro da gestação.

---

## 3. Questão central: **simular** a cobertura/pai ou representar a **ausência**?

Duas filosofias:

### 3.1 Simular (fabricar dado)
Criar um `BreedingEvent` sintético (data estimada, tipo de reprodução arbitrário) e/ou um "touro desconhecido" para preencher os campos obrigatórios.

### 3.2 Representar a ausência (null / "desconhecido")
Modelar explicitamente que **não houve cobertura registrada** e que o **pai é desconhecido** (campos nulos), permitindo capturar o pai **opcionalmente** quando conhecido (CU-C).

### 3.3 Comparação

| Critério | Simular | Representar ausência |
|----------|---------|----------------------|
| Honestidade do dado | ❌ Cria serviço/pai que não existiram | ✅ Diz a verdade ("desconhecido") |
| Índices reprodutivos (Spec #7) | ❌ Polui taxa de concepção, serviços/concepção com serviços falsos | ✅ Basta excluir gestações sem cobertura |
| Genealogia (Spec #10) | ❌ Pai fabricado engana a filiação | ✅ Pai nulo = "desconhecido", verdadeiro |
| Animal-fantasma no rebanho | ❌ "Touro desconhecido" viraria um `Animal` falso | ✅ Não cria animal |
| Necessidade de flag p/ distinguir real x fake | Sim (inevitável) | Já é distinguível (cobertura nula) |
| **Acoplamento na sincronização (§6)** | ❌ Gestação passa a depender de um `BreedingEvent` (real ou fake) — cria ordem de dependência no sync | ✅ Gestação é fato independente — sincroniza sozinha |
| Complexidade | Alta (ver §4) | Menor |

### 3.4 Recomendação (§3)
> **Não simular.** Representar a ausência explicitamente (`BreedingEventId = null`, pai nulo) e permitir **captura opcional** do pai quando o produtor souber (CU-C). Fabricar cobertura/pai corrompe justamente os dados que o sistema existe para dar — KPIs reprodutivos e genealogia — exige uma flag para separar o que é real, e ainda **acopla** a gestação a um `BreedingEvent` na sincronização (§6). A ausência é um estado legítimo e informativo.

---

## 4. Por que "cadastrar cobertura a partir da gestação" é mais complicado

Uma tentativa natural seria: no endpoint de gestação, criar por baixo um `BreedingEvent` que "gera" a gestação pelo fluxo normal. Na prática, isso **enfraquece várias invariantes** hoje garantidas:

| Invariante atual | Conflito no cadastro retroativo |
|------------------|---------------------------------|
| `ReproductionType` obrigatório (IA ou Monta) | Se o pai é desconhecido (CU-B), não há tipo — exigiria um tipo novo "Desconhecido/Retroativo" ou tornar `SireAnimalId`/`SemenSampleId` nuláveis, quebrando a regra "IA tem sêmen, monta tem touro". |
| IA consome **dose** de sêmen (`SemenSampleMovement`) | Uma IA retroativa **baixaria uma dose real** do estoque — incorreto: a dose não passou pelo sistema. Exigiria pular o movimento (caso especial). |
| RN-18 (bloqueia nova cobertura com diagnóstico pendente) e **RN-19 (bloqueia cobertura para animal prenhe)** | A vaca **já está prenhe** — a própria cobertura retroativa seria **barrada pelas nossas regras**. Exigiria bypass. |
| Gestação só nasce no **diagnóstico = sucesso** | O fluxo retroativo teria que criar a cobertura **e** marcá-la como sucesso **e** gerar a gestação num passo só, furando o workflow de diagnóstico. |

**Conclusão:** derivar uma cobertura a partir da gestação obriga a: (a) tornar touro/sêmen opcionais, (b) pular consumo de dose, (c) burlar RN-18/RN-19, (d) atalhar o diagnóstico. É **mais** código e **mais** exceções — e no fim ainda fabrica um serviço que não existiu. Não compensa.

---

## 5. Abordagem proposta — gestação retroativa **direta**

Coerente com §3.4 e §4: criar a gestação como fato de primeira classe, **sem** cobertura.

### 5.1 Modelo (mudança de nulabilidade + migração)
- `AnimalPregnancy.BreedingEventId` → `int?` (nulável).
- Relação `AnimalPregnancy ⇄ BreedingEvent` vira **opcional**; o índice único vira **filtrado** (`WHERE [BreedingEventId] IS NOT NULL`) — mesmo padrão já usado em `AnimalCalvingCalf.AnimalId` (Spec 6.4).
- `AnimalPregnancyDto.BreedingEventId` → `int?`.
- **Origem retroativa é inferida por `BreedingEventId == null`** — sem coluna/flag nova (decisão de §10, item 3).

### 5.2 Endpoint novo
```
POST /api/animals/{animalId}/pregnancies        (cadastro retroativo)
```
Entrada (`AnimalPregnancyRetroactiveCreateDto`):

| Campo | Obrigatório | Notas |
|-------|-------------|-------|
| `ConfirmationDate` | Sim | Data em que a prenhez foi/está confirmada. |
| `EstimatedConceptionDate` | Condicional | Data estimada de concepção. Se a data prevista de parto **não** for informada, o serviço calcula `ExpectedCalvingDate = EstimatedConceptionDate + GestationDays` (const já existente = 280). |
| `ExpectedCalvingDate` | Condicional | Data prevista de parto informada direto. **Prevalece** sobre o cálculo por concepção quando ambas são informadas. |
| `SireAnimalId` | Não | Touro do rebanho, se conhecido (CU-C). |
| `SemenSampleId` | Não | Sêmen, se conhecido (CU-C). |
| `Notes` | Não | Ex.: "vaca comprada gestante". |
| `ClientRequestId` | Não | Chave de idempotência (RN-07); reenvio com o mesmo valor não duplica a gestação. |

- **Data de parto — aceitar as duas formas (RN-03):** informar **pelo menos uma** entre `EstimatedConceptionDate` e `ExpectedCalvingDate` — **podem coexistir**. Se vier a data prevista, ela prevalece; se vier só a concepção, o serviço a deriva reusando `GestationDays`. Isso atende tanto o produtor que sabe a data do parto quanto o que só sabe estimar a concepção.
- Status inicial = `Confirmed`. `BreedingEventId = null`.
- `SireAnimalId`/`SemenSampleId` são **mutuamente exclusivos** e **opcionais** — capturam o pai **sem** fabricar uma cobertura.

### 5.3 Regras de negócio

| # | Regra | Onde aplicar |
|---|-------|--------------|
| RN-01 | O animal deve ser fêmea `Cow`/`Heifer` **ativa**. Inativo → `ConflictException` (409); classificação inválida → `BusinessRuleException` (422). | Service |
| RN-02 | O animal **não** pode ter gestação ativa confirmada (`HasActiveConfirmedByAnimalIdAsync`) — evita empilhar. | Service → lança `ConflictException` (409) |
| RN-03 | Informar **pelo menos uma** entre `EstimatedConceptionDate` e `ExpectedCalvingDate` (podem coexistir; a data prevista prevalece); a `ExpectedCalvingDate` resultante deve ser `> ConfirmationDate`. | DTO (`IValidatableObject`) + Service |
| RN-04 | Informar **no máximo um** entre `SireAnimalId` e `SemenSampleId`; ambos opcionais. | DTO (`IValidatableObject`) |
| RN-05 | Se `SireAnimalId` informado, deve ser `Animal` `Bull` ativo; se `SemenSampleId`, amostra ativa. | Service → lança `NotFoundException`/`BusinessRuleException` |
| RN-06 | Uma gestação retroativa (`BreedingEventId == null`) **não** consome dose de sêmen mesmo que `SemenSampleId` seja informado — o vínculo é só genealógico. | Service |
| RN-07 | Idempotência no cadastro (ver §6.3): um reenvio do mesmo cadastro **não** cria gestação duplicada. | Service (proposto) |

### 5.4 Onde guardar o pai capturado — **depende da Spec #10**
Duas opções, a alinhar com a decisão de genealogia:
- **Se Spec #10 for "snapshot" (Alt. B):** o pai vai para as FKs de filiação do `Animal`/`AnimalParentage` — a gestação retroativa só as preenche. Encaixe perfeito.
- **Se Spec #10 for "derivação" (Alt. A):** a derivação não acha pai (não há cobertura). Então `SireAnimalId?`/`SemenSampleId?` teriam que morar na **própria `AnimalPregnancy`** e a genealogia passaria a ler "cobertura **ou** pai direto da gestação".

> **Observação de projeto:** o cadastro retroativo é um **argumento a favor do snapshot (Spec #10 Alt. B)** — com snapshot, "pai conhecido sem cobertura" é trivial; com derivação pura, ele não tem onde existir.

---

## 6. Impacto no funcionamento offline e sincronização

> **Premissa (definição de escopo do TCC):** o app deverá futuramente **funcionar offline** e **sincronizar** depois; e, mesmo online, **internet e servidores são instáveis**. Ainda **não há estratégia de sincronização fechada** — esta seção **mapeia o impacto** do cadastro de gestação sem cobertura e **sugere caminhos**, sem decidir a arquitetura de sync (que será uma spec própria).

### 6.1 A gestação direta é, por si só, amiga do offline
O ponto mais importante: **desacoplar a gestação da cobertura reduz o problema de ordem de sincronização.**

- No modelo atual, uma gestação **só nasce** de um `BreedingEvent` diagnosticado. Se isso valesse offline, para sincronizar uma gestação criada no campo o cliente teria de **primeiro** sincronizar (e ter o servidor aceitar) a cobertura + o diagnóstico — um **grafo de dependências** frágil sob internet instável.
- Uma `AnimalPregnancy` com `BreedingEventId = null` depende **apenas do `Animal`** (que já existe no onboarding). É um fato **independente**, que sincroniza sozinho. Menos arestas no grafo = menos falhas parciais de sync.

**Conclusão:** a mesma decisão que resolve o onboarding (§3.4/§5) também é a mais barata de sincronizar. Isso reforça a recomendação de **não simular** cobertura.

### 6.2 Impactos a considerar (independem da arquitetura final de sync)

| # | Impacto | Descrição | Severidade |
|---|---------|-----------|------------|
| I-1 | **Identidade gerada no servidor** | `BaseEntity.Id` é `int` identity (gerado no INSERT do servidor). Um registro criado offline não tem Id estável até sincronizar — e registros que **dependem dele** (parto/perda retroativos da mesma gestação, feitos offline) não têm como referenciá-lo. | Alta |
| I-2 | **Reenvio sob instabilidade (idempotência)** | Com internet ruim, o cliente pode reenviar o `POST` sem ter recebido o `201`. Sem idempotência, cria **gestação duplicada**. RN-02 barra empilhar gestação *ativa*, mas **não** cobre um reenvio idêntico legítimo. | Alta |
| I-3 | **Ordem de dependências no sync** | Mitigada por §6.1 (gestação só depende do `Animal`). Resta a dependência opcional do pai (`SireAnimalId`/`SemenSampleId`): se o touro também foi cadastrado offline, precisa sincronizar antes — ou o vínculo é adiado. | Média |
| I-4 | **Conflitos de duas origens** | App offline e web podem registrar gestação para o **mesmo animal**. No momento do sync, RN-02 dispara `409`. É preciso uma **política de conflito** (rejeitar, mesclar, manter a mais recente). | Média |
| I-5 | **Relógio do cliente** | `ConfirmationDate`, `EstimatedConceptionDate`, `CreatedAt` vêm do dispositivo, cujo relógio pode estar errado. Menos crítico aqui (datas passadas, digitadas), mas o servidor deve **validar** (RN-03) e não confiar cegamente em `CreatedAt` do cliente para ordenação de conflitos. | Baixa |
| I-6 | **Custo de round-trips (CU-B)** | Compra de vaca gestante = entrada do animal (Spec #2) **+** gestação. Dois `POST` sequenciais e dependentes dobram os round-trips num link instável (ver memória de *design para ambiente restrito*). | Média |

### 6.3 Caminhos sugeridos

**Caminho 1 — Endpoint direto forward-compatible (mínimo, recomendado agora)**
Implementar §5 como está, com `int Id`, **sem** motor de sync ainda, mas já preparando o terreno:
- Aceitar no DTO um **`ClientRequestId` (Guid) opcional** como **chave de idempotência** (RN-07). O servidor guarda o par (`ClientRequestId` → gestação criada); um reenvio com o mesmo `ClientRequestId` **retorna a gestação existente** (200) em vez de criar outra. Resolve I-2 sem exigir toda a arquitetura offline.
- Manter a gestação dependente só do `Animal` (§6.1). Nada bloqueia o sync futuro.

**Caminho 2 — Identidade estável para criação offline (quando o sync entrar)**
Para I-1/I-3, introduzir uma **chave estável gerada no cliente** nas entidades criáveis offline — um `Guid`/`ULID` único (como PK **ou** como coluna secundária `SyncId` única) — para que dependentes offline (parto/perda) referenciem a gestação **antes** de ela sincronizar. É mudança transversal (`BaseEntity` + migração) e **deve ser decidida na spec de sincronização**, não aqui. Este spec apenas **não impede**: o `ClientRequestId` do Caminho 1 pode evoluir para essa chave.

**Caminho 3 — Cadastro em lote para CU-B (menos requests)**
Para I-6, permitir que a entrada de vaca comprada gestante (Spec #2) **embuta** a gestação num único payload (recurso aninhado), em vez de dois `POST` dependentes. Alinha com a diretriz de **menos requisições / aninhar recursos pequenos** para ambiente restrito. A decisão de aninhar vs. endpoint independente é a questão em aberto §10.4.

**Fora de escopo aqui:** o motor de sincronização em si (fila offline, resolução de conflito I-4, política de merge, mapeamento de IDs) → **Spec #14 (Sincronização Offline)**, que generaliza os ganchos daqui (`SyncId`, `ClientRequestId`, LWW, ordenação por dependência). Esta spec só garante que o **modelo de gestação retroativa não cria obstáculos** para ela.

---

## 7. Genealogia do bezerro nascido de gestação retroativa

- **Mãe:** sempre conhecida (`pregnancy.AnimalId` → `AnimalCalving.AnimalId`). ✅
- **Pai:** `null` (desconhecido) por padrão; preenchido se CU-C informou `SireAnimalId`/`SemenSampleId`.
- Consumidores da genealogia (Spec #10) devem exibir **"pai desconhecido"** quando nulo — nunca fabricar.

> Modelagem final da filiação → Spec #10. Aqui só se **alimentam** os campos de pai.

---

## 8. O que continua funcionando sem mudança (confirmado no código)

- **Status "Prenhe"** deriva da gestação (`HasActiveConfirmedByAnimalIdAsync`), não da cobertura → correto.
- **RN-19 + autocomplete de cobertura** já filtram por *gestação confirmada ativa* → a vaca com gestação retroativa **não** recebe nova cobertura nem aparece no autocomplete. Consistente.
- **Parto** (`AnimalCalvingService.CreateAsync`) só depende da gestação (status `Confirmed`, `ConfirmationDate`) → gera o bezerro normalmente.
- **Mapeamentos:** `AnimalPregnancyProfile` **não** desreferencia `BreedingEvent` → sem risco de `NullReferenceException` com `BreedingEventId = null`.

---

## 9. Índices zootécnicos (Spec #7)

Gestações com `BreedingEventId == null` **não** têm serviço associado. Métricas baseadas em cobertura (taxa de concepção, serviços/concepção, dias em aberto, intervalo cobertura–concepção) devem **excluí-las** (facilmente distinguíveis pelo FK nulo). Métricas baseadas só em prenhez/parto (nº de gestações, nascimentos) as incluem normalmente.

---

## 10. Impactos e camadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `AnimalPregnancy.cs` | `BreedingEventId` → `int?`; (se §5.4 opção A-derivação) adicionar `SireAnimalId?`/`SemenSampleId?` |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Relação com `BreedingEvent` opcional; índice único **filtrado**; FKs de pai `Restrict` (se aplicável) |
| `Infrastructure/Migrations` | *(nova)* | **Requer aprovação** — nulabilidade + índice filtrado + FKs (+ `ClientRequestId` se adotado no Caminho 1) |
| `Application/DTOs` | `AnimalPregnancyRetroactiveCreateDto` (novo); `AnimalPregnancyDto` (`BreedingEventId` → `int?`) | Criar/ajustar |
| `Application/Interfaces` + `Application/Services` | `IAnimalPregnancyService` / `AnimalPregnancyService` | `CreateRetroactiveAsync` com RN-01..07; reusar `GestationDays` |
| `Api/Controllers` | `PregnanciesController` | `POST /api/animals/{animalId}/pregnancies` |
| `Program.cs` | — | Sem novos registros de DI (serviços/repos já existentes) |

---

## 11. Questões em aberto (para discutir com o parceiro de TCC)

1. **Local do pai capturado (§5.4):** FKs na `AnimalPregnancy` (derivação) ou snapshot no `Animal` (Spec #10 Alt. B)? — decisão conjunta com a Spec #10.
2. **Idempotência (§6.3, Caminho 1):** adotar `ClientRequestId` já neste spec, ou deixar toda idempotência para a spec de sincronização? (Recomendação: adotar já — é barato e resolve I-2 no online instável.)
3. **Chave estável offline (§6.3, Caminho 2):** confirmar que a decisão de `Guid`/`SyncId` fica na spec de sincronização, não aqui.
4. **Compra gestante (CU-B, §6.3 Caminho 3):** cadastro da gestação **aninhado** na entrada por compra (Spec #2) ou endpoint independente chamado depois?
5. **Perda/parto retroativos:** permitir também registrar perda ou parto já ocorridos de uma gestação retroativa (datas passadas)?

---

## 12. Fora do Escopo desta Spec

- **Motor de sincronização offline** (fila, resolução de conflito, mapeamento de IDs, política de merge) → **Spec #14 (Sincronização Offline)**. Aqui só se mapeia o impacto e sugerem-se ganchos (§6).
- **Modelagem final da genealogia** → Spec #10 (esta spec apenas alimenta os campos de pai).
- **Cálculo de índices zootécnicos** → Spec #7 (aqui só se define a exclusão das gestações sem cobertura).
- **Fluxo normal de cobertura → gestação** → Specs #5 e #6.
- **Entrada de animais por compra (dados gerais)** → Spec #2.
