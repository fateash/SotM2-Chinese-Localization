using TMPro;
using UnityEngine;
namespace SotM2Chinese;
// Dedicated real bold CJK face; never add it to the body's shared fallback list.
internal static class EnvironmentTitleFont {
 static TMP_FontAsset? font;
 static bool preparing;
 static readonly Dictionary<int,(TMP_Text Text,TMP_FontAsset Font,Material Material)> originals=new();
 internal static void Prepare(string glyphs){
  if(font!=null || preparing || !Plugin.Chinese || string.IsNullOrEmpty(glyphs))return;
  preparing=true;
  try {
   var path=Path.Combine(Plugin.Root,"NotoSansCJKsc-Bold.otf");
   if(!File.Exists(path)){Plugin.Once("title-font-missing","ENVIRONMENT_TITLE_FONT_MISSING using regular fallback");return;}
   var candidate=TMP_FontAsset.CreateFontAsset(path,0,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
   if(candidate==null)return;
   if(!candidate.TryAddCharacters(new string(glyphs.Distinct().ToArray()),out string missing,true) || !string.IsNullOrEmpty(missing)){
    UnityEngine.Object.Destroy(candidate);Plugin.Once("title-font-glyphs","ENVIRONMENT_TITLE_FONT_MISSING_GLYPHS "+missing);return;
   }
   candidate.name="SotM2 Environment Titles Bold";font=candidate;KeepAlive();
   Plugin.Once("title-font-ready","ENVIRONMENT_TITLE_FONT_READY NotoSansCJKsc-Bold dedicated-title-face");
  }catch(Exception e){Plugin.Once("title-font-error","ENVIRONMENT_TITLE_FONT_ERROR "+e);}
  finally {preparing=false;}
 }
 internal static void Apply(TMP_Text text,string glyphs){
  Prepare(glyphs);if(font==null || text.font==font)return;
  int id=text.GetInstanceID();
  if(!originals.ContainsKey(id))originals[id]=(text,text.font,text.fontSharedMaterial);
  // Retain the original text box, size/autosizing, alignment, color and outline.
  var outline=text.outlineColor;var width=text.outlineWidth;
  text.font=font;text.fontSharedMaterial=font.material;
  text.outlineColor=outline;text.outlineWidth=width;text.SetAllDirty();
  Plugin.Once("title-font-applied|"+id,"ENVIRONMENT_TITLE_FONT_APPLIED object="+text.gameObject.name);
 }
 internal static void Restore(TMP_Text text){
  if(!originals.Remove(text.GetInstanceID(),out var old))return;
  text.font=old.Font;text.fontSharedMaterial=old.Material;text.SetAllDirty();
 }
 internal static void KeepAlive(){
  foreach(var entry in originals.ToArray())if(entry.Value.Text==null)originals.Remove(entry.Key);
  if(font==null)return;
  Preserve(font);if(font.material!=null)Preserve(font.material);
  foreach(var tex in font.atlasTextures)if(tex!=null)Preserve(tex);
 }
 static void Preserve(UnityEngine.Object asset){asset.hideFlags|=HideFlags.DontUnloadUnusedAsset;UnityEngine.Object.DontDestroyOnLoad(asset);}
}
