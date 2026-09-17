# Spec #6: Gestação e Parto — Índice

**Módulo:** Gestação e Parto  
**Versão:** 1.0  
**Data:** 24/Ago/2026  
**Status:** Aprovado para implementação  
**Depende de:** Spec #5 (Eventos Reprodutivos), Spec #3 (ECC)  
**Referenciado por:** Spec #7 (Dashboards)

---

## Visão Geral

Este módulo implementa o acompanhamento da gestação e do parto a partir da confirmação de prenhez registrada no Spec #5. É composto por três sub-specs, cada um cobrindo uma entidade:

| Sub-spec | Entidade | Arquivo |
|----------|----------|---------|
| **6.1 — Gestação** | `AnimalPregnancy` | [spec-gestacao-parto-6.1-gravidez.md](spec-gestacao-parto-6.1-gravidez.md) |
| **6.2 — Parto** | `AnimalCalving` | [spec-gestacao-parto-6.2-parto.md](spec-gestacao-parto-6.2-parto.md) |
| **6.3 — Cria** | `AnimalCalvingCalf` | [spec-gestacao-parto-6.3-cria.md](spec-gestacao-parto-6.3-cria.md) |

---

## Fluxo entre as entidades

```
BreedingEvent (Spec #5)
  │ Status = Successful
  ▼
AnimalPregnancy          ← Spec 6.1
  │ Status = Confirmed
  │ POST /api/pregnancies/{id}/calvings
  ▼
AnimalCalving            ← Spec 6.2
  │ (criado junto)
  ▼
AnimalCalvingCalf (N)    ← Spec 6.3
```

---

## Completações do Spec #5

Este módulo implementa os itens deixados como pendentes no Spec #5:

| Item pendente (Spec #5) | Implementado em |
|------------------------|----------------|
| D9 — Criação automática de `AnimalPregnancy` ao confirmar prenhez | Spec 6.1, CU-01 |
| CU-05 passo 3 — Bloqueio de inativação de `BreedingEvent` com gestação ativa | Spec 6.1, CU-06 |
| D10 — Estado `Pregnant` do `ReproductiveStatus` | Spec 6.1, seção 5.5 |
| D10 — Estado `Postpartum` do `ReproductiveStatus` | Spec 6.2, seção 5.4 |

---

## Ordem de implementação sugerida

1. **Spec 6.1** — `AnimalPregnancy` (depende só do Spec #5, já implementado)
2. **Spec 6.3** — `AnimalCalvingCalf` (entidade e DTOs, sem dependência de serviço próprio)
3. **Spec 6.2** — `AnimalCalving` (orquestra criação de crias e atualiza gestação)

> As migrations dos três sub-specs devem ser agrupadas em uma única migration ou executadas em sequência respeitando a ordem das FKs: `AnimalPregnancies` → `AnimalCalvings` → `AnimalCalvingCalves`.
