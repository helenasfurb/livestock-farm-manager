Spec #18 — Dashboards de Índices (Produtivo · Reprodutivo · Estoque)

Módulo: Dashboards / Índices zootécnicos
Versão: 0.2 (rascunho — §6.2.1 refinada e implementada: índices da ficha do animal)
Data: 12/Set/2026
Fonte: Decks VetSymbio Jr (Sem. 2–4: Gestão Produtiva, Controle Sanitário, Gestão de Estoque); specs de eixo #5, #6.1, #6.2, #11/#11.1/#11.2, #17. Materializa a "Spec #7 (Dashboards)" referenciada por #5, #6.1 e #11.
Status: Especificada (não implementada) — decisões-base fechadas; itens dependentes e em aberto em §9.
Depende de: #5 (Eventos Reprodutivos), #6.1 (Gestação), #6.2 (Parto), #11 / #11.1 / #11.2 (Produção e Lactação), #17 (Estoque). Opcional: #14 (Vacinação), #16 (Tratamentos/Mastite).
Referenciado por: —

1. Contexto e Objetivo

Esta spec define os dashboards de índices do MuuBoi — produtivo, reprodutivo e de estoque — mais um dashboard sanitário como escopo opcional adicional (D11). Cada dashboard opera em dois níveis: propriedade (rebanho) e animal (drill-down) — D3.

É a materialização da "Spec #7 (Dashboards)" que #5, #6.1 e #11 já citam como consumidora de seus dados (ServiceNumber para "% prenhez ao 1º serviço", Lactation.StartDate para DEL, BreedingEvent.Status para taxas). Este documento não redefine as entidades dos eixos, nem o dashboard de estoque já especificado no #17 — ele agrega e deriva na leitura os índices e descreve como são apresentados, remetendo às specs de eixo como fonte de verdade.

