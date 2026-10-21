using System.Collections.Generic;
using System.Linq;
using BepInEx;
using FluffyUnderware.Curvy;
using UnityEngine;

namespace OverItTogether
{
    /// <summary>
    /// The community's speedrun splits, using the same trigger boxes as the official LiveSplit
    /// autosplitter for this game (Tairoon's GettingOverIt.asl): a split fires the first time the
    /// player's position enters its box. Overall "% to top" still comes from the game's route spline.
    /// </summary>
    public static class Splits
    {
        public struct Box { public float MinX, MaxX, MinY, MaxY; }
        public static readonly List<string> Names = new List<string>();
        public static readonly List<Box> Boxes = new List<Box>();
        public static int Count => Names.Count;
        public static float RouteLength;

        const float Inf = 1e9f;
        static readonly (string name, Box box)[] Default =
        {
            ("Tutorial",     new Box { MinX = -12, MaxX = -8,  MinY = -Inf, MaxY = Inf }),
            ("Chimney",      new Box { MinX = 24,  MaxX = Inf, MinY = 82,   MaxY = Inf }),
            ("Slide",        new Box { MinX = 12,  MaxX = 18,  MinY = 123,  MaxY = 128 }),
            ("Furniture",    new Box { MinX = 4,   MaxX = 6,   MinY = 162,  MaxY = 167 }),
            ("Orange Hell",  new Box { MinX = 18,  MaxX = 24,  MinY = 216,  MaxY = 222 }),
            ("Anvil",        new Box { MinX = 73,  MaxX = 76,  MinY = 249,  MaxY = 254 }),
            ("Bucket",       new Box { MinX = 18,  MaxX = 22,  MinY = 279,  MaxY = 285 }),
            ("Ice Mountain", new Box { MinX = 42,  MaxX = 46,  MinY = 317,  MaxY = 323 }),
            ("Tower",        new Box { MinX = -Inf, MaxX = Inf, MinY = 359, MaxY = 365 }),
        };

        public static void Load()
        {
            Names.Clear(); Boxes.Clear();
            foreach (var d in Default) { Names.Add(d.name); Boxes.Add(d.box); }
            var meter = PoseCapture.Meter;
            var spline = meter != null ? meter.GetComponent<CurvySpline>() : null;
            if (spline != null) { if (spline.Count == 0) spline.Refresh(); RouteLength = spline.Length; }
            Plugin.Log.LogInfo("Splits: " + string.Join(", ", Names) + $" (route {RouteLength:F0})");
        }

        public static bool Inside(int i, Vector2 p)
        {
            var b = Boxes[i];
            return p.x > b.MinX && p.x < b.MaxX && p.y > b.MinY && p.y < b.MaxY;
        }

        public static Dictionary<string, Transform> SpawnPoints()
        {
            var dict = new Dictionary<string, Transform>();
            var ds = Object.FindObjectOfType<DebugSpawn>();
            if (ds != null) foreach (Transform c in ds.transform) dict[c.name] = c;
            return dict;
        }
    }
    /// <summary>Restart, freeze, save/load and teleport, built on the game's own Saviour.</summary>
    public static class Practice
    {
        static SaveState saved;
        static Saviour Sav => Object.FindObjectOfType<Saviour>();

        public static void ResetToStart()
        {
            Guard.Hold();
            var s = Sav; if (s == null) return;
            s.ResetPlayerButNotDialogue();
        }

        public static void FreezeInput(float seconds)
        {
            PoseCapture.Player?.PauseInput(seconds);
        }

        public static void SavePosition()
        {
            var s = Sav; if (s == null) return;
            saved = s.Save();
            Session.I?.Notice("Position saved");
        }

        public static void LoadPosition()
        {
            Guard.Hold();
            var s = Sav; if (s == null || saved == null) { Session.I?.Notice("Nothing saved yet"); return; }
            s.Load(saved);
        }

        /// <summary>Move the whole player (all 6 bodies) so it stands at `target`, at rest.</summary>
        public static void TeleportTo(Vector3 target)
        {
            Guard.Hold();
            var s = Sav; if (s == null) return;
            var st = s.Save();
            var delta = (Vector2)(target - st.playerPos) + new Vector2(0, 0.6f);
            st.playerPos += (Vector3)delta;
            for (int i = 0; i < st.rbPositions.Length; i++) st.rbPositions[i] += delta;
            for (int i = 0; i < st.rbLinearVelocities.Length; i++) st.rbLinearVelocities[i] = Vector2.zero;
            for (int i = 0; i < st.rbAngularVelocities.Length; i++) st.rbAngularVelocities[i] = 0f;
            st.camPos += (Vector3)delta;
            s.Load(st);
        }

        /// <summary>Teleport targets: the start plus, for each split, the game's nearest built-in checkpoint.</summary>
        public static List<(string name, Vector3 pos)> SectionPoints()
        {
            var list = new List<(string, Vector3)>();
            var spawns = Splits.SpawnPoints();
            if (spawns.TryGetValue("Spawn01", out var start)) list.Add(("Start", start.position));
            for (int i = 0; i < Splits.Count; i++)
            {
                var b = Splits.Boxes[i];
                float cx = b.MinX < -1e8f || b.MaxX > 1e8f ? float.NaN : (b.MinX + b.MaxX) / 2f;
                float cy = b.MinY < -1e8f || b.MaxY > 1e8f ? float.NaN : (b.MinY + b.MaxY) / 2f;
                Transform best = null; float bestD = float.MaxValue;
                foreach (var t in spawns.Values)
                {
                    float dx = float.IsNaN(cx) ? 0 : t.position.x - cx, dy = float.IsNaN(cy) ? 0 : t.position.y - cy;
                    // boxes with an open side (e.g. Chimney: x > 24, y > 82) prefer points inside them
                    if (float.IsNaN(cx) && (t.position.x < b.MinX || t.position.x > b.MaxX)) dx = 1000;
                    if (float.IsNaN(cy) && (t.position.y < b.MinY || t.position.y > b.MaxY)) dy = 1000;
                    float d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best != null) list.Add((Splits.Names[i], best.position));
            }
            return list;
        }

        public static void TeleportToSpawn(string name)
        {
            if (Splits.SpawnPoints().TryGetValue(name, out var t)) TeleportTo(t.position);
        }
    }
}
