Cloud-Native Health Canary (Infrastructure Branch)

This branch focuses on separating infrastructure provisioning from application logic. The creation of cloud resources (S3 Buckets) has been moved out of the C# codebase and into Terraform.
🏗️ Architectural Change: Provisioning vs. Execution

In the previous version, the .NET Worker was responsible for creating the S3 bucket if it didn't exist. This is problematic in production due to permission constraints (Least Privilege) and configuration drift.

Current Workflow:

    Terraform: Defines and creates the required S3 buckets in LocalStack.

    .NET Worker: Performs a "Fail-Fast" check on startup to verify the bucket exists. If the infrastructure is missing, the app logs a critical error and exits.

🛠️ Infrastructure as Code (IaC)

The /terraform directory contains the HCL (HashiCorp Configuration Language) files to manage the local environment.

    Provider Config: Configured to "hijack" standard AWS calls and redirect them to http://localhost:4566.

    Resource Definition: Defines the canary-reports bucket with standard naming conventions.

    State Management: Uses a local .tfstate file to track resource status. (Note: .tfstate is excluded from Git to prevent state conflicts and data leakage).

💻 C# Refactoring

    Interface Abstraction: Introduced IReportStorage to decouple the worker from the AWS SDK. This allows for easier unit testing and future-proofs the app against changes in storage providers.

    Startup Probe: Implemented a check in StartAsync using AmazonS3Util.DoesS3BucketExistV2Async.

    Resilience: The app now handles missing infrastructure by failing immediately during the bootstrap phase rather than crashing during an active incident.

🚀 How to Run (New Order of Operations)

Because the app no longer creates its own resources, you must follow this sequence:
1. Start the Environment
PowerShell

docker-compose up -d localstack health_db

2. Provision Infrastructure

Navigate to the /terraform folder:
PowerShell

terraform init
terraform apply -auto-approve

3. Start the Worker
PowerShell

docker-compose up -d app

🧪 Verification

To verify the "Fail-Fast" logic:

    Run terraform destroy.

    Attempt to start the app container: docker-compose up app.

    Check logs: docker logs canary-worker. You should see: CRITICAL: Storage service is not responding or bucket is missing!

What's next?

Next step is to introduce Asynchronous Messaging. I'll be adding a RabbitMQ broker to the stack to decouple the "Health Check" logic from the "Alerting" logic.
