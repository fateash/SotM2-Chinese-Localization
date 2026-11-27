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
  ["button.draw.card"]=(0,"")
 };
 static Regex Rx(string pattern)=>new(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
 // The game renders this label uppercase, but captured m_text uses lowercase left.
 static readonly Regex PhaseRemaining=Rx(@"\A(?<phase>PLAY|DRAW) PHASE \((?<n>[0-9]{1,6}) left\)\z");
 static readonly Regex Confirmation=Rx(@"\A(?<action>[A-Za-z][A-Za-z -]{0,80}) (?<n>[0-9]{1,6}) (?<unit>target|targets|card|cards|HP)\z");
 public static ConfirmationParts? ParseConfirmation(string source){
  if(source.Length>256)return null;
  var m=Confirmation.Match(source);if(!m.Success)return null;
  string number=m.Groups["n"].Value,unit=m.Groups["unit"].Value;
  if(unit!="HP" && ((number=="1")!=(unit=="target" || unit=="card")))return null;
  return new(m.Groups["action"].Value,number,unit,unit=="HP"?"hp-amount":unit.StartsWith("card",StringComparison.Ordinal)?"card-count":"target-count");
 }
 static readonly Regex Header=Rx(@"\A(?<prefix><b>[^<>\r\n]{1,200}:</b> )(?<body>.+)\z");
 static readonly Regex Single=Rx(@"\AClick a target to deal it <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy)</nobr> damage\.\z");
 static readonly Regex Multiple=Rx(@"\AClick up to (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy)</nobr> damage\.\z");
 static readonly Regex Effect=Rx(@"\AClick a button on (?<card>[^<>\r\n]{1,200}) to choose an effect\.\z");
 static readonly Regex DrawDiscard=Rx(@"\AClick a location to either draw or discard (?<card>[^<>\r\n]{1,200})\.\z");
 static readonly Dictionary<string,string> DamageNames=new(StringComparer.Ordinal){["fire"]="火焰",["melee"]="近战",["cold"]="寒冷",["energy"]="能量"};
 static readonly Regex DiscardMany=Rx(@"\AClick (?<n>[0-9]{1,6}) cards in hand to discard them\.\z");
 static readonly Regex Reduce=Rx(@"\AClick up to (?<n>[0-9]{1,6}) targets to reduce damage dealt to them\.\z");
 static readonly Regex UnspecifiedDamage=Rx(@"\AClick a target to deal it (?<type>melee) damage\.\z");
 static readonly Regex DamageOrder=Rx(@"\AClick targets to choose the order of dealing them <nobr>(?<n>[0-9]{1,6}) (?<type>energy)</nobr> damage\.\z");
 static readonly Regex Reaction=Rx(@"\A<b>(?<from>[^<>\r\n]{1,200})</b> will deal <b>(?<to>[^<>\r\n]{1,200})</b> (?<n>[0-9]{1,6}) (?<type>energy) damage\. Click a reaction to use it\.\z");
 const string Heroes="AbsoluteZero|Bunker|CaptainCosmic|Fanatic|Haka|Legacy|Ra|Tachyon|Tempest|TheArgentAdept|TheWraith|Unity";
 static readonly Regex HeroSelection=Rx(@"\ACanvas/Main Panel/(?:3 Hero Layout/Inactive Hero Location [12]/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/Selection Button/Selection Button Label\z");
 static readonly Regex DeckSelection=Rx(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/(?:Deck and Trash/Active Deck View|Inactive Deck View)|Villain Row/BaronBlade/Inner Panel/Foreground Elements/Active Deck View|InsulaPrimalis/Inner Panel/Foreground Elements/Active Deck View)/Selection Button/Selection Button Label\z");
 static readonly Dictionary<string,Dictionary<string,string>> CardButtons=new(StringComparer.Ordinal){
  ["TheStaffOfRa"]=new(){["Double Damage"]="button.double.damage"},
  ["SynapticInterrupt"]=new(){["Draw Card"]="button.draw.card",["Play Card"]="button.play.card"},
  ["KeenVision"]=new(){["Draw Card"]="button.draw.card",["Use Power"]="button.use.power"},
  ["SilverShadow"]=new(){["Draw Card"]="button.draw.card",["Play Card"]="button.play.card"}
 };
 public string? ApplyCardButton(string path,string card,string source){
  if(!path.StartsWith("Canvas/Main Panel/",StringComparison.Ordinal) && !path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal))return null;
  if(!path.EndsWith("/Select Effect Container/Select Effect Button(Clone)/Label",StringComparison.Ordinal))return null;
  return CardButtons.TryGetValue(card,out var sources) && sources.TryGetValue(source,out var id)?Format(id):null;
 }
 static readonly Regex Description=Rx(@"\A(?<name>[^<>\r\n]{1,200}) (?<verb>drew|revealed|played):\z");
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
 public string Glyphs()=>string.Concat(templates.Values)+string.Concat(DamageNames.Values);
 public string? Apply(string path,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(path==PhasePath){
   var phase=PhaseRemaining.Match(source);
   return phase.Success?Format(phase.Groups["phase"].Value=="PLAY"?"phase.play.remaining":"phase.draw.remaining",phase.Groups["n"].Value):null;
  }
  if(path==FinishPath){
   var parts=ParseConfirmation(source);
   // Shared grammar does not authorize translating an unverified action/unit pair.
   return parts switch {
    {Action:"Damage",NumberKind:"target-count"}=>Format("finish.damage",parts.Number),
    {Action:"Discard",NumberKind:"card-count"}=>Format("finish.discard",parts.Number),
    {Action:"Select",NumberKind:"card-count"}=>Format("finish.select",parts.Number),_=>null};
  }
  if(path==ContinuePath)return source switch {"Skip Play Phase"=>Format("button.skip.play"),"Skip Power Phase"=>Format("button.skip.power"),_=>null};
  if(path==DrawPath && source=="Draw")return Format("button.draw");
  if(path==DiscardPath && source=="Discard")return Format("button.discard");
  if(HeroSelection.IsMatch(path))return source switch {"Play a Card"=>Format("button.play.card"),"Use a Power"=>Format("button.use.power"),"Put a Card"=>Format("button.put.card"),_=>null};
  if(DeckSelection.IsMatch(path) && source=="Reveal")return Format("button.reveal");
  if(path==DescriptionPath){
   var m=Description.Match(source);if(!m.Success)return null;
   return Format(m.Groups["verb"].Value switch {"drew"=>"description.draw","revealed"=>"description.reveal",_=>"description.play"},m.Groups["name"].Value);
  }
  if(path!=SurfaceProbeRules.HintPath)return null;
  var header=Header.Match(source);
  string prefix=header.Success?header.Groups["prefix"].Value:"",body=header.Success?header.Groups["body"].Value:source;
  var fixedText=body switch {
   SurfaceProbeRules.PlayHint=>"hint.play", "Click a card to play it."=>"hint.play.click",
   "Click or drag a power to use it."=>"hint.power.drag", "Click a power to use it."=>"hint.power.click",
   "Click a hero to play a card."=>"hint.hero.play", "Click a hero to use a power."=>"hint.hero.power",
   "Click an ability to use it."=>"hint.ability", "Click a card in the deck to summon it."=>"hint.summon",
   "Click a hero to have them put a card on top of their deck."=>"hint.hero.topdeck",
   "Click a card in the trash to put it on top of its deck."=>"hint.trash.topdeck",
   "Click a card in hand to discard it."=>"hint.discard.single", "Click a deck to reveal a card."=>"hint.deck.reveal",_=>null};
  if(fixedText!=null)return prefix+Format(fixedText);
  if(!header.Success)return null;
  var match=Single.Match(body);
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
