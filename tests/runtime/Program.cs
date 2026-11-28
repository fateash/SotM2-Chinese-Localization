using SotM2Chinese;
int count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
var rules=new DisplayRules(new[]{
 new DisplayEntry{ObjectName="Body Label",Card="LegacyCharacter",Mode="style",Style="PowerTitle",Source="Galvanize",Target="测试标题"},
 new DisplayEntry{ObjectName="Body Label",Mode="style",Style="StartPhase",Source="START PHASE",Target="开始阶段"},
 new DisplayEntry{ObjectName="Body Label",Mode="phase-inline",Source="START PHASE",Target="开始阶段"},
 new DisplayEntry{ObjectName="Body Label",Mode="style",Style="PowerHeading",Source="POWER: ",Target="异能: "},
 new DisplayEntry{ObjectName="Keywords Label",Mode="keyword",Source="ongoing",Target="持续"},
 new DisplayEntry{ObjectName="Keywords Label",Mode="keyword",Source="limited",Target="限定"},
 new DisplayEntry{ObjectName="Flavor Text Label",Card="MobileDefensePlatform",Source="“Until we take that <b>blasted</b> floating fortress\r\nout, Blade is practically untouchable!”",Target="“测试引语<b>强调</b>\r\n第二行”"},
 new DisplayEntry{ObjectName="Title Label",Card="BolsterAllies",Source="Bolster Allies",Target="测试卡名"}
});
string prefix="<style=PowerTitle>Galvanize</style>\r\n<style=PowerHeading>POWER: </style><style=PowerBody>";
string suffix="直到你的<nobr><b>START PHASE</b></nobr>，盟友角色造成的伤害+1。\r\n</style>";
var result=rules.Apply("Body Label","LegacyCharacter",prefix+suffix)!;
Check(result.Contains("<style=PowerTitle>测试标题</style>"),"semantic ability title inside complete body");
Check(result.Contains("<style=PowerHeading>异能: </style>"),"replace power label instead of append marker");
Check(result.Contains("<nobr><b>开始阶段</b></nobr>"),"inline phase with tags preserved");
Check(result.Contains("伤害+1。\r\n</style>"),"body numbers/newline preserved");
Check(rules.Apply("Body Label","OtherCharacter","<style=PowerTitle>Galvanize</style>")==null,"card title identity isolation");
foreach(var body in new[]{"Deals <b>(Baron Blade)</b> 2 damage.","Deals <b>(Legacy)</b> 8 damage.","Each Hero discards 1 card."}){
 var source="<sprite name=\"StartPhase\"><style=StartPhase>START PHASE</style>\r\n"+body;
 Check(rules.Apply("Body Label","AnyCard",source)==source.Replace(">START PHASE<",">开始阶段<"),"phase independent of changing rule text "+body);
}
Check(rules.Apply("Keywords Label","Any","ongoing, limited")=="持续, 限定","keyword list delimiters preserved");
Check(rules.Apply("Keywords Label","Any","ONGOING, unfamiliar, LIMITED")=="持续, unfamiliar, 限定","unknown keywords retained");
Check(rules.Apply("Keywords Label","Any","ongoing-like")==null,"no partial keyword substitution");
Check(rules.Apply("Body Label","Any","PowerTitle Galvanize START PHASE 27")==null,"plain body text never globally substituted");
Check(rules.Apply("HP Label","LegacyCharacter",prefix+suffix)==null,"HP object excluded");
Check(rules.Apply("Title Label","OtherCard","Bolster Allies")==null,"exact card names scoped");
Check(rules.Apply("Title Label","BolsterAllies","Bolster Allies")=="测试卡名","exact card name matches");
Check(rules.Apply("Flavor Text Label","MobileDefensePlatform","“Until we take that <b>blasted</b> floating fortress\r\nout, Blade is practically untouchable!”")=="“测试引语<b>强调</b>\r\n第二行”","CRLF quote exact matching");
bool rejected=false;try{new DisplayRules(new[]{new DisplayEntry{ObjectName="HP Label",Source="32",Target="99"}});}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid display scope rejected");
var catalog=Catalog.Read(args[0]);Check(catalog.SchemaVersion==2 && catalog.BuildId=="26880829" && catalog.RuntimeVersion=="0.1.6","compiled catalog declares supported schema/build/runtime");
var probe=new SurfaceProbeRules{DeckTitles=new[]{"Legacy","Baron Blade"}};
Check(probe.Apply(SurfaceProbeRules.HintPath,SurfaceProbeRules.PlayHint)=="示·"+SurfaceProbeRules.PlayHint,"exact observed hint marker");
Check(probe.Apply(SurfaceProbeRules.TurnPath,"Legacy")=="组·Legacy","exact HUD deck title marker");
Check(probe.Apply("Other/Turn Taker Label","Legacy")==null,"same object name in other hierarchy excluded");
Check(probe.Apply(SurfaceProbeRules.TurnPath,"Legacy deals 3 damage")==null,"no names replaced inside dynamic sentences");
Check(probe.Apply(SurfaceProbeRules.TurnPath,"32")==null,"numeric content never marked");
Check(probe.Apply(SurfaceProbeRules.TurnPath,"组·Legacy")==null,"markers not accumulated");
Check(probe.Apply(SurfaceProbeRules.HintPath,"Choose a Hero")==null,"unknown dynamic hint captured without rewriting");
Check(!SurfaceProbeRules.CanObserve("Hint Label",false,true),"input fields excluded from capture");
Check(!SurfaceProbeRules.CanObserve("Title Label",true,false),"CardView titles excluded from this probe");
Check(!SurfaceProbeRules.CanObserve("HP Label",false,false),"HP objects excluded from capture");
Check(SurfaceProbeRules.CanObserve("Selection Button Label",false,false),"selection buttons eligible for read-only capture");
var pilot=SurfaceProbeRules.Read(args.Length>1?args[1]:"probe/dynamic-pilot.json");
Check(pilot.SchemaVersion==2 && pilot.Dynamic!=null,"pilot requires explicit schema 2 opt-in");
Check(pilot.Apply(SurfaceProbeRules.TurnPath,"Ra")=="拉神","pilot translates approved HUD name");
Check(pilot.Apply(SurfaceProbeRules.HintPath,SurfaceProbeRules.PlayHint)=="点击或拖动一张牌以将其打出。","pilot replaces prior hint marker");
foreach(int n in new[]{0,1,2,3,12,0}){
 var input="Damage "+n+" "+(n==1?"target":"targets");
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,input)=="对"+n+"个目标造成伤害","selection count remains live "+n);
}
foreach(var pair in new[]{("Ra","2","fire","火焰"),("Nimble Strike","9","melee","近战"),("Ra","1","fire","火焰")}){
 var input="<b>"+pair.Item1+":</b> Click a target to deal it <nobr>"+pair.Item2+" "+pair.Item3+"</nobr> damage.";
 var expected="<b>"+pair.Item1+":</b> 点击一个目标，对其造成<nobr>"+pair.Item2+"点"+pair.Item4+"伤害</nobr>。";
 Check(pilot.Apply(SurfaceProbeRules.HintPath,input)==expected,"source amount type preserved "+pair);
}
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Consuming Flame:</b> Click up to 3 targets to deal them <nobr>2 fire</nobr> damage.")=="<b>Consuming Flame:</b> 点击至多3个目标，对其各造成<nobr>2点火焰伤害</nobr>。","multi-target amount and upper limit separate");
foreach(var input in new[]{"<b>Ra:</b> Click a target to deal it <nobr>2 unknown</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>-2 fire</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>2 fire</nobr> damage. Extra"})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,input)==null,"unknown grammar untouched "+input);
Check(pilot.Apply("Other/Finish Button Label","Damage 3 targets")==null,"same label other hierarchy untouched");
Check(pilot.Apply(DynamicDisplayRules.FinishPath,"Damage 1 targets")==null,"unexpected plural form untouched");
Check(pilot.Apply(DynamicDisplayRules.FinishPath,"对3个目标造成伤害")==null,"no repeated translation");
Check(pilot.Apply(DynamicDisplayRules.DrawPath,"Discard")==null,"draw/discard location separated");
Check(pilot.Apply("CardView/Selection Button Label","Double Damage")==null,"card buttons capture-only");
Check(pilot.Apply(DynamicDisplayRules.DescriptionPath,"Ra drew:")=="Ra抽到的牌：","draw description not a rule model mutation");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Name {1}:</b> Click a button on Card {0} to choose an effect.")=="<b>Name {1}:</b> 点击Card {0}上的按钮，选择一个效果。","placeholder-like names remain literal arguments");
var invalid=new Dictionary<string,string>(pilot.Templates);invalid["finish.damage"]="对3个目标造成伤害";
rejected=false;try{new DynamicDisplayRules(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"missing dynamic parameter rejected");
invalid=new(pilot.Templates);invalid["hint.attack.single"]="{0}{1}{2}";
rejected=false;try{new DynamicDisplayRules(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"missing rich-text wrapper rejected");
invalid=new(pilot.Templates);invalid["finish.damage"]="对{{0}}个目标造成伤害";
rejected=false;try{new DynamicDisplayRules(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"escaped placeholder cannot freeze parameter");
var discardParts=DynamicDisplayRules.ParseConfirmation("Discard 3 cards");
Check(discardParts is {Action:"Discard",Number:"3",NumberKind:"card-count"},"shared confirmation grammar separates card count");
var healParts=DynamicDisplayRules.ParseConfirmation("Heal 2 targets");
Check(healParts is {Action:"Heal",Number:"2",NumberKind:"target-count"},"synthetic recovery target count distinct from HP amount");
var hpParts=DynamicDisplayRules.ParseConfirmation("Regain 3 HP");
Check(hpParts is {Number:"3",NumberKind:"hp-amount"},"HP amount is never treated as selected target count");
foreach(var unverified in new[]{"Heal 2 targets","Regain 3 HP","Buff 2 targets"})
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,unverified)==null,"unverified confirmation action only captured: "+unverified);
Check(DynamicDisplayRules.ParseConfirmation("Choose how much HP")==null,"unrecognized confirmation remains raw evidence");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/surface-0.1.7.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,input=sample.GetProperty("source").GetString()!;
  var output=pilot.Apply(path,input);
  Check((output!=null)==sample.GetProperty("expectTranslated").GetBoolean(),"captured evidence: "+input);
  if(output!=null)Check(pilot.Apply(path,output)==null,"captured output idempotent: "+input);
 }
}
foreach(var n in new[]{0,1,2,3,0}){
 string noun=n==1?"card":"cards";
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,"Discard "+n+" "+noun)=="弃置"+n+"张牌","discard live count "+n);
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,"Select "+n+" "+noun)=="选择"+n+"张牌","generic selection distinct from discard "+n);
}
Check(pilot.Apply(DynamicDisplayRules.FinishPath,"Select 3 targets")==null,"unverified unit pair not inferred from cards");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Card {0}:</b> Click a card to play it.")=="<b>Card {0}:</b> 点击一张牌以将其打出。","optional source prefix retained literally");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Blade Battalion:</b> Click a target to deal it melee damage.")=="<b>Blade Battalion:</b> 点击一个目标，对其造成近战伤害。","missing original amount not invented");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Counterpoint Bulwark:</b> Click up to 3 targets to reduce damage dealt to them.")=="<b>Counterpoint Bulwark:</b> 点击至多3个目标，减少它们受到的伤害。","reduce target count is not reduction amount");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Synaptic Interrupt:</b> <b>Powered Turret</b> will deal <b>Tachyon</b> 3 energy damage. Click a reaction to use it.")=="<b>Synaptic Interrupt:</b> <b>Powered Turret</b>将对<b>Tachyon</b>造成3点能量伤害。点击一个反应以使用它。","reaction source target amount preserved");
string effectPath="Canvas/Main Panel/Ra/Inner Panel/Foreground Elements/Play Area/CardView TheStaffOfRa-2 SmallCardFront/Select Effect Container/Select Effect Button(Clone)/Label";
Check(pilot.Dynamic!.ApplyCardButton(effectPath,"TheStaffOfRa","Double Damage")=="伤害翻倍","effect button card identity and suffix");
Check(pilot.Dynamic.ApplyCardButton(effectPath,"OtherCard","Double Damage")=="伤害翻倍","common effect action text works across cards in the scoped effect-button role");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Effect Panel/Effect View(Clone)"),"TheStaffOfRa","Double Damage")==null,"effect status labels excluded");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Power Container/UsePower"),"TheStaffOfRa","Double Damage")==null,"power body labels excluded");
Check(pilot.Apply("Other/Selection Button Label","Play a Card")==null,"hero button requires observed hierarchy role");
Check(pilot.Apply("Canvas/Main Panel/Legacy/Inner Panel/Foreground Elements/Selection Button/Selection Button Label","Play a Card")=="打出一张牌","active hero role supported");
Check(SurfaceProbeRules.CanObserve("Phase Label",false,false),"phase remaining label enabled for capture");
// Live raw evidence covers PLAY 3 -> 2; DRAW shares the screenshot-observed label grammar.
foreach(var phase in new[]{("PLAY","出牌"),("DRAW","抽牌")}){
 foreach(var n in new[]{"3","2","1","0","3","12","999999"}){
  var output=pilot.Apply(DynamicDisplayRules.PhasePath,phase.Item1+" PHASE ("+n+" left)");
  Check(output==phase.Item2+"阶段（剩余"+n+"次）","phase count remains live: "+phase.Item1+n);
  Check(pilot.Apply(DynamicDisplayRules.PhasePath,output!)==null,"translated phase remains stable");
 }
}
foreach(var input in new[]{"出牌阶段","PLAY PHASE (-1 left)","PLAY PHASE (1.5 left)","PLAY PHASE (1000000 left)","PLAY PHASE (2 left) suffix","prefix PLAY PHASE (2 left)","PLAY PHASE (2 left)\n","START PHASE (2 left)","PLAY PHASE (<b>2</b> left)","PLAY PHASE (2 LEFT)"})
 Check(pilot.Apply(DynamicDisplayRules.PhasePath,input)==null,"nonmatching phase left to existing behavior: "+input);
