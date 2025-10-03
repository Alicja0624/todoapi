namespace todoapi
{
    public class TaskReq
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public bool IsComplete { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int? ParentTaskId { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
