$ErrorActionPreference = 'Stop'

$cert = New-SelfSignedCertificate `
  -Type CodeSigning `
  -Subject 'CN=596D3380-CDB9-4F95-8A87-41D57AB91BFE' `
  -KeyUsage DigitalSignature `
  -FriendlyName 'AutoCenter GitHub Release signing key' `
  -CertStoreLocation 'Cert:\CurrentUser\My' `
  -NotAfter (Get-Date).AddYears(10) `
  -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

Write-Host "Generated cert thumbprint: $($cert.Thumbprint)"

$pfxPath = Join-Path $PSScriptRoot 'AutoCenter_TemporaryKey.pfx'
# Empty password — this is a self-signed dev cert committed to the public repo,
# so a password adds no security (the secret would have to be committed too).
$pwd = New-Object System.Security.SecureString
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $pfxPath -Password $pwd | Out-Null
Write-Host "Exported $pfxPath"

$cerPath = Join-Path $PSScriptRoot 'AutoCenter_TemporaryKey.cer'
Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cerPath -Type CERT | Out-Null
Write-Host "Exported $cerPath (public)"

Write-Host ""
Write-Host "Add to AutoCenter.csproj GitHub Release PropertyGroup:"
Write-Host "  <PackageCertificateKeyFile>AutoCenter_TemporaryKey.pfx</PackageCertificateKeyFile>"
Write-Host "  <PackageCertificateThumbprint>$($cert.Thumbprint)</PackageCertificateThumbprint>"
