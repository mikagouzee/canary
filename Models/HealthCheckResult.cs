namespace CloudNativeCanary.Models;

public class HealthCheckResult
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsUp { get; set; }
    public int StatusCode { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}