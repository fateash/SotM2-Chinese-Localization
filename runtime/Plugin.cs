using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Handelabra.Sentinels2.View;
using Handelabra.Sentinels2.Engine.Controller;
using UObj=UnityEngine.Object;
namespace SotM2Chinese;

[BepInPlugin("sotm2.zh.prototype", "SotM2 Chinese Prototype", "0.1.26")]
public sealed class Plugin:BasePlugin {
 internal static Plugin Instance=null!;
 internal static Catalog Texts=new();
 internal static SurfaceProbe? Surface;
 internal static bool Chinese, Ready;
 internal static HashSet<string> UiSources=new(StringComparer.Ordinal);
 internal static TMP_FontAsset? Cjk;
 internal static string Root="";
 internal static readonly HashSet<string> Events=new(StringComparer.Ordinal);
 internal static DateTime Started=DateTime.UtcNow;
 public override void Load() {
  Instance=this; Root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
  Chinese=Config.Bind("Prototype","EnableChinese",false,"Enable limited Chinese display; restart game after changing.").Value;
  Texts=Catalog.Read(Path.Combine(Root,"sample-texts.json"));
  var surfacePath=Path.Combine(Root,"surface-probe.json");
  if(File.Exists(surfacePath)){Surface=new SurfaceProbe(surfacePath);Log.LogInfo("SURFACE_PROBE_ENABLED configured-pilot bounded-capture");}
  UiSources=new HashSet<string>(Texts.Ui.Select(e=>e.Source),StringComparer.Ordinal);
  Log.LogInfo("PROTOTYPE_LOADED 0.1.26-rc.1 build=26880829 mode="+(Chinese?"Chinese":"Probe"));
  // Resolve types/methods without patching the game model or rule engine.
  var method=typeof(TextHelper).GetMethods().Single(m=>m.Name=="ReplaceTemplatesAndInsertSpecifics"&&m.GetParameters().Length==8);
  Log.LogInfo("DISPLAY_METHOD_RESOLVED "+method);
  AddComponent<ProbeBehaviour>();
  if(Chinese) {
   var harmony=new Harmony("sotm2.zh.prototype");
   harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(CardPrefix)));
   foreach(var render in new[]{
    typeof(TextMeshProUGUI).GetMethod("OnPreRenderCanvas",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),
    typeof(TextMeshPro).GetMethod("OnPreRenderObject",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)}) {
    if(render==null || render.GetParameters().Length!=0) throw new MissingMethodException("TMP pre-render entry not found");
    harmony.Patch(render,prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeTextRender)));
    Log.LogInfo("UI_RENDER_HOOK_INSTALLED "+render.DeclaringType!.Name+"."+render.Name);
   }
  }
  Log.LogInfo("PROTOTYPE_READY component registered; awaiting Unity Update");
 }
 internal static readonly Dictionary<string,string> LastCardState=new(StringComparer.Ordinal);
 internal static void CardState(string card,string source,string state) {
  string key=card+"|"+source;
  if(LastCardState.Count>=100 && !LastCardState.ContainsKey(key))return;
  if(!LastCardState.TryGetValue(key,out var previous) || previous!=state) {
   LastCardState[key]=state;
   if(cardStateLogs++<200)Instance.Log.LogInfo("CARD_DISPLAY_STATE card="+card+" state="+state+" previous="+(previous??"none"));
  }
 }
 private static int cardStateLogs;
 [ThreadStatic] private static bool inRender;
 public static void BeforeTextRender(TMP_Text __instance) {
  if(!Chinese || inRender || __instance==null)return;
  inRender=true;
  try {
   if(!TryReadStringLabel(__instance,out var source)){Surface?.ObserveBlockedTitle(__instance);return;}
   if(__instance.GetComponentInParent<TMP_InputField>()!=null)return;
   bool approvedDeckName=false;
   var marker=Surface?.ObserveAndApply(__instance,source,out approvedDeckName);
   var translated=UiSources.Contains(source)?Texts.UiText(__instance.gameObject.name,source):null;
   if(translated==null && Texts.DisplayRules.HasObject(__instance.gameObject.name)) {
    string card="";
    var view=__instance.GetComponentInParent<CardView>();
    if(view!=null && view.Card!=null && view.Card.Card!=null)card=view.Card.Card.CardDefinition.Identifier;
    translated=Texts.DisplayRules.Apply(__instance.gameObject.name,card,source);
   }
   // Reviewed names must also win over an older installed catalog at the bound name role.
   if(approvedDeckName)translated=marker;
   else translated ??= marker;
   translated=Surface?.Complete(__instance,source,translated)??translated;
   // Numeric / formatted buffers are excluded before reading the backing string.
   if(translated==null && !DisplayScope.ContainsHan(source))return;
   if(__instance.GetComponentInParent<TMP_InputField>()!=null)return;
   if(!FontSupport.EnsureReady())return;
   FontSupport.AttachFallback(__instance);
   if(translated==null)return;
   __instance.text=translated;
   if(marker!=null && translated==marker)Once("surface-applied|"+__instance.gameObject.name+source,"SURFACE_DISPLAY_APPLIED object="+__instance.gameObject.name+" source="+Clip(source));
   Once("render-ui|"+__instance.gameObject.name+source,"UI_TRANSLATED_BEFORE_RENDER object="+__instance.gameObject.name+" source="+Clip(source));
  } catch(Exception e) {Once("render-error","UI_RENDER_FALLBACK "+e);}
  finally {inRender=false;}
 }
 // TMP get_text is NOT a pure read in this pinned game build: it clears the
 // backing-string dirty flag without updating m_text. Do not call it, including
 // from diagnostics. Formatted HP / counters and array writers belong to TMP.
 internal static bool TryReadStringLabel(TMP_Text text,out string source) {
  source="";
  var input=text.m_inputSource;
  if(input!=TMP_Text.TextInputSources.TextInputBox && input!=TMP_Text.TextInputSources.TextString) {
   Once("buffer-bypass|"+input,"DYNAMIC_TEXT_BYPASS input="+input+" policy=no-text-getter-no-font-no-write");
   return false;
  }
  if(text.m_IsTextBackingStringDirty)return false;
  source=text.m_text;
  return !string.IsNullOrEmpty(source);
 }
 internal static string Clip(string? text) => JsonSerializer.Serialize(text==null?null:text.Length>640?text.Substring(0,640)+"…":text);
 internal static void Once(string key,string message) {
  if(Events.Count<500 && Events.Add(key)) Instance.Log.LogInfo(message);
 }
 // Argument replacement affects only this display call. The stored English model is never changed.
 public static void CardPrefix(CardController __0,GameTextController __1,ref string __2) {
  if(!Chinese || string.IsNullOrEmpty(__2))return;
  if(__0==null || __1==null) {
   var input=__2;
   if(Texts.Cards.Any(e=>e.Source==input)) Once("card-input|"+__2,"CARD_INPUT_NOT_READY hasCard="+(__0!=null)+" hasGameText="+(__1!=null)+" source="+Clip(__2));
   return;
  }
  try {
   var card=__0.Card.CardDefinition.Identifier;
   if(!Texts.Cards.Any(e=>e.Card==card)) return;
   Once("card-hook|"+card,"CARD_HOOK_SEEN card="+card);
   if(!FontSupport.EnsureReady()) {CardState(card,__2,"English-font-not-ready");return;}
   var source=__2;
   var translated=Texts.CardBlock(card,source);
   if(translated==null) { CardState(card,source,"English-not-in-catalog");Once("unmatched|"+card+source,"CARD_TEXT_UNMATCHED card="+card+" source="+Clip(source)); return; }
   var model=__1.GameText;
   if(model==null) { CardState(card,source,"English-no-game-text-model");Once("model-missing|"+card,"CARD_FALLBACK card="+card+" reason=no-GameText-model"); return; }
   string? effectiveEnglish=null,effectiveChinese=null;
   bool neutral=Catalog.ReplacementNeutral(source,translated,text=> {
    string value=model.PerformReplacementsOnGameText(text);
    if(text==source) effectiveEnglish=value; else effectiveChinese=value;
    return value;
   });
   if(!neutral) {
    CardState(card,source,"English-effective-rule-change");
    Once("dynamic|"+card+source,"DYNAMIC_FALLBACK card="+card+" reason=effective-text-change source="+Clip(source)+" after="+Clip(effectiveEnglish)+" candidateAfter="+Clip(effectiveChinese));
    return;
   }
   __2=translated;
   CardState(card,source,"Chinese");
   Once("card|"+card+source,"CARD_TRANSLATED card="+card+" guard=effective-replacement-neutral source="+Clip(source));
  } catch(Exception e) { Once("card-error", "CARD_FALLBACK "+e); }
 }
}

