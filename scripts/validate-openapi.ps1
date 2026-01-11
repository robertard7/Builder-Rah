if (!(Test-Path "openapi.yaml")) {
  Write-Error "openapi.yaml missing"
  exit 1
}
Write-Host "openapi.yaml present"
