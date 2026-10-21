using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Steamworks;
using UnityEngine;

namespace OverItTogether
{
    public enum Msg : byte
    {
        Hello = 1,      // name, version
        Pose = 2,       // PoseFrame
        RaceCountdown = 3, // host -> all: seconds until go
        RaceGo = 4,     // host -> all: race clock starts now
        Split = 5,      // player -> all: section index, time
        Finish = 6,     // player -> all: time
        Results = 7,    // host -> all: finish order text
        Chat = 8,       // short text (emotes, notices)
        ClockSync = 9,  // host -> all: host race clock
        Bye = 10,
        RaceAbort = 11,
    }

    /// <summary>A peer id that works for both transports (Steam id or loopback port).</summary>
    public struct PeerId : IEquatable<PeerId>
    {
        public ulong Value;
        public PeerId(ulong v) { Value = v; }
        public bool Equals(PeerId o) => Value == o.Value;
        public override bool Equals(object o) => o is PeerId p && Equals(p);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    public interface ITransport
    {
        PeerId Self { get; }
        void Send(PeerId to, byte[] data, bool reliable);
        bool TryReceive(out PeerId from, out byte[] data);
        void Close();
    }

    /// <summary>Steam P2P (relayed by Steam, NAT-friendly). Peers are accepted only if they are in our lobby.</summary>
    public class SteamTransport : ITransport
    {
        readonly Callback<P2PSessionRequest_t> onRequest;
        readonly Func<CSteamID, bool> isLobbyMember;
        byte[] buf = new byte[64 * 1024];
        public PeerId Self => new PeerId(SteamUser.GetSteamID().m_SteamID);

        public SteamTransport(Func<CSteamID, bool> isLobbyMember)
        {
            this.isLobbyMember = isLobbyMember;
            SteamNetworking.AllowP2PPacketRelay(true);
            onRequest = Callback<P2PSessionRequest_t>.Create(r =>
            {
                if (this.isLobbyMember(r.m_steamIDRemote)) SteamNetworking.AcceptP2PSessionWithUser(r.m_steamIDRemote);
            });
        }

        public void Send(PeerId to, byte[] data, bool reliable)
        {
            SteamNetworking.SendP2PPacket(new CSteamID(to.Value), data, (uint)data.Length,
                reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay, 0);
        }

        public bool TryReceive(out PeerId from, out byte[] data)
        {
            from = default; data = null;
            if (!SteamNetworking.IsP2PPacketAvailable(out uint size, 0)) return false;
            if (size > buf.Length) buf = new byte[size];
            if (!SteamNetworking.ReadP2PPacket(buf, size, out uint read, out CSteamID sender, 0)) return false;
            data = new byte[read];
            Buffer.BlockCopy(buf, 0, data, 0, (int)read);
            from = new PeerId(sender.m_SteamID);
            return true;
        }

        public void Close() { onRequest?.Dispose(); }
    }

    /// <summary>DEV ONLY: UDP on 127.0.0.1 so two copies of the game on one PC can test multiplayer.</summary>
    public class LoopbackTransport : ITransport
    {
        readonly UdpClient udp;
        readonly int port;
        public PeerId Self => new PeerId((ulong)port);

        public LoopbackTransport(int port)
        {
            this.port = port;
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            udp.Client.Blocking = false;
        }

        public void Send(PeerId to, byte[] data, bool reliable)
        {
            try { udp.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, (int)to.Value)); } catch { }
        }

        public bool TryReceive(out PeerId from, out byte[] data)
        {
            from = default; data = null;
            try
            {
                if (udp.Available <= 0) return false;
                var ep = new IPEndPoint(IPAddress.Any, 0);
                data = udp.Receive(ref ep);
                from = new PeerId((ulong)ep.Port);
                return true;
            }
            catch { return false; }
        }

        public void Close() { try { udp.Close(); } catch { } }
    }

    public static class Packet
    {
        public static byte[] Make(Msg type, Action<BinaryWriter> body = null)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)type);
                body?.Invoke(w);
                return ms.ToArray();
            }
        }
    }
}
