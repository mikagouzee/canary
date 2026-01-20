namespace CloudNativeCanary.Models;

public class CanaryConfig
{
    public int CheckIntervalSeconds { get; set; } = 60;
    public List<CanaryTarget> Targets { get; set; } = new();
}

public class CanaryTarget
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}