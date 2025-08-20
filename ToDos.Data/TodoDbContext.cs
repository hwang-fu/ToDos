using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ToDos.Data;

public class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
    {
    }

    public DbSet<TodoItem> ToDos => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dtoToLong = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        var nullableDtoToLong = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null
        );
        modelBuilder.Entity<TodoItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(255).IsRequired();
            e.Property(t => t.Priority).HasDefaultValue(TodoPriority.Normal);

            e.Property(t => t.DueDate).HasConversion(nullableDtoToLong);
            e.Property(t => t.CompletedDate).HasConversion(nullableDtoToLong);
            e.Property(t => t.CreatedDate).HasConversion(dtoToLong);
            e.Property(t => t.UpdatedDate).HasConversion(dtoToLong);
            
            e.HasIndex(t => new { t.IsCompleted, t.DueDate });
        });
    }
}