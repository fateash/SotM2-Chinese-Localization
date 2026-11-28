using System.Globalization;
using System.Text.RegularExpressions;
namespace SotM2Chinese;
// Opt-in pilot. Only complete observed surfaces/grammar; never touch game data.
public sealed record ConfirmationParts(string Action,string Number,string Unit,string NumberKind);
public sealed class DynamicDisplayRules {
 public const string PhasePath="Canvas/HUD Panel/State Box/Phase Box/Phase Label";
 public const string FinishPath="Canvas/Main Panel/Button Panel/Finish Button/Finish Button Label";
 public const string ContinuePath="Canvas/Main Panel/Button Panel/Continue Button/Text (TMP)";
 public const string DescriptionPath="Canvas/Temporary Full Card Panel/Description Label";
 public const string CardsUnderPath="Full Card Canvas/Full Card Panel/Cards Under/Scroll View/Viewport/Content/Heading Label";
 public const string CardsNextToPath="Full Card Canvas/Full Card Panel/Cards Next To/Scroll View/Viewport/Content/Heading Label";
 public const string DrawPath="Canvas/Main Panel/Tachyon/Inner Panel/Foreground Elements/Hand/Selection Button/Selection Button Label";
 public const string DiscardPath="Canvas/Main Panel/Tachyon/Inner Panel/Foreground Elements/Deck and Trash/Active Trash View/Selection Button/Selection Button Label";
 readonly Dictionary<string,string> templates;
 readonly Func<string,string> resolveName;
 static readonly Dictionary<string,(int Args,string Tags)> Specs=new(){
  ["hint.destroy.play.under.upto"]=(3,""),["hint.destroy.play.under.upto.first"]=(3,""),["hint.destroy.play.under.exact.first"]=(3,""),
  ["hint.card.replace"]=(0,""),
  ["hint.hero.discard.hand"]=(0,""),
  ["button.play.item"]=(0,""),
  ["message.villain.trash15"]=(0,""),
  ["message.villain.targets10"]=(0,""),
  ["message.zero.under"]=(1,""),
  ["result.wagner.exploded"]=(0,""),
  ["hint.summon.search"]=(0,""),
  ["button.location.deck"]=(0,""),
  ["button.location.trash"]=(0,""),
  ["hint.blank.target"]=(0,""),
  ["hint.blank.inplay"]=(0,""),
  ["hint.card.draw"]=(0,""),
  ["hint.card.discard"]=(0,""),
  ["hint.card.bury"]=(0,""),
  ["hint.trash.salvage.upto"]=(2,""),
  ["hint.target.topdeck"]=(0,""),
  ["hint.attack.required.unspecified"]=(4,""),["hint.hp.rank.multiple"]=(3,""),
  ["hint.damage.amount"]=(0,""),["hint.attack.remaining"]=(4,"<nobr></nobr>"),
  ["finish.regain.targets"]=(1,""),["hint.regain.hero"]=(0,""),["hint.regain.upto"]=(2,""),
  ["hint.attack.required.list"]=(5,"<nobr></nobr>"),["hint.attack.exact.unspecified"]=(3,""),
  ["hint.attack.exact.dual"]=(6,"<nobr></nobr><nobr></nobr>"),
  ["hint.attack.exact.triple"]=(8,"<nobr></nobr><nobr></nobr><nobr></nobr>"),
  ["hint.attack.upto.dual"]=(6,"<nobr></nobr><nobr></nobr>"),
  ["hint.attack.upto.triple"]=(8,"<nobr></nobr><nobr></nobr><nobr></nobr>"),
  ["hint.attack.remaining.dual"]=(6,"<nobr></nobr><nobr></nobr>"),
  ["hint.attack.remaining.triple"]=(8,"<nobr></nobr><nobr></nobr><nobr></nobr>"),
  ["hint.attack.single.triple"]=(7,"<nobr></nobr><nobr></nobr><nobr></nobr>"),
  ["hint.attack.required.dual"]=(7,"<nobr></nobr><nobr></nobr>"),
  ["hint.attack.required.triple"]=(9,"<nobr></nobr><nobr></nobr><nobr></nobr>"),
  ["hint.attack.dual"]=(5,"<nobr></nobr><nobr></nobr>"),
  ["button.most"]=(0,""),["button.play.golem"]=(0,""),
  ["hint.hero.most.play"]=(0,""),["hint.trash.under"]=(0,""),["hint.hero.salvage"]=(0,""),["hint.trash.salvage"]=(0,""),
  ["hint.trash.owner.discard"]=(3,""),["hint.destroy.play.under.exact"]=(3,""),
  ["button.replace"]=(0,""),["button.skip.draw.action"]=(0,""),["button.skip.power.action"]=(0,""),
  ["button.discover.item"]=(0,""),["button.discover.golem"]=(0,""),
  ["hint.replace-discard"]=(2,""),["hint.draw.owner"]=(3,""),
  ["hint.hero.draw"]=(0,""),["hint.hero.discard"]=(0,""),["hint.hand.under"]=(0,""),
  ["hint.discard.upto"]=(2,""),["hint.destroy.exact"]=(2,""),
  ["hint.phase.target"]=(2,"<nobr><b></b></nobr>"),
  ["hint.collect.deck"]=(0,""),["hint.summon.deck.trash"]=(0,""),["hint.prevent.dealing"]=(0,""),
  ["hint.put.under"]=(2,""),["hint.destroy.play.under"]=(2,""),
  ["heading.cards.under"]=(1,""),["heading.cards.next"]=(1,""),["description.collect"]=(1,""),
  ["button.newgame.play"]=(0,""),["button.newgame.randomize"]=(0,""),["button.newgame.back"]=(0,""),["button.result.share"]=(0,""),["button.message.continue"]=(0,""),
  ["finish.destroy"]=(1,""),["hint.destroy.targets"]=(2,""),["hint.destroy.cards"]=(2,""),
  ["button.skip.incapacitated"]=(0,""),["button.take.play"]=(0,""),["button.take.power"]=(0,""),["hint.hero.take.play"]=(0,""),["hint.hero.take.power"]=(0,""),
  ["description.summon"]=(1,""),["phase.setup"]=(0,""),["result.hp"]=(2,"<nobr></nobr>"),["result.victory"]=(1,""),["result.defeat"]=(1,""),["message.game-over"]=(0,""),["message.all-incapacitated"]=(0,""),
  ["button.play.construct"]=(0,""),["button.discover.construct"]=(0,""),
  ["hint.lowest.hp"]=(0,""),["description.discover"]=(1,""),["phase.power.remaining"]=(1,""),
  ["button.common.1"]=(0,""),
  ["button.common.2"]=(0,""),
  ["button.common.3"]=(0,""),
  ["button.common.4"]=(0,""),
  ["button.common.5"]=(0,""),
  ["button.common.6"]=(0,""),
  ["button.common.7"]=(0,""),
  ["button.common.8"]=(0,""),
  ["button.common.9"]=(0,""),
  ["button.common.10"]=(0,""),
  ["button.common.11"]=(0,""),
  ["button.common.12"]=(0,""),
  ["button.common.13"]=(0,""),
  ["button.common.14"]=(0,""),
  ["button.common.15"]=(0,""),
  ["button.common.16"]=(0,""),
  ["button.common.17"]=(0,""),
  ["button.common.18"]=(0,""),
  ["button.common.19"]=(0,""),
  ["button.common.20"]=(0,""),
  ["button.common.21"]=(0,""),
  ["button.common.22"]=(0,""),
  ["button.common.23"]=(0,""),
  ["hint.selection.1"]=(0,""),
  ["hint.selection.2"]=(0,""),
  ["hint.selection.3"]=(0,""),
  ["hint.selection.4"]=(0,""),
  ["hint.selection.5"]=(0,""),
  ["hint.selection.6"]=(0,""),
  ["hint.selection.7"]=(0,""),
  ["hint.selection.8"]=(0,""),
  ["hint.selection.9"]=(0,""),
  ["hint.selection.10"]=(0,""),
  ["hint.selection.11"]=(0,""),
  ["hint.selection.12"]=(0,""),
  ["hint.selection.13"]=(0,""),
  ["hint.selection.14"]=(0,""),
  ["hint.selection.15"]=(0,""),
  ["hint.selection.16"]=(0,""),
  ["hint.selection.17"]=(0,""),
  ["hint.selection.18"]=(0,""),
  ["hint.selection.19"]=(0,""),
  ["hint.selection.20"]=(0,""),
  ["hint.selection.21"]=(0,""),
  ["hint.selection.22"]=(0,""),
  ["hint.selection.23"]=(0,""),
  ["hint.selection.24"]=(0,""),
  ["button.regain.hp"]=(1,""),
  ["button.deal.one"]=(1,""),
  ["button.deal.self"]=(1,""),
  ["button.deal.many"]=(1,""),
  ["button.deal.amount"]=(1,""),
  ["hint.play"]=(0,""),["hint.power.drag"]=(0,""),["hint.power.click"]=(0,""),
  ["finish.damage"]=(1,""),["hint.attack.single"]=(3,"<nobr></nobr>"),
  ["hint.attack.multiple"]=(4,"<nobr></nobr>"),["hint.effect"]=(2,""),
  ["hint.draw-discard"]=(2,""),["button.draw"]=(0,""),["button.discard"]=(0,""),
  ["button.skip.play"]=(0,""),["button.skip.power"]=(0,""),
  ["description.draw"]=(1,""),["description.reveal"]=(1,""),["description.play"]=(1,""),
  ["finish.discard"]=(1,""),
  ["finish.select"]=(1,""),
  ["phase.play.remaining"]=(1,""),["phase.draw.remaining"]=(1,""),
  ["hint.play.click"]=(0,""),
  ["hint.hero.play"]=(0,""),
  ["hint.hero.power"]=(0,""),
  ["hint.ability"]=(0,""),
  ["hint.summon"]=(0,""),
  ["hint.hero.topdeck"]=(0,""),
  ["hint.trash.topdeck"]=(0,""),
  ["hint.discard.single"]=(0,""),
  ["hint.deck.reveal"]=(0,""),
  ["hint.discard.multiple"]=(2,""),
  ["hint.reduce"]=(2,""),
  ["hint.attack.unspecified"]=(2,""),
  ["hint.attack.order"]=(3,"<nobr></nobr>"),
  ["hint.reaction"]=(5,"<b></b><b></b>"),["hint.reaction.first"]=(5,"<b></b><b></b>"),
  ["button.play.card"]=(0,""),
  ["button.use.power"]=(0,""),
  ["button.put.card"]=(0,""),
  ["button.reveal"]=(0,""),
  ["button.double.damage"]=(0,""),
  ["button.draw.card"]=(0,""),
  ["hint.attack.exact"]=(4,"<nobr></nobr>"),["hint.attack.required"]=(5,"<b></b><nobr></nobr>"),
  ["hint.attack.order.unspecified"]=(2,""),["hint.play.location"]=(2,""),
  ["hint.hero.destroy"]=(0,""),["hint.card.destroy"]=(0,""),["hint.hero.fewest"]=(0,""),
  ["button.destroy.card"]=(0,""),["button.play.here"]=(0,""),["button.fewest"]=(0,""),
  ["button.skip"]=(0,""),["button.skip.play.action"]=(0,""),
  ["hint.phase.activate"]=(3,"<nobr><b></b></nobr>"),["hint.incapacitated"]=(0,""),
  ["hint.damage.type"]=(0,"")
 };
 static Regex Rx(string pattern)=>new(pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
 // The game renders this label uppercase, but captured m_text uses lowercase left.
 static readonly Regex PhaseRemaining=Rx(@"\A(?<phase>PLAY|DRAW|POWER) PHASE \((?<n>[0-9]{1,6}) left\)\z");
 static readonly Regex Confirmation=Rx(@"\A(?<action>[A-Za-z][A-Za-z -]{0,80}) (?<n>[0-9]{1,6}) (?<unit>target|targets|card|cards|HP)\z");
 public static ConfirmationParts? ParseConfirmation(string source){
  if(source.Length>256)return null;
  var m=Confirmation.Match(source);if(!m.Success)return null;
  string number=m.Groups["n"].Value,unit=m.Groups["unit"].Value;
  if(unit!="HP" && ((number=="1")!=(unit=="target" || unit=="card")))return null;
  return new(m.Groups["action"].Value,number,unit,unit=="HP"?"hp-amount":unit.StartsWith("card",StringComparison.Ordinal)?"card-count":"target-count");
 }
 static readonly Regex Header=Rx(@"\A(?<prefix><b>[^<>\r\n]{1,200}:</b> )(?<body>.+)\z");
 static readonly Regex Single=Rx(@"\AClick a target to deal it <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Multiple=Rx(@"\AClick up to (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Effect=Rx(@"\AClick a button on (?<card>[^<>\r\n]{1,200}) to choose an effect\.\z");
 static readonly Regex DrawDiscard=Rx(@"\AClick a location to either draw or discard (?<card>[^<>\r\n]{1,200})\.\z");
 static readonly Regex ReplaceDiscard=Rx(@"\AClick a location to either replace or discard (?<card>[^<>\r\n]{1,200})\.\z");
 static readonly Regex DrawOwner=Rx(@"\AClick (?<owner>[^<>\r\n]{1,200})'s deck to draw (?<n>[0-9]{1,6}) (?<unit>card|cards)\.\z");
 static readonly Regex DiscardUpTo=Rx(@"\AClick up to (?<n>[0-9]{1,6}) cards in hand to discard them\.\z");
 static readonly Regex DestroyExact=Rx(@"\AClick (?<n>[0-9]{1,6}) (?<unit>card|cards) in play to destroy (?<pronoun>it|them)\.\z");
 static readonly Dictionary<string,string> DamageNames=new(StringComparer.Ordinal){["fire"]="火焰",["melee"]="近战",["cold"]="寒冷",["energy"]="能量",["infernal"]="邪能",["lightning"]="闪电",["projectile"]="投射",["psychic"]="精神",["radiant"]="光耀",["sonic"]="音波",["toxic"]="毒素"};
 static readonly Regex DiscardMany=Rx(@"\AClick (?<n>[0-9]{1,6}) cards in hand to discard them\.\z");
 static readonly Regex Reduce=Rx(@"\AClick up to (?<n>[0-9]{1,6}) targets to reduce damage dealt to them\.\z");
 static readonly Regex UnspecifiedDamage=Rx(@"\AClick a target to deal it (?<type>[a-z]+) damage\.\z");
 static readonly Regex DamageOrder=Rx(@"\AClick targets to choose the order of dealing them <nobr>(?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic)</nobr> damage\.\z");
 static readonly Regex Reaction=Rx(@"\A<b>(?<from>[^<>\r\n]{1,200})</b> will deal <b>(?<to>[^<>\r\n]{1,200})</b> (?<n>[0-9]{1,6}) (?<type>fire|melee|cold|energy|infernal|lightning|projectile|psychic|radiant|sonic|toxic) damage\. Click a (?<choice>reaction to use it|card to activate first)\.\z");
 static readonly Regex ExactTargets=Rx(@"\AClick (?<count>[0-9]{1,6}) targets to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex RequiredTarget=Rx(@"\AClick <b>(?<target>[^<>\r\n]{1,200})</b> and (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex RequiredUnspecified=Rx(@"\AClick <b>(?<target>[^<>\r\n]{1,200})</b>(?:, <b>(?<target>[^<>\r\n]{1,200})</b>){0,11} and (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them (?<type>[a-z]+) damage\.\z");
 static readonly Regex MultipleHpRank=Rx(@"\AClick (?<count>[0-9]{1,6}) targets to determine which has the (?<rank>lowest|highest) HP\.\z");
 static readonly Regex RemainingTargets=Rx(@"\AClick (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex RegainTargets=Rx(@"\A(?<count>[0-9]{1,6}) (?<unit>target|targets) regain HP\z");
 static readonly Regex RegainUpTo=Rx(@"\AClick up to (?<count>[0-9]{1,6}) targets to regain HP\.\z");
 static readonly Regex RequiredTargets=Rx(@"\AClick <b>(?<target>[^<>\r\n]{1,200})</b>(?:, <b>(?<target>[^<>\r\n]{1,200})</b>){1,11} and (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them <nobr>(?<n>[0-9]{1,6}) (?<type>[a-z]+)</nobr> damage\.\z");
 static readonly Regex ExactUnspecified=Rx(@"\AClick (?<count>[0-9]{1,6}) targets to deal them (?<type>[a-z]+) damage\.\z");
 static readonly Regex TargetMultiDamage=Rx(@"\AClick (?:(?<single>a target to deal it)|(?<upto>up to )?(?<count>[0-9]{1,6})(?<remaining> more)? (?<unit>target|targets) to deal (?<pronoun>it|them)) <nobr>(?<n1>[0-9]{1,6}) (?<type1>[a-z]+)</nobr>(?: and <nobr>(?<n2>[0-9]{1,6}) (?<type2>[a-z]+)</nobr>|, <nobr>(?<n2>[0-9]{1,6}) (?<type2>[a-z]+)</nobr>, and <nobr>(?<n3>[0-9]{1,6}) (?<type3>[a-z]+)</nobr>) damage\.\z");
 static readonly Regex RequiredMultiDamage=Rx(@"\AClick <b>(?<target>[^<>\r\n]{1,200})</b>(?:, <b>(?<target>[^<>\r\n]{1,200})</b>){0,11} and (?<count>[0-9]{1,6}) more (?<unit>target|targets) to deal them <nobr>(?<n1>[0-9]{1,6}) (?<type1>[a-z]+)</nobr>(?: and <nobr>(?<n2>[0-9]{1,6}) (?<type2>[a-z]+)</nobr>|, <nobr>(?<n2>[0-9]{1,6}) (?<type2>[a-z]+)</nobr>, and <nobr>(?<n3>[0-9]{1,6}) (?<type3>[a-z]+)</nobr>) damage\.\z");
 static readonly Regex DualDamage=Rx(@"\AClick a target to deal it <nobr>(?<n1>[0-9]{1,6}) (?<type1>[a-z]+)</nobr> and <nobr>(?<n2>[0-9]{1,6}) (?<type2>[a-z]+)</nobr> damage\.\z");
 static readonly Regex UnspecifiedOrder=Rx(@"\AClick targets to choose the order of dealing them (?<type>[a-z]+) damage\.\z");
 // The repeated 'to' is present in the game's current complete display string.
 static readonly Regex PlayLocation=Rx(@"\AClick a location to to play (?<card>[^<>\r\n]{1,200}) there\.\z");
 static readonly Regex PhaseActivate=Rx("\\AClick a (?:(?<target>target)|card(?<inplay> in play)?) to activate its <nobr>(?<sprite><sprite name=\"(?<phase>Start|Play|Power|Draw|End|Incapacitated)Phase\">) <b>(?<title>[A-Z]+) PHASE</b></nobr> effect\\.\\z");
 static readonly Dictionary<string,string> PhaseNames=new(StringComparer.Ordinal){["Start"]="开始阶段",["Play"]="出牌阶段",["Power"]="异能阶段",["Draw"]="抽牌阶段",["End"]="结束阶段",["Incapacitated"]="失能阶段"};
 const string Heroes="AbsoluteZero|Bunker|CaptainCosmic|Fanatic|Haka|Legacy|Ra|Tachyon|Tempest|TheArgentAdept|TheWraith|Unity";
 const string Villains="AkashBhuta|BaronBlade|CitizenDawn|GrandWarlordVoss|Omnitron|TheMatriarch";
 const string Environments="FreedomTower|InsulaPrimalis|Magmaria|Megalopolis|TheRuinsOfAtlantis|WagnerMarsBase";
 const string HeroPanel=@"Canvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]|5 Hero Layout/Inactive Hero Location [1-4])/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/";
 const string NonHeroPanel=@"Canvas/Main Panel/(?:Villain Row/(?:"+Villains+@")|(?:"+Environments+@"))/Inner Panel/Foreground Elements/";
 static readonly Regex HeroSelection=Rx(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]|5 Hero Layout/Inactive Hero Location [1-4])/)?(?:"+Heroes+@")/Inner Panel/Foreground Elements/Selection Button/Selection Button Label\z");
 static readonly Regex NextToCharacter=Rx(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]|5 Hero Layout/Inactive Hero Location [1-4])/)?(?<hero>"+Heroes+@")/Inner Panel/Character Card Container/\k<hero>Character/Foreground Elements/Next To Selection Button/Selection Button Label\z");
 // Observed small-card placement buttons in the play area and beside a character.
 static readonly Regex NextToSmallCard=Rx(@"\ACanvas/Main Panel/(?:(?:3 Hero Layout/Inactive Hero Location [12]|5 Hero Layout/Inactive Hero Location [1-4])/)?(?<hero>"+Heroes+@")/Inner Panel/(?:Foreground Elements/Play Area/Scroll View/Viewport/Card Container|Character Card Container/\k<hero>Character/Next To Location View/Card Container)/CardView (?<card>[A-Za-z][A-Za-z0-9_]{0,199})-[0-9]{1,6} SmallCardFront/Next To Selection Button/Selection Button Label\z");
 static readonly Regex DeckSelection=Rx(@"\A(?:"+HeroPanel+@"(?:Deck and Trash/Active Deck View|Inactive Deck View)|"+NonHeroPanel+@"Active Deck View)/Selection Button/Selection Button Label\z");
 static readonly Regex TrashSelection=Rx(@"\A(?:"+HeroPanel+@"(?:Deck and Trash/Active Trash View|Bottom Area Panel/Inactive Trash View)|"+NonHeroPanel+@"Active Trash View)/Selection Button/Selection Button Label\z");
 static readonly Dictionary<string,string> CommonButtons=new(StringComparer.Ordinal){["Play Construct"]="button.play.construct",["Discover Construct"]="button.discover.construct",["Cannot Deal Damage"]="button.common.1",["Deal Damage"]="button.common.2",["Deal Hero Targets Damage"]="button.common.3",["Deal cold damage"]="button.common.4",["Deal fire damage"]="button.common.5",["Destroy a Card"]="button.common.6",["Destroy this Card"]="button.common.7",["Double Damage"]="button.common.8",["Draw & Use Power"]="button.common.9",["Draw 2 Cards"]="button.common.10",["Draw Card"]="button.common.11",["Draw or Discard"]="button.common.12",["Play Card"]="button.common.13",["Play a Card"]="button.common.14",["Put Card Under Ordnance"]="button.common.15",["Put a Card"]="button.common.16",["Replace or Discard"]="button.common.17",["Salvage Item"]="button.common.18",["Salvage Ongoing"]="button.common.19",["Skip to Draw"]="button.common.20",["Skip to Power"]="button.common.21",["Use Power"]="button.common.22",["Use a Power"]="button.common.23"};
 static readonly Dictionary<string,string> SelectionTitles=new(StringComparer.Ordinal){["Select Damage Source to Deal Damage"]="hint.selection.1",["Select Hero to Discover"]="hint.selection.2",["Select Hero to destroy cards"]="hint.selection.3",["Select Hero to discard cards"]="hint.selection.4",["Select Hero to salvage cards"]="hint.selection.5",["Select Hero to take a phase now"]="hint.selection.6",["Select Location to play card to"]="hint.selection.7",["Select Location to play from"]="hint.selection.8",["Select Location to put a card from into the deck"]="hint.selection.9",["Select Location to put a card from on top of the deck"]="hint.selection.10",["Select Location to reveal from"]="hint.selection.11",["Select Location to shuffle its cards into the deck"]="hint.selection.12",["Select Redirection Target"]="hint.selection.13",["Select location to Summon from (decks will be shuffled)"]="hint.selection.14",["Select location to send destroyed card"]="hint.selection.15",["Select someone to put a card back in their deck"]="hint.selection.16",["Select someone to put a card back on top of their deck"]="hint.selection.17",["Select where it the card should be drawn or discarded."]="hint.selection.18",["Select where the card should go."]="hint.selection.19",["Select whether or not to draw a card."]="hint.selection.20",["Select which Hero is considered to have the most cards in hand"]="hint.selection.21",["Select which deck to draw from..."]="hint.selection.22",["Select which of these targets is considered to have the Lowest HP"]="hint.selection.23",["Select who is considered to have the most cards in play"]="hint.selection.24"};
 static readonly Regex EffectAmount=Rx(@"\A(?<before>Regain |Deal 1 Target |Deal Self |Deal )(?<n>[0-9]{1,6})(?<after> HP| Damage| damage| Targets 3 Damage)\z");
 string? CommonButton(string source){
  if(source=="Play Item")return Format("button.play.item");
  if(source=="Play Golem")return Format("button.play.golem");
  if(source=="Discover Item")return Format("button.discover.item");
  if(source=="Discover Golem")return Format("button.discover.golem");
  if(CommonButtons.TryGetValue(source,out var key))return Format(key);
  var m=EffectAmount.Match(source);if(!m.Success)return null;
  string n=m.Groups["n"].Value;
  return (m.Groups["before"].Value,m.Groups["after"].Value) switch {
   ("Regain "," HP")=>Format("button.regain.hp",n),
   ("Deal 1 Target "," Damage")=>Format("button.deal.one",n),
   ("Deal Self "," damage")=>Format("button.deal.self",n),
   ("Deal "," Targets 3 Damage")=>Format("button.deal.many",n),
   ("Deal "," damage")=>Format("button.deal.amount",n),_=>null};
 }
 public string? ApplyCardButton(string path,string card,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(source=="Play Here"){
   var next=NextToCharacter.Match(path);
   if(next.Success)return card==next.Groups["hero"].Value+"Character"?Format("button.play.here"):null;
   var small=NextToSmallCard.Match(path);
   return small.Success && card==small.Groups["card"].Value?Format("button.play.here"):null;
  }
  if(!path.StartsWith("Canvas/Main Panel/",StringComparison.Ordinal) && !path.StartsWith("Full Card Canvas/Full Card Panel/",StringComparison.Ordinal))return null;
  if(!path.EndsWith("/Select Effect Container/Select Effect Button(Clone)/Label",StringComparison.Ordinal))return null;
  return CommonButton(source);
 }
 static readonly Dictionary<string,(string Source,string Template)> ScopedButtons=new(StringComparer.Ordinal){
  ["Canvas/Main Panel/Button Panel/Right/Play Button/Text (TMP)"]=("Play!","button.newgame.play"),
  ["Canvas/Main Panel/Button Panel/Left/Randomize Button/Text (TMP)"]=("Randomize","button.newgame.randomize"),
  ["Canvas/Main Panel/Button Panel/Left/Back Button/Text (TMP)"]=("Back\n","button.newgame.back"),
  ["Canvas/Main Panel/Button Container/Bottom Buttons/Temp Share Button/Text"]=("Share","button.result.share"),
  ["Canvas/Main Panel/Message Panel/Panel/Confirm Container/Confirm Button/Text"]=("Continue","button.message.continue")
 };
 static readonly Regex RevealedTitlePath=Rx(@"\ACanvas/Revealed Cards Manager/\[RevealCardsAction: Source=(?<name>[^<>\r\n\[\]]{1,200})\]/Title Label\z");
 static readonly Regex DestroyMany=Rx(@"\AClick up to (?<n>[0-9]{1,6}) (?<kind>targets|cards in play) to destroy them\.\z");
 static readonly Regex ReducedHp=Rx(@"\A(?<name>[^<>\r\n]{1,200}) has been reduced to <nobr>(?<n>[0-9]{1,6}) HP\.</nobr>\z");
 static readonly Regex ZeroCardsUnder=Rx(@"\AThere are 0 cards under (?<name>[^<>\r\n]{1,200})\.\z");
 static readonly Regex Defeat=Rx(@"\A(?<name>[^<>\r\n]{1,200}) has triumphed, forcing the Heroes to regroup and fight another day\.\.\.\z");
 static readonly Regex Victory=Rx(@"\AYou have defeated (?<name>[^<>\r\n]{1,200}) and saved the day!\z");
 static readonly Regex Description=Rx(@"\A(?<name>[^<>\r\n]{1,200}) (?<verb>drew|revealed|played|discovered|summoned|collected):\z");
 static readonly Regex RelatedCards=Rx(@"\ACards (?<relation>under|next to) (?<name>[^<>\r\n]{1,200})\z");
 static readonly Regex PutUnder=Rx(@"\AClick a card in play to put (?<name>[^<>\r\n]{1,200}) under it\.\z");
 static readonly Regex SalvageTrashUpTo=Rx(@"\AClick up to (?<n>[0-9]{1,6}) (?<unit>card|cards) in the trash to salvage (?<pronoun>it|them)\.\z");
 static readonly Regex DestroyPlayOrUnder=Rx(@"\AClick a card in play or under (?<name>[^<>\r\n]{1,200}) to destroy it\.\z");
 static readonly Regex DestroyPlayOrUnderExact=Rx(@"\AClick (?<n>[0-9]{1,6}) (?<unit>card|cards) in play or under (?<name>[^<>\r\n]{1,200}) to destroy (?<pronoun>it|them)(?<first> before other cards)?\.\z");
 static readonly Regex DestroyPlayOrUnderUpTo=Rx(@"\AClick up to (?<n>[0-9]{1,6}) (?<unit>card|cards) in play or under (?<name>[^<>\r\n]{1,200}) to destroy (?<pronoun>it|them)(?<first> before other cards)?\.\z");
 static readonly Regex TrashOwnerDiscard=Rx(@"\AClick (?<owner>[^<>\r\n]{1,200})'s trash to discard (?<card>[^<>\r\n]{1,200})\.\z");
 public DynamicDisplayRules(Dictionary<string,string> values,Func<string,string>? nameResolver=null){
  resolveName=nameResolver??(s=>s);
  if(values==null || values.Count!=Specs.Count)throw new InvalidDataException("Incomplete dynamic pilot templates");
  foreach(var (key,spec) in Specs){
   if(!values.TryGetValue(key,out var value) || string.IsNullOrWhiteSpace(value) || value.Length>512)throw new InvalidDataException("Missing pilot template: "+key);
   var tokens=Regex.Matches(value,@"\{[^{}]*\}").Select(m=>m.Value).OrderBy(x=>x);
   if(!tokens.SequenceEqual(Enumerable.Range(0,spec.Args).Select(i=>"{"+i+"}").OrderBy(x=>x)))throw new InvalidDataException("Pilot argument mismatch: "+key);
   if(Regex.Replace(value,@"\{[0-9]+\}","").IndexOfAny(new[]{'{','}'})>=0)throw new InvalidDataException("Escaped or stray pilot braces: "+key);
   if(string.Concat(Regex.Matches(value,@"<[^<>]*>").Select(m=>m.Value))!=spec.Tags)throw new InvalidDataException("Pilot markup mismatch: "+key);
   try{string.Format(CultureInfo.InvariantCulture,value,Enumerable.Repeat<object>("test",spec.Args).ToArray());}catch(FormatException){throw new InvalidDataException("Invalid pilot format: "+key);}
  }
  templates=new(values,StringComparer.Ordinal);
 }
 string Format(string id,params object[] args)=>string.Format(CultureInfo.InvariantCulture,templates[id],args.Select(a=>a is string s?(object)resolveName(s):a).ToArray());
 public string Glyphs()=>string.Concat(templates.Values)+string.Concat(DamageNames.Values)+string.Concat(PhaseNames.Values)+"在场";
 public string? Apply(string path,string source){
  if(string.IsNullOrEmpty(source) || source.Length>2048)return null;
  if(ScopedButtons.TryGetValue(path,out var button) && source==button.Source)return Format(button.Template);
  if(path==CardsUnderPath || path==CardsNextToPath){
   var cards=RelatedCards.Match(source);
   if(!cards.Success || string.IsNullOrWhiteSpace(cards.Groups["name"].Value))return null;
   bool under=cards.Groups["relation"].Value=="under";
   return under==(path==CardsUnderPath)?Format(under?"heading.cards.under":"heading.cards.next",cards.Groups["name"].Value):null;
  }
  if(path==PhasePath){
   foreach(var item in PhaseNames)if(source==item.Key.ToUpperInvariant()+" PHASE")return item.Value;
   if(source=="SETUP")return Format("phase.setup");
   var phase=PhaseRemaining.Match(source);
   return phase.Success?Format(phase.Groups["phase"].Value switch {"PLAY"=>"phase.play.remaining","DRAW"=>"phase.draw.remaining",_=>"phase.power.remaining"},phase.Groups["n"].Value):null;
  }
  if(path==FinishPath){
   var regain=RegainTargets.Match(source);
   if(regain.Success && ((regain.Groups["count"].Value=="1")== (regain.Groups["unit"].Value=="target")))return Format("finish.regain.targets",regain.Groups["count"].Value);
   var parts=ParseConfirmation(source);
   // Shared grammar does not authorize translating an unverified action/unit pair.
   return parts switch {
    {Action:"Damage",NumberKind:"target-count"}=>Format("finish.damage",parts.Number),
    {Action:"Discard",NumberKind:"card-count"}=>Format("finish.discard",parts.Number),
    {Action:"Destroy",NumberKind:"card-count"}=>Format("finish.destroy",parts.Number),
    {Action:"Select",NumberKind:"card-count"}=>Format("finish.select",parts.Number),_=>null};
  }
  if(path==ContinuePath)return source switch {"Skip"=>Format("button.skip"),"Skip Play"=>Format("button.skip.play.action"),"Skip Draw"=>Format("button.skip.draw.action"),"Skip Power"=>Format("button.skip.power.action"),"Skip Incapacitated Phase"=>Format("button.skip.incapacitated"),"Skip Play Phase"=>Format("button.skip.play"),"Skip Power Phase"=>Format("button.skip.power"),_=>null};
  if(path==DrawPath && source=="Draw")return Format("button.draw");
  if(TrashSelection.IsMatch(path))return source switch {"Discard"=>Format("button.discard"),"Trash"=>Format("button.location.trash"),_=>null};
  if(HeroSelection.IsMatch(path))return source switch {"Draw"=>Format("button.draw"),"Discard"=>Format("button.discard"),"Play a Card"=>Format("button.play.card"),"Use a Power"=>Format("button.use.power"),"Put a Card"=>Format("button.put.card"),"Destroy a Card"=>Format("button.destroy.card"),"Has the Fewest"=>Format("button.fewest"),"Has the Most"=>Format("button.most"),"Take Play Phase"=>Format("button.take.play"),"Take Power Phase"=>Format("button.take.power"),_=>null};
  if(DeckSelection.IsMatch(path))return source switch {"Deck"=>Format("button.location.deck"),"Reveal"=>Format("button.reveal"),"Draw"=>Format("button.draw"),"Replace"=>Format("button.replace"),_=>null};
  if(path==DescriptionPath){
   var m=Description.Match(source);if(!m.Success)return null;
   return Format(m.Groups["verb"].Value switch {"drew"=>"description.draw","revealed"=>"description.reveal","discovered"=>"description.discover","summoned"=>"description.summon","collected"=>"description.collect",_=>"description.play"},m.Groups["name"].Value);
  }
  var revealedTitle=RevealedTitlePath.Match(path);
  if(revealedTitle.Success){
   var title=Description.Match(source);
   return title.Success && title.Groups["verb"].Value=="revealed" && title.Groups["name"].Value==revealedTitle.Groups["name"].Value?Format("description.reveal",title.Groups["name"].Value):null;
  }
  if(path=="Canvas/Main Panel/Message Panel/Panel/Heading Label" && source=="Game Over")return Format("message.game-over");
  if(path=="Canvas/Main Panel/Message Panel/Panel/Message Label"){
   if(source=="All of the Heroes are incapacitated.")return Format("message.all-incapacitated");
   if(source=="There are 15 or more cards in the Villain trash.")return Format("message.villain.trash15");
   if(source=="There are 10 or more Villain targets in play.")return Format("message.villain.targets10");
   var zero=ZeroCardsUnder.Match(source);if(zero.Success)return Format("message.zero.under",zero.Groups["name"].Value);
   var hp=ReducedHp.Match(source);return hp.Success?Format("result.hp",hp.Groups["name"].Value,hp.Groups["n"].Value):null;
  }
  if(path=="Canvas/Main Panel/Heading Label"){
   if(source=="The Wagner Mars Base exploded!")return Format("result.wagner.exploded");
   var defeat=Defeat.Match(source);if(defeat.Success)return Format("result.defeat",defeat.Groups["name"].Value);
   var victory=Victory.Match(source);return victory.Success?Format("result.victory",victory.Groups["name"].Value):null;
  }
  if(path!=SurfaceProbeRules.HintPath)return null;
  var header=Header.Match(source);
  string prefix=header.Success?header.Groups["prefix"].Value:"",body=header.Success?header.Groups["body"].Value:source;
  var fixedText=body switch {
   "Click a button to choose how much damage to deal."=>"hint.damage.amount",
   "Click a card to replace it."=>"hint.card.replace",
   "Click a hero to discard their hand."=>"hint.hero.discard.hand",
   "Click a Hero to discard their hand."=>"hint.hero.discard.hand",
   "Click a location to search it for a card to summon."=>"hint.summon.search",
   "Click a target to make it have no game text."=>"hint.blank.target",
   "Click a card in play to make it have no game text."=>"hint.blank.inplay",
   "Click a card to draw it."=>"hint.card.draw",
   "Click a card to discard it."=>"hint.card.discard",
   "Click a card to bury it."=>"hint.card.bury",
   "Click a target to put it on top of its deck."=>"hint.target.topdeck",
   "Click a Hero to regain HP."=>"hint.regain.hero",
   "Click the hero who is considered to have the most cards in play."=>"hint.hero.most.play",
   "Click a card in the trash to put it under a card."=>"hint.trash.under",
   "Click a hero to salvage a card."=>"hint.hero.salvage",
   "Click a card in the trash to salvage it."=>"hint.trash.salvage",
   "Click a hero to draw a card."=>"hint.hero.draw",
   "Click a hero to discard a card."=>"hint.hero.discard",
   "Click a card in hand to put it under a card."=>"hint.hand.under",
   "Click a card in the deck to collect it."=>"hint.collect.deck",
   "Click a card in the deck or in the trash to summon it."=>"hint.summon.deck.trash",
   "Click a target to prevent it from dealing damage."=>"hint.prevent.dealing",
   SurfaceProbeRules.PlayHint=>"hint.play", "Click a card to play it."=>"hint.play.click",
   "Click or drag a power to use it."=>"hint.power.drag", "Click a power to use it."=>"hint.power.click",
   "Click a hero to play a card."=>"hint.hero.play", "Click a hero to use a power."=>"hint.hero.power",
   "Click an incapacitated ability to use it."=>"hint.incapacitated", "Click a button to choose a damage type."=>"hint.damage.type",
   "Click a target to determine which has the lowest HP."=>"hint.lowest.hp",
   "Click a hero to take their Play phase now."=>"hint.hero.take.play",
   "Click a hero to take their Power phase now."=>"hint.hero.take.power",
   "Click a hero to destroy a card."=>"hint.hero.destroy", "Click a card in play to destroy it."=>"hint.card.destroy",
   "Click the hero who is considered to have the fewest cards in hand."=>"hint.hero.fewest",
   "Click an ability to use it."=>"hint.ability", "Click a card in the deck to summon it."=>"hint.summon",
   "Click a hero to have them put a card on top of their deck."=>"hint.hero.topdeck",
   "Click a card in the trash to put it on top of its deck."=>"hint.trash.topdeck",
   "Click a card in hand to discard it."=>"hint.discard.single", "Click a deck to reveal a card."=>"hint.deck.reveal",_=>null};
  if(fixedText!=null)return prefix+Format(fixedText);
  if(SelectionTitles.TryGetValue(body,out var selectionTitle))return prefix+Format(selectionTitle);
  var salvage=SalvageTrashUpTo.Match(body);
  if(salvage.Success && (salvage.Groups["n"].Value=="1")== (salvage.Groups["unit"].Value=="card") && (salvage.Groups["n"].Value=="1")== (salvage.Groups["pronoun"].Value=="it"))return Format("hint.trash.salvage.upto",prefix,salvage.Groups["n"].Value);
  var ownerDiscard=TrashOwnerDiscard.Match(body);
  if(ownerDiscard.Success)return Format("hint.trash.owner.discard",prefix,ownerDiscard.Groups["owner"].Value,ownerDiscard.Groups["card"].Value);
  var underExact=DestroyPlayOrUnderExact.Match(body);
  if(underExact.Success && (underExact.Groups["n"].Value=="1")== (underExact.Groups["unit"].Value=="card") && (underExact.Groups["n"].Value=="1")== (underExact.Groups["pronoun"].Value=="it"))return Format(underExact.Groups["first"].Success?"hint.destroy.play.under.exact.first":"hint.destroy.play.under.exact",prefix,underExact.Groups["n"].Value,underExact.Groups["name"].Value);
  var underUpTo=DestroyPlayOrUnderUpTo.Match(body);
  if(underUpTo.Success && !string.IsNullOrWhiteSpace(underUpTo.Groups["name"].Value) && (underUpTo.Groups["n"].Value=="1")== (underUpTo.Groups["unit"].Value=="card") && (underUpTo.Groups["n"].Value=="1")== (underUpTo.Groups["pronoun"].Value=="it"))return Format(underUpTo.Groups["first"].Success?"hint.destroy.play.under.upto.first":"hint.destroy.play.under.upto",prefix,underUpTo.Groups["n"].Value,underUpTo.Groups["name"].Value);
  var replace=ReplaceDiscard.Match(body);
  if(replace.Success)return Format("hint.replace-discard",prefix,replace.Groups["card"].Value);
  var ownerDraw=DrawOwner.Match(body);
  if(ownerDraw.Success && (ownerDraw.Groups["n"].Value=="1")== (ownerDraw.Groups["unit"].Value=="card"))return Format("hint.draw.owner",prefix,ownerDraw.Groups["owner"].Value,ownerDraw.Groups["n"].Value);
  var discardUpTo=DiscardUpTo.Match(body);
  if(discardUpTo.Success)return Format("hint.discard.upto",prefix,discardUpTo.Groups["n"].Value);
  var destroyExact=DestroyExact.Match(body);
  if(destroyExact.Success && (destroyExact.Groups["n"].Value=="1")== (destroyExact.Groups["unit"].Value=="card") && (destroyExact.Groups["n"].Value=="1")== (destroyExact.Groups["pronoun"].Value=="it"))return Format("hint.destroy.exact",prefix,destroyExact.Groups["n"].Value);
  var underHint=PutUnder.Match(body);
  if(underHint.Success && !string.IsNullOrWhiteSpace(underHint.Groups["name"].Value))return Format("hint.put.under",prefix,underHint.Groups["name"].Value);
  underHint=DestroyPlayOrUnder.Match(body);
  if(underHint.Success && !string.IsNullOrWhiteSpace(underHint.Groups["name"].Value))return Format("hint.destroy.play.under",prefix,underHint.Groups["name"].Value);
  var place=PlayLocation.Match(body);
  if(place.Success)return Format("hint.play.location",prefix,place.Groups["card"].Value);
  // Compose two complete recognized instructions; never translate a prefix of an unknown remainder.
  const string powerChoice="Click a power to use it. ";
  const string playChoice="Click or drag a card to play it. ";
  string choice=body.StartsWith(powerChoice,StringComparison.Ordinal)?powerChoice:body.StartsWith(playChoice,StringComparison.Ordinal)?playChoice:"";
  bool combined=choice.Length>0;
  var phaseHint=PhaseActivate.Match(combined?body.Substring(choice.Length):body);
  if(phaseHint.Success && (!combined || (!phaseHint.Groups["target"].Success && phaseHint.Groups["phase"].Value==(choice==powerChoice?"Power":"Play"))) && phaseHint.Groups["title"].Value==phaseHint.Groups["phase"].Value.ToUpperInvariant())
   return prefix+(combined?Format(choice==powerChoice?"hint.power.click":"hint.play")+" ":"")+(phaseHint.Groups["target"].Success?Format("hint.phase.target",phaseHint.Groups["sprite"].Value,PhaseNames[phaseHint.Groups["phase"].Value]):Format("hint.phase.activate",phaseHint.Groups["inplay"].Success?"在场的":"",phaseHint.Groups["sprite"].Value,PhaseNames[phaseHint.Groups["phase"].Value]));
  var destroy=DestroyMany.Match(body);
  if(destroy.Success)return Format(destroy.Groups["kind"].Value=="targets"?"hint.destroy.targets":"hint.destroy.cards",prefix,destroy.Groups["n"].Value);
  var regainHint=RegainUpTo.Match(body);
  if(regainHint.Success)return Format("hint.regain.upto",prefix,regainHint.Groups["count"].Value);
  var hpRank=MultipleHpRank.Match(body);
  if(hpRank.Success && hpRank.Groups["count"].Value!="1")return Format("hint.hp.rank.multiple",prefix,hpRank.Groups["count"].Value,hpRank.Groups["rank"].Value=="lowest"?"低":"高");
  if(!header.Success)return null;
  var requiredUnspecified=RequiredUnspecified.Match(body);
  if(requiredUnspecified.Success && ((requiredUnspecified.Groups["count"].Value=="1")== (requiredUnspecified.Groups["unit"].Value=="target")) && DamageNames.TryGetValue(requiredUnspecified.Groups["type"].Value,out var requiredNoAmountType)){
   string targets=string.Join("、",requiredUnspecified.Groups["target"].Captures.Cast<Capture>().Select(c=>"<b>"+resolveName(c.Value)+"</b>"));
   return Format("hint.attack.required.unspecified",prefix,targets,requiredUnspecified.Groups["count"].Value,requiredNoAmountType);
  }
  var remaining=RemainingTargets.Match(body);
  if(remaining.Success && ((remaining.Groups["count"].Value=="1")== (remaining.Groups["unit"].Value=="target")) && DamageNames.TryGetValue(remaining.Groups["type"].Value,out var remainingType))
   return Format("hint.attack.remaining",prefix,remaining.Groups["count"].Value,remaining.Groups["n"].Value,remainingType);
  var targetMulti=TargetMultiDamage.Match(body);
  if(targetMulti.Success && !(targetMulti.Groups["upto"].Success && targetMulti.Groups["remaining"].Success) &&
     (targetMulti.Groups["single"].Success || ((targetMulti.Groups["count"].Value=="1")== (targetMulti.Groups["unit"].Value=="target") && (targetMulti.Groups["count"].Value=="1" || targetMulti.Groups["pronoun"].Value=="them")))){
   var amounts=new List<string>();bool valid=true;
   for(int i=1;i<=3;i++){
    if(!targetMulti.Groups["n"+i].Success)continue;
    if(!DamageNames.TryGetValue(targetMulti.Groups["type"+i].Value,out var type)){valid=false;break;}
    amounts.Add(targetMulti.Groups["n"+i].Value);amounts.Add(type);
   }
   if(valid){
    bool single=targetMulti.Groups["single"].Success;
    string kind=single?"single":targetMulti.Groups["upto"].Success?"upto":targetMulti.Groups["remaining"].Success?"remaining":"exact";
    string key=single && amounts.Count==4?"hint.attack.dual":"hint.attack."+kind+(amounts.Count==6?".triple":".dual");
    var args=new List<string>{prefix};if(!single)args.Add(targetMulti.Groups["count"].Value);args.AddRange(amounts);
    return Format(key,args.ToArray());
   }
  }
  var multiDamage=RequiredMultiDamage.Match(body);
  if(multiDamage.Success && ((multiDamage.Groups["count"].Value=="1")== (multiDamage.Groups["unit"].Value=="target")) &&
     DamageNames.TryGetValue(multiDamage.Groups["type1"].Value,out var multiType1) && DamageNames.TryGetValue(multiDamage.Groups["type2"].Value,out var multiType2)){
   string targets=string.Join("、",multiDamage.Groups["target"].Captures.Cast<Capture>().Select(c=>"<b>"+resolveName(c.Value)+"</b>"));
   if(!multiDamage.Groups["n3"].Success)return Format("hint.attack.required.dual",prefix,targets,multiDamage.Groups["count"].Value,multiDamage.Groups["n1"].Value,multiType1,multiDamage.Groups["n2"].Value,multiType2);
   if(DamageNames.TryGetValue(multiDamage.Groups["type3"].Value,out var multiType3))return Format("hint.attack.required.triple",prefix,targets,multiDamage.Groups["count"].Value,multiDamage.Groups["n1"].Value,multiType1,multiDamage.Groups["n2"].Value,multiType2,multiDamage.Groups["n3"].Value,multiType3);
  }
  var dual=DualDamage.Match(body);
  if(dual.Success && DamageNames.TryGetValue(dual.Groups["type1"].Value,out var type1) && DamageNames.TryGetValue(dual.Groups["type2"].Value,out var type2))
   return Format("hint.attack.dual",prefix,dual.Groups["n1"].Value,type1,dual.Groups["n2"].Value,type2);
  var requiredList=RequiredTargets.Match(body);
  if(requiredList.Success && ((requiredList.Groups["count"].Value=="1")== (requiredList.Groups["unit"].Value=="target")) && DamageNames.TryGetValue(requiredList.Groups["type"].Value,out var listType)){
   // Only the individually parsed, complete name fields are resolved; retain every bold span.
   string targets=string.Join("、",requiredList.Groups["target"].Captures.Cast<Capture>().Select(c=>"<b>"+resolveName(c.Value)+"</b>"));
   return Format("hint.attack.required.list",prefix,targets,requiredList.Groups["count"].Value,requiredList.Groups["n"].Value,listType);
  }
  var exactUnspecified=ExactUnspecified.Match(body);
  if(exactUnspecified.Success && exactUnspecified.Groups["count"].Value!="1" && DamageNames.TryGetValue(exactUnspecified.Groups["type"].Value,out var noAmountType))
   return Format("hint.attack.exact.unspecified",prefix,exactUnspecified.Groups["count"].Value,noAmountType);
  var match=ExactTargets.Match(body);
  if(match.Success && DamageNames.TryGetValue(match.Groups["type"].Value,out var exactType))
   return Format("hint.attack.exact",prefix,match.Groups["count"].Value,match.Groups["n"].Value,exactType);
  match=RequiredTarget.Match(body);
  if(match.Success && ((match.Groups["count"].Value=="1")== (match.Groups["unit"].Value=="target")) && DamageNames.TryGetValue(match.Groups["type"].Value,out var requiredType))
   return Format("hint.attack.required",prefix,match.Groups["target"].Value,match.Groups["count"].Value,match.Groups["n"].Value,requiredType);
  match=UnspecifiedOrder.Match(body);
  if(match.Success && DamageNames.TryGetValue(match.Groups["type"].Value,out var orderType))return Format("hint.attack.order.unspecified",prefix,orderType);
  match=Single.Match(body);
  if(match.Success)return Format("hint.attack.single",prefix,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Multiple.Match(body);
  if(match.Success)return Format("hint.attack.multiple",prefix,match.Groups["count"].Value,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=DiscardMany.Match(body);if(match.Success)return Format("hint.discard.multiple",prefix,match.Groups["n"].Value);
  match=Reduce.Match(body);if(match.Success)return Format("hint.reduce",prefix,match.Groups["n"].Value);
  match=UnspecifiedDamage.Match(body);if(match.Success && DamageNames.TryGetValue(match.Groups["type"].Value,out var unspecifiedType))return Format("hint.attack.unspecified",prefix,unspecifiedType);
  match=DamageOrder.Match(body);if(match.Success)return Format("hint.attack.order",prefix,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Reaction.Match(body);if(match.Success)return Format(match.Groups["choice"].Value=="card to activate first"?"hint.reaction.first":"hint.reaction",prefix,match.Groups["from"].Value,match.Groups["to"].Value,match.Groups["n"].Value,DamageNames[match.Groups["type"].Value]);
  match=Effect.Match(body);if(match.Success)return Format("hint.effect",prefix,match.Groups["card"].Value);
  match=DrawDiscard.Match(body);if(match.Success)return Format("hint.draw-discard",prefix,match.Groups["card"].Value);
  return null;
 }
}
