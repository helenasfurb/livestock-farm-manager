# Spec #23: Recuperação de Senha por Código

**Módulo:** Conta / Autenticação (`ApplicationUser`, ASP.NET Identity, e-mail)
**Versão:** 0.1
**Data:** 25/Out/2026
**Status:** 📝 **Rascunho.** Abordagem (código de 6 dígitos no app) escolhida pela usuária em 25/Out/2026; decisões da §4 aguardam aprovação.
**Depende de:** Spec #21 (`spec-exclusao-conta-perfil.md`) — validação de sessão por `SecurityStamp` (`ISessionValidator`) · regras de senha do Identity (padrão, sem customização).
**Relaciona-se com:** Spec #22 (`spec-portal-administrativo.md`) — limitador de requisições (`AddRateLimiter`, `ResolveClientIp`) já implementado na branch `feature/admin-portal` · Spec #14 §5.6 (autenticação fica fora do offline) · `Docs/catalogo-erros-api.md` (§4.1, §6).
**Substitui:** a branch `task/password-recovery` (commit `9c1239a`, 21/Ago/2026), que fica como referência e **não** é mesclada (§3).

> **Decisão de abordagem:** o MuuBoi tem **um único cliente** — o app Android — e nenhum front web. Em vez de um link para uma página de redefinição, o e-mail traz um **código de 6 dígitos** que o usuário digita no próprio app, junto com a senha nova. Funciona mesmo que o e-mail seja lido no computador, e não depende de chaves de criptografia que se perdem quando o container reinicia (§3, item 2).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 25/Out/2026 | Rascunho: código de 6 dígitos por e-mail, duas rotas, invalidação de sessões, limite de tentativas, falha de envio sem `500` |

---

## 1. Contexto e objetivo

Hoje não há como recuperar o acesso: quem esquece a senha depende de alguém mexer no banco. A rota de troca de senha (`PATCH /api/auth/me/password`) exige a senha atual e um usuário logado.

