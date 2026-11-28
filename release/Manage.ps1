param([ValidateSet('Install','Uninstall','Rollback')][string]$Action='Install',[string]$GamePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
function Hash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function Json([string]$p,$v){[IO.File]::WriteAllText($p,($v|ConvertTo-Json -Depth 20),(New-Object Text.UTF8Encoding($false)))}
function Safe([string]$root,[string]$rel){
 if(!$rel -or [IO.Path]::IsPathRooted($rel) -or $rel -match '(^|[\\/])\.\.([\\/]|$)' -or $rel.Contains(':')){throw 'Unsafe path.'}
 $p=[IO.Path]::GetFullPath((Join-Path $root $rel))
 if(!$p.StartsWith($root.TrimEnd([char[]]@('/','\'))+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped root.'}
 return $p
}
function NoLinks([string]$p){
 if(Test-Path -LiteralPath $p){
  foreach($x in @((Get-Item -LiteralPath $p -Force))+@(Get-ChildItem -LiteralPath $p -Recurse -Force)){
   if(($x.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw ('Linked path: '+$x.FullName)}
  }
 }
}
function Put([string]$src,[string]$dst){
 New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($dst)) -Force|Out-Null
 Copy-Item -LiteralPath $src -Destination $dst -Force
}
function Relative([string]$p){$p.Substring($GamePath.Length+1).Replace('\','/')}
function ProjectPath([string]$rel){
 return $rel -eq 'SotM2ChinesePlayerState.json' -or $rel -eq 'BepInEx/config/sotm2.zh.prototype.cfg' -or $rel -eq 'BepInEx/cache/chainloader_typeloader.dat' -or $rel.StartsWith('BepInEx/plugins/',[StringComparison]::Ordinal)
}
if([string]::IsNullOrWhiteSpace($GamePath)){$GamePath=Read-Host 'Paste the game folder from Steam > Manage > Browse local files'}
if([string]::IsNullOrWhiteSpace($GamePath)){throw 'Game folder required.'}
$GamePath=(Resolve-Path -LiteralPath $GamePath.Trim().Trim('"')).Path.TrimEnd([char[]]@('/','\'))
if(!(Test-Path -LiteralPath (Safe $GamePath 'Sentinels2.exe'))){throw 'Sentinels2.exe not found.'}
if(Get-Process -Name 'Sentinels2','Sentinels of the Multiverse 2' -ErrorAction SilentlyContinue){throw 'Close the game first.'}
NoLinks $GamePath
$statePath=Safe $GamePath 'SotM2ChinesePlayerState.json'
$archiveRoot=Safe $GamePath 'SotM2ChinesePlayerBackups'
$pluginRel='BepInEx/plugins/SotM2ChinesePrototype'
$plugin=Safe $GamePath $pluginRel
$configRel='BepInEx/config/sotm2.zh.prototype.cfg'
function OwnDlls {
 $dir=Safe $GamePath 'BepInEx/plugins'
 if(Test-Path -LiteralPath $dir){
  foreach($f in Get-ChildItem -LiteralPath $dir -Recurse -File -Filter '*.dll'){
   $own=$f.Name -like 'SotM2ChinesePrototype*.dll'
   if(!$own){try{$own=[Reflection.AssemblyName]::GetAssemblyName($f.FullName).Name -eq 'SotM2ChinesePrototype'}catch{}}
   if($own){$f.FullName}
  }
 }
}
function ChineseConfig([string]$p){
 if(!(Test-Path -LiteralPath $p -PathType Leaf)){return $false}
 $lines=@(Get-Content -LiteralPath $p | ForEach-Object {$_.Trim()} | Where-Object {$_ -and !$_.StartsWith('#') -and !$_.StartsWith(';')})
 return ($lines -join '|') -eq '[Prototype]|EnableChinese = true'
}
function GetState {
 if(!(Test-Path -LiteralPath $statePath)){throw 'No player installation record.'}
 $s=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json
 if($s.product -ne 'SotM2ChinesePlayer'){throw 'Invalid installation record.'}
 foreach($f in $s.files.PSObject.Properties){
  if(!$f.Name.StartsWith($pluginRel+'/',[StringComparison]::Ordinal) -and $f.Name -ne $configRel){throw 'Invalid owned path.'}
  $p=Safe $GamePath $f.Name
  $valid=if($f.Name -eq $configRel){ChineseConfig $p}else{(Test-Path -LiteralPath $p -PathType Leaf) -and (Hash $p) -eq $f.Value}
  if(!$valid){throw ('Installed file changed; preserve your edits before continuing: '+$f.Name)}
 }
 return $s
}
if($Action -eq 'Uninstall'){
 $s=GetState
 # Only remove hash-matching owned files. Shared loader, saves and other mods remain.
 foreach($f in $s.files.PSObject.Properties){Remove-Item -LiteralPath (Safe $GamePath $f.Name)}
 Remove-Item -LiteralPath $statePath
 Write-Host 'Chinese localization uninstalled. Shared loader and your saves are preserved.'
 exit 0
}
if($Action -eq 'Rollback'){
 $s=GetState
 $archive=Safe $archiveRoot $s.backup
 NoLinks $archive
 $j=Get-Content -LiteralPath (Safe $archive 'transaction.json') -Raw|ConvertFrom-Json
 if($j.product -ne 'SotM2ChinesePlayer' -or $j.status -ne 'installed'){throw 'Invalid rollback transaction.'}
 foreach($f in $j.originals.PSObject.Properties){
  if(!(ProjectPath $f.Name) -or (Hash (Safe $archive ('original/'+$f.Name))) -ne $f.Value){throw 'Invalid or damaged backup.'}
  $dst=Safe $GamePath $f.Name
  if((Test-Path -LiteralPath $dst) -and $f.Name -ne 'SotM2ChinesePlayerState.json' -and $s.files.PSObject.Properties.Name -notcontains $f.Name){throw ('Rollback destination occupied: '+$f.Name)}
 }
 foreach($f in $s.files.PSObject.Properties){Remove-Item -LiteralPath (Safe $GamePath $f.Name)}
 Remove-Item -LiteralPath $statePath
 foreach($f in $j.originals.PSObject.Properties){Put (Safe $archive ('original/'+$f.Name)) (Safe $GamePath $f.Name)}
 Write-Host 'Previous project files restored. Shared loader is preserved.'
 exit 0
}
$m=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw|ConvertFrom-Json
if($m.product -ne 'SotM2ChinesePlayer' -or $m.flavor -ne 'player-no-diagnostics'){throw 'Not a player package.'}
foreach($rel in $m.dependencies){
 if($rel -notin @('winhttp.dll','doorstop_config.ini','.doorstop_version') -and $rel -notmatch '^(BepInEx/(core|interop)|dotnet)/[^:]+$'){throw 'Invalid dependency path.'}
 if($m.files.PSObject.Properties.Name -notcontains $rel){throw 'Missing dependency hash.'}
}
foreach($rel in $m.files.PSObject.Properties.Name){
 if($m.dependencies -notcontains $rel -and !$rel.StartsWith($pluginRel+'/',[StringComparison]::Ordinal) -and $rel -notin @($configRel,'BepInEx/config/BepInEx.cfg')){throw 'Invalid payload path.'}
}
NoLinks (Join-Path $PSScriptRoot 'payload')
foreach($f in $m.files.PSObject.Properties){
 $p=Safe $PSScriptRoot ('payload/'+$f.Name)
 if(!(Test-Path -LiteralPath $p) -or (Hash $p) -ne $f.Value){throw ('Package damaged: '+$f.Name)}
}
foreach($f in $m.gameFiles.PSObject.Properties){
 $p=Safe $GamePath $f.Name
 if(!(Test-Path -LiteralPath $p) -or (Hash $p) -ne $f.Value){throw ('Unsupported game build: '+$f.Name)}
}
# Dependencies are pinned and shared. Never replace a different loader in place.
foreach($rel in $m.dependencies){
 $dst=Safe $GamePath $rel
 if((Test-Path -LiteralPath $dst) -and (Hash $dst) -ne $m.files.$rel){throw ('Different shared runtime found; no files changed: '+$rel)}
}
$loaderCfg=Safe $GamePath 'BepInEx/config/BepInEx.cfg'
if(Test-Path -LiteralPath $loaderCfg){
 $cfg=Get-Content -LiteralPath $loaderCfg -Raw
 if($cfg -notmatch '(?m)^\s*UpdateInteropAssemblies\s*=\s*false\s*$' -or $cfg -notmatch '(?m)^\s*PreloadIL2CPPInteropAssemblies\s*=\s*true\s*$'){throw 'Existing BepInEx settings differ from the pinned runtime; no files changed.'}
}
# Back up only project-owned content; keep unknown neighboring mods intact.
$old=@{}
if(Test-Path -LiteralPath $plugin){foreach($f in Get-ChildItem -LiteralPath $plugin -File -Recurse -Force){$old[(Relative $f.FullName)]=$f.FullName}}
foreach($f in @(OwnDlls)){$old[(Relative $f)]=$f}
foreach($rel in @($configRel,'BepInEx/cache/chainloader_typeloader.dat','SotM2ChinesePlayerState.json')){
 $p=Safe $GamePath $rel;if(Test-Path -LiteralPath $p){$old[$rel]=$p}
}
$id=(Get-Date).ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N')
$archive=Safe $archiveRoot $id
New-Item -ItemType Directory -Path $archive -Force|Out-Null
$j=@{product='SotM2ChinesePlayer';status='prepared';originals=@{};created=@();version=$m.version}
foreach($rel in $old.Keys){Put $old[$rel] (Safe $archive ('original/'+$rel));$j.originals[$rel]=Hash $old[$rel]}
Json (Safe $archive 'transaction.json') $j
$written=New-Object 'System.Collections.Generic.List[string]'
$removed=New-Object 'System.Collections.Generic.List[string]'
try {
 foreach($rel in $old.Keys){Remove-Item -LiteralPath $old[$rel];$removed.Add($rel)}
 foreach($f in $m.files.PSObject.Properties){
  $rel=$f.Name;$dst=Safe $GamePath $rel
  if($m.dependencies -contains $rel -and (Test-Path -LiteralPath $dst)){continue}
  if($rel -eq 'BepInEx/config/BepInEx.cfg' -and (Test-Path -LiteralPath $dst)){continue}
  if(Test-Path -LiteralPath $dst){throw ('Unexpected occupied destination: '+$rel)}
  $written.Add($rel)
  Put (Safe $PSScriptRoot ('payload/'+$rel)) $dst
  if((Hash $dst) -ne $f.Value){throw ('Copy verification failed: '+$rel)}
 }
 $owned=@{}
 foreach($f in $m.files.PSObject.Properties){if($f.Name.StartsWith($pluginRel+'/') -or $f.Name -eq $configRel){$owned[$f.Name]=$f.Value}}
 if(@(OwnDlls).Count -ne 1){throw 'Duplicate project plugin detected.'}
 Json $statePath @{product='SotM2ChinesePlayer';version=$m.version;backup=$id;files=$owned}
 $j.status='installed';$j.created=@($written);Json (Safe $archive 'transaction.json') $j
 Write-Host ('Installed '+$m.version+'. Start the game through Steam. Backup: '+$archive)
} catch {
 $failure=$_
 foreach($rel in $written){$dst=Safe $GamePath $rel;if(Test-Path -LiteralPath $dst){Remove-Item -LiteralPath $dst}}
 if(Test-Path -LiteralPath $statePath){Remove-Item -LiteralPath $statePath}
 foreach($rel in $removed){Put (Safe $archive ('original/'+$rel)) (Safe $GamePath $rel)}
 $j.status='rolled-back';Json (Safe $archive 'transaction.json') $j
 throw $failure
}
