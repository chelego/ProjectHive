using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ProjectHive.EditorTools
{
    [InitializeOnLoad]
    public static class EditorShortcutUtility
    {
        private const string ProjectHiveShortcutProfileId = "ProjectHiveShortcuts";
        private const string PreviousProfileSessionKey = "ProjectHive.EditorShortcuts.PreviousProfile";

        private static readonly string[] PlayModeDisabledShortcutIds =
        {
            "Main Menu/File/Save",
            "Main Menu/File/Save As...",
            "Main Menu/Window/General/Scene",
            "Main Menu/Window/General/Game",
            "Main Menu/Window/General/Inspector",
            "Main Menu/Window/General/Hierarchy",
            "Main Menu/Window/General/Project",
        };

        static EditorShortcutUtility()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("ProjectHive/Editor Shortcuts/Apply Play Mode Shortcut Guard")]
        public static void ApplyPlayModeShortcutGuard()
        {
            IShortcutManager shortcutManager = ShortcutManager.instance;
            EnsureProjectHiveProfile(shortcutManager);

            DisablePlayModeShortcuts(shortcutManager);
            Debug.Log($"Disabled editor shortcuts in '{shortcutManager.activeProfileId}' profile while testing play mode controls.");
        }

        [MenuItem("ProjectHive/Editor Shortcuts/Apply Play Mode Shortcut Guard", true)]
        private static bool CanApplyPlayModeShortcutGuard()
        {
            return PlayModeDisabledShortcutIds.All(ShortcutManager.instance.GetAvailableShortcutIds().Contains);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SessionState.SetString(PreviousProfileSessionKey, ShortcutManager.instance.activeProfileId);
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    ApplyPlayModeShortcutGuard();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    RestorePreviousShortcutProfile();
                    break;
            }
        }

        private static void EnsureProjectHiveProfile(IShortcutManager shortcutManager)
        {
            if (!shortcutManager.IsProfileIdValid(ProjectHiveShortcutProfileId))
                shortcutManager.CreateProfile(ProjectHiveShortcutProfileId);

            shortcutManager.activeProfileId = ProjectHiveShortcutProfileId;
        }

        private static void DisablePlayModeShortcuts(IShortcutManager shortcutManager)
        {
            foreach (string shortcutId in PlayModeDisabledShortcutIds)
                shortcutManager.RebindShortcut(shortcutId, ShortcutBinding.empty);
        }

        private static void RestorePreviousShortcutProfile()
        {
            IShortcutManager shortcutManager = ShortcutManager.instance;
            string previousProfileId = SessionState.GetString(PreviousProfileSessionKey, string.Empty);

            if (string.IsNullOrEmpty(previousProfileId) || !shortcutManager.IsProfileIdValid(previousProfileId))
                return;

            shortcutManager.activeProfileId = previousProfileId;
            SessionState.EraseString(PreviousProfileSessionKey);
        }
    }
}
