using System.Text.Json;
namespace SotM2Chinese;
// Diagnostic policy only; production inventories and game identifiers are unchanged.
public sealed class SurfaceProbeRules {
 public int SchemaVersion {get;set;}=1;
 public string BuildId {get;set;}="26880829";
 public Dictionary<string,string> Templates {get;set;}=new();
 [System.Text.Json.Serialization.JsonIgnore] public DynamicDisplayRules? Dynamic {get;private set;}
 public string AllGlyphs()=>Dynamic?.Glyphs()??Glyphs;
 public string[] DeckTitles {get;set;}=Array.Empty<string>();
 public const string HintPath="Canvas/HUD Panel/Hint Panel/Hint Box/Hint Label";
 public const string TurnPath="Canvas/HUD Panel/State Box/Turn Taker Box/Turn Taker Label";
 public const string PlayHint="Click or drag a card to play it.";
 public const string Glyphs="示·组·";
 static readonly HashSet<string> ObservedObjects=new(StringComparer.Ordinal){
  "Hint Label","Selection Button Label","Choose For Me Button Label","Skip Button Label",
  "Finish Button Label","Turn Taker Label","Deck Label","Heading Label","Info Label",
  "Message Label","Tooltip Label","Description Label","Title Label","Header","Heading",
  "Label","Text","Text (TMP)"
 };
 public static SurfaceProbeRules Read(string path){
  var p=JsonSerializer.Deserialize<SurfaceProbeRules>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty surface probe");
  if((p.SchemaVersion!=1 && p.SchemaVersion!=2) || p.BuildId!="26880829" || p.DeckTitles==null || p.DeckTitles.Length>24 || p.DeckTitles.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>100))throw new InvalidDataException("Invalid surface probe");
  if(p.SchemaVersion==2)p.Dynamic=new DynamicDisplayRules(p.Templates);
  return p;
 }
 public static bool CanObserve(string objectName,bool inCardView,bool inInputField)=>
  !inCardView && !inInputField && ObservedObjects.Contains(objectName);
 public string? Apply(string path,string source){
  if(SchemaVersion==2)return Dynamic?.Apply(path,source);
  // Full hierarchy and full source required: never replace names inside sentences.
  if(path==HintPath && source==PlayHint)return "示·"+source;
  if(path==TurnPath && DeckTitles.Contains(source,StringComparer.Ordinal))return "组·"+source;
  return null;
 }
}
