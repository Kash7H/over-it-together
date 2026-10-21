using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace OverItTogether
{
    /// <summary>Player settings, saved in BepInEx/config/kash.overittogether.cfg.</summary>
    public static class Settings
    {
        public static ConfigEntry<bool> ShowGhosts, ShowNames, ShowHud, ShowFeed;

        public static void Bind(ConfigFile cfg)
        {
            ShowGhosts = cfg.Bind("Display", "ShowGhosts", true, "Show other players");
            ShowNames = cfg.Bind("Display", "ShowNames", true, "Show name tags above other players");
            ShowHud = cfg.Bind("Display", "ShowHud", true, "Show the race HUD in the top left");
            ShowFeed = cfg.Bind("Display", "ShowFeed", true, "Show split and join messages");
        }
    }

    static class MenuPatches
    {
        // In a level the game's Esc goes straight to the main menu. With the mod, Esc opens our pause menu instead
        // (it has its own Main menu button).
        [HarmonyPatch(typeof(MainMenuButton), "Update"), HarmonyPrefix]
        static bool EscOpensPauseMenu() => !Plugin.InLevel;
    }
}
