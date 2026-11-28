namespace SotM2Chinese;
// The player compiler removes calls, including argument evaluation.
internal static class Diagnostics {
 [System.Diagnostics.Conditional("SOTM_DIAGNOSTICS")]
 internal static void Info(object message) => Plugin.Instance.Log.LogInfo(message);
}
