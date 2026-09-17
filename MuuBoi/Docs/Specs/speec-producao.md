# Spec — Produção Leiteira e Status de Lactação

**Área de feature:** Produção (leite) e ciclo de lactação
**Status:** Em iteração — decisões D1–D11 firmadas; O1–O4 em aberto
**Specs relacionadas:** #5 (Eventos Reprodutivos), #6 (Gestação/Parto)
**Última atualização:** 2026-08-30 (rev. 2 — adiciona D11: onboarding de lactações pré-existentes)

---

## 1. Contexto e escopo

O produtor-piloto não mede o leite por animal: ele registra apenas o **total diário
de leite do rebanho**. A secagem das vacas é feita de forma medicada (terapia da vaca
seca), como uma ação pontual, e não por decurso de tempo.

Esta spec define o fluxo produtivo mínimo que sustenta, hoje e no futuro, os índices:

- Produção de leite por vaca por período (dia ou período selecionado)
- Produção do rebanho por período (dia ou período)
- Dias em lactação (DEL) por animal

Requisitos transversais: **baixa complexidade** (sem job agendado, sem estado mutável
espelhado) e **comportamento offline-first** (sinal ruim, servidor possivelmente instável).

**Restrição-chave reconhecida:** com dado apenas de total diário, *não existe* produção
individual medida. O que é entregável é a **média por vaca em lactação** (derivada). Ver D1.

---

## 2. Fundamentos do domínio (base para as decisões)

- **Ciclo:** parto → início da lactação → secagem → período seco → novo parto.
- **DEL (dias em lactação)** é o tempo decorrido do parto até o fim da lactação; o ideal
  de referência é ~305 dias (10 meses).
- **Período seco** ideal ~60 dias antes do parto seguinte, para regeneração do úbere.
- **Secagem medicada** = terapia da vaca seca (antibiótico intramamário de longa ação nos
  tetos, geralmente com selante). É uma **ação explícita** do produtor, não um evento por tempo.
- **Índices produtivos** consagrados incluem produção diária, proporção de vacas em
  lactação e dias em leite — família na qual os índices desta spec se encaixam.

