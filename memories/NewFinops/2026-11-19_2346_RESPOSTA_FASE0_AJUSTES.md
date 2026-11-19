# Resposta: Fase 0, ajustes (health anônimo + 401 limpo em /api)

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 19/11/2026 23:46 (Brasília)
**Em resposta a:** `2026-11-19_2045_FASE0_AJUSTES.md`
**Responsável humano:** HLGDR

Ajustes entregues no commit `a0eb0ca` (2 arquivos, +34 / −4). Obrigado pela correção do item 5: anotado que o `6bc0fb4` não tem segredos.

---

## 1. Ajuste 1: `/api/health` anônimo

`HealthController` ganhou `[AllowAnonymous]` (+ `using Microsoft.AspNetCore.Authorization`). A resposta continua só `{ status, timestamp }`. O filtro global `RequireAuthenticatedUser` segue valendo para o resto.

## 2. Ajuste 2: 401/403/404 limpos em `/api`

### ⚠️ Desvio da implementação sugerida (com motivo)

Não usei `OnRedirectToIdentityProvider`. No `OpenIdConnectHandler`, o challenge **busca a configuração do Entra (`.well-known/openid-configuration`) antes** de disparar esse evento. Na prática:
- com placeholders (smoke test local), o `fetch` em `/api` continuaria dando **500**;
- em produção, cada 401 em `/api` faria uma consulta de metadados ao Entra antes de responder.

Em vez disso, usei `ForwardDefaultSelector` nas `OpenIdConnectOptions`: em `/api`, as operações do esquema OIDC são **encaminhadas ao esquema de cookie**, que já tinha o `OnRedirectToLogin` → 401. Ganhei o `OnRedirectToAccessDenied` → 403. O encaminhamento acontece antes de qualquer lógica do OIDC, então não há 302, nem chamada externa, nem conflito com os eventos que o `AddMicrosoftIdentityWebApp` registra. Os eventos dele continuam intactos, sem precisar encadear. Fora de `/api` o seletor retorna `null` e o fluxo OIDC é o normal.

Se preferir mesmo a versão com `OnRedirectToIdentityProvider` encadeado, troco, mas o teste de 401 com placeholders só passaria com credenciais reais.

### Diff resumido do `Program.cs`

```diff
+// Em /api, challenge/forbid do OIDC → esquema de cookie (401/403, sem ir ao Entra)
+builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
+{
+    options.ForwardDefaultSelector = context =>
+        context.Request.Path.StartsWithSegments("/api")
+            ? CookieAuthenticationDefaults.AuthenticationScheme
+            : null;
+});

 builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
 {
     options.Events.OnRedirectToLogin = context =>
     {
         if (context.Request.Path.StartsWithSegments("/api"))
-            context.Response.StatusCode = 401;
+            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
         else
             context.Response.Redirect(context.RedirectUri);
         return Task.CompletedTask;
     };
+
+    options.Events.OnRedirectToAccessDenied = context =>
+    {
+        if (context.Request.Path.StartsWithSegments("/api"))
+            context.Response.StatusCode = StatusCodes.Status403Forbidden;
+        else
+            context.Response.Redirect(context.RedirectUri);
+        return Task.CompletedTask;
+    };
 });
 ...
 app.UseDefaultFiles();
 app.UseStaticFiles();
-app.MapFallbackToFile("index.html");
-
 app.MapControllers();
 app.MapRazorPages();
+
+// Rotas /api inexistentes → 404 (e não o index.html da SPA)
+app.MapFallback("/api/{**slug}", () => Results.NotFound());
+app.MapFallbackToFile("index.html");
```

### AuthGuard

Sem alteração. Ele já trata qualquer resposta `!ok` (inclusive 401) com `window.location.href = '/login'`. Agora o caminho é um 401 direto, sem depender do `catch` de CORS.

---

## 3. Validação

| # | Verificação | Resultado |
|---|---|---|
| 1 | `dotnet build` | ✅ 0 erros, 0 avisos |
| 1 | `npm run build` | ✅ OK |

**Smoke test** (`dotnet run`, placeholders do Entra, sem banco, build do frontend copiado temporariamente para `backend/wwwroot` e apagado depois):

| Requisição | Esperado | Obtido |
|---|---|---|
| `GET /api/health` sem login | 200 | ✅ **200** `application/json`: `{"status":"healthy","timestamp":"..."}` |
| `GET /api/auth/me` sem sessão | 401 | ✅ **401** (sem `Location`) |
| `GET /api/naoexiste` | 404 | ✅ **404** |
| `GET /api/auth/naoexiste` | 404 | ✅ **404** |
| `GET /` sem sessão | SPA carrega | ✅ **200** `text/html` (o AuthGuard então recebe 401 e vai para `/login`) |
| `GET /login` | SPA | ✅ **200** `text/html` |
| `GET /qualquer/rota` | SPA | ✅ **200** `text/html` |

Log do servidor durante o teste: **0 linhas `fail`** (antes, os 500 vinham do OIDC tentando buscar o tenant placeholder).

O `/MicrosoftIdentity/Account/SignIn` (botão de login) continua indo para o OIDC normalmente. Com placeholders ele falha, como esperado. Só dá para validar com as credenciais reais no Coolify.

— VSClaude
