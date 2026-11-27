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
Check(pilot.Apply(SurfaceProbeRules.TurnPath,"Ra")==null,"pilot removes prior HUD marker");
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
foreach(var input in new[]{"<b>Ra:</b> Click a target to deal it <nobr>2 unknown</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>-2 fire</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>2 fire</nobr> damage. Extra", "Click a target to deal it <nobr>2 fire</nobr> damage."})
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
  Check(pilot.Dynamic.ApplyCardButton(path,"CosmicCrest",source)==(source=="Play Construct"?"打出Construct牌":"发现Construct牌"),"construct action scoped to small/full effect button");
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
foreach(var wrong in new[]{"5 Hero Layout/Inactive Hero Location 0","5 Hero Layout/Inactive Hero Location 5","3 Hero Layout/Inactive Hero Location 3","4 Hero Layout/Inactive Hero Location 1"})
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
Console.WriteLine("RUNTIME_CHECKS_COMPLETE "+count);