Check(pilot.Apply("Other/Phase Label","PLAY PHASE (3 left)")==null,"phase path must match exactly");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"PLAY PHASE (3 left)")==null,"phase grammar cannot affect hints");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/phase-0.1.9.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray())
  Check(pilot.Apply(sample.GetProperty("path").GetString()!,sample.GetProperty("source").GetString()!)==sample.GetProperty("expected").GetString(),"live 0.1.9 phase capture");
}
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/surface-0.1.8.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,input=sample.GetProperty("source").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();string card=sample.GetProperty("cardId").GetString()!;
  var output=inCard?pilot.Dynamic.ApplyCardButton(path,card,input):pilot.Apply(path,input);
  Check((output!=null)==sample.GetProperty("expectTranslated").GetBoolean(),"0.1.8 capture: "+path+" => "+input);
  if(output!=null){
   Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,output):pilot.Apply(path,output))==null,"0.1.8 output idempotent: "+input);
   string Tags(string t)=>string.Join("|",System.Text.RegularExpressions.Regex.Matches(t,@"<[^<>]+>").Select(m=>m.Value));
   Check(Tags(input)==Tags(output),"0.1.8 original rich-text tags preserved: "+input);
  }
 }
}
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-expansion-0.1.10.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,input=sample.GetProperty("source").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  string? output=inCard?pilot.Dynamic.ApplyCardButton(path,card,input):pilot.Apply(path,input);
  Check(output==sample.GetProperty("expected").GetString(),"expanded live golden: "+input);
  Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,output!):pilot.Apply(path,output!))==null,"expanded output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(input,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(output!,@"<[^<>]+>").Select(m=>m.Value)),"expanded markup preserved");
 }
}
var damageTypes=new Dictionary<string,string>{["cold"]="寒冷",["energy"]="能量",["fire"]="火焰",["infernal"]="邪能",["lightning"]="闪电",["melee"]="近战",["projectile"]="投射",["psychic"]="精神",["radiant"]="光耀",["sonic"]="音波",["toxic"]="毒素"};
foreach(var pair in damageTypes){
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Source:</b> Click up to 17 targets to deal them <nobr>3 "+pair.Key+"</nobr> damage.")=="<b>Source:</b> 点击至多17个目标，对其各造成<nobr>3点"+pair.Value+"伤害</nobr>。","all frozen damage types: "+pair.Key);
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Source:</b> Click 2 targets to deal them <nobr>3 "+pair.Key+"</nobr> damage.")=="<b>Source:</b> 点击2个目标，对其各造成<nobr>3点"+pair.Value+"伤害</nobr>。","required count distinct from up to: "+pair.Key);
}
foreach(var bad in new[]{"<b>Card:</b> Click 2 targets to deal them <nobr>3 unknown</nobr> damage.","<b>Card:</b> Click <b>Target</b> and 1 more targets to deal them <nobr>3 melee</nobr> damage.","Click a location to to play <b>Name</b> there.","Click a card in play to activate its <nobr><sprite name=\"EndPhase\"> <b>START PHASE</b></nobr> effect."})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,bad)==null,"reject ambiguous/new markup or invalid grammar "+bad);
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a card to activate its <nobr><sprite name=\"StartPhase\"> <b>START PHASE</b></nobr> effect.")=="点击一张牌，发动其<nobr><sprite name=\"StartPhase\"> <b>开始阶段</b></nobr>效果。","phase hint without in-play prefix");
Check(pilot.Dynamic.ApplyCardButton(effectPath,"TheStaffOfRa","Regain 3 HP")=="恢复3点生命值","effect amount is HP not target count");
Check(pilot.Dynamic.ApplyCardButton(effectPath,"TheStaffOfRa","Deal 2 Targets 3 Damage")=="对2个目标造成3点伤害","effect target count and fixed damage remain separate");
Check(pilot.Dynamic.ApplyCardButton(effectPath,"TheStaffOfRa","Regain 3 targets")==null,"effect HP grammar cannot consume targets");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Effect Panel/Effect View(Clone)"),"TheStaffOfRa","Regain 3 HP")==null,"HP status excluded from effect-button translation");
string nextTo="Canvas/Main Panel/Legacy/Inner Panel/Character Card Container/LegacyCharacter/Foreground Elements/Next To Selection Button/Selection Button Label";
Check(pilot.Dynamic.ApplyCardButton(nextTo,"LegacyCharacter","Play Here")=="在此打出","placement role with matched character identity");
Check(pilot.Dynamic.ApplyCardButton(nextTo,"RaCharacter","Play Here")==null,"placement card/path mismatch rejected");
Check(pilot.Dynamic.ApplyCardButton(nextTo.Replace("LegacyCharacter/Foreground","RaCharacter/Foreground"),"RaCharacter","Play Here")==null,"placement hero/card mismatch rejected");
Check(pilot.Apply("Other/Hint Label","Select Redirection Target")==null,"selection title not translated outside hint role");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/placement-0.1.11.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  Check(pilot.Dynamic.ApplyCardButton(path,card,"Play Here")=="在此打出","observed placement button: "+card);
  Check(pilot.Dynamic.ApplyCardButton(path,card,"在此打出")==null,"placement translation idempotent");
  Check(pilot.Dynamic.ApplyCardButton(path,"OtherCard","Play Here")==null,"placement identity mismatch rejected");
 }
}
string smallPlacement="Canvas/Main Panel/CaptainCosmic/Inner Panel/Foreground Elements/Play Area/Scroll View/Viewport/Card Container/CardView CosmicCrest-1 SmallCardFront/Next To Selection Button/Selection Button Label";
Check(pilot.Dynamic.ApplyCardButton(smallPlacement.Replace("Main Panel/","Main Panel/3 Hero Layout/Inactive Hero Location 2/"),"CosmicCrest","Play Here")=="在此打出","same play-area role on inactive hero");
foreach(var path in new[]{smallPlacement.Replace("Next To Selection Button","HP"),smallPlacement.Replace("Play Area","Hand Area"),smallPlacement.Replace("CosmicCrest-1 ","CosmicCrest-1 suffix "),smallPlacement.Replace("SmallCardFront","SmallCardBack"),smallPlacement+"/Extra",smallPlacement.Replace("Canvas/Main Panel","Other/Main Panel"),smallPlacement.Replace("CosmicCrest-1 ","OtherCard-1 ")})
 Check(pilot.Dynamic.ApplyCardButton(path,"CosmicCrest","Play Here")==null,"placement scope rejected: "+path);
