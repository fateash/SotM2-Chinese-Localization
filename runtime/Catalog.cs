using System.Text.Json;
using System.Text.RegularExpressions;
namespace SotM2Chinese;
public sealed class UiEntry { public string ObjectName {get;set;}=""; public string Source {get;set;}=""; public string Target {get;set;}=""; }
public sealed class CardEntry { public string Card {get;set;}=""; public string Source {get;set;}=""; public string Target {get;set;}=""; }
public sealed class Catalog {
 public int SchemaVersion {get;set;}=1;
 public string RuntimeVersion {get;set;}="0.1.4";
 public string BuildId {get;set;}="26880829";
 public DisplayEntry[] Display {get;set;}=Array.Empty<DisplayEntry>();
 [System.Text.Json.Serialization.JsonIgnore] public DisplayRules DisplayRules {get;private set;}=new(Array.Empty<DisplayEntry>());
 public UiEntry[] Ui {get;set;}=Array.Empty<UiEntry>();
 public CardEntry[] Cards {get;set;}=Array.Empty<CardEntry>();
 public static Catalog Read(string path) {
  var c=JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty catalog");
  if(c.SchemaVersion!=1 && c.SchemaVersion!=2)throw new InvalidDataException("Unknown schema");
  if(c.BuildId!="26880829" || (c.RuntimeVersion!="0.1.4" && c.RuntimeVersion!="0.1.6"))throw new InvalidDataException("Catalog build/runtime mismatch");
  if(c.Ui==null || c.Cards==null || c.Display==null)throw new InvalidDataException("Null catalog arrays");
  c.DisplayRules=new DisplayRules(c.Display);
  foreach(var e in c.Display)Validate(e.Source,e.Target);
  var keys=new HashSet<string>(StringComparer.Ordinal);
  foreach(var e in c.Ui) { Validate(e.Source,e.Target); if(e.ObjectName.Length==0 || !keys.Add("ui|"+e.ObjectName+"|"+e.Source)) throw new InvalidDataException("Duplicate/empty UI key"); }
  foreach(var e in c.Cards) { Validate(e.Source,e.Target); if(e.Card.Length==0 || !keys.Add("card|"+e.Card+"|"+e.Source)) throw new InvalidDataException("Duplicate/empty card key"); }
  return c;
 }
 static void Validate(string source,string target) {
  if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(target)) throw new InvalidDataException("Empty translation");
  string Tokens(string s)=>string.Join("\n",Regex.Matches(s,@"\{[^{}]+\}|<[^<>]+>").Cast<Match>().Select(m=>m.Value).OrderBy(s=>s,StringComparer.Ordinal));
  if(Tokens(source)!=Tokens(target)) throw new InvalidDataException("Token/tag mismatch: "+source);
 }
 public string? UiText(string name,string source)=>Ui.FirstOrDefault(e=>e.ObjectName==name&&e.Source==source)?.Target;
 public string? CardText(string card,string source,bool replacementsActive)=>replacementsActive ? null : Cards.FirstOrDefault(e=>e.Card==card&&e.Source==source)?.Target;
 // Match complete source lines only; retain all line breaks and unknown text verbatim.
 public string? CardBlock(string card,string source) {
  var parts=Regex.Split(source,@"(\r\n|\n|\r)"); bool changed=false;
  for(int i=0;i<parts.Length;i+=2) {
   var translated=CardText(card,parts[i],false);
   if(translated!=null) {parts[i]=translated;changed=true;}
  }
  return changed ? string.Concat(parts) : null;
 }
 // The native display-time replacement function is read-only. Check both languages:
 // unrelated/no-op entries must not suppress translation; effective changes retain English.
 public static bool ReplacementNeutral(string source,string translated,Func<string,string> apply) =>
  string.Equals(apply(source),source,StringComparison.Ordinal) &&
  string.Equals(apply(translated),translated,StringComparison.Ordinal);
 public string Glyphs()=>string.Concat(Ui.Select(e=>e.Target).Concat(Cards.Select(e=>e.Target)).Concat(Display.Select(e=>e.Target)));
}
