terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    rabbitmq = { source = "cyrilgdn/rabbitmq" }
  }
}

provider "aws" {
  s3_use_path_style = true    #This forces Terraform to use http://localhost:4566/canary-reports instead of the DNS-heavy canary-reports.localhost, which is much more stable for local development.

  region                      = "us-east-1"
  access_key                  = "test"
  secret_key                  = "test"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

#hijack the AWS S3 endpoint to point to localstack
  endpoints {
    s3 = "http://localhost:4566"
  }
}

provider "rabbitmq" {
  endpoint = "http://localhost:15672"
  username = var.rabbitmq_user
  password = var.rabbitmq_password
}

resource "rabbitmq_queue" "health_reports" {
  name       = "canary.health.reports"
  settings {
    durable    = true
    auto_delete = false
  }  
}

resource "aws_s3_bucket" "reports" {
  bucket = "canary-reports"
}

output "s3_bucket_name" {
  value = aws_s3_bucket.reports.id
}