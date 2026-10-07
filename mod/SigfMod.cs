// Portal 2 Takeover: SkateBIRD's playground becomes a white test chamber run by a snarky AI.
//  - the whole level is retextured with white chamber panels;
//  - Q / E fire a blue / orange portal from the bird; anything that rolls in one comes out of the other, speed kept;
//  - a companion cube you can shove through portals, sentry turrets you can smash with the board or the cube;
//  - three tests (portal, turrets, cake) narrated by a voice and a floating core.
using System.Collections;
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    static Vector3 S, F, R, L;                 // start spot on the floor, forward, right (flat)
    static Vector3 w1Face, w2Face;          // portal wall faces: blue wall ahead, orange wall to the left
    static Vector3 w1N, w2N;
    static bool built, started;
    static bool portalDone, cakeOut, cakeDone;
    static int downs, total;
    static int round;
    static GameObject cube;
    static float lastFire;

    public override void OnLoad() => G.StartLevel = "playground";

    public override void OnReady()
    {
        Layout();
        Restyle();
        Core.Spawn();
        Portals.OnBoardPass += blue => { if (!portalDone) { portalDone = true; Voice.Queue("glados2.wav", "AI: Well done. Fast bird goes in, fast bird comes out."); } };
        Turret.OnDown += t =>
        {
            downs++;
            if (downs >= total && !cakeOut) { cakeOut = true; Voice.Queue("glados3.wav", "AI: Oh. You broke every turret. Fine. There is cake."); Mix.After(1.5f, () => SpawnCake()); }
        };
        Cake.OnEaten += () =>
        {
            cakeDone = true;
            Voice.Queue("glados4.wav", "AI: Here is your cake. It is a lie.");
            Mix.Say("THE CAKE IS A LIE", 4f, Gold, 0.3f, 80);
            Mix.After(14f, () => { NewRound(false); Mix.After(2f, AutoPortals); });
        };
        G.OnTrick(t => TrickFx());
        Mix.Every(1f, Hud, "Hud");
        if (!Mix.DemoMode)
        {
            NewRound(true);
            Mix.After(4f, () => { if (Time.unscaledTime - lastFire > 3f && Portals.A == null) AutoPortals(); });
        }
    }

    // ---------- the chamber ----------
    static void Layout()
    {
        S = G.Pos; S.y = Portals.FloorY(S);
        // the way the camera looks at the start is the way the bird rides
        F = G.Cam != null ? G.Cam.transform.forward : G.Forward; F.y = 0f;
        F = F.sqrMagnitude < 1e-3f ? G.Forward : F.normalized;
        R = Vector3.Cross(Vector3.up, F).normalized;
        // the exit lane goes on whichever side has the most free floor
        var o = S + F * 2f + Vector3.up * 0.5f;
        float right = Physics.Raycast(o, R, out var hr, 30f, ~0, QueryTriggerInteraction.Ignore) ? hr.distance : 30f;
        float left = Physics.Raycast(o, -R, out var hl, 30f, ~0, QueryTriggerInteraction.Ignore) ? hl.distance : 30f;
        L = left >= right ? -R : R;
        w1N = -F; w2N = -L;
        w1Face = Build.Slab(S + F * 9f, w1N, 6f, 3.2f, 0.6f);
        w2Face = Build.Slab(S + L * 8f + F * 2f, w2N, 6f, 3.2f, 0.6f);
        built = true;
        Mix.Log("layout S " + S + " F " + F + " L " + L + " free " + left + "/" + right + " w1 " + w1Face + " w2 " + w2Face);
    }

    static void Restyle()
    {
        var tex = Build.Panel();
        var bird = G.BoardObject.transform.root;
        int n = 0;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (r.transform.IsChildOf(bird) || r.name.StartsWith("tree") || r.bounds.size.magnitude > 200f || r.GetComponentInParent<Prop>() != null || r.name.StartsWith("Sigf")) continue;
            var ms = r.materials;
            foreach (var m in ms)
            {
                m.mainTexture = tex; m.color = new Color(0.78f, 0.83f, 0.92f);
                var s = r.bounds.size; float k = Mathf.Max(s.x, s.z, 0.5f) / 3f;
                m.mainTextureScale = new Vector2(k, Mathf.Max(s.y, 0.5f) > 1f ? Mathf.Max(s.y, s.z) / 3f : k);
            }
            r.materials = ms;
            n++;
        }
        Mix.Log("restyled " + n + " renderers");
    }

    static void NewRound(bool intro)
    {
        round++;
        Portals.CloseAll();
        foreach (var t in new List<Turret>(Turret.All)) if (t != null) Object.Destroy(t.gameObject);
        if (cube != null) Object.Destroy(cube);
        foreach (var c in Object.FindObjectsOfType<Cake>()) Object.Destroy(c.gameObject);
        Portals.Movers.Clear();
        Gels.Clear();
        { var g1 = S + F * 2.4f; g1.y = Portals.FloorY(g1); Gels.Add(false, g1); }
        { var c = S + F * 6f - L * 3f; var e = S + F * 2f; var d = (c - e); d.y = 0; var g2 = c - d.normalized * 2.6f; g2.y = Portals.FloorY(g2); Gels.Add(true, g2); }
        portalDone = cakeOut = cakeDone = false; downs = 0; Turret.VoiceCool = 0f;
        float[] lat = { 5.8f, 3.9f, 2.0f, -3.0f };
        float[] fwd = { 2.05f, 1.95f, 2.0f, 2.6f };
        total = lat.Length;
        for (int i = 0; i < lat.Length; i++)
        {
            var p = S + L * lat[i] + F * fwd[i];
            p.y = Portals.FloorY(p) + 0.05f;
            Turret.Spawn(p);
        }
        var cp = S + F * 3.6f + R * 0.0f; cp.y = Portals.FloorY(cp) + 0.3f;
        cube = Cube.Spawn(cp);
        if (intro)
        {
            Mix.Say("PORTAL 2 TAKEOVER", 4f, Color.white, 0.17f, 72);
            Voice.Queue("glados1.wav", "AI: Welcome to the Bird Skating Enrichment Center. Please try not to fall off.");
        }
    }

    static void SpawnCake() { var p = S + F * 6f - L * 3f; p.y = Portals.FloorY(p); Cake.Spawn(p); }

    static void AutoPortals()
    {
        lastFire = Time.unscaledTime;
        FireAt(true, w1Face + w1N * 0.01f + Vector3.up * 0.85f);
        Mix.After(0.6f, () => FireAt(false, w2Face + w2N * 0.01f + Vector3.up * 0.85f));
    }

    static void FireAt(bool blue, Vector3 target)
    {
        var o = G.Pos + Vector3.up * 0.5f + G.Forward * 0.3f;
        Portals.Fire(blue, o, (target - o).normalized);
    }

    static void TrickFx()
    {
        var p = G.Pos + Vector3.up * 0.4f;
        Mix.Burst(p, Portals.Blue, 14, 3f, 0.05f, 1.2f);
        Mix.Burst(p, Portals.Orange, 14, 3f, 0.05f, 1.2f);
        var l = Mix.Glow(p, Portals.Blue, 3f, 3f); Object.Destroy(l.gameObject, 0.3f);
    }

    static void Hud()
    {
        string t;
        if (!portalDone) t = "TEST 1: Fire portals (Q blue, E orange) and ride through one";
        else if (!cakeOut) t = "TEST 2: Smash the turrets  " + downs + "/" + total;
        else if (!cakeDone) t = "TEST 3: Grab the cake!";
        else t = "TESTING COMPLETE. New chamber soon...";
        Mix.Say(t, 1.6f, Gold, 0.07f, 32);
    }

    public override void OnUpdate()
    {
        if (!built) return;
        Portals.Update();
        Gels.Update();
        try
        {
            if (Input.GetKeyDown(KeyCode.Q)) PlayerFire(true);
            if (Input.GetKeyDown(KeyCode.E)) PlayerFire(false);
            if (Input.GetKeyDown(KeyCode.R) && cube != null) cube.transform.position = G.Pos + G.Forward * 1f + Vector3.up * 0.5f;
        }
        catch (System.InvalidOperationException) { }
    }

    static void PlayerFire(bool blue)
    {
        lastFire = Time.unscaledTime;
        var cam = G.Cam;
        var d = cam != null ? cam.transform.forward : G.Forward;
        d.y = Mathf.Max(d.y, -0.05f);
        Portals.Fire(blue, G.Pos + Vector3.up * 0.5f + d.normalized * 0.3f, d.normalized);
    }

    // ---------- the clip ----------
    public override IEnumerator Demo()
    {
        started = true;
        G.GetUp();
        NewRound(true);
        G.Teleport(S + Vector3.up * 0.25f, Quaternion.LookRotation(F, Vector3.up));
        G.SetVelocity(Vector3.zero);
        yield return Mix.Wait(1.8f);
        // 1. the portal gun
        FireAt(true, w1Face + w1N * 0.01f + Vector3.up * 0.85f);
        yield return Mix.Wait(0.9f);
        FireAt(false, w2Face + w2N * 0.01f + Vector3.up * 0.85f);
        yield return Mix.Wait(1.6f);
        // 2. the companion cube is kicked through the blue portal and comes out of the orange one, into the turrets
        if (cube != null)
        {
            var rb = cube.GetComponent<Rigidbody>();
            rb.velocity = F * 9f + Vector3.up * 0.5f;
            rb.angularVelocity = R * 8f;
            Mix.Say("KICK THE CUBE THROUGH THE PORTAL", 2.5f, Color.white, 0.3f, 40);
            G.Screm();
        }
        yield return Mix.Wait(4.5f);
        // 3. the bird itself: ride in the blue portal, fly out of the orange one at full speed
        Mix.Say("NOW THE BIRD", 2f, Color.white, 0.3f, 44);
        float t0 = Time.unscaledTime;
        G.Teleport(S - F * 1f + Vector3.up * 0.25f, Quaternion.LookRotation(F, Vector3.up));
        G.SetVelocity(Vector3.zero);
        yield return Mix.Wait(0.3f);
        while (!portalDone && Time.unscaledTime - t0 < 9f)
        {
            if (G.Bailed) G.GetUp();
            if (G.Speed < 5f) G.Boost(1.6f);
            yield return Mix.Wait(0.1f);
        }
        // keep the speed up through the turret lane
        t0 = Time.unscaledTime;
        while (downs < total && Time.unscaledTime - t0 < 14f)
        {
            string st = ""; foreach (var t in Turret.All) if (t != null) st += (t.dead ? "x" : "T") + t.transform.position.ToString("F1") + " ";
            Mix.Log("lane bird " + G.Pos.ToString("F1") + " sp " + G.Speed.ToString("F1") + " " + st);
            if (G.Bailed) G.GetUp();
            Steer();
            yield return Mix.Wait(0.15f);
        }
        yield return Mix.Wait(1.0f);
        // 4. cake
        t0 = Time.unscaledTime;
        while (!cakeDone && Time.unscaledTime - t0 < 12f)
        {
            if (G.Bailed) G.GetUp();
            if (Cake.Current != null)
            {
                var to = Cake.Current.transform.position - G.Pos; to.y = 0;
                if (to.magnitude > 0.5f) { G.Teleport(G.Pos, Quaternion.LookRotation(to.normalized, Vector3.up)); G.SetVelocity(to.normalized * 4f); }
            }
            yield return Mix.Wait(0.25f);
        }
        // victory laps: tricks over the gel and the portals until the clip ends
        for (int i = 0; i < 12; i++)
        {
            if (G.Bailed) G.GetUp();
            var dir = i % 2 == 0 ? -L : L;
            G.Teleport(S + F * 4.5f + L * (i % 2 == 0 ? 2.5f : -2.5f) + Vector3.up * 0.25f, Quaternion.LookRotation(dir, Vector3.up));
            G.SetVelocity(dir * 4.5f);
            yield return Mix.Wait(0.6f);
            G.Launch(3.4f);
            yield return Mix.Wait(0.25f);
            G.Flip(i % 2 == 0 ? "Kickflip" : "Heelflip");
            if (i % 3 == 0) G.Screm();
            yield return Mix.Wait(2.0f);
        }
    }

    static void Steer()
    {
        // push toward the nearest standing turret, else along the lane
        Turret best = null; float bd = 99f;
        foreach (var t in Turret.All) if (t != null && !t.dead) { float d = Vector3.Distance(t.transform.position, G.Pos); if (d < bd) { bd = d; best = t; } }
        if (best == null) return;
        var to = best.transform.position - G.Pos; to.y = 0;
        if (G.Speed > 5f && Vector3.Angle(G.Velocity, to) < 12f) return;   // already on its way
        var v = to.normalized * 5.5f; v.y = G.Velocity.y;
        G.Teleport(G.Pos, Quaternion.LookRotation(to.normalized, Vector3.up));
        G.SetVelocity(v);
    }
}

/// <summary>The narrator: plays voice lines one after another and shows their caption.</summary>
public static class Voice
{
    static readonly Queue<string[]> q = new Queue<string[]>();
    static bool running;
    public static void Queue(string clip, string caption)
    {
        q.Enqueue(new[] { clip, caption });
        if (!running) { running = true; Mix.Run(Pump(), "Voice"); }
    }
    static IEnumerator Pump()
    {
        while (q.Count > 0)
        {
            var l = q.Dequeue();
            var c = Mix.Sound(l[0]);
            Mix.Play(c, null, 1f);
            Mix.Say(l[1], c.length + 0.4f, new Color(1f, 0.95f, 0.7f), 0.86f, 34);
            if (Core.Inst != null) Core.Inst.talking = true;
            yield return Mix.Wait(c.length + 0.5f);
            if (Core.Inst != null) Core.Inst.talking = false;
        }
        running = false;
    }
}
