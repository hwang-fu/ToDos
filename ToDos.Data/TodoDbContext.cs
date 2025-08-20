using Microsoft.EntityFrameworkCore;

namespace ToDos.Data;

class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
    {
    }

    public DbSet<TodoItem> ToDos => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(255).IsRequired();
            e.Property(t => t.Priority).HasDefaultValue(TodoPriority.Normal);
            e.HasIndex(t => new { t.IsCompleted, t.DueDate });
        });
    }
}