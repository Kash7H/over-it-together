using System.IO;
using System.Xml.Serialization;
using BepInEx;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace OverItTogether
{
    /// <summary>
    /// Protects the player's own single-player climb.
    /// - Before any race reset, teleport or practice load, the current climb is stored and the game's own saving is paused.
    /// - "Return to my climb" puts the player back exactly and resumes saving.
    /// - While paused (or in a dev test copy) nothing that counts as beating the game can be recorded:
    ///   no save writes, no NumWins, no Steam achievements/stats.
    /// If the game is closed while paused, the real save is simply the one from before the race.
    /// </summary>
    public static class Guard
    {
        public static bool Holding { get; private set; }
        static SaveState personal;
        static string FilePath => Path.Combine(Paths.ConfigPath, "OverItTogether.my-climb.xml");

        public static bool BlockSaves => Holding || Plugin.NoSave;

        /// <summary>Call before anything that moves the player away from their own climb.</summary>
        public static void Hold()
        {
            if (Holding) return;
            var sav = Object.FindObjectOfType<Saviour>();
            if (sav == null) return;
            personal = sav.Save();
            try
            {
                using (var fs = File.Create(FilePath)) new XmlSerializer(typeof(SaveState)).Serialize(fs, personal);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Could not write climb backup: " + e.Message); }
            Holding = true;
            Session.I?.Notice("Your own climb is saved and paused");
        }

        /// <summary>While holding, nobody goes above the race finish into the ending area (no spoilers, no accidental finish).</summary>
        public static void EnforceCeiling()
        {
            if (!Holding || !Plugin.InLevel || PoseCapture.Player == null) return;
            var y = PoseCapture.Player.transform.position.y;
            if (y > CeilingY) { Practice.TeleportTo(new Vector3(62.05f, 359.61f, 0)); Session.I?.Notice("That's the finish - the ending stays locked during races"); }
        }
        public static float CeilingY = 420f;   // above the last checkpoint (Spawn22, y 360), below the ending (Spawn23, y 501)

        /// <summary>Make where you are now your real progress (e.g. you got higher in a race) and resume saving.</summary>
        public static void KeepCurrent()
        {
            if (!Holding) return;
            Holding = false;
            personal = null;
            try { File.Delete(FilePath); } catch { }
            Session.I?.Notice("This is now your own climb");
        }

        /// <summary>Put the player back on their own climb and resume normal saving.</summary>
        public static void Release()
        {
            if (!Holding) return;
            var sav = Object.FindObjectOfType<Saviour>();
            if (personal == null && File.Exists(FilePath))
                using (var fs = File.OpenRead(FilePath)) personal = (SaveState)new XmlSerializer(typeof(SaveState)).Deserialize(fs);
            if (sav != null && personal != null) sav.Load(personal);
            Holding = false;
            try { File.Delete(FilePath); } catch { }
            Session.I?.Notice("Back on your own climb");
        }
    }

    static class GuardPatches
    {
        [HarmonyPatch(typeof(Saviour), nameof(Saviour.SaveGameNow)), HarmonyPrefix]
        static bool NoSaveWhileHolding() => !Guard.BlockSaves;

        // "game completed" counter
        [HarmonyPatch(typeof(PlayerPrefs), nameof(PlayerPrefs.SetInt)), HarmonyPrefix]
        static bool NoWinCount(string key) => !(Guard.BlockSaves && key == "NumWins");

        // Steam achievements and stats
        [HarmonyPatch(typeof(SteamUserStats), nameof(SteamUserStats.SetAchievement)), HarmonyPrefix]
        static bool NoAchievements(ref bool __result) { if (!Guard.BlockSaves) return true; __result = false; return false; }

        [HarmonyPatch(typeof(SteamUserStats), nameof(SteamUserStats.StoreStats)), HarmonyPrefix]
        static bool NoStats(ref bool __result) { if (!Guard.BlockSaves) return true; __result = false; return false; }
    }
}
