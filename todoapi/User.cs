using System.Text.Json.Serialization;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    [JsonIgnore]
    public ICollection<Todo> Todos { get; set; }
}
