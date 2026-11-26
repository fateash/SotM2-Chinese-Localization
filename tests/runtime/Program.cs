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
Console.WriteLine("RUNTIME_CHECKS_COMPLETE "+count);
