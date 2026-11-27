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
foreach(var input in new[]{"<b>Ra:</b> Click a target to deal it <nobr>2 toxic</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>-2 fire</nobr> damage.","<b>Ra:</b> Click a target to deal it <nobr>2 fire</nobr> damage. Extra", "Click a target to deal it <nobr>2 fire</nobr> damage."})
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
Check(pilot.Dynamic.ApplyCardButton(effectPath,"OtherCard","Double Damage")==null,"other card cannot reuse effect label mapping");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Effect Panel/Effect View(Clone)"),"TheStaffOfRa","Double Damage")==null,"effect status labels excluded");
Check(pilot.Dynamic.ApplyCardButton(effectPath.Replace("Select Effect Container/Select Effect Button(Clone)","Power Container/UsePower"),"TheStaffOfRa","Double Damage")==null,"power body labels excluded");
Check(pilot.Apply("Other/Selection Button Label","Play a Card")==null,"hero button requires observed hierarchy role");
Check(pilot.Apply("Canvas/Main Panel/Legacy/Inner Panel/Foreground Elements/Selection Button/Selection Button Label","Play a Card")=="打出一张牌","active hero role supported");
Check(SurfaceProbeRules.CanObserve("Phase Label",false,false),"phase remaining label enabled for capture");
Check(pilot.Apply("Canvas/HUD Panel/State Box/Phase Box/Phase Label","DRAW PHASE (2 LEFT)")==null,"phase suffix remains capture-only pending raw text evidence");
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
Console.WriteLine("RUNTIME_CHECKS_COMPLETE "+count);
