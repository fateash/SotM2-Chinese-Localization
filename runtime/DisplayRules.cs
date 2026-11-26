using System.Text.RegularExpressions;
namespace SotM2Chinese;
public sealed class DisplayEntry {
 public string ObjectName {get;set;}="";
 public string Card {get;set;}="";
 public string Mode {get;set;}="exact";
 public string Style {get;set;}="";
 public string Source {get;set;}="";
 public string Target {get;set;}="";
}
// Pure display transformation: never accesses the engine model, TMP or HP buffers.
public sealed class DisplayRules {
 private readonly Dictionary<string,DisplayEntry[]> groups;
 private static readonly HashSet<string> Objects=new(StringComparer.Ordinal) {
  "Title Label","Card Name Label","Flavor Text Label","Flavor Reference Label",
  "Keywords Label","Phase Label","Body Label","Setup Label","Advanced Label","Advanced Heading Label"
 };
 private static readonly HashSet<string> Styles=new(StringComparer.Ordinal) {
  "PowerTitle","PowerHeading","StartPhase","PlayPhase","PowerPhase","DrawPhase","EndPhase","IncapacitatedPhase"
 };
 private static readonly Regex Styled=new("(<style=(?<style>[A-Za-z]+)>)(?<value>[^<>]*)(</style>)",RegexOptions.CultureInvariant);
 private static readonly Regex InlinePhase=new("(<nobr><b>)(?<value>[^<>]*)(</b></nobr>)",RegexOptions.CultureInvariant);
 public DisplayRules(DisplayEntry[] entries) {
  var identities=new HashSet<string>(StringComparer.Ordinal);
  foreach(var e in entries) {
   if(!Objects.Contains(e.ObjectName) || string.IsNullOrEmpty(e.Source) || string.IsNullOrEmpty(e.Target))throw new InvalidDataException("Invalid display scope/text");
   if(e.Mode!="exact" && e.Mode!="style" && e.Mode!="keyword" && e.Mode!="phase-inline")throw new InvalidDataException("Unknown display mode");
   if(e.Mode=="style" && !Styles.Contains(e.Style))throw new InvalidDataException("Unknown display style");
   if(e.Mode=="keyword" && (e.ObjectName!="Keywords Label" || e.Source.IndexOfAny(new[]{',','<','>','\r','\n'})>=0))throw new InvalidDataException("Invalid keyword scope");
   if(e.Mode=="phase-inline" && !Regex.IsMatch(e.Source,@"^(Start|Play|Power|Draw|End|Incapacitated) Phase$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))throw new InvalidDataException("Invalid phase literal");
   var source=e.Mode=="keyword"?e.Source.ToUpperInvariant():e.Source;
   if(!identities.Add(e.ObjectName+"\0"+e.Card+"\0"+e.Mode+"\0"+e.Style+"\0"+source))throw new InvalidDataException("Duplicate display identity");
  }
  groups=entries.GroupBy(e=>e.ObjectName,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
 }
 public bool HasObject(string name)=>groups.ContainsKey(name);
 public string? Apply(string name,string card,string source) {
  if(!groups.TryGetValue(name,out var all))return null;
  var entries=all.Where(e=>e.Card.Length==0 || e.Card==card).ToArray();
  string? Lookup(string mode,string value,string style="") {
   var matches=entries.Where(e=>e.Mode==mode && e.Style==style && string.Equals(e.Source,value,mode=="keyword"?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)).ToArray();
   // Prefer card-specific entry; conflicting equal-specificity mappings fail closed.
   if(matches.Any(e=>e.Card.Length>0))matches=matches.Where(e=>e.Card.Length>0).ToArray();
   return matches.Length>0 && matches.All(e=>e.Target==matches[0].Target)?matches[0].Target:null;
  }
  var exact=Lookup("exact",source);
  if(exact!=null)return exact==source?null:exact;
  string ReplaceValue(Match m,string? target) {
   if(target==null)return m.Value;
   var v=m.Groups["value"];int i=v.Index-m.Index;
   return m.Value.Substring(0,i)+target+m.Value.Substring(i+v.Length);
  }
  string result=Styled.Replace(source,m=>ReplaceValue(m,Lookup("style",m.Groups["value"].Value,m.Groups["style"].Value)));
  result=InlinePhase.Replace(result,m=>ReplaceValue(m,Lookup("phase-inline",m.Groups["value"].Value)));
  if(name=="Keywords Label" && !result.Contains('<') && !result.Contains('>')) {
   result=Regex.Replace(result,@"[^,\r\n]+",m=> {
    var value=m.Value.Trim();var t=Lookup("keyword",value);if(t==null)return m.Value;
    int start=m.Value.IndexOf(value,StringComparison.Ordinal);
    return m.Value.Substring(0,start)+t+m.Value.Substring(start+value.Length);
   });
  }
  return result==source?null:result;
 }
}