public sealed class ProbeBehaviour:MonoBehaviour {
 public ProbeBehaviour(IntPtr ptr):base(ptr) { }
 private long next;

 private bool stopped;
 public void Update() {
  try {
   Plugin.Once("callback-entry","UNITY_CALLBACK_ENTER before Unity API calls");
   if(stopped) return;
   if(Plugin.Chinese)FontSupport.EnsureReady();
   long now=System.Diagnostics.Stopwatch.GetTimestamp();
   if(now<next) return;
   next=now+System.Diagnostics.Stopwatch.Frequency;
   Plugin.Once("update","UNITY_UPDATE_OK");
   var texts=Resources.FindObjectsOfTypeAll<TMP_Text>();
   int active=0, translated=0;
   foreach(var t in texts) {
    if(t==null || t.gameObject==null || !t.gameObject.activeInHierarchy || !t.gameObject.scene.IsValid()) continue;
    active++;
    if(!Plugin.Chinese || !Plugin.Ready || !Plugin.TryReadStringLabel(t,out var current))continue;
    var zh=Plugin.UiSources.Contains(current)?Plugin.Texts.UiText(t.gameObject.name,current):null;
    if(zh==null && !DisplayScope.ContainsHan(current))continue;
    // The scanner obeys the same scope as the pre-render hook; HP never reaches here.
    if(t.GetComponentInParent<TMP_InputField>()!=null)continue;
    FontSupport.AttachFallback(t);
    if(zh!=null) { t.text=zh; t.SetAllDirty(); translated++; Plugin.Once("ui|"+t.gameObject.name+current,"UI_TRANSLATED object="+t.gameObject.name+" source="+current.Replace("\n","\\n")); }
   }
   Plugin.Once("scan","UI_SCAN_OK activeTMP="+active+" legacyUI="+Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>().Length);
   if(active>0) Plugin.Once("active-ui","ACTIVE_UI_FOUND count="+active);
   FontSupport.KeepFontAlive();
  } catch(Exception ex) { Plugin.Ready=false; Plugin.Once("update-error", "UPDATE_ERROR "+ex); stopped=true; }
 }
 }

