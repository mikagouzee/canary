namespace Canary.Services;

public interface IReportStorage
{
    Task<bool> IsHealthyAsync();
    Task UploadReportAsync(string name, string url);
}