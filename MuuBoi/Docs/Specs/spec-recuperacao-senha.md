# Spec #23: Recuperação de Senha e Confirmação de E-mail por Código

**Módulo:** Conta / Autenticação (`ApplicationUser`, ASP.NET Identity, e-mail)
**Versão:** 1.0
**Data:** 26/Out/2026
**Status:** ✅ **Implementada** na branch `feature/password-recovery` (Fases 1–7, 9 e 10), validada localmente com envio real pelo Gmail. ⏳ Falta a Fase 8 (configuração em produção), a fazer no deploy. Abordagem (código de 6 dígitos no app) escolhida pela usuária em 25/Out/2026; para a confirmação de e-mail, a usuária decidiu em 26/Out/2026: login bloqueado até confirmar, membros confirmam no 1º login e contas existentes marcadas como confirmadas. As decisões das §4 e §4.1 foram aprovadas fase a fase, junto com o código de cada uma.
**Depende de:** Spec #21 (`spec-exclusao-conta-perfil.md`) — validação de sessão por `SecurityStamp` (`ISessionValidator`) · regras de senha do Identity (padrão, sem customização) · Spec de Gestão de Membros (`spec-gestao-membros.md`) — criação de membro com senha temporária.
**Relaciona-se com:** Spec #22 (`spec-portal-administrativo.md`) — limitador de requisições (`AddRateLimiter`, `ResolveClientIp`) já implementado na branch `feature/admin-portal` · Spec #14 §5.6 (autenticação fica fora do offline) · `Docs/catalogo-erros-api.md` (§4.1, §6).
**Substitui:** a branch `task/password-recovery` (commit `9c1239a`, 21/Ago/2026), que fica como referência e **não** é mesclada (§3).

> **Decisão de abordagem:** o MuuBoi tem **um único cliente** — o app Android — e nenhum front web. Em vez de um link para uma página de redefinição, o e-mail traz um **código de 6 dígitos** que o usuário digita no próprio app, junto com a senha nova. Funciona mesmo que o e-mail seja lido no computador, e não depende de chaves de criptografia que se perdem quando o container reinicia (§3, item 2). A confirmação de e-mail usa o **mesmo mecanismo** (§4.1).

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 25/Out/2026 | Rascunho: código de 6 dígitos por e-mail, duas rotas, invalidação de sessões, limite de tentativas, falha de envio sem `500` |
| 0.2 | 26/Out/2026 | Inclui o fluxo de **confirmação de e-mail** (§4.1, §5.3–§5.6): cadastro sem JWT, login bloqueado até confirmar, duas rotas novas, membros confirmam no 1º login, contas existentes marcadas como confirmadas, redefinição de senha também confirma o e-mail |
| 1.0 | 26/Out/2026 | Implementada (Fases 1–7, 9, 10). Ajustes vindos da implementação: validade medida do código (~6–7 min, R2); diferença de tempo medida (~4,2 s × ~0,03 s); limite por IP **por rota** e `429` no formato A (R6); limite por conta compartilhado em `CodeAttemptLimiter`; migração aplicada no startup (C7, risco mitigado) |

---

## 1. Contexto e objetivo

Hoje não há como recuperar o acesso: quem esquece a senha depende de alguém mexer no banco. A rota de troca de senha (`PATCH /api/auth/me/password`) exige a senha atual e um usuário logado.

Também não há nenhuma garantia de que o e-mail de uma conta pertence a quem a criou. O cadastro (`POST /api/auth/register`) já devolve o JWT, e o membro criado pelo Admin entra com a senha temporária sem nunca provar o e-mail. Com a recuperação de senha, isso vira um problema: os códigos iriam para um e-mail que pode ser de outra pessoa ou ter sido digitado errado (§8).

