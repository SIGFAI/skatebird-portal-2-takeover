// Test-chamber props: companion cube, sentry turret, cake, hovering core, wall slabs. All have real colliders.
using System.Collections;
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Prop : MonoBehaviour { }

public static class Build
{
    public static GameObject Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 scale, Color c, float glow = 0f, Vector3? euler = null)
    {
        var go = Mix.Shape(t, Vector3.zero, scale, c, false, "SigfPart");
        if (glow > 0f) Mix.Paint(go, c, glow);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
        return go;
    }

    public static Texture2D Panel()
    {
        var t = Mix.Texture("panel.png");
        t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    /// <summary>A solid white wall slab standing on the floor; face points along normal. Returns the point on its face.</summary>
    public static Vector3 Slab(Vector3 basePos, Vector3 normal, float w, float h, float thick)
    {
        float fy = Portals.FloorY(basePos);
        var center = new Vector3(basePos.x, fy + h / 2f, basePos.z) - normal * (thick / 2f);
        var go = Mix.Shape(PrimitiveType.Cube, center, new Vector3(w, h, thick), Color.white, true, "SigfSlab");
        go.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
        go.transform.localScale = new Vector3(w, h, thick);
        Mix.Paint(go, new Color(0.9f, 0.93f, 1f), 0f, Panel());
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.material.mainTextureScale = new Vector2(w / 1.2f, h / 1.2f);
        // dark trim along the top so the slab reads as a chamber wall
        var trim = Part(go.transform, PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(1.02f, 0.025f / h * 4f, 1.02f), new Color(0.12f, 0.13f, 0.15f));
        return new Vector3(basePos.x, fy, basePos.z);
    }
}

public class Cube : MonoBehaviour
{
    Rigidbody rb; float soundCool;
    public static GameObject Spawn(Vector3 pos)
    {
        var go = Mix.Shape(PrimitiveType.Cube, pos, Vector3.one * 0.36f, Color.white, true, "SigfCompanionCube");
        Mix.Paint(go, Color.white, 0f, Mix.Texture("cube.png"));
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.8f; rb.drag = 0.15f; rb.angularDrag = 0.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        go.GetComponent<Collider>().material = new PhysicMaterial { dynamicFriction = 0.03f, staticFriction = 0.2f, bounciness = 0.15f, frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Average };
        Mix.Log("gravity " + Physics.gravity);
        go.AddComponent<Prop>();
        go.AddComponent<Cube>();
        Mix.Glow(pos, new Color(1f, 0.5f, 0.75f), 1.2f, 0.8f, go.transform);
        Portals.Movers.Add(new Mover { rb = rb, half = 0.22f });
        return go;
    }
    void Awake() { rb = GetComponent<Rigidbody>(); }
    void Update()
    {
        soundCool -= Time.unscaledDeltaTime;
        if (transform.position.y < Portals.FloorY(transform.position) - 8f) transform.position = G.Pos + G.Forward * 3f + Vector3.up * 1f;
    }
    void OnCollisionEnter(Collision c)
    {
        if (soundCool > 0f || c.relativeVelocity.magnitude < 1f) return;
        soundCool = 0.25f;
        Mix.Play(Mix.Sound("cube_hit.wav"), transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 5f), Random.Range(0.9f, 1.15f));
    }
}

public class Turret : MonoBehaviour
{
    public static readonly List<Turret> All = new List<Turret>();
    public static System.Action<Turret> OnDown;
    public static float VoiceCool;
    public int hp = 3;
    public bool dead;
    Rigidbody rb;
    Renderer[] bodyRends; Color[] bodyCols;
    Transform eye, beam; Light eyeLight;
    float aware, fireAt, hurtCool, flash;
    bool pinged;
    int shots;

