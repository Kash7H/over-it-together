using System.Collections.Generic;
using UnityEngine;

namespace OverItTogether
{
    /// <summary>
    /// A visual-only copy of another player: pot, hammer and Diogenes cloned from the local player,
    /// with every script, collider, rigidbody, joint, camera, light, sound and particle removed.
    /// It cannot touch anything. Poses arrive as PoseFrames and are interpolated.
    /// </summary>
    public class Ghost
    {
        public string Name;
        public Color Tint;
        public GameObject Root;
        Transform pot, hammer, dude, hips;
        Transform[] bones;
        Transform[] poleBones;
        TextMesh label;
        readonly List<PoseFrame> buffer = new List<PoseFrame>();
        public PoseFrame Latest;
        public float LastReceived;
        const float InterpDelay = 0.12f;   // seconds behind the newest frame, smooths network jitter
        const float ZOffset = 0.6f;        // draw slightly behind the local player

        public Ghost(string name, Color tint)
        {
            Name = name; Tint = tint;
            Build();
        }

        void Build()
        {
            var pose = PoseCapture.PoseCtl;
            var player = PoseCapture.Player.transform;
            Root = new GameObject("OIT_Ghost_" + Name);
            Object.DontDestroyOnLoad(Root);

            pot = Clone(pose.potMeshHub, Root.transform);
            hammer = Clone(PoseCapture.FindHammerMesh(player), Root.transform);
            dude = Clone(pose.dudeMeshHub, Root.transform);
            hips = dude.Find("mixamorig:Hips");
            bones = PoseCapture.Rig(dude);

            // hammer pole: a skinned mesh whose bones live under the real player's joints.
            // Clone it, then point it at our own stand-in bones.
            if (PoseCapture.PoleMesh != null && PoseCapture.PoleBones.Length > 0)
            {
                var poleRoot = Clone(PoseCapture.PoleMesh, Root.transform);
                var smr = poleRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
                poleBones = new Transform[PoseCapture.PoleBones.Length];
                for (int i = 0; i < poleBones.Length; i++)
                {
                    poleBones[i] = new GameObject("PoleBone" + i).transform;
                    poleBones[i].SetParent(Root.transform, false);
                    poleBones[i].localScale = PoseCapture.PoleBones[i].lossyScale;
                }
                if (smr != null)
                {
                    smr.bones = poleBones;
                    smr.rootBone = poleBones[0];
                    smr.updateWhenOffscreen = true;
                }
            }

            var tag = new GameObject("Label");
            tag.transform.SetParent(Root.transform, false);
            label = tag.AddComponent<TextMesh>();
            label.text = Name;
            label.characterSize = 0.08f;
            label.fontSize = 48;
            label.anchor = TextAnchor.LowerCenter;
            label.color = Tint;
            label.GetComponent<MeshRenderer>().sortingOrder = 1000;
        }

        Transform Clone(Transform src, Transform parent)
        {
            var go = Object.Instantiate(src.gameObject, parent);
            go.name = src.name;
            go.transform.localScale = src.lossyScale;   // parent scale is lost when cloning a child
            Strip(go);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                // per-ghost material copies, tinted
                var mats = r.materials;
                foreach (var m in mats)
                    if (m.HasProperty("_Color")) m.color = Color.Lerp(m.color, Tint, 0.45f);
                r.materials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go.transform;
        }

        static void Strip(GameObject go)
        {
            // order matters: joints before bodies, scripts last
            foreach (var c in go.GetComponentsInChildren<Joint2D>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in go.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in go.GetComponentsInChildren<ReflectionProbe>(true)) Object.DestroyImmediate(c.gameObject);
        }

        public void Push(PoseFrame f)
        {
            buffer.Add(f);
            Latest = f;
            LastReceived = UnityEngine.Time.unscaledTime;
            if (buffer.Count > 40) buffer.RemoveAt(0);
        }

        /// <summary>Show the pose at (newest time - delay), interpolated.</summary>
        public void Update()
        {
            if (Root == null || buffer.Count == 0) return;
            float target = buffer[buffer.Count - 1].Time - InterpDelay;
            PoseFrame show = buffer[0];
            for (int i = buffer.Count - 1; i > 0; i--)
            {
                var a = buffer[i - 1]; var b = buffer[i];
                if (a.Time <= target && b.Time >= target)
                {
                    float span = b.Time - a.Time;
                    show = PoseFrame.Lerp(a, b, span > 0.0001f ? (target - a.Time) / span : 1f);
                    break;
                }
                if (b.Time < target) { show = b; break; }
            }
            Apply(show);
        }

        public void ApplyDirect(PoseFrame f) => Apply(f);

        void Apply(PoseFrame f)
        {
            var z = new Vector3(0, 0, ZOffset);
            pot.SetPositionAndRotation(f.PotPos + z, f.PotRot);
            hammer.SetPositionAndRotation(f.HammerPos + z, f.HammerRot);
            dude.SetPositionAndRotation(f.DudePos + z, f.DudeRot);
            hips.localPosition = f.HipsLocalPos;
            int n = Mathf.Min(bones.Length, f.Bones.Length);
            for (int i = 0; i < n; i++) { bones[i].localRotation = f.Bones[i]; if (f.BonePos != null) bones[i].localPosition = f.BonePos[i]; }
            if (poleBones != null && f.PoleBonePos != null)
            {
                int m = Mathf.Min(poleBones.Length, f.PoleBonePos.Length);
                for (int i = 0; i < m; i++) poleBones[i].SetPositionAndRotation(f.PoleBonePos[i] + z, f.PoleBoneRot[i]);
            }
            label.transform.position = f.PotPos + new Vector3(0, 1.6f, -1f);
            label.transform.rotation = Quaternion.identity;
        }

        public void SetVisible(bool v) { if (Root != null && Root.activeSelf != v) Root.SetActive(v); }
        public void ShowLabel(bool v) { if (label != null && label.gameObject.activeSelf != v) label.gameObject.SetActive(v); }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }
    }
}