Check(pilot.Dynamic.ApplyCardButton(smallPlacement,"CosmicCrest","Play Here extra")==null,"placement requires complete source");
string adjacent=smallPlacement.Replace("Foreground Elements/Play Area/Scroll View/Viewport","Character Card Container/LegacyCharacter/Next To Location View");
Check(pilot.Dynamic.ApplyCardButton(adjacent,"CosmicCrest","Play Here")==null,"adjacent character must match hero identity");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.12.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  var actual=inCard?pilot.Dynamic.ApplyCardButton(path,card,source):pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.12 captured backlog: "+source);
  Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,actual!):pilot.Apply(path,actual!))==null,"0.1.13 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.13 exact markup preservation");
 }
}
foreach(var n in new[]{"0","1","2","17","999999"})
 Check(pilot.Apply(DynamicDisplayRules.PhasePath,"POWER PHASE ("+n+" left)")=="异能阶段（剩余"+n+"次）","power count preserved "+n);
foreach(var source in new[]{"POWER PHASE (-1 left)","POWER PHASE (1.5 left)","POWER PHASE (1000000 left)","POWER PHASE (1 LEFT)","POWER PHASE (1 left) extra","START PHASE (1 left)"})
 Check(pilot.Apply(DynamicDisplayRules.PhasePath,source)==null,"power count rejects unsupported grammar");
