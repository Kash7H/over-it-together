using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace OverItTogether
{
    public class Peer
    {
        public PeerId Id;
        public string Name = "Player";
        public Color Color;
        public Ghost Ghost;
        public PoseFrame Last;
        public float LastSeen;
        public int Section = -1;          // index of last split passed
        public float[] SplitTimes;
        public float FinishTime = -1;
        public bool Greeted;
        public float Progress => Last != null ? Last.Progress : 0f;
    }

    public enum RaceState { Idle, Countdown, Racing, Finished }

    /// <summary>Lobby, peers, ghosts, race and splits. One instance, lives for the whole game session.</summary>
    public class Session
    {
        public static Session I;
        public static readonly Color[] Palette =
        {
            new Color(0.30f, 0.70f, 1.00f), new Color(1.00f, 0.45f, 0.35f), new Color(0.45f, 0.90f, 0.40f),
            new Color(1.00f, 0.85f, 0.25f), new Color(0.80f, 0.50f, 1.00f), new Color(0.30f, 0.95f, 0.90f),
            new Color(1.00f, 0.60f, 0.85f), new Color(0.85f, 0.85f, 0.85f),
        };

        public ITransport Net;
        public readonly Dictionary<PeerId, Peer> Peers = new Dictionary<PeerId, Peer>();
        public string MyName = "Player";
        public bool IsHost;
        public PeerId HostId;
        public bool Connected => Net != null;
        public string Status = "";

        // Steam lobby
        public CSteamID Lobby = CSteamID.Nil;
        Callback<LobbyCreated_t> cbCreated;
        Callback<LobbyEnter_t> cbEnter;
        Callback<GameLobbyJoinRequested_t> cbJoinReq;
        Callback<LobbyChatUpdate_t> cbChatUpdate;

        // race
        public RaceState Race = RaceState.Idle;
        public float RaceStartLocal;     // Time.unscaledTime when the clock hit 0
        public float CountdownEnd;
        public int MySection = -1;
        public float[] MySplits;
        public int FinishIndex = -1;    // last split of this race (host picks; -1 = last split in the list)
        public int RaceFinish => FinishIndex >= 0 && FinishIndex < Splits.Count ? FinishIndex : Splits.Count - 1;
        public float MyFinish = -1;
        public List<string> Results = new List<string>();
        public float RaceTime => Race == RaceState.Racing || Race == RaceState.Finished ? Time.unscaledTime - RaceStartLocal : 0f;
        public readonly List<string> Feed = new List<string>();   // recent events for the HUD

        float nextPose, nextSync;
        const float PoseRate = 1f / 15f;

        public Session()
        {
            I = this;
            try { MyName = SteamFriends.GetPersonaName(); } catch { }
            MySplits = new float[Splits.Count];
        }

        // ---------- connect ----------
        public void HostSteamLobby()
        {
            Leave();
            Status = "Creating lobby...";
            HookSteam();
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 8);
        }

        public void InviteFriends()
        {
            if (Lobby != CSteamID.Nil) SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
        }

        public void JoinSteamLobby(CSteamID id)
        {
            Leave();
            HookSteam();
            Status = "Joining...";
            SteamMatchmaking.JoinLobby(id);
        }

        public void HookSteam()
        {
            if (cbCreated != null) return;
            cbCreated = Callback<LobbyCreated_t>.Create(r =>
            {
                if (r.m_eResult != EResult.k_EResultOK) { Status = "Could not create lobby (" + r.m_eResult + ")"; return; }
                var id = new CSteamID(r.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(id, "oit", Plugin.Version);
                SteamMatchmaking.SetLobbyData(id, "name", MyName + "'s race");
            });
            cbEnter = Callback<LobbyEnter_t>.Create(r =>
            {
                Lobby = new CSteamID(r.m_ulSteamIDLobby);
                var ver = SteamMatchmaking.GetLobbyData(Lobby, "oit");
                if (!string.IsNullOrEmpty(ver) && ver != Plugin.Version)
                    Notice($"Lobby uses Over It Together {ver}, you have {Plugin.Version}. Things may break.");
                Net?.Close();
                Net = new SteamTransport(IsLobbyMember);
                var owner = SteamMatchmaking.GetLobbyOwner(Lobby);
                HostId = new PeerId(owner.m_SteamID);
                IsHost = owner == SteamUser.GetSteamID();
                Status = IsHost ? "Hosting. Invite friends!" : "Joined " + SteamFriends.GetFriendPersonaName(owner) + "'s lobby";
                RefreshLobbyMembers();
                Broadcast(Packet.Make(Msg.Hello, w => { w.Write(MyName); w.Write(Plugin.Version); }), true);
            });
            cbJoinReq = Callback<GameLobbyJoinRequested_t>.Create(r => JoinSteamLobby(r.m_steamIDLobby));
            cbChatUpdate = Callback<LobbyChatUpdate_t>.Create(r =>
            {
                RefreshLobbyMembers();
                var who = new CSteamID(r.m_ulSteamIDUserChanged);
                var change = (EChatMemberStateChange)r.m_rgfChatMemberStateChange;
                if ((change & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0) Notice(SteamFriends.GetFriendPersonaName(who) + " joined");
                else Notice(SteamFriends.GetFriendPersonaName(who) + " left");
                var owner = SteamMatchmaking.GetLobbyOwner(Lobby);
                HostId = new PeerId(owner.m_SteamID);
                IsHost = owner == SteamUser.GetSteamID();
            });
        }

        bool IsLobbyMember(CSteamID id)
        {
            if (Lobby == CSteamID.Nil) return false;
            int n = SteamMatchmaking.GetNumLobbyMembers(Lobby);
            for (int i = 0; i < n; i++) if (SteamMatchmaking.GetLobbyMemberByIndex(Lobby, i) == id) return true;
            return false;
        }

        void RefreshLobbyMembers()
        {
            if (Lobby == CSteamID.Nil) return;
            var me = SteamUser.GetSteamID();
            var present = new HashSet<PeerId>();
            int n = SteamMatchmaking.GetNumLobbyMembers(Lobby);
            for (int i = 0; i < n; i++)
            {
                var m = SteamMatchmaking.GetLobbyMemberByIndex(Lobby, i);
                if (m == me) continue;
                var id = new PeerId(m.m_SteamID);
                present.Add(id);
                var p = GetPeer(id);
                p.Name = SteamFriends.GetFriendPersonaName(m);
            }
            foreach (var gone in Peers.Keys.Where(k => !present.Contains(k)).ToList()) RemovePeer(gone);
        }

        /// <summary>DEV: two game copies on one PC. OIT_DEV_PORT=this port, OIT_DEV_PEERS=other ports.</summary>
        public void StartLoopback(int port, int[] peers)
        {
            Leave();
            Net = new LoopbackTransport(port);
            MyName = "Dev" + port;
            var all = peers.Concat(new[] { port }).ToArray();
            HostId = new PeerId((ulong)all.Min());
            IsHost = port == all.Min();
            foreach (var p in peers) GetPeer(new PeerId((ulong)p)).Name = "Dev" + p;
            Status = "Loopback " + (IsHost ? "host" : "client") + " on " + port;
            Broadcast(Packet.Make(Msg.Hello, w => { w.Write(MyName); w.Write(Plugin.Version); }), true);
        }

        public void Leave()
        {
            if (Net != null) Broadcast(Packet.Make(Msg.Bye), true);
            if (Lobby != CSteamID.Nil) { SteamMatchmaking.LeaveLobby(Lobby); Lobby = CSteamID.Nil; }
            Net?.Close(); Net = null;
            foreach (var k in Peers.Keys.ToList()) RemovePeer(k);
            IsHost = false; Status = ""; Race = RaceState.Idle;
        }

        Peer GetPeer(PeerId id)
        {
            if (!Peers.TryGetValue(id, out var p))
            {
                p = new Peer { Id = id, Color = Palette[(Peers.Count + 1) % Palette.Length], LastSeen = Time.unscaledTime, SplitTimes = new float[Splits.Count] };
                for (int i = 0; i < p.SplitTimes.Length; i++) p.SplitTimes[i] = -1;
                Peers[id] = p;
            }
            return p;
        }

        void RemovePeer(PeerId id)
        {
            if (Peers.TryGetValue(id, out var p)) p.Ghost?.Destroy();
            Peers.Remove(id);
        }

        public void Broadcast(byte[] data, bool reliable)
        {
            if (Net == null) return;
            foreach (var p in Peers.Keys) Net.Send(p, data, reliable);
        }

        public void Notice(string text)
        {
            Feed.Add(text);
            if (Feed.Count > 6) Feed.RemoveAt(0);
            Plugin.Log.LogInfo("[feed] " + text);
        }

        // ---------- per frame ----------
        public void Update()
        {
            if (Net == null) return;
            Application.runInBackground = true;   // keep ghosts and the race clock live while alt-tabbed
            while (Net.TryReceive(out var from, out var data)) Handle(from, data);

            if (Plugin.InLevel && PoseCapture.Ready && Time.unscaledTime >= nextPose)
            {
                nextPose = Time.unscaledTime + PoseRate;
                var f = PoseCapture.Capture(Race == RaceState.Racing ? RaceTime : Time.unscaledTime);
                f.Time = Time.unscaledTime;
                Broadcast(Packet.Make(Msg.Pose, w => f.Write(w)), false);
                TrackMyProgress(PoseCapture.Player.transform.position);
            }

            if (IsHost && Race == RaceState.Racing && Time.unscaledTime >= nextSync)
            {
                nextSync = Time.unscaledTime + 2f;
                var t = RaceTime;
                Broadcast(Packet.Make(Msg.ClockSync, w => w.Write(t)), false);
            }

            if (Race == RaceState.Countdown && Time.unscaledTime >= CountdownEnd) Go();

            // drop silent peers in loopback mode (Steam mode uses lobby membership)
            if (Lobby == CSteamID.Nil)
                foreach (var k in Peers.Where(kv => Time.unscaledTime - kv.Value.LastSeen > 15f && kv.Value.Last != null).Select(kv => kv.Key).ToList())
                { Notice(Peers[k].Name + " timed out"); RemovePeer(k); }
        }

        public void LateUpdate()
        {
            foreach (var p in Peers.Values)
            {
                if (p.Ghost == null && p.Last != null && Plugin.InLevel && PoseCapture.Ready)
                    p.Ghost = new Ghost(p.Name, p.Color);
                p.Ghost?.Update();
                p.Ghost?.SetVisible(Settings.ShowGhosts.Value);
                p.Ghost?.ShowLabel(Settings.ShowNames.Value);
            }
        }

        public void OnLevelUnloaded()
        {
            foreach (var p in Peers.Values) { p.Ghost?.Destroy(); p.Ghost = null; }
        }

        void Handle(PeerId from, byte[] data)
        {
            if (data.Length == 0) return;
            var p = GetPeer(from);
            p.LastSeen = Time.unscaledTime;
            using (var r = new BinaryReader(new MemoryStream(data, 1, data.Length - 1)))
            {
                switch ((Msg)data[0])
                {
                    case Msg.Hello:
                        p.Name = r.ReadString();
                        var ver = r.ReadString();
                        if (p.Ghost != null) { p.Ghost.Destroy(); p.Ghost = null; }
                        Notice(p.Name + " is here" + (ver != Plugin.Version ? $" (version {ver}!)" : ""));
                        // answer once so a newcomer learns our name too
                        if (!p.Greeted) { p.Greeted = true; Net.Send(from, Packet.Make(Msg.Hello, w => { w.Write(MyName); w.Write(Plugin.Version); }), true); }
                        break;
                    case Msg.Pose:
                        p.Last = PoseFrame.Read(r);
                        p.Ghost?.Push(p.Last);
                        break;
                    case Msg.RaceCountdown:
                        if (from.Equals(HostId)) { float cd = r.ReadSingle(); FinishIndex = r.ReadInt16(); BeginCountdown(cd); }
                        break;
                    case Msg.RaceAbort:
                        if (from.Equals(HostId)) { Race = RaceState.Idle; Notice("Race cancelled"); }
                        break;
                    case Msg.ClockSync:
                        if (from.Equals(HostId) && Race == RaceState.Racing)
                        {
                            float hostT = r.ReadSingle();
                            float drift = hostT - RaceTime;
                            if (Mathf.Abs(drift) > 0.05f) RaceStartLocal -= drift * 0.5f;   // ease toward host clock
                        }
                        break;
                    case Msg.Split:
                    {
                        int idx = r.ReadByte(); float t = r.ReadSingle();
                        if (idx < p.SplitTimes.Length) p.SplitTimes[idx] = t;
                        p.Section = Math.Max(p.Section, idx);
                        Notice($"{p.Name}  {Splits.Names[idx]}  {Fmt(t)}");
                        break;
                    }
                    case Msg.Finish:
                    {
                        float t = r.ReadSingle();
                        p.FinishTime = t;
                        OnSomeoneFinished(p.Name, t);
                        break;
                    }
                    case Msg.Chat:
                        Notice(p.Name + ": " + r.ReadString());
                        break;
                    case Msg.Bye:
                        Notice(p.Name + " left");
                        RemovePeer(from);
                        break;
                }
            }
        }

        // ---------- race ----------
        public void HostStartRace(float countdown = 5f)
        {
            if (!IsHost && Connected) return;
            int fin = FinishIndex;
            Broadcast(Packet.Make(Msg.RaceCountdown, w => { w.Write(countdown); w.Write((short)fin); }), true);
            BeginCountdown(countdown);
        }

        public void HostCancelRace()
        {
            if (!IsHost && Connected) return;
            Broadcast(Packet.Make(Msg.RaceAbort), true);
            Race = RaceState.Idle;
        }

        void BeginCountdown(float seconds)
        {
            Race = RaceState.Countdown;
            CountdownEnd = Time.unscaledTime + seconds;
            MySection = -1; MyFinish = -1; Results.Clear();
            // splits are loaded with the level, after the session was created: size arrays now
            MySplits = Enumerable.Repeat(-1f, Splits.Count).ToArray();
            foreach (var p in Peers.Values) { p.Section = -1; p.FinishTime = -1; p.SplitTimes = Enumerable.Repeat(-1f, Splits.Count).ToArray(); }
            Practice.ResetToStart();
            Practice.FreezeInput(seconds);
            Notice("Race starts in " + Mathf.CeilToInt(seconds));
        }

        void Go()
        {
            Race = RaceState.Racing;
            RaceStartLocal = Time.unscaledTime;
            Notice("GO!");
        }

        void TrackMyProgress(Vector2 pos)
        {
            if (Race != RaceState.Racing) return;
            // like the LiveSplit autosplitter: each split fires the first time you enter its box (skips allowed)
            for (int i = 0; i <= RaceFinish && i < Splits.Count; i++)
            {
                if (MySplits[i] >= 0 || !Splits.Inside(i, pos)) continue;
                float t = RaceTime;
                MySplits[i] = t;
                MySection = Mathf.Max(MySection, i);
                int idx = i;
                Broadcast(Packet.Make(Msg.Split, w => { w.Write((byte)idx); w.Write(t); }), true);
                Notice($"{MyName}  {Splits.Names[i]}  {Fmt(t)}");
                if (i == RaceFinish) { FinishMe(); break; }
            }
        }
        void FinishMe()
        {
            if (MyFinish >= 0) return;
            MyFinish = RaceTime;
            Race = RaceState.Finished;
            float t = MyFinish;
            Broadcast(Packet.Make(Msg.Finish, w => w.Write(t)), true);
            OnSomeoneFinished(MyName, t);
        }

        void OnSomeoneFinished(string name, float t)
        {
            Results.Add($"{Results.Count + 1}. {name}  {Fmt(t)}");
            Notice(Results.Count == 1 ? $"{name} WINS in {Fmt(t)}!" : $"{name} finished {Ordinal(Results.Count)} ({Fmt(t)})");
        }

        static string Ordinal(int n) => n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        public static string Fmt(float t)
        {
            if (t < 0) return "--:--";
            int h = (int)(t / 3600), m = (int)(t / 60) % 60; float s = t % 60f;
            return h > 0 ? $"{h}:{m:00}:{s:00.0}" : $"{m}:{s:00.0}";
        }

        /// <summary>Everyone's standings, best first: (name, color, progress, section, finish).</summary>
        public List<(string name, Color color, float progress, int section, float finish, bool me)> Standings()
        {
            var list = new List<(string, Color, float, int, float, bool)>();
            float myProg = PoseCapture.Meter != null ? PoseCapture.Meter.progress : 0f;
            list.Add((MyName, Color.white, myProg, MySection, MyFinish, true));
            foreach (var p in Peers.Values) list.Add((p.Name, p.Color, p.Progress, p.Section, p.FinishTime, false));
            return list.OrderByDescending(x => x.Item5 >= 0 ? 1e9f - x.Item5 : x.Item3).ToList();
        }
    }
}
