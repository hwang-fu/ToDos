namespace ToDos.Data;

public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    private bool IsCompleted { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;
    public TodoPriority Priority { get; set; } = TodoPriority.Normal;

    public void MarkAsCompleted()
    {
        if (!this.IsCompleted)
        {
            this.IsCompleted = true;
            this.CompletedDate = DateTimeOffset.UtcNow;
        }
    }
}