var phaseMap=new Dictionary<string,string>{["Start"]="开始阶段",["Play"]="出牌阶段",["Power"]="异能阶段",["Draw"]="抽牌阶段",["End"]="结束阶段",["Incapacitated"]="失能阶段"};
foreach(var phase in phaseMap){
 foreach(bool inplay in new[]{false,true}){
  string instruction="Click a card"+(inplay?" in play":"")+" to activate its <nobr><sprite name=\""+phase.Key+"Phase\"> <b>"+phase.Key.ToUpperInvariant()+" PHASE</b></nobr> effect.";
  string expected="点击一张"+(inplay?"在场的":"")+"牌，发动其<nobr><sprite name=\""+phase.Key+"Phase\"> <b>"+phase.Value+"</b></nobr>效果。";
  Check(pilot.Apply(SurfaceProbeRules.HintPath,instruction)==expected,"complete phase enum "+phase.Key);
  Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a power to use it. "+instruction)==(phase.Key=="Power"?"点击一个异能以使用它。 "+expected:null),"combined choice is bounded to power phase "+phase.Key);
 }
}
foreach(var source in new[]{"Click a power to use it. Unknown instruction.","Click a power to use it. Click a card to activate its <nobr><sprite name=\"PowerPhase\"> <b>DRAW PHASE</b></nobr> effect.","Click a power to use it. Click a card to activate its <nobr><sprite name=\"PowerPhase\"> <b>POWER PHASE</b></nobr> effect. Extra."})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source)==null,"no partial translation of unrecognized compound hint");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Card:</b> Click a power to use it. Click a card to activate its <nobr><sprite name=\"PowerPhase\"> <b>POWER PHASE</b></nobr> effect.")=="<b>Card:</b> 点击一个异能以使用它。 点击一张牌，发动其<nobr><sprite name=\"PowerPhase\"> <b>异能阶段</b></nobr>效果。","combined hint keeps optional source heading");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a target to determine which has the lowest HP.")=="点击一个目标，将其视为生命值最低的目标。","lowest HP selection without source header");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a target to determine which has the lowest HP (will not be dealt damage).")==null,"unknown suffix cannot drop damage immunity condition");
Check(pilot.Apply(DynamicDisplayRules.DescriptionPath,"Other Card discovered:")=="Other Card发现的牌：","discovery heading keeps source name");
Check(pilot.Apply(DynamicDisplayRules.DescriptionPath,"<b>Cosmic Crest</b> discovered:")==null,"unexpected discovery markup not stripped");
foreach(var source in new[]{"Play Construct","Discover Construct"}){
 foreach(var path in new[]{effectPath,"Full Card Canvas/Full Card Panel/CosmicCrest/Select Effect Container/Select Effect Button(Clone)/Label"})
  Check(pilot.Dynamic.ApplyCardButton(path,"CosmicCrest",source)==(source=="Play Construct"?"打出构造牌":"发现构造牌"),"construct action scoped to small/full effect button");
 Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Power Container/UsePower"),"CosmicCrest",source)==null,"construct action cannot rewrite power body");
}
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.13.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  var actual=inCard?pilot.Dynamic.ApplyCardButton(path,card,source):pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.13 observed surface: "+source);
  Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,actual!):pilot.Apply(path,actual!))==null,"0.1.14 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.14 markup preserved");
 }
}
for(int n=0;n<=8;n++){
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,"Destroy "+n+" "+(n==1?"card":"cards"))=="破坏"+n+"张牌","destroy confirmation tracks count "+n);
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Card:</b> Click up to "+n+" targets to destroy them.")=="<b>Card:</b> 点击至多"+n+"个目标以将其破坏。","destroy target count separate from cards "+n);
}
foreach(var source in new[]{"Destroy 1 cards","Destroy 2 card","Destroy 1 target","Destroy 2 targets","Destroy 2 HP","Destroy -1 cards","Destroy 1.5 cards"})
 Check(pilot.Apply(DynamicDisplayRules.FinishPath,source)==null,"destroy rejects wrong unit/plural/numeric form");
foreach(var slot in new[]{1,2,3,4}){
 string character="Canvas/Main Panel/5 Hero Layout/Inactive Hero Location "+slot+"/Legacy/Inner Panel/Character Card Container/LegacyCharacter/Foreground Elements/Next To Selection Button/Selection Button Label";
 string hero="Canvas/Main Panel/5 Hero Layout/Inactive Hero Location "+slot+"/Legacy/Inner Panel/Foreground Elements/Selection Button/Selection Button Label";
 Check(pilot.Dynamic.ApplyCardButton(character,"LegacyCharacter","Play Here")=="在此打出","five-hero placement slot "+slot);
 Check(pilot.Apply(hero,"Play a Card")=="打出一张牌","five-hero action slot "+slot);
 Check(pilot.Apply(hero,"Take Play Phase")=="进行出牌阶段","five-hero phase action slot "+slot);
 Check(pilot.Dynamic.ApplyCardButton(character,"RaCharacter","Play Here")==null,"five-hero mismatched character excluded");
}
string fiveHero="Canvas/Main Panel/5 Hero Layout/Inactive Hero Location 1/Legacy/Inner Panel/Foreground Elements/Selection Button/Selection Button Label";
foreach(var wrong in new[]{"5 Hero Layout/Inactive Hero Location 0","5 Hero Layout/Inactive Hero Location 5","3 Hero Layout/Inactive Hero Location 3","4 Hero Layout/Inactive Hero Location 4"})
 Check(pilot.Apply(fiveHero.Replace("5 Hero Layout/Inactive Hero Location 1",wrong),"Play a Card")==null,"layout/index pairing must be authorized");
