
param(
    [string]$username,
    [string]$password,
    [string]$appname,
    [string]$filepath
)

$apiUrl = "https://$appname.scm.azurewebsites.net/api/zipdeploy"

echo "> $apiUrl, $username : $password"

$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $username, $password)))
$userAgent = "powershell/1.0"
Invoke-RestMethod -Uri $apiUrl -Headers @{Authorization=("Basic {0}" -f $base64AuthInfo); "content-type" = "multipart/form-data"} -UserAgent $userAgent -Method POST -InFile $filepath