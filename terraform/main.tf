terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
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

resource "aws_s3_bucket" "reports" {
  bucket = "canary-reports"
}

output "s3_bucket_name" {
  value = aws_s3_bucket.reports.id
}