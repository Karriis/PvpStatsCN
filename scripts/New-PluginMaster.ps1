param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$Repository
)

$ErrorActionPreference = 'Stop'

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$entry = [ordered]@{}
foreach($property in $manifest.PSObject.Properties) {
    $entry[$property.Name] = $property.Value
}

$releaseBaseUrl = "https://github.com/$Repository/releases/latest/download"
$entry['IsHide'] = $false
$entry['IsTestingExclusive'] = $false
$entry['DownloadLinkInstall'] = "$releaseBaseUrl/latest.zip"
$entry['DownloadLinkUpdate'] = "$releaseBaseUrl/latest.zip"
$entry['LastUpdate'] = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()

@($entry) | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
