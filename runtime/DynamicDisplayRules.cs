using System.Globalization;
using System.Text.RegularExpressions;
namespace SotM2Chinese;
// Opt-in pilot. Only complete observed surfaces/grammar; never touch game data.
public sealed record ConfirmationParts(string Action,string Number,string Unit,string NumberKind);
public sealed class DynamicDisplayRules {
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
  ["description.draw"]=(1,""),["description.reveal"]=(1,""),["description.play"]=(1,"")
 };
 static Regex Rx(string pattern)=>new(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
 static readonly Regex Confirmation=Rx(@"\A(?<action>[A-Za-z][A-Za-z -]{0,80}) (?<n>[0-9]{1,6}) (?<unit>target|targets|card|cards|HP)\z");
 public static ConfirmationParts? ParseConfirmation(string source){
  if(source.Length>256)return null;
  var m=Confirmation.Match(source);if(!m.Success)return null;
  string number=m.Groups["n"].Value,unit=m.Groups["unit"].Value;
  if(unit!="HP" && ((number=="1")!=(unit=="target" || unit=="card")))return null;
  return new(m.Groups["action"].Value,number,unit,unit=="HP"?"hp-amount":unit.StartsWith("card",StringComparison.Ordinal)?"card-count":"target-count");
 }
 static readonly Regex Header=Rx(@"\A(?<prefix><b>[^<>\r\n]{1,200}:</b> )(?<body>.+)\z");
 static readonly Regex Single=Rx(@"\AClick a target to deal it <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee)</nobr> damage\.\z");
 static readonly Regex Multiple=Rx(@"\AClick up to (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee)</nobr> damage\.\z");
 static readonly Regex Effect=Rx(@"\AClick a button on (?<card>[^<>\r\n]{1,200}) to choose an effect\.\z");
 static readonly Regex DrawDiscard=Rx(@"\AClick a location to either draw or discard (?<card>[^<>\r\n]{1,200})\.\z");
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
 public string Glyphs()=>string.Concat(templates.Values)+"火焰近战";
 public string? Apply(string path,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(path==FinishPath){
   var parts=ParseConfirmation(source);
   // Shared grammar does not authorize translating an unverified action/unit pair.
   return parts is {Action:"Damage",NumberKind:"target-count"}?Format("finish.damage",parts.Number):null;
  }
  if(path==ContinuePath)return source switch {"Skip Play Phase"=>Format("button.skip.play"),"Skip Power Phase"=>Format("button.skip.power"),_=>null};
  if(path==DrawPath && source=="Draw")return Format("button.draw");
  if(path==DiscardPath && source=="Discard")return Format("button.discard");
  if(path==DescriptionPath){
   var m=Description.Match(source);if(!m.Success)return null;
   return Format(m.Groups["verb"].Value switch {"drew"=>"description.draw","revealed"=>"description.reveal",_=>"description.play"},m.Groups["name"].Value);
  }
  if(path!=SurfaceProbeRules.HintPath)return null;
  var fixedText=source switch {
   SurfaceProbeRules.PlayHint=>"hint.play", "Click or drag a power to use it."=>"hint.power.drag", "Click a power to use it."=>"hint.power.click",_=>null};
  if(fixedText!=null)return Format(fixedText);
  var header=Header.Match(source);if(!header.Success)return null;
  string prefix=header.Groups["prefix"].Value,body=header.Groups["body"].Value;
  var match=Single.Match(body);
  if(match.Success)return Format("hint.attack.single",prefix,match.Groups["n"].Value,match.Groups["type"].Value=="fire"?"火焰":"近战");
  match=Multiple.Match(body);
  if(match.Success)return Format("hint.attack.multiple",prefix,match.Groups["count"].Value,match.Groups["n"].Value,match.Groups["type"].Value=="fire"?"火焰":"近战");
  match=Effect.Match(body);if(match.Success)return Format("hint.effect",prefix,match.Groups["card"].Value);
  match=DrawDiscard.Match(body);if(match.Success)return Format("hint.draw-discard",prefix,match.Groups["card"].Value);
  return null;
 }
}