    public static Turret Spawn(Vector3 pos)
    {
        var root = new GameObject("SigfTurret");
        root.transform.position = pos;
        var cap = root.AddComponent<CapsuleCollider>();
        cap.radius = 0.13f; cap.height = 0.62f; cap.center = new Vector3(0, 0.31f, 0);
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.9f; rb.drag = 2.5f; rb.angularDrag = 2f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        root.AddComponent<Prop>();
        var white = new Color(0.95f, 0.96f, 1f); var dark = new Color(0.15f, 0.16f, 0.18f);
        var t = root.transform;
        Build.Part(t, PrimitiveType.Capsule, new Vector3(0, 0.34f, 0), new Vector3(0.16f, 0.22f, 0.16f), white);
        Build.Part(t, PrimitiveType.Cube, new Vector3(-0.115f, 0.34f, -0.01f), new Vector3(0.025f, 0.34f, 0.14f), white, 0f, new Vector3(0, -12, 0));
        Build.Part(t, PrimitiveType.Cube, new Vector3(0.115f, 0.34f, -0.01f), new Vector3(0.025f, 0.34f, 0.14f), white, 0f, new Vector3(0, 12, 0));
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(-0.07f, 0.28f, 0.1f), new Vector3(0.025f, 0.06f, 0.025f), dark, 0f, new Vector3(90, 0, 0));
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0.07f, 0.28f, 0.1f), new Vector3(0.025f, 0.06f, 0.025f), dark, 0f, new Vector3(90, 0, 0));
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad + 0.5f;
            var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var leg = Build.Part(t, PrimitiveType.Cylinder, dir * 0.07f + new Vector3(0, 0.11f, 0), new Vector3(0.015f, 0.12f, 0.015f), dark);
            leg.transform.localRotation = Quaternion.AngleAxis(-25f, Vector3.Cross(Vector3.up, dir));
        }
        var eyeGo = Build.Part(t, PrimitiveType.Sphere, new Vector3(0, 0.38f, 0.1f), new Vector3(0.075f, 0.075f, 0.05f), new Color(1f, 0.1f, 0.05f), 4f);
        var tu = root.AddComponent<Turret>();
        tu.eye = eyeGo.transform;
        tu.eyeLight = Mix.Glow(eyeGo.transform.position + root.transform.forward * 0.1f, new Color(1f, 0.1f, 0.05f), 1.6f, 1.2f, eyeGo.transform);
        var b = Mix.Shape(PrimitiveType.Cube, pos, new Vector3(0.012f, 0.012f, 1f), Color.red, false, "SigfBeam");
        Mix.Paint(b, new Color(1f, 0.1f, 0.1f), 4f);
        b.SetActive(false);
        tu.beam = b.transform;
        tu.rb = rb;
        return tu;
    }

    void Awake() { All.Add(this); bodyRends = null; }
    void OnDestroy() { All.Remove(this); if (beam != null) Destroy(beam.gameObject); }

    Vector3 EyePos => transform.position + Vector3.up * 0.38f + transform.forward * 0.1f;

    void Update()
    {
        if (dead) return;
        if (bodyRends == null)
        {
            bodyRends = GetComponentsInChildren<Renderer>();
            bodyCols = new Color[bodyRends.Length];
            for (int i = 0; i < bodyRends.Length; i++) bodyCols[i] = bodyRends[i].material.color;
        }
        hurtCool -= Time.unscaledDeltaTime;
        if (flash > 0f)
        {
            flash -= Time.unscaledDeltaTime;
            if (flash <= 0f) for (int i = 0; i < bodyRends.Length; i++) if (bodyRends[i] != null && bodyRends[i].transform != eye) bodyRends[i].material.color = bodyCols[i];
        }
        var target = G.Pos + Vector3.up * 0.3f;
        var to = target - EyePos;
        float flat = new Vector3(to.x, 0, to.z).magnitude;
        bool see = flat < 10f;
        if (!see) { aware = 0f; beam.gameObject.SetActive(false); pinged = false; return; }
        aware += Time.unscaledDeltaTime;
        var look = Quaternion.LookRotation(new Vector3(to.x, 0, to.z) + Vector3.forward * 0.001f, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, Time.unscaledDeltaTime * 5f));
        eye.localScale = new Vector3(0.075f, 0.075f, 0.05f) * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f));
        beam.gameObject.SetActive(true);
        float len = to.magnitude;
        beam.position = EyePos + to / 2f; beam.rotation = Quaternion.LookRotation(to);
        beam.localScale = new Vector3(0.012f, 0.012f, len);
        if (!pinged && aware > 0.4f)
        {
            pinged = true;
            Mix.Play(Mix.Sound("turret_ping.wav"), transform.position, 0.8f, Random.Range(0.95f, 1.1f));
            if (VoiceCool <= 0f) { VoiceCool = 14f; Mix.Play(Mix.Sound("turret1.wav"), transform.position, 1f); Mix.Say("TURRET: \"Hello! Target acquired.\"", 2.2f, new Color(1f, 0.5f, 0.45f), 0.76f, 30); }
        }
        if (aware > 1.0f && Time.unscaledTime > fireAt) { fireAt = Time.unscaledTime + 1.6f; Mix.Run(Burst(), "TurretFire"); }
    }

    IEnumerator Burst()
    {
        for (int i = 0; i < 4 && !dead; i++)
        {
            Shoot();
            yield return Mix.Wait(0.12f);
        }
    }

    void Shoot()
    {
        var from = EyePos;
        var spread = Random.insideUnitSphere * 0.22f;
        var target = G.Pos + Vector3.up * 0.3f + spread;
        var b = Mix.Shape(PrimitiveType.Cube, from, new Vector3(0.025f, 0.025f, 0.14f), new Color(1f, 0.85f, 0.2f), false, "SigfBullet");
        Mix.Paint(b, new Color(1f, 0.85f, 0.2f), 4f);
        var l = Mix.Glow(from, new Color(1f, 0.8f, 0.3f), 2f, 2.5f);
        Destroy(l.gameObject, 0.06f);
        Mix.Burst(from, new Color(1f, 0.9f, 0.4f), 3, 1.5f, 0.03f, 0.2f);
        Mix.Run(Fly(b, from, target), "Bullet");
    }

    static IEnumerator Fly(GameObject b, Vector3 from, Vector3 target)
    {
        var d = (target - from).normalized;
        var p = from; float life = 0;
        b.transform.rotation = Quaternion.LookRotation(d);
        while (b != null && life < 1.4f)
        {
            p += d * 9f * Time.unscaledDeltaTime; life += Time.unscaledDeltaTime;
            b.transform.position = p;
            if (Vector3.Distance(p, G.Pos + Vector3.up * 0.3f) < 0.3f || Vector3.Dot(target - p, d) < 0f) break;
            yield return null;
        }
        if (b != null) { Mix.Burst(p, new Color(1f, 0.8f, 0.3f), 5, 2f, 0.03f, 0.3f); Destroy(b); }
    }

    void OnCollisionEnter(Collision c) => Smash(c, c.relativeVelocity.magnitude, 1.6f);
    void OnCollisionStay(Collision c) => Smash(c, Mathf.Max(c.relativeVelocity.magnitude, c.rigidbody != null ? c.rigidbody.velocity.magnitude : 0f), 2.5f);

    // the board or a cube slamming into a turret hurts it; shoving it along keeps hurting it
    void Smash(Collision c, float speed, float min)
    {
        if (dead || hurtCool > 0f) return;
        bool byBoard = c.rigidbody == G.Body || (c.collider != null && G.BoardObject != null && c.collider.transform.IsChildOf(G.BoardObject.transform.root));
        bool byCube = c.rigidbody != null && c.rigidbody.GetComponent<Cube>() != null;
        if (!(byBoard || byCube)) return;
        if (byBoard) speed = Mathf.Max(speed, G.Speed);
        if (speed < min) return;
        hurtCool = 0.4f;
        var at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        Hurt(speed >= 4.5f ? 2 : 1, at, (transform.position - at).normalized);
    }

    public void Hurt(int dmg, Vector3 at, Vector3 dir)
    {
        hp -= dmg;
        flash = 0.12f;
        foreach (var r in bodyRends) if (r != null) r.material.color = Color.white;
        Mix.Burst(at, new Color(1f, 0.85f, 0.3f), 14, 3.5f, 0.035f, 0.7f);
        var l = Mix.Glow(at, new Color(1f, 0.8f, 0.4f), 2.5f, 3f);
        Destroy(l.gameObject, 0.1f);
        Mix.Play(Mix.Sound("cube_hit.wav"), at, 1f, 1.25f);
        dir.y = 0; rb.AddForce((dir.normalized + Vector3.up * 0.6f) * 1.6f, ForceMode.Impulse);
        Mix.Log("turret hit, hp " + hp);
        if (hp <= 0) Die(at);
    }

    void Die(Vector3 at)
    {
        dead = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.AddTorque(Random.onUnitSphere * 0.6f, ForceMode.Impulse);
        rb.AddForce(Vector3.up * 1.2f, ForceMode.Impulse);
        if (beam != null) beam.gameObject.SetActive(false);
        if (eyeLight != null) eyeLight.intensity = 0f;
        Mix.Paint(eye.gameObject, new Color(0.12f, 0.02f, 0.02f));
        Mix.Burst(transform.position + Vector3.up * 0.3f, new Color(1f, 0.8f, 0.3f), 30, 4f, 0.04f, 1.2f);
        Mix.Burst(transform.position + Vector3.up * 0.3f, new Color(0.9f, 0.95f, 1f), 10, 3f, 0.07f, 1.5f);
        var l = Mix.Glow(transform.position + Vector3.up * 0.3f, new Color(1f, 0.7f, 0.2f), 4f, 4f);
        Destroy(l.gameObject, 0.25f);
        Mix.Play(Mix.Sound("turret_down.wav"), transform.position, 1f);
        // sparks keep fizzing from the wreck
        for (int i = 1; i <= 4; i++) Mix.After(0.4f * i, () => { if (this != null) Mix.Burst(transform.position + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.3f), 6, 2f, 0.03f, 0.5f); });
        OnDown?.Invoke(this);
    }
}

