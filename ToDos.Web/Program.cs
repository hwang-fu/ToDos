using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using ToDos.Data;
using ToDos.Web.Components;
using ToDos.Web.Components.Auth;
using ToDos.Web.Components.Pages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core (SQLite)
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=todos.db";
    opt.UseSqlite(cs);
});

/*
 * in Blazor Server, the HttpClient you call from components runs on the server and doesn’t automatically include the browser’s auth cookie. So your API sees those calls as anonymous after login, and it may redirect to /login (HTML) or 308 (trailing slash) → your JSON parsing blows up.
 */
// builder.Services.AddScoped(sp =>
// {
//     var nav = sp.GetRequiredService<NavigationManager>(); // resolved inside circuit scope
//     return new HttpClient { BaseAddress = new Uri(nav.BaseUri) }; 
// });
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("self", (serviceProvider, client) =>
    {
        var http = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        client.BaseAddress = new Uri($"{http.Request.Scheme}://{http.Request.Host}/");
    })
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var http = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        var baseUri = new Uri($"{http.Request.Scheme}://{http.Request.Host}");
        foreach (var kv in http.Request.Cookies)
        {
            handler.CookieContainer.Add(baseUri, new Cookie(kv.Key, kv.Value));
        }
        
        return handler;
    });

// ===== AuthN/AuthZ (Cookie + Roles) =====
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";           // where to redirect on 401 for MVC/Pages (Blazor uses AuthorizeView)
        opt.AccessDeniedPath = "/forbidden";
        opt.SlidingExpiration = true;
        opt.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                var p = ctx.Request.Path;
                var isApi = p.StartsWithSegments("/api");
                var isHub = p.StartsWithSegments("/_blazor");
                var wantsJson = ctx.Request.Headers.TryGetValue("Accept", out var acc) 
                                && acc
                                    .Any(v => v.Contains("application/json", StringComparison.OrdinalIgnoreCase));

                if (isApi || isHub || wantsJson)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opt =>
{
    // Everyone must be authenticated to use the app by default:
    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Example fine-grained policy:
    opt.AddPolicy("CanWriteTasks", p => p.RequireRole("Admin"));
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// ===== Middleware order matters =====
app.UseAuthentication();
app.UseAuthorization();

// ===== Auth Endpoints =====
var auth = app.MapGroup("/auth")
    .AllowAnonymous()
    .DisableAntiforgery();

auth.MapPost("/login", async Task<IResult> (
    HttpContext http,
    IUserStore store,
    LoginRequest request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest("Invalid login attempt.");
    }

    if (!store.Validate(request.Username, request.Password, out var user) || user is null)
    {
        return Results.Unauthorized();
    }

    var principal = AuthHelpers.ToPrincipal(user);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = request.RememberMe
        });
    var returnUrl = http.Request.Query["ReturnUrl"].FirstOrDefault();
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/todos" : returnUrl);
    
    // // Read the body yourself, if it's HTML or empty, this returns null
    // var req = await http.Request.ReadFromJsonAsync<LoginRequest>();
    // if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
    //     return Results.BadRequest("Invalid JSON.");
    //
    // if (!store.Validate(req.Username, req.Password, out var user) || user is null)
    //     return Results.Unauthorized();
    //
    // var principal = AuthHelpers.ToPrincipal(user);
    // await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
    //     new AuthenticationProperties { IsPersistent = req.RememberMe });
    //
    // return Results.Ok(new LoginResult(user.Username, user.Roles));
});

auth.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return TypedResults.NoContent();
});

// Optional: quick “whoami”
app.MapGet("/me", (HttpContext http) =>
{
    var user = http.User;
    return (bool) user?.Identity?.IsAuthenticated
        ? Results.Ok(new
        {
            name = user.Identity!.Name,
            roles = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
        })
        : Results.Unauthorized();
});

// ===== Tasks API =====
var todos = app.MapGroup("/api/todos").RequireAuthorization();

// Read endpoints: any authenticated user
todos.MapGet("/", async (ApplicationDbContext db) =>
    await db.ToDos.AsNoTracking()
        .OrderBy(t => t.IsCompleted)
        .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
        .ToListAsync());

todos.MapGet("/{id:guid}", async Task<Results<Ok<TodoItem>, NotFound>> (Guid id, ApplicationDbContext db) =>
{
    var task = await db.ToDos.FindAsync(id);
    return task is null ? TypedResults.NotFound() : TypedResults.Ok(task);
});

// Write endpoints: only Admins (policy)
todos.MapPost("/", [Authorize(Policy = "CanWriteTasks")] async Task<Created<TodoItem>> (TodoItem incoming, ApplicationDbContext db) =>
{
    incoming.Id = Guid.NewGuid();
    incoming.CreatedDate = DateTimeOffset.UtcNow;
    db.ToDos.Add(incoming);
    await db.SaveChangesAsync();
    return TypedResults.Created($"/api/todos/{incoming.Id}", incoming);
});

todos.MapPut("/{id:guid}", [Authorize(Policy = "CanWriteTasks")] async Task<Results<NoContent, NotFound>> (Guid id, TodoItem update, ApplicationDbContext db) =>
{
    var task = await db.ToDos.FindAsync(id);
    if (task is null) return TypedResults.NotFound();

    task.Title = update.Title;
    task.Description = update.Description;
    task.DueDate = update.DueDate;
    task.Priority = update.Priority;
    task.IsCompleted = update.IsCompleted;
    if (task.IsCompleted) task.MarkAsCompleted();

    await db.SaveChangesAsync();
    return TypedResults.NoContent();
});

todos.MapDelete("/{id:guid}", [Authorize(Policy = "CanWriteTasks")] async Task<Results<NoContent, NotFound>> (Guid id, ApplicationDbContext db) =>
{
    var task = await db.ToDos.FindAsync(id);
    if (task is null) return TypedResults.NotFound();

    db.ToDos.Remove(task);
    await db.SaveChangesAsync();
    return TypedResults.NoContent();
});

// ===== Blazor root =====
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();

// (for WebApplicationFactory in tests)
public partial class Program { }
