// Gel puddles on the floor: blue repulsion gel throws whatever rolls on it into the air, orange propulsion gel makes it shoot forward.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Gel
{
    public bool bounce;
    public Vector3 pos;
    public float radius;
    public GameObject root;
    public float cool;
}

public static class Gels
{
    public static readonly List<Gel> All = new List<Gel>();
    static readonly Color BlueGel = new Color(0.15f, 0.45f, 1f), OrangeGel = new Color(1f, 0.5f, 0.05f);

    public static void Clear()
    {
        foreach (var g in All) if (g.root != null) Object.Destroy(g.root);
        All.Clear();
    }

    public static void Add(bool bounce, Vector3 floorPos, float radius = 0.95f)
    {
        var col = bounce ? BlueGel : OrangeGel;
        var g = new Gel { bounce = bounce, pos = floorPos, radius = radius };
        var root = new GameObject(bounce ? "SigfGelBlue" : "SigfGelOrange");
        root.transform.position = floorPos;
        g.root = root;
        // an irregular splat: a few overlapping flat blobs
        var rnd = new System.Random((int)(floorPos.x * 100) ^ (int)(floorPos.z * 37));
        for (int i = 0; i < 7; i++)
        {
            float a = (float)rnd.NextDouble() * 6.28f, d = i == 0 ? 0f : radius * 0.55f * (float)rnd.NextDouble();
            float sz = i == 0 ? radius * 1.7f : radius * (0.5f + 0.5f * (float)rnd.NextDouble());
            var blob = Build.Part(root.transform, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * d, 0.012f + i * 0.002f, Mathf.Sin(a) * d), new Vector3(sz, 0.012f, sz * (0.75f + 0.3f * (float)rnd.NextDouble())), col, 0.9f, new Vector3(0, (float)rnd.NextDouble() * 180f, 0));
            Object.Destroy(blob.GetComponent<Collider>());
        }
        Mix.Glow(floorPos + Vector3.up * 0.5f, col, 2.5f, 1.4f, root.transform);
        All.Add(g);
    }

    public static void Update()
    {
        for (int i = 0; i < All.Count; i++)
        {
            var g = All[i];
            g.cool -= Time.unscaledDeltaTime;
            if (g.root == null) continue;
            if (g.cool <= 0f && G.Board != null && Near(g, G.Pos) && G.Pos.y - g.pos.y < 0.7f)
            {
                g.cool = 0.5f;
                var col = g.bounce ? BlueGel : OrangeGel;
                if (g.bounce)
                {
                    G.Launch(6.5f);
                    Mix.Play(Mix.Sound("portal_pass.wav"), G.Pos, 0.7f, 1.8f);
                }
                else
                {
                    var v = G.Velocity; v.y = 0f;
                    var dir = v.sqrMagnitude > 0.5f ? v.normalized : G.Forward;
                    G.SetVelocity(dir * 11f + Vector3.up * 0.3f);
                    Mix.Play(Mix.Sound("portal_shot.wav"), G.Pos, 0.7f, 1.3f);
                }
                Mix.Burst(G.Pos + Vector3.up * 0.1f, col, 22, 3.2f, 0.06f, 0.9f);
                Mix.Log("gel " + (g.bounce ? "blue bounce" : "orange boost"));
            }
            foreach (var m in Portals.Movers)
            {
                if (m.rb == null || !Near(g, m.rb.position) || m.rb.position.y - g.pos.y > 0.6f) continue;
                if (g.bounce && m.rb.velocity.y < 1f) { var v = m.rb.velocity; v.y = 5.5f; m.rb.velocity = v; Mix.Burst(m.rb.position, BlueGel, 12, 2.5f, 0.05f, 0.7f); }
                else if (!g.bounce) { var v = m.rb.velocity; v.y = 0; if (v.magnitude > 0.5f && v.magnitude < 8f) m.rb.velocity = v.normalized * 8f; }
            }
        }
    }

    static bool Near(Gel g, Vector3 p)
    {
        var d = p - g.pos; d.y = 0f;
        return d.magnitude < g.radius;
    }
}
