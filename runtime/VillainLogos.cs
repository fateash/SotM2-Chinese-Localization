using System.IO.Compression;
using System.Text;
using Handelabra.Sentinels2.View;
using UnityEngine;
using UnityEngine.UI;
using UObj=UnityEngine.Object;

namespace SotM2Chinese;
internal static class VillainLogos {
 sealed class Artwork {
  internal byte[] Bytes=Array.Empty<byte>();
  internal int Width,Height;
  internal Texture2D? Texture;
  internal Sprite? Sprite;
 }
 sealed class Binding {
  internal CardView View=null!;
  internal string Expected="";
  internal Image? Image;
  internal Sprite? Original,Replacement;
 }
 static readonly Dictionary<string,Artwork> assets=new(StringComparer.Ordinal);
 static readonly Dictionary<int,Binding> views=new();
 static bool attempted,inSetter;

 internal static void Prepare(){
  if(attempted || !Plugin.Chinese)return;
  attempted=true;
  try {
   using var file=File.OpenRead(Path.Combine(Plugin.Root,"villain-logos.bytes"));
   using var gzip=new GZipStream(file,CompressionMode.Decompress);
   using var reader=new BinaryReader(gzip,Encoding.ASCII);
   if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="SOTMLOG1" || reader.ReadInt32()!=6)
    throw new InvalidDataException("Invalid logo header");
   var decoded=new Dictionary<string,Artwork>(StringComparer.Ordinal);
   for(int i=0;i<6;i++){
    int count=reader.ReadInt32();if(count<1 || count>64)throw new InvalidDataException("Invalid logo identity");
    var name=Encoding.ASCII.GetString(reader.ReadBytes(count));
    int w=reader.ReadInt32(),h=reader.ReadInt32(),length=reader.ReadInt32();
    if(!VillainLogoRules.Sizes.TryGetValue(name,out var size) || size!=(w,h) || length!=checked(w*h*4) || decoded.ContainsKey(name))
     throw new InvalidDataException("Unexpected logo dimensions or identity");
    var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
    decoded.Add(name,new Artwork{Bytes=bytes,Width=w,Height=h});
   }
   if(gzip.ReadByte()!=-1)throw new InvalidDataException("Trailing logo data");
   foreach(var item in decoded)assets.Add(item.Key,item.Value);
   Plugin.Once("logos-ready","VILLAIN_LOGOS_READY six-reviewed-base-villain-artworks");
  }catch(Exception e){assets.Clear();Plugin.Once("logos-load-error","VILLAIN_LOGOS_FALLBACK original sprites retained: "+e);}
 }
 internal static void Expect(CardView view,string spriteName){
  if(!Plugin.Chinese || view==null)return;
  int id=view.GetInstanceID();
  if(!views.TryGetValue(id,out var state))views[id]=state=new Binding{View=view};
  if(state.Expected!=spriteName){Restore(state);state.Expected=spriteName??"";}
 }
 static void Restore(Binding state){
  if(state.Image!=null && state.Replacement!=null && state.Image.sprite==state.Replacement && state.Original!=null){
   inSetter=true;
   try{state.Image.sprite=state.Original;}finally{inSetter=false;}
  }
  state.Image=null;state.Original=null;state.Replacement=null;
 }
 static Sprite? Replacement(string name,Sprite original){
  Prepare();if(!assets.TryGetValue(name,out var art))return null;
  if(art.Sprite!=null)return art.Sprite;
  Texture2D? texture=null;
  try {
   texture=new Texture2D(art.Width,art.Height,TextureFormat.RGBA32,false);
   texture.name="SotM2ZH.Texture."+name;texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
   texture.LoadRawTextureData(art.Bytes);texture.Apply(false,true);
   var sprite=Sprite.Create(texture,new Rect(0,0,art.Width,art.Height),new Vector2(0.5f,0.5f),original.pixelsPerUnit);
   if(sprite==null)throw new InvalidDataException("Sprite creation failed");
   sprite.name="SotM2ZH."+name;
   texture.hideFlags|=HideFlags.DontUnloadUnusedAsset;sprite.hideFlags|=HideFlags.DontUnloadUnusedAsset;
   UObj.DontDestroyOnLoad(texture);UObj.DontDestroyOnLoad(sprite);
   art.Texture=texture;art.Sprite=sprite;return sprite;
  }catch{if(texture!=null)UObj.Destroy(texture);throw;}
 }
 internal static void BeforeSprite(Image image,ref Sprite value){
  if(!Plugin.Chinese || inSetter || image==null || value==null)return;
  if(!VillainLogoRules.Sizes.ContainsKey(value.name))return;
  // Only a bound CardView.LogoImage may be changed; a sprite name alone is insufficient.
  var view=image.GetComponentInParent<CardView>();
  if(view==null || view.LogoImage!=image || !views.TryGetValue(view.GetInstanceID(),out var state))return;
  if(!VillainLogoRules.CanReplace(state.Expected,value.name,true))return;
  var replacement=Replacement(state.Expected,value);if(replacement==null)return;
  state.Image=image;state.Original=value;state.Replacement=replacement;value=replacement;
  Plugin.Once("logo|"+state.Expected,"VILLAIN_LOGO_APPLIED sprite="+state.Expected+" bound=CardView.LogoImage");
 }
 internal static void Refresh(){
  if(!Plugin.Chinese)return;
  foreach(var pair in views.ToArray()){
   var state=pair.Value;var view=state.View;
   if(view==null){views.Remove(pair.Key);continue;}
   if(!view.gameObject.activeInHierarchy || !view.gameObject.scene.IsValid())continue;
   var image=view.LogoImage;if(image==null)continue;
   if(state.Image!=null && state.Image!=image)Restore(state);
   var original=image.sprite;if(original==null || original==state.Replacement)continue;
   var replacement=original;BeforeSprite(image,ref replacement);
   if(replacement==original)continue;
   inSetter=true;
   try{image.sprite=replacement;}finally{inSetter=false;}
  }
 }
}
