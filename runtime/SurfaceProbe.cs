using System.Text.Json;
using TMPro;
using Handelabra.Sentinels2.View;
namespace SotM2Chinese;
internal sealed class SurfaceProbe {
 readonly SurfaceProbeRules rules;
 readonly RenderedDisplayRules completion;
 internal string Glyphs()=>rules.AllGlyphs()+RenderedDisplayRules.Glyphs;
 readonly HashSet<string> seen=new(StringComparer.Ordinal);
 readonly Dictionary<string,int> counts=new(StringComparer.Ordinal);
 bool stopped;
 internal SurfaceProbe(string path){rules=SurfaceProbeRules.Read(path);completion=new RenderedDisplayRules(Plugin.Texts,rules.DeckNames,rules.Templates);}
 // Diagnostic metadata only: never read buffered title text, mutate flags, or touch HP.
 readonly HashSet<string> blockedTitles=new(StringComparer.Ordinal);
 internal void ObserveBlockedTitle(TMP_Text text){
  try {
   if(blockedTitles.Count>=24 || text.gameObject.name!="Title Label" || text.GetComponentInParent<TMP_InputField>()!=null)return;
   var view=text.GetComponentInParent<CardView>();
   if(view==null || (view.TitleLabel!=text && !IsEnvironmentDeckTitle(text)))return;
   string path=PathOf(text);
   if(blockedTitles.Add(path))Plugin.Instance.Log.LogInfo("TITLE_INPUT_PROBE "+JsonSerializer.Serialize(new{
    path,inputSource=text.m_inputSource.ToString(),backingStringDirty=text.m_IsTextBackingStringDirty,
    isDeckBack=view.IsDeckBack,cardId=view.Card?.Card?.CardDefinition.Identifier??"",policy="metadata-only-no-text-read-no-write"}));
  }catch(Exception e){Plugin.Once("title-input-probe-error","TITLE_INPUT_PROBE_STOPPED "+e.GetType().Name);}
 }
 readonly HashSet<string> titleSnapshots=new(StringComparer.Ordinal);
 readonly HashSet<string> titleInputs=new(StringComparer.Ordinal);
 internal void ObserveTitleInput(TMP_Text text,string argument){
  try {
  if(titleInputs.Count>=80 || text.gameObject.name.IndexOf("title",StringComparison.OrdinalIgnoreCase)<0 || text.GetComponentInParent<TMP_InputField>()!=null)return;
  string path=PathOf(text);var view=text.GetComponentInParent<CardView>();var back=text.GetComponentInParent<CardBackView>();
  string clipped=argument.Length>160?argument.Substring(0,160):argument;
  string key=path+"|"+clipped;
  if(titleInputs.Add(key))Plugin.Instance.Log.LogInfo("ENV_TITLE_ARGUMENT_DIAGNOSTIC "+JsonSerializer.Serialize(new{
   path,argument=clipped,truncated=argument.Length>160,baseView=view!=null,directBackView=back!=null,
   isDeckBack=view!=null && view.IsDeckBack,boundTitle=view!=null && view.TitleLabel==text,policy="original-string-argument-only-observer"}));

  }catch(Exception e){Plugin.Once("ObserveTitleInput-error","ENV_TITLE_DIAGNOSTIC_STOPPED ObserveTitleInput "+e.GetType().Name);}
 }
 internal void ObserveTitleSnapshot(TMP_Text text){
  try {
  if(titleSnapshots.Count>=100 || text.gameObject.name.IndexOf("title",StringComparison.OrdinalIgnoreCase)<0 || text.GetComponentInParent<TMP_InputField>()!=null)return;
  string path=PathOf(text);var view=text.GetComponentInParent<CardView>();var back=text.GetComponentInParent<CardBackView>();
  bool safe=Plugin.TryReadStringLabel(text,out var source);
  string key=path+"|"+text.m_inputSource+"|"+text.m_IsTextBackingStringDirty+"|"+safe;
  if(titleSnapshots.Add(key))Plugin.Instance.Log.LogInfo("ENV_TITLE_SNAPSHOT_DIAGNOSTIC "+JsonSerializer.Serialize(new{
   path,baseView=view!=null,directBackView=back!=null,isDeckBack=view!=null && view.IsDeckBack,
   boundTitle=view!=null && view.TitleLabel==text,backBoundTitle=back!=null && back.TitleLabel==text,
   inputSource=text.m_inputSource.ToString(),backingStringDirty=text.m_IsTextBackingStringDirty,
   safeSource=safe?(source.Length>160?source.Substring(0,160):source):null,
   policy="active-title-observer-no-buffer-read-no-write"}));

  }catch(Exception e){Plugin.Once("ObserveTitleSnapshot-error","ENV_TITLE_DIAGNOSTIC_STOPPED ObserveTitleSnapshot "+e.GetType().Name);}
 }
 readonly HashSet<string> backSnapshots=new(StringComparer.Ordinal);
 internal void UpdateBackViews(){
  try {
   foreach(var back in UnityEngine.Resources.FindObjectsOfTypeAll<CardBackView>())
    if(back!=null && back.gameObject.activeInHierarchy && back.gameObject.scene.IsValid()){
     ObserveBackSnapshot(back);
     RefreshEnvironmentTitle(back);
    }
  }catch(Exception e){Plugin.Once("back-scan-error","ENV_TITLE_DIAGNOSTIC_STOPPED back-scan "+e.GetType().Name);}
 }
 readonly HashSet<string> titleWrites=new(StringComparer.Ordinal);
 readonly HashSet<string> titlePersistence=new(StringComparer.Ordinal);
 internal void RefreshEnvironmentTitle(CardBackView view){
  try {
   var title=view.TitleLabel;
   if(!Plugin.Chinese || title==null || !title.gameObject.activeInHierarchy ||
      title.GetComponentInParent<TMP_InputField>()!=null || !Plugin.TryReadStringLabel(title,out var source))return;
   bool bound=SurfaceProbeRules.IsEnvironmentTitleRole(title.gameObject.name,
    title.transform.parent?.name??"",view.IsDeckBack,view.TitleLabel==title);
   var target=rules.ApplyEnvironmentDeckTitle(title.gameObject.name,source,bound);
   string path=PathOf(title);
   if(target==null){
    if(bound && rules.IsDeckName(source) && titleWrites.Contains(path) && titlePersistence.Count<40 && titlePersistence.Add(path))
     Plugin.Instance.Log.LogInfo("ENV_TITLE_PERSISTED "+JsonSerializer.Serialize(new{path,source}));
    return;
   }
   if(!FontSupport.EnsureReady())return;
   FontSupport.AttachFallback(title,target);
   title.text=target;
   title.SetAllDirty();
   if(titleWrites.Count<40 && titleWrites.Add(path)){
    bool readable=Plugin.TryReadStringLabel(title,out var written);
    Plugin.Instance.Log.LogInfo("ENV_TITLE_BOUND_WRITE "+JsonSerializer.Serialize(new{
     path,source,target,readable,written=readable?written:null,
     inputSource=title.m_inputSource.ToString(),backingStringDirty=title.m_IsTextBackingStringDirty,
     identity="active CardBackView.TitleLabel",policy="safe-string-only"}));
   }
  }catch(Exception e){Plugin.Once("bound-title-write-error","ENV_TITLE_BOUND_WRITE_ERROR "+e);}
 }
 internal void ObserveBackSnapshot(CardBackView view){
  try {
  if(backSnapshots.Count>=40)return;
  var title=view.TitleLabel;string path=title!=null?PathOf(title):view.gameObject.name;
  string card=view.Card?.Card?.CardDefinition?.Identifier??"";
  string key=path+"|"+card;
  if(backSnapshots.Add(key))Plugin.Instance.Log.LogInfo("ENV_BACK_SNAPSHOT_DIAGNOSTIC "+JsonSerializer.Serialize(new{
   path,card,isDeckBack=view.IsDeckBack,viewType=view.CardViewType.ToString(),hasTitle=title!=null,
   titleName=title?.gameObject.name,titleActive=title!=null && title.gameObject.activeInHierarchy,
   policy="view-bindings-only-no-text-read-no-write"}));

  }catch(Exception e){Plugin.Once("ObserveBackSnapshot-error","ENV_TITLE_DIAGNOSTIC_STOPPED ObserveBackSnapshot "+e.GetType().Name);}
 }
 internal static bool IsEnvironmentDeckTitle(TMP_Text text){
  if(text.gameObject.name!="Title Label" || text.GetComponentInParent<TMP_InputField>()!=null)return false;
  var view=text.GetComponentInParent<CardView>();
  return view!=null && SurfaceProbeRules.IsEnvironmentTitleRole(text.gameObject.name,
   text.transform.parent?.name??"",view.IsDeckBack,view.TitleLabel==text);
 }
 internal string? EnvironmentTitleInput(TMP_Text text,string source){
  var target=rules.ApplyEnvironmentDeckTitle(text.gameObject.name,source,IsEnvironmentDeckTitle(text));
  if(target!=null)Plugin.Once("environment-title-input|"+PathOf(text)+source,"ENVIRONMENT_TITLE_INPUT "+JsonSerializer.Serialize(new{path=PathOf(text),source,target,identity="CardView.IsDeckBack + (TitleLabel binding or environment title child)",policy="string-argument-only-no-buffer-read"}));
  return target;
 }
 internal void ApplySectionLayout(TMP_Text text,string source){
  if(RenderedDisplayRules.IsInsetNewGameRating(PathOf(text),text.gameObject.name,source)){
   var rating=text.rectTransform;
   // Same right/top anchoring as the serialized controls; expand leftward, never outward.
   var anchor=new UnityEngine.Vector2(1,1);var ratingPosition=new UnityEngine.Vector2(-10,-8);var ratingSize=new UnityEngine.Vector2(160,42);
   if(rating.anchorMin!=anchor)rating.anchorMin=anchor;
   if(rating.anchorMax!=anchor)rating.anchorMax=anchor;
   if(rating.pivot!=anchor)rating.pivot=anchor;
   if(rating.anchoredPosition!=ratingPosition)rating.anchoredPosition=ratingPosition;
   if(rating.sizeDelta!=ratingSize)rating.sizeDelta=ratingSize;
   if(text.margin!=UnityEngine.Vector4.zero)text.margin=UnityEngine.Vector4.zero;
   if(text.fontSizeMax!=28)text.fontSizeMax=28;
   if(text.fontSizeMin!=16)text.fontSizeMin=16;
   if(!text.enableAutoSizing)text.enableAutoSizing=true;
   if(text.enableWordWrapping)text.enableWordWrapping=false;
   if(text.alignment!=TextAlignmentOptions.TopRight)text.alignment=TextAlignmentOptions.TopRight;
   return;
  }
  if(!RenderedDisplayRules.IsInsetNewGameSection(PathOf(text),text.gameObject.name,source))return;
  var rect=text.rectTransform;
  var position=new UnityEngine.Vector2(10,-10);var size=new UnityEngine.Vector2(130,42);
  if(rect.anchoredPosition!=position)rect.anchoredPosition=position;
  if(rect.sizeDelta!=size)rect.sizeDelta=size;
  if(text.enableAutoSizing)text.enableAutoSizing=false;
  if(text.fontSize!=28)text.fontSize=28;
  if(text.alignment!=TextAlignmentOptions.TopLeft)text.alignment=TextAlignmentOptions.TopLeft;
 }
 internal string? Complete(TMP_Text text,string source,string? previous){
  var view=text.GetComponentInParent<CardView>();
  string card=view?.Card?.Card?.CardDefinition.Identifier??"";
  return completion.Apply(PathOf(text),text.gameObject.name,card,view!=null,text.GetComponentInParent<UnityEngine.UI.Button>()!=null,source,previous,IsEffectTooltip(text),IsButtonLabel(text));
 }
 static bool IsButtonLabel(TMP_Text text)=>DynamicDisplayRules.IsButtonLabelName(text.gameObject.name)
  && text.GetComponentInParent<UnityEngine.UI.Button>()!=null && text.GetComponentInParent<TMP_InputField>()==null;
 static bool IsEffectTooltip(TMP_Text text){
  var effect=text.GetComponentInParent<EffectView>();
  if(effect!=null && effect.TooltipLabel==text)return true;
  var helper=TooltipHelper.ShowingTooltipHelper;
  effect=helper==null?null:helper.GetComponentInParent<EffectView>();
  return effect!=null && effect.TooltipLabel==text;
 }
 static string PathOf(TMP_Text text){
  var parts=new List<string>();var node=text.transform;
  while(node!=null && parts.Count<32){parts.Add(node.name);node=node.parent;}
  if(node!=null)return "";parts.Reverse();return string.Join("/",parts);
 }
 internal string? ObserveAndApply(TMP_Text text,string source,out bool approvedDeckName){
  approvedDeckName=false;
  if(stopped)return null;
  try {
   string name=text.gameObject.name;
   var view=text.GetComponentInParent<CardView>();
   bool inCard=view!=null;
   bool environmentDeckTitle=IsEnvironmentDeckTitle(text);
   bool deckNameTitle=inCard && name=="Title Label" && (view!.TitleLabel==text || environmentDeckTitle) && rules.IsDeckName(source);
   // Candidate capture is broader than the card-ID and effect-button translation scope.
   bool cardButton=inCard && (string.Equals(source.Trim(),"Double Damage",StringComparison.OrdinalIgnoreCase) ||
    (text.GetComponentInParent<UnityEngine.UI.Button>()!=null &&
     (name=="Selection Button Label" || name=="Text" || name=="Text (TMP)" || name=="Label")));
   bool effectTooltip=IsEffectTooltip(text);
   bool buttonLabel=IsButtonLabel(text);
   bool ruleLabel=inCard && name is "Body Label" or "Setup Label" or "Advanced Label" or "Advanced Heading Label";
   if(!buttonLabel && !effectTooltip && !cardButton && !deckNameTitle && !ruleLabel && !SurfaceProbeRules.CanObserve(name,inCard,false))return null;
   var parts=new List<string>();var node=text.transform;
   while(node!=null && parts.Count<32){parts.Add(node.name);node=node.parent;}
   // Never mark a truncated hierarchy.
   if(node!=null)return null;
   parts.Reverse();string path=string.Join("/",parts);
   string card="";
   if(inCard && view!.Card!=null && view.Card.Card!=null)card=view.Card.Card.CardDefinition.Identifier;
   string? target=rules.ApplyEnvironmentDeckTitle(name,source,environmentDeckTitle) ?? (inCard?rules.ApplyCardTitle(name,source,deckNameTitle):rules.ApplyDeckName(path,source));
   approvedDeckName=target!=null;
   target ??= inCard?rules.Dynamic?.ApplyCardButton(path,card,source):rules.Apply(path,source);
   target ??= rules.Dynamic?.ApplyStableButton(name,buttonLabel,source);
   if(!source.StartsWith("示·",StringComparison.Ordinal) && !source.StartsWith("组·",StringComparison.Ordinal)){
    string clipped=source.Length>3072?source.Substring(0,3072):source;
    string key=path+"\0"+clipped;
    counts.TryGetValue(name,out var n);
    if(seen.Count<1200 && n<200 && seen.Add(key)){
     counts[name]=n+1;
     Plugin.Instance.Log.LogInfo("SURFACE_PROBE "+JsonSerializer.Serialize(new{
      objectName=name,path,cardId=card,inCardView=inCard,cardButtonCandidate=cardButton,deckNameTitle,effectTooltip,buttonLabel,environmentDeckTitle,source=clipped,truncated=source.Length>3072,
      inputSource=text.m_inputSource.ToString(),markerCandidate=target!=null,
      confirmation=path==DynamicDisplayRules.FinishPath?DynamicDisplayRules.ParseConfirmation(clipped):null,
      status="observed-not-live-accepted"}));
    }
   }
   return target;
  }catch(Exception e){stopped=true;Plugin.Instance.Log.LogWarning("SURFACE_PROBE_STOPPED "+e);return null;}
 }
}
