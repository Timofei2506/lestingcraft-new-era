# Windows cloud runner only. Hub is retained for licensing, not Editor installation.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $env:GITHUB_ACTIONS -ne 'true') {
    throw 'This script installs Unity only on a GitHub-hosted Windows runner.'
}
$versionText = Get-Content (Join-Path $env:GITHUB_WORKSPACE 'ProjectSettings/ProjectVersion.txt') -Raw
$version = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)').Groups[1].Value
$revision = [regex]::Match($versionText, 'm_EditorVersionWithRevision:[^\r\n]*\(([0-9a-f]+)\)').Groups[1].Value
if (-not $version -or -not $revision) { throw 'Pinned Unity version/revision missing.' }
$root = Join-Path $env:RUNNER_TEMP 'UnityEditors'
$destination = Join-Path $root $version
$editorExe = Join-Path $destination 'Editor/Unity.exe'
$playerSupport = Join-Path $destination 'Editor/Data/PlaybackEngines/windowsstandalonesupport'
if (-not (Test-Path $editorExe)) {
    $drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot($root).Substring(0,1))
    Write-Host ('Free runner space: {0:N1} GiB' -f ($drive.Free / 1GB))
    if ($drive.Free -lt 14GB) { throw 'Insufficient runner disk space for the download and Editor.' }
    $releaseUrl = "https://services.api.unity.com/unity/editor/release/v1/releases?version=$version&limit=1"
    $response = Invoke-RestMethod -Uri $releaseUrl -TimeoutSec 60
    $release = $response.results | Where-Object version -eq $version | Select-Object -First 1
    $download = $release.downloads | Where-Object { $_.platform -eq 'WINDOWS' -and $_.architecture -eq 'X86_64' } | Select-Object -First 1
    if (-not $download) { throw 'Official Windows x64 Editor download missing.' }
    $uri = [uri]$download.url
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'download.unity3d.com' -or $uri.AbsolutePath -notlike "/download_unity/$revision/*") {
        throw 'Download does not match the pinned official Unity revision.'
    }
    $installer = Join-Path $env:RUNNER_TEMP "UnitySetup64-$version.exe"
    Write-Host "Downloading official Unity $version ($revision), not optional IL2CPP/Android/WebGL modules."
    $headersFile = Join-Path $env:RUNNER_TEMP "UnitySetup64-$version.headers"
    & curl.exe --dump-header $headersFile --fail --location --retry 3 --retry-all-errors --connect-timeout 30 --max-time 600 --output $installer $uri.AbsoluteUri
    if ($LASTEXITCODE -ne 0) { throw 'Unity Editor download failed.' }
    $actualLength = (Get-Item $installer).Length
    # Release API rounds this 4 GB size (272 bytes short for 6000.0.32f1).
    # Use the final CDN response Content-Length, then exact digest + Authenticode.
    $lengthHeaders = [regex]::Matches((Get-Content $headersFile -Raw), '(?im)^Content-Length:\s*(\d+)\s*$')
    if ($lengthHeaders.Count -eq 0) { throw 'CDN Content-Length is missing.' }
    $expectedLength = [long]$lengthHeaders[$lengthHeaders.Count-1].Groups[1].Value
    Write-Host "Installer bytes: $actualLength; CDN: $expectedLength; release metadata (approximate): $($download.downloadSize.value)"
    if ($actualLength -ne $expectedLength) { throw 'Incomplete Unity installer transfer.' }
    if (-not $download.integrity.StartsWith('md5-')) { throw 'Expected checksum metadata missing.' }
    $digestBytes = [Convert]::FromBase64String($download.integrity.Substring(4))
    $expected = [Text.Encoding]::UTF8.GetString($digestBytes)
    if ($expected -notmatch '^[a-fA-F0-9]{32}$') { $expected = [Convert]::ToHexString($digestBytes) }
    $actual = (Get-FileHash -Path $installer -Algorithm MD5).Hash
    if ($actual -ne $expected) { throw 'Unity installer checksum mismatch.' }
    # Metadata checksum checks transport; Authenticode verifies publisher/trust as well.
    $signature = Get-AuthenticodeSignature -FilePath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') {
        throw "Unity installer signature is not trusted: $($signature.Status)"
    }
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    Write-Host 'Installing Editor directly (15 minute limit, no Hub install-state polling).'
    # NSIS /D must be the final argument; this runner path contains no spaces.
    if ($destination -match '\s') { throw 'NSIS destination must not contain spaces on this runner.' }
    $process = Start-Process -FilePath $installer -ArgumentList "/S /D=$destination" -PassThru
    $null = $process.Handle
    if (-not $process.WaitForExit(900000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw 'Direct Unity installer timed out.'
    }
    $process.Refresh()
    if ($process.ExitCode -notin @(0,3010)) { throw "Unity installer exit code $($process.ExitCode)" }
    Remove-Item $installer -Force
}
if (-not (Test-Path $editorExe) -or -not (Test-Path $playerSupport)) {
    throw 'Unity.exe or built-in Windows Mono player support is missing after installation.'
}
# unity-action uses these paths directly; no Hub GetEditor/install fallback is needed.
"UNITY_EDITOR_PATH=$editorExe" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
"UNITY_PROJECT_PATH=$env:GITHUB_WORKSPACE" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
$editors = ConvertTo-Json -InputObject @(@{version=$version;path=$editorExe}) -Compress
"UNITY_EDITORS=$editors" | Out-File $env:GITHUB_ENV -Append -Encoding utf8
Write-Host "Ready: $editorExe (Windows Mono player support verified)."
