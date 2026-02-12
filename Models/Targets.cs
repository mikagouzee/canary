namespace Canary.Models;

public class Target
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastCheckedAt { get; set; }
}