string popup="Canvas/Revealed Cards Manager/[RevealCardsAction: Source=Captain Cosmic]/Title Label";
Check(pilot.Apply(popup,"Captain Cosmic revealed:")=="Captain Cosmic展示的牌：","reveal popup matches action owner");
Check(pilot.Apply(popup,"Legacy revealed:")==null,"reveal popup rejects source mismatch");
Check(pilot.Apply(popup,"Captain Cosmic discovered:")==null,"reveal popup rejects wrong action");
Check(pilot.Apply(popup.Replace("RevealCardsAction","OtherAction"),"Captain Cosmic revealed:")==null,"reveal popup rejects unknown action role");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click up to 3 cards in play to destroy them. Extra condition.")==null,"destroy hint never drops unknown condition");
Check(pilot.Apply("Canvas/Main Panel/Message Panel/Panel/Message Label","Baron Blade has been reduced to <nobr>17 HP.</nobr>")=="Baron Blade的生命值已降至<nobr>17点</nobr>。","HP result text preserves supplied number");
Check(pilot.Apply("Canvas/Main Panel/HP Label","Baron Blade has been reduced to <nobr>0 HP.</nobr>")==null,"HP result translation cannot target HP counter");
Check(pilot.Apply("Canvas/Main Panel/Button Panel/Left/Back Button/Text (TMP)","Back")==null,"button source newline must match exactly");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.14.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!;
  var actual=pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.14 observed source: "+source);
  Check(pilot.Apply(path,actual!)==null,"0.1.15 translated output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.15 optional heading markup preserved");
 }
}
foreach(var relation in new[]{"under","next to"}){
 string path=relation=="under"?DynamicDisplayRules.CardsUnderPath:DynamicDisplayRules.CardsNextToPath;
 Check(pilot.Apply(path,"Cards "+relation+" Test 2")=="Test 2"+(relation=="under"?"下方的牌":"旁边的牌"),"related card heading keeps numeric name");
 Check(pilot.Apply(path,"Cards "+(relation=="under"?"next to":"under")+" Test 2")==null,"related heading must agree with container");
 foreach(var name in new[]{""," ","<b>Card</b>","Card\nOther",new string('A',201)})
  Check(pilot.Apply(path,"Cards "+relation+" "+name)==null,"reject empty/markup/multiline/overlong related card name");
 Check(pilot.Apply(path,"Title Label")==null,"do not guess placeholder meaning");
 Check(pilot.Apply(path.Replace("Heading Label","HP Label"),"Cards "+relation+" Test")==null,"related headings cannot alter counters");
}
foreach(var pair in new[]{
 ("Click a card in the deck to collect it.","点击牌库中的一张牌以将其搜集。"),
 ("Click a card in the deck or in the trash to summon it.","点击牌库或弃牌堆中的一张牌以将其召唤。"),
 ("Click a target to prevent it from dealing damage.","点击一个目标，防止其造成伤害。"),
 ("Click a card in play to put Card 2 under it.","点击一张在场的牌，将Card 2置于其下方。"),
 ("Click a card in play or under Card 2 to destroy it.","点击一张在场或位于Card 2下方的牌以将其破坏。")}){
 Check(pilot.Apply(SurfaceProbeRules.HintPath,pair.Item1)==pair.Item2,"complete hint without optional source prefix");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,pair.Item1+" Extra condition.")==null,"never drop extra rule clause");
 Check(pilot.Apply(SurfaceProbeRules.HintPath.Replace("Hint Label","Body Label"),pair.Item1)==null,"hints excluded from rule body");
}
foreach(var source in new[]{"Click a target to prevent it from being dealt damage.","Click a card in the trash to collect it.","Click a card in the deck and in the trash to summon it.","Click a card in play or next to Card to destroy it.","Click a card in play to put <b>Card</b> under it.","Click a card in play or under <b>Card</b> to destroy it.","Click a card in play to put   under it."})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source)==null,"reject unsupported action, range or markup");
Check(pilot.Apply(DynamicDisplayRules.DescriptionPath,"Card 2 collected:")=="Card 2搜集的牌：","collected heading keeps name");
Check(pilot.Apply(popup,"Captain Cosmic collected:")==null,"collect heading cannot replace reveal action title");
Check(pilot.Apply(DynamicDisplayRules.DescriptionPath,"Card collected: extra")==null,"collect title complete string required");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.15.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  var actual=inCard?pilot.Dynamic.ApplyCardButton(path,card,source):pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.15 captured source: "+source);
  Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,actual!):pilot.Apply(path,actual!))==null,"0.1.16 translated output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.16 markup and source heading preserved");
 }
}
foreach(var n in new[]{0,1,2,3,6,12,999999}){
 string noun=n==1?"card":"cards",pronoun=n==1?"it":"them";
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click Unity's deck to draw "+n+" "+noun+".")=="点击Unity的牌库，抽"+n+"张牌。","owner draw number and singular preserved");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click "+n+" "+noun+" in play to destroy "+pronoun+".")=="点击"+n+"张在场的牌以将其破坏。","exact destroy amount does not become up to");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click up to "+n+" cards in hand to discard them.")=="点击至多"+n+"张手牌以将其弃置。","discard upper limit preserved");
}
foreach(var bad in new[]{"Click Unity's deck to draw 1 cards.","Click Unity's deck to draw 2 card.","Click Unity's deck to draw -2 cards.","Click Unity's deck to draw 1.5 cards.","Click Unity's deck to draw 1000000 cards.","Click 1 cards in play to destroy them.","Click 2 cards in play to destroy it.","Click 1 card in play to destroy them.","Click 2 targets in play to destroy them.","Click up to 6 cards in play to discard them.","Click 3 cards in play to destroy them. Extra condition.","Click a location to either replace and discard Card.","Click a location to either replace or discard <b>Card</b>."})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,bad)==null,"unsupported amount/grammar/range/markup not partially translated");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a location to either replace or discard Dr. Stinson's Secondary Lab.")=="选择一个区域，将Dr. Stinson's Secondary Lab放回原处或弃置。","replace meaning and literal apostrophe preserved");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click Dr. Stinson's deck to draw 2 cards.")=="点击Dr. Stinson的牌库，抽2张牌。","owner apostrophe not lost");
foreach(var phase in phaseMap){
 string source="Click a target to activate its <nobr><sprite name=\""+phase.Key+"Phase\"> <b>"+phase.Key.ToUpperInvariant()+" PHASE</b></nobr> effect.";
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source)=="点击一个目标，发动其<nobr><sprite name=\""+phase.Key+"Phase\"> <b>"+phase.Value+"</b></nobr>效果。","target distinct from card for phase "+phase.Key);
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source.Replace("<b>"+phase.Key.ToUpperInvariant()+" PHASE","<b>OTHER PHASE"))==null,"phase sprite and title must agree");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a power to use it. "+source)==null,"unobserved target compound not authorized");
}
foreach(var damage in new[]{("cold","寒冷"),("energy","能量"),("fire","火焰"),("infernal","邪能"),("lightning","闪电"),("melee","近战"),("projectile","投射"),("psychic","精神"),("radiant","光耀"),("sonic","音波"),("toxic","毒素")})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Card:</b> Click a target to deal it "+damage.Item1+" damage.")=="<b>Card:</b> 点击一个目标，对其造成"+damage.Item2+"伤害。","unspecified damage never invents amount "+damage.Item1);
Check(pilot.Apply(SurfaceProbeRules.HintPath,"<b>Card:</b> Click a target to deal it unknown damage.")==null,"unknown damage enum does not throw or rewrite");
string deckRole="Canvas/Main Panel/5 Hero Layout/Inactive Hero Location 4/Unity/Inner Panel/Foreground Elements/Inactive Deck View/Selection Button/Selection Button Label";
string trashRole=deckRole.Replace("Inactive Deck View","Bottom Area Panel/Inactive Trash View");
Check(pilot.Apply(deckRole,"Discard")==null,"discard cannot bind to deck destination");
Check(pilot.Apply(trashRole,"Replace")==null,"replace cannot bind to trash destination");
Check(pilot.Apply(trashRole,"Draw")==null,"draw cannot bind to trash destination");
foreach(var role in new[]{deckRole,trashRole}){
 foreach(var bad in new[]{"5 Hero Layout/Inactive Hero Location 0","5 Hero Layout/Inactive Hero Location 5","3 Hero Layout/Inactive Hero Location 3","4 Hero Layout/Inactive Hero Location 4"})
  Check(pilot.Apply(role.Replace("5 Hero Layout/Inactive Hero Location 4",bad),role==deckRole?"Replace":"Discard")==null,"invalid layout-slot pair rejected for destination button");
 Check(pilot.Apply(role.Replace("/Unity/","/UnknownHero/"),role==deckRole?"Draw":"Discard")==null,"unknown owner route excluded");
 Check(pilot.Apply(role.Replace("Selection Button Label","HP Label"),"Draw")==null,"numeric counter role excluded");
}
foreach(var action in new[]{"Discover Item","Discover Golem"}){
 Check(pilot.Dynamic.ApplyCardButton(effectPath,"FlashForge",action)==(action=="Discover Item"?"发现物品牌":"发现机偶牌"),"common discovery action uses reviewed subtype");
 Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Power Container/UsePower"),"FlashForge",action)==null,"discover action cannot rewrite rule body");
}
Check(pilot.Apply(DynamicDisplayRules.ContinuePath,"Skip Draw")=="跳过抽牌","skip draw action without assuming phase scope");
Check(pilot.Apply(DynamicDisplayRules.ContinuePath,"Skip Draw Everything")==null,"skip action complete source required");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.16.json"))){
 foreach(var sample in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  var actual=inCard?pilot.Dynamic.ApplyCardButton(path,card,source):pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.16 observed source: "+source);
  Check((inCard?pilot.Dynamic.ApplyCardButton(path,card,actual!):pilot.Apply(path,actual!))==null,"0.1.17 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.17 rich text preserved");
 }
}
foreach(var n in new[]{0,1,2,3,17,999999}){
 string noun=n==1?"card":"cards",pronoun=n==1?"it":"them";
 Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click "+n+" "+noun+" in play or under Card 2 to destroy "+pronoun+".")=="点击"+n+"张在场或位于Card 2下方的牌以将其破坏。","exact destruction keeps count and alternative areas");
}
foreach(var bad in new[]{"Click 1 cards in play or under Card to destroy them.","Click 2 cards in play or under Card to destroy it.","Click 1 card in play or under Card to destroy them.","Click -2 cards in play or under Card to destroy them.","Click 1.5 cards in play or under Card to destroy them.","Click 1000000 cards in play or under Card to destroy them.","Click 2 cards in play and under Card to destroy them.","Click 2 cards in play or under <b>Card</b> to destroy them.","Click 2 cards in play or under Card to destroy them. Extra."})
 Check(pilot.Apply(SurfaceProbeRules.HintPath,bad)==null,"reject malformed count, range, markup or trailing clause");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click Akash'Bhuta's trash to discard Card 2.")=="点击Akash'Bhuta的弃牌堆，将Card 2弃置。","nonhero apostrophe and numeric card name preserved");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click Omnitron's deck to discard Card.")==null,"discard location instruction cannot silently change deck to trash");
