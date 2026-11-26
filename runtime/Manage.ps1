param([ValidateSet('Install','TestTerms','Restore','Collect')][string]$Action='Install',[string]$GamePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
function Digest([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant() }
if ([string]::IsNullOrWhiteSpace($GamePath)) {
 $GamePath=Read-Host 'Game folder (Enter = E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2)'
 if ([string]::IsNullOrWhiteSpace($GamePath)) {$GamePath='E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2'}
}
$GamePath=$GamePath.Trim().Trim('"')
if (!(Test-Path -LiteralPath $GamePath -PathType Container)) {throw 'Game folder not found.'}
$GamePath=(Resolve-Path -LiteralPath $GamePath).Path
$plugin=Join-Path $GamePath 'BepInEx/plugins/SotM2ChinesePrototype'
$backup=Join-Path $plugin 'semantic-runtime-backup'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$names=@('SotM2ChinesePrototype.dll','sample-texts.json')
if ($Action -eq 'Collect') {
 $log=Join-Path $GamePath 'BepInEx/LogOutput.log'
 if (!(Test-Path -LiteralPath $log)) {throw 'LogOutput.log not found.'}
 $out=Join-Path $PSScriptRoot ('SotM2-Semantic-Evidence-'+(Get-Date).ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
 New-Item -ItemType Directory -Path $out | Out-Null
 Copy-Item -LiteralPath $log -Destination $out
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $out
 $state=@{};foreach($n in $names) {$p=Join-Path $plugin $n;if(Test-Path -LiteralPath $p){$state[$n]=Digest $p}}
 $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'installed-hashes.json') -Encoding UTF8
 Compress-Archive -LiteralPath $out -DestinationPath ($out+'.zip')
 Write-Host ('Evidence: '+$out+'.zip');exit 0
}
if (Get-Process -Name 'Sentinels2','Sentinels of the Multiverse 2' -ErrorAction SilentlyContinue) {throw 'Close the game first.'}
foreach($n in $names) {if(!(Test-Path -LiteralPath (Join-Path $plugin $n))) {throw ('Existing installation required: '+$n)}}
$hasBackup=Test-Path -LiteralPath $backup
if($hasBackup) {
 $state=Get-Content -LiteralPath (Join-Path $backup 'original-hashes.json') -Raw | ConvertFrom-Json
 foreach($n in $names) {if((Digest (Join-Path $backup $n)) -ne $state.$n) {throw ('Backup hash mismatch: '+$n)}}
}
if($Action -eq 'Restore') {
 if(!$hasBackup) {throw 'No semantic runtime backup found.'}
 foreach($n in $names) {
  $valid=@($state.$n)
  if($n -eq 'sample-texts.json') {$valid+=@($manifest.catalogSha256,$manifest.termCatalogSha256)} else {$valid+=$manifest.dllSha256}
  if((Digest (Join-Path $plugin $n)) -notin $valid) {throw ('File changed after install; refusing to overwrite: '+$n)}
 }
 foreach($n in $names) {Copy-Item -LiteralPath (Join-Path $backup $n) -Destination (Join-Path $plugin $n) -Force}
 foreach($n in $names) {if((Digest (Join-Path $plugin $n)) -ne $state.$n) {throw 'Restore verification failed; backup retained.'}}
 Remove-Item -LiteralPath $backup -Recurse -Force
 Write-Host 'Restored the exact version present before this kit was installed.';exit 0
}
foreach($file in $manifest.gameFiles.PSObject.Properties) {
 $path=Join-Path $GamePath $file.Name
 if(!(Test-Path -LiteralPath $path) -or (Digest $path) -ne $file.Value) {throw ('Game build mismatch: '+$file.Name)}
}
$currentDll=Digest (Join-Path $plugin $names[0]);$currentText=Digest (Join-Path $plugin $names[1])
if($hasBackup) {
 if($currentDll -ne $manifest.dllSha256 -or $currentText -notin @($manifest.catalogSha256,$manifest.termCatalogSha256)) {throw 'Installed files changed; no overwrite.'}
} else {
 if($currentDll -notin $manifest.acceptedDlls -or $currentText -notin $manifest.acceptedCatalogs) {throw 'Unknown installed DLL/catalog. No files changed; retain this version for comparison.'}
}
$payloads=@{'SotM2ChinesePrototype.dll'='payload/SotM2ChinesePrototype.dll';'sample-texts.json'='payload/sample-texts.json'}
$expected=@{'SotM2ChinesePrototype.dll'=$manifest.dllSha256;'sample-texts.json'=$manifest.catalogSha256}
if($Action -eq 'TestTerms') {$payloads['sample-texts.json']='optional-term-test/sample-texts.json';$expected['sample-texts.json']=$manifest.termCatalogSha256}
foreach($n in $names) {if((Digest (Join-Path $PSScriptRoot $payloads[$n])) -ne $expected[$n]) {throw ('Package hash mismatch: '+$n)}}
if(!$hasBackup) {
 New-Item -ItemType Directory -Path $backup | Out-Null
 $state=@{}
 foreach($n in $names) {Copy-Item -LiteralPath (Join-Path $plugin $n) -Destination (Join-Path $backup $n);$state[$n]=Digest (Join-Path $backup $n)}
 $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'original-hashes.json') -Encoding UTF8
}
$previous=@{};foreach($n in $names) {$previous[$n]=[IO.File]::ReadAllBytes((Join-Path $plugin $n))}
try {
 foreach($n in $names) {Copy-Item -LiteralPath (Join-Path $PSScriptRoot $payloads[$n]) -Destination (Join-Path $plugin $n) -Force}
 foreach($n in $names) {if((Digest (Join-Path $plugin $n)) -ne $expected[$n]) {throw 'Copy verification failed.'}}
} catch {foreach($n in $names) {[IO.File]::WriteAllBytes((Join-Path $plugin $n),$previous[$n])};throw}
Write-Host ('0.1.6-rc.1 installed: '+$Action+'. Existing font, configuration and older probe backup retained.')
