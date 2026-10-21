using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OverItTogether
{
    /// <summary>All on-screen UI (Unity IMGUI): main menu button, pause menu with tabs, race HUD.</summary>
    public static class UI
    {
        public static bool PanelOpen;
        public static int tab;
        static readonly string[] Tabs = { "Multiplayer", "Race", "Practice", "Settings", "Controls" };
        static GUIStyle box, title, label, small, big, btn, tabOn, tabOff;
        static Texture2D dark, accent;
        static Vector2 scroll;

        static void Styles()
        {
            if (box != null) return;
            dark = Tex(new Color(0.06f, 0.06f, 0.07f, 0.88f));
            accent = Tex(new Color(0.25f, 0.55f, 0.95f, 1f));
            box = new GUIStyle(GUI.skin.box) { normal = { background = dark }, padding = new RectOffset(16, 16, 12, 12) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white }, richText = true, wordWrap = true };
            small = new GUIStyle(label) { fontSize = 13, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            big = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 34 };
            tabOff = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 32 };
            tabOn = new GUIStyle(tabOff) { normal = { background = accent, textColor = Color.white }, hover = { background = accent, textColor = Color.white } };
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        public static void TogglePanel()
        {
            PanelOpen = !PanelOpen;
            if (Plugin.InLevel)
            {
                if (PanelOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; PoseCapture.Player?.PauseInput(-1f); }
                else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; PoseCapture.Player?.PauseInput(0f); }
            }
        }

        public static void Draw()
        {
            Styles();
            var s = Session.I;
            if (Plugin.Disabled)
            {
                GUI.Label(new Rect(20, Screen.height - 40, 1000, 30), "Over It Together is turned off: " + Plugin.DisabledReason, small);
                return;
            }
            bool inMenu = SceneManager.GetActiveScene().buildIndex == 0;

            if (inMenu && !PanelOpen)
            {
                var r = new Rect(Screen.width * 0.6f, Screen.height * 0.8f, 240, 46);
                if (GUI.Button(r, "Multiplayer", new GUIStyle(btn) { fontSize = 22, fixedHeight = 46 })) { tab = 0; TogglePanel(); }
                if (s.Connected) GUI.Label(new Rect(r.x, r.yMax + 6, 700, 30), s.Status + "  -  press Continue to play", label);
            }

            if (Plugin.InLevel && Settings.ShowHud.Value) DrawHud(s);
            if (PanelOpen) DrawPanel(s);
        }

        // ---------------- HUD ----------------
        static void DrawHud(Session s)
        {
            var feed = Settings.ShowFeed.Value ? s.Feed : new System.Collections.Generic.List<string>();
            bool active = s.Connected || s.Race != RaceState.Idle || feed.Count > 0;
            if (!active)
            {
                GUI.Label(new Rect(14, Screen.height - 30, 500, 24), "Esc: Over It Together menu", small);
                return;
            }
            var st = s.Standings();
            float h = 64 + st.Count * 24 + feed.Count * 20;
            GUILayout.BeginArea(new Rect(14, 14, 380, h), box);
            if (s.Race == RaceState.Racing || s.Race == RaceState.Finished)
                GUILayout.Label(Session.Fmt(s.MyFinish >= 0 ? s.MyFinish : s.RaceTime), title);
            else GUILayout.Label(s.Connected ? $"{st.Count} climbing" : "Solo", title);

            foreach (var p in st)
            {
                float pct = Splits.RouteLength > 0 ? Mathf.Clamp01(p.progress / Splits.RouteLength) * 100f : 0f;
                string sec = p.section >= 0 && p.section < Splits.Count ? Splits.Names[p.section] : "";
                string right = p.finish >= 0 ? "finished " + Session.Fmt(p.finish) : $"{pct:0}%  {sec}";
                var col = ColorUtility.ToHtmlStringRGB(p.color);
                GUILayout.Label($"<color=#{col}>{(p.me ? "> " : "")}{p.name}</color>  <color=#bbbbbb>{right}</color>", label);
            }
            foreach (var line in feed) GUILayout.Label(line, small);
            GUILayout.EndArea();

            if (s.Race == RaceState.Countdown)
            {
                int n = Mathf.CeilToInt(s.CountdownEnd - Time.unscaledTime);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 140), n > 0 ? n.ToString() : "GO", big);
            }
            else if (s.Race == RaceState.Racing && s.RaceTime < 1.2f)
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 140), "GO", big);

            if (s.Results.Count > 0)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 340, 14, 326, 46 + s.Results.Count * 24), box);
                GUILayout.Label("Results", title);
                foreach (var r in s.Results) GUILayout.Label(r, label);
                GUILayout.EndArea();
            }
        }

        // ---------------- pause menu ----------------
        static void DrawPanel(Session s)
        {
            float w = Mathf.Min(640, Screen.width - 40), hgt = Mathf.Min(Screen.height - 80, 640);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2, (Screen.height - hgt) / 2, w, hgt), box);
            GUILayout.Label("Over It Together", title);
            if (!string.IsNullOrEmpty(s.Status)) GUILayout.Label(s.Status, small);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (GUILayout.Button(Tabs[i], i == tab ? tabOn : tabOff)) tab = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: TabMultiplayer(s); break;
                case 1: TabRace(s); break;
                case 2: TabPractice(s); break;
                case 3: TabSettings(); break;
                case 4: TabControls(); break;
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Plugin.InLevel ? "Resume (Esc)" : "Close", btn)) TogglePanel();
            if (Plugin.InLevel && GUILayout.Button("Main menu", btn)) { TogglePanel(); SceneManager.LoadScene(0, LoadSceneMode.Single); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static void TabMultiplayer(Session s)
        {
            if (!s.Connected)
            {
                GUILayout.Label("Play with friends: everyone climbs at the same time and sees each other. No collisions.", label);
                GUILayout.Space(6);
                if (GUILayout.Button("Host a lobby", btn)) s.HostSteamLobby();
                GUILayout.Space(6);
                GUILayout.Label("To join a friend: accept their Steam invite, or right-click them in your Steam friends list and choose Join Game. " +
                                "Start the game with OverItTogether.exe first.", small);
                return;
            }
            if (s.Lobby.IsValid() && GUILayout.Button("Invite friends", btn)) s.InviteFriends();
            GUILayout.Space(6);
            GUILayout.Label("Players", label);
            GUILayout.Label("  " + s.MyName + (s.IsHost ? "  (host)" : "") + "  (you)", small);
            foreach (var p in s.Peers.Values)
                GUILayout.Label($"  <color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.Name}</color>" + (p.Id.Equals(s.HostId) ? "  (host)" : ""), small);
            GUILayout.Space(10);
            if (GUILayout.Button("Leave lobby", btn)) s.Leave();
        }

        static void TabRace(Session s)
        {
            if (!Plugin.InLevel) { GUILayout.Label("Press Continue on the main menu to enter the level first.", label); return; }
            if (!s.Connected) GUILayout.Label("Host or join a lobby in the Multiplayer tab to race friends. Solo, you can race the clock.", small);
            bool canControl = s.IsHost || !s.Connected;
            if (canControl && Splits.Count > 0 && (s.Race == RaceState.Idle || s.Race == RaceState.Finished))
            {
                int fi = s.RaceFinish;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Finish line", label, GUILayout.Width(110));
                if (GUILayout.Button("<", btn, GUILayout.Width(36))) s.FinishIndex = Mathf.Max(0, fi - 1);
                GUILayout.Label(Splits.Names[fi], label, GUILayout.Width(170));
                if (GUILayout.Button(">", btn, GUILayout.Width(36))) s.FinishIndex = Mathf.Min(Splits.Count - 1, fi + 1);
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
                if (GUILayout.Button(s.Results.Count > 0 ? "Rematch" : "Start race", btn)) { s.HostStartRace(); TogglePanel(); }
                GUILayout.Label("Everyone is moved to the start, there's a countdown, then GO. Your own climb is saved first.", small);
            }
            else if (canControl && (s.Race == RaceState.Countdown || s.Race == RaceState.Racing))
            {
                if (GUILayout.Button("Cancel race", btn)) s.HostCancelRace();
            }
            else GUILayout.Label(s.Race == RaceState.Idle ? "Waiting for the host to start a race." : "Race in progress.", label);

            if (s.Results.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Last race", label);
                foreach (var r in s.Results) GUILayout.Label("  " + r, small);
            }
            GUILayout.Space(10);
            GUILayout.Label("Splits: " + string.Join("  >  ", Splits.Names), small);
            ClimbButtons(s);
        }

        static void TabPractice(Session s)
        {
            if (!Plugin.InLevel) { GUILayout.Label("Enter the level first.", label); return; }
            bool racing = s.Race == RaceState.Racing || s.Race == RaceState.Countdown;
            if (racing) { GUILayout.Label("Practice tools are off during a race.", label); return; }
            GUILayout.Label("Your own climb is saved before you use any of these, and you can return to it any time.", small);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save position", btn)) Practice.SavePosition();
            if (GUILayout.Button("Load position", btn)) { Practice.LoadPosition(); TogglePanel(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Teleport to a section", label);
            var points = Practice.SectionPoints();
            for (int i = 0; i < points.Count; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, points.Count); j++)
                    if (GUILayout.Button(points[j].name, btn)) { Practice.TeleportTo(points[j].pos); TogglePanel(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            if (GUILayout.Button("Back to the very start", btn)) { Practice.ResetToStart(); TogglePanel(); }
            ClimbButtons(s);
        }

        static void ClimbButtons(Session s)
        {
            if (!Guard.Holding || s.Race == RaceState.Racing || s.Race == RaceState.Countdown) return;
            GUILayout.Space(12);
            GUILayout.Label("Your own climb is paused", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Return to my own climb", btn)) { Guard.Release(); TogglePanel(); }
            if (GUILayout.Button("Keep this climb", btn)) { Guard.KeepCurrent(); TogglePanel(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Return puts you back exactly where you were. Keep makes where you are now your real progress.", small);
        }

        static void TabSettings()
        {
            Settings.ShowGhosts.Value = GUILayout.Toggle(Settings.ShowGhosts.Value, "  Show other players", label);
            Settings.ShowNames.Value = GUILayout.Toggle(Settings.ShowNames.Value, "  Show name tags", label);
            Settings.ShowHud.Value = GUILayout.Toggle(Settings.ShowHud.Value, "  Show the race HUD (top left)", label);
            Settings.ShowFeed.Value = GUILayout.Toggle(Settings.ShowFeed.Value, "  Show split and join messages", label);
            GUILayout.Space(8);
            GUILayout.Label("Settings are saved automatically.", small);
        }

        static void TabControls()
        {
            GUILayout.Label("<b>Keys</b>", label);
            GUILayout.Label("Esc  -  open or close this menu (in a level)\nF1  -  same as Esc\nMain menu  -  Multiplayer button", small);
            GUILayout.Space(8);
            GUILayout.Label("<b>Playing with friends</b>", label);
            GUILayout.Label("1. Everyone starts the game with OverItTogether.exe.\n2. One player opens Multiplayer and clicks Host a lobby, then Invite friends.\n" +
                            "3. Friends accept the Steam invite.\n4. The host starts a race from the Race tab.", small);
            GUILayout.Space(8);
            GUILayout.Label("<b>Good to know</b>", label);
            GUILayout.Label("- Other players can't bump into you.\n" +
                            "- Splits follow the speedrun community's sections: " + string.Join(", ", Splits.Names) + ".\n" +
                            "- Races and practice never overwrite your own save unless you choose Keep this climb.\n" +
                            "- Finishing a race never plays the real ending. That's yours to reach yourself.", small);
        }
    }
}
