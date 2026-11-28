namespace SotM2Chinese;

// Exact sprite identity only. Variants and other artwork deliberately have no mapping.
internal static class VillainLogoRules {
 internal static readonly IReadOnlyDictionary<string,(int Width,int Height)> Sizes =
  new Dictionary<string,(int,int)>(StringComparer.Ordinal) {
   ["BaronBladeCharacterLogo"]=(1000,328), ["AkashBhutaCharacterLogo"]=(1000,197),
   ["CitizenDawnCharacterLogo"]=(986,700), ["GrandWarlordVossCharacterLogo"]=(1000,580),
   ["OmnitronCharacterLogo"]=(1000,167), ["TheMatriarchCharacterLogo"]=(1000,403)
  };
 internal static bool CanReplace(string expected,string actual,bool boundLogo) =>
  boundLogo && Sizes.ContainsKey(expected) && string.Equals(expected,actual,StringComparison.Ordinal);
}
