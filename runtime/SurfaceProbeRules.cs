using System.Text.Json;
namespace SotM2Chinese;
// Diagnostic policy only; production inventories and game identifiers are unchanged.
public sealed class SurfaceProbeRules {
 public int SchemaVersion {get;set;}=1;
 public string BuildId {get;set;}="26880829";
 public Dictionary<string,string> Templates {get;set;}=new();
 [System.Text.Json.Serialization.JsonIgnore] public DynamicDisplayRules? Dynamic {get;private set;}
 public Dictionary<string,string> DeckNames {get;set;}=new(StringComparer.Ordinal);
 public string AllGlyphs()=>(Dynamic?.Glyphs()??Glyphs)+string.Concat(DeckNames.Values);
 public string[] DeckTitles {get;set;}=Array.Empty<string>();
 public const string HintPath="Canvas/HUD Panel/Hint Panel/Hint Box/Hint Label";
 public const string TurnPath="Canvas/HUD Panel/State Box/Turn Taker Box/Turn Taker Label";
 public const string AvailableTitlePath="Canvas/Available Area Popup/Available Area/New Game Available Item Container/New Game Available Item(Clone)/Title Label";
 public const string PlayHint="Click or drag a card to play it.";
 public const string Glyphs="示·组·";
 static readonly HashSet<string> ObservedObjects=new(StringComparer.Ordinal){
  "Phase Label","Hint Label","Selection Button Label","Choose For Me Button Label","Skip Button Label",
  "Finish Button Label","Turn Taker Label","Deck Label","Heading Label","Info Label",
  "Message Label","Tooltip Label","Description Label","Title Label","Header","Heading",
  "Label","Text","Text (TMP)","Abandon Label","Rating Label"
 };
 public static SurfaceProbeRules Read(string path){
  var p=JsonSerializer.Deserialize<SurfaceProbeRules>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty surface probe");
  if((p.SchemaVersion!=1 && p.SchemaVersion!=2) || p.BuildId!="26880829" || p.DeckTitles==null || p.DeckTitles.Length>24 || p.DeckTitles.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>100))throw new InvalidDataException("Invalid surface probe");
  if(p.DeckNames==null || p.DeckNames.Count>24 || p.DeckNames.Any(e=>!ValidName(e.Key)||!ValidName(e.Value)))throw new InvalidDataException("Invalid deck names");
  if(p.SchemaVersion==2)p.Dynamic=new DynamicDisplayRules(p.Templates);
  return p;
 }
 public static bool CanObserve(string objectName,bool inCardView,bool inInputField)=>
  !inCardView && !inInputField && ObservedObjects.Contains(objectName);
 static bool ValidName(string s)=>!string.IsNullOrWhiteSpace(s) && s.Length<=100 && !s.Any(c=>char.IsDigit(c)||char.IsControl(c)||"<>{}".Contains(c));
 // CardView.TitleLabel identity is checked against the actual TMP component by the caller.
 // The complete source identifies one approved deck; never replace a substring or model title.
 public string? ApplyCardTitle(string objectName,string source,bool isBoundTitle)=>
  SchemaVersion==2 && isBoundTitle && objectName=="Title Label" && DeckNames.TryGetValue(source,out var target)?target:null;
 public bool IsDeckName(string source)=>DeckNames.ContainsKey(source) || DeckNames.ContainsValue(source);
 static readonly Dictionary<string,string> VillainResultNames=new(StringComparer.Ordinal){["BaronBlade"]="Baron Blade",["TheMatriarch"]="The Matriarch",["AkashBhuta"]="Akash'Bhuta",["CitizenDawn"]="Citizen Dawn",["GrandWarlordVoss"]="Grand Warlord Voss",["Omnitron"]="Omnitron"};
 static readonly Dictionary<string,string> HeroResultNames=new(StringComparer.Ordinal){["AbsoluteZero"]="Absolute Zero",["Bunker"]="Bunker",["CaptainCosmic"]="Captain Cosmic",["Fanatic"]="Fanatic",["Haka"]="Haka",["Legacy"]="Legacy",["Ra"]="Ra",["Tachyon"]="Tachyon",["Tempest"]="Tempest",["TheArgentAdept"]="The Argent Adept",["TheWraith"]="The Wraith",["Unity"]="Unity"};
 bool IsNamePath(string path,string source)=>path==TurnPath || path==AvailableTitlePath ||
  HeroResultNames.Any(e=>path=="Canvas/Main Panel/Hero Container/"+e.Key+"/Title And HP Tooltip/Title Label" && source==e.Value) ||
  VillainResultNames.Any(e=>path=="Canvas/Main Panel/"+e.Key+"/Title BG/Title Label" && source==e.Value) ||
  path=="Canvas/Main Panel/Villain View/Title BG/Title Label" ||
  path=="Canvas/Main Panel/Environment Background/Title Label" ||
  Enumerable.Range(1,5).Any(i=>path=="Canvas/Main Panel/Hero Container/End Game Hero View ("+i+")/Title And HP Tooltip/Title Label");
 public string? ApplyDeckName(string path,string source)=>
  SchemaVersion==2 && DeckNames.TryGetValue(source,out var target) && IsNamePath(path,source)?target:null;
 public string? Apply(string path,string source){
  if(SchemaVersion==2){
   return ApplyDeckName(path,source) ?? Dynamic?.Apply(path,source);
  }
  // Full hierarchy and full source required: never replace names inside sentences.
  if(path==HintPath && source==PlayHint)return "示·"+source;
  if(path==TurnPath && DeckTitles.Contains(source,StringComparer.Ordinal))return "组·"+source;
  return null;
 }
}
