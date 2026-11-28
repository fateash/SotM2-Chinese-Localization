using System.Text.RegularExpressions;
namespace SotM2Chinese;
// Completes display-time expansions and alternate power-button rendering. No engine calls.
// Match whole authored rule lines for the bound card, never arbitrary name substrings.
public sealed class RenderedDisplayRules {
 readonly Catalog catalog;
 readonly DynamicDisplayRules? namedDynamic;
 readonly Dictionary<string,(Regex Pattern,string[] Names)[]> parameterNames=new(StringComparer.Ordinal);
 readonly Dictionary<string,HashSet<string>> incapSources=new(StringComparer.Ordinal);
 readonly Dictionary<string,string> names=new(StringComparer.Ordinal);
 readonly Dictionary<string,(Regex Pattern,string Target)[]> rules=new(StringComparer.Ordinal);
 static readonly Regex Token=new(@"\{[^{}]+\}",RegexOptions.CultureInvariant);
 static readonly Regex PowerPath=new(@"/Power Container/[0-9]{1,6}/UsePower-[A-Za-z0-9_]+-(?<card>[A-Za-z0-9_]+)/Label\z",RegexOptions.CultureInvariant);
 const string Heroes="AbsoluteZero|Bunker|CaptainCosmic|Fanatic|Haka|Legacy|Ra|Tachyon|Tempest|TheArgentAdept|TheWraith|Unity";
 static readonly Regex IncapPath=new(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]|5 Hero Layout/Inactive Hero Location [1-4])/)?(?<hero>"+Heroes+@")/Inner Panel/Foreground Elements/Incap Ability Container/Incap Ability Button\(Clone\)/Label\z",RegexOptions.CultureInvariant);
 const string Bullet="<size=70%><sprite name=\"BulletPoint\"></size><indent=1em>";
 const string Setup="<b><i>Setup</i></b>: ";
 public const string Glyphs="准备进阶演奏伴奏制造灭绝减加确定要放弃本局游戏吗点击一位英雄确定谁视为生命值最低者不会因此受到伤害牌库抽张先行动在场无法不可削减最高任何造成受到寒冷火焰能量近战投射精神闪电毒素光耀音波邪能";
 public RenderedDisplayRules(Catalog c,Dictionary<string,string> deckNames,Dictionary<string,string>? dynamicTemplates=null){
  catalog=c;
  foreach(var g in c.Display.Where(e=>e.ObjectName=="Title Label" && e.Mode=="exact").GroupBy(e=>e.Source,StringComparer.Ordinal))
   if(g.Select(e=>e.Target).Distinct(StringComparer.Ordinal).Count()==1)names[g.Key]=g.First().Target;
  foreach(var e in deckNames)names[e.Key]=e.Value;
  namedDynamic=dynamicTemplates?.Count>0?new DynamicDisplayRules(dynamicTemplates,ResolveArgument):null;
  var tokens=new Dictionary<string,(string En,string Zh)>(StringComparer.Ordinal);
  var nameTokens=new HashSet<string>(StringComparer.Ordinal);
  foreach(var e in c.Display.Where(e=>e.ObjectName=="Title Label" && e.Card.EndsWith("Character",StringComparison.Ordinal) && deckNames.ContainsKey(e.Source)))
   {string token="{"+e.Card.Substring(0,e.Card.Length-9)+"}";tokens[token]=(e.Source,deckNames[e.Source]);nameTokens.Add(token);}
  foreach(var p in new[]{"Start","Play","Power","Draw","End","Incapacitated"}){
   string en="<nobr><b>"+p.ToUpperInvariant()+" PHASE</b></nobr>";
   tokens["{"+p+"}"]=(en,c.DisplayRules.Apply("Body Label","",en)??en);
  }
  tokens["{Flip}"]=("<sprite name=\"Flip\" color=#FFFFFF>","<sprite name=\"Flip\" color=#FFFFFF>");
  foreach(var p in new[]{("Perform","演奏"),("Accompany","伴奏"),("Fabricate","制造"),("Exterminate","灭绝")}){
   string en="<nobr><sprite name=\""+p.Item1+"\" color=#FFFFFF> <style=BoldItalic>"+p.Item1.ToUpperInvariant()+"</style></nobr>";
   tokens["{"+p.Item1+"Ability}"]=(en,en.Replace(">"+p.Item1.ToUpperInvariant()+"<",">"+p.Item2+"<"));
  }
  string h="<sprite name=\"H\" color=#000000>";
  tokens["{H}"]=(h,h);
  foreach(var n in new[]{1,2})foreach(var sign in new[]{"-","+"})
   tokens["{H "+sign+" "+n+"}"]=(h+(sign=="-"?" minus ":" plus ")+n,h+(sign=="-"?"减":"加")+n);
  string? Expand(string s,bool zh){
   if(Token.Matches(s).Any(m=>!tokens.ContainsKey(m.Value)))return null;
   return Token.Replace(s,m=>zh?tokens[m.Value].Zh:tokens[m.Value].En);
  }
  foreach(var g in c.Cards.GroupBy(e=>e.Card,StringComparer.Ordinal)){
   var pairs=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
   var namePatterns=new List<(Regex Pattern,string[] Names)>();
   incapSources[g.Key]=new(StringComparer.Ordinal);
   foreach(var e in g){
    string? target=Expand(e.Target,true);if(target==null)continue;
    // A rendered H may be an engine-highlighted current value. Only replace
    // named captures; keep the complete live numeric/text spans byte-for-byte.
    if(Token.Matches(e.Target).Any(m=>nameTokens.Contains(m.Value))){
     var translatedNames=new List<string>();int offset=0;string pattern="";
     foreach(Match token in Token.Matches(e.Target)){
      pattern+=Regex.Escape(e.Target.Substring(offset,token.Index-offset));var value=tokens[token.Value];
      if(nameTokens.Contains(token.Value)){
       pattern+="(?<n"+translatedNames.Count+">"+Regex.Escape(value.En)+"|"+Regex.Escape(value.Zh)+")";translatedNames.Add(value.Zh);
      }else if(token.Value.StartsWith("{H",StringComparison.Ordinal))
       pattern+="(?:"+Regex.Escape(value.En)+@"|<b>\*[+−-]?[0-9]{1,6}\*</b>)";
      else pattern+="(?:"+Regex.Escape(value.En)+"|"+Regex.Escape(value.Zh)+")";
      offset=token.Index+token.Length;
     }
     pattern+=Regex.Escape(e.Target.Substring(offset));
     namePatterns.Add((new Regex(@"(?<prefix>\A|\r\n|\n|\r|\\r\\n|\\n|\\r|<style=PowerBody>|"+Regex.Escape(Setup)+@")"+pattern+@"(?=\r|\n|\\r|\\n|</style>|\z)",RegexOptions.CultureInvariant),translatedNames.ToArray()));
    }
    if(e.Source.StartsWith(Bullet,StringComparison.Ordinal) && e.Source.EndsWith("</indent>",StringComparison.Ordinal)){
     foreach(var candidate in new[]{Expand(e.Source.Substring(Bullet.Length,e.Source.Length-Bullet.Length-9),false),Expand(e.Target.Substring(Bullet.Length,e.Target.Length-Bullet.Length-9),false)})
      if(candidate!=null)incapSources[g.Key].Add(candidate);
    }
    foreach(var source in new[]{Expand(e.Source,false),Expand(e.Target,false)}){
     if(source==null || source==target)continue;
     if(!pairs.ContainsKey(source))pairs[source]=new(StringComparer.Ordinal);
     pairs[source].Add(target);
    }
   }
   parameterNames[g.Key]=namePatterns.ToArray();
   rules[g.Key]=pairs.Where(p=>p.Value.Count==1).OrderByDescending(p=>p.Key.Length).Select(p=>(new Regex(
    @"(?<prefix>\A|\r\n|\n|\r|\\r\\n|\\n|\\r|<style=PowerBody>|"+Regex.Escape(Setup)+@")"+Regex.Escape(p.Key)+@"(?=\r|\n|\\r|\\n|</style>|\z)",RegexOptions.CultureInvariant),p.Value.Single())).ToArray();
  }
 }
 static readonly Dictionary<string,string> Damage=new(StringComparer.OrdinalIgnoreCase){
  ["cold"]="寒冷",["fire"]="火焰",["energy"]="能量",["melee"]="近战",["projectile"]="投射",["psychic"]="精神",["lightning"]="闪电",["toxic"]="毒素",["radiant"]="光耀",["sonic"]="音波",["infernal"]="邪能"};
 string? EffectTooltip(string source){
  // Bound EffectView.TooltipLabel only. Copy each signed amount without arithmetic or normalization.
  var m=Regex.Match(source,@"\A(?<n>[+−-][0-9]{1,6}) (?:(?<type>[a-z]+) )?damage dealt (?<direction>by|to) (?<name>[^<>\r\n]{1,200})\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  if(m.Success && (!m.Groups["type"].Success || Damage.ContainsKey(m.Groups["type"].Value)))
   return Name(m.Groups["name"].Value)+(m.Groups["direction"].Value.Equals("by",StringComparison.OrdinalIgnoreCase)?"造成的":"受到的")+
    (m.Groups["type"].Success?Damage[m.Groups["type"].Value]:"")+"伤害"+m.Groups["n"].Value;
  m=Regex.Match(source,@"\A(?<name>[^<>\r\n]{1,200}) is (?<open><b>)?immune(?<close></b>)? to (?<type>all|[a-z]+) damage\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  if(m.Success && m.Groups["open"].Success==m.Groups["close"].Success && (m.Groups["type"].Value.Equals("all",StringComparison.OrdinalIgnoreCase) || Damage.ContainsKey(m.Groups["type"].Value)))
   return Name(m.Groups["name"].Value)+m.Groups["open"].Value+"无法受到"+m.Groups["close"].Value+(m.Groups["type"].Value.Equals("all",StringComparison.OrdinalIgnoreCase)?"任何":Damage[m.Groups["type"].Value])+"伤害";
  m=Regex.Match(source,@"\A(?<name>[^<>\r\n]{1,200}) (?<open><b>)?cannot deal(?<close></b>)? any damage\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  if(m.Success && m.Groups["open"].Success==m.Groups["close"].Success)
   return Name(m.Groups["name"].Value)+m.Groups["open"].Value+"无法造成"+m.Groups["close"].Value+"任何伤害";
  m=Regex.Match(source,@"\ADamage dealt by (?<name>[^<>\r\n]{1,200}) is (?<open><b>)?irreducible(?<close></b>)?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  if(m.Success && m.Groups["open"].Success==m.Groups["close"].Success)
   return Name(m.Groups["name"].Value)+"造成的伤害"+m.Groups["open"].Value+"不可削减"+m.Groups["close"].Value;
  return null;
 }
 public static bool IsPowerButton(string path,string card,bool inCard,bool button){
  if(!inCard || !button || card.Length==0 ||
   (!path.StartsWith("Canvas/Main Panel/",StringComparison.Ordinal) && !path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal)))return false;
  var m=PowerPath.Match(path);return m.Success && m.Groups["card"].Value==card;
 }
 string ResolveArgument(string s){
  var prefix=Regex.Match(s,@"\A<b>(?<name>[^<>\r\n]+):</b> \z");
  return prefix.Success?"<b>"+Name(prefix.Groups["name"].Value)+":</b> ":Name(s);
 }
 string Name(string s)=>names.TryGetValue(s,out var t)?t:s;
 public string? Apply(string path,string obj,string card,bool inCard,bool button,string source,string? previous=null,bool effectTooltip=false){
  string result=previous??source;
  if(effectTooltip)return EffectTooltip(source)??previous;
  if(!inCard){
   result=namedDynamic?.Apply(path,source)??result;
   var incap=IncapPath.Match(path);
   if(obj=="Label" && incap.Success){
    string hero=incap.Groups["hero"].Value+"Character";
    if(incapSources.TryGetValue(hero,out var allowed) && allowed.Contains(source)){
     foreach(var e in rules[hero])result=e.Pattern.Replace(result,m=>m.Groups["prefix"].Value+e.Target);
     result=catalog.DisplayRules.Apply("Body Label",hero,result)??result;
    }
   }
  }
  if(inCard && button && obj=="Label" && rules.ContainsKey(card) &&
   (path.StartsWith("Canvas/Main Panel/",StringComparison.Ordinal) || path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal))){
   var ability=Regex.Match(path,@"/Select Effect Container/[0-9]{1,6}/UseAbility-(?<ability>Perform|Accompany|Fabricate|Exterminate)-(?<card>[A-Za-z0-9_]+)-[0-9]{1,6}-[^\r\n]+/Label\z");
   if(ability.Success && ability.Groups["card"].Value==card){
    string a=ability.Groups["ability"].Value;
    string label="<nobr><sprite name=\""+a+"\" color=#FFFFFF> <style=BoldItalic>"+a.ToUpperInvariant()+"</style></nobr>";
    if(source==label)return source.Replace(">"+a.ToUpperInvariant()+"<",">"+(a switch {"Perform"=>"演奏","Accompany"=>"伴奏","Fabricate"=>"制造",_=>"灭绝"})+"<");
   }
  }
  if(inCard && rules.TryGetValue(card,out var entries) &&
    (obj is "Body Label" or "Setup Label" or "Advanced Label" || IsPowerButton(path,card,inCard,button))){
   // Apply on the unmodified input before phase labels change template expansion text.
   result=source;
   foreach(var e in entries)result=e.Pattern.Replace(result,m=>m.Groups["prefix"].Value+e.Target);
   foreach(var e in parameterNames[card])result=e.Pattern.Replace(result,m=>{
    string value=m.Value;for(int i=e.Names.Length-1;i>=0;i--){var field=m.Groups["n"+i];int start=field.Index-m.Index;value=value.Substring(0,start)+e.Names[i]+value.Substring(start+field.Length);}return value;
   });
   foreach(var ability in new[]{("Perform","演奏"),("Accompany","伴奏"),("Fabricate","制造"),("Exterminate","灭绝")}){
    string heading="<style=AbilityHeading><sprite name=\""+ability.Item1+"\"> "+ability.Item1.ToUpperInvariant()+"</style>";
    result=result.Replace(heading,heading.Replace(" "+ability.Item1.ToUpperInvariant()+"</style>"," "+ability.Item2+"</style>"));
   }
   result=result.Replace("<style=ReactionHeading>REACTION: </style>","<style=ReactionHeading>反应: </style>");
   if(obj=="Setup Label" && result.StartsWith(Setup,StringComparison.Ordinal))result="<b><i>准备</i></b>: "+result.Substring(Setup.Length);
   result=catalog.DisplayRules.Apply(IsPowerButton(path,card,inCard,button)?"Body Label":obj,card,result)??result;
   // The keyword banner overlaps the opening rule on both Omnitron faces.
   // Reserve one display line only at this reviewed opening; retain the source newline encoding.
   const string omnitronOpening="全能智创生命值为0且为场上唯一装置牌时，英雄才能获胜。";
   if(card=="OmnitronCharacter" && obj=="Body Label" && result.StartsWith(omnitronOpening,StringComparison.Ordinal))
    result=(source.Contains("\\r\\n",StringComparison.Ordinal)?"\\r\\n":source.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n")+result;
   // Recharge Mode: seven lines overrun the full-card rules box with the CJK font.
   // Scope to the reviewed full body; preserve text, phase sprites, font size and newline bytes.
   const string rechargeBody="你无法使用异能。\n<voffset=-0.3em><space=-1em><sprite name=\"StartPhase\"><space=.5em><style=StartPhase>开始阶段</style></voffset>\n破坏此牌。\n<voffset=-0.3em><space=-1em><sprite name=\"DrawPhase\"><space=.5em><style=DrawPhase>抽牌阶段</style></voffset>\n你可以额外抽4张牌。\n<voffset=-0.3em><space=-1em><sprite name=\"EndPhase\"><space=.5em><style=EndPhase>结束阶段</style></voffset>\n将任意数量的手牌置于场上的任意军械牌下。\n";
   if(card=="RechargeMode" && obj=="Body Label" &&
      (path.StartsWith("Canvas/",StringComparison.Ordinal) || path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal)) &&
      Regex.IsMatch(path,@"/CardView RechargeMode-[0-9]+ FullCardFront(?: \(Temporary\))?/Body Box/Body Label\z") &&
      result.Replace("\\r\\n","\n").Replace("\\n","\n").Replace("\r\n","\n").Replace("\r","\n")==rechargeBody)
    result="<line-height=85%>"+result+"</line-height>";
   if(result==source)result=previous??source;
  }
  if(inCard && obj=="Advanced Heading Label"){
   // Native decoration is preserved, including its phase heading on the next line.
   result=Regex.Replace(result,@"\A(?<open><size=75%>)?<i>Advanced\.\.\.</i>(?<close></size>)?(?=\r?\n|\z)",m=>
    m.Groups["open"].Success==m.Groups["close"].Success?m.Groups["open"].Value+"<i>进阶……</i>"+m.Groups["close"].Value:m.Value);
  }
  if(!inCard && obj=="Abandon Label" && path=="Overlay Canvas/Abandon Panel/Panel/Abandon Label" && source=="Are you sure you want to abandon your game in progress?")
   result="确定要放弃本局游戏吗？";
  if(!inCard && path==SurfaceProbeRules.HintPath){
   var m=Regex.Match(source,@"\A(?<prefix><b>[^<>\r\n]+:</b> )?Click (?<kind>a Hero|a target) to determine which has the (?<order>lowest|highest) HP(?<safe> \(will not be dealt damage\))?\.\z");
   if(m.Success && result==source)result=m.Groups["prefix"].Value+"点击"+(m.Groups["kind"].Value=="a Hero"?"一位英雄":"一个目标")+"，确定谁视为生命值最"+(m.Groups["order"].Value=="highest"?"高":"低")+"者"+(m.Groups["safe"].Success?"（不会因此受到伤害）":"")+"。";
   var immune=Regex.Match(source,@"\A(?<prefix><b>[^<>\r\n]+:</b> )?Click a target to make it immune to damage\.\z");
   if(immune.Success)result=immune.Groups["prefix"].Value+"点击一个目标，使其无法受到伤害。";
   var first=Regex.Match(source,@"\A(?<prefix><b>[^<>\r\n]+:</b> )?Click a card in play to act first\.\z");
   if(first.Success)result=first.Groups["prefix"].Value+"点击一张在场的牌，让其先行动。";
   var draw=Regex.Match(source,@"\A(?<prefix><b>[^<>\r\n]+:</b> )?Click (?<owner>[^<>\r\n]{1,200})'s deck to draw a card\.\z");
   if(draw.Success)result=draw.Groups["prefix"].Value+"点击"+Name(draw.Groups["owner"].Value)+"的牌库，抽一张牌。";
   // Translate only the complete source-name field of a recognized hint.
   if(result!=source)result=Regex.Replace(result,@"\A<b>(?<name>[^<>\r\n]+):</b> ",m=>"<b>"+Name(m.Groups["name"].Value)+":</b> ");
  }
  if(!inCard && previous!=null && (path==DynamicDisplayRules.DescriptionPath ||
    path.StartsWith("Canvas/Revealed Cards Manager/[RevealCardsAction: Source=",StringComparison.Ordinal) && path.EndsWith("]/Title Label",StringComparison.Ordinal))){
   var m=Regex.Match(source,@"\A(?<name>[^<>\r\n]+) (drew|revealed|played|discovered|summoned|collected):\z");
   if(m.Success && result.StartsWith(m.Groups["name"].Value,StringComparison.Ordinal))
    result=Name(m.Groups["name"].Value)+result.Substring(m.Groups["name"].Length);
  }
  return result==source?null:result;
 }
}
