using Microsoft.EntityFrameworkCore;
using ToDos.Data;
using ToDos.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<TodoDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(conn);
});

builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

var todos = app.MapGroup("/api/todos");

todos.MapGet("/", async (TodoDbContext db) => 
    await db.ToDos.AsNoTracking()
        .OrderBy(t => t.IsCompleted)
        .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
        .ToListAsync());


app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
