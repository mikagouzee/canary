# Start the infrastructure
kubectl apply -f ./Manifests/postgres.yaml, ./Manifests/rabbitmq.yaml, ./Manifests/localstack.yaml, ./Manifests/canary.yaml

# Open the tunnel in the background
# This way localstack S3 endpoint is available at localhost:4566 so the dashboard is fully functional
Write-Host "Opening S3 Tunnel to localhost:4566..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "kubectl port-forward service/localstack-service 4566:4566" -WindowStyle Minimized