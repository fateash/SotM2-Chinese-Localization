param([ValidateSet('Install','Restore','Collect')][string]$Action='Install',[string]$GamePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
function Digest([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
if([string]::IsNullOrWhiteSpace($GamePath)){
 $GamePath=Read-Host 'Game folder (Enter = E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2)'
 if([string]::IsNullOrWhiteSpace($GamePath)){$GamePath='E:\SteamLibrary\steamapps\common\Sentinels of the Multiverse 2'}
}
$GamePath=$GamePath.Trim().Trim('"')
if(!(Test-Path -LiteralPath $GamePath -PathType Container)){throw 'Game folder not found.'}
$GamePath=(Resolve-Path -LiteralPath $GamePath).Path
$plugin=Join-Path $GamePath 'BepInEx/plugins/SotM2ChinesePrototype'
$backup=Join-Path $GamePath 'SotM2ChineseBackups/dynamic-pilot-0.1.16'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$names=@('SotM2ChinesePrototype.dll','surface-probe.json')
$expected=@{'SotM2ChinesePrototype.dll'=$manifest.dllSha256;'surface-probe.json'=$manifest.probeSha256}
if($Action -eq 'Collect'){
 $log=Join-Path $GamePath 'BepInEx/LogOutput.log'
 if(!(Test-Path -LiteralPath $log)){throw 'LogOutput.log not found.'}
 $out=Join-Path $PSScriptRoot ('SotM2-Dynamic-Evidence-'+(Get-Date).ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
 New-Item -ItemType Directory -Path $out | Out-Null
 Copy-Item -LiteralPath $log -Destination $out
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $out
 $installed=@{};foreach($n in ($names+@('sample-texts.json'))){$p=Join-Path $plugin $n;if(Test-Path -LiteralPath $p){$installed[$n]=Digest $p}}
 $installed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'installed-hashes.json') -Encoding UTF8
 Compress-Archive -LiteralPath $out -DestinationPath ($out+'.zip')
 Write-Host ('Evidence: '+$out+'.zip');exit 0
}
if(Get-Process -Name 'Sentinels2','Sentinels of the Multiverse 2' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
$dll=Join-Path $plugin $names[0];$catalog=Join-Path $plugin 'sample-texts.json';$probe=Join-Path $plugin $names[1]
if(!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $catalog)){throw 'Existing accepted 0.1.6 through 0.1.15 installation required.'}
$hasBackup=Test-Path -LiteralPath $backup
if($hasBackup){
 $state=Get-Content -LiteralPath (Join-Path $backup 'original-hashes.json') -Raw | ConvertFrom-Json
 foreach($n in $names){if($state.$n -ne 'ABSENT' -and (Digest (Join-Path $backup ($n+'.backup'))) -ne $state.$n){throw ('Backup hash mismatch: '+$n)}}
}
if($Action -eq 'Restore'){
 if(!$hasBackup){throw 'No surface probe backup found.'}
 foreach($n in $names){
  $p=Join-Path $plugin $n;$actual='ABSENT';if(Test-Path -LiteralPath $p){$actual=Digest $p}
  if($actual -notin @($expected[$n],$state.$n)){throw ('File changed after install; refusing to overwrite: '+$n)}
 }
 foreach($n in $names){
  $p=Join-Path $plugin $n
  if($state.$n -eq 'ABSENT'){if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p}}
  else{Copy-Item -LiteralPath (Join-Path $backup ($n+'.backup')) -Destination $p -Force}
 }
 foreach($n in $names){$p=Join-Path $plugin $n;$actual='ABSENT';if(Test-Path -LiteralPath $p){$actual=Digest $p};if($actual -ne $state.$n){throw 'Restore verification failed; backup retained.'}}
 Remove-Item -LiteralPath $backup -Recurse -Force
 Write-Host 'Restored previous DLL and probe state. Translation catalog was never changed.';exit 0
}
foreach($file in $manifest.gameFiles.PSObject.Properties){
 $p=Join-Path $GamePath $file.Name
 if(!(Test-Path -LiteralPath $p) -or (Digest $p) -ne $file.Value){throw ('Game build mismatch: '+$file.Name)}
}
if((Digest $catalog) -notin $manifest.acceptedCatalogs){throw 'Catalog differs from accepted 0.1.6 catalogs; no files changed.'}
if($hasBackup){
 foreach($n in $names){if(!(Test-Path -LiteralPath (Join-Path $plugin $n)) -or (Digest (Join-Path $plugin $n)) -ne $expected[$n]){throw 'Installed probe files changed; no overwrite.'}}
}else{
 $oldProbe='ABSENT';if(Test-Path -LiteralPath $probe){$oldProbe=Digest $probe}
 $oldDll=Digest $dll;$accepted=$false
 foreach($pair in $manifest.acceptedInstallations){if($pair.dll -eq $oldDll -and $pair.probe -eq $oldProbe){$accepted=$true}}
 if(!$accepted){throw 'DLL/probe pair is not an accepted installation; no files changed.'}
}
foreach($n in $names){if((Digest (Join-Path $PSScriptRoot ('payload/'+$n))) -ne $expected[$n]){throw ('Package hash mismatch: '+$n)}}
if(!$hasBackup){
 $staging=$backup+'.pending-'+[Guid]::NewGuid().ToString('N')
 New-Item -ItemType Directory -Path $staging -Force | Out-Null
 try{
  $state=@{}
  foreach($n in $names){
   $p=Join-Path $plugin $n;$state[$n]='ABSENT'
   if(Test-Path -LiteralPath $p){$state[$n]=Digest $p;Copy-Item -LiteralPath $p -Destination (Join-Path $staging ($n+'.backup'));if((Digest (Join-Path $staging ($n+'.backup'))) -ne $state[$n]){throw 'Backup verification failed.'}}
  }
  $state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staging 'original-hashes.json') -Encoding UTF8
  Move-Item -LiteralPath $staging -Destination $backup
 }catch{if(Test-Path -LiteralPath $staging){Remove-Item -LiteralPath $staging -Recurse -Force};throw}
}
$previous=@{};foreach($n in $names){$p=Join-Path $plugin $n;$previous[$n]=$null;if(Test-Path -LiteralPath $p){$previous[$n]=[IO.File]::ReadAllBytes($p)}}
try{
 foreach($n in $names){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('payload/'+$n)) -Destination (Join-Path $plugin $n) -Force}
 foreach($n in $names){if((Digest (Join-Path $plugin $n)) -ne $expected[$n]){throw 'Install verification failed.'}}
}catch{
 foreach($n in $names){$p=Join-Path $plugin $n;if($null -eq $previous[$n]){if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p}}else{[IO.File]::WriteAllBytes($p,$previous[$n])}}
 throw
}
Write-Host '0.1.16-rc.1 installed. Existing translations, font and configuration retained. Restart the game to test.'
