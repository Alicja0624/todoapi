namespace todoapi
{
    public class TaskReq
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public bool IsComplete { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
