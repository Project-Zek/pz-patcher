param(
    [string]$Version='1.0.17.0',
    [string]$FileName='Project-Zek-Patcher',
    [string]$ManifestUrl,
    [string]$OutputDirectory,
    [string]$MSBuild,
    [switch]$Online
)
$ErrorActionPreference='Stop'
function Invoke-NativeBuild([string]$Executable, [string[]]$Arguments, [string]$Log) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Executable
    $quoted = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            '"' + [regex]::Replace([regex]::Replace($argument,'(\\*)"','$1$1\"'),'(\\+)$','$1$1') + '"'
        } else { $argument }
    }
    $info.Arguments = $quoted -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # Some hosts supply both Path and PATH. MSBuild's .NET Framework tool runner
    # rejects these duplicate keys; normalize the child environment without changing the parent.
    $info.EnvironmentVariables.Clear()
    $environment = [Environment]::GetEnvironmentVariables()
    foreach ($key in $environment.Keys) { $info.EnvironmentVariables[$key.ToString().ToUpperInvariant()] = $environment[$key].ToString() }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    [IO.File]::WriteAllText($Log,($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()),(New-Object Text.UTF8Encoding $false))
    $code = $process.ExitCode
    $process.Dispose()
    return $code
}
try {
    if($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {throw 'Use a four-part numeric assembly version.'}
    if($FileName -notmatch '^[A-Za-z0-9_-]{1,64}$') {throw 'Use a simple executable basename without slash or extension.'}
    if($ManifestUrl) {
        $endpoint=$null
        if(-not [Uri]::TryCreate($ManifestUrl,[UriKind]::Absolute,[ref]$endpoint) -or $endpoint.Scheme -ne 'https' -or $endpoint.AbsolutePath -notmatch '\.ya?ml$' -or $endpoint.UserInfo) {throw 'Use a direct absolute HTTPS .yml/.yaml manifest URL without credentials.'}
        if($Online -and ($endpoint.Host.EndsWith('.invalid') -or $endpoint.Host.EndsWith('.example'))) {throw 'An online build requires your real published HTTPS manifest URL.'}
    }
    if(-not $MSBuild) {
        $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if(Test-Path -LiteralPath $vswhere) {$MSBuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1}
    }
    if(-not $MSBuild -or -not(Test-Path -LiteralPath $MSBuild)) {throw 'VS2022 MSBuild was not found; supply -MSBuild.'}
    if(-not $OutputDirectory) {$OutputDirectory=Join-Path $PSScriptRoot ('dist\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))}
    $release=[IO.Path]::GetFullPath($OutputDirectory)
    if(Test-Path -LiteralPath $release) {throw 'Choose a new output directory.'}
    [void](New-Item -ItemType Directory -Path $release)
    $solution=Join-Path $PSScriptRoot 'EQEmu Patcher\EQEmu Patcher.sln'
    $cachedPackages=Join-Path (Split-Path $PSScriptRoot -Parent) 'Dependency-Cache\packages'
    if(Test-Path -LiteralPath $cachedPackages) {
        $packages=Join-Path $PSScriptRoot 'EQEmu Patcher\packages'
        if(-not(Test-Path -LiteralPath $packages)) {[void](New-Item -ItemType Directory -Path $packages)}
        Get-ChildItem -LiteralPath $cachedPackages -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $packages -Recurse -Force
        }
        [IO.File]::WriteAllText((Join-Path $release 'restore.log'),'Using bundled pinned dependencies, including .NET Framework reference assemblies.')
    } else {
        $code=Invoke-NativeBuild $MSBuild @($solution,'/t:Restore','/p:RestorePackagesConfig=true','/nologo','/v:minimal') (Join-Path $release 'restore.log')
        if($code) {throw 'Restore failed; read restore.log.'}
    }
    $arguments=@($solution,'/t:Rebuild','/p:Configuration=Release','/p:Platform=Any CPU',"/p:VERSION=$Version","/p:FILE_NAME=$FileName",'/nologo','/v:minimal')
    if($ManifestUrl) {$arguments+="/p:FILELIST_URL=$ManifestUrl"}
    if($Online) {$arguments+='/p:OnlinePatcher=true'}
    $code=Invoke-NativeBuild $MSBuild $arguments (Join-Path $release 'build.log')
    if($code) {throw 'Build failed; read build.log.'}
    $binary=Join-Path $PSScriptRoot 'EQEmu Patcher\EQEmu Patcher\bin\Release\Project-Zek-Patcher.exe'
    Copy-Item -LiteralPath $binary -Destination (Join-Path $release ($FileName+'.exe'))
    Copy-Item -LiteralPath ($binary+'.config') -Destination (Join-Path $release ($FileName+'.exe.config'))
    $mode=if($Online){'Online'}else{'Local-files-only'}
    Write-Output ($mode+' build complete: '+$release)
} catch {Write-Error $_;exit 1}