Esta spec entrega:
1. **Pedir o código** de redefinição informando o e-mail, sem estar logado.
2. **Redefinir a senha** com e-mail + código + senha nova.
3. **Encerrar as sessões abertas** da conta ao redefinir (mesma garantia da troca de senha, Spec #21).
4. **Confirmar o e-mail** com um código antes do primeiro acesso, no cadastro e no 1º login de membros.
5. **Proteções** contra enumeração de e-mails, disparo em massa e tentativa de adivinhar o código.

## 2. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/auth/forgot-password` | Troca de e-mail da conta (hoje nenhuma rota altera o e-mail) |
| `POST /api/auth/reset-password` | Envio por SMS ou WhatsApp |
| `POST /api/auth/confirm-email` | Login com Google/Microsoft |
| `POST /api/auth/resend-confirmation` | Tradução das mensagens de perfis, login externo e recovery codes do Identity (não usados) |
| Mudança no `register` (sem JWT) e no `login` (bloqueio) | Implementação das telas no app |
| Migração de dados: contas existentes confirmadas | Prazo de carência para confirmar (descartado, C2) |
| Envio do código por SMTP (Gmail) | |
| Limite de tentativas por IP e por conta | |
| Configuração em produção (secrets do Container App) | |
| Telas do app (contrato, §6) | |

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
| 9 | Toda a lógica de autenticação está no `AuthController` (precedente) | Lógica nova num service testável (R9, C10) |
| 10 | `EmailConfirmed` existe na tabela do Identity (`AspNetUsers`), mas fica `false` para todas as contas — só o usuário de sistema do `DatabaseSeeder` nasce confirmado. O login não olha esse campo | C2, C7 |
| 11 | `POST /api/auth/register` cria a propriedade e o Admin e devolve `201` com o JWT (`AuthResponseDto`) | C3 — passa a devolver só uma mensagem |
| 12 | `POST /api/users` (Admin) cria o membro com `TemporaryPassword`; o membro faz login direto com ela | C6 — confirma no 1º login |
| 13 | O login responde `403` "Usuário desativado." **antes** de conferir a senha | Mantido como está (fora do escopo); o bloqueio novo (C2) fica **depois** da senha |

## 4. Decisões propostas — recuperação de senha

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| R1 | **Código de 6 dígitos** gerado pelo próprio Identity: `options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider` no `AddIdentityCore` (⚠️ `Program.cs`). Com isso, `GeneratePasswordResetTokenAsync` passa a devolver um código numérico (TOTP baseado no `SecurityStamp`) e `ResetPasswordAsync` o valida — sem tabela nova nem migração | (a) Token longo por link — depende do front web (§3, item 3) e das chaves de Data Protection (item 2). (b) Código próprio gravado em tabela — exige migração, hash e expiração manuais |
| R2 | **Validade:** a do provedor padrão — janela de passos de 3 minutos com tolerância, **entre 6 e 9 minutos**, conforme o ponto do passo em que o código foi gerado. **Medido em 26/Out:** gerado às 20:56:06, aceito até 21:02:11 e recusado às 21:03:11 (~6–7 min). O e-mail informa "válido por alguns minutos", sem prometer um número exato | Provedor customizado com validade configurável — mais código para pouco ganho |
| R3 | **Uso único na prática:** redefinir a senha troca o `SecurityStamp`, e todos os códigos emitidos antes deixam de valer | — |
| R4 | **`forgot-password` sempre responde `200`** com a mesma mensagem ("Se o e-mail estiver cadastrado, você receberá um código em instantes."), exista ou não a conta, esteja ou não ativa. O código é enviado para toda conta **ativa**, confirmada ou não (C8) | Responder `404` para e-mail inexistente — permitiria descobrir quem tem conta |
| R5 | **Redefinir encerra as sessões:** depois do `ResetPasswordAsync`, `ISessionValidator.Invalidate(user.Id)` (mesmo comportamento da troca de senha, Spec #21) | — |
| R6 | **Limite por IP** com o `AddRateLimiter` do ASP.NET (o mesmo mecanismo do login do portal, Spec #22): `forgot-password` e `reset-password` com **5 requisições a cada 15 minutos por IP** → `429` `{ "error": "Muitas tentativas. Tente novamente mais tarde." }` (formato A, igual ao do portal, para um único `OnRejected` após o merge). A mesma política vale para as rotas de confirmação (C9). **Contador por IP + rota** (decidido em 26/Out): cada rota tem seus 5, para o fluxo normal (cadastro → confirmação → reenvio) não esgotar o limite. Política `AuthRateLimitPolicies.AccountCodes`; `ResolveClientIp` idêntico ao da `feature/admin-portal` | Sem limite — permitiria disparar e-mails em massa |
| R7 | **Limite por conta** no `reset-password`: depois de **5 códigos errados** para o mesmo usuário, novas tentativas são recusadas por 15 minutos (contador em memória, `IMemoryCache`), com a mesma mensagem genérica do código inválido. Protege contra quem troca de IP para adivinhar o código | (a) Lockout do Identity — também bloquearia o **login** da vítima. (b) Trocar o `SecurityStamp` após 5 erros — derrubaria as sessões da vítima a cada ataque |
| R8 | **Falha no envio do e-mail:** registra no log (`LogError`) e responde o mesmo `200` genérico. O usuário pode pedir de novo | Responder `500` — revela que o e-mail existe e não deixa rastro |
| R9 | **`IPasswordRecoveryService` + `PasswordRecoveryService`** (`Application/`), recebendo `UserManager<ApplicationUser>`, `IEmailService`, `ISessionValidator`, `IMemoryCache` e `ILogger`; o `AuthController` só chama o service | Manter a lógica no controller (como o resto do `AuthController`) — não seria testável |
| R10 | *(Opcional)* **Mensagens de senha em português** com um `IdentityErrorDescriber` próprio (⚠️ `Program.cs`) — atende também cadastro, troca de senha e criação de membro | Deixar em inglês e o app validar antes (lista de regras já levantada) |
| R11 | **Resposta do reset:** `200` com mensagem; o usuário faz login em seguida (não devolve JWT) | Devolver o JWT já logado — mais cômodo, mas mistura duas responsabilidades e esconde o login com a senha nova |

**Risco aceito — diferença de tempo:** o envio por SMTP leva alguns segundos (medido em 26/Out: **~4,2 s** para e-mail cadastrado contra **~0,03 s** para inexistente, no primeiro envio pelo Gmail) e só acontece para e-mails cadastrados. Alguém medindo o tempo de resposta poderia inferir se a conta existe. Evitar isso exige enviar em segundo plano (fila + `BackgroundService`); para o TCC, fica registrado como risco (§8) e melhoria futura.

## 4.1 Decisões propostas — confirmação de e-mail

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| C1 | **Mesmo mecanismo do R1:** `options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider` (⚠️ `Program.cs`). `GenerateEmailConfirmationTokenAsync` devolve um código de 6 dígitos e `ConfirmEmailAsync` o valida. O Identity inclui a finalidade no cálculo do código, então **um código de redefinição não confirma e-mail, e vice-versa**. Mesma validade do R2 | Link de confirmação — mesmos problemas do §3, itens 2 e 3 |
| C2 | **Login bloqueado até confirmar** *(decidido pela usuária)*. No `login`, **depois** de conferir a senha: se `EmailConfirmed = false` → `403` `{ "message": "Confirme seu e-mail para entrar. Enviamos um código para você.", "code": "EmailNotConfirmed" }` e **um código novo é enviado**. O bloqueio vem depois da senha para não revelar o estado da conta a quem não sabe a senha. O campo `code` (aditivo ao formato D) separa este `403` do "Usuário desativado." | (a) Usar o app normalmente e exigir a confirmação só na recuperação — a conta não confirmada funcionaria por tempo indeterminado. (b) Prazo de carência — mais estados para explicar e testar |
| C3 | **Cadastro sem JWT:** `register` cria a propriedade e o Admin como hoje, envia o código e responde `201` `{ "message": "Conta criada. Enviamos um código de confirmação para o seu e-mail.", "email": "..." }`. Falha no envio → mesmo `201`, erro no log; o app oferece "Reenviar código" (C5). ⚠️ **Muda o contrato do app:** a resposta deixa de ser `AuthResponseDto` | Devolver o JWT e bloquear depois — o app teria um token válido para uma conta que ainda não pode entrar |
| C4 | **`confirm-email` devolve o JWT:** com o código certo, `EmailConfirmed = true` e a resposta é `200` com o `AuthResponseDto` — a mesma do login. A confirmação é o último passo do cadastro e do 1º login, que já terminariam logados; pedir a senha de novo só aumenta o atrito. Quem tem acesso ao e-mail já conseguiria entrar pela redefinição de senha, então isso não abre um caminho novo. **Repetir é seguro:** com a conta já confirmada e o código ainda válido, responde o mesmo `200` (o app pode reenviar se a internet cair no meio) | Responder só `200` com mensagem e mandar para o login, como no R11 — consistente, mas obriga o usuário a digitar a senha logo depois de criá-la |
| C5 | **`resend-confirmation` sempre responde `200`** com a mesma mensagem ("Se o e-mail estiver cadastrado e aguardando confirmação, você receberá um código em instantes."). Só envia para conta existente, ativa e não confirmada | Responder `404`/`409` — revelaria quem tem conta e quem já confirmou |
| C6 | **Membros confirmam no 1º login** *(decidido pela usuária)*. `POST /api/users` continua igual e **não** envia código: o código vence em minutos e o membro pode demorar dias para entrar. No 1º login com a senha temporária, cai no C2 e recebe o código. `UserListItemDto` e `UserResponseDto` ganham `EmailConfirmed` (bool), para o Admin ver "Aguardando confirmação" na lista de membros | Membro já nascer confirmado — um e-mail digitado errado pelo Admin só seria descoberto quando a recuperação falhasse |
| C7 | **Contas existentes confirmadas** *(decidido pela usuária)*: migração só de dados, `UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0` (⚠️ migração). **Precisa ir no mesmo deploy** que o bloqueio do C2, senão todo mundo que já usa o app fica sem entrar — garantido pelo `Database.Migrate()` no startup (§7.5) | Exigir confirmação de todos no próximo login — sem migração, mas todos passam pela tela de código uma vez |
| C8 | **Redefinir a senha também confirma o e-mail:** o código do reset chegou por e-mail, o que prova o acesso a ele. No `reset-password` com sucesso, se `EmailConfirmed = false`, passa a `true`. Resolve o membro que esqueceu a senha temporária antes de confirmar | Exigir confirmar antes de poder redefinir — o membro nessa situação ficaria sem saída |
| C9 | **Limites:** a política por IP do R6 vale também para `confirm-email`, `resend-confirmation` e `register` (o cadastro envia e-mail para um endereço qualquer). O `confirm-email` tem o limite por conta do R7 (5 códigos errados → 15 min recusando), com contador próprio. Para não lotar a caixa do usuário, **no máximo um envio a cada 60 segundos por conta**, somando login (C2), cadastro e reenvio; dentro desse intervalo a resposta é a mesma, sem novo envio | Sem intervalo — repetir o login com a senha certa dispararia um e-mail a cada tentativa |
| C10 | **`IEmailConfirmationService` + `EmailConfirmationService`** (`Application/`), com `UserManager<ApplicationUser>`, `IEmailService`, `IMemoryCache` e `ILogger`. O `AuthController` chama o service no `register`, no `login` (C2) e nas duas rotas novas. `IEmailService` ganha `SendEmailConfirmationCodeAsync(toEmail, toName, code)` → `Task<bool>` | Juntar com o `PasswordRecoveryService` — os dois fluxos ficariam acoplados num service grande |

## 5. Contrato da API

### 5.1 `POST /api/auth/forgot-password`

```json
{ "email": "maria@exemplo.com" }
```

| Situação | Resposta |
|---|---|
| E-mail cadastrado e ativo (confirmado ou não, C8) | `200` `{ "message": "Se o e-mail estiver cadastrado, você receberá um código em instantes." }` e o e-mail é enviado |
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
| Código válido, senha dentro das regras | `200` `{ "message": "Senha redefinida com sucesso." }`; sessões encerradas (R5); e-mail marcado como confirmado (C8) |
| Código errado, expirado, já usado, e-mail inexistente ou conta inativa | `400` `{ "message": "Código inválido ou expirado." }` — **mesma** mensagem em todos os casos |
| 5 códigos errados para a conta nos últimos 15 min | `400` com a mesma mensagem, sem nem testar o código (R7) |
| Senha fora das regras do Identity | `400` `{ "message": "Erro ao redefinir a senha.", "errors": [ ... ] }` (formato D; mensagens em português, R10) |
| Campos ausentes / código fora de 6 dígitos / senha com menos de 6 caracteres | `400` (formato B) com mensagens em português |
| Mais de 5 tentativas em 15 min do mesmo IP | `429` |

**Ordem de validação no reset:** formato (DTO) → limite por conta (R7) → usuário existe e está ativo → código + senha (`ResetPasswordAsync`). Se o código estiver errado, o erro é "código inválido" mesmo que a senha também esteja fora das regras — o usuário não fica sabendo das regras da senha sem ter o código certo.

### 5.3 `POST /api/auth/register` (mudança)

Corpo igual ao de hoje. Muda só a resposta de sucesso:

| Situação | Resposta |
|---|---|
| Conta criada | `201` `{ "message": "Conta criada. Enviamos um código de confirmação para o seu e-mail.", "email": "maria@exemplo.com" }` e o código é enviado. **Sem JWT** (C3) |
| Conta criada, mas falha no SMTP | `201` com a mesma resposta; erro no log |
| E-mail já cadastrado | `409` "Email já cadastrado." (sem mudança) |
| Senha fora das regras | `400` "Erro ao criar usuário." + `errors` (sem mudança) |
| Mais de 5 cadastros em 15 min do mesmo IP | `429` (C9) |

### 5.4 `POST /api/auth/login` (mudança)

Corpo e respostas de hoje mantidos. Entra um caso novo, conferido **depois** da senha:

| Situação | Resposta |
|---|---|
| Senha certa, e-mail não confirmado | `403` `{ "message": "Confirme seu e-mail para entrar. Enviamos um código para você.", "code": "EmailNotConfirmed" }` e um código novo é enviado (no máximo um a cada 60 s, C9) |
| Senha certa, e-mail confirmado | `200` `AuthResponseDto` (sem mudança) |

**Ordem no login:** usuário existe → ativo (`403` "Usuário desativado.", como hoje) → senha (`401`) → e-mail confirmado (`403` `EmailNotConfirmed`) → JWT.

### 5.5 `POST /api/auth/confirm-email`

```json
{ "email": "maria@exemplo.com", "code": "731045" }
```

| Situação | Resposta |
|---|---|
| Código válido | `200` `AuthResponseDto` (mesmo do login); `EmailConfirmed = true` (C4) |
| Conta já confirmada e código ainda válido (reenvio) | `200` `AuthResponseDto` (C4) |
| Código errado ou expirado, e-mail inexistente ou conta inativa | `400` `{ "message": "Código inválido ou expirado." }` — **mesma** mensagem em todos os casos |
| 5 códigos errados para a conta nos últimos 15 min | `400` com a mesma mensagem, sem testar o código (C9) |
| Campos ausentes / código fora de 6 dígitos / e-mail inválido | `400` (formato B) com mensagens em português |
| Mais de 5 tentativas em 15 min do mesmo IP | `429` |

**Ordem de validação:** formato (DTO) → limite por conta → usuário existe e está ativo → código (`ConfirmEmailAsync`) → JWT.

### 5.6 `POST /api/auth/resend-confirmation`

```json
{ "email": "maria@exemplo.com" }
```

| Situação | Resposta |
|---|---|
| Conta existente, ativa e não confirmada | `200` `{ "message": "Se o e-mail estiver cadastrado e aguardando confirmação, você receberá um código em instantes." }` e o código é enviado (no máximo um a cada 60 s, C9) |
| E-mail inexistente, conta inativa ou já confirmada | `200` com a **mesma** mensagem, sem envio (C5) |
| Falha no SMTP | `200` com a mesma mensagem; erro no log |
| E-mail ausente ou inválido | `400` (formato B) |
| Mais de 5 pedidos em 15 min do mesmo IP | `429` |

**E-mail enviado** (texto simples):

```
Assunto: Confirme seu e-mail — MuuBoi

Olá, Maria!

Seu código para confirmar o e-mail no MuuBoi é:

    731045

Ele é válido por alguns minutos. Se você não criou uma conta no MuuBoi, ignore este e-mail.

Equipe MuuBoi
```

### 5.7 `GET /api/users` e respostas de membro (mudança)

`UserListItemDto` e `UserResponseDto` ganham `"emailConfirmed": true | false` (C6). Nenhuma outra mudança em `/api/users`.

## 6. Contrato do app

Fora do offline: todas as rotas desta spec exigem conexão, como o login (Spec #14 §5.6).

### 6.1 Recuperação de senha

1. **Tela "Esqueci minha senha"** (a partir do login): campo de e-mail → `forgot-password` → vai para a tela do código mostrando a mensagem genérica.
2. **Tela "Redefinir senha"**: código (6 dígitos, teclado numérico), senha nova e confirmação.
   - Validar a senha **antes** de enviar, com as mesmas regras do servidor: mínimo 6 caracteres, um número, uma minúscula (a–z), uma maiúscula (A–Z) e um caractere especial (espaço e letras acentuadas contam como especial).
   - Botão **"Reenviar código"** com espera de 60 segundos entre envios.
3. `200` → mensagem de sucesso → volta para o login com o e-mail preenchido.
4. `400` "Código inválido ou expirado." → mostrar a mensagem e oferecer "Reenviar código".
5. `429` → mostrar a mensagem e desabilitar o envio por alguns minutos.
6. Depois de redefinir, os outros aparelhos logados nessa conta recebem `401` na próxima requisição e voltam para o login (Spec #21).

### 6.2 Confirmação de e-mail

1. **Cadastro:** `201` → tela **"Confirme seu e-mail"** com o e-mail da resposta. O app **não** recebe token aqui.
2. **Login:** `403` com `code = "EmailNotConfirmed"` → mesma tela "Confirme seu e-mail" (o código já foi enviado pelo servidor). Diferenciar pelo campo `code`, não pela mensagem; `403` sem `code` continua sendo "Usuário desativado.".
3. **Tela "Confirme seu e-mail":** campo de código (6 dígitos, teclado numérico) → `confirm-email`.
   - `200` → guardar o token e entrar no app, igual ao login.
   - `400` → mostrar "Código inválido ou expirado." e oferecer "Reenviar código".
   - Botão **"Reenviar código"** → `resend-confirmation`, com espera de 60 segundos entre envios.
   - `429` → mostrar a mensagem e desabilitar o envio por alguns minutos.
4. Se a resposta do `confirm-email` não chegar (internet caiu), o app pode reenviar o mesmo código: responde `200` enquanto o código for válido (C4).
5. **Membro novo:** o Admin informa a senha temporária fora do app. No 1º login, o membro cai no item 2.
6. **Lista de membros (Admin):** mostrar "Aguardando confirmação" quando `emailConfirmed = false`.

## 7. Fases de implementação e validação

Branch proposta: `feature/password-recovery`, a partir da `dev`. Cada fase é proposta com o código exato e só implementada após aprovação.

| Fase | Status |
|---|---|
| 1 — Provedores de código e serviço de e-mail (R1, R2, C1) | ✅ Aplicada; envio real pelo Gmail e validade (~6–7 min, R2) validados em 26/Out. Recusa cruzada de códigos (C1) fica para a Fase 4 |
| 2 — `forgot-password` (R4, R8) | ✅ Aplicada e validada localmente (26/Out) |
| 3 — `reset-password` e sessões (R3, R5, R7, C8) | ✅ Aplicada e validada localmente (26/Out). Limite por conta (R7) e confirmação do e-mail (C8) cobertos por testes unitários |
| 4 — Confirmação de e-mail: `register`, `login`, `confirm-email`, `resend-confirmation` (C2–C6, C10) | ✅ Aplicada e validada localmente (26/Out), inclusive C1 nos dois sentidos e C8 (conta confirmada pela redefinição de senha). Limite por conta no `confirm-email` coberto por testes unitários. Limite compartilhado extraído para `CodeAttemptLimiter` |
| 5 — Migração das contas existentes (C7) | ✅ `20261027004233_Spec23_ConfirmExistingEmails` criada e aplicada no banco local (26/Out): 24 contas não confirmadas → 0; membro de teste entra direto; conta criada depois continua passando pela confirmação |
| 6 — Limite por IP (R6, C9) | ✅ Aplicada e validada localmente (26/Out): 6ª chamada na mesma rota → `429`; outra rota segue liberada; login sem limite |
| 7 — *(Opcional)* Mensagens de senha em português (R10) | ✅ Aplicada e validada localmente (26/Out): `PortugueseIdentityErrorDescriber` (`Application/Helpers`); cadastro, redefinição, troca de senha e criação de membro respondem em português; códigos dos erros mantidos |
| 8 — Configuração em produção | ⏳ No deploy (variáveis em `deploy.md`) |
| 9 — Testes | ✅ Escritos junto de cada fase: `PasswordRecoveryServiceTests` (13) e `EmailConfirmationServiceTests` (14); suíte com 356 testes passando (26/Out) |
| 10 — Documentação | ✅ `catalogo-erros-api.md` (§1, §2, §4.1, §4.2, §5.1, §6, §7, §8), Spec #21 (nota), Gestão de Membros (nota), `deploy.md` (variáveis de e-mail e migração no startup), esta spec (26/Out) |

### 7.1 Fase 1 — Provedores de código e serviço de e-mail

- `AddIdentityCore` com `PasswordResetTokenProvider` e `EmailConfirmationTokenProvider` = `TokenOptions.DefaultEmailProvider` (⚠️ `Program.cs`).
- `IEmailService.SendPasswordResetCodeAsync(toEmail, toName, code)` e `SendEmailConfirmationCodeAsync(toEmail, toName, code)` → `Task<bool>`; `EmailService` em `Infrastructure/Services` (SMTP com `System.Net.Mail`, seção `Email` da configuração). Sem `MailKit`.
- Registro do `IEmailService` (⚠️ `Program.cs`).

| Validação planejada | Esperado |
|---|---|
| `GeneratePasswordResetTokenAsync` / `GenerateEmailConfirmationTokenAsync` | Código de 6 dígitos |
| Código de redefinição usado no `ConfirmEmailAsync` (e vice-versa) | Recusado (C1) |
| Validade real do código | Medir: código aceito até quantos minutos depois de gerado (confirma R2) |
| Envio pelo SMTP com as credenciais do user-secrets | E-mail chega com o código |

### 7.2 Fase 2 — `forgot-password`

| Validação planejada | Esperado |
|---|---|
| E-mail cadastrado (confirmado ou não) | `200` + e-mail recebido |
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
| Conta não confirmada redefine a senha | `200`; `EmailConfirmed = true`; login entra direto (C8) |

### 7.4 Fase 4 — Confirmação de e-mail

| Validação planejada | Esperado |
|---|---|
| Cadastro | `201` sem token + e-mail com o código |
| Login antes de confirmar | `403` `EmailNotConfirmed` + novo código |
| Login antes de confirmar com senha errada | `401` "Credenciais inválidas." (não revela o estado da conta) |
| Dois logins seguidos em menos de 60 s | Só um e-mail enviado |
| `confirm-email` com código certo | `200` com JWT; login seguinte entra direto |
| `confirm-email` repetido com o mesmo código | `200` com JWT |
| `confirm-email` com código errado / e-mail inexistente / conta inativa | `400`, mesma mensagem |
| 6ª tentativa errada na mesma conta | `400` sem testar o código; liberado após 15 min |
| `resend-confirmation` para conta inexistente / confirmada / inativa | `200`, mesma mensagem, nada enviado |
| Membro criado pelo Admin | Nenhum e-mail na criação; `emailConfirmed = false` na lista; 1º login → `403` `EmailNotConfirmed` |

### 7.5 Fase 5 — Migração das contas existentes

- Migração só de dados (⚠️ migração): `20261027004233_Spec23_ConfirmExistingEmails`, com `UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0` e `Down` vazio (não dá para saber quais eram confirmadas).
- Vai **no mesmo deploy** da Fase 4 sem passo manual: o `Program.cs` roda `Database.Migrate()` ao subir o app, antes da primeira requisição.

| Validação planejada | Esperado |
|---|---|
| Conta criada antes da migração | Login entra direto |
| Conta criada depois da migração | Passa pela confirmação |

### 7.6 Fase 6 — Limite por IP

- `AddRateLimiter` com a política de `forgot-password`, `reset-password`, `confirm-email`, `resend-confirmation` e `register` (⚠️ `Program.cs`). Se a `feature/admin-portal` já estiver na `dev`, reaproveitar o `AddRateLimiter` e o `ResolveClientIp` de lá; senão, criar aqui e resolver o conflito no merge do portal.

| Validação planejada | Esperado |
|---|---|
| 6ª requisição em 15 min do mesmo IP | `429` |
| Login e demais rotas | Sem limite novo |

### 7.7 Fase 8 — Configuração em produção

No Container App, variáveis de ambiente (as sensíveis como *secret*):

| Variável | Valor |
|---|---|
| `Email__SmtpHost` | `smtp.gmail.com` |
| `Email__SmtpPort` | `587` |
| `Email__FromEmail` / `Email__Username` | conta Gmail do MuuBoi |
| `Email__Password` | **senha de app** do Gmail (secret) — exige verificação em duas etapas ativa na conta; não é a senha normal |
| `Email__FromName` | `MuuBoi` |

### 7.8 Fase 9 — Testes

- `PasswordRecoveryServiceTests` (novo; `UserManager`, `IEmailService`, `ISessionValidator` mockados): resposta genérica (inexistente, inativo, falha de envio), código válido, inválido, limite por conta, sessões invalidadas, conta não confirmada passa a confirmada (C8).
- `EmailConfirmationServiceTests` (novo; `UserManager`, `IEmailService` mockados): envio no cadastro e no login, intervalo de 60 s, código válido, inválido, repetido, limite por conta, reenvio com resposta genérica (inexistente, inativa, já confirmada, falha de envio).

### 7.9 Fase 10 — Documentação

`catalogo-erros-api.md` (§4.1, §4.2 e §6 — inclusive o novo `403` com `code`), Spec #21 (nota), spec de Gestão de Membros (nota sobre o 1º login), `deploy.md` (variáveis de e-mail e ordem migração + deploy), esta spec.

## 8. Riscos

| Risco | Consequência | Tratamento |
|---|---|---|
| Diferença de tempo de resposta (envio só para e-mail cadastrado) | Permite inferir se a conta existe | Aceito; melhoria futura: envio em segundo plano |
| Contadores em memória (tentativas e intervalo de 60 s) | Zeram quando o container reinicia; com mais de uma réplica, cada uma conta separado | Aceito: o limite por IP (R6) continua valendo, e o código expira em minutos |
| Gmail bloquear ou limitar o envio | Usuários não recebem o código — e, com a confirmação, **novas contas não conseguem entrar** | Log do erro (R8); botão de reenviar; alternativa futura: serviço de e-mail transacional |
| E-mail cai no spam | Usuário não encontra o código | Orientar no app a conferir o spam |
| Migração C7 aplicada depois do deploy do bloqueio | Todos os usuários atuais ficam sem entrar até a migração rodar | Mitigado: o app aplica as migrações ao subir (`Database.Migrate()` no `Program.cs`), então a C7 entra junto com o bloqueio. Só voltaria a ser risco se as migrações deixassem de rodar no startup |
| Cadastro com e-mail de outra pessoa | A dona do e-mail recebe um código que não pediu | Aceito: a conta nunca é usada sem o código; o texto do e-mail orienta a ignorar. Limite por IP no `register` (C9) |
| Vários usuários atrás do mesmo IP (CGNAT de operadora móvel) | Dois usuários pedindo código pela mesma rota no mesmo intervalo podem somar mais de 5 e receber `429` | Aceito para o TCC (5 por rota, decidido em 26/Out); se aparecer na prática, subir o `PermitLimit` |
| App antigo instalado depois do deploy | Espera JWT no `register` e não trata o `403` `EmailNotConfirmed` | Publicar a versão do app junto com o deploy da API |

## 9. Questões em aberto

1. ~~Aprovar R1–R11.~~ Aprovadas fase a fase em 26/Out, junto com o código (R10 incluída).
2. ~~Aprovar C1–C10.~~ Aprovadas fase a fase em 26/Out, junto com o código.
3. ~~Qual conta Gmail envia os e-mails?~~ `contatomuuboi@gmail.com`, com verificação em duas etapas e senha de app (26/Out).
4. ~~A `feature/admin-portal` entra na `dev` antes desta?~~ Resolvida em 26/Out: o `AddRateLimiter` nasce aqui com o mesmo esqueleto e o mesmo `ResolveClientIp` do portal; no merge, fica um só `AddRateLimiter` com as duas políticas, um só `UseRateLimiter` e um só `ResolveClientIp`.