Esta spec entrega:
1. **Pedir o código** informando o e-mail, sem estar logado.
2. **Redefinir a senha** com e-mail + código + senha nova.
3. **Encerrar as sessões abertas** da conta ao redefinir (mesma garantia da troca de senha, Spec #21).
4. **Proteções** contra enumeração de e-mails, disparo em massa e tentativa de adivinhar o código.

## 2. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/auth/forgot-password` | Confirmação de e-mail no cadastro |
| `POST /api/auth/reset-password` | Troca de e-mail da conta |
| Envio do código por SMTP (Gmail) | Envio por SMS ou WhatsApp |
| Limite de tentativas por IP e por conta | Login com Google/Microsoft |
| Configuração em produção (secrets do Container App) | Tradução das mensagens de senha do Identity (§4, R10 — opcional) |
| Telas do app (contrato, §5) | Implementação das telas no app |

## 3. Situação atual

| # | Situação | Tratamento |
|---|---|---|
| 1 | A branch `task/password-recovery` (21/Ago) tem `forgot-password`/`reset-password`, `IEmailService`/`EmailService` (SMTP Gmail) e responde mensagem genérica — mas está **97 commits atrás** da `dev`, e o `AuthController` e o `Program.cs` mudaram muito desde então | Refazer em cima da `dev`, aproveitando o que está certo |
| 2 | O token padrão do Identity (`GeneratePasswordResetTokenAsync`) é protegido por chaves de *Data Protection*. No container, elas ficam só em memória — o log de produção avisa `No XML encryptor configured` — e somem a cada reinício. Com `minReplicas = 0`, um reinício entre o pedido e o uso **invalida o token** | Código TOTP (R1), que não depende dessas chaves |
| 3 | O e-mail da branch aponta para `http://localhost:3000/reset-password`, um front web que não existe | Sem link: o e-mail traz o código (R1) |
| 4 | O texto do e-mail diz "expira em 1 hora", mas o token padrão vale 1 dia | Texto com a validade real do código (R2) |
| 5 | O reset não chama `ISessionValidator.Invalidate` — a validação de sessão usa cache em memória, e quem já estava logado continuaria logado até o cache expirar | R5 |
| 6 | Nenhum limite de tentativas | R6, R7 |
| 7 | Falha no SMTP vira `500` (e, como só acontece para e-mail cadastrado, revela que ele existe) | R8 |
| 8 | Pacote `MailKit` adicionado e não usado (o código usa `System.Net.Mail`); mensagens de validação dos DTOs sem português; `IEmailService` retorna `Task` | Remover o pacote; mensagens em português; `Task<bool>` |
| 9 | Toda a lógica de autenticação está no `AuthController` (precedente) | Lógica nova num service testável (R9) |

## 4. Decisões propostas

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| R1 | **Código de 6 dígitos** gerado pelo próprio Identity: `options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider` no `AddIdentityCore` (⚠️ `Program.cs`). Com isso, `GeneratePasswordResetTokenAsync` passa a devolver um código numérico (TOTP baseado no `SecurityStamp`) e `ResetPasswordAsync` o valida — sem tabela nova nem migração | (a) Token longo por link — depende do front web (§3, item 3) e das chaves de Data Protection (item 2). (b) Código próprio gravado em tabela — exige migração, hash e expiração manuais |
| R2 | **Validade:** a do provedor padrão — janela de passos de 3 minutos com tolerância, **cerca de 9 minutos** (⚠️ confirmar na Fase 1). O e-mail informa "válido por alguns minutos" sem prometer um número exato até a confirmação | Provedor customizado com validade configurável — mais código para pouco ganho |
| R3 | **Uso único na prática:** redefinir a senha troca o `SecurityStamp`, e todos os códigos emitidos antes deixam de valer | — |
| R4 | **`forgot-password` sempre responde `200`** com a mesma mensagem ("Se o e-mail estiver cadastrado, você receberá um código em instantes."), exista ou não a conta, esteja ou não ativa | Responder `404` para e-mail inexistente — permitiria descobrir quem tem conta |
| R5 | **Redefinir encerra as sessões:** depois do `ResetPasswordAsync`, `ISessionValidator.Invalidate(user.Id)` (mesmo comportamento da troca de senha, Spec #21) | — |
| R6 | **Limite por IP** com o `AddRateLimiter` do ASP.NET (o mesmo mecanismo do login do portal, Spec #22): `forgot-password` e `reset-password` com **5 requisições a cada 15 minutos por IP** → `429` "Muitas tentativas. Tente novamente mais tarde." | Sem limite — permitiria disparar e-mails em massa |
| R7 | **Limite por conta** no `reset-password`: depois de **5 códigos errados** para o mesmo usuário, novas tentativas são recusadas por 15 minutos (contador em memória, `IMemoryCache`), com a mesma mensagem genérica do código inválido. Protege contra quem troca de IP para adivinhar o código | (a) Lockout do Identity — também bloquearia o **login** da vítima. (b) Trocar o `SecurityStamp` após 5 erros — derrubaria as sessões da vítima a cada ataque |
| R8 | **Falha no envio do e-mail:** registra no log (`LogError`) e responde o mesmo `200` genérico. O usuário pode pedir de novo | Responder `500` — revela que o e-mail existe e não deixa rastro |
| R9 | **`IPasswordRecoveryService` + `PasswordRecoveryService`** (`Application/`), recebendo `UserManager<ApplicationUser>`, `IEmailService`, `ISessionValidator`, `IMemoryCache` e `ILogger`; o `AuthController` só chama o service | Manter a lógica no controller (como o resto do `AuthController`) — não seria testável |
| R10 | *(Opcional)* **Mensagens de senha em português** com um `IdentityErrorDescriber` próprio (⚠️ `Program.cs`) — atende também cadastro, troca de senha e criação de membro | Deixar em inglês e o app validar antes (lista de regras já levantada) |
| R11 | **Resposta do reset:** `200` com mensagem; o usuário faz login em seguida (não devolve JWT) | Devolver o JWT já logado — mais cômodo, mas mistura duas responsabilidades e esconde o login com a senha nova |

**Risco aceito — diferença de tempo:** o envio por SMTP leva 1–2 segundos e só acontece para e-mails cadastrados. Alguém medindo o tempo de resposta poderia inferir se a conta existe. Evitar isso exige enviar em segundo plano (fila + `BackgroundService`); para o TCC, fica registrado como risco (§8) e melhoria futura.

## 5. Contrato da API

### 5.1 `POST /api/auth/forgot-password`

```json
{ "email": "maria@exemplo.com" }
```

| Situação | Resposta |
|---|---|
| E-mail cadastrado e ativo | `200` `{ "message": "Se o e-mail estiver cadastrado, você receberá um código em instantes." }` e o e-mail é enviado |
| E-mail inexistente ou conta inativa | `200` com a **mesma** mensagem, sem envio |
| Falha no SMTP | `200` com a mesma mensagem; erro no log (R8) |
| E-mail ausente ou inválido | `400` (formato B) "O e-mail é obrigatório." / "O e-mail informado é inválido." |
| Mais de 5 pedidos em 15 min do mesmo IP | `429` "Muitas tentativas. Tente novamente mais tarde." |

**E-mail enviado** (texto simples):

```
Assunto: Código para redefinir sua senha — MuuBoi

Olá, Maria!

Seu código para redefinir a senha do MuuBoi é:

    482915

Ele é válido por alguns minutos. Se você não pediu a redefinição, ignore este e-mail — sua senha continua a mesma.

Equipe MuuBoi
```

### 5.2 `POST /api/auth/reset-password`

```json
{ "email": "maria@exemplo.com", "code": "482915", "newPassword": "NovaSenha1!" }
```

| Situação | Resposta |
|---|---|
| Código válido, senha dentro das regras | `200` `{ "message": "Senha redefinida com sucesso." }`; sessões encerradas (R5) |
| Código errado, expirado, já usado, e-mail inexistente ou conta inativa | `400` `{ "message": "Código inválido ou expirado." }` — **mesma** mensagem em todos os casos |
| 5 códigos errados para a conta nos últimos 15 min | `400` com a mesma mensagem, sem nem testar o código (R7) |
| Senha fora das regras do Identity | `400` `{ "message": "Erro ao redefinir a senha.", "errors": [ ... ] }` (formato D; em inglês, salvo R10) |
| Campos ausentes / código fora de 6 dígitos / senha com menos de 6 caracteres | `400` (formato B) com mensagens em português |
| Mais de 5 tentativas em 15 min do mesmo IP | `429` |

**Ordem de validação no reset:** formato (DTO) → limite por conta (R7) → usuário existe e está ativo → código + senha (`ResetPasswordAsync`). Se o código estiver errado, o erro é "código inválido" mesmo que a senha também esteja fora das regras — o usuário não fica sabendo das regras da senha sem ter o código certo.

## 6. Contrato do app

Fora do offline: as duas rotas exigem conexão, como o login (Spec #14 §5.6).

1. **Tela "Esqueci minha senha"** (a partir do login): campo de e-mail → `forgot-password` → vai para a tela do código mostrando a mensagem genérica.
2. **Tela "Redefinir senha"**: código (6 dígitos, teclado numérico), senha nova e confirmação.
   - Validar a senha **antes** de enviar, com as mesmas regras do servidor: mínimo 6 caracteres, um número, uma minúscula (a–z), uma maiúscula (A–Z) e um caractere especial (espaço e letras acentuadas contam como especial).
   - Botão **"Reenviar código"** com espera de 60 segundos entre envios.
3. `200` → mensagem de sucesso → volta para o login com o e-mail preenchido.
4. `400` "Código inválido ou expirado." → mostrar a mensagem e oferecer "Reenviar código".
5. `429` → mostrar a mensagem e desabilitar o envio por alguns minutos.
6. Depois de redefinir, os outros aparelhos logados nessa conta recebem `401` na próxima requisição e voltam para o login (Spec #21).

## 7. Fases de implementação e validação

Branch proposta: `feature/password-recovery`, a partir da `dev`. Cada fase é proposta com o código exato e só implementada após aprovação.

| Fase | Status |
|---|---|
| 1 — Provedor de código e serviço de e-mail (R1, R2) | ⏳ |
| 2 — `forgot-password` (R4, R8) | ⏳ |
| 3 — `reset-password` e sessões (R3, R5, R7) | ⏳ |
| 4 — Limite por IP (R6) | ⏳ |
| 5 — *(Opcional)* Mensagens de senha em português (R10) | ⏳ |
| 6 — Configuração em produção | ⏳ |
| 7 — Testes | ⏳ |
| 8 — Documentação | ⏳ |

### 7.1 Fase 1 — Provedor de código e serviço de e-mail

- `AddIdentityCore` com `PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider` (⚠️ `Program.cs`).
- `IEmailService.SendPasswordResetCodeAsync(toEmail, toName, code)` → `Task<bool>`; `EmailService` em `Infrastructure/Services` (SMTP com `System.Net.Mail`, seção `Email` da configuração). Sem `MailKit`.
- Registro do `IEmailService` (⚠️ `Program.cs`).

| Validação planejada | Esperado |
|---|---|
| `GeneratePasswordResetTokenAsync` | Código de 6 dígitos |
| Validade real do código | Medir: código aceito até quantos minutos depois de gerado (confirma R2) |
| Envio pelo SMTP com as credenciais do user-secrets | E-mail chega com o código |

### 7.2 Fase 2 — `forgot-password`

| Validação planejada | Esperado |
|---|---|
| E-mail cadastrado | `200` + e-mail recebido |
| Inexistente / conta inativa | `200`, mesma mensagem, nada enviado |
| SMTP com senha errada | `200`, mesma mensagem, `fail:` no log |
| E-mail inválido | `400` em português |

### 7.3 Fase 3 — `reset-password` e sessões

| Validação planejada | Esperado |
|---|---|
| Código certo + senha válida | `200`; login com a senha nova funciona; com a antiga, não |
| Token antigo de outro aparelho | `401` na requisição seguinte |
| Mesmo código de novo | `400` "Código inválido ou expirado." |
| Código errado / e-mail inexistente / conta inativa | `400`, mesma mensagem |
| Código certo + senha fraca | `400` com a lista de regras |
| 6ª tentativa errada na mesma conta | `400` sem testar o código, inclusive com o código certo; liberado após 15 min |

### 7.4 Fase 4 — Limite por IP

- `AddRateLimiter` com a política das duas rotas (⚠️ `Program.cs`). Se a `feature/admin-portal` já estiver na `dev`, reaproveitar o `AddRateLimiter` e o `ResolveClientIp` de lá; senão, criar aqui e resolver o conflito no merge do portal.

| Validação planejada | Esperado |
|---|---|
| 6ª requisição em 15 min do mesmo IP | `429` |
| Login e demais rotas | Sem limite novo |

### 7.5 Fase 6 — Configuração em produção

No Container App, variáveis de ambiente (as sensíveis como *secret*):

| Variável | Valor |
|---|---|
| `Email__SmtpHost` | `smtp.gmail.com` |
| `Email__SmtpPort` | `587` |
| `Email__FromEmail` / `Email__Username` | conta Gmail do MuuBoi |
| `Email__Password` | **senha de app** do Gmail (secret) — exige verificação em duas etapas ativa na conta; não é a senha normal |
| `Email__FromName` | `MuuBoi` |

### 7.6 Fase 7 — Testes

`PasswordRecoveryServiceTests` (novo; `UserManager`, `IEmailService`, `ISessionValidator` mockados): resposta genérica (inexistente, inativo, falha de envio), código válido, inválido, limite por conta, sessões invalidadas.

### 7.7 Fase 8 — Documentação

`catalogo-erros-api.md` (§4.1 e §6), Spec #21 (nota), `deploy.md` (variáveis de e-mail), esta spec.

## 8. Riscos

| Risco | Consequência | Tratamento |
|---|---|---|
| Diferença de tempo de resposta (envio só para e-mail cadastrado) | Permite inferir se a conta existe | Aceito; melhoria futura: envio em segundo plano |
| Contador de tentativas em memória | Zera quando o container reinicia; com mais de uma réplica, cada uma conta separado | Aceito: o limite por IP (R6) continua valendo, e o código expira em minutos |
| Gmail bloquear ou limitar o envio | Usuários não recebem o código | Log do erro (R8); alternativa futura: serviço de e-mail transacional |
| E-mail cai no spam | Usuário não encontra o código | Orientar no app a conferir o spam |
| Cadastro não confirma o e-mail | Alguém cadastrado com e-mail de outra pessoa recebe os códigos dessa pessoa | Fora do escopo (confirmação de e-mail) |

## 9. Questões em aberto

1. Aprovar R1–R11 — em especial R7 (limite por conta), R10 (traduzir as mensagens de senha, opcional) e R11 (não devolver JWT).
2. Qual conta Gmail envia os e-mails? (Precisa de verificação em duas etapas e senha de app.)
3. A `feature/admin-portal` entra na `dev` antes desta? (Define onde nasce o `AddRateLimiter`, Fase 4.)
