using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Canary.Models;
using MassTransit;

namespace CloudNativeCanary.Services;

public class FileStorageService : IReportStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IPublishEndpoint _publishEndpoint; 
    private readonly IConfiguration _config;
    private const string BucketName = "canary-reports";
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration config, IPublishEndpoint publishEndpoint, ILogger<FileStorageService> logger){
        _config = config;
        _publishEndpoint = publishEndpoint;
        _logger = logger;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying S3 bucket existence.");
            return false;
        }
    }

    public async Task UploadReportAsync(string targetName, string url)
    {
        // 1. Create a simple report
        var fileName = $"failure-{targetName}-{DateTime.UtcNow:UnixEpochSeconds}.txt";
        var content = $"CRITICAL FAILURE\nTarget: {targetName}\nURL: {url}\nDetected At: {DateTime.UtcNow}";

       // 2. Upload to S3
        var request = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = fileName,
            ContentBody = content
        };

        await _s3Client.PutObjectAsync(request);

        // 3. Message Queue
        try
        {
            await _publishEndpoint.Publish(new HealthReportCreated
            {
                Id = Guid.NewGuid(),
                TargetUrl = url,
                IsSuccess = false,
                CheckedAt = DateTime.UtcNow,
                ReportS3Key = fileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "CRTICAL: Could not queue notification for {TargetUrl}. Fallback: Check S3 bucket {Bucket} manually.", url, BucketName);
            // Fallback: Continue to upload even if messaging fails
            // should send a mail or something
        }
    }

}