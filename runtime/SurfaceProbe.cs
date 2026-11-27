using System.Text.Json;
using TMPro;
using Handelabra.Sentinels2.View;
namespace SotM2Chinese;
internal sealed class SurfaceProbe {
 readonly SurfaceProbeRules rules;
 readonly HashSet<string> seen=new(StringComparer.Ordinal);
 readonly Dictionary<string,int> counts=new(StringComparer.Ordinal);
 bool stopped;
 internal SurfaceProbe(string path){rules=SurfaceProbeRules.Read(path);}
 internal string? ObserveAndApply(TMP_Text text,string source){
  if(stopped)return null;
  try {
   string name=text.gameObject.name;
   if(!SurfaceProbeRules.CanObserve(name,text.GetComponentInParent<CardView>()!=null,false))return null;
   var parts=new List<string>();var node=text.transform;
   while(node!=null && parts.Count<32){parts.Add(node.name);node=node.parent;}
   // Never mark a truncated hierarchy.
   if(node!=null)return null;
   parts.Reverse();string path=string.Join("/",parts);
   string? target=rules.Apply(path,source);
   if(!source.StartsWith("示·",StringComparison.Ordinal) && !source.StartsWith("组·",StringComparison.Ordinal)){
    string clipped=source.Length>3072?source.Substring(0,3072):source;
    string key=path+"\0"+clipped;
    counts.TryGetValue(name,out var n);
    if(seen.Count<1200 && n<200 && seen.Add(key)){
     counts[name]=n+1;
     Plugin.Instance.Log.LogInfo("SURFACE_PROBE "+JsonSerializer.Serialize(new{
      objectName=name,path,source=clipped,truncated=source.Length>3072,
      inputSource=text.m_inputSource.ToString(),markerCandidate=target!=null,
      status="observed-not-live-accepted"}));
    }
   }
   return target;
  }catch(Exception e){stopped=true;Plugin.Instance.Log.LogWarning("SURFACE_PROBE_STOPPED "+e);return null;}
 }
}
