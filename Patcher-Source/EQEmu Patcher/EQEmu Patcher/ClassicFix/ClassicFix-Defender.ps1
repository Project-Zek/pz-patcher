param(
    [Parameter(Mandatory=$true)][string]$ClientRoot,
    [ValidateSet('Add','Remove')][string]$Action='Add',
    [switch]$Folder,
    [switch]$Process
)
$ErrorActionPreference='Stop'
$resultFile=$null
try {
    $client=[IO.Path]::GetFullPath($ClientRoot).TrimEnd('\')
    $drive=[IO.Path]::GetPathRoot($client).TrimEnd('\')
    if($client -eq $drive -or $client -eq $env:WINDIR -or $client.StartsWith($env:WINDIR+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Select the actual client folder, outside Windows and below the drive root.'}
    foreach($target in @($client,(Join-Path $client 'eqgame.exe'),(Join-Path $client 'ClassicFix'))) {
        if(Test-Path -LiteralPath $target) {
            $item=Get-Item -LiteralPath $target -Force
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'Linked client paths are not supported.'}
        }
    }
    $game=Join-Path $client 'eqgame.exe'
    if(-not(Test-Path -LiteralPath $game -PathType Leaf)) {throw 'eqgame.exe is missing from the selected folder.'}
    # This helper is restricted to the same audited TAKPv22 executable as the launcher.
    $hash=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($game)
    try {$gameHash=([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-','')} finally {$stream.Dispose();$hash.Dispose()}
    if($gameHash -ne '41585AFE85F419D57D6FF8A43681CBF7B030A227B8BA0C76E8F3A276D2B81012') {throw 'This is not the supported TAKPv22 game executable. No Defender settings changed.'}
    $support=Join-Path $client 'ClassicFix';[void](New-Item -ItemType Directory -Force -Path $support)
    $resultFile=Join-Path $support 'defender-result.txt'
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $principal=New-Object Security.Principal.WindowsPrincipal($identity)
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {throw 'Windows administrator approval is required for this optional action.'}
    if(-not(Get-Command Get-MpPreference -ErrorAction SilentlyContinue)) {
        # A parent PowerShell 7 process can supply a different module search path.
        $defenderModule=Join-Path $PSHOME 'Modules\Defender\Defender.psd1'
        if(Test-Path -LiteralPath $defenderModule) {Import-Module -Name $defenderModule -ErrorAction Stop}
    }
    if(-not(Get-Command Get-MpPreference -ErrorAction SilentlyContinue)) {throw 'Microsoft Defender is unavailable on this Windows installation.'}
    $stateFile=Join-Path $support 'defender-owned.json'
    if(Test-Path -LiteralPath $stateFile) {$owned=Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json} else {$owned=[pscustomobject]@{client=$client;folder=$false;process=$false}}
    if($owned.client -ne $client) {throw 'The exclusion record belongs to another client folder.'}
    $messages=New-Object Collections.Generic.List[string]
    if($Action -eq 'Add') {
        if(-not $Folder -and -not $Process) {throw 'Select at least one optional exclusion.'}
        $prefs=Get-MpPreference
        if($Folder) {
            if(@($prefs.ExclusionPath) -contains $client) {$messages.Add('Folder exclusion already existed; preserved.')} else {
                Add-MpPreference -ExclusionPath $client
                $owned.folder=$true
                [IO.File]::WriteAllText($stateFile,($owned | ConvertTo-Json),(New-Object Text.UTF8Encoding $false))
                $messages.Add('Added folder exclusion: '+$client)
            }
        }
        if($Process) {
            if(@($prefs.ExclusionProcess) -contains $game) {$messages.Add('Process exclusion already existed; preserved.')} else {
                Add-MpPreference -ExclusionProcess $game
                $owned.process=$true
                [IO.File]::WriteAllText($stateFile,($owned | ConvertTo-Json),(New-Object Text.UTF8Encoding $false))
                $messages.Add('Added process exclusion: '+$game)
            }
        }
        $verified=Get-MpPreference
        if(($Folder -and @($verified.ExclusionPath) -notcontains $client) -or ($Process -and @($verified.ExclusionProcess) -notcontains $game)) {throw 'Windows policy prevented verifying one or more requested exclusions. Check Defender settings; the record is retained for removal.'}
    } else {
        # Remove only these exact entries when this helper recorded adding them.
        # Never replace the exclusion lists or remove unrelated/pre-existing exclusions.
        if($owned.folder) {Remove-MpPreference -ExclusionPath $client;$owned.folder=$false;$messages.Add('Removed tool-added folder exclusion.')}
        if($owned.process) {Remove-MpPreference -ExclusionProcess $game;$owned.process=$false;$messages.Add('Removed tool-added process exclusion.')}
        if($messages.Count -eq 0) {$messages.Add('No tool-added exclusions to remove.')}
        [IO.File]::WriteAllText($stateFile,($owned | ConvertTo-Json),(New-Object Text.UTF8Encoding $false))
    }
    [IO.File]::WriteAllText($resultFile,($messages -join [Environment]::NewLine),(New-Object Text.UTF8Encoding $false))
    exit 0
} catch {
    if($resultFile) {[IO.File]::WriteAllText($resultFile,$_.Exception.Message,(New-Object Text.UTF8Encoding $false))}
    Write-Error $_
    exit 1
}
