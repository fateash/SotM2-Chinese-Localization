using System.Globalization;
using System.Text.RegularExpressions;
namespace SotM2Chinese;
// Opt-in pilot. Only complete observed surfaces/grammar; never touch game data.
public sealed record ConfirmationParts(string Action,string Number,string Unit,string NumberKind);
public sealed class DynamicDisplayRules {
 public const string PhasePath="Canvas/HUD Panel/State Box/Phase Box/Phase Label";
 public const string FinishPath="Canvas/Main Panel/Button Panel/Finish Button/Finish Button Label";
 public const string ContinuePath="Canvas/Main Panel/Button Panel/Continue Button/Text (TMP)";
 public const string DescriptionPath="Canvas/Temporary Full Card Panel/Description Label";
 public const string DrawPath="Canvas/Main Panel/Tachyon/Inner Panel/Foreground Elements/Hand/Selection Button/Selection Button Label";
 public const string DiscardPath="Canvas/Main Panel/Tachyon/Inner Panel/Foreground Elements/Deck and Trash/Active Trash View/Selection Button/Selection Button Label";
 readonly Dictionary<string,string> templates;
 static readonly Dictionary<string,(int Args,string Tags)> Specs=new(){
  ["button.play.construct"]=(0,""),["button.discover.construct"]=(0,""),
  ["hint.lowest.hp"]=(0,""),["description.discover"]=(1,""),["phase.power.remaining"]=(1,""),
  ["button.common.1"]=(0,""),
  ["button.common.2"]=(0,""),
  ["button.common.3"]=(0,""),
  ["button.common.4"]=(0,""),
  ["button.common.5"]=(0,""),
  ["button.common.6"]=(0,""),
  ["button.common.7"]=(0,""),
  ["button.common.8"]=(0,""),
  ["button.common.9"]=(0,""),
  ["button.common.10"]=(0,""),
  ["button.common.11"]=(0,""),
  ["button.common.12"]=(0,""),
  ["button.common.13"]=(0,""),
  ["button.common.14"]=(0,""),
  ["button.common.15"]=(0,""),
  ["button.common.16"]=(0,""),
  ["button.common.17"]=(0,""),
  ["button.common.18"]=(0,""),
  ["button.common.19"]=(0,""),
  ["button.common.20"]=(0,""),
  ["button.common.21"]=(0,""),
  ["button.common.22"]=(0,""),
  ["button.common.23"]=(0,""),
  ["hint.selection.1"]=(0,""),
  ["hint.selection.2"]=(0,""),
  ["hint.selection.3"]=(0,""),
  ["hint.selection.4"]=(0,""),
  ["hint.selection.5"]=(0,""),
  ["hint.selection.6"]=(0,""),
  ["hint.selection.7"]=(0,""),
  ["hint.selection.8"]=(0,""),
  ["hint.selection.9"]=(0,""),
  ["hint.selection.10"]=(0,""),
  ["hint.selection.11"]=(0,""),
  ["hint.selection.12"]=(0,""),
  ["hint.selection.13"]=(0,""),
  ["hint.selection.14"]=(0,""),
  ["hint.selection.15"]=(0,""),
  ["hint.selection.16"]=(0,""),
  ["hint.selection.17"]=(0,""),
  ["hint.selection.18"]=(0,""),
  ["hint.selection.19"]=(0,""),
  ["hint.selection.20"]=(0,""),
  ["hint.selection.21"]=(0,""),
  ["hint.selection.22"]=(0,""),
  ["hint.selection.23"]=(0,""),
  ["hint.selection.24"]=(0,""),
  ["button.regain.hp"]=(1,""),
  ["button.deal.one"]=(1,""),
  ["button.deal.self"]=(1,""),
  ["button.deal.many"]=(1,""),
  ["button.deal.amount"]=(1,""),
  ["hint.play"]=(0,""),["hint.power.drag"]=(0,""),["hint.power.click"]=(0,""),
  ["finish.damage"]=(1,""),["hint.attack.single"]=(3,"<nobr></nobr>"),
  ["hint.attack.multiple"]=(4,"<nobr></nobr>"),["hint.effect"]=(2,""),
  ["hint.draw-discard"]=(2,""),["button.draw"]=(0,""),["button.discard"]=(0,""),
  ["button.skip.play"]=(0,""),["button.skip.power"]=(0,""),
  ["description.draw"]=(1,""),["description.reveal"]=(1,""),["description.play"]=(1,""),
  ["finish.discard"]=(1,""),
  ["finish.select"]=(1,""),
  ["phase.play.remaining"]=(1,""),["phase.draw.remaining"]=(1,""),
  ["hint.play.click"]=(0,""),
  ["hint.hero.play"]=(0,""),
  ["hint.hero.power"]=(0,""),
  ["hint.ability"]=(0,""),
  ["hint.summon"]=(0,""),
  ["hint.hero.topdeck"]=(0,""),
  ["hint.trash.topdeck"]=(0,""),
  ["hint.discard.single"]=(0,""),
  ["hint.deck.reveal"]=(0,""),
  ["hint.discard.multiple"]=(2,""),
  ["hint.reduce"]=(2,""),
  ["hint.attack.unspecified"]=(2,""),
  ["hint.attack.order"]=(3,"<nobr></nobr>"),
  ["hint.reaction"]=(5,"<b></b><b></b>"),
  ["button.play.card"]=(0,""),
  ["button.use.power"]=(0,""),
  ["button.put.card"]=(0,""),
  ["button.reveal"]=(0,""),
  ["button.double.damage"]=(0,""),
  ["button.draw.card"]=(0,""),
  ["hint.attack.exact"]=(4,"<nobr></nobr>"),["hint.attack.required"]=(5,"<b></b><nobr></nobr>"),
  ["hint.attack.order.unspecified"]=(2,""),["hint.play.location"]=(2,""),
  ["hint.hero.destroy"]=(0,""),["hint.card.destroy"]=(0,""),["hint.hero.fewest"]=(0,""),
  ["button.destroy.card"]=(0,""),["button.play.here"]=(0,""),["button.fewest"]=(0,""),
  ["button.skip"]=(0,""),["button.skip.play.action"]=(0,""),
  ["hint.phase.activate"]=(3,"<nobr><b></b></nobr>"),["hint.incapacitated"]=(0,""),
  ["hint.damage.type"]=(0,"")
 };
 static Regex Rx(string pattern)=>new(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
 // The game renders this label uppercase, but captured m_text uses lowercase left.
 static readonly Regex PhaseRemaining=Rx(@"\A(?<phase>PLAY|DRAW|POWER) PHASE \((?<n>[0-9]{1,6}) left\)\z");
 static readonly Regex Confirmation=Rx(@"\A(?<action>[A-Za-z][A-Za-z -]{0,80}) (?<n>[0-9]{1,6}) (?<unit>target|targets|card|cards|HP)\z");
 public static ConfirmationParts? ParseConfirmation(string source){
  if(source.Length>256)return null;
  var m=Confirmation.Match(source);if(!m.Success)return null;
  string number=m.Groups["n"].Value,unit=m.Groups["unit"].Value;
  if(unit!="HP" && ((number=="1")!=(unit=="target" || unit=="card")))return null;
  return new(m.Groups["action"].Value,number,unit,unit=="HP"?"hp-amount":unit.StartsWith("card",StringComparison.Ordinal)?"card-count":"target-count");
 }
 static readonly Regex Header=Rx(@"\A(?<prefix><b>[^<>\r\n]{1,200}:</b> )(?<body>.+)\z");
 static readonly Regex Single=Rx(@"\AClick a target to deal it <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Multiple=Rx(@"\AClick up to (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Effect=Rx(@"\AClick a button on (?<card>[^<>\r\n]{1,200}) to choose an effect\.\z");
 static readonly Regex DrawDiscard=Rx(@"\AClick a location to either draw or discard (?<card>[^<>\r\n]{1,200})\.\z");
 static readonly Dictionary<string,string> DamageNames=new(StringComparer.Ordinal){["fire"]="火焰",["melee"]="近战",["cold"]="寒冷",["energy"]="能量",["infernal"]="邪能",["lightning"]="闪电",["projectile"]="投射",["psychic"]="精神",["radiant"]="光耀",["sonic"]="音波",["toxic"]="毒素"};
 static readonly Regex DiscardMany=Rx(@"\AClick (?<n>[0-9]{1,6}) cards in hand to discard them\.\z");
 static readonly Regex Reduce=Rx(@"\AClick up to (?<n>[0-9]{1,6}) targets to reduce damage dealt to them\.\z");
 static readonly Regex UnspecifiedDamage=Rx(@"\AClick a target to deal it (?<type>melee) damage\.\z");
 static readonly Regex DamageOrder=Rx(@"\AClick targets to choose the order of dealing them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Reaction=Rx(@"\A<b>(?<from>[^<>\r\n]{1,200})</b> will deal <b>(?<to>[^<>\r\n]{1,200})</b> (?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic) damage\. Click a reaction to use it\.\z");
 static readonly Regex ExactTargets=Rx(@"\AClick (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex RequiredTarget=Rx(@"\AClick <b>(?<target>[^<>\r\n]{1,200})</b> and (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex UnspecifiedOrder=Rx(@"\AClick targets to choose the order of dealing them (?<type>[a-z]+) damage\.\z");
 // The repeated 'to' is present in the game's current complete display string.
 static readonly Regex PlayLocation=Rx(@"\AClick a location to to play (?<card>[^<>\r\n]{1,200}) there\.\z");
 static readonly Regex PhaseActivate=Rx("\\AClick a card(?<inplay> in play)? to activate its <nobr>(?<sprite><sprite name=\"(?<phase>Start|Play|Power|Draw|End|Incapacitated)Phase\">) <b>(?<title>[A-Z]+) PHASE</b></nobr> effect\\.\\z");
 static readonly Dictionary<string,string> PhaseNames=new(StringComparer.Ordinal){["Start"]="开始阶段",["Play"]="出牌阶段",["Power"]="异能阶段",["Draw"]="抽牌阶段",["End"]="结束阶段",["Incapacitated"]="失能阶段"};
 const string Heroes="AbsoluteZero|Bunker|CaptainCosmic|Fanatic|Haka|Legacy|Ra|Tachyon|Tempest|TheArgentAdept|TheWraith|Unity";
 static readonly Regex HeroSelection=Rx(@"\ACanvas/Main Panel/(?:3 Hero Layout/Inactive Hero Location [12]/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/Selection Button/Selection Button Label\z");
 static readonly Regex NextToCharacter=Rx(@"\ACanvas/Main Panel/(?:3 Hero Layout/Inactive Hero Location [12]/)?(?<hero>"+Heroes+@")/Inner Panel/Character Card Container/\k<hero>Character/Foreground Elements/Next To Selection Button/Selection Button Label\z");
 // Observed small-card placement buttons in the play area and beside a character.
 static readonly Regex NextToSmallCard=Rx(@"\ACanvas/Main Panel/(?:3 Hero Layout/Inactive Hero Location [12]/)?(?<hero>"+Heroes+@")/Inner Panel/(?:Foreground Elements/Play Area/Scroll View/Viewport/Card Container|Character Card Container/\k<hero>Character/Next To Location View/Card Container)/CardView (?<card>[A-Za-z][A-Za-z0-9_]{0,199})-[0-9]{1,6} SmallCardFront/Next To Selection Button/Selection Button Label\z");
 static readonly Regex DeckSelection=Rx(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/(?:Deck and Trash/Active Deck View|Inactive Deck View)|Villain Row/BaronBlade/Inner Panel/Foreground Elements/Active Deck View|InsulaPrimalis/Inner Panel/Foreground Elements/Active Deck View)/Selection Button/Selection Button Label\z");
 static readonly Dictionary<string,string> CommonButtons=new(StringComparer.Ordinal){["Play Construct"]="button.play.construct",["Discover Construct"]="button.discover.construct",["Cannot Deal Damage"]="button.common.1",["Deal Damage"]="button.common.2",["Deal Hero Targets Damage"]="button.common.3",["Deal cold damage"]="button.common.4",["Deal fire damage"]="button.common.5",["Destroy a Card"]="button.common.6",["Destroy this Card"]="button.common.7",["Double Damage"]="button.common.8",["Draw & Use Power"]="button.common.9",["Draw 2 Cards"]="button.common.10",["Draw Card"]="button.common.11",["Draw or Discard"]="button.common.12",["Play Card"]="button.common.13",["Play a Card"]="button.common.14",["Put Card Under Ordnance"]="button.common.15",["Put a Card"]="button.common.16",["Replace or Discard"]="button.common.17",["Salvage Item"]="button.common.18",["Salvage Ongoing"]="button.common.19",["Skip to Draw"]="button.common.20",["Skip to Power"]="button.common.21",["Use Power"]="button.common.22",["Use a Power"]="button.common.23"};
 static readonly Dictionary<string,string> SelectionTitles=new(StringComparer.Ordinal){["Select Damage Source to Deal Damage"]="hint.selection.1",["Select Hero to Discover"]="hint.selection.2",["Select Hero to destroy cards"]="hint.selection.3",["Select Hero to discard cards"]="hint.selection.4",["Select Hero to salvage cards"]="hint.selection.5",["Select Hero to take a phase now"]="hint.selection.6",["Select Location to play card to"]="hint.selection.7",["Select Location to play from"]="hint.selection.8",["Select Location to put a card from into the deck"]="hint.selection.9",["Select Location to put a card from on top of the deck"]="hint.selection.10",["Select Location to reveal from"]="hint.selection.11",["Select Location to shuffle its cards into the deck"]="hint.selection.12",["Select Redirection Target"]="hint.selection.13",["Select location to Summon from (decks will be shuffled)"]="hint.selection.14",["Select location to send destroyed card"]="hint.selection.15",["Select someone to put a card back in their deck"]="hint.selection.16",["Select someone to put a card back on top of their deck"]="hint.selection.17",["Select where it the card should be drawn or discarded."]="hint.selection.18",["Select where the card should go."]="hint.selection.19",["Select whether or not to draw a card."]="hint.selection.20",["Select which Hero is considered to have the most cards in hand"]="hint.selection.21",["Select which deck to draw from..."]="hint.selection.22",["Select which of these targets is considered to have the Lowest HP"]="hint.selection.23",["Select who is considered to have the most cards in play"]="hint.selection.24"};
 static readonly Regex EffectAmount=Rx(@"\A(?<before>Regain |Deal 1 Target |Deal Self |Deal )(?<n>[0-9]{1,6})(?<after> HP| Damage| damage| Targets 3 Damage)\z");
 string? CommonButton(string source){
  if(CommonButtons.TryGetValue(source,out var key))return Format(key);
  var m=EffectAmount.Match(source);if(!m.Success)return null;
  string n=m.Groups["n"].Value;
  return (m.Groups["before"].Value,m.Groups["after"].Value) switch {
   ("Regain "," HP")=>Format("button.regain.hp",n),
   ("Deal 1 Target "," Damage")=>Format("button.deal.one",n),
   ("Deal Self "," damage")=>Format("button.deal.self",n),
   ("Deal "," Targets 3 Damage")=>Format("button.deal.many",n),
   ("Deal "," damage")=>Format("button.deal.amount",n),_=>null};
 }
 public string? ApplyCardButton(string path,string card,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(source=="Play Here"){
   var next=NextToCharacter.Match(path);
   if(next.Success)return card==next.Groups["hero"].Value+"Character"?Format("button.play.here"):null;
   var small=NextToSmallCard.Match(path);
   return small.Success && card==small.Groups["card"].Value?Format("button.play.here"):null;
  }
  if(!path.StartsWith("Canvas/Main Panel/",StringComparison.Ordinal) && !path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal))return null;
  if(!path.EndsWith("/Select Effect Container/Select Effect Button(Clone)/Label",StringComparison.Ordinal))return null;
  return CommonButton(source);
 }
 static readonly Regex Description=Rx(@"\A(?<name>[^<>\r\n]{1,200}) (?<verb>drew|revealed|played|discovered):\z");
 public DynamicDisplayRules(Dictionary<string,string> values){
  if(values==null || values.Count!=Specs.Count)throw new InvalidDataException("Incomplete dynamic pilot templates");
  foreach(var (key,spec) in Specs){
   if(!values.TryGetValue(key,out var value) || string.IsNullOrWhiteSpace(value) || value.Length>512)throw new InvalidDataException("Missing pilot template: "+key);
   var tokens=Regex.Matches(value,@"\{[^{}]*\}").Select(m=>m.Value).OrderBy(x=>x);
   if(!tokens.SequenceEqual(Enumerable.Range(0,spec.Args).Select(i=>"{"+i+"}").OrderBy(x=>x)))throw new InvalidDataException("Pilot argument mismatch: "+key);
   if(Regex.Replace(value,@"\{[0-9]+\}","").IndexOfAny(new[]{'{','}'})>=0)throw new InvalidDataException("Escaped or stray pilot braces: "+key);
   if(string.Concat(Regex.Matches(value,@"<[^<>]*>").Select(m=>m.Value))!=spec.Tags)throw new InvalidDataException("Pilot markup mismatch: "+key);
   try{string.Format(CultureInfo.InvariantCulture,value,Enumerable.Repeat<object>("test",spec.Args).ToArray());}catch(FormatException){throw new InvalidDataException("Invalid pilot format: "+key);}
  }
  templates=new(values,StringComparer.Ordinal);
 }
 string Format(string id,params object[] args)=>string.Format(CultureInfo.InvariantCulture,templates[id],args);
 public string Glyphs()=>string.Concat(templates.Values)+string.Concat(DamageNames.Values)+string.Concat(PhaseNames.Values)+"在场";
 public string? Apply(string path,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(path==PhasePath){
   var phase=PhaseRemaining.Match(source);
   return phase.Success?Format(phase.Groups["phase"].Value switch {"PLAY"=>"phase.play.remaining","DRAW"=>"phase.draw.remaining",_=>"phase.power.remaining"},phase.Groups["n"].Value):null;
  }
  if(path==FinishPath){
   var parts=ParseConfirmation(source);
   // Shared grammar does not authorize translating an unverified action/unit pair.
   return parts switch {
    {Action:"Damage",NumberKind:"target-count"}=>Format("finish.damage",parts.Number),
    {Action:"Discard",NumberKind:"card-count"}=>Format("finish.discard",parts.Number),
    {Action:"Select",NumberKind:"card-count"}=>Format("finish.select",parts.Number),_=>null};
  }
  if(path==ContinuePath)return source switch {"Skip"=>Format("button.skip"),"Skip Play"=>Format("button.skip.play.action"),"Skip Play Phase"=>Format("button.skip.play"),"Skip Power Phase"=>Format("button.skip.power"),_=>null};
  if(path==DrawPath && source=="Draw")return Format("button.draw");
  if(path==DiscardPath && source=="Discard")return Format("button.discard");
  if(HeroSelection.IsMatch(path))return source switch {"Play a Card"=>Format("button.play.card"),"Use a Power"=>Format("button.use.power"),"Put a Card"=>Format("button.put.card"),"Destroy a Card"=>Format("button.destroy.card"),"Has the Fewest"=>Format("button.fewest"),_=>null};
  if(DeckSelection.IsMatch(path) && source=="Reveal")return Format("button.reveal");
  if(path==DescriptionPath){
   var m=Description.Match(source);if(!m.Success)return null;
   return Format(m.Groups["verb"].Value switch {"drew"=>"description.draw","revealed"=>"description.reveal","discovered"=>"description.discover",_=>"description.play"},m.Groups["name"].Value);
  }
  if(path!=SurfaceProbeRules.HintPath)return null;
  var header=Header.Match(source);
  string prefix=header.Success?header.Groups["prefix"].Value:"",body=header.Success?header.Groups["body"].Value:source;
  var fixedText=body switch {
   SurfaceProbeRules.PlayHint=>"hint.play", "Click a card to play it."=>"hint.play.click",
   "Click or drag a power to use it."=>"hint.power.drag", "Click a power to use it."=>"hint.power.click",
   "Click a hero to play a card."=>"hint.hero.play", "Click a hero to use a power."=>"hint.hero.power",
   "Click an incapacitated ability to use it."=>"hint.incapacitated", "Click a button to choose a damage type."=>"hint.damage.type",
   "Click a target to determine which has the lowest HP."=>"hint.lowest.hp",
   "Click a hero to destroy a card."=>"hint.hero.destroy", "Click a card in play to destroy it."=>"hint.card.destroy",
   "Click the hero who is considered to have the fewest cards in hand."=>"hint.hero.fewest",
   "Click an ability to use it."=>"hint.ability", "Click a card in the deck to summon it."=>"hint.summon",
   "Click a hero to have them put a card on top of their deck."=>"hint.hero.topdeck",
   "Click a card in the trash to put it on top of its deck."=>"hint.trash.topdeck",
   "Click a card in hand to discard it."=>"hint.discard.single", "Click a deck to reveal a card."=>"hint.deck.reveal",_=>null};
  if(fixedText!=null)return prefix+Format(fixedText);
  if(SelectionTitles.TryGetValue(body,out var selectionTitle))return prefix+Format(selectionTitle);
  var place=PlayLocation.Match(body);
  if(place.Success)return Format("hint.play.location",prefix,place.Groups["card"].Value);
  // Compose two complete recognized instructions; never translate a prefix of an unknown remainder.
  const string powerChoice="Click a power to use it. ";
  bool combined=body.StartsWith(powerChoice,StringComparison.Ordinal);
  var phaseHint=PhaseActivate.Match(combined?body.Substring(powerChoice.Length):body);
  if(phaseHint.Success && (!combined || phaseHint.Groups["phase"].Value=="Power") && phaseHint.Groups["title"].Value==phaseHint.Groups["phase"].Value.ToUpperInvariant())
   return prefix+(combined?Format("hint.power.click")+" ":"")+Format("hint.phase.activate",phaseHint.Groups["inplay"].Success?"在场的":"",phaseHint.Groups["sprite"].Value,PhaseNames[phaseHint.Groups["phase"].Value]);
  if(!header.Success)return null;
  var match=ExactTargets.Match(body);
  if(match.Success && DamageNames.TryGetValue(match.Groups["type"].Value,out var exactType))
   return Format("hint.attack.exact",prefix,match.Groups["count"].Value,match.Groups["n"].Value,exactType);
  match=RequiredTarget.Match(body);
  if(match.Success && ((match.Groups["count"].Value=="1")== (match.Groups["unit"].Value=="target")) && DamageNames.TryGetValue(match.Groups["type"].Value,out var requiredType))
   return Format("hint.attack.required",prefix,match.Groups["target"].Value,match.Groups["count"].Value,match.Groups["n"].Value,requiredType);
  match=UnspecifiedOrder.Match(body);
  if(match.Success && DamageNames.TryGetValue(match.Groups["type"].Value,out var orderType))return Format("hint.attack.order.unspecified",prefix,orderType);
  match=Single.Match(body);
  if(match.Success)return Format("hint.attack.single",prefix,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Multiple.Match(body);
  if(match.Success)return Format("hint.attack.multiple",prefix,match.Groups["count"].Value,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=DiscardMany.Match(body);if(match.Success)return Format("hint.discard.multiple",prefix,match.Groups["n"].Value);
  match=Reduce.Match(body);if(match.Success)return Format("hint.reduce",prefix,match.Groups["n"].Value);
  match=UnspecifiedDamage.Match(body);if(match.Success)return Format("hint.attack.unspecified",prefix,DamageNames[match.Groups["type"].Value]);
  match=DamageOrder.Match(body);if(match.Success)return Format("hint.attack.order",prefix,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Reaction.Match(body);if(match.Success)return Format("hint.reaction",prefix,match.Groups["from"].Value,match.Groups["to"].Value,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Effect.Match(body);if(match.Success)return Format("hint.effect",prefix,match.Groups["card"].Value);
  match=DrawDiscard.Match(body);if(match.Success)return Format("hint.draw-discard",prefix,match.Groups["card"].Value);
  return null;
 }
}
