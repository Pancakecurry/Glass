[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$')]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [uri] $ReleaseBaseUri,

    [Parameter(Mandatory = $true)]
    [string] $Publisher,

    [Parameter(Mandatory = $true)]
    [string] $BundleFileName,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$templatePath = Join-Path $PSScriptRoot '..\src\Glass.App\Distribution\Glass.appinstaller.template'
$baseUri = $ReleaseBaseUri.AbsoluteUri.TrimEnd('/')
$content = [IO.File]::ReadAllText($templatePath)
$content = $content.Replace('{{VERSION}}', $Version)
$content = $content.Replace('{{PUBLISHER}}', [Security.SecurityElement]::Escape($Publisher))
$content = $content.Replace('{{APPINSTALLER_URI}}', "$baseUri/Glass.appinstaller")
$content = $content.Replace('{{BUNDLE_URI}}', "$baseUri/$BundleFileName")
[IO.File]::WriteAllText($OutputPath, $content, [Text.UTF8Encoding]::new($false))

Write-Host "Generated $OutputPath for $BundleFileName."
