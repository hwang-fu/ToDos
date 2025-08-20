using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.HttpResults;
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

builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>(); // resolved inside circuit scope
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) }; // e.g., https://localhost:7142/
});

builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    //app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // app.UseHsts();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseAntiforgery();

var todos = app.MapGroup("/api/todos");

// GET /api/todos
todos.MapGet("/", async (TodoDbContext db) => 
    await db.ToDos.AsNoTracking()
        .OrderBy(t => t.IsCompleted)
        .ThenBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
        .ToListAsync());

// GET /api/todos/{id}
todos.MapGet("/{id:guid}", async Task<Results<Ok<TodoItem>, NotFound>> (Guid id, TodoDbContext db) =>
{
    var todo = await db.ToDos.FindAsync(id);
    return todo is null ? TypedResults.NotFound() : TypedResults.Ok(todo);
});

// POST /api/todos
todos.MapPost("/", async Task<Created<TodoItem>> (TodoItem item, TodoDbContext db) =>
{
    item.Id = Guid.NewGuid();
    item.CreatedDate = DateTimeOffset.UtcNow;
    db.ToDos.Add(item);
    await db.SaveChangesAsync();
    return TypedResults.Created<TodoItem>($"/api/todos/{item.Id}", item);
});

// PUT /api/todos/{id}
todos.MapPut("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, TodoItem item, TodoDbContext db) =>
{
    var todo = await db.ToDos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    todo.Title = item.Title;
    todo.Description = item.Description;
    todo.DueDate = item.DueDate;
    todo.Priority = item.Priority;
    todo.IsCompleted = item.IsCompleted;
    todo.UpdatedDate = DateTimeOffset.UtcNow;
    if (todo.IsCompleted)
    {
        todo.MarkAsCompleted();
    }
    
    await db.SaveChangesAsync();
    return TypedResults.NoContent();

});

// DELETE /api/todos/{id}
todos.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, TodoDbContext db) =>
{
    var todo = await db.ToDos.FindAsync(id);
    if (todo is null)
    {
        return TypedResults.NotFound();
    }

    db.ToDos.Remove(todo);
    await db.SaveChangesAsync();
    return TypedResults.NoContent();
});


app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