public class Cake : MonoBehaviour
{
    public static Cake Current;
    public static System.Action OnEaten;
    float y0, armed;
    public static void Spawn(Vector3 pos)
    {
        var root = new GameObject("SigfCake");
        root.transform.position = pos;
        var t = root.transform;
        var sponge = new Color(0.78f, 0.5f, 0.25f); var cream = new Color(1f, 0.95f, 0.88f); var pink = new Color(1f, 0.4f, 0.6f);
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.07f, 0), new Vector3(0.42f, 0.07f, 0.42f), sponge);
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(0.44f, 0.018f, 0.44f), cream);
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.23f, 0), new Vector3(0.32f, 0.06f, 0.32f), sponge);
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.30f, 0), new Vector3(0.34f, 0.018f, 0.34f), pink);
        Build.Part(t, PrimitiveType.Sphere, new Vector3(0.07f, 0.35f, 0.04f), Vector3.one * 0.07f, Color.red, 0.6f);
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(-0.04f, 0.39f, -0.02f), new Vector3(0.02f, 0.06f, 0.02f), new Color(0.4f, 0.7f, 1f));
        Build.Part(t, PrimitiveType.Sphere, new Vector3(-0.04f, 0.47f, -0.02f), new Vector3(0.035f, 0.06f, 0.035f), new Color(1f, 0.8f, 0.2f), 4f);
        Mix.Glow(pos + Vector3.up * 0.6f, new Color(1f, 0.75f, 0.35f), 3f, 2.5f, t);
        // a tall beam of light so the cake can be seen from across the chamber
        var beam = Build.Part(t, PrimitiveType.Cylinder, new Vector3(0, 2.2f, 0), new Vector3(0.12f, 2.2f, 0.12f), new Color(1f, 0.9f, 0.5f), 2.5f);
        Object.Destroy(beam.GetComponent<Collider>());
        t.localScale = Vector3.one * 1.5f;
        Mix.Play(Mix.Sound("portal_pass.wav"), pos, 0.8f, 1.5f);
        var c = root.AddComponent<Cake>();
        c.y0 = pos.y; c.armed = Time.unscaledTime + 3f;
        Current = c;
    }
    void Update()
    {
        transform.Rotate(0, 90f * Time.unscaledDeltaTime, 0);
        var p = transform.position; p.y = y0 + 0.5f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f);
        transform.position = p;
        Mix.Burst(transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.4f), 1, 0.5f, 0.025f, 0.8f);
        if (Time.unscaledTime > armed && Vector3.Distance(G.Pos + Vector3.up * 0.3f, transform.position + Vector3.up * 0.2f) < 1.0f)
        {
            Mix.Burst(transform.position, Color.white, 30, 4f, 0.05f, 1.5f);
            Mix.Burst(transform.position, new Color(1f, 0.4f, 0.7f), 30, 4f, 0.05f, 1.5f);
            Mix.Burst(transform.position, Portals.Blue, 20, 4f, 0.05f, 1.5f);
            Mix.Play(Mix.Sound("cake_chime.wav"), transform.position, 1f);
            Current = null;
            Destroy(gameObject);
            OnEaten?.Invoke();
        }
    }
}

