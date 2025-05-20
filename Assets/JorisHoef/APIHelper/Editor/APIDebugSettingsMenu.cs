using UnityEditor;
using UnityEngine;

namespace JorisHoef.APIHelper.Editor
{
    /// <summary>
    /// Adds top‐menu entry under "Develop-Options" to toggle the APIDebugSettings.LogRawJson flag at runtime
    /// </summary>
    [InitializeOnLoad]
    public static class APIDebugSettingsMenu
    {
        private const string MENU_PATH = "Develop-Options/Log Raw JSON";
        private const string PREF_KEY   = "JorisHoef.APIHelper.LogRawJson";
    
        static APIDebugSettingsMenu()
        {
            bool storedValue = EditorPrefs.GetBool(PREF_KEY, APIDebugSettings.LogRawJson);
            APIDebugSettings.LogRawJson = storedValue;
        }

        [MenuItem(MENU_PATH)]
        private static void ToggleLogRawJson()
        {
            bool current  = EditorPrefs.GetBool(PREF_KEY, false);
            bool newValue = !current;
            EditorPrefs.SetBool(PREF_KEY, newValue);
            APIDebugSettings.LogRawJson = newValue;
            Debug.Log($"APIDebugSettings.LogRawJson is now: {newValue}");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleLogRawJsonValidate()
        {
            Menu.SetChecked(MENU_PATH, EditorPrefs.GetBool(PREF_KEY, false));
            return true;
        }
    }
}