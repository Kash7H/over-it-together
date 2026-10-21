using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OverItTogether
{
    /// <summary>One snapshot of a player's visible pose. Everything a ghost needs to look identical.</summary>
    public class PoseFrame
    {
        public float Time;               // sender's race clock / local time, seconds
        public Vector3 PotPos;
        public Quaternion PotRot;
        public Vector3 HammerPos;
        public Quaternion HammerRot;
        public Vector3 DudePos;
        public Quaternion DudeRot;
        public Quaternion[] Bones;       // local rotations of the dude rig, fixed order
        public Vector3[] BonePos;        // local positions (the arm IK moves hands/elbows by position)
        public Vector3 HipsLocalPos;
        public float Progress;           // ProgressMeter distance along the route
        public Vector3[] PoleBonePos;    // world pose of the bones that drive the hammer pole mesh
        public Quaternion[] PoleBoneRot;

        public void Write(BinaryWriter w)
        {
            w.Write(Time);
            WriteV(w, PotPos); WriteQ(w, PotRot);
            WriteV(w, HammerPos); WriteQ(w, HammerRot);
            WriteV(w, DudePos); WriteQ(w, DudeRot);
            WriteV(w, HipsLocalPos);
            w.Write(Progress);
            w.Write((byte)Bones.Length);
            foreach (var q in Bones) WriteQSmall(w, q);
            foreach (var p in BonePos) WriteV(w, p);
            w.Write((byte)PoleBonePos.Length);
            for (int i = 0; i < PoleBonePos.Length; i++) { WriteV(w, PoleBonePos[i]); WriteQ(w, PoleBoneRot[i]); }
        }

        public static PoseFrame Read(BinaryReader r)
        {
            var f = new PoseFrame();
            f.Time = r.ReadSingle();
            f.PotPos = ReadV(r); f.PotRot = ReadQ(r);
            f.HammerPos = ReadV(r); f.HammerRot = ReadQ(r);
            f.DudePos = ReadV(r); f.DudeRot = ReadQ(r);
            f.HipsLocalPos = ReadV(r);
            f.Progress = r.ReadSingle();
            int n = r.ReadByte();
            f.Bones = new Quaternion[n];
            for (int i = 0; i < n; i++) f.Bones[i] = ReadQSmall(r);
            f.BonePos = new Vector3[n];
            for (int i = 0; i < n; i++) f.BonePos[i] = ReadV(r);
            int m = r.ReadByte();
            f.PoleBonePos = new Vector3[m]; f.PoleBoneRot = new Quaternion[m];
            for (int i = 0; i < m; i++) { f.PoleBonePos[i] = ReadV(r); f.PoleBoneRot[i] = ReadQ(r); }
            return f;
        }

        public static PoseFrame Lerp(PoseFrame a, PoseFrame b, float t)
        {
            var f = new PoseFrame
            {
                Time = Mathf.Lerp(a.Time, b.Time, t),
                PotPos = Vector3.Lerp(a.PotPos, b.PotPos, t),
                PotRot = Quaternion.Slerp(a.PotRot, b.PotRot, t),
                HammerPos = Vector3.Lerp(a.HammerPos, b.HammerPos, t),
                HammerRot = Quaternion.Slerp(a.HammerRot, b.HammerRot, t),
                DudePos = Vector3.Lerp(a.DudePos, b.DudePos, t),
                DudeRot = Quaternion.Slerp(a.DudeRot, b.DudeRot, t),
                HipsLocalPos = Vector3.Lerp(a.HipsLocalPos, b.HipsLocalPos, t),
                Progress = Mathf.Lerp(a.Progress, b.Progress, t),
                Bones = new Quaternion[Mathf.Min(a.Bones.Length, b.Bones.Length)]
            };
            f.BonePos = new Vector3[f.Bones.Length];
            for (int i = 0; i < f.Bones.Length; i++) { f.Bones[i] = Quaternion.Slerp(a.Bones[i], b.Bones[i], t); f.BonePos[i] = Vector3.Lerp(a.BonePos[i], b.BonePos[i], t); }
            int pm = Mathf.Min(a.PoleBonePos.Length, b.PoleBonePos.Length);
            f.PoleBonePos = new Vector3[pm]; f.PoleBoneRot = new Quaternion[pm];
            for (int i = 0; i < pm; i++) { f.PoleBonePos[i] = Vector3.Lerp(a.PoleBonePos[i], b.PoleBonePos[i], t); f.PoleBoneRot[i] = Quaternion.Slerp(a.PoleBoneRot[i], b.PoleBoneRot[i], t); }
            return f;
        }

        static void WriteV(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static Vector3 ReadV(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static void WriteQ(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        static Quaternion ReadQ(BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        // bones: 4 x int16 (plenty of precision for visuals)
        static void WriteQSmall(BinaryWriter w, Quaternion q)
        {
            w.Write((short)(Mathf.Clamp(q.x, -1f, 1f) * 32767)); w.Write((short)(Mathf.Clamp(q.y, -1f, 1f) * 32767));
            w.Write((short)(Mathf.Clamp(q.z, -1f, 1f) * 32767)); w.Write((short)(Mathf.Clamp(q.w, -1f, 1f) * 32767));
        }
        static Quaternion ReadQSmall(BinaryReader r)
        {
            var q = new Quaternion(r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 0.0001f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }
    }

    /// <summary>Reads the local player's pose every frame.</summary>
    public static class PoseCapture
    {
        public static PlayerControl Player;
        public static PoseControl PoseCtl;
        public static ProgressMeter Meter;
        static Transform pot, hammerMesh, dude, hips;
        static Transform[] bones;
        public static Transform PoleMesh;          // Player/handle (skinned pole mesh)
        public static Transform[] PoleBones;      // bones of that mesh (they live under the hammer joints)

        public static bool Ready => Player != null && pot != null;

        public static void Bind()
        {
            Player = Object.FindObjectOfType<PlayerControl>();
            PoseCtl = Object.FindObjectOfType<PoseControl>();
            Meter = Object.FindObjectOfType<ProgressMeter>();
            if (Player == null || PoseCtl == null) { pot = null; return; }
            pot = PoseCtl.potMeshHub;
            dude = PoseCtl.dudeMeshHub;
            hammerMesh = FindHammerMesh(Player.transform);
            hips = dude.Find("mixamorig:Hips");
            bones = Rig(dude);
            PoleMesh = Player.transform.Find("handle");
            var smr = PoleMesh != null ? PoleMesh.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            PoleBones = smr != null ? smr.bones : new Transform[0];
            Plugin.Log.LogInfo($"Pose bind: {bones.Length} body bones, {PoleBones.Length} pole bones");
        }

        /// <summary>The bone list in a stable order (same prefab on every PC).</summary>
        public static Transform[] Rig(Transform dudeRoot)
        {
            var hipsT = dudeRoot.Find("mixamorig:Hips");
            var list = new List<Transform>(hipsT.GetComponentsInChildren<Transform>(true));
            return list.ToArray();
        }

        public static Transform FindHammerMesh(Transform playerRoot)
        {
            foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "RetopoGroup1") return t;
            return null;
        }

        public static PoseFrame Capture(float time)
        {
            var f = new PoseFrame
            {
                Time = time,
                PotPos = pot.position, PotRot = pot.rotation,
                HammerPos = hammerMesh.position, HammerRot = hammerMesh.rotation,
                DudePos = dude.position, DudeRot = dude.rotation,
                HipsLocalPos = hips.localPosition,
                Progress = Meter != null ? Meter.progress : 0f,
                Bones = new Quaternion[bones.Length]
            };
            f.BonePos = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) { f.Bones[i] = bones[i].localRotation; f.BonePos[i] = bones[i].localPosition; }
            f.PoleBonePos = new Vector3[PoleBones.Length]; f.PoleBoneRot = new Quaternion[PoleBones.Length];
            for (int i = 0; i < PoleBones.Length; i++) { f.PoleBonePos[i] = PoleBones[i].position; f.PoleBoneRot[i] = PoleBones[i].rotation; }
            return f;
        }
    }
}