/// <summary>A floating white personality core with a glowing blue eye: it hovers by the bird and watches it.</summary>
public class Core : MonoBehaviour
{
    public static Core Inst;
    public bool talking;
    Transform eye; Light lamp; Vector3 vel;
    public static void Spawn()
    {
        var root = new GameObject("SigfCore");
        var t = root.transform;
        Build.Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.27f, new Color(0.96f, 0.97f, 1f));
        Build.Part(t, PrimitiveType.Sphere, new Vector3(0, 0, 0.14f), new Vector3(0.2f, 0.2f, 0.07f), new Color(0.05f, 0.06f, 0.08f));
        var e = Build.Part(t, PrimitiveType.Sphere, new Vector3(0, 0, 0.17f), new Vector3(0.12f, 0.12f, 0.05f), new Color(0.3f, 0.7f, 1f), 3.5f);
        Build.Part(t, PrimitiveType.Cube, new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.04f, 0.06f), new Color(0.2f, 0.22f, 0.25f));
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(-0.17f, 0.02f, 0), new Vector3(0.05f, 0.03f, 0.05f), new Color(0.2f, 0.22f, 0.25f), 0f, new Vector3(0, 0, 90));
        Build.Part(t, PrimitiveType.Cylinder, new Vector3(0.17f, 0.02f, 0), new Vector3(0.05f, 0.03f, 0.05f), new Color(0.2f, 0.22f, 0.25f), 0f, new Vector3(0, 0, 90));
        var c = root.AddComponent<Core>();
        c.eye = e.transform;
        c.lamp = Mix.Glow(t.position, new Color(0.3f, 0.7f, 1f), 3f, 1.5f, t);
        Inst = c;
        t.position = G.Pos + Vector3.up * 1.2f;
    }
    void Update()
    {
        var cam = G.Cam;
        if (cam == null) return;
        var target = G.Pos + cam.transform.right * -1.0f + cam.transform.forward * 0.6f + Vector3.up * (1.0f + 0.06f * Mathf.Sin(Time.unscaledTime * 2.2f));
        transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, 0.25f, 12f, Time.unscaledDeltaTime);
        var look = (G.Pos + Vector3.up * 0.3f) - transform.position;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.unscaledDeltaTime * 6f);
        float k = talking ? 1f + 0.35f * Mathf.Sin(Time.unscaledTime * 22f) : 1f;
        eye.localScale = new Vector3(0.12f, 0.12f, 0.05f) * k;
        lamp.intensity = talking ? 2.5f + Mathf.Sin(Time.unscaledTime * 22f) : 1.2f;
    }
}