Consequência de projeto: a transição **lactação → seca é dirigida por evento**, o que
elimina a necessidade de job agendado para esse status (diferente das transições
reprodutivas por tempo tratadas na Spec #5). Fontes na seção 9.

---

## 3. Decisões

### D1 — "Por vaca" no curto prazo é **média por vaca em lactação**, não produção individual

**Decisão:** o índice "produção/vaca" é calculado como `total do período ÷ vaca-dias em
lactação no período`. Produção individual medida fica reservada para o futuro (D7).

**Justificativa:** o produtor só fornece o total do rebanho. Desagregar um total em
valores individuais que nunca foram medidos seria inventar dado. A média por vaca em
lactação é um índice zootécnico legítimo e honesto com a fonte.

**Alternativas descartadas:**
- *Ratear o total igualmente e persistir como "produção individual":* cria dado falso e
  induz o usuário a interpretar como medição real.

**Camadas impactadas:** Application (serviço de índices), UI (rótulo do índice deve dizer
"média por vaca em lactação").

---

### D2 — Status de lactação é **derivado**, nunca armazenado como flag

**Decisão:** não existe coluna booleana `IsLactating`. O status é uma projeção calculada
sobre o fato do ciclo (D3).

**Justificativa:** coerente com o princípio de não manter status espelhado (evita dado
velho e job de atualização). O status "em lactação" é sempre recomputado a partir da
existência de uma `Lactation` aberta.

**Alternativas descartadas:**
- *Coluna de status mutável em `Animal`:* fica desatualizada, exige atualização
  transacional a cada evento e conflita em cenário offline.

**Camadas impactadas:** Domain, Application (resolver de status).

---

### D3 — Entidade `Lactation` como fato do ciclo

**Decisão:** modelar `Lactation` como registro de um ciclo real:
- **Parto abre** a `Lactation`: `StartDate` = data do parto, `EndDate` = null (aberta/em lactação).
- **Secagem fecha** a `Lactation`: `EndDate` = data da secagem.
- **Invariante:** no máximo uma `Lactation` aberta por animal.

**Justificativa:** `Lactation` não é estado duplicado — é o fato de origem (um ciclo com
início e fim, como um período de vigência). O status continua derivado (D2): "em lactação"
⇔ existe `Lactation` com `EndDate == null`. Pré-materializar o par parto/secagem torna
trivial a query do denominador dos índices.

**Alternativas descartadas:**
- *Derivar tudo de eventos soltos (Parto/Secagem) sem entidade de ciclo:* a contagem de
  "quantas em lactação no dia D" vira consulta *gaps-and-islands* (parear cada parto com a
  secagem seguinte). Mais complexo do que o requisito de simplicidade admite.

**Camadas impactadas:** Domain (`Lactation`), Persistence (tabela + índices), Application.

---

### D4 — Transição lactação → seca é **event-driven**, sem job agendado

**Decisão:** a secagem é registrada como evento explícito que fecha a `Lactation`. Não há
processo temporal inferindo secagem.

**Justificativa:** a secagem real é medicada e pontual; o status muda exatamente quando o
produtor registra a ação. Elimina scheduler para este status.

**Alternativas descartadas:**
- *Inferir secagem por tempo (ex.: 305 dias após o parto):* contradiz a operação real e
  reintroduz a dependência de job que se quer evitar.

**Camadas impactadas:** Application, API (endpoint de secagem).

---

### D5 — DEL calculado on-the-fly

**Decisão:** `DEL = referenceDate − Lactation.StartDate`, congelado em `EndDate` quando a
lactação está fechada. Nada é armazenado ou atualizado diariamente.

**Justificativa:** aritmética de data pura; recalcular é mais barato e mais seguro do que
manter contador.

**Camadas impactadas:** Application (serviço de índices), Mobile (cálculo local possível).

---

### D6 — `MilkProduction` como total diário do rebanho

**Decisão:** entidade `MilkProduction` com `Date` e `Liters` (`decimal`). **Permitir
múltiplos lançamentos por dia** (ex.: ordenha da manhã e da tarde) que **somam**; campo
opcional `Milking` para rotular a ordenha.

**Justificativa:** casa com registro incremental e offline (lançar quando ordenha, sem
depender de editar "o registro do dia"). `decimal` evita a armadilha de tipo em agregação.

**Alternativas descartadas:**
- *Um único registro por dia com upsert:* obriga leitura-modificação-escrita, pior para
  append offline e mais sujeito a conflito.

**Camadas impactadas:** Domain, Persistence, API, Mobile.

---

### D7 — Costura para produção individual futura, sem retrabalho

**Decisão:** a camada de índices lê de uma **fonte de produção abstrata**. Hoje a fonte é
`MilkProduction` (total manual). No futuro, adicionar entidade opcional `MilkYield` (por
animal/ordenha); quando presente, o total do dia passa a ser a soma dos individuais.

**Justificativa:** desenhar a junta agora permite introduzir medição individual (medidor
na ordenha) sem redesenho da camada de índices — só troca a fonte.

**Alternativas descartadas:**
- *Amarrar os índices diretamente à tabela de total:* obrigaria reescrever a camada de
  índices quando surgir medição individual.

**Camadas impactadas:** Application (contrato da fonte de produção). `MilkYield` fica
**fora do escopo atual** (seção 8), apenas com a junta prevista.

---

### D8 — Índices calculados on-the-fly por agregação

**Decisão:** todos os índices são consultas de agregação; nenhum estado derivado é
persistido. O denominador de "por vaca" é **vaca-dias em lactação**. Somas usam tipo largo
(`decimal`/`bigint`).

**Justificativa:** as fórmulas são simples (seção 5) e evitam tabelas de resumo que
precisariam de manutenção. O cast largo evita overflow em `SUM()`.

**Camadas impactadas:** Application, Persistence (índices de apoio).

---

### D9 — Offline-first: sincronizar fatos crus, não estado calculado

**Decisão:**
- O dispositivo sincroniza **fatos** (totais de leite, eventos de parto/secagem), nunca
  status nem DEL.
- Cada `MilkProduction`, parto e secagem nasce com **`Id` (GUID) gerado no cliente**; o
  servidor faz **upsert idempotente** por esse id.
- Operações são replay-safe (total diário chaveado por `(FarmId, Date, Id)` e somado;
  eventos são fatos pontuais).
- Índices podem ser computados localmente a partir do Room para visualização offline; o
  servidor recomputa de forma autoritativa na sincronização.

**Justificativa:** como status/DEL são derivados (D2/D5), não há "conflito de status" —
ninguém escreve status. Sincroniza-se o análogo dos lançamentos de uma conta, não o saldo;
qualquer nó recalcula. GUID no cliente mata duplicação por retry em rede instável.

**Alternativas descartadas:**
- *Sincronizar o status/DEL calculado:* gera conflitos entre nós e depende de ordem de
  chegada.
- *Id gerado no servidor:* impede idempotência em reenvio offline.

**Camadas impactadas:** API (contrato de upsert idempotente), Mobile (fila de sync, Room),
Persistence.

---

### D10 — Vínculo `Lactation` ↔ parto reprodutivo, com responsabilidades separadas

**Decisão:** o registro de parto do lado reprodutivo (Spec #6) é o gatilho que abre a
`Lactation`, na mesma ação. Guardar `Lactation.CalvingEventId` como elo opcional.

**Justificativa:** reprodução e produção são assuntos distintos; o elo dá rastreabilidade
sem acoplar os modelos.

**Camadas impactadas:** Domain, Application (orquestração parto → abertura de lactação).

---

### D11 — Onboarding de lactações pré-existentes (lactação sem parto vinculado)

**Decisão:** no primeiro uso do app, vacas já lactando são cadastradas com uma `Lactation`
**aberta e sem parto vinculado** (`CalvingEventId == null`). A `StartDate` (data do parto
que iniciou a lactação atual) é **obrigatória**, ainda que estimada. Marcar a proveniência
com `Origin = InitialSeed` e sinalizar data aproximada com `StartDateEstimated = true`
quando for palpite do produtor.

**Justificativa:** os índices produtivos desta spec dependem da **data** (`StartDate`), não
do objeto Parto — DEL, `cowLactationDays` e proporção só usam `StartDate`. Logo, a lactação
"semeada" alimenta corretamente todo o lado produtivo desde o dia um. É o equivalente a um
**saldo de abertura** contábil: um ponto de partida declarado, não um lançamento histórico.

`StartDate` continua **obrigatória** por integridade dos índices: uma vaca marcada como em
lactação mas sem `StartDate` seria contada na proporção porém sumiria de `cowLactationDays`
(a fórmula de sobreposição exige um início), encolhendo o denominador e **inflando
artificialmente a média por vaca**. Sem `StartDate`, portanto, não se abre a lactação.

**Implicações no dado futuro (todas no lado reprodutivo e auto-limitadas):**
- *IEP (intervalo entre partos):* o primeiro intervalo dessas vacas não é calculável (sem
  parto anterior registrado), ou fica estimado se a `StartDate` semeada for usada como proxy.
- *Ordem de lactação / paridade:* desconhecida, salvo se o produtor informar. Só relevante
  para índices por ordem de lactação (fora de escopo).
- *Auto-cura:* o efeito do onboarding afeta **apenas o primeiro ciclo de cada vaca**. Ao ser
  seca no app (fecha a lactação) e parir novamente (abre nova, agora com `CalvingEventId`),
  o animal passa a ser 100% event-driven. Não se propaga aos índices produtivos.

**Alternativas descartadas:**
- *Exigir um evento de Parto sintético no onboarding:* injeta um parto que nunca foi
  observado no lado reprodutivo, poluindo IEP/paridade com dado falso. Pior do que assumir a
  lacuna explicitamente.
- *Permitir lactação aberta sem `StartDate`:* quebra a consistência entre proporção e média
  por vaca (ver acima).
- *Derivar a proveniência só de `CalvingEventId == null`:* funciona, mas `Origin` explícito
  é mais legível em query/relatório e sobrevive a futuras origens de lactação.

**Camadas impactadas:** Domain (`Origin`, `StartDateEstimated`), Application (fluxo de
onboarding em lote), API (endpoint/carga inicial), Mobile (tela de cadastro: "está em
lactação?" → "desde quando?" com opção de data aproximada), UI/Relatórios (marcar DEL
dessas vacas como aproximado enquanto `StartDateEstimated`).

---

## 4. Modelo de dados

Identificadores em inglês. Todas as entidades carregam `FarmId` (raiz de tenant) e entram
no global query filter por `_farmId` do `DbContext` (padrão já vigente no projeto).

### `Lactation`
| Campo | Tipo | Observações |
|---|---|---|
| `Id` | `Guid` | Gerado no cliente (idempotência de sync) |
| `FarmId` | tenant | Sob global query filter |
| `AnimalId` | FK | Animal correspondente |
| `StartDate` | `date` | Data do parto (abre a lactação) |
| `EndDate` | `date?` | Data da secagem; `null` = em lactação |
| `CalvingEventId` | `Guid?` | Elo opcional ao parto reprodutivo (D10); `null` em lactações semeadas (D11) |
| `Origin` | enum | `Calving` (aberta por parto) \| `InitialSeed` (semeada no onboarding — D11) |
| `StartDateEstimated` | `bool` | `true` quando a `StartDate` foi estimada pelo produtor (D11) |

**Invariante:** no máximo uma linha com `EndDate == null` por `AnimalId`.
**Regra (D11):** `StartDate` é obrigatória em toda lactação, inclusive nas `InitialSeed`.
**Índice sugerido:** `(FarmId, AnimalId, EndDate)` para localizar a lactação aberta.

### `MilkProduction`
| Campo | Tipo | Observações |
|---|---|---|
| `Id` | `Guid` | Gerado no cliente |
| `FarmId` | tenant | Sob global query filter |
| `Date` | `date` | Dia de produção |
| `Liters` | `decimal` | Total do lançamento |
| `Milking` | enum? | Opcional (ex.: Morning/Evening); múltiplos por dia somam |

**Índice sugerido:** `(FarmId, Date)` para agregação por período.

### `MilkYield` — **fora do escopo atual** (junta prevista em D7)
`Id`, `FarmId`, `AnimalId`, `Date`, `Liters`, `Milking?`. Só será introduzida quando
houver medição individual na ordenha.

---

## 5. Cálculo dos índices

Convenção: vaca está em lactação nos dias `[StartDate, EndDate)` (ver O1).

**Produção do rebanho por período**
```
SUM(Liters) WHERE FarmId = @farm AND Date BETWEEN @start AND @end
```

**Média por vaca em lactação no período** (o "por vaca" de D1)
```
mediaPorVaca = totalLiters(@start,@end) / cowLactationDays(@start,@end)

cowLactationDays = Σ  (min(EndDate ?? @end+1, @end+1) − max(StartDate, @start))
                   sobre cada Lactation que sobrepõe [@start, @end]
```
Interpretação: litros por vaca por dia, ponderado corretamente por quanto tempo cada vaca
esteve em lactação dentro do período.

**Dias em lactação (DEL) por animal**
```
DEL = (referenceDate − StartDate)   -- para a Lactation aberta do animal
```

**Proporção de vacas em lactação** (índice-bônus, padrão de mercado)
```
proporcao = vacasEmLactacao(@date) / totalVacasAdultas(@date)

vacasEmLactacao(@date) = COUNT(Lactation)
    WHERE StartDate <= @date AND (EndDate IS NULL OR EndDate > @date)
```

Todas as somas devem usar tipo largo (`decimal`/`bigint`) na projeção agregada.

**Dependência de qualidade de dado:** os índices valem o que vale o log de eventos. Se uma
secagem não for registrada, o denominador (`cowLactationDays`) infla e a média cai
artificialmente. O log de eventos é a fonte de verdade — não há compensação automática de
evento faltante.

---

## 6. Contrato de sincronização (offline-first)

Resumo operacional das decisões D5, D8 e D9:

1. **Payloads são fatos crus:** `MilkProduction`, evento de parto, evento de secagem.
   Nunca status nem DEL.
2. **Upsert idempotente por `Id` (GUID do cliente):** reenvio após falha de rede não
   duplica.
3. **Comutatividade:** totais do mesmo dia somam por `Id`; eventos são pontuais.
   Reprocessamento é seguro.
4. **Leitura offline:** app computa DEL e total do dia localmente a partir do Room.
5. **Autoridade:** servidor recomputa índices na sincronização; como nada de derivado é
   transmitido, não há conflito de status entre nós.

---

## 7. Decisões em aberto

- **O1 — Fronteira do dia da secagem.** No dia da secagem a vaca conta como em lactação ou
  já seca? Recomendação: em lactação em `[StartDate, EndDate)` (seca a partir da data da
  secagem). *A definir.*
- **O2 — Colostro.** A secagem leva ~2 semanas até cessar a produção, e no início há
  colostro (primeiros dias), normalmente fora do tanque. Para o status, a vaca é "em
  lactação" desde o parto. Se o colostro entra ou não no **total registrado** é decisão de
  lançamento do produtor, não do modelo. *Confirmar com o produtor.*
- **O3 — Invariante de lactação aberta.** Registrar parto com uma lactação ainda aberta
  deve dar **erro** ou **fechar a anterior**? Recomendação: validar e barrar (qualidade de
  dado). Observação: o onboarding (D11) é o caso legítimo de **criar** lactações abertas em
  lote — a invariante "no máximo uma aberta por animal" vale igualmente ali (uma vaca não
  pode ser semeada com duas lactações abertas). *A definir apenas o comportamento no parto.*
- **O4 — Novilha nulípara × vaca seca.** Para o denominador de produção, ambas são "não
  lactantes" (tanto faz). Distinção semântica (nulípara vs. seca) fica derivável dos mesmos
  fatos se necessária no futuro. *Fora do escopo atual.*

---

## 8. Fora de escopo (por ora)

- `MilkYield` / medição individual por animal (junta prevista em D7).
- Curva de lactação, persistência, pico de produção e demais índices avançados.
- RFID/EID e OCR de brinco (tratados em outra frente).
- Distinção de status reprodutivo detalhado (Spec #5).

---

## 9. Referências

- Embrapa Gado de Leite — *Período Seco* (Ageitec): secagem ~60 dias antes do parto para
  descanso e regeneração do úbere.
- Embrapa — *Terço médio e final da lactação*: encerramento da lactação e início do preparo
  para o próximo parto.
- MilkPoint / EducaPoint — *Período de lactação e dias em leite*: definição e interpretação
  dos índices; período médio de lactação ~305 dias.
- MilkPoint — *Curva de lactação*: secagem em média aos ~305 dias, seguida de período seco
  de ~60 dias.
- JA Saúde Animal — *Processo de secagem*: terapia da vaca seca (antibiótico intramamário +
  selante) como base da secagem medicada.
- UFPel (SIEPE) — *Avaliação de dias em lactação (DEL)*: DEL como tempo do parto ao fim da
  lactação e sua leitura de desempenho produtivo/reprodutivo.