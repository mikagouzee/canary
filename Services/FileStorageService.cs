using Amazon.S3;
using Amazon.S3.Model;

namespace CloudNativeCanary.Services;

public class FileStorageService : IReportStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private const string BucketName = "canary-reports";

    public FileStorageService(IConfiguration config)
    {
        _config = config;

        // Pull values from .env (mapped via Docker Compose)
        var accessKey = _config["AWS_ACCESS_KEY_ID"] ?? "test";
        var secretKey = _config["AWS_SECRET_ACCESS_KEY"] ?? "test";
        var serviceUrl = _config["AWS_S3_ENDPOINT"] ?? "http://localhost:4566";

        // Configuration for LocalStack
        var s3Config = new AmazonS3Config
        {
            // Inside Docker, we use the service name 'localstack'
            // Outside Docker (F5), we would use 'localhost'
            ServiceURL = serviceUrl,
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            return await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, BucketName);
        }
        catch
        {
            _logger.LogError(ex, "Error verifying S3 bucket existence.");
            return false;
        }
    }

    public async Task UploadReportAsync(string targetName, string url)
    {
        // 1. Ensure the bucket exists
        //try { await _s3Client.PutBucketAsync(BucketName); } catch { /* Ignore if exists */ }

        // 2. Create a simple report
        var fileName = $"failure-{targetName}-{DateTime.UtcNow:UnixEpochSeconds}.txt";
        var content = $"CRITICAL FAILURE\nTarget: {targetName}\nURL: {url}\nDetected At: {DateTime.UtcNow}";

        // 3. Upload to S3
        var request = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = fileName,
            ContentBody = content
        };

        await _s3Client.PutObjectAsync(request);
    }
}