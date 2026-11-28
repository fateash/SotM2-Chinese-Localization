using System.Text.Json;
using TMPro;
using Handelabra.Sentinels2.View;
namespace SotM2Chinese;
internal sealed class SurfaceProbe {
 readonly SurfaceProbeRules rules;
 internal string Glyphs()=>rules.AllGlyphs();
 readonly HashSet<string> seen=new(StringComparer.Ordinal);
 readonly Dictionary<string,int> counts=new(StringComparer.Ordinal);
 bool stopped;
 internal SurfaceProbe(string path){rules=SurfaceProbeRules.Read(path);}
 internal string? ObserveAndApply(TMP_Text text,string source,out bool approvedDeckName){
  approvedDeckName=false;
  if(stopped)return null;
  try {
   string name=text.gameObject.name;
   var view=text.GetComponentInParent<CardView>();
   bool inCard=view!=null;
   bool deckNameTitle=inCard && name=="Title Label" && view!.TitleLabel==text && rules.IsDeckName(source);
   // Candidate capture is broader than the card-ID and effect-button translation scope.
   bool cardButton=inCard && (string.Equals(source.Trim(),"Double Damage",StringComparison.OrdinalIgnoreCase) ||
    (text.GetComponentInParent<UnityEngine.UI.Button>()!=null &&
     (name=="Selection Button Label" || name=="Text" || name=="Text (TMP)" || name=="Label")));
   if(!cardButton && !deckNameTitle && !SurfaceProbeRules.CanObserve(name,inCard,false))return null;
   var parts=new List<string>();var node=text.transform;
   while(node!=null && parts.Count<32){parts.Add(node.name);node=node.parent;}
   // Never mark a truncated hierarchy.
   if(node!=null)return null;
   parts.Reverse();string path=string.Join("/",parts);
   string card="";
   if(inCard && view!.Card!=null && view.Card.Card!=null)card=view.Card.Card.CardDefinition.Identifier;
   string? target=inCard?rules.ApplyCardTitle(name,source,deckNameTitle):rules.ApplyDeckName(path,source);
   approvedDeckName=target!=null;
   target ??= inCard?rules.Dynamic?.ApplyCardButton(path,card,source):rules.Apply(path,source);
   if(!source.StartsWith("示·",StringComparison.Ordinal) && !source.StartsWith("组·",StringComparison.Ordinal)){
    string clipped=source.Length>3072?source.Substring(0,3072):source;
    string key=path+"\0"+clipped;
    counts.TryGetValue(name,out var n);
    if(seen.Count<1200 && n<200 && seen.Add(key)){
     counts[name]=n+1;
     Plugin.Instance.Log.LogInfo("SURFACE_PROBE "+JsonSerializer.Serialize(new{
      objectName=name,path,cardId=card,inCardView=inCard,cardButtonCandidate=cardButton,deckNameTitle,source=clipped,truncated=source.Length>3072,
      inputSource=text.m_inputSource.ToString(),markerCandidate=target!=null,
      confirmation=path==DynamicDisplayRules.FinishPath?DynamicDisplayRules.ParseConfirmation(clipped):null,
      status="observed-not-live-accepted"}));
    }
   }
   return target;
  }catch(Exception e){stopped=true;Plugin.Instance.Log.LogWarning("SURFACE_PROBE_STOPPED "+e);return null;}
 }
}
