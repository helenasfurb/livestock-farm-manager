# Planejamento de Dashboard - MuuBoi MVP

## Objetivo

O dashboard do MuuBoi tem como objetivo fornecer uma visão geral do rebanho, permitindo que o produtor acompanhe indicadores relacionados à composição dos animais, sanidade, reprodução e evolução do peso.

O foco do MVP é apresentar informações relevantes utilizando apenas os dados já cadastrados no sistema.

---

# Estrutura Geral

## Filtros Globais

### Raça

Filtro disponível para a maior parte dos indicadores e gráficos.

Exemplos:

* Todas as raças
* Nelore
* Angus
* Hereford

### Sexo (Opcional para evolução futura)

* Todos
* Machos
* Fêmeas

---

# Indicadores Principais (Cards)

## Total de Animais

**Objetivo**

Exibir a quantidade total de animais cadastrados.

**Pergunta respondida**

> Quantos animais existem no rebanho?

---

## Animais Gestantes

**Objetivo**

Exibir a quantidade de animais com gestação ativa.

**Fonte**

```csharp
IsPregnant == true
```

**Pergunta respondida**

> Quantas matrizes estão prenhas atualmente?

---

## Vacinas Pendentes

**Objetivo**

Exibir a quantidade de aplicações em atraso.

**Fonte**

```csharp
NextApplicationDate <= DateTime.Today
```

**Pergunta respondida**

> Quantas vacinas precisam ser aplicadas?

---

## Tratamentos Ativos

**Objetivo**

Exibir a quantidade de animais em tratamento.

**Fonte**

```csharp
EndDate == null || EndDate >= DateTime.Today
```

**Pergunta respondida**

> Quantos animais estão recebendo medicação atualmente?

---

# Gráficos

## 1. Distribuição por Sexo

### Tipo

Gráfico de Pizza

### Filtro

* Raça

### Dados

* Machos
* Fêmeas

### Objetivo

Permitir visualizar rapidamente a composição do rebanho por sexo.

### Pergunta respondida

> Qual a distribuição entre machos e fêmeas?

---

## 2. Distribuição por Raça

### Tipo

Gráfico de Barras

### Filtro

* Sexo (opcional)

### Dados

Quantidade de animais por raça.

### Objetivo

Identificar quais raças possuem maior representatividade dentro do rebanho.

### Pergunta respondida

> Qual raça possui maior participação no rebanho?

---

## 4. Vacinas Aplicadas por Mês

### Tipo

Gráfico de Colunas

### Filtros

* Raça
* Sexo

### Dados

Quantidade de vacinas aplicadas por mês.

### Fonte

```csharp
AnimalVaccination.ApplicationDate
```

### Objetivo

Acompanhar a atividade sanitária do rebanho.

### Pergunta respondida

> O calendário vacinal está sendo executado corretamente?

---

## 5. Partos Previstos por Mês

### Tipo

Gráfico de Colunas

### Dados

Quantidade de partos previstos agrupados por mês.

### Fonte

```csharp
ExpectedBirthDate
```

### Objetivo

Auxiliar no planejamento reprodutivo.

### Pergunta respondida

> Em quais meses ocorrerão mais partos?

---
# Escopo do MVP

## Cards

* Total de Animais
* Animais Gestantes
* Vacinas Pendentes
* Tratamentos Ativos

## Gráficos

* Distribuição por Sexo
* Distribuição por Raça
* Evolução do Peso Médio
* Vacinas Aplicadas por Mês
* Partos Previstos por Mês
