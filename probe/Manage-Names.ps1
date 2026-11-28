param([ValidateSet('CleanInstall','Verify','Restore','Collect')][string]$Action='Verify',[string]$GamePath,[string]$ArchivePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
function Digest([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function WriteJson([string]$p,$value){[IO.File]::WriteAllText($p,($value | ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))}
function SafePath([string]$root,[string]$relative){
 if([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)' -or $relative.Contains(':')){throw 'Unsafe relative path.'}
 $full=[IO.Path]::GetFullPath((Join-Path $root $relative))
 if(!$full.StartsWith($root.TrimEnd([char[]]@('/','\'))+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped root.'}
 return $full
}
function AssertNoLinks([string]$p){
 if(Test-Path -LiteralPath $p){
  foreach($e in @((Get-Item -LiteralPath $p -Force))+@(Get-ChildItem -LiteralPath $p -Recurse -Force -ErrorAction Stop)){
   if(($e.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw ('Linked path requires review: '+$e.FullName)}
  }
 }
}
if([string]::IsNullOrWhiteSpace($GamePath)){
 $GamePath=Read-Host 'Game folder (Enter = E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2)'
 if([string]::IsNullOrWhiteSpace($GamePath)){$GamePath='E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2'}
}
$GamePath=$GamePath.Trim().Trim('"')
if(!(Test-Path -LiteralPath $GamePath -PathType Container)){throw 'Game folder not found.'}
$GamePath=(Resolve-Path -LiteralPath $GamePath).Path
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$pluginRelative='BepInEx/plugins/SotM2ChinesePrototype'
$plugin=SafePath $GamePath $pluginRelative
$configRelative='BepInEx/config/sotm2.zh.prototype.cfg'
$stateRelative='SotM2ChineseCleanState.json'
$statePath=SafePath $GamePath $stateRelative
$archiveRoot=SafePath $GamePath 'SotM2ChineseArchives'
$names=@('SotM2ChinesePrototype.dll','surface-probe.json','sample-texts.json','NotoSansCJKsc-Regular.otf','OFL.txt')
if((($manifest.pluginFiles.PSObject.Properties.Name | Sort-Object) -join '|') -ne (($names | Sort-Object) -join '|')){throw 'Unexpected package file list.'}
function ChineseConfig([string]$p){
 if(!(Test-Path -LiteralPath $p -PathType Leaf)){return $false}
 $lines=@(Get-Content -LiteralPath $p | ForEach-Object {$_.Trim()} | Where-Object {$_ -and !$_.StartsWith('#') -and !$_.StartsWith(';')})
 return ($lines -join '|') -eq '[Prototype]|EnableChinese = true'
}
function AssertClosed {
 if(Get-Process -Name 'Sentinels2','Sentinels of the Multiverse 2' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
}
function AssertBuild {
 foreach($f in $manifest.gameFiles.PSObject.Properties){
  $p=SafePath $GamePath $f.Name
  if(!(Test-Path -LiteralPath $p -PathType Leaf) -or (Digest $p) -ne $f.Value){throw ('Game build mismatch: '+$f.Name)}
 }
 foreach($f in $manifest.runtimeFiles.PSObject.Properties){
  $p=SafePath $GamePath $f.Name
  if(!(Test-Path -LiteralPath $p -PathType Leaf) -or (Digest $p) -ne $f.Value){throw ('Base runtime mismatch: '+$f.Name+'. No files changed.')}
 }
}
function AssertPayload {
 foreach($n in $names){if((Digest (SafePath $PSScriptRoot ('payload/'+$n))) -ne $manifest.pluginFiles.$n){throw ('Package hash mismatch: '+$n)}}
 if((Digest (SafePath $PSScriptRoot 'payload/sotm2.zh.prototype.cfg')) -ne $manifest.configSha256){throw 'Package config mismatch.'}
}
function OwnDlls {
 $dir=SafePath $GamePath 'BepInEx/plugins'
 if(!(Test-Path -LiteralPath $dir)){return}
 foreach($f in Get-ChildItem -LiteralPath $dir -Filter '*.dll' -Recurse -File -Force){
  $own=$f.Name -like 'SotM2ChinesePrototype*.dll'
  if(!$own){try{$own=[Reflection.AssemblyName]::GetAssemblyName($f.FullName).Name -eq 'SotM2ChinesePrototype'}catch{}}
  if($own){$f.FullName}
 }
}
function VerifyInstalled {
 foreach($n in $names){$p=SafePath $plugin $n;if(!(Test-Path -LiteralPath $p) -or (Digest $p) -ne $manifest.pluginFiles.$n){throw ('Installed file mismatch: '+$n)}}
 $files=@(Get-ChildItem -LiteralPath $plugin -Recurse -File -Force)
 if($files.Count -ne $names.Count){throw 'Unexpected files in managed plugin folder.'}
 if(!(ChineseConfig (SafePath $GamePath $configRelative))){throw 'Installed config mismatch.'}
 $dlls=@(OwnDlls)
 if($dlls.Count -ne 1 -or $dlls[0] -ne (SafePath $plugin 'SotM2ChinesePrototype.dll')){throw 'Duplicate or misplaced project DLL.'}
}
if($Action -eq 'Collect'){
 $out=Join-Path $PSScriptRoot ('SotM2-Dynamic-Evidence-'+(Get-Date).ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
 New-Item -ItemType Directory -Path $out | Out-Null
 Copy-Item -LiteralPath (SafePath $GamePath 'BepInEx/LogOutput.log') -Destination $out
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $out
 $installed=@{};foreach($n in $names){$p=SafePath $plugin $n;if(Test-Path -LiteralPath $p){$installed[$n]=Digest $p}}
 WriteJson (Join-Path $out 'installed-hashes.json') $installed
 $report=@{projectDlls=@(OwnDlls);pureStateVerified=$false;runtimeFiles=@{}}
 foreach($f in $manifest.runtimeFiles.PSObject.Properties){$p=SafePath $GamePath $f.Name;if(Test-Path -LiteralPath $p){$report.runtimeFiles[$f.Name]=Digest $p}else{$report.runtimeFiles[$f.Name]='ABSENT'}}
 try{VerifyInstalled;$report.pureStateVerified=$true}catch{$report.verificationError=$_.Exception.Message}
 WriteJson (Join-Path $out 'clean-state-check.json') $report
 Compress-Archive -LiteralPath $out -DestinationPath ($out+'.zip');Write-Host ('Evidence: '+$out+'.zip');exit 0
}
AssertClosed
AssertNoLinks (SafePath $GamePath 'BepInEx')
AssertNoLinks $archiveRoot
AssertNoLinks (SafePath $GamePath 'SotM2ChineseBackups')
if($Action -eq 'Verify'){AssertBuild;VerifyInstalled;Write-Host 'CLEAN_STATE_VERIFIED: one project DLL, complete matching payload and Chinese configuration.';exit 0}
function RestoreArchive($journal,[string]$archive){
 foreach($f in $journal.originalFiles.PSObject.Properties){if((Digest (SafePath $archive ('original/'+$f.Name))) -ne $f.Value){throw ('Archive hash mismatch: '+$f.Name)}}
 foreach($relative in $journal.moves){
  $src=SafePath $archive ('original/'+$relative);$dst=SafePath $GamePath $relative
  if(Test-Path -LiteralPath $dst){throw ('Restore destination occupied: '+$relative)}
  New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($dst)) -Force | Out-Null
  Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
 }
 foreach($f in $journal.originalFiles.PSObject.Properties){if((Digest (SafePath $GamePath $f.Name)) -ne $f.Value){throw ('Restore verification failed: '+$f.Name)}}
}
if($Action -eq 'Restore'){
 if([string]::IsNullOrWhiteSpace($ArchivePath)){
  if(!(Test-Path -LiteralPath $statePath)){throw 'No clean-install state. Supply -ArchivePath for an interrupted transaction.'}
  $state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json;$ArchivePath=SafePath $archiveRoot $state.archive
 }
 $ArchivePath=[IO.Path]::GetFullPath($ArchivePath)
 if(!$ArchivePath.StartsWith($archiveRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive must be inside SotM2ChineseArchives.'}
 $j=Get-Content -LiteralPath (Join-Path $ArchivePath 'transaction.json') -Raw | ConvertFrom-Json
 if($j.product -ne 'SotM2ChinesePrototype' -or $j.status -notin @('installed','archived')){throw 'Archive is not a completed clean-install transaction.'}
 foreach($f in $j.originalFiles.PSObject.Properties){if((Digest (SafePath $ArchivePath ('original/'+$f.Name))) -ne $f.Value){throw 'Damaged archive; no files changed.'}}
 # Unknown additions or edits must never be erased by Restore.
 if(Test-Path -LiteralPath $plugin){foreach($f in Get-ChildItem -LiteralPath $plugin -Recurse -File -Force){
  if($f.DirectoryName -ne $plugin -or $f.Name -notin $names -or (Digest $f.FullName) -ne $j.installedFiles.($f.Name)){throw 'Plugin files changed after install; restore stopped.'}
 }}
 $cfg=SafePath $GamePath $configRelative
 if((Test-Path -LiteralPath $cfg) -and !(ChineseConfig $cfg)){throw 'Configuration changed after install; restore stopped.'}
 $cacheRelative='BepInEx/cache/chainloader_typeloader.dat'
 foreach($relative in $j.moves){if($relative -notin @($pluginRelative,$configRelative,$stateRelative,$cacheRelative) -and (Test-Path -LiteralPath (SafePath $GamePath $relative))){throw ('Restore destination occupied: '+$relative)}}
 $cache=SafePath $GamePath $cacheRelative
 if(Test-Path -LiteralPath $cache){
  $cacheArchive=Join-Path $ArchivePath ('post-install-cache-'+[Guid]::NewGuid().ToString('N'))
  Move-Item -LiteralPath $cache -Destination $cacheArchive
 }
 foreach($p in @($plugin,$cfg,$statePath)){if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p -Recurse -Force}}
 RestoreArchive $j $ArchivePath
 $j.status='restored';WriteJson (Join-Path $ArchivePath 'transaction.json') $j
 Write-Host ('Previous project installation restored. Archive retained: '+$ArchivePath);exit 0
}
AssertBuild;AssertPayload
$moves=New-Object 'System.Collections.Generic.List[string]'
foreach($relative in @($pluginRelative,$configRelative,'SotM2ChineseBackups','SotM2-prototype-install.json','BepInEx/cache/chainloader_typeloader.dat',$stateRelative)){
 if(Test-Path -LiteralPath (SafePath $GamePath $relative)){AssertNoLinks (SafePath $GamePath $relative);$moves.Add($relative)}
}
foreach($dll in @(OwnDlls)){
 if(!$dll.StartsWith($plugin+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){$moves.Add($dll.Substring($GamePath.Length+1).Replace('\','/'))}
}
$archive=SafePath $archiveRoot ((Get-Date).ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $archive -Force | Out-Null
$originalFiles=@{}
foreach($relative in $moves){
 $p=SafePath $GamePath $relative;$item=Get-Item -LiteralPath $p -Force
 $files=@($item);if($item.PSIsContainer){$files=@(Get-ChildItem -LiteralPath $p -File -Recurse -Force)}
 foreach($f in $files){$originalFiles[$f.FullName.Substring($GamePath.Length+1).Replace('\','/')]=Digest $f.FullName}
}
$j=@{product='SotM2ChinesePrototype';version=$manifest.version;status='prepared';moves=@($moves.ToArray());originalFiles=$originalFiles;installedFiles=$manifest.pluginFiles;configSha256=$manifest.configSha256}
WriteJson (Join-Path $archive 'transaction.json') $j
Write-Host ('Recovery archive: '+$archive)
$moved=New-Object 'System.Collections.Generic.List[string]';$writingNew=$false
try{
 foreach($relative in $moves){
  $dst=SafePath $archive ('original/'+$relative);New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($dst)) -Force | Out-Null
  Move-Item -LiteralPath (SafePath $GamePath $relative) -Destination $dst;$moved.Add($relative)
 }
 foreach($f in $originalFiles.Keys){if((Digest (SafePath $archive ('original/'+$f))) -ne $originalFiles[$f]){throw 'Archive verification failed.'}}
 $j.status='archived';WriteJson (Join-Path $archive 'transaction.json') $j
 $writingNew=$true
 New-Item -ItemType Directory -Path $plugin -Force | Out-Null
 foreach($n in $names){Copy-Item -LiteralPath (SafePath $PSScriptRoot ('payload/'+$n)) -Destination (SafePath $plugin $n) -Force}
 New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName((SafePath $GamePath $configRelative))) -Force | Out-Null
 Copy-Item -LiteralPath (SafePath $PSScriptRoot 'payload/sotm2.zh.prototype.cfg') -Destination (SafePath $GamePath $configRelative) -Force
 VerifyInstalled
 WriteJson $statePath @{product='SotM2ChinesePrototype';version=$manifest.version;archive=[IO.Path]::GetFileName($archive)}
 $j.status='installed';WriteJson (Join-Path $archive 'transaction.json') $j
}catch{
 $failure=$_.Exception.Message
 if($writingNew){foreach($p in @($plugin,(SafePath $GamePath $configRelative),$statePath)){if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p -Recurse -Force}}}
 foreach($relative in $moved){
  $dst=SafePath $GamePath $relative;New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($dst)) -Force | Out-Null
  Copy-Item -LiteralPath (SafePath $archive ('original/'+$relative)) -Destination $dst -Recurse -Force
 }
 $j.status='rolled-back';WriteJson (Join-Path $archive 'transaction.json') $j
 throw ($failure+' | Previous files restored; archive: '+$archive)
}
Write-Host '0.1.30-rc.1 clean installation complete. Reviewed text and name rules installed; old project files archived outside plugin loading paths.'
