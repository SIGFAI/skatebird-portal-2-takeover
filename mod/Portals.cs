// Portal pair: two glowing ovals on walls or floors; anything solid that reaches one comes out of the other with its speed kept.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Portal
{
    public bool blue;
    public Vector3 c, n, up, right;
    public GameObject root;
    public Light light;
    public Transform[] rim;
    public float born;
}

public class Mover
{
    public Rigidbody rb;
    public float half = 0.2f;
    public float cool;
}

public static class Portals
{
    public static readonly Color Blue = new Color(0.1f, 0.6f, 1f);
    public static readonly Color Orange = new Color(1f, 0.45f, 0.05f);
    public static Portal A, B;               // A = blue, B = orange
    public static readonly List<Mover> Movers = new List<Mover>();
    public static System.Action<bool> OnBoardPass;   // true = it came out of the blue portal
    public static System.Action<Rigidbody> OnPropPass;
    static float boardCool;
    const float HalfW = 0.6f, HalfH = 0.95f;

    public static float FloorY(Vector3 p)
    {
        if (RayFirst(p + Vector3.up * 0.6f, Vector3.down, 6f, out var h, false)) return h.point.y;
        return p.y;
    }

    /// <summary>First solid thing along a ray, skipping the bird, the board and the mod's own props.</summary>
    public static bool RayFirst(Vector3 origin, Vector3 dir, float range, out RaycastHit best, bool skipProps = true)
    {
        best = default;
        float bd = float.MaxValue;
        bool any = false;
        foreach (var h in Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance < bd && !h.collider.isTrigger && !IsRider(h.collider) && !(skipProps && h.collider.GetComponentInParent<Prop>() != null) && h.collider.GetComponentInParent<Cube>() == null && h.collider.GetComponentInParent<Turret>() == null)
            {
                best = h; bd = h.distance; any = true;
            }
        }
        return any;
    }

    static bool IsRider(Collider c)
    {
        var b = G.BoardObject;
        return b != null && (c.transform.IsChildOf(b.transform.root) || c.attachedRigidbody == G.Body);
    }

    public static Portal Open(bool blue, Vector3 point, Vector3 normal, Vector3 upHint)
    {
        Close(blue ? A : B);
        var p = new Portal { blue = blue, n = normal.normalized, born = Time.unscaledTime };
        bool floorish = Mathf.Abs(p.n.y) > 0.75f;
        var proj = Vector3.ProjectOnPlane(upHint, p.n);
        if (floorish) p.up = proj.sqrMagnitude > 0.01f ? proj.normalized : Vector3.forward;
        else p.up = Vector3.ProjectOnPlane(Vector3.up, p.n).normalized;
        p.right = Vector3.Cross(p.up, p.n).normalized;
        p.c = point;
        if (!floorish) p.c.y = Mathf.Max(point.y, FloorY(point) + 0.85f);
        p.c += p.n * 0.015f;
        var col = blue ? Blue : Orange;
        var root = new GameObject(blue ? "SigfPortalBlue" : "SigfPortalOrange");
        root.transform.SetPositionAndRotation(p.c, Quaternion.LookRotation(p.n, p.up));
        p.root = root;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root.transform, false);
        q.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        q.transform.localScale = new Vector3(HalfW * 2.15f, HalfH * 2.15f, 1f);
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = Mix.Texture(blue ? "portal_blue.png" : "portal_orange.png");
        q.GetComponent<Renderer>().material = mat;
        p.rim = new Transform[26];
        for (int i = 0; i < p.rim.Length; i++)
        {
            float a = i / (float)p.rim.Length * Mathf.PI * 2f;
            var s = Mix.Shape(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.07f, col, false, "SigfPortalRim");
            Mix.Paint(s, col, 2.5f);
            s.transform.SetParent(root.transform, false);
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * HalfW * 0.98f, Mathf.Sin(a) * HalfH * 0.98f, 0.03f);
            p.rim[i] = s.transform;
        }
        p.light = Mix.Glow(p.c + p.n * 0.5f, col, 3.5f, 2.2f, root.transform);
        if (blue) A = p; else B = p;
        Mix.Burst(p.c, col, 18, 2.5f, 0.05f, 1f);
        Mix.Play(Mix.Sound("portal_pass.wav"), p.c, 0.7f, blue ? 1f : 0.85f);
        return p;
    }

    public static void Close(Portal p)
    {
        if (p == null) return;
        if (p.root != null) Object.Destroy(p.root);
        if (p == A) A = null;
        if (p == B) B = null;
    }

    public static void CloseAll() { Close(A); Close(B); }

    public static void Update()
    {
        float t = Time.unscaledTime;
        foreach (var p in new[] { A, B })
        {
            if (p == null || p.root == null) continue;
            float grow = Mathf.Clamp01((t - p.born) / 0.35f);
            float sc = (0.25f + 0.75f * Mathf.SmoothStep(0, 1, grow)) * (1f + 0.025f * Mathf.Sin(t * 9f + (p.blue ? 0 : 2)));
            p.root.transform.localScale = Vector3.one * sc;
            for (int i = 0; i < p.rim.Length; i++)
            {
                float f = 0.75f + 0.5f * Mathf.PerlinNoise(i * 0.7f, t * 5f + (p.blue ? 0 : 9));
                p.rim[i].localScale = Vector3.one * 0.07f * f / sc;
            }
            p.light.intensity = 2f + 0.8f * Mathf.Sin(t * 7f);
        }
        if (A == null || B == null) return;
        boardCool -= Time.unscaledDeltaTime;
        if (G.Board != null && boardCool <= 0f) PassBoard();
        for (int i = Movers.Count - 1; i >= 0; i--)
        {
            var m = Movers[i];
            if (m.rb == null) { Movers.RemoveAt(i); continue; }
            m.cool -= Time.unscaledDeltaTime;
            if (m.cool <= 0f) PassProp(m);
        }
    }

    static bool Hits(Portal p, Vector3 pt, Vector3 v, float half, bool ground, float reach)
    {
        var rel = pt - p.c;
        float d = Vector3.Dot(rel, p.n);
        var lat = rel - p.n * d;
        float lx = Vector3.Dot(lat, p.right) / (HalfW + half * 0.4f), ly = Vector3.Dot(lat, p.up) / (HalfH + half * 0.4f);
        if (lx * lx + ly * ly >= 1f) return false;
        bool floorish = Mathf.Abs(p.n.y) > 0.75f;
        if (floorish) return d < 0.25f + half && d > -0.8f && (ground ? v.magnitude > 0.8f || v.y < -0.8f : Vector3.Dot(v, p.n) < -0.3f);
        return d < reach && d > -0.7f && Vector3.Dot(v, p.n) < -0.35f;
    }

    static Vector3 ExitPoint(Portal to, float lift)
    {
        var pos = to.c + to.n * 0.55f;
        if (Mathf.Abs(to.n.y) < 0.75f) pos.y = FloorY(pos) + lift;
        return pos;
    }

    // Rotates the velocity from the entry frame to the exit frame, turned half a turn: in through the face, out through the face.
    static Vector3 ExitVelocity(Portal from, Portal to, Vector3 v, float minOut)
    {
        var fromRot = Quaternion.LookRotation(from.n, from.up);
        var toRot = Quaternion.LookRotation(to.n, to.up);
        var q = toRot * Quaternion.Euler(0, 180f, 0) * Quaternion.Inverse(fromRot);
        var o = q * v;
        float outSpeed = Vector3.Dot(o, to.n);
        if (outSpeed < minOut) o += to.n * (minOut - outSpeed);
        return o;
    }

    static void PassBoard()
    {
        Vector3 pt = G.Pos + Vector3.up * 0.3f, v = G.Velocity;
        Portal from = null, to = null;
        if (Hits(A, pt, v, 0.25f, true, 1.1f)) { from = A; to = B; }
        else if (Hits(B, pt, v, 0.25f, true, 1.1f)) { from = B; to = A; }
        if (from == null) return;
        var nv = ExitVelocity(from, to, v, Mathf.Max(3.5f, v.magnitude * 0.9f));
        Vector3 pos = ExitPoint(to, 0.25f);
        Vector3 dir = Vector3.ProjectOnPlane(nv, Vector3.up);
        if (dir.sqrMagnitude < 0.05f) dir = Vector3.ProjectOnPlane(G.Forward, Vector3.up);
        if (Mathf.Abs(to.n.y) > 0.75f) pos = to.c + to.n * 0.4f;
        G.Teleport(pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
        G.SetVelocity(nv);
        boardCool = 0.6f;
        Mix.Flash(to.blue ? Blue : Orange, 0.8f, 0.75f);
        Mix.Burst(from.c, from.blue ? Blue : Orange, 14, 3f, 0.05f, 0.8f);
        Mix.Burst(pos, to.blue ? Blue : Orange, 22, 3.5f, 0.05f, 1f);
        Mix.Play(Mix.Sound("portal_pass.wav"), pos, 0.9f);
        Mix.Log("board passed " + (from.blue ? "blue" : "orange") + " -> " + (to.blue ? "blue" : "orange") + " speed " + v.magnitude.ToString("F1") + " -> " + nv.magnitude.ToString("F1"));
        OnBoardPass?.Invoke(to.blue);
    }

    static void PassProp(Mover m)
    {
        Vector3 pt = m.rb.position, v = m.rb.velocity;
        Portal from = null, to = null;
        if (Hits(A, pt, v, m.half, false, 0.7f)) { from = A; to = B; }
        else if (Hits(B, pt, v, m.half, false, 0.7f)) { from = B; to = A; }
        if (from == null) return;
        var nv = ExitVelocity(from, to, v, Mathf.Max(2.5f, v.magnitude));
        var pos = to.c + to.n * (0.35f + m.half);
        m.rb.position = pos; m.rb.transform.position = pos;
        m.rb.velocity = nv;
        m.cool = 0.5f;
        Mix.Burst(pos, to.blue ? Blue : Orange, 12, 3f, 0.04f, 0.8f);
        Mix.Play(Mix.Sound("portal_pass.wav"), pos, 0.7f, 1.2f);
        Mix.Log("prop passed portal speed " + v.magnitude.ToString("F1") + " -> " + nv.magnitude.ToString("F1"));
        OnPropPass?.Invoke(m.rb);
    }

    /// <summary>The portal gun: a glowing bolt flies from the bird to the surface it aims at, then the portal opens there.</summary>
    public static bool Fire(bool blue, Vector3 origin, Vector3 dir)
    {
        if (!RayFirst(origin, dir, 70f, out var h)) { Mix.Log("fire: no surface"); return false; }
        Mix.Log("fire " + (blue ? "blue" : "orange") + " at " + h.collider.name + " " + h.point + " n " + h.normal);
        var col = blue ? Blue : Orange;
        var bolt = Mix.Sphere(origin, 0.12f, col, false);
        Mix.Paint(bolt, col, 3f);
        Mix.Glow(origin, col, 3f, 3f, bolt.transform);
        Mix.Play(Mix.Sound("portal_shot.wav"), origin, 0.9f, blue ? 1.15f : 0.95f);
        Mix.Run(Fly(bolt, origin, h.point, h.normal, blue), "PortalBolt");
        return true;
    }

    static System.Collections.IEnumerator Fly(GameObject bolt, Vector3 from, Vector3 to, Vector3 n, bool blue)
    {
        float dur = Mathf.Clamp(Vector3.Distance(from, to) / 40f, 0.12f, 0.6f), t = 0;
        while (t < dur && bolt != null)
        {
            t += Time.unscaledDeltaTime;
            bolt.transform.position = Vector3.Lerp(from, to, t / dur);
            Mix.Burst(bolt.transform.position, blue ? Blue : Orange, 1, 0.3f, 0.05f, 0.4f);
            yield return null;
        }
        if (bolt != null) Object.Destroy(bolt);
        Open(blue, to, n, G.Forward);
    }
}
