using Deucarian.API;
using UnityEditor;

namespace Deucarian.API.Editor
{
    /// <summary>
    /// Persists the advanced raw-JSON preference used by Control Center.
    /// </summary>
    [InitializeOnLoad]
    public static class APIDebugSettingsMenu
    {
        private const string PREF_KEY  = "Deucarian.API.LogRawJson";

        static APIDebugSettingsMenu()
        {
            bool storedValue = EditorPrefs.GetBool(PREF_KEY, APIDebugSettings.LogRawJson);
            APIDebugSettings.LogRawJson = storedValue;
        }

        internal static bool LogRawJsonEnabled =>
            EditorPrefs.GetBool(PREF_KEY, APIDebugSettings.LogRawJson);

        internal static void ToggleLogRawJson()
        {
            bool newValue = !EditorPrefs.GetBool(PREF_KEY, false);
            EditorPrefs.SetBool(PREF_KEY, newValue);
            APIDebugSettings.LogRawJson = newValue;
            ApiLog.General.Info($"APIDebugSettings.LogRawJson is now: {newValue}");
        }

    }
}
