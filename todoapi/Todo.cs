namespace todoapi
{
    public class Todo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = "";
        public int Priority { get; set; } = 0;
        public bool IsComplete { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int? ParentTaskId { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }

    }
}