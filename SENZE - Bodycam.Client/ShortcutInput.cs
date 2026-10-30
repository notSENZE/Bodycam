using BepInEx.Configuration;
using UnityEngine;

namespace SenzeBodycam;

internal static class ShortcutInput
{
    internal static bool IsDown(KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None
            && Input.GetKeyDown(shortcut.MainKey)
            && AreModifiersHeld(shortcut);
    }

    private static bool AreModifiersHeld(KeyboardShortcut shortcut)
    {
        foreach (var modifier in shortcut.Modifiers)
        {
            if (!Input.GetKey(modifier))
            {
                return false;
            }
        }

        return true;
    }
}
