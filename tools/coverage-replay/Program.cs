using System.Text.Json;
using System.Text.RegularExpressions;
using SotM2Chinese;

// Offline only: linked production display rules; no game, TMP, font or model calls.
if(args.Length!=5)throw new ArgumentException("catalog probe snapshot contracts output");
var catalog=Catalog.Read(args[0]);
var probe=SurfaceProbeRules.Read(args[1]);
using var snapshot=JsonDocument.Parse(File.ReadAllText(args[2]));
using var contracts=JsonDocument.Parse(File.ReadAllText(args[3]));
string Str(JsonElement x,string k)=>x.GetProperty(k).GetString()!;
string? Dynamic(JsonElement x,string source)=>x.GetProperty("inCardView").GetBoolean()
 ? probe.Dynamic!.ApplyCardButton(Str(x,"path"),Str(x,"cardId"),source)
 : probe.Apply(Str(x,"path"),source);
string? Match(JsonElement x,string source)=>catalog.UiText(Str(x,"objectName"),source)
 ?? catalog.DisplayRules.Apply(Str(x,"objectName"),Str(x,"cardId"),source) ?? Dynamic(x,source);
var contexts=contracts.RootElement.GetProperty("families").EnumerateArray()
 .SelectMany(f=>f.GetProperty("contexts").EnumerateArray())
 .Where(c=>c.TryGetProperty("case",out _)).Select(c=>c.GetProperty("case")).ToArray();
foreach(var c in contexts){
 var actual=Match(c,Str(c,"source"));
 var expected=c.GetProperty("expected").ValueKind==JsonValueKind.Null?null:Str(c,"expected");
 if(actual!=expected)throw new InvalidDataException("Coverage contract failed: "+Str(c,"path")+" / "+Str(c,"source"));
}
object Classify(JsonElement x){
 string source=Str(x,"source"),reason="unresolved-full-string";string? target=null;
 bool elsewhere=false;
 if(x.GetProperty("truncated").GetBoolean())reason="truncated-evidence";
 else if(Str(x,"inputSource")!="TextString" && Str(x,"inputSource")!="TextInputBox")reason="protected-input-buffer";
 else if(Regex.IsMatch(source,@"\A\s*[+-]?\d+(?:\.\d+)?\s*\z"))reason="protected-number";
 else {
  target=Match(x,source);
  if(target!=null && target!=source)reason="current-rule-match";
  else {
   string visible=Regex.Replace(source,@"<[^<>]*>","");
   if(DisplayScope.ContainsHan(visible) && !Regex.IsMatch(visible,"[A-Za-z]"))reason="localized-text-observed";
   else {
    elsewhere=catalog.Ui.Any(e=>e.Source==source) || catalog.Display.Any(e=>e.Mode=="exact" && e.Source==source)
     || contexts.Any(c=>Match(c,source)!=null);
    if(elsewhere)reason="known-source-route-gap";
   }
  }
 }
 return new {id=Str(x,"id"),reason,target,knownTranslationElsewhere=elsewhere,
  capturedDynamicMatch=x.GetProperty("markerCandidate").GetBoolean(),liveAccepted=false};
}
var rows=snapshot.RootElement.GetProperty("samples").EnumerateArray().Select(Classify).ToList();
foreach(var c in contracts.RootElement.GetProperty("classificationCases").EnumerateArray()){
 var classified=JsonSerializer.SerializeToElement(Classify(c));
 if(Str(classified,"reason")!=Str(c,"expectedReason"))throw new InvalidDataException("Classification contract failed: "+Str(c,"id"));
}
File.WriteAllText(args[4],JsonSerializer.Serialize(new{schemaVersion=1,contractChecks=contexts.Length,rows},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("COVERAGE_REPLAY_COMPLETE "+rows.Count+" samples; "+contexts.Length+" context contracts");
