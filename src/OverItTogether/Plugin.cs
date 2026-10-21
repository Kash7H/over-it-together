using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OverItTogether
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "kash.overittogether";
        public const string Name = "Over It Together";
        public const string Version = "0.2.0";
        static readonly string[] TestedGameVersions = { "1.7" };

        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal static bool InLevel;
        internal static bool Disabled;
        internal static string DisabledReason = "";
        internal static bool NoSave;
        internal static bool IsDev => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OIT_DEV_PORT"));   // dev: the second game copy must not overwrite the real save

        Session session;
        bool autoContinued;
        // dev: echo ghost (replays your own moves 3 s later) to test ghost rendering solo
        Ghost echo;
        readonly Queue<PoseFrame> echoQueue = new Queue<PoseFrame>();
        float nextEcho;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{Name} {Version}. Game version {Application.version}, Unity {Application.unityVersion}");

            // version check: turn off cleanly instead of breaking the game after an update
            var missing = new[] { "PlayerControl", "PoseControl", "Saviour", "ProgressMeter", "DebugSpawn", "SteamManager" }
                .Where(n => Type.GetType(n + ", Assembly-CSharp") == null).ToList();
            if (!TestedGameVersions.Contains(Application.version) || missing.Count > 0)
            {
                Disabled = true;
                DisabledReason = missing.Count > 0 ? "the game changed (missing " + string.Join(", ", missing) + ")"
                                                   : $"game version {Application.version} is newer than this mod (tested on {string.Join(", ", TestedGameVersions)})";
                Log.LogWarning("Disabled: " + DisabledReason);
                return;
            }

            // dev test copies never save, never count wins, never unlock achievements
            NoSave = Environment.GetEnvironmentVariable("OIT_NOSAVE") == "1" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OIT_DEV_PORT"));
            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(GuardPatches));
            harmony.PatchAll(typeof(MenuPatches));
            Settings.Bind(Config);
            session = new Session();
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(EndOfFrameLoop());
            Invoke(nameof(AutoConnect), 4f);
        }

        void AutoConnect()
        {
            // dev loopback: OIT_DEV_PORT=27701 OIT_DEV_PEERS=27702
            var port = Environment.GetEnvironmentVariable("OIT_DEV_PORT");
            if (!string.IsNullOrEmpty(port))
            {
                var peers = (Environment.GetEnvironmentVariable("OIT_DEV_PEERS") ?? "").Split(',').Where(x => x.Length > 0).Select(int.Parse).ToArray();
                session.StartLoopback(int.Parse(port), peers);
                return;
            }
            if (!SteamManager.Initialized) { Log.LogWarning("Steam not initialised - online play unavailable"); return; }
            session.HookSteam();
            // launched by accepting a Steam invite: "+connect_lobby <id>"
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "+connect_lobby");
            if (i >= 0 && i + 1 < args.Length && ulong.TryParse(args[i + 1], out var lobby))
                session.JoinSteamLobby(new CSteamID(lobby));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"Scene loaded: {scene.name} (index {scene.buildIndex})");
            if (scene.buildIndex == 0 && Environment.GetEnvironmentVariable("OIT_AUTOCONTINUE") == "1" && !autoContinued)
            { autoContinued = true; SceneManager.LoadScene(1, LoadSceneMode.Single); return; }
            InLevel = false;
            session.OnLevelUnloaded();
            echo?.Destroy(); echo = null; echoQueue.Clear();
            if (UI.PanelOpen) UI.TogglePanel();
            if (FindObjectOfType<PlayerControl>() != null) Invoke(nameof(BindLevel), 1f);
        }

        private void BindLevel()
        {
            PoseCapture.Bind();
            InLevel = PoseCapture.Ready;
            if (InLevel) Splits.Load();
            Log.LogInfo("Level bound: " + InLevel);
        }

        private void Update()
        {
            if (Disabled) return;
            session.Update();
            DevCommands();

            if (Input.GetKeyDown(KeyCode.F1) || (InLevel && Input.GetKeyDown(KeyCode.Escape))) UI.TogglePanel();
            if (!InLevel) return;
            Guard.EnforceCeiling();

            if (IsDev && Input.GetKeyDown(KeyCode.F9))
            {
                if (echo == null) { echo = new Ghost("Echo", new Color(0.3f, 0.7f, 1f)); Log.LogInfo("Echo ghost on"); }
                else { echo.Destroy(); echo = null; echoQueue.Clear(); Log.LogInfo("Echo ghost off"); }
            }
            if (echo != null)
                while (echoQueue.Count > 0 && echoQueue.Peek().Time <= Time.unscaledTime - 3f)
                {
                    var f = echoQueue.Dequeue(); f.Time += 3f; echo.Push(f);
                }
        }

        // DEV: drop commands into BepInEx/config/oit_cmd_<port>.txt (race, cancel, tp SpawnNN, top, save, load, start)
        float nextCmdPoll;
        void DevCommands()
        {
            var port = Environment.GetEnvironmentVariable("OIT_DEV_PORT");
            if (string.IsNullOrEmpty(port) || Time.unscaledTime < nextCmdPoll) return;
            nextCmdPoll = Time.unscaledTime + 0.5f;
            var file = System.IO.Path.Combine(Paths.ConfigPath, "oit_cmd_" + port + ".txt");
            if (!System.IO.File.Exists(file)) return;
            var lines = System.IO.File.ReadAllLines(file); System.IO.File.Delete(file);
            foreach (var raw in lines)
            {
                var a = raw.Trim().Split(' ');
                Log.LogInfo("devcmd: " + raw);
                switch (a[0])
                {
                    case "race": session.HostStartRace(a.Length > 1 ? float.Parse(a[1]) : 3f); break;
                    case "cancel": session.HostCancelRace(); break;
                    case "finish": session.FinishIndex = int.Parse(a[1]); break;
                    case "tp": Practice.TeleportToSpawn(a[1]); break;
                    case "top": var e = Splits.SpawnPoints(); Practice.TeleportTo(new Vector3(62.05f, 359.61f, 0)); break;
                    case "save": Practice.SavePosition(); break;
                    case "load": Practice.LoadPosition(); break;
                    case "start": Practice.ResetToStart(); break;
                    case "tpxyz": Practice.TeleportTo(new Vector3(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture), 0)); break;
                    case "where": Log.LogInfo("player at " + PoseCapture.Player.transform.position.ToString("F2")); break;
                    case "panel": UI.TogglePanel(); break;
                    case "tab": UI.tab = int.Parse(a[1]); break;
                }
            }
        }

        private System.Collections.IEnumerator EndOfFrameLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (InLevel && echo != null && Time.unscaledTime >= nextEcho)
                {
                    nextEcho = Time.unscaledTime + 1f / 15f;
                    echoQueue.Enqueue(PoseCapture.Capture(Time.unscaledTime));
                }
            }
        }

        private void LateUpdate()
        {
            if (Disabled) return;
            session.LateUpdate();
            echo?.Update();
        }

        private void OnGUI()
        {
            if (Disabled && SceneManager.GetActiveScene().buildIndex != 0) return;
            UI.Draw();
        }

        private void OnApplicationQuit()
        {
            session?.Leave();
        }

        internal static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }

}
