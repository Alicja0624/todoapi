public class Todo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public bool IsComplete { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

}