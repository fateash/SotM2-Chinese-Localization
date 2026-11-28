namespace SotM2Chinese;
internal static class VillainDeckBackRules {
 internal static readonly IReadOnlyDictionary<string,(int Width,int Height)> Sizes =
  new Dictionary<string,(int,int)>(StringComparer.Ordinal) {
   ["BaronBladeDeckBack"]=(600,600),["AkashBhutaDeckBack"]=(600,600),
   ["CitizenDawnDeckBack"]=(512,512),["GrandWarlordVossDeckBack"]=(600,600),
   ["OmnitronDeckBack"]=(600,600),["TheMatriarchDeckBack"]=(600,600)
  };
 internal static bool CanReplace(string expected,string actual,bool boundMain,bool deckBack,bool fullCard,float width,float height) =>
  boundMain && deckBack && !fullCard && Sizes.TryGetValue(expected,out var size) &&
  string.Equals(expected,actual,StringComparison.Ordinal) && width==size.Width && height==size.Height;
}