internal static class FontSupport {
 private static bool preparing;
 private static long nextAttempt;
 private static int generation;
 internal static bool EnsureReady() {
  if(!Plugin.Chinese)return false;
  if(Plugin.Ready && Plugin.Cjk!=null)return true;
  if(preparing)return false;
  long now=System.Diagnostics.Stopwatch.GetTimestamp();
  if(now<nextAttempt)return false;
  nextAttempt=now+System.Diagnostics.Stopwatch.Frequency/4;
  preparing=true;
  try {
   if(Plugin.Ready) {
    Plugin.Ready=false;
    Plugin.Instance.Log.LogWarning("FONT_LIFETIME_LOST; rebuilding before text display");
   }
   if(TMP_Settings.instance==null)return false;
   if(Plugin.Cjk==null) {
    Plugin.Cjk=TMP_FontAsset.CreateFontAsset(Path.Combine(Plugin.Root,"NotoSansCJKsc-Regular.otf"),0,64,8,GlyphRenderMode.SDFAA,2048,2048);
    if(Plugin.Cjk==null) {Plugin.Once("font-retry","FONT_CREATE_RETURNED_NULL");return false;}
    Plugin.Cjk.name="SotM2 CJK Prototype";
    Preserve(Plugin.Cjk);
    if(Plugin.Cjk.material!=null)Preserve(Plugin.Cjk.material);
    generation++;
   }
   var chars=new string((Plugin.Texts.Glyphs()+(Plugin.Surface?.Glyphs()??"")).Where(c=>c>127).Distinct().ToArray());
   bool ok=Plugin.Cjk.TryAddCharacters(chars,out string missing,true);
   if(!ok || !string.IsNullOrEmpty(missing)) {Plugin.Once("font-missing","FONT_GLYPHS_MISSING "+missing);return false;}
   KeepFontAlive();Plugin.Ready=true;
   Plugin.Instance.Log.LogInfo("FONT_READY generation="+generation+" glyphs="+chars.Length+" atlas=2048 multiAtlas="+Plugin.Cjk.isMultiAtlasTexturesEnabled);
   return true;
  } catch(Exception e) {Plugin.Ready=false;Plugin.Once("font-error","FONT_INIT_FALLBACK "+e);return false;}
  finally {preparing=false;}
 }
 private static void Preserve(UObj asset) {
  asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
  UObj.DontDestroyOnLoad(asset);
 }
 internal static void AttachFallback(TMP_Text text) {
  var font=text.font;
  if(font==null || font==Plugin.Cjk)return;
  var list=font.fallbackFontAssetTable;
  if(list==null) {list=new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();font.fallbackFontAssetTable=list;}
  if(!list.Contains(Plugin.Cjk!)){list.Add(Plugin.Cjk!);text.SetAllDirty();}
 }
 internal static void KeepFontAlive() {
  if(Plugin.Cjk==null)return;
  foreach(var tex in Plugin.Cjk.atlasTextures)if(tex!=null)Preserve(tex);
 }
}
