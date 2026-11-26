namespace SotM2Chinese;
internal static class DisplayScope {
 // Do not classify symbols (minus signs, ellipses, spaces, sprite tags) as Chinese.
 internal static bool ContainsHan(string value) {
  foreach(char c in value)
   if((c>='\u3400' && c<='\u9fff') || (c>='\uf900' && c<='\ufaff')) return true;
  return false;
 }
}
