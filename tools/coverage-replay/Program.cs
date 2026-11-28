using System.Text.Json;
using System.Text.RegularExpressions;
using SotM2Chinese;

// Offline only: linked production display rules; no game, TMP, font or model calls.
if(args.Length!=5)throw new ArgumentException("catalog probe snapshot contracts output");
var catalog=Catalog.Read(args[0]);
var probe=SurfaceProbeRules.Read(args[1]);
var completion=new RenderedDisplayRules(catalog,probe.DeckNames);
using var snapshot=JsonDocument.Parse(File.ReadAllText(args[2]));
using var contracts=JsonDocument.Parse(File.ReadAllText(args[3]));
var conditionalCatalogs=new Dictionary<string,DisplayRules>(StringComparer.Ordinal);
if(contracts.RootElement.TryGetProperty("conditionalCatalogs",out var conditional))
 foreach(var entry in conditional.EnumerateArray())
  conditionalCatalogs.Add(entry.GetProperty("id").GetString()!,new DisplayRules(entry.GetProperty("displayEntries").Deserialize<DisplayEntry[]>()!));
string Str(JsonElement x,string k)=>x.GetProperty(k).GetString()!;
string? Dynamic(JsonElement x,string source)=>x.GetProperty("inCardView").GetBoolean()
 ? probe.ApplyCardTitle(Str(x,"objectName"),source,x.TryGetProperty("deckNameTitle",out var nt) && nt.GetBoolean()) ?? probe.Dynamic!.ApplyCardButton(Str(x,"path"),Str(x,"cardId"),source)
 : probe.Apply(Str(x,"path"),source);
string? BaseMatch(JsonElement x,string source)=>x.TryGetProperty("catalogId",out var cid)
 ? conditionalCatalogs[cid.GetString()!].Apply(Str(x,"objectName"),Str(x,"cardId"),source)
 : (x.GetProperty("inCardView").GetBoolean()
    ? probe.ApplyCardTitle(Str(x,"objectName"),source,x.TryGetProperty("deckNameTitle",out var nt) && nt.GetBoolean())
    : probe.ApplyDeckName(Str(x,"path"),source))
   ?? catalog.UiText(Str(x,"objectName"),source) ?? catalog.DisplayRules.Apply(Str(x,"objectName"),Str(x,"cardId"),source) ?? Dynamic(x,source);
string? Match(JsonElement x,string source){
 var previous=BaseMatch(x,source);
 if(x.TryGetProperty("catalogId",out _))return previous;
 return completion.Apply(Str(x,"path"),Str(x,"objectName"),Str(x,"cardId"),x.GetProperty("inCardView").GetBoolean(),x.TryGetProperty("cardButtonCandidate",out var b) && b.GetBoolean(),source,previous,x.TryGetProperty("effectTooltip",out var et) && et.GetBoolean())??previous;
}
var contexts=contracts.RootElement.GetProperty("families").EnumerateArray()
 .SelectMany(f=>f.GetProperty("contexts").EnumerateArray())
 .Where(c=>c.TryGetProperty("case",out _)).Select(c=>c.GetProperty("case")).ToArray();
foreach(var c in contexts){
 bool dynamicOnly=c.TryGetProperty("matchLayer",out var layer) && layer.GetString()=="dynamic";
 var actual=dynamicOnly?Dynamic(c,Str(c,"source")):Match(c,Str(c,"source"));
 if(dynamicOnly && Match(c,Str(c,"source"))!=Str(c,"integratedExpected"))
  throw new InvalidDataException("Integrated catalog contract failed: "+Str(c,"path")+" / "+Str(c,"source"));
 var expected=c.GetProperty("expected").ValueKind==JsonValueKind.Null?null:Str(c,"expected");
 if(actual!=expected)throw new InvalidDataException("Coverage contract failed: "+Str(c,"path")+" / "+Str(c,"source"));
}
// Recognize a translated mixed-language observation only when a complete source
// at the same control produces exactly that output. A Chinese suffix alone is insufficient.
var provenOutputs=new HashSet<(string Path,string Card,string Name,bool InCard,string Text)>();
foreach(var x in snapshot.RootElement.GetProperty("samples").EnumerateArray()){
 if(x.GetProperty("truncated").GetBoolean() || (Str(x,"inputSource")!="TextString" && Str(x,"inputSource")!="TextInputBox"))continue;
 // Preserve proof of pre-completion output in historical sessions; origin is not full localization.
 foreach(var output in new[]{Match(x,Str(x,"source")),BaseMatch(x,Str(x,"source"))})
  if(output!=null && output!=Str(x,"source"))provenOutputs.Add((Str(x,"path"),Str(x,"cardId"),Str(x,"objectName"),x.GetProperty("inCardView").GetBoolean(),output));
}
object Classify(JsonElement x){
 string source=Str(x,"source"),reason="unresolved-full-string";string? target=null;
 bool elsewhere=false;
 if(x.GetProperty("truncated").GetBoolean())reason="truncated-evidence";
 else if(Str(x,"inputSource")!="TextString" && Str(x,"inputSource")!="TextInputBox")reason="protected-input-buffer";
 else if(Regex.IsMatch(source,@"\A\s*[+-]?\d+(?:\.\d+)?\s*\z"))reason="protected-number";
 else if(Regex.IsMatch(source,@"\A<size=75%>[+-]</size><space=-0\.1em>[0-9]{1,6}\z"))reason="protected-number-markup";
 else {
  target=Match(x,source);
  if(target!=null && target!=source)reason="current-rule-match";
  else {
   string visible=Regex.Replace(source,@"<[^<>]*>","");
   if(DisplayScope.ContainsHan(visible) && !Regex.IsMatch(visible,"[A-Za-z]"))reason="localized-text-observed";
   else if(provenOutputs.Contains((Str(x,"path"),Str(x,"cardId"),Str(x,"objectName"),x.GetProperty("inCardView").GetBoolean(),source)))reason="translated-output-observed";
   else {
    elsewhere=catalog.Ui.Any(e=>e.Source==source) || catalog.Display.Any(e=>e.Mode=="exact" && e.Source==source)
     || contexts.Where(c=>!c.TryGetProperty("catalogId",out _)).Any(c=>Match(c,source)!=null);
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