Princípios herdados (as specs #5, #11 e #17 já os aplicam):

Nunca armazenar estado derivável — toda taxa, contagem, faixa e status é calculada na leitura sobre fatos brutos; sem coluna espelho.

Sem jobs agendados — índices por tempo (DEL, dias em aberto, elegibilidade para IA, cobertura/ruptura de estoque) resolvem-se na leitura, comparando o relógio (hoje) com as datas do ledger/eventos.

Set-based / bulk, sem N+1 — todo índice sobre o rebanho resolve em nº fixo de queries (padrão ReproductiveStatusResolver / StockForecastResolver).

Ortogonalidade estrita — dashboards são somente-leitura e não escrevem em nenhum eixo; cruzamentos entre eixos são JOIN de leitura.

Tenant por PropertyId — todo cálculo e endpoint escopado pelo query filter global (D12).

2. Escopo

2.1 Dentro

Resolvers/helpers e endpoints de leitura dos índices produtivos e reprodutivos, derivados na leitura, set-based.

Reuso do StockForecastResolver do #17 para os números de estoque (mesmo padrão client-side dos demais); os endpoints /api/stock/dashboard e /api/stock/alerts do #17 como via de carga/sync — sem redefinição.

Tabela de parâmetros de referência por propriedade (faixas de DEL, PEV, metas) — D8.

Telas mobile (Jetpack Compose) dos dashboards: seletor de período, drill-down por animal, e o comportamento de exibição de índice indisponível/estimado.

Dashboard sanitário especificado como opcional adicional (D11), sobre #14/#16.

2.2 Fora

Item

Justificativa

Produção individual por animal (curva de lactação, pico, persistência, produção por lactação)

Fora de escopo por #11 §8: o leite é total do rebanho (MilkProduction). O entregável por vaca é a média por vaca em lactação (D6). A junta para medição individual (MilkYield) já está prevista em #11 D7.

Distribuição do rebanho por faixa de DEL (histograma)

Fora de escopo por ora (decisão do produto). As faixas canônicas (D9) se sobrepõem (45–60 cai em duas), então exigem cortes não sobrepostos antes de virar histograma — ver §9/Q7. O DEL individual (no detalhe do animal) permanece.

Dashboard / agregados por animal (separados)

Uma vaca não tem taxa nem distribuição (conceitos de rebanho). Os índices por animal vivem na ficha existente (GET /api/animals/{id} + históricos), aberta por drill-down da lista ou do dashboard (D3).

Timeline do animal

Não necessária a princípio (decisão do produto). Os índices por animal ficam em campos diretos: DEL, data do parto, genealogia, IEP, data de liberação do leite.

Eixo de dieta / consumo por categoria-animal

Dieta é opcional e não decidida (#17 §9, D11). Consumo/previsão de estoque usam o fluxo real do ledger (D10).

Offline-first

Server-side agora; offline-first é evolução futura aditiva (§8), mesmo racional de #14/#16/#17.

Redefinição do dashboard de estoque

Já é do #17. Esta spec reusa o resolver dele (D10).

Detecção de estro/cio

Fora do escopo do produto (#5).

3. Decisões

D1 — Server-side agora; offline-first como evolução futura aditiva

Decisão: os dashboards são endpoints de leitura server-side que computam os índices na hora, set-based, com tenant por PropertyId. O offline-first (cache no device, cálculo local, sincronização) fica como evolução futura aditiva (§8), não no v1.

Justificativa: é o padrão real e mais recente da casa — #14, #16 e #17 foram todas "reenquadradas para o padrão server-side", com offline documentado como aditivo. Como os índices são funções puras sobre fatos (D2), o mesmo resolver roda depois sobre o cache local sem reescrita.

Alternativas descartadas:

Local-first como caminho primário já no v1: descartado — desalinha do estado atual dos eixos (todos server-side) e anteciparia infra de sync/Room que o projeto ainda trata como futura.

Camadas impactadas: Api (controllers de dashboard), Application (resolvers/helpers), Mobile (consome os endpoints; cache é §8).

D2 — Índices derivados na leitura, por resolver/helper set-based

Decisão: nenhum índice é persistido. Cada dashboard tem um resolver/helper puro (padrão ReproductiveStatusResolver / StockForecastResolver); o serviço reúne os fatos em queries agregadas de custo fixo e o resolver projeta os números. Reusa IReproductiveStatusResolver (#5) e StockForecastResolver (#17).

Justificativa: "never store derived", "no scheduled jobs", "bulk over N+1" — os três já vigentes. Concentra a regra num ponto testável por eixo.

Camadas impactadas: Application (helpers/serviços de índice), Infrastructure (métodos de repositório agregados).

D3 — Dois níveis: propriedade (dashboard) e animal (ficha existente)

Decisão: o dashboard é de propriedade (rebanho): agregados, distribuições e taxas. O nível animal não é um dashboard separado nem agregados por animal — é a ficha do animal já existente (GET /api/animals/{id} + #6.2), que é a fonte única por animal. Ela é aberta ao tocar num animal (drill-down) tanto na lista de animais comum quanto nas listas temáticas do dashboard ("elegíveis para IA", "leite retido"). Os índices por animal que o #18 contempla são: DEL (dias em lactação), data do último parto, genealogia (pai/mãe), intervalo entre partos e data de liberação do leite (fim da carência). Timeline não entra a princípio.

Justificativa: uma vaca sozinha não tem taxa nem distribuição — esses são conceitos de rebanho. O que ela tem é estado + histórico, que a ficha já serve. Um "dashboard por animal" duplicaria a ficha e arriscaria divergir; reusar a ficha mantém fonte única e enxuga o escopo do #18. Os selos leves de estado (status reprodutivo/sanitário, leite retido) já vêm no AnimalListItemDto (#5/#6/#16), então a lista já mostra o resumo antes de abrir.

Camadas impactadas: Mobile (drill-down da lista de animais e das listas do dashboard para a ficha); Application/Api (reusa GET /api/animals/{id} + #6.2 para parto/IEP — DEL, data do parto, genealogia, IEP, data de liberação do leite; nada de endpoint de agregado por animal).

D4 — Janela de período livre por intervalo de datas; presets; default mês corrente

Decisão: taxas e agregações por período usam uma janela livre por intervalo (dateFrom/dateTo) — não é travada em mês. O default, sem datas, é o mês corrente (convenção herdada do #17). Sobre o mesmo endpoint, o frontend oferece presets: Hoje · 3 dias · 7 dias · Mês · Personalizado (intervalo). Cada preset é só um par dateFrom/dateTo; nenhum exige mudança de backend.

Justificativa: a mesma fórmula muda de número conforme a janela; deixá-la livre e navegável cobre tanto o acompanhamento operacional (dia/semana) quanto a comparação histórica (mês/ano), sem ambiguidade.

Cuidado (janelas curtas): as taxas (concepção, prenhez ao 1º serviço) ficam ruidosas em janela curta — o denominador fica pequeno e o valor oscila. Ao permitir dia/3 dias, exibir o número absoluto ao lado (ex.: "47% — 7 de 15 coberturas") para não ler pico falso. Produção não sofre disso (a spec já prevê L/dia, L/mês, L/ano).

Alternativas descartadas:

Filtro só mensal: descartado — o produtor precisa também de janelas curtas (dia/semana); o intervalo livre cobre os dois sem custo de backend.

Camadas impactadas: Api (query params dateFrom/dateTo), Mobile (seletor com presets + intervalo personalizado; contagem absoluta ao lado das taxas em janela curta).

D5 — Taxa de concepção: bucket "aguardando diagnóstico" fora do denominador

Decisão: taxa_concepção = coberturas Successful ÷ coberturas diagnosticadas (Successful + Unsuccessful) × 100, na janela. Coberturas com Status = AwaitingDiagnosis não entram no denominador — vão para um bucket "aguardando diagnóstico" exibido à parte.

Justificativa: o desfecho por cobertura já existe no modelo (BreedingEvent.Status, #5), então o bucket é literal — não é inferência. Contar pendentes como não-prenhes enviesaria a taxa para baixo no fim de cada janela.

Alternativas descartadas:

Pendente = não-prenhe: viés sistemático para baixo.

Camadas impactadas: Application (resolver reprodutivo classificando por Status), Mobile (card + bucket).

D6 — "Por vaca" produtivo = média por vaca em lactação (não individual)

Decisão: o índice "produção/vaca" é a média por vaca em lactação = totalLitros(período) ÷ cowLactationDays(período), exatamente como #11 D1/§5. Produção individual, curva, pico e persistência ficam fora de escopo (#11 §8), reservados para quando entrar MilkYield (#11 D7).

Justificativa: o produtor só registra o total diário do rebanho. Desagregar em individuais que nunca foram medidos inventaria dado. A média por vaca em lactação é o índice honesto com a fonte.

Camadas impactadas: Application (índice produtivo), Mobile (rótulo deve dizer "média por vaca em lactação", não "produção da vaca").

D7 — Índices reprodutivos avançados dependem de #5/#6.1/#6.2 (aprovados) — não bloqueados

Decisão: serviços por concepção, dias em aberto, intervalo entre partos (IEP), perda gestacional e "% prenhez ao 1º serviço" são especificados aqui e ligados quando os repositórios de #6.1/#6.2 existirem. São dependências de specs aprovadas, não bloqueios em aberto.

Justificativa: #5 (BreedingEvent + ServiceNumber), #6.1 (AnimalPregnancy + LostPregnancy/LossDate) e #6.2 (AnimalCalving + data do parto) estão todos "Aprovado para implementação". Não há decisão de produto pendente — só ordem de implementação.

Camadas impactadas: Application (consome IAnimalPregnancyRepository/IAnimalCalvingRepository).

D8 — Parâmetros de referência por propriedade

Decisão: os valores que alimentam metas, faixas e elegibilidade ficam configuráveis por propriedade, seguindo o padrão já usado para PostpartumDaysThreshold (#5 D12). Abrange: faixas de DEL (D9), PEV (período de espera voluntário para elegibilidade à IA) e metas (IEP alvo).

Justificativa: os decks afirmam que esses valores "variam conforme a propriedade". O projeto já trata PostpartumDaysThreshold como config por propriedade — estender é coerente.

Divergência registrada: hoje nem tudo é configurável: a gestação de 280 dias é constante no serviço (#6.1 D2) e o pós-parto de 60 dias vive no resolver (#5 D12). Antes de implementar, decidir quais migram para a tabela de parâmetros e quais seguem como constante. Ver §9/Q2.

Camadas impactadas: Domain/Infrastructure (tabela de parâmetros por propriedade + seed), Application (leitura dos limiares).

D9 — Faixas de DEL canônicas

Decisão: faixa canônica recém-parida 0–21 · início 22–60 · pico 45–90 · meio 100–200 · final >200 DEL.

Justificativa / divergência registrada: os decks (REF-1) trazem duas faixas conflitantes (a acima vs. 0–30 / 45–90 / 100–200 / 200–305 / >305) e dois picos (M 45–70, P 50–90). Escolhida uma canônica para não haver limiar inconsistente; fica editável por propriedade (D8).

Escopo: o histograma de distribuição por faixa de DEL que usaria estas faixas está fora de escopo por ora (§2.2, Q7); elas ficam registradas para quando ele voltar e para rotular o DEL individual no detalhe do animal. Note que a faixa canônica se sobrepõe (45–60), então precisa de cortes não sobrepostos antes de virar histograma.

Camadas impactadas: Application (banda de DEL), Mobile (cores/faixas).

D10 — Estoque: reusa o StockForecastResolver do #17 (mesmo padrão client-side dos demais)

Decisão: o dashboard de estoque deriva seus números reusando o helper StockForecastResolver do #17 sobre o ledger — o mesmo padrão dos dashboards produtivo e reprodutivo (resolver puro rodando sobre os fatos). Os endpoints GET /api/stock/dashboard e GET /api/stock/alerts do #17 (com os read-models StockDashboardDto/StockAlertDto: saldo, cobertura, ruptura, severidade) seguem como a via server-side / de carga, não como a única forma de computar. Previsão/consumo vêm do fluxo real do ledger; consumo por categoria-animal via dieta é opcional futuro (#17 §9).

Justificativa: manter o estoque no mesmo padrão dos outros dois dashboards (resolver sobre fatos, e não "chamar um endpoint pronto") elimina um caminho de código divergente e deixa o offline (§8) uniforme e aditivo — o mesmo resolver roda sobre o Room quando não há rede, igual aos demais. Continua sem redefinir o #17: reusa o helper dele, não recria a lógica.

Alternativas descartadas:

Dashboard de estoque apenas consome o endpoint do #17: descartado — deixaria o estoque como a única peça sem caminho de cálculo local, criando um fluxo online/offline diferente dos outros dashboards e dívida para o §8.

Camadas impactadas: Application (reusa StockForecastResolver do #17; nenhuma lógica de estoque nova), Mobile (mesma via de resolver dos demais dashboards; endpoints do #17 como carga/sync).

D11 — Dashboard sanitário como escopo opcional adicional

Decisão: o dashboard sanitário é opcional adicional, fora do escopo mínimo, sobre #14 (Vacinação) e #16 (Tratamentos/Mastite).

Justificativa: não estava entre os três dashboards-alvo, mas os eixos já proveem os fatos, e o próprio #16 §9 já pré-lista as agregações do dashboard sanitário. Especificá-lo como opcional permite priorizá-lo depois sem retrabalho.

Camadas impactadas: Application (reusa os helpers AnimalSanitaryStatusResolver do #16 e VaccinationEventStatusResolver do #14; agrega sobre HealthCase/MastitisTest/VaccinationEvent), Mobile.

D12 — Escopo por PropertyId em toda leitura; read-only

Decisão: todo índice e endpoint de dashboard é escopado por PropertyId (query filter global); dashboards não escrevem em nenhum eixo.

Justificativa: isolamento de tenant do banco compartilhado; ortogonalidade.

Camadas impactadas: Api/Infrastructure (query filter aplicado aos caminhos de agregação).

D13 — Filtro de período compartilhado por aba; zona "Agora" não obedece ao período

Decisão: cada aba tem um único seletor de período, compartilhado por toda a tela: alterá-lo recomputa juntos todos os cards da zona "No período". Os cards de estado atual (zona "Agora": saldo de estoque, distribuição por status, leite retido/carência, DEL individual, elegíveis para IA) são "hoje" — não se movem com o período; mudam por relógio (virada do dia) ou por dado novo.

Justificativa: um filtro por card seria confuso e caro; um por aba dá previsibilidade (US-05: "a janela vale para todas as taxas da tela"). Separar visualmente as duas zonas evita a leitura errada de que "o saldo/DEL não atualizou" ao trocar o período. A zona "Agora" reúne exatamente os índices clock-sensitive — os que precisam recomputar ao abrir o app / na virada do dia, mesmo sem dado novo (ver §8: cache no cliente deve invalidar por data, não só por dado).

Alternativas descartadas:

Um filtro por card: descartado — ruído de UI e custo sem ganho.

Aplicar o período também aos cards de estado: incoerente — "saldo de estoque no mês passado" não é o que o produtor quer ver na ordenha de hoje.

Camadas impactadas: Mobile (um PeriodSelector no DashboardScaffold por aba; layout em duas zonas rotuladas; recompute local por relógio/dado na zona "Agora").

D14 — PEV como constante no resolver no v1; configurável por propriedade é melhoria futura

Decisão: o período de espera voluntário (PEV) que alimenta a "elegibilidade para IA" (elegivelIA = hoje ≥ dataUltimoParto + PEV) entra no v1 como constante no ReproductiveDashboardResolver, ao lado de PostpartumDaysThreshold (#5 D12) — mesmo padrão de constante que a casa já usa. A elegibilidade para IA fica liberada no v1 (não depende mais de campo novo).

Justificativa: o índice é útil já; travá-lo atrás de um campo/tabela configurável adiaria a entrega sem ganho real neste momento. O valor único por rebanho é o número honesto até haver demanda de configuração.

Implementação futura (mantida): migrar o PEV para a tabela de parâmetros de referência por propriedade (D8) quando a configuração por propriedade for necessária — troca a constante pela leitura da config, sem mexer no resolver. Resolve Q5 para o v1.

Camadas impactadas: Application (constante no ReproductiveDashboardResolver).

D15 — Reprodutivo avançado (CU-03) implementável no v1

Decisão: os índices reprodutivos avançados — serviços por concepção, dias em aberto (média do rebanho), intervalo entre partos (IEP médio), perda gestacional e % prenhez ao 1º serviço — entram no v1. Os repositórios de #6.1 (AnimalPregnancy) e #6.2 (AnimalCalving) já existem; ServiceNumber (#5), AnimalPregnancyStatus.LostPregnancy e AnimalCalving.CalvingDate já estão no modelo. Confirma o D7 (era dependência de spec aprovada, não bloqueio) — deixa de estar em aberto.

Justificativa: as fontes existem e os índices são agregados de rebanho no período, derivados na leitura como os demais. O IEP/dias-em-aberto por animal já vive na ficha (§6.2.1); aqui é a versão agregada.

Camadas impactadas: Application (ReproductiveDashboardResolver; DashboardService), Infrastructure (métodos batch em IBreedingEventRepository/IAnimalCalvingRepository/IAnimalPregnancyRepository), Api (GET /api/dashboard/reproductive/advanced).

D16 — Parâmetros de referência como constantes no v1; sem tabela e sem migração

Decisão: no v1 os parâmetros de referência (PEV, faixas de DEL, metas de IEP) ficam como constantes no resolver. Não se cria a tabela de parâmetros por propriedade (D8) nem migração associada.

Justificativa: evita infra de config antes de haver demanda; alinha ao estado atual (280 dias de gestação e 60 de pós-parto já vivem como constante no código — Div-4).

Implementação futura (mantida): a tabela por propriedade do D8 (global-por-tenant com seed dos defaults dos decks) permanece como evolução; quando entrar, os resolvers passam a ler os limiares da config em vez das constantes. Resolve Q2/Div-4 para o v1.

Camadas impactadas: Application (constantes nos resolvers).

D17 — Padrão de leitura set-based obrigatório (anti-N+1) e endpoints por aba autossuficientes

Decisão: todo índice de rebanho resolve em nº fixo de queries via métodos batch de repositório (recebem uma janela from/to ou IReadOnlyCollection<int> e devolvem dicionário/agregado); nenhum método por-animal é chamado em loop. Os resolvers (ProductiveDashboardResolver, ReproductiveDashboardResolver) são puros e recebem today como parâmetro. Cada endpoint por aba é autossuficiente: devolve a zona "Agora" e a zona "No período" numa única resposta, e embute as listas de drill-down (elegíveis para IA, leite retido) inline reusando AnimalListItemDto — sem requisições de follow-up.

Justificativa: os repositórios hoje são majoritariamente por-animal (GetLastActiveByAnimalIdAsync, GetActiveConfirmedByAnimalIdAsync, GetLastActiveAwaitingDiagnosisDateAsync); usá-los em loop seria N+1. O template já existe: GetReproductiveStatusMapAsync colapsa o rebanho numa query e resolve em memória. Autossuficiência por aba minimiza round trips (internet ruim) e mantém a fórmula pura sobre fatos brutos — a mesma que o cliente recomputa no offline (§8), com a zona "Agora" recomputada localmente na virada do dia (D13).

Nota de reuso: GetReproductiveStatusMapAsync passa a derivar de um irmão GetReproductiveFactsMapAsync (devolve os fatos brutos HasConfirmedPregnancy/LastCalvingDate/LastAwaitingBreedingDate), para que status + elegibilidade para IA saiam da mesma query sem custo extra.

Camadas impactadas: Infrastructure (métodos batch), Application (resolvers puros + DashboardService), Api (endpoints por aba).

4. Histórias de Usuário

US-01 — Ver o dashboard produtivo da propriedade

Como produtor, quero ver a produção do rebanho e a média por vaca em lactação num período, para acompanhar a evolução da fazenda.

Critérios: produção total (dia/mês/ano) e média por vaca em lactação (rótulo explícito); distribuição do rebanho por fase (em lactação/seca); período default = mês corrente, com presets (Hoje/3d/7d/Mês) e intervalo personalizado (D4).

US-02 — Ver o dashboard reprodutivo

Como produtor, quero ver taxa de concepção, status do rebanho e as vacas elegíveis para IA, para avaliar o desempenho reprodutivo.

Critérios: taxa de concepção com bucket "aguardando diagnóstico" à parte; distribuição por status combinado (reprodutivo × produtivo); lista de elegíveis para IA (pós-PEV); estimativas de diagnóstico e de parto.

US-03 — Ver o dashboard de estoque

Como produtor, quero ver gasto, consumo e saldo em R$ e os insumos no ponto crítico, para planejar compras.

Critérios: números via StockForecastResolver do #17 (endpoints /api/stock/dashboard e /api/stock/alerts como via de carga/sync).

US-04 — Detalhar um animal (drill-down)

Como produtor, quero abrir um animal e ver seus índices, para entender o caso individual.

Critérios: DEL (dias em lactação); data do último parto e intervalo entre partos (IEP); previsão do próximo parto com o id da gestação que a origina; data de liberação do leite (fim da carência); genealogia imediata (pai/mãe). Sem timeline (a princípio). A ficha abre tanto pela lista de animais quanto pelas listas do dashboard (D3).

Nota: todos esses índices são derivados na leitura sobre fatos já existentes (#6.1 gestação, #6.2 parto, #11 lactação, #16 carência, #5/#10 filiação) e vêm embutidos no próprio AnimalDto (§6.2.1). A genealogia adota a Alternativa A da Spec #10 (derivação na leitura, sem migração), limitada aos pais imediatos; o pedigree recursivo continua reservado ao endpoint próprio do #10.

US-05 — Escolher o período

Como produtor, quero navegar por períodos, para comparar meses.

Critérios: default mês corrente; navegação retroativa; a janela vale para todas as taxas da tela.

5. Casos de Uso / Endpoints (comportamento)

#

Endpoint

Comportamento

CU-01

GET /api/dashboard/productive?dateFrom=&dateTo=

Produção total, média por vaca em lactação, distribuição por fase. Set-based; sem datas → mês corrente.

CU-02

GET /api/dashboard/reproductive?dateFrom=&dateTo=

Taxa de concepção (+ bucket pendente), status combinado, elegíveis para IA, estimativas. Set-based.

CU-03

GET /api/dashboard/reproductive/advanced?…

Serviços/concepção, dias em aberto, IEP, perda gestacional, % 1º serviço — quando #6.1/#6.2 implementados (D7).

CU-04

GET /api/animals/{id}

A ficha do animal, aberta ao tocar num animal na lista comum ou nas listas do dashboard. Todos os índices são derivados na leitura e embutidos no próprio AnimalDto (§6.2.1), sem endpoint por animal e sem estado persistido: DEL (#11) — já existente; data do último parto e IEP (#6.2); previsão do próximo parto + id da gestação (#6.1); data de liberação do leite (MilkWithheldUntil, #16) — já existente; genealogia imediata pai/mãe (#10 Alternativa A). Sem timeline (D3).

CU-05

GET /api/stock/dashboard, GET /api/stock/alerts

Via de carga server-side do #17; os números vêm do StockForecastResolver (#17), reusado no cliente quando offline (D10).

CU-06

GET /api/dashboard/sanitary?… (opcional)

Distribuição sanitária, mastite por período/quarto, vacinação, carência ativa — sobre #14/#16 (D11).

Todos escopados por PropertyId (D12). Filtro/agregação no repositório (server-side).

6. Especificação Técnica

6.1 Índices produtivos (fonte: #11)

Convenção: vaca em lactação nos dias [StartDate, EndDate).

producaoRebanho(período)  = Σ MilkProduction.Liters WHERE Date ∈ [ini, fim]

mediaPorVacaEmLactacao    = totalLitros(ini,fim) / cowLactationDays(ini,fim)
cowLactationDays          = Σ ( min(EndDate ?? fim+1, fim+1) − max(StartDate, ini) )
                            sobre cada Lactation que sobrepõe [ini, fim]

DEL(animal)               = referenceDate(hoje) − Lactation.StartDate   (congela em EndDate se fechada)

proporcaoEmLactacao(dia)  = COUNT(Lactation aberta no dia) / totalVacasAdultas(dia)

Analogia (DEL / curva). A lactação é como o fôlego de um corredor: sobe rápido, atinge o pico (~45–90 DEL) e cai devagar. O valor por vaca aqui é a média do rebanho em lactação, não a vazão de cada animal — sem medidor individual, é o número honesto. Curva/pico/persistência individuais entram só com MilkYield (#11 D7).

6.2 Índices reprodutivos (fontes: #5, #6.1, #6.2)

statusCombinado           = ReproductiveStatus × statusProdutivo
                            ex.: Pregnant × EmLactação → "Prenha em lactação"

taxaConcepcao(%)          = coberturas(Successful) / coberturas(Successful+Unsuccessful) × 100
bucketPendente            = coberturas(AwaitingDiagnosis)                        (fora do denominador — D5)

prenhezAo1ºServico(%)     = gestações confirmadas com ServiceNumber = 1 / total de 1ºs serviços × 100   (#5 D8)
elegivelIA(animal)        = hoje ≥ dataUltimoParto + PEV(propriedade)            (D8)
previsaoParto             = BreedingDate + 280 (#6.1 D2)  [± janela de exibição]

servicosPorConcepcao      = Σ coberturas / Σ gestações confirmadas               (dep. #6.1)
diasEmAberto              = concepção(BreedingDate do Successful) − parto anterior(AnimalCalving.CalvingDate #6.2)
intervaloEntrePartos      = CalvingDate(n) − CalvingDate(n−1)                     (dep. #6.2)
perdaGestacional          = AnimalPregnancy WHERE Status = LostPregnancy (por período/vaca/lote; #6.1)

Analogia (dias em aberto). É o tempo ocioso da linha de produção entre um "lote" e o próximo: da parição à nova concepção. Quanto maior, maior o custo — por isso vale tanto quanto a taxa de concepção.

O status reprodutivo (Open/AwaitingConfirmation/Pregnant/Postpartum) e a resolução em bloco já existem em ReproductiveStatusResolver (#5); o dashboard os reaproveita e agrega, não recria.

6.2.1 Índices da ficha do animal (nível animal — refinamento técnico, v0.2)

Escopo desta subseção. Materializa os índices por animal do D3/US-04/CU-04 — os que vivem na ficha (GET /api/animals/{id}), não no dashboard de rebanho. Todos são derivados na leitura e embutidos no AnimalDto (nenhum endpoint por animal, nenhum estado persistido — RN-01/D2). Alinha ao ambiente instável: embutir na própria ficha evita requisições extras (uma leitura resolve a ficha inteira) e, como são funções puras sobre fatos, os mesmos resolvers rodam sobre o cache local no offline-first (§8).

Campos adicionados ao AnimalDto (além de DEL/ProductiveStatus/ReproductiveStatus/MilkWithheldUntil já existentes):

| Campo | Tipo | Fonte / derivação | Nulo quando |
|-------|------|-------------------|-------------|
| lastCalvingDate | DateTime? | CalvingDate do parto ativo mais recente (#6.2) | animal sem parto |
| calvingIntervalDays | int? | CalvingDate(n) − CalvingDate(n−1) dos dois últimos partos ativos (IEP; #6.2) | menos de 2 partos |
| nextCalving | { pregnancyId, expectedCalvingDate } ? | gestação ativa e confirmada: Id + ExpectedCalvingDate (#6.1) | sem gestação confirmada em curso |
| parentage | { mother, father } ? | genealogia imediata derivada da cadeia cria→parto→gestação→cobertura (#10 Alt. A) | animal sem cadeia de parto (ex.: adquirido) |

Previsão do próximo parto (nextCalving). Retorna o par pregnancyId + expectedCalvingDate da gestação ativa e confirmada do animal. O id é intencional: permite abrir a gestação a partir da ficha (drill-down repro), sem uma segunda requisição para descobri-la. A data reusa AnimalPregnancy.ExpectedCalvingDate já persistida (não recalcula BreedingDate + 280 aqui — a gestação é a fonte de verdade; #6.1 D2). Objeto único (não dois campos soltos) para acoplar data e id e dar um só nulo quando não há gestação.

Genealogia (parentage) — Alternativa A da Spec #10. Pais imediatos derivados na leitura, sem coluna nova nem migração:
- mother = AnimalCalving.Animal (vaca/novilha do rebanho) → { id, name, tagNumber }.
- father = união: touro do rebanho (Bull) ou sêmen (Semen). Resolve sire/semen preferindo o nível da gestação (SireAnimalId/SemenSampleId, cadastro retroativo #13) e caindo para o da cobertura (BreedingEvent.SireAnimalId/SemenSampleId, fluxo normal). Bull → { id, name, tagNumber }; Semen → { semenSampleId, name, bullRegistration, bullBreed, geneticsCompany }.
- A leitura da cadeia não filtra por IsActive (preserva histórico mesmo com cobertura/gestação inativada — #10 §2); o único filtro é o de tenant (PropertyId). Só cobre nascidos na propriedade; adquirido → parentage null. Profundidade = 1 (pais); pedigree recursivo (avós+) fica no endpoint dedicado do #10.

Resolvers / métodos (padrão puro e testável — D2):
- ReproductiveStatusResolver.CalvingIntervalDays(lastCalvingDate, previousCalvingDate) — puro; null se faltar um dos dois.
- GenealogyResolver.Resolve(AnimalCalvingCalf birth) → AnimalParentageDto? — puro; mapeia a cadeia carregada para o DTO.
- IAnimalCalvingRepository.GetRecentActiveByAnimalIdAsync(animalId, count) — os N partos ativos mais recentes (usa 2: último parto + IEP; reaproveitado para o status reprodutivo).
- IAnimalCalvingRepository.GetParentageByAnimalIdAsync(animalId) — a cria do animal com parto → mãe e gestação → cobertura/sire/sêmen, em uma query.
- IAnimalPregnancyRepository.GetActiveConfirmedByAnimalIdAsync(animalId) — a gestação confirmada em curso (nextCalving; reaproveitada para o status reprodutivo em vez do HasActive… booleano).

Orçamento de queries (performance). A derivação reprodutiva reaproveita as buscas: GetRecentActive… substitui o "último parto" e GetActiveConfirmed… substitui o booleano HasActiveConfirmed…, ambos já necessários ao status reprodutivo — logo lastCalvingDate/IEP/nextCalving saem sem query extra. A genealogia acrescenta exatamente uma query (a cadeia da cria). Índices reprodutivos gateados a Cow/Heifer; genealogia vale para qualquer animal nascido na propriedade.

6.3 Estoque (fonte: #17 — reusado, não redefinido)

Deriva os números reusando o StockForecastResolver do #17 sobre o ledger (mesmo padrão dos demais dashboards): saldo, cobertura, ruptura e severidade. Os read-models StockDashboardDto (PeriodSpent, PeriodConsumedValue, StockValue, Items) e StockAlertDto (ponto crítico) e os endpoints do #17 são a via server-side / de carga. Sem redefinição.

Analogia (previsão de término). "Combustível ÷ consumo médio = km até o posto": saldo ÷ ritmo do ledger = dias até acabar. O ponto crítico é a luz da reserva — gatilho primário do alerta (#17 D12).

6.4 Sanitário (opcional — fontes: #14 Vacinação, #16 Tratamentos/Mastite)

Somente leitura, agregando os fatos dos eixos sanitários (ortogonal — não escreve neles). As agregações abaixo já estão pré-listadas em #16 §9 como "especificáveis à parte sobre as tabelas desta spec" — é o que esta seção materializa.

Status sanitário do rebanho — distribuição por SanitaryStatus (Healthy / UnderObservation / UnderTreatment / InWithdrawal) via AnimalSanitaryStatusResolver (#16), já resolvido em bloco na listagem de animais.

Leite retido agora — animais com MilkWithheldUntil no futuro (carência ativa), via milkWithheldOnly (#16 D14). MilkWithheldUntil = MAX(ApplicationDate + WithdrawalPeriodDays), boundary inclusivo (libera em hoje >= data).

Mastite — casos HealthCase com DiseaseType = Mastitis por período; por quarto afetado (AffectedQuarters, flags FrontLeft/FrontRight/RearLeft/RearRight); testes por MastitisTestType (fundo preto / CMT / CCS / microbiológico) e Result.

Vacinação — distribuição por status derivado VaccinationEventStatus (Scheduled / Overdue / Applied) via VaccinationEventStatusResolver (#14); próximos reforços = PredictedDate dos eventos Scheduled/Overdue.

Métricas do painel (de #16 §9) — nº de animais em carência agora; casos de mastite por mês; quartos mais afetados; medicamentos mais usados; duração média de tratamento; recorrência por animal.

Limitação registrada. A distinção clínica × subclínica dos decks (REF-1) não é um campo no #16 — o modelo só tem DiseaseType { Mastitis, Other }. Derivá-la exigiria um campo novo ou inferência a partir de CMT/CCS/sintomas; fica como Q6, não como índice pronto.

Futuro (cross-axis). Leite descartado por carência = JOIN de leitura com o eixo produtivo (MilkProduction/Lactation), já previsto em #16 Q4 — fora do v1.

6.5 Resolvers e endpoints

Camada

Item

Ação

Application/Helpers

ProductiveDashboardResolver

Criar — funções puras sobre MilkProduction/Lactation

Application/Helpers

ReproductiveDashboardResolver

Criar — reusa ReproductiveStatusResolver; agrega taxas

Application/Helpers

StockForecastResolver

Reusar do #17

Application/Services

DashboardService

Criar — reúne fatos set-based e projeta os DTOs

Api/Controllers

DashboardController

Criar — endpoints do §5

6.6 Regras de Negócio

#

Regra

Onde

RN-01

Nenhum índice persistido; tudo derivado na leitura.

Resolver/Service

RN-02

"Data atual" dos índices por tempo = relógio do servidor (hoje).

Resolver

RN-03

Taxa de concepção exclui AwaitingDiagnosis do denominador; bucket à parte.

ReproductiveDashboardResolver (D5)

RN-04

Elegibilidade IA = último parto + PEV da propriedade.

Resolver (D8)

RN-05

DEL = hoje − Lactation.StartDate, congelado em EndDate.

Resolver (#11 D5)

RN-06

"Por vaca" = média por vaca em lactação (total ÷ cowLactationDays), rótulo explícito.

Service/Mobile (D6)

RN-07

Estoque: reusa o StockForecastResolver do #17 sobre o ledger; não recria a lógica. Endpoints do #17 = via de carga/sync.

Application/Mobile (D10)

RN-08

Limiares (faixas DEL, PEV, metas) lidos da config por propriedade.

Resolver (D8)

RN-09

Toda leitura/agregação escopada por PropertyId.

Repository (D12)

6.7 Camadas impactadas

Camada

Ação

Application/Helpers

ProductiveDashboardResolver, ReproductiveDashboardResolver (criar); StockForecastResolver/ReproductiveStatusResolver (reusar)

Application/Services

DashboardService (criar)

Application/DTOs

ProductiveDashboardDto, ReproductiveDashboardDto, itens de drill-down (criar)

Infrastructure/Repositories

métodos agregados (produção por período; coberturas/gestações/partos por período)

Infrastructure/Data

tabela de parâmetros por propriedade + seed (D8) — requer aprovação

Api/Controllers

DashboardController (criar)

Mobile (Compose)

telas do §6.8

6.8 Frontend / Mobile (Jetpack Compose)

Composable

Responsabilidade

DashboardScaffold

Abas Produtivo · Reprodutivo · Estoque · [Sanitário]; header com PeriodSelector.

PeriodSelector

Janela livre por intervalo; presets Hoje/3d/7d/Mês + personalizado; default mês corrente (D4).

IndicatorCard

KPI + unidade + banda/cor + subtítulo; estados "não estimável" e "indisponível" próprios.

StatusDistributionChart

Distribuição por status combinado repro × produtivo.

PendingDiagnosisBucket

Coberturas AwaitingDiagnosis, fora da taxa de concepção (D5).

StockDashboardView

Números via StockForecastResolver (#17); endpoints /api/stock/dashboard + /api/stock/alerts como carga/sync.

AnimalDetail (ficha)

Campos por animal (todos no AnimalDto, §6.2.1): DEL, data do último parto, IEP, previsão do próximo parto (com id da gestação → abre a gestação), data de liberação do leite, genealogia imediata (pai/mãe). Reusa GET /api/animals/{id}. Aberta ao tocar num animal na lista comum ou nas listas do dashboard. Sem timeline (a princípio).

Exibição. Índice ainda não implementado (dep. #6.2) aparece como "em breve", não com valor falso. O rótulo produtivo por vaca é sempre "média por vaca em lactação". Estoque reaproveita os cards do #17.

7. Notas de Migração

Requer aprovação explícita antes de executar.

Os índices são derivados — na maioria, nenhuma tabela nova. A única estrutura candidata é a tabela de parâmetros de referência por propriedade (D8: faixas de DEL, PEV, metas), global-por-tenant com seed dos defaults dos decks. Só criar após decidir Q2 (o que é config vs. constante). Índices de apoio às agregações por período (produção, coberturas/gestações/partos) devem ser avaliados caso o volume exija.

8. Como habilitar offline-first depois (sem retrabalho)

Mesmo racional de #17 §8. Como os índices são funções puras sobre fatos (D2), a via é aditiva:

Os resolvers (ProductiveDashboardResolver, ReproductiveDashboardResolver, StockForecastResolver) rodam idênticos sobre o cache local (Room) e sobre o banco — "barracão sem sinal: o dashboard mostra os números do último dado local".

Sincroniza-se fato bruto (produção, coberturas, movimentos), nunca índice calculado — sem conflito de estado (já é a decisão D9 do #11).

ClientId Guid de idempotência entra por cima do Id int, como no #17 §8; PropertyId segue como tenant.

O cache no cliente invalida por data, não só por dado: os índices clock-sensitive (DEL, carência, elegibilidade IA, cobertura) recomputam na virada do dia / ao abrir o app (D13).

Snapshots pré-agregados de janelas pesadas (ex.: L/ano) podem ser adicionados como cache versionado quando necessário.

9. Divergências e Questões Futuras

Divergências registradas (nenhuma resolvida silenciosamente):

#

Divergência

Decisão

Div-1

Faixas de DEL conflitantes entre dois slides dos decks.

Canônica 0-21/22-60/45-90/100-200/>200 (D9), editável por propriedade.

Div-2

Decks derivam consumo da dieta; #17 é autossuficiente (ledger, consumo manual).

Previsão/consumo pelo ledger real (D10); dieta é opcional futuro.

Div-3

Arquitetura: local-first (framing antigo) vs. server-side (specs #14/#16/#17).

Server-side agora, offline aditivo (D1/§8).

Div-4

Parâmetros: 280 (gestação, #6.1 D2) e 60 (pós-parto, #5 D12) hoje no código, não em config.

Decidir o que migra para a tabela por propriedade (D8/Q2).

Questões futuras / dependências:

Q1 — Retenção local (só relevante quando o offline (§8) entrar): janela do cache no device (proposta ≥ 13 meses para cobrir IEP e L/ano).

Q2 — Config vs. constante dos parâmetros de referência (D8/Div-4). Resolvida para o v1: constantes no resolver (D16); tabela por propriedade fica como melhoria futura.

Q3 — MilkYield / pesagem individual: destrava curva, pico, persistência e produção por lactação (fora de escopo hoje, #11 D7).

Q4 — Eixo de dieta: destrava consumo/previsão por categoria-animal no estoque (#17 §9).

Q5 — PEV: hoje não há campo de período de espera voluntário. Resolvida para o v1: PEV como constante no resolver (D14), elegibilidade para IA liberada; campo configurável por propriedade (D8) fica como melhoria futura.

Q6 — Dashboard sanitário (D11): §6.4 já detalhada sobre #14/#16 (lidos na íntegra). Restam: confirmar prioridade de implementação e decidir se a distinção clínica × subclínica vira campo no #16 ou fica inferida (hoje não é modelada).

Q7 — Faixas de DEL não sobrepostas: antes de reintroduzir o histograma de distribuição por faixa de DEL (§2.2), definir cortes mutuamente exclusivos — as faixas canônicas do D9 se sobrepõem em 45–60.

Versão 0.3 — decisões de implementação dos dashboards de rebanho fechadas (D14–D17), para histórico, sem descartar a implementação futura. PEV como constante no resolver, elegibilidade para IA liberada no v1 (D14, resolve Q5); melhoria futura = PEV configurável por propriedade (D8). Reprodutivo avançado (CU-03) implementável no v1 pois #6.1/#6.2 já existem (D15, confirma D7). Parâmetros de referência como constantes, sem tabela e sem migração no v1 (D16, resolve Q2/Div-4); tabela por propriedade (D8) mantida como evolução. Padrão de leitura set-based/batch obrigatório contra N+1, resolvers puros recebendo today, endpoints por aba autossuficientes (Agora + No período numa resposta, listas de drill-down embutidas), mantendo a via aditiva do offline-first §8 (D17). Nenhuma decisão-base (D1–D13) alterada; nada implementado ainda.

Versão 0.2 — refinado e implementado o nível animal (§6.2.1): índices da ficha embutidos no AnimalDto — lastCalvingDate, calvingIntervalDays (IEP), nextCalving { pregnancyId, expectedCalvingDate } e parentage (genealogia imediata pai/mãe, Alternativa A da Spec #10, derivada na leitura sem migração), somados aos já existentes DEL e MilkWithheldUntil. Derivação reprodutiva reaproveita queries (sem custo extra além de uma query de genealogia); tudo derivado na leitura, apto ao offline-first (§8). Dashboards de rebanho (produtivo/reprodutivo/estoque) seguem conforme v0.1 abaixo.

Versão 0.1 — decisões-base fechadas (D1–D13); alinhada às specs #5, #6.1, #6.2, #11, #14, #16 e #17. Estoque reusa o resolver do #17 (D10); filtro de período livre por intervalo com presets (D4); um filtro compartilhado por aba, zona "Agora" fora do período (D13); nível animal = ficha existente (DEL, data do parto, genealogia, IEP, data de liberação do leite; sem timeline a princípio), acessível pela lista de animais e pelo dashboard, sem agregados por animal (D3); distribuição por faixa de DEL fora de escopo por ora (§2.2/Q7). Índices produtivos e a taxa de concepção são implementáveis já; reprodutivos avançados dependem de #6.1/#6.2. Pronta para revisão.