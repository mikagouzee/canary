using System.ComponentModel.DataAnnotations;

namespace Canary.Models;

public class Target
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string Url { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastCheckedAt { get; set; }
}
