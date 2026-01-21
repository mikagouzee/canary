namespace Canary.Models;

public record HealthReportCreated
{
    public Guid Id { get; init; }
    public required string TargetUrl { get; init; }
    public bool IsSuccess { get; init; }
    public DateTime CheckedAt { get; init; }
    public required string ReportS3Key { get; init; }
}