foreach(var source in new[]{"Click a card in the trash to put it under a card.","Click a hero to salvage a card.","Click a card in the trash to salvage it.","Click the hero who is considered to have the most cards in play."}){
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source)!=null,"complete fixed action without card heading");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,source+" Extra condition.")==null,"fixed action cannot drop condition");
}
foreach(var phase in phaseMap){
 string clause="Click a card to activate its <nobr><sprite name=\""+phase.Key+"Phase\"> <b>"+phase.Key.ToUpperInvariant()+" PHASE</b></nobr> effect.";
 string combinedSource="Click or drag a card to play it. "+clause;
 string? expected=phase.Key=="Play"?"点击或拖动一张牌以将其打出。 点击一张牌，发动其<nobr><sprite name=\"PlayPhase\"> <b>出牌阶段</b></nobr>效果。":null;
 Check(pilot.Apply(SurfaceProbeRules.HintPath,combinedSource)==expected,"play compound only accepts matching phase");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,combinedSource+" Extra.")==null,"compound never discards suffix");
 Check(pilot.Apply(SurfaceProbeRules.HintPath,combinedSource.Replace("Click a card to activate","Click a target to activate"))==null,"unobserved target compound remains excluded");
}
Check(pilot.Apply(fiveHero,"Has the Most")=="最多","most button does not invent hand or play area");
Check(pilot.Apply(fiveHero.Replace("/Legacy/","/Omnitron/"),"Has the Most")==null,"nonhero cannot inherit inactive hero selector");
Check(pilot.Dynamic.ApplyCardButton(effectPath,"UnityCharacter","Play Golem")=="打出机偶牌","play golem action uses reviewed subtype");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Power Container/UsePower"),"UnityCharacter","Play Golem")==null,"play golem cannot rewrite power body");
using(var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.17.json"))){
 foreach(var x in evidence.RootElement.GetProperty("samples").EnumerateArray()){
  string source=x.GetProperty("source").GetString()!,expected=x.GetProperty("expected").GetString()!;
  Check(pilot.Apply(SurfaceProbeRules.HintPath,source)==expected,"0.1.18 incoming damage selection");
  Check(pilot.Apply(SurfaceProbeRules.HintPath,expected)==null,"0.1.18 idempotent");
  Check(pilot.Apply(SurfaceProbeRules.HintPath,source+" Extra condition.")==null,"0.1.18 trailing clauses protected");
  Check(pilot.Apply(DynamicDisplayRules.FinishPath,source)==null,"0.1.18 wrong control excluded");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(expected,@"<[^<>]+>").Select(m=>m.Value)),"0.1.18 markup preserved");
 }
}
Console.WriteLine("RUNTIME_CHECKS_COMPLETE "+count);
// Exact names, card-title binding, and wrong-role guards use the same production functions.
using(var nameCases=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/deck-names-0.1.19.json")))
 foreach(var c in nameCases.RootElement.EnumerateArray()){
  string src=c.GetProperty("source").GetString()!;
  var expected=c.GetProperty("expected").GetString();
  bool inCard=c.GetProperty("inCardView").GetBoolean();
  bool bound=c.TryGetProperty("deckNameTitle",out var b)&&b.GetBoolean();
  string? actual=inCard?pilot.ApplyCardTitle(c.GetProperty("objectName").GetString()!,src,bound):pilot.Apply(c.GetProperty("path").GetString()!,src);
  Check(actual==expected,"0.1.19 deck name scope: "+c.GetProperty("path").GetString()+" / "+src);
 }
Check(pilot.DeckNames.Values.All(n=>n.All(c=>pilot.AllGlyphs().Contains(c))),"all approved name glyphs are preloaded");
using(var evidence032=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.32.json"))){
 foreach(var sample in evidence032.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!;
  string? actual=pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.32 full grammar output");
  Check(pilot.Apply(path,actual!)==null,"0.1.32 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.32 rich-text sequence preserved");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"[0-9]+").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"[0-9]+").Select(m=>m.Value)),"0.1.32 numeric sequence preserved");
 }
}
using(var evidence033=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/dynamic-0.1.33.json"))){
 foreach(var sample in evidence033.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!;
  string? actual=pilot.Apply(path,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.33 full grammar output");
  Check(pilot.Apply(path,actual!)==null,"0.1.33 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.33 rich-text sequence preserved");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"[0-9]+").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"[0-9]+").Select(m=>m.Value)),"0.1.33 numeric sequence preserved");
 }
}
var grantCompletion033=new RenderedDisplayRules(catalog,pilot.DeckNames,pilot.Templates);
string grantSource033="<style=PowerHeading>POWER: </style><style=PowerBody>This Hero deals 1 target 3 energy damage.\r\n</style>";
string grantPath033="Canvas/Main Panel/Tachyon/Inner Panel/Character Card Container/TachyonCharacter/Foreground Elements/Power Container/761/UsePower-Tachyon-TachyonCharacter/Label";
string grantTarget033="<style=PowerHeading>异能: </style><style=PowerBody>该英雄对1个目标造成3点能量伤害。\r\n</style>";
Check(grantCompletion033.Apply(grantPath033,"Label","TachyonCharacter",true,true,grantSource033)==grantTarget033,"granted power uses donor reviewed text on recipient button");
Check(grantCompletion033.Apply(grantPath033,"Label","TachyonCharacter",true,true,grantTarget033)==null,"granted power Chinese output idempotent");
Check(grantCompletion033.Apply(grantPath033,"Label","LegacyCharacter",true,true,grantSource033)==null,"granted power recipient identity mismatch rejected");
Check(grantCompletion033.Apply(grantPath033,"Label","TachyonCharacter",true,false,grantSource033)==null,"granted power requires actual button");
Check(grantCompletion033.Apply(grantPath033,"Label","TachyonCharacter",true,true,grantSource033.Replace("3 energy","4 energy"))==grantSource033.Replace("3 energy","4 energy").Replace("POWER: ","异能: "),"unreviewed granted amount never forced to fixed 3");
foreach(var t in pilot.Templates){
 string visible=System.Text.RegularExpressions.Regex.Replace(t.Value,@"<[^<>]+>","");
 Check(!System.Text.RegularExpressions.Regex.IsMatch(visible,"[A-Za-z]"),"approved dynamic template has no leftover English token: "+t.Key);
}
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click up to 2 targets to deal them <nobr>2 melee</nobr> damage.")=="点击至多2个目标，对其各造成<nobr>2点近战伤害</nobr>。","optional heading preserves separate target count and damage amount");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click a target to deal it <nobr>2 fire</nobr> damage.")=="点击一个目标，对其造成<nobr>2点火焰伤害</nobr>。","single target damage heading optional");
Check(pilot.Apply("Canvas/Other/Hint Label","Click up to 2 targets to deal them <nobr>2 melee</nobr> damage.")==null,"optional heading does not relax HUD path");
Check(pilot.Apply(SurfaceProbeRules.HintPath,"Click up to 2 targets to deal them <nobr>2 melee</nobr> damage. Extra.")==null,"optional heading does not drop unknown condition");
using(var evidence034=System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/fixtures/display-0.1.34.json"))){
 foreach(var sample in evidence034.RootElement.GetProperty("samples").EnumerateArray()){
  string path=sample.GetProperty("path").GetString()!,source=sample.GetProperty("source").GetString()!,obj=sample.GetProperty("objectName").GetString()!,card=sample.GetProperty("cardId").GetString()!;
  bool inCard=sample.GetProperty("inCardView").GetBoolean();
  string? actual=grantCompletion033.Apply(path,obj,card,inCard,false,source);
  Check(actual==sample.GetProperty("expected").GetString(),"0.1.34 complete scoped output");
  Check(grantCompletion033.Apply(path,obj,card,inCard,false,actual!)==null,"0.1.34 output idempotent");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"<[^<>]+>").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"<[^<>]+>").Select(m=>m.Value)),"0.1.34 sprite colors and rich-text sequence preserved");
  Check(string.Join("|",System.Text.RegularExpressions.Regex.Matches(source,@"[0-9]+").Select(m=>m.Value))==string.Join("|",System.Text.RegularExpressions.Regex.Matches(actual!,@"[0-9]+").Select(m=>m.Value)),"0.1.34 numeric sequence preserved");
 }
}
Check(SurfaceProbeRules.CanObserve("Rating Label",false,false),"new-game rating label observable");
Check(!SurfaceProbeRules.CanObserve("Rating Label",false,true),"rating input field remains protected");

foreach(var hero035 in pilot.DeckNames.Keys.Where(x=>new[]{"Legacy","Tachyon","Absolute Zero"}.Contains(x))){
 string hero=hero035.Replace(" ","");
 for(int slot=1;slot<=3;slot++){
  string path="Canvas/Main Panel/4 Hero Layout/Inactive Hero Location "+slot+"/"+hero+"/Inner Panel/Character Card Container/"+hero+"Character/Foreground Elements/Next To Selection Button/Selection Button Label";
  Check(pilot.Dynamic!.ApplyCardButton(path,hero+"Character","Play Here")=="在此打出","four-hero placement full source and identity");
  Check(pilot.Dynamic.ApplyCardButton(path,"WrongCharacter","Play Here")==null,"four-hero placement bound identity required");
 }
}
// Every known hero is hosted by the same view in current/3/4/5-player slots.
// Exhaust the role/action matrix independently of individual screenshot identities.
var heroActions035=new[]{("Draw","抽牌"),("Discard","弃置"),("Play a Card","打出一张牌"),("Use a Power","使用异能"),("Put a Card","放置一张牌"),("Destroy a Card","破坏一张牌"),("Has the Fewest","最少"),("Has the Most","最多"),("Take Play Phase","进行出牌阶段"),("Take Power Phase","进行异能阶段")};
var deckActions035=new[]{("Deck","牌库"),("Reveal","展示"),("Draw","抽牌"),("Replace","放回原处")};
var trashActions035=new[]{("Trash","弃牌堆"),("Discard","弃置"),("Reshuffle","洗回牌库")};
foreach(string hero in "AbsoluteZero Bunker CaptainCosmic Fanatic Haka Legacy Ra Tachyon Tempest TheArgentAdept TheWraith Unity".Split(' ')){
 foreach(int players in new[]{0,3,4,5})for(int slot=1;slot<=(players==0?1:players-1);slot++){
  string host="Canvas/Main Panel/"+(players==0?"":players+" Hero Layout/Inactive Hero Location "+slot+"/")+hero+"/Inner Panel/";
  string selection=host+"Foreground Elements/Selection Button/Selection Button Label";
  foreach(var a in heroActions035)Check(pilot.Apply(selection,a.Item1)==a.Item2,"3/4/5/current hero action "+hero+" "+players+"/"+slot+" "+a.Item1);
  string deck=host+"Foreground Elements/"+(players==0?"Deck and Trash/Active Deck View":"Inactive Deck View")+"/Selection Button/Selection Button Label";
  foreach(var a in deckActions035)Check(pilot.Apply(deck,a.Item1)==a.Item2,"3/4/5/current deck action "+a.Item1);
  string trash=host+"Foreground Elements/"+(players==0?"Deck and Trash/Active Trash View":"Bottom Area Panel/Inactive Trash View")+"/Selection Button/Selection Button Label";
  foreach(var a in trashActions035)Check(pilot.Apply(trash,a.Item1)==a.Item2,"3/4/5/current trash action "+a.Item1);
  string hand=host+"Foreground Elements/"+(players==0?"Hand":"Bottom Area Panel/Inactive Hand View")+"/Selection Button/Selection Button Label";
  Check(pilot.Apply(hand,"Put Here")=="放在此处","3/4/5/current hand destination");
  string character=host+"Character Card Container/"+hero+"Character/Foreground Elements/Next To Selection Button/Selection Button Label";
  Check(pilot.Dynamic!.ApplyCardButton(character,hero+"Character","Play Here")=="在此打出","3/4/5/current character destination");
  Check(pilot.Dynamic.ApplyCardButton(character,"WrongCharacter","Play Here")==null,"3/4/5/current mismatched destination identity excluded");
  foreach(string zone in new[]{"Foreground Elements/Play Area/Scroll View/Viewport/Card Container/","Character Card Container/"+hero+"Character/Next To Location View/Card Container/"}){
   string small=host+zone+"CardView CosmicCrest-1 SmallCardFront/Next To Selection Button/Selection Button Label";
   Check(pilot.Dynamic.ApplyCardButton(small,"CosmicCrest","Play Here")=="在此打出","3/4/5/current small-card destination");
   string effect=host+zone+"CardView CosmicCrest-1 SmallCardFront/Select Effect Container/Select Effect Button(Clone)/Label";
   Check(pilot.Dynamic.ApplyCardButton(effect,"CosmicCrest","Play Construct")=="打出构造牌","3/4/5/current play construct");
   Check(pilot.Dynamic.ApplyCardButton(effect,"CosmicCrest","Discover Construct")=="发现构造牌","3/4/5/current discover construct");
  }
  string power=host+"Character Card Container/"+hero+"Character/Foreground Elements/Power Container/761/UsePower-"+hero+"-"+hero+"Character/Label";
  Check(grantCompletion033.Apply(power,"Label",hero+"Character",true,true,grantSource033)==grantTarget033,"3/4/5/current granted power");
  Check(pilot.Apply(deck,"Discard")==null && pilot.Apply(trash,"Draw")==null && pilot.Apply(hand,"Play Here")==null,"3/4/5/current destination purposes separated");
  if(players>0)foreach(int invalidSlot035 in new[]{0,players}){
   string bad=character.Replace("Inactive Hero Location "+slot+"/","Inactive Hero Location "+invalidSlot035+"/");
   Check(pilot.Dynamic.ApplyCardButton(bad,hero+"Character","Play Here")==null,"layout-specific placement bounds retained");
  }
 }
}

// Stable semantic actions are independent of hierarchy, but never independent of Button/label identity.
var semantic036=new RenderedDisplayRules(catalog,pilot.DeckNames,pilot.Templates);
foreach(var obj in new[]{"Label","Text","Text (TMP)","Selection Button Label","Finish Button Label"})
 foreach(var source in new[]{"Play Here","PLAY HERE","  Play\nHere  ","Put Here","Skip Draw Phase","Play Construct","Discover Construct","Bury 2 cards","BURY 2 CARDS","Discard 1 card"}){
  string? value=semantic036.Apply("New Scene/New Layout/Action/"+obj,obj,"",false,true,source,null,false,true);
  Check(value!=null && DisplayScope.ContainsHan(value),"semantic full button phrase across unseen hosts");
  Check(semantic036.Apply("New Scene/"+obj,obj,"",false,true,value!,null,false,true)==null,"semantic fallback idempotent");
  Check(semantic036.Apply("New Scene/"+obj,obj,"",false,true,source)==null,"historical candidate alone is not Button proof");
  Check(semantic036.Apply("New Scene/"+obj,obj,"",false,false,source,null,false,true)==null,"inconsistent Button proof rejected");
 }
foreach(var obj in new[]{"Body Label","HP Label","Title Label","Input Field","Count Label"})
 Check(semantic036.Apply("New Scene/"+obj,obj,"",false,true,"Play Here",null,false,true)==null,"non-action role excluded even beneath Button");
foreach(var text in new[]{"Play","Draw","Power","Replace","Construct","Click Play Here","Play Here and draw 2 cards","<b>Play Here</b>","Bury 1 cards","Bury 2 card","Bury -1 card","Discard 2 targets","42"})
 Check(semantic036.Apply("New Scene/Label","Label","",false,true,text,null,false,true)==null,"semantic whole-phrase or grammar negative "+text);
Check(semantic036.Apply("New Scene/Label","Label","",false,true,"Discard 002 cards",null,false,true)=="弃置002张牌","literal number spelling retained");

// Inline styles switch away from the main TMP font: retain the style, attach fallback to its font.
foreach(var heading in new[]{"演奏","伴奏","制造","灭绝"}){
 string styled="<style=AbilityHeading><sprite name=\"Perform\"> "+heading+"</style>";
 Check(RenderedDisplayRules.RequiredInlineFonts(styled).SequenceEqual(new[]{"AvenirNextLTPro-HeavyCn"}),"ability heading font policy "+heading);
 Check(RenderedDisplayRules.RequiredInlineFonts(heading).Length==0,"plain Chinese does not load unrelated inline fonts");
}
Check(RenderedDisplayRules.RequiredInlineFonts("<style=BoldItalic>制造</style>").SequenceEqual(new[]{"AvenirNextLTPro-Bold"}),"inline rule ability font differs from heading font");
Check(RenderedDisplayRules.RequiredInlineFonts("<style=AbilityHeading>123</style>").Length==0,"numeric styled buffer has no font request");
Check(RenderedDisplayRules.RequiredInlineFonts("<style=Unrecognized>制造</style>").Length==0,"unknown styles are not rewritten or guessed");
foreach(var area in new[]{("Villain","反派"),("Environment","环境")}){
 string section="Canvas/Main Panel/"+area.Item1+"/Selected Area/Fade Panel/Title Label";
 Check(RenderedDisplayRules.IsInsetNewGameSection(section,"Title Label",area.Item2),"newgame section inset is bound to actual title role");
 Check(!RenderedDisplayRules.IsInsetNewGameSection(section,"Rating Label",area.Item2),"rating size/position untouched");
 Check(!RenderedDisplayRules.IsInsetNewGameSection("Other/Title Label","Title Label",area.Item2),"same word in another scene untouched");
}
foreach(var env in new[]{"Freedom Tower","Insula Primalis","Magmaria","Megalopolis","The Ruins of Atlantis","Wagner Mars Base"}){
 Check(pilot.ApplyEnvironmentDeckTitle("Title Label",env,true)==pilot.DeckNames[env],"environment deck title at bound preview/battle role "+env);
 Check(pilot.ApplyEnvironmentDeckTitle("Title Label",env,false)==null,"missing actual title identity rejected");
 Check(pilot.ApplyEnvironmentDeckTitle("HP Label",env,true)==null,"environment-name vocabulary does not authorize arbitrary controls");
 Check(pilot.ApplyEnvironmentDeckTitle("Title Label",env+" 4",true)==null,"full approved name only");
}
Check(pilot.ApplyEnvironmentDeckTitle("Title Label","Legacy",true)==null,"environment deck title cannot borrow hero role");
Check(pilot.Dynamic!.Apply("Canvas/Main Panel/Heading Label","The Wagner Mars Base exploded!")=="瓦格纳火星基地爆炸了！","Wagner special explosion heading already included");
Check(pilot.Dynamic.Apply("Canvas/Main Panel/Message Panel/Panel/Message Label","There are 0 cards under Self-Destruct Sequence.")=="Self-Destruct Sequence下方有0张牌。","Wagner zero-card reason with reviewed card name");
Check(pilot.ApplyEnvironmentDeckTitle("Title Label","  MAGMARIA  ",true)=="熔岩域","bound environment title accepts case and outer whitespace");
foreach(var env in new[]{"Freedom Tower","Insula Primalis","Magmaria","Megalopolis","The Ruins of Atlantis","Wagner Mars Base"}){
 Check(pilot.IsLocalizedEnvironmentTitle(pilot.DeckNames[env]),"dedicated title face accepts reviewed environment "+env);
 Check(!pilot.IsLocalizedEnvironmentTitle(pilot.DeckNames[env]+" 3"),"title face rejects count and extended sentence");
}
foreach(var source in new[]{"Legacy","传承","123","原初岛上方的牌",""})
 Check(!pilot.IsLocalizedEnvironmentTitle(source),"title font does not leak to body/hero/counter: "+source);
Console.WriteLine("Total checks including deck names: "+count);
