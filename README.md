Cloud-Native Health Canary

A .NET 8 Worker Service that monitors web endpoints and infrastructure. It stores status history in PostgreSQL and uploads incident reports to S3 (via LocalStack) when failures are detected.
Technical Stack

    Worker: .NET 8

    Database: PostgreSQL 16 + Entity Framework Core

    Cloud Infrastructure (Emulated): LocalStack (S3)

    Orchestration: Docker Compose

Project Log: Phase 1
Worker & Database Setup

    Basic Loop: Initialized a worker service to ping a list of URLs using HttpClient.

    Persistence: Configured EF Core with the Npgsql provider.

    Auth Troubleshooting: Fixed a 28P01 authentication error. The issue was a mismatch between the .env variables and the container's initial state. Resolved by explicitly mapping environment variables in docker-compose.yml and resetting the persistent volume.

    Networking: Updated the connection string to use the Docker service name (health_db) instead of localhost to allow inter-container communication.

LocalStack Integration

    S3 Implementation: Added a FileStorageService using the AWS SDK.

    Incident Reporting: Configured the worker to generate a text-based report on every non-success HTTP status code.

    Connectivity: Pointed the S3 client to http://localhost:4566. Enabled ForcePathStyle in the AmazonS3Config—essential for LocalStack to route bucket requests correctly.

    Verification: Confirmed file uploads by exec-ing into the container and running awslocal s3 ls.

Setup
1. Environment

Create a .env file in the root directory:
Plaintext

DB_USER=postgres
DB_PASSWORD=your_password
DB_NAME=canary_db
AWS_S3_ENDPOINT=http://localstack:4566

2. Run
PowerShell

docker-compose up -d --build

3. Verification

    Check Logs: docker logs localstack-canary

    Check S3 (via Browser): http://localhost:4566/_localstack/health

    Check DB: Use any client to connect to localhost:5432 using the credentials in your .env.

Current Roadmap

    [Main] Core service with DB logging and S3 uploads.

    [Next] feature/terraform: Move S3 bucket provisioning out of the C# code and into Terraform.

    [Planned] feature/sqs: Add asynchronous alerting via SQS.

How to trigger an incident report

To verify the S3 upload logic works:

    Open appsettings.json.

    Add a non-existent URL (e.g., http://localhost:9999/broken).

    Restart the stack.

    Run docker exec -it localstack-canary awslocal s3 ls s3://canary-reports to see the generated report.
