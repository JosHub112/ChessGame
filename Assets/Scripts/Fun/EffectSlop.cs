using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// EffectSlop - 69 toggleable juice/slop effects for the chess game.
///
/// SETUP: just drop this file in your project. It spawns itself (and survives scene restarts).
///        You can also put it on any GameObject manually. No prefabs, sprites or audio files needed.
///        Press F1 in play mode to open the panel and toggle every effect individually.
///
/// It is a pure OBSERVER: it never edits Chessman / Game / MovePlate. It watches the board
/// (piece coordinates, MovePlates, current player, check, game over) and reacts.
/// </summary>
public class EffectSlop : MonoBehaviour
{
    // =====================================================================
    //  THE 69 EFFECTS
    // =====================================================================
    public enum Fx
    {
        // Selection & interaction (13)
        SelectPop, SelectRing, SelectSparkles, SelectLevitate, SelectSound,
        PlatePopIn, PlatePulse, AttackPlateFlicker, PlateSparkle,
        HoverLift, HoverPlateGlow, CursorTrail, ClickRipple,

        // Movement (17)
        MoveHopArc, MoveGhostTrail, MoveDustPuff, LandDustRing, LandSquash, LandShockwave, LandMicroShake,
        MoveWhooshSound, LandThudSound, MoveStreakLine, FromSquareFlash, ToSquareFlash,
        KnightSpin, PawnWiggle, LastMoveHighlight, TurnVignetteTint, TurnBanner,

        // Captures (15)
        CaptureBurst, CaptureShockwave, CaptureShake, CaptureHitStop, CaptureFlash, CaptureText, CaptureSlash,
        CaptureZoomPunch, VictimGhost, VictimShards, CaptureCrunchSound, CaptureSpeedLines, CaptureCombo,
        CaptureCameraRoll, CaptureValuePopup,

        // Check / game end (15)
        CheckVignette, CheckText, CheckKingJitter, CheckDangerRing, CheckAlarmSound,
        MateExplosion, MateSlowMo, MateConfetti, MateCameraZoom, MateLetterbox, MateFireworks,
        LoserKingCollapse, WinnerAura, MateFanfareSound, StalemateGrayOut,

        // Intro & ambient (9)
        IntroDrop, IntroSound, DustMotes, BackgroundHueShift, IdleBreathing, BoardTwinkle,
        CameraSway, StaticVignette, PieceShadows
    }

    // =====================================================================
    //  SETTINGS
    // =====================================================================
    [Header("Global")]
    public bool masterEnabled = true;
    [Range(0f, 2f)] public float intensity = 1f;
    public bool soundEnabled = true;
    [Range(0f, 1f)] public float volume = 0.6f;
    public Key panelKey = Key.F1;

    // Board <-> world (matches Chessman.SetCoords)
    const float Sq = 0.66f, Org = -2.3f;
    static Vector3 B2W(int x, int y, float z = -1f) { return new Vector3(x * Sq + Org, y * Sq + Org, z); }

    // =====================================================================
    //  STATE
    // =====================================================================
    bool[] on;
    string[] fxNames;
    bool On(Fx f) { return masterEnabled && on[(int)f]; }

    Game game; Camera cam;
    float nextFind;

    class Track
    {
        public int id; public Chessman cm; public Transform tf; public SpriteRenderer sr;
        public int x, y; public string player, type; public Sprite sprite;
        public Vector3 baseScale; public Color baseColor; public float phase;
        public Vector3 lastPos, lastScale; public Quaternion lastRot; public Color lastColor = Color.white;
        public float introClock; public bool introDone;
        public float moveT = 1f, moveDur = 0.3f, arc; public Vector3 moveFrom; public float ghostAcc;
        public float squash, pop, wiggle, spinT = 1f, hover, hoverGoal, selAmt, checkAmt, collapseT = -1f;
        public GameObject shadow; public SpriteRenderer shadowSr;
    }
    class Plate
    {
        public int id; public Transform tf; public SpriteRenderer sr; public bool attack;
        public Vector3 baseScale; public Color baseColor; public float born, phase, hover, hoverGoal;
    }
    class MoveRec { public Track t; public int fx, fy, tx, ty; }

    readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
    readonly Dictionary<int, Plate> plates = new Dictionary<int, Plate>();
    readonly HashSet<int> seenIds = new HashSet<int>();
    readonly List<Track> vanished = new List<Track>();
    readonly List<MoveRec> moves = new List<MoveRec>();
    int selectedId;

    string lastPlayer; bool checkActive, gameOverHandled;
    int baseOrder, baseLayer; bool layerSet;
    float introUntil; bool introSoundPlayed;
    int whiteStreak, blackStreak;

    // camera
    Vector3 camBasePos; Quaternion camBaseRot; float camBaseSize, camBaseFov; Color camBaseBg;
    float shake, zoom, roll, mateZoom, mateZoomGoal; Vector3 mateFocus;
    Vector3 mouseWorld, lastMouseWorld; Vector2 mouseNorm;

    // time control
    float hitStopUntil, slowmo = 1f, slowmoGoal = 1f, slowmoResetAt; bool timeDirty;

    // overlays
    float flashA; Color flashC = Color.white;
    float turnTint; Color turnTintC = Color.white;
    float letterbox, letterboxGoal, gray, grayGoal;
    bool showPanel; Vector2 scroll; float startTime;

    // continuous game-over effects
    bool confettiOn, fireworksOn; Track auraTrack;
    float dustAcc, twinkleAcc, trailAcc, confettiAcc, fwAcc, auraAcc, ringAcc;

    // last move highlight
    bool hasLast; int lfx, lfy, ltx, lty; GameObject hlFrom, hlTo; SpriteRenderer hlFromSr, hlToSr;

    // =====================================================================
    //  BOOTSTRAP
    // =====================================================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirst<EffectSlop>() != null) return;
        GameObject go = new GameObject("EffectSlop");
        go.AddComponent<EffectSlop>();
        DontDestroyOnLoad(go); // survives the SceneManager.LoadScene restart in Game.cs
    }

    static T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_3_OR_NEWER
        return FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return FindObjectsOfType<T>();
#endif
    }
    static T FindFirst<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_3_OR_NEWER
        return FindFirstObjectByType<T>();
#else
        return FindObjectOfType<T>();
#endif
    }

    void Awake()
    {
        int n = Enum.GetValues(typeof(Fx)).Length;
        on = new bool[n]; fxNames = new string[n];
        for (int i = 0; i < n; i++) { on[i] = true; fxNames[i] = Nice(((Fx)i).ToString()); }

        fxRoot = new GameObject("EffectSlop_FX").transform;
        fxRoot.SetParent(transform, false);

        BuildSprites();
        BuildAudio();
        srcs = new AudioSource[8];
        for (int i = 0; i < srcs.Length; i++)
        {
            srcs[i] = gameObject.AddComponent<AudioSource>();
            srcs[i].playOnAwake = false; srcs[i].spatialBlend = 0f;
        }
        startTime = Time.unscaledTime;
        ResetState();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Time.timeScale = 1f;
        RestoreCamera();
    }
    void OnSceneLoaded(Scene s, LoadSceneMode m) { ResetState(); }

    void ResetState()
    {
        foreach (Track t in tracks.Values) if (t.shadow != null) Destroy(t.shadow);
        tracks.Clear(); plates.Clear(); selectedId = 0;
        for (int i = 0; i < live.Count; i++) { live[i].tf.gameObject.SetActive(false); pool.Push(live[i]); }
        live.Clear(); texts.Clear(); jobs.Clear();
        if (hlFrom != null) Destroy(hlFrom);
        if (hlTo != null) Destroy(hlTo);
        hasLast = false;

        game = null; cam = null;
        lastPlayer = null; checkActive = false; gameOverHandled = false;
        shake = zoom = roll = mateZoom = mateZoomGoal = 0f;
        flashA = turnTint = letterbox = letterboxGoal = gray = grayGoal = 0f;
        slowmo = slowmoGoal = 1f; hitStopUntil = 0f;
        confettiOn = fireworksOn = false; auraTrack = null;
        whiteStreak = blackStreak = 0;
        introUntil = Time.time + 0.8f; introSoundPlayed = false;
        Time.timeScale = 1f; timeDirty = false;
    }

    void TryFind()
    {
        if (Time.unscaledTime < nextFind) return;
        nextFind = Time.unscaledTime + 0.25f;
        game = FindFirst<Game>();
        cam = Camera.main;
        if (game != null && cam != null)
        {
            camBasePos = cam.transform.position; camBaseRot = cam.transform.rotation;
            camBaseSize = cam.orthographicSize; camBaseFov = cam.fieldOfView; camBaseBg = cam.backgroundColor;
        }
        else { game = null; cam = null; }
    }

    void RestoreCamera()
    {
        if (cam == null) return;
        cam.transform.position = camBasePos; cam.transform.rotation = camBaseRot;
        cam.orthographicSize = camBaseSize; cam.fieldOfView = camBaseFov; cam.backgroundColor = camBaseBg;
    }

    // =====================================================================
    //  MAIN LOOP
    // =====================================================================
    void Update()
    {
        float dt = Time.deltaTime, rdt = Time.unscaledDeltaTime;

        if (Keyboard.current != null && Keyboard.current[panelKey].wasPressedThisFrame) showPanel = !showPanel;

        UpdateTime(rdt);
        if (game == null || cam == null) { TryFind(); UpdateParticles(dt); return; }

        PollPieces();
        PollPlates();
        PollTurnAndState();
        PollInput();
        UpdateTracks(dt);
        UpdateAmbient(dt);
        RunJobs();
        UpdateParticles(dt);
        UpdateOverlays(rdt);
    }

    void LateUpdate()
    {
        if (game == null || cam == null) return;
        foreach (Track t in tracks.Values) ApplyPose(t);
        ApplyPlates();
        ApplyHighlights();
        ApplyCamera(Time.unscaledDeltaTime);
    }

    // ---------------------------------------------------------------------
    //  Time control (hit-stop + slow-mo)
    // ---------------------------------------------------------------------
    void UpdateTime(float rdt)
    {
        if (slowmoGoal < 1f && Time.unscaledTime > slowmoResetAt) slowmoGoal = 1f;
        slowmo = Mathf.MoveTowards(slowmo, slowmoGoal, rdt * 3f);

        bool stopping = Time.unscaledTime < hitStopUntil;
        if (stopping || slowmo < 0.999f)
        {
            Time.timeScale = stopping ? 0.02f : Mathf.Max(0.05f, slowmo);
            timeDirty = true;
        }
        else if (timeDirty) { Time.timeScale = 1f; timeDirty = false; }
    }

    // =====================================================================
    //  POLLING THE GAME
    // =====================================================================
    void PollPieces()
    {
        Chessman[] all = FindAll<Chessman>();
        seenIds.Clear(); moves.Clear(); vanished.Clear();

        foreach (Chessman cm in all)
        {
            string pl = cm.GetPlayer();
            if (pl == null) continue; // Activate() not called yet
            int id = cm.gameObject.GetInstanceID();
            seenIds.Add(id);

            Track t;
            if (!tracks.TryGetValue(id, out t)) { tracks[id] = NewTrack(cm, id); continue; }

            int cx = cm.GetXBoard(), cy = cm.GetYBoard();
            if (cx != t.x || cy != t.y)
            {
                moves.Add(new MoveRec { t = t, fx = t.x, fy = t.y, tx = cx, ty = cy });
                t.x = cx; t.y = cy;
            }
        }

        foreach (KeyValuePair<int, Track> kv in tracks) if (!seenIds.Contains(kv.Key)) vanished.Add(kv.Value);
        foreach (Track v in vanished) { tracks.Remove(v.id); if (v.shadow != null) Destroy(v.shadow); }

        foreach (MoveRec m in moves)
        {
            Track victim = null;
            foreach (Track v in vanished) if (v.x == m.tx && v.y == m.ty) { victim = v; break; }
            OnMoved(m.t, m.fx, m.fy, m.tx, m.ty, victim);
        }
    }

    Track NewTrack(Chessman cm, int id)
    {
        Track t = new Track();
        t.id = id; t.cm = cm; t.tf = cm.transform; t.sr = cm.GetComponent<SpriteRenderer>();
        t.x = cm.GetXBoard(); t.y = cm.GetYBoard(); t.player = cm.GetPlayer();
        string n = cm.name; int us = n.IndexOf('_');
        t.type = us >= 0 ? n.Substring(us + 1) : n;
        t.baseScale = t.tf.localScale;
        t.baseColor = t.sr != null ? t.sr.color : Color.white;
        t.sprite = t.sr != null ? t.sr.sprite : null;
        t.phase = Random.value * 6.2832f;

        if (t.sr != null && !layerSet) { baseOrder = t.sr.sortingOrder; baseLayer = t.sr.sortingLayerID; layerSet = true; }

        if (Time.time < introUntil && On(Fx.IntroDrop))
        {
            float row = t.y < 4 ? t.y : 7 - t.y;
            t.introClock = -(0.05f + t.x * 0.045f + row * 0.03f + Random.value * 0.05f);
            if (!introSoundPlayed && On(Fx.IntroSound)) { introSoundPlayed = true; Play(cIntro, 0.7f); }
        }
        else t.introDone = true;
        return t;
    }

    void PollPlates()
    {
        MovePlate[] all = FindAll<MovePlate>();
        seenIds.Clear();
        int newCount = 0; GameObject refObj = null;

        foreach (MovePlate mp in all)
        {
            int id = mp.gameObject.GetInstanceID();
            seenIds.Add(id);
            refObj = mp.GetReference();
            if (plates.ContainsKey(id)) continue;

            Plate pl = new Plate();
            pl.id = id; pl.tf = mp.transform; pl.sr = mp.GetComponent<SpriteRenderer>();
            pl.attack = mp.attack; pl.baseScale = pl.tf.localScale;
            pl.baseColor = mp.attack ? new Color(1f, 0f, 0f, 1f) : (pl.sr != null ? pl.sr.color : Color.white);
            pl.phase = Random.value * 6.2832f;
            pl.born = Time.time + Mathf.Min(newCount * 0.025f, 0.25f);
            plates[id] = pl; newCount++;

            if (On(Fx.PlateSparkle))
                Burst(pl.tf.position, 3, mp.attack ? new Color(1f, 0.4f, 0.1f, 1f) : new Color(0.6f, 1f, 1f, 1f), 0.45f, 0.06f, 0.5f, 0f, starSpr);
        }

        List<int> dead = null;
        foreach (int id in plates.Keys) if (!seenIds.Contains(id)) { if (dead == null) dead = new List<int>(); dead.Add(id); }
        if (dead != null) foreach (int id in dead) plates.Remove(id);

        if (plates.Count == 0) { selectedId = 0; return; }
        if (newCount > 0 && refObj != null)
        {
            selectedId = refObj.GetInstanceID();
            Track t;
            if (tracks.TryGetValue(selectedId, out t)) OnSelected(t);
        }
    }

    void PollTurnAndState()
    {
        string cur = game.GetCurrentPlayer();
        bool over = game.IsGameOver();

        if (cur != lastPlayer)
        {
            bool first = lastPlayer == null;
            lastPlayer = cur; checkActive = false;
            OnTurn(cur, first, over);
            if (!first && !over && game.IsInCheck(cur)) { checkActive = true; OnCheck(cur); }
        }
        if (over && !gameOverHandled) { gameOverHandled = true; OnGameOver(); }
    }

    void PollInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 sp = mouse.position.ReadValue();
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
        wp.z = -2f;
        lastMouseWorld = mouseWorld; mouseWorld = wp;
        mouseNorm = new Vector2(Mathf.Clamp((sp.x / Screen.width - 0.5f) * 2f, -1f, 1f), Mathf.Clamp((sp.y / Screen.height - 0.5f) * 2f, -1f, 1f));

        foreach (Track t in tracks.Values) t.hoverGoal = 0f;
        foreach (Plate p in plates.Values) p.hoverGoal = 0f;

        Collider2D col = Physics2D.OverlapPoint(new Vector2(wp.x, wp.y));
        if (col != null && !gameOverHandled)
        {
            int id = col.gameObject.GetInstanceID();
            Plate pl; Track tr;
            if (plates.TryGetValue(id, out pl)) pl.hoverGoal = 1f;
            else if (tracks.TryGetValue(id, out tr)) tr.hoverGoal = 1f;
        }

        if (mouse.leftButton.wasPressedThisFrame && On(Fx.ClickRipple))
        {
            Ring(wp, new Color(1f, 1f, 1f, 0.8f), 0.4f, 0.4f);
            Burst(wp, 5, new Color(1f, 0.9f, 0.5f, 1f), 0.8f, 0.05f, 0.4f, 0f, starSpr);
        }

        if (On(Fx.CursorTrail) && (mouseWorld - lastMouseWorld).sqrMagnitude > 0.0003f)
        {
            trailAcc += Time.unscaledDeltaTime;
            if (trailAcc > 0.025f)
            {
                trailAcc = 0f;
                Color c = Color.HSVToRGB((Time.time * 0.6f) % 1f, 0.6f, 1f); c.a = 0.55f;
                Emit(softSpr, wp, c, new Color(c.r, c.g, c.b, 0f), Vector2.one * 0.16f, Vector2.zero, 0.5f, Random.insideUnitCircle * 0.15f, 0f, 1f, 0f, 0f, 30);
            }
        }
    }

    // =====================================================================
    //  EVENT HANDLERS
    // =====================================================================
    static readonly Color Dust = new Color(0.85f, 0.75f, 0.6f, 0.7f);

    void OnSelected(Track t)
    {
        Vector3 p = t.tf.position;
        if (On(Fx.SelectPop)) t.pop = 1f;
        if (On(Fx.SelectRing)) Ring(p, new Color(0.4f, 0.9f, 1f, 0.9f), 0.55f, 0.45f);
        if (On(Fx.SelectSparkles)) Burst(p, 10, new Color(1f, 0.9f, 0.4f, 1f), 1.2f, 0.07f, 0.6f, 1f, starSpr);
        if (On(Fx.SelectSound)) Play(cSelect, 0.6f, Random.Range(0.95f, 1.15f));
    }

    void OnIntroLand(Track t)
    {
        Burst(t.tf.position + Vector3.down * 0.2f, 4, Dust, 0.5f, 0.1f, 0.4f, -0.1f, softSpr);
        if (On(Fx.IntroSound) && (t.type == "king" || t.type == "queen")) Play(cThud, 0.4f, 1.2f);
    }

    void OnTurn(string cur, bool first, bool over)
    {
        if (On(Fx.TurnVignetteTint))
        {
            turnTint = 1f;
            turnTintC = cur == "white" ? new Color(0.85f, 0.92f, 1f) : new Color(0.55f, 0.2f, 0.85f);
        }
        if (On(Fx.TurnBanner) && !over)
        {
            string s = first ? "WHITE TO MOVE" : cur.ToUpper() + "'S TURN";
            Color c = cur == "white" ? new Color(0.9f, 0.95f, 1f) : new Color(0.75f, 0.5f, 1f);
            AddScreenText(s, new Vector2(0.5f, 0.07f), c, 0.055f, 1.1f);
        }
    }

    void OnMoved(Track t, int fx, int fy, int tx, int ty, Track victim)
    {
        Vector3 from = B2W(fx, fy), to = B2W(tx, ty);
        float dist = Vector3.Distance(from, to);
        float delay = 0f;
        bool capture = victim != null;

        int streak;
        if (t.player == "white") { whiteStreak = capture ? whiteStreak + 1 : 0; streak = whiteStreak; }
        else { blackStreak = capture ? blackStreak + 1 : 0; streak = blackStreak; }

        if (On(Fx.MoveHopArc))
        {
            t.moveFrom = from; t.moveT = 0f;
            t.moveDur = Mathf.Clamp(0.16f + dist * 0.06f, 0.2f, 0.5f);
            t.arc = Mathf.Clamp(0.12f + dist * 0.05f, 0.14f, 0.45f) * (t.type == "knight" ? 1.5f : 1f);
            delay = t.moveDur;
        }
        else t.moveT = 1f;

        if (On(Fx.KnightSpin) && t.type == "knight") t.spinT = 0f;
        if (On(Fx.PawnWiggle) && t.type == "pawn") t.wiggle = 1f;
        if (On(Fx.MoveDustPuff)) Burst(from + Vector3.down * 0.15f, 7, Dust, 0.8f, 0.14f, 0.5f, -0.2f, softSpr);

        if (On(Fx.MoveStreakLine))
        {
            Vector3 d = to - from;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Emit(squareSpr, (from + to) * 0.5f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f),
                 new Vector2(dist, 0.06f), new Vector2(dist, 0f), 0.35f, Vector2.zero, 0f, 0f, 0f, ang, 5);
        }
        if (On(Fx.FromSquareFlash)) SquareFlash(fx, fy, new Color(0.3f, 0.7f, 1f, 0.55f));
        if (On(Fx.ToSquareFlash)) SquareFlash(tx, ty, new Color(1f, 0.85f, 0.3f, 0.65f));
        if (On(Fx.MoveWhooshSound)) Play(cWhoosh, 0.6f, Mathf.Clamp(1.4f - dist * 0.1f, 0.7f, 1.4f));

        if (On(Fx.LastMoveHighlight)) { hasLast = true; lfx = fx; lfy = fy; ltx = tx; lty = ty; }

        // keep the victim visible on its square until the attacker actually lands
        if (capture && delay > 0f && victim.sprite != null)
            Emit(victim.sprite, victim.lastPos, victim.lastColor, victim.lastColor, V2(victim.lastScale), V2(victim.lastScale),
                 delay, Vector2.zero, 0f, 0f, 0f, victim.lastRot.eulerAngles.z, 0, -0.9f);

        After(delay, () => OnLanded(t, to, victim, streak));
    }

    void OnLanded(Track t, Vector3 to, Track victim, int streak)
    {
        if (On(Fx.LandSquash)) t.squash = 1f;
        if (On(Fx.LandDustRing)) { Ring(to + Vector3.down * 0.2f, Dust, 0.5f, 0.4f); Burst(to + Vector3.down * 0.2f, 6, Dust, 0.7f, 0.12f, 0.45f, -0.1f, softSpr); }
        if (On(Fx.LandShockwave)) Ring(to, new Color(1f, 1f, 1f, 0.5f), 0.95f, 0.5f);
        if (On(Fx.LandMicroShake) && victim == null) AddShake(0.025f);
        if (On(Fx.LandThudSound) && victim == null) Play(cThud, 0.7f, Random.Range(0.9f, 1.1f));
        if (victim != null) OnCaptured(t, victim, to, streak);
    }

    static readonly string[] CapWords = { "SLAIN!", "OBLITERATED!", "YEET!", "BONK!", "DELETED", "GG", "SLAY!", "RIP", "NOPE.", "DEMOLISHED" };

    void OnCaptured(Track mover, Track v, Vector3 w, int streak)
    {
        Color pc = v.player == "white" ? new Color(0.95f, 0.95f, 1f, 1f) : new Color(0.6f, 0.3f, 0.85f, 1f);

        if (On(Fx.CaptureBurst))
        {
            Burst(w, 24, pc, 3.2f, 0.16f, 0.9f, 3f);
            Burst(w, 16, new Color(1f, 0.55f, 0.15f, 1f), 4f, 0.1f, 0.7f, 2f, starSpr);
        }
        if (On(Fx.CaptureShockwave)) { Ring(w, new Color(1f, 0.25f, 0.15f, 0.9f), 1.3f, 0.5f); Ring(w, new Color(1f, 0.8f, 0.3f, 0.7f), 0.8f, 0.35f); }
        if (On(Fx.CaptureShake)) AddShake(0.12f);
        if (On(Fx.CaptureHitStop)) hitStopUntil = Time.unscaledTime + 0.07f * Mathf.Clamp(intensity, 0.3f, 1.5f);
        if (On(Fx.CaptureFlash)) Flash(0.35f, Color.white);
        if (On(Fx.CaptureText)) AddText(CapWords[Random.Range(0, CapWords.Length)], w + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.15f), 0.06f, 1.0f, 1f, 0.5f);

        if (On(Fx.CaptureSlash))
        {
            float ang = Random.Range(25f, 65f) * (Random.value < 0.5f ? 1f : -1f);
            Emit(squareSpr, w, Color.white, new Color(1f, 0.2f, 0.1f, 0f), new Vector2(2.1f, 0.09f), new Vector2(2.6f, 0f), 0.25f, Vector2.zero, 0f, 0f, 0f, ang, 15);
        }
        if (On(Fx.CaptureZoomPunch)) zoom = Mathf.Max(zoom, 0.04f * intensity);

        if (On(Fx.VictimGhost) && v.sprite != null)
            Emit(v.sprite, v.lastPos, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f), V2(v.lastScale), V2(v.lastScale) * 1.25f,
                 0.9f, new Vector2(0f, 0.9f), 0f, 0f, 0f, v.lastRot.eulerAngles.z, 8);

        if (On(Fx.VictimShards))
            for (int i = 0; i < 10; i++)
            {
                Vector2 vel = new Vector2(Random.Range(-2.2f, 2.2f), Random.Range(1f, 3.5f));
                Emit(squareSpr, w, pc, new Color(pc.r, pc.g, pc.b, 0f), Vector2.one * Random.Range(0.05f, 0.11f), Vector2.one * 0.02f,
                     0.9f, vel, 7f, 0.3f, Random.Range(-540f, 540f), Random.value * 360f, 12);
            }

        if (On(Fx.CaptureCrunchSound)) Play(cCrunch, 0.9f, Random.Range(0.9f, 1.1f));

        if (On(Fx.CaptureSpeedLines))
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * 6.2832f + Random.Range(-0.1f, 0.1f);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Emit(squareSpr, w + (Vector3)(dir * 0.35f), new Color(1f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0f),
                     new Vector2(0.7f, 0.03f), new Vector2(0.1f, 0.02f), 0.25f, dir * 3.5f, 0f, 0f, 0f, a * Mathf.Rad2Deg, 14);
            }

        if (On(Fx.CaptureCombo) && streak >= 2)
        {
            AddScreenText("x" + streak + " COMBO!", new Vector2(0.5f, 0.2f), new Color(1f, 0.85f, 0.2f), 0.08f, 1.3f);
            Play(cCombo, 0.7f, 1f + streak * 0.08f);
        }

        if (On(Fx.CaptureCameraRoll)) roll += Random.Range(1.8f, 3f) * (Random.value < 0.5f ? 1f : -1f) * intensity;

        if (On(Fx.CaptureValuePopup))
        {
            int val = 0;
            switch (v.type) { case "pawn": val = 1; break; case "knight": case "bishop": val = 3; break; case "rook": val = 5; break; case "queen": val = 9; break; }
            if (val > 0) AddText("+" + val, w + Vector3.up * 0.15f + Vector3.right * 0.35f, new Color(0.5f, 1f, 0.6f), 0.05f, 1.2f, 1.6f, 0f);
        }
    }

    void OnCheck(string cur)
    {
        Track k = FindKing(cur);
        Vector3 kp = k != null ? k.tf.position : Vector3.zero;
        if (On(Fx.CheckText)) AddText("CHECK!", kp + Vector3.up * 0.55f, new Color(1f, 0.2f, 0.2f), 0.075f, 1.5f, 0.5f, 1.2f);
        if (On(Fx.CheckAlarmSound)) Play(cAlarm, 0.7f);
    }

    void OnGameOver()
    {
        string loser = game.GetCurrentPlayer();
        string winner = loser == "white" ? "black" : "white";
        bool mate = game.IsInCheck(loser);
        checkActive = false;
        Track lk = FindKing(loser), wk = FindKing(winner);

        if (mate)
        {
            if (lk != null)
            {
                Vector3 kp = lk.tf.position;
                if (On(Fx.MateExplosion))
                {
                    Burst(kp, 70, new Color(1f, 0.55f, 0.1f, 1f), 4.5f, 0.2f, 1.3f, 2f);
                    Burst(kp, 40, new Color(1f, 0.9f, 0.3f, 1f), 3f, 0.12f, 1f, 2f, starSpr);
                    Burst(kp, 30, new Color(1f, 0.15f, 0.1f, 1f), 6f, 0.1f, 0.8f, 1f);
                    Ring(kp, new Color(1f, 0.6f, 0.2f, 0.9f), 3f, 0.8f);
                    Ring(kp, new Color(1f, 1f, 1f, 0.8f), 5f, 0.6f);
                    Flash(0.7f, Color.white); AddShake(0.3f);
                    AddScreenText("CHECKMATE!", new Vector2(0.5f, 0.32f), new Color(1f, 0.3f, 0.2f), 0.11f, 2.5f);
                }
                if (On(Fx.LoserKingCollapse)) lk.collapseT = 0f;
                if (On(Fx.MateCameraZoom)) { mateZoomGoal = 0.2f; mateFocus = kp; }
            }
            if (On(Fx.MateSlowMo)) { slowmoGoal = 0.3f; slowmoResetAt = Time.unscaledTime + 1.4f; }
            if (On(Fx.MateLetterbox)) letterboxGoal = 1f;
            if (On(Fx.MateConfetti)) confettiOn = true;
            if (On(Fx.MateFireworks)) fireworksOn = true;
            if (On(Fx.WinnerAura)) auraTrack = wk;
            if (On(Fx.MateFanfareSound)) Play(cFanfare, 0.8f);
        }
        else if (On(Fx.StalemateGrayOut))
        {
            grayGoal = 1f;
            Play(cSad, 0.8f);
        }
    }

    Track FindKing(string player)
    {
        foreach (Track t in tracks.Values) if (t.type == "king" && t.player == player) return t;
        return null;
    }

    // =====================================================================
    //  PER-FRAME ANIMATION STATE
    // =====================================================================
    void UpdateTracks(float dt)
    {
        foreach (Track t in tracks.Values)
        {
            if (t.tf == null) continue;

            if (!t.introDone)
            {
                t.introClock += dt;
                if (t.introClock >= 0.55f) { t.introDone = true; OnIntroLand(t); }
            }

            if (t.moveT < 1f)
            {
                t.moveT = Mathf.Min(1f, t.moveT + dt / Mathf.Max(0.05f, t.moveDur));
                if (On(Fx.MoveGhostTrail) && t.sprite != null)
                {
                    t.ghostAcc += dt;
                    if (t.ghostAcc > 0.03f)
                    {
                        t.ghostAcc = 0f;
                        Color c = new Color(0.55f, 0.8f, 1f, 0.5f);
                        Emit(t.sprite, t.tf.position, c, new Color(c.r, c.g, c.b, 0f), V2(t.tf.lossyScale), V2(t.tf.lossyScale) * 0.9f,
                             0.3f, Vector2.zero, 0f, 0f, 0f, t.tf.eulerAngles.z, 0, -0.95f);
                    }
                }
            }

            t.squash = Mathf.Max(0f, t.squash - dt * 2.8f);
            t.pop = Mathf.Max(0f, t.pop - dt * 3.5f);
            t.wiggle = Mathf.Max(0f, t.wiggle - dt * 2.5f);
            if (t.spinT < 1f) t.spinT = Mathf.Min(1f, t.spinT + dt / 0.55f);
            t.hover = Mathf.MoveTowards(t.hover, t.hoverGoal, dt * 8f);
            t.selAmt = Mathf.MoveTowards(t.selAmt, t.id == selectedId ? 1f : 0f, dt * 7f);
            bool kingChecked = checkActive && t.type == "king" && t.player == lastPlayer;
            t.checkAmt = Mathf.MoveTowards(t.checkAmt, kingChecked ? 1f : 0f, dt * 5f);
            if (t.collapseT >= 0f && t.collapseT < 1f) t.collapseT = Mathf.Min(1f, t.collapseT + dt / 0.7f);
        }
    }

    void ApplyPose(Track t)
    {
        if (t.tf == null) return;
        float tm = Time.time, inten = Mathf.Clamp(intensity, 0f, 2f);
        Vector3 gp = B2W(t.x, t.y);
        float arcY = 0f, yoff = 0f, sx = 1f, sy = 1f, rot = 0f, alpha = 1f;
        Color tint = Color.white;

        // hop arc + stretch
        if (t.moveT < 1f)
        {
            gp = Vector3.Lerp(t.moveFrom, gp, EaseInOut(t.moveT));
            float s = Mathf.Sin(t.moveT * Mathf.PI);
            arcY = s * t.arc;
            float st = s * 0.18f * inten;
            sx *= 1f - st * 0.5f; sy *= 1f + st;
        }
        // landing squash (damped wobble)
        if (t.squash > 0f)
        {
            float v = Mathf.Cos((1f - t.squash) * Mathf.PI * 3f) * t.squash * 0.3f * inten;
            sx *= 1f + v; sy *= 1f - v; yoff -= v * 0.08f;
        }
        // selection pop
        if (t.pop > 0f) { float v = Mathf.Sin((1f - t.pop) * Mathf.PI) * 0.25f * inten; sx *= 1f + v; sy *= 1f + v; }
        // hover lift
        if (On(Fx.HoverLift) && t.hover > 0f) { sx *= 1f + 0.08f * t.hover; sy *= 1f + 0.08f * t.hover; yoff += 0.05f * t.hover; }
        // levitate while selected
        if (On(Fx.SelectLevitate) && t.selAmt > 0f) yoff += t.selAmt * (0.09f + Mathf.Sin(tm * 6f + t.phase) * 0.03f);
        // idle breathing
        if (On(Fx.IdleBreathing)) { float b = Mathf.Sin(tm * 1.7f + t.phase) * 0.012f; sx *= 1f + b; sy *= 1f - b; }
        // pawn wiggle / knight spin
        if (t.wiggle > 0f) rot += Mathf.Sin(t.wiggle * 18f) * 8f * t.wiggle;
        if (t.spinT < 1f) rot += 360f * EaseOut(t.spinT);
        // check jitter + red pulse
        Vector3 jitter = Vector3.zero;
        if (t.checkAmt > 0f && On(Fx.CheckKingJitter))
        {
            jitter = new Vector3(Random.value - 0.5f, Random.value - 0.5f, 0f) * 0.03f * t.checkAmt;
            tint = Color.Lerp(tint, new Color(1f, 0.4f, 0.4f), (0.5f + 0.5f * Mathf.Sin(tm * 10f)) * t.checkAmt);
        }
        // loser king collapse
        if (t.collapseT >= 0f && On(Fx.LoserKingCollapse))
        {
            float e = EaseOut(Mathf.Clamp01(t.collapseT));
            rot += 90f * e * (t.player == "white" ? 1f : -1f);
            yoff -= 0.18f * e; alpha *= Mathf.Lerp(1f, 0.45f, e);
        }
        // intro drop
        float introAlpha = 1f;
        if (!t.introDone && On(Fx.IntroDrop))
        {
            if (t.introClock < 0f) introAlpha = 0f;
            else { float k = t.introClock / 0.55f; yoff += (1f - EaseOutBounce(k)) * 3.2f; introAlpha = Mathf.Clamp01(k * 5f); }
        }
        alpha *= introAlpha;

        float air = arcY + yoff;
        t.tf.position = gp + new Vector3(0f, air, 0f) + jitter;
        t.tf.localScale = new Vector3(t.baseScale.x * sx, t.baseScale.y * sy, t.baseScale.z);
        t.tf.rotation = Quaternion.Euler(0f, 0f, rot);
        if (t.sr != null)
        {
            Color c = t.baseColor * tint; c.a = t.baseColor.a * alpha;
            t.sr.color = c; t.sprite = t.sr.sprite;
        }
        t.lastPos = t.tf.position; t.lastScale = t.tf.lossyScale; t.lastRot = t.tf.rotation;
        t.lastColor = t.sr != null ? t.sr.color : Color.white;

        // drop shadow
        if (On(Fx.PieceShadows) && t.sr != null)
        {
            if (t.shadow == null)
            {
                t.shadow = new GameObject("SlopShadow");
                t.shadow.transform.SetParent(fxRoot, false);
                t.shadowSr = t.shadow.AddComponent<SpriteRenderer>();
                t.shadowSr.sprite = softSpr;
            }
            t.shadow.SetActive(true);
            t.shadowSr.sortingOrder = t.sr.sortingOrder; t.shadowSr.sortingLayerID = t.sr.sortingLayerID;
            float lift = Mathf.Clamp01(air * 2.5f);
            t.shadow.transform.position = new Vector3(gp.x, gp.y - 0.2f, -0.5f);
            t.shadow.transform.localScale = new Vector3(0.62f * (1f - lift * 0.4f), 0.2f * (1f - lift * 0.4f), 1f);
            t.shadowSr.color = new Color(0f, 0f, 0f, 0.38f * (1f - lift * 0.6f) * alpha);
        }
        else if (t.shadow != null) t.shadow.SetActive(false);
    }

    void ApplyPlates()
    {
        float tm = Time.time;
        foreach (Plate pl in plates.Values)
        {
            if (pl.tf == null || pl.sr == null) continue;
            pl.hover = Mathf.MoveTowards(pl.hover, pl.hoverGoal, Time.unscaledDeltaTime * 10f);
            float age = tm - pl.born, s = 1f;
            Color c = pl.baseColor;

            if (On(Fx.PlatePopIn)) { if (age < 0f) s = 0f; else if (age < 0.3f) s = EaseOutBack(age / 0.3f); }
            if (On(Fx.PlatePulse)) s *= 1f + 0.07f * Mathf.Sin(tm * 5f + pl.phase);
            if (pl.attack && On(Fx.AttackPlateFlicker))
            {
                float f = 0.5f + 0.5f * Mathf.Sin(tm * 18f + pl.phase);
                c = Color.Lerp(new Color(1f, 0.1f, 0.05f, 1f), new Color(1f, 0.7f, 0.1f, 1f), f * 0.6f);
                s *= 1f + 0.04f * f;
            }
            if (On(Fx.HoverPlateGlow) && pl.hover > 0f) { s *= 1f + 0.25f * pl.hover; c = Color.Lerp(c, Color.white, 0.5f * pl.hover); }

            pl.tf.localScale = pl.baseScale * s;
            pl.sr.color = c;
        }
    }

    void ApplyHighlights()
    {
        bool show = On(Fx.LastMoveHighlight) && hasLast;
        if (!show) { if (hlFrom != null) hlFrom.SetActive(false); if (hlTo != null) hlTo.SetActive(false); return; }

        if (hlFrom == null) { hlFrom = MakeQuad("SlopLastFrom", out hlFromSr); hlTo = MakeQuad("SlopLastTo", out hlToSr); }
        hlFrom.SetActive(true); hlTo.SetActive(true);
        float pulse = 0.18f + 0.06f * Mathf.Sin(Time.time * 3f);
        hlFrom.transform.position = B2W(lfx, lfy, -0.35f); hlTo.transform.position = B2W(ltx, lty, -0.35f);
        hlFrom.transform.localScale = hlTo.transform.localScale = new Vector3(Sq, Sq, 1f);
        hlFromSr.color = new Color(1f, 0.9f, 0.3f, pulse * 0.7f);
        hlToSr.color = new Color(1f, 0.9f, 0.3f, pulse);
        hlFromSr.sortingOrder = hlToSr.sortingOrder = baseOrder; hlFromSr.sortingLayerID = hlToSr.sortingLayerID = baseLayer;
    }

    GameObject MakeQuad(string n, out SpriteRenderer sr)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(fxRoot, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSpr;
        return go;
    }

    void ApplyCamera(float rdt)
    {
        float dt = Time.deltaTime;
        shake *= Mathf.Exp(-dt * 9f);
        zoom *= Mathf.Exp(-dt * 7f);
        roll *= Mathf.Exp(-dt * 6f);
        mateZoom = Mathf.Lerp(mateZoom, mateZoomGoal, 1f - Mathf.Exp(-rdt * 1.5f));

        Vector3 off = Vector3.zero;
        float swayRoll = 0f, tm = Time.time;
        if (shake > 0.0005f) off += (Vector3)(Random.insideUnitCircle * shake);
        if (On(Fx.CameraSway))
        {
            off += new Vector3(Mathf.Sin(tm * 0.5f) * 0.03f + mouseNorm.x * 0.06f, Mathf.Cos(tm * 0.4f) * 0.03f + mouseNorm.y * 0.06f, 0f);
            swayRoll = Mathf.Sin(tm * 0.3f) * 0.3f;
        }
        if (mateZoom > 0.001f)
            off += new Vector3(mateFocus.x - camBasePos.x, mateFocus.y - camBasePos.y, 0f) * mateZoom * 1.2f;

        cam.transform.position = camBasePos + off;
        cam.transform.rotation = camBaseRot * Quaternion.Euler(0f, 0f, roll + swayRoll);

        float zf = Mathf.Clamp(1f - zoom - mateZoom, 0.3f, 1.5f);
        if (cam.orthographic) cam.orthographicSize = camBaseSize * zf; else cam.fieldOfView = camBaseFov * zf;

        if (cam.clearFlags == CameraClearFlags.SolidColor)
        {
            if (On(Fx.BackgroundHueShift))
            {
                float h, s, v; Color.RGBToHSV(camBaseBg, out h, out s, out v);
                cam.backgroundColor = Color.HSVToRGB((h + tm * 0.03f) % 1f, Mathf.Max(s, 0.35f), Mathf.Max(v, 0.22f));
            }
            else cam.backgroundColor = camBaseBg;
        }
    }

    void AddShake(float a) { shake = Mathf.Max(shake, a * intensity); }
    void Flash(float a, Color c) { flashA = Mathf.Max(flashA, a * Mathf.Clamp(intensity, 0f, 1.5f)); flashC = c; }

    // =====================================================================
    //  AMBIENT / CONTINUOUS EFFECTS
    // =====================================================================
    void UpdateAmbient(float dt)
    {
        if (On(Fx.DustMotes))
        {
            dustAcc += dt;
            while (dustAcc > 0.15f)
            {
                dustAcc -= 0.15f;
                Vector3 p = new Vector3(Random.Range(-3.4f, 3.4f), Random.Range(-3.4f, 3.4f), 0f);
                Emit(softSpr, p, new Color(1f, 1f, 1f, 0.18f), new Color(1f, 1f, 1f, 0f), Vector2.one * 0.12f, Vector2.one * 0.2f,
                     6f, new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.06f, 0.15f)), 0f, 0f, 0f, 0f, 20);
            }
        }
        if (On(Fx.BoardTwinkle))
        {
            twinkleAcc += dt;
            while (twinkleAcc > 0.18f)
            {
                twinkleAcc -= 0.18f;
                Vector3 p = B2W(Random.Range(0, 8), Random.Range(0, 8)) + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), 0f);
                Emit(starSpr, p, new Color(1f, 0.95f, 0.6f, 0.9f), new Color(1f, 0.95f, 0.6f, 0f), Vector2.one * 0.2f, Vector2.zero, 0.7f, Vector2.zero, 0f, 0f, 60f, 0f, 5);
            }
        }
        if (checkActive && On(Fx.CheckDangerRing))
        {
            ringAcc += dt;
            if (ringAcc > 0.55f)
            {
                ringAcc = 0f;
                Track k = FindKing(lastPlayer);
                if (k != null && k.tf != null) Ring(k.tf.position, new Color(1f, 0.1f, 0.1f, 0.85f), 0.9f, 0.7f);
            }
        }
        if (confettiOn && On(Fx.MateConfetti))
        {
            confettiAcc += dt;
            while (confettiAcc > 0.02f)
            {
                confettiAcc -= 0.02f;
                Color c = Color.HSVToRGB(Random.value, 0.75f, 1f);
                Emit(squareSpr, new Vector3(Random.Range(-4f, 4f), 4.2f, 0f), c, new Color(c.r, c.g, c.b, 0.9f), new Vector2(0.06f, 0.12f), new Vector2(0.06f, 0.12f),
                     3.5f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-1f, -2f)), 0.8f, 0.2f, Random.Range(-360f, 360f), Random.value * 360f, 40);
            }
        }
        if (fireworksOn && On(Fx.MateFireworks))
        {
            fwAcc += dt;
            if (fwAcc > 0.4f)
            {
                fwAcc = 0f;
                Vector3 p = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-1f, 3f), 0f);
                Color c = Color.HSVToRGB(Random.value, 0.7f, 1f);
                Burst(p, 30, c, 3f, 0.09f, 1f, 1.5f, starSpr, 1.2f);
                Ring(p, new Color(c.r, c.g, c.b, 0.7f), 1.2f, 0.5f);
                Play(cPop, 0.5f, Random.Range(0.8f, 1.3f));
            }
        }
        if (auraTrack != null && auraTrack.tf != null && On(Fx.WinnerAura))
        {
            auraAcc += dt;
            while (auraAcc > 0.03f)
            {
                auraAcc -= 0.03f;
                float a = Time.time * 4f;
                Vector3 p = auraTrack.tf.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.45f;
                Emit(starSpr, p, new Color(1f, 0.85f, 0.2f, 1f), new Color(1f, 0.85f, 0.2f, 0f), Vector2.one * 0.14f, Vector2.zero, 0.5f, new Vector2(0f, 0.3f), 0f, 0f, 90f, 0f, 20);
                Emit(softSpr, auraTrack.tf.position, new Color(1f, 0.85f, 0.3f, 0.08f), new Color(1f, 0.85f, 0.3f, 0f), Vector2.one * 0.9f, Vector2.one * 1.1f, 0.1f, Vector2.zero, 0f, 0f, 0f, 0f, 0, -0.9f);
            }
        }
    }

    void UpdateOverlays(float rdt)
    {
        turnTint = Mathf.MoveTowards(turnTint, 0f, rdt * 1.8f);
        flashA = Mathf.MoveTowards(flashA, 0f, rdt * 3f);
        letterbox = Mathf.MoveTowards(letterbox, letterboxGoal, rdt * 2f);
        gray = Mathf.MoveTowards(gray, grayGoal, rdt * 1.5f);
        for (int i = texts.Count - 1; i >= 0; i--) { texts[i].age += rdt; if (texts[i].age >= texts[i].life) texts.RemoveAt(i); }
    }

    // =====================================================================
    //  PARTICLE POOL (SpriteRenderer based = works in Built-in, URP and 2D renderer)
    // =====================================================================
    class P
    {
        public Transform tf; public SpriteRenderer sr; public Vector3 vel, s0, s1;
        public Color c0, c1; public float life, age, grav, drag, spin, rot;
    }
    Transform fxRoot;
    readonly List<P> live = new List<P>();
    readonly Stack<P> pool = new Stack<P>();

    static Vector2 V2(Vector3 v) { return new Vector2(v.x, v.y); }

    P Emit(Sprite spr, Vector3 pos, Color c0, Color c1, Vector2 s0, Vector2 s1, float life, Vector2 vel,
           float grav = 0f, float drag = 0f, float spin = 0f, float rot = 0f, int order = 10, float z = -2f)
    {
        if (spr == null || live.Count > 1800) return null;
        P p;
        if (pool.Count > 0) p = pool.Pop();
        else
        {
            GameObject go = new GameObject("fx");
            go.transform.SetParent(fxRoot, false);
            p = new P { tf = go.transform, sr = go.AddComponent<SpriteRenderer>() };
        }
        p.tf.gameObject.SetActive(true);
        p.sr.sprite = spr; p.sr.sortingOrder = baseOrder + order; p.sr.sortingLayerID = baseLayer;
        pos.z = z; p.tf.position = pos;
        p.rot = rot; p.tf.rotation = Quaternion.Euler(0f, 0f, rot);
        p.s0 = new Vector3(s0.x, s0.y, 1f); p.s1 = new Vector3(s1.x, s1.y, 1f);
        p.tf.localScale = p.s0;
        p.c0 = c0; p.c1 = c1; p.sr.color = c0;
        p.vel = new Vector3(vel.x, vel.y, 0f);
        p.life = Mathf.Max(0.01f, life); p.age = 0f; p.grav = grav; p.drag = drag; p.spin = spin;
        live.Add(p);
        return p;
    }

    void UpdateParticles(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            P p = live[i];
            p.age += dt;
            if (p.age >= p.life)
            {
                p.tf.gameObject.SetActive(false); pool.Push(p);
                live[i] = live[live.Count - 1]; live.RemoveAt(live.Count - 1);
                continue;
            }
            float k = p.age / p.life, ke = 1f - (1f - k) * (1f - k);
            p.vel.y -= p.grav * dt;
            if (p.drag > 0f) p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
            p.tf.position += p.vel * dt;
            p.rot += p.spin * dt; p.tf.rotation = Quaternion.Euler(0f, 0f, p.rot);
            p.tf.localScale = Vector3.Lerp(p.s0, p.s1, ke);
            p.sr.color = Color.Lerp(p.c0, p.c1, k);
        }
    }

    void Burst(Vector3 pos, int n, Color c, float speed, float size, float life, float grav = 3f, Sprite spr = null, float drag = 1.5f)
    {
        n = Mathf.RoundToInt(n * Mathf.Clamp(intensity, 0.2f, 2f));
        if (spr == null) spr = softSpr;
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * 6.2832f;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.35f, 1f);
            Color cc = Color.Lerp(c, Color.white, Random.value * 0.4f); cc.a = c.a;
            float sz = size * Random.Range(0.6f, 1.4f);
            Emit(spr, pos, cc, new Color(cc.r, cc.g, cc.b, 0f), Vector2.one * sz, Vector2.zero, life * Random.Range(0.7f, 1.2f), v, grav, drag, 0f, 0f, 12);
        }
    }

    void Ring(Vector3 pos, Color c, float radius, float life)
    {
        Emit(ringSpr, pos, c, new Color(c.r, c.g, c.b, 0f), Vector2.one * radius * 0.3f, Vector2.one * radius * 2f, life, Vector2.zero, 0f, 0f, 0f, 0f, 11);
    }

    void SquareFlash(int x, int y, Color c)
    {
        Emit(squareSpr, B2W(x, y), c, new Color(c.r, c.g, c.b, 0f), Vector2.one * Sq, Vector2.one * Sq, 0.55f, Vector2.zero, 0f, 0f, 0f, 0f, 0, -0.4f);
    }

    // =====================================================================
    //  TINY SCHEDULER (scaled time, so slow-mo / hit-stop delay it naturally)
    // =====================================================================
    class Job { public float at; public Action a; }
    readonly List<Job> jobs = new List<Job>();
    void After(float delay, Action a) { jobs.Add(new Job { at = Time.time + delay, a = a }); }
    void RunJobs()
    {
        for (int i = 0; i < jobs.Count; i++)
            if (Time.time >= jobs[i].at) { Job j = jobs[i]; jobs.RemoveAt(i); i--; j.a(); }
    }

    // =====================================================================
    //  FLOATING TEXT + OVERLAYS (IMGUI)
    // =====================================================================
    class FT { public string s; public Vector3 world; public bool screen; public Vector2 frac; public float age, life, size, rise, shake; public Color c; }
    readonly List<FT> texts = new List<FT>();
    GUIStyle labelStyle;

    void AddText(string s, Vector3 world, Color c, float size, float life, float rise = 1f, float shk = 0f)
    {
        texts.Add(new FT { s = s, world = world, c = c, size = size, life = life, rise = rise, shake = shk });
    }
    void AddScreenText(string s, Vector2 frac, Color c, float size, float life)
    {
        texts.Add(new FT { s = s, screen = true, frac = frac, c = c, size = size, life = life, rise = 0.3f });
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.Repaint) DrawOverlays();
        if (showPanel) DrawPanel();
    }

    void DrawOverlays()
    {
        if (game == null || cam == null) return;
        if (masterEnabled)
        {
            if (On(Fx.StaticVignette)) DrawTex(vigTex, new Color(0f, 0f, 0f, 0.35f));
            if (turnTint > 0.01f) DrawTex(vigTex, new Color(turnTintC.r, turnTintC.g, turnTintC.b, turnTint * 0.5f));
            if (checkActive && On(Fx.CheckVignette)) DrawTex(vigTex, new Color(1f, 0f, 0f, 0.4f + 0.25f * Mathf.Sin(Time.unscaledTime * 7f)));
            if (gray > 0.01f) DrawTex(Texture2D.whiteTexture, new Color(0.5f, 0.5f, 0.55f, gray * 0.45f));
            if (flashA > 0.01f) DrawTex(Texture2D.whiteTexture, new Color(flashC.r, flashC.g, flashC.b, flashA));
            if (letterbox > 0.01f)
            {
                float h = Screen.height * 0.09f * letterbox;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
        DrawTexts();
        if (Time.unscaledTime - startTime < 8f) GUI.Label(new Rect(10, Screen.height - 28, 420, 24), "EffectSlop: press " + panelKey + " for the effects panel");
    }

    void DrawTex(Texture2D tex, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    void DrawTexts()
    {
        if (texts.Count == 0) return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };

        foreach (FT ft in texts)
        {
            float k = ft.age / ft.life;
            float pop = 1f + 0.6f * Mathf.Clamp01(1f - ft.age / 0.18f);
            float a = k > 0.65f ? 1f - (k - 0.65f) / 0.35f : 1f;

            Vector2 p;
            if (ft.screen) p = new Vector2(ft.frac.x * Screen.width, ft.frac.y * Screen.height);
            else { Vector3 sp = cam.WorldToScreenPoint(ft.world); p = new Vector2(sp.x, Screen.height - sp.y); }
            p.y -= ft.rise * k * Screen.height * 0.1f;
            if (ft.shake > 0f) p += Random.insideUnitCircle * ft.shake * Screen.height * 0.01f;

            int px = Mathf.Max(8, Mathf.RoundToInt(ft.size * Screen.height * pop));
            labelStyle.fontSize = px;
            Rect r = new Rect(p.x - 400f, p.y - px, 800f, px * 2f);

            labelStyle.normal.textColor = new Color(0f, 0f, 0f, a * 0.85f);
            float o = Mathf.Max(1f, px * 0.06f);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), ft.s, labelStyle);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), ft.s, labelStyle);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), ft.s, labelStyle);
            GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), ft.s, labelStyle);
            labelStyle.normal.textColor = new Color(ft.c.r, ft.c.g, ft.c.b, a);
            GUI.Label(r, ft.s, labelStyle);
        }
    }

    void DrawPanel()
    {
        float h = Mathf.Min(Screen.height - 20f, 580f);
        GUILayout.BeginArea(new Rect(10f, 10f, 300f, h), GUI.skin.box);
        GUILayout.Label("EffectSlop  (" + panelKey + " to hide)");
        masterEnabled = GUILayout.Toggle(masterEnabled, "Master enabled");
        soundEnabled = GUILayout.Toggle(soundEnabled, "Sound");
        GUILayout.Label("Intensity  " + intensity.ToString("0.00"));
        intensity = GUILayout.HorizontalSlider(intensity, 0f, 2f);
        GUILayout.Label("Volume  " + volume.ToString("0.00"));
        volume = GUILayout.HorizontalSlider(volume, 0f, 1f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("All on")) for (int i = 0; i < on.Length; i++) on[i] = true;
        if (GUILayout.Button("All off")) for (int i = 0; i < on.Length; i++) on[i] = false;
        GUILayout.EndHorizontal();
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < on.Length; i++) on[i] = GUILayout.Toggle(on[i], fxNames[i]);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    static string Nice(string s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // =====================================================================
    //  EASING
    // =====================================================================
    static float EaseOut(float k) { return 1f - (1f - k) * (1f - k); }
    static float EaseInOut(float k) { return k * k * (3f - 2f * k); }
    static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1f; float x = k - 1f; return 1f + c3 * x * x * x + c1 * x * x; }
    static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (x < 1f / d1) return n1 * x * x;
        if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
        if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
        x -= 2.625f / d1; return n1 * x * x + 0.984375f;
    }

    // =====================================================================
    //  PROCEDURAL SPRITES / TEXTURES (no assets needed)
    // =====================================================================
    Sprite softSpr, circleSpr, ringSpr, squareSpr, starSpr;
    Texture2D vigTex;

    void BuildSprites()
    {
        softSpr = MakeSprite(64, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); float a = Mathf.Clamp01(1f - r); return a * a; });
        circleSpr = MakeSprite(64, (u, v) => Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 32f));
        ringSpr = MakeSprite(128, (u, v) => { float d = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.9f); return Mathf.Clamp01((0.08f - d) * 64f); });
        squareSpr = MakeSprite(4, (u, v) => 1f);
        starSpr = MakeSprite(64, (u, v) => Mathf.Clamp01((1f - (Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v)))) * 3f));

        int n = 64;
        vigTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) / 1.2f;
                float t = Mathf.Clamp01((r - 0.35f) / 0.65f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(t * t * (3f - 2f * t) * 255f));
            }
        vigTex.SetPixels32(px); vigTex.Apply();
    }

    static Sprite MakeSprite(int size, Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(u, v)) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 unit == whole sprite
    }

    // =====================================================================
    //  PROCEDURAL AUDIO (no assets needed)
    // =====================================================================
    const int SR = 44100;
    AudioSource[] srcs; int srcIdx;
    AudioClip cSelect, cWhoosh, cThud, cCrunch, cAlarm, cFanfare, cSad, cIntro, cCombo, cPop;

    void Play(AudioClip c, float vol = 1f, float pitch = 1f)
    {
        if (!masterEnabled || !soundEnabled || c == null || srcs == null) return;
        AudioSource s = srcs[srcIdx++ % srcs.Length];
        s.pitch = pitch;
        s.PlayOneShot(c, vol * volume * Mathf.Clamp(intensity, 0.2f, 1.5f));
    }

    void BuildAudio()
    {
        cSelect = Clip("select", Tone(600f, 900f, 0.09f, 0, 0.5f, 2f));
        cWhoosh = Clip("whoosh", Whoosh(0.22f, 0.9f));
        cThud = Clip("thud", Mix(Tone(150f, 45f, 0.18f, 0, 0.9f, 2.5f), Tone(0f, 0f, 0.05f, 3, 0.25f, 3f)));
        cCrunch = Clip("crunch", Mix(Tone(0f, 0f, 0.22f, 3, 0.8f, 3f), Tone(110f, 40f, 0.22f, 1, 0.4f, 2.5f)));
        cAlarm = Clip("alarm", Seq(Tone(880f, 880f, 0.12f, 1, 0.3f, 0.5f), Tone(0f, 0f, 0.04f, 0, 0f, 1f), Tone(660f, 660f, 0.12f, 1, 0.3f, 0.5f),
                                    Tone(0f, 0f, 0.04f, 0, 0f, 1f), Tone(880f, 880f, 0.12f, 1, 0.3f, 0.5f)));
        cFanfare = Clip("fanfare", Seq(Tone(523f, 523f, 0.12f, 2, 0.35f, 0.8f), Tone(659f, 659f, 0.12f, 2, 0.35f, 0.8f),
                                       Tone(784f, 784f, 0.12f, 2, 0.35f, 0.8f), Tone(1047f, 1047f, 0.5f, 2, 0.35f, 1.2f)));
        cSad = Clip("sad", Tone(420f, 110f, 0.9f, 2, 0.35f, 1.2f));
        cIntro = Clip("intro", Seq(Tone(262f, 262f, 0.1f, 1, 0.25f, 0.8f), Tone(392f, 392f, 0.1f, 1, 0.25f, 0.8f), Tone(523f, 523f, 0.25f, 1, 0.25f, 1.2f)));
        cCombo = Clip("combo", Tone(500f, 1400f, 0.22f, 1, 0.3f, 1.5f));
        cPop = Clip("pop", Mix(Tone(0f, 0f, 0.12f, 3, 0.5f, 3f), Tone(900f, 300f, 0.12f, 0, 0.3f, 2f)));
    }

    static AudioClip Clip(string n, float[] data)
    {
        AudioClip c = AudioClip.Create(n, data.Length, 1, SR, false);
        c.SetData(data, 0);
        return c;
    }

    // wave: 0 sine, 1 square, 2 saw, 3 noise
    static float[] Tone(float f0, float f1, float dur, int wave, float vol, float decay)
    {
        int n = Mathf.Max(1, (int)(SR * dur)); float[] d = new float[n]; float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n, f = Mathf.Lerp(f0, f1, t);
            ph += f / SR; ph -= Mathf.Floor(ph);
            float s;
            switch (wave)
            {
                case 0: s = Mathf.Sin(ph * 6.2832f); break;
                case 1: s = ph < 0.5f ? 1f : -1f; break;
                case 2: s = ph * 2f - 1f; break;
                default: s = Random.value * 2f - 1f; break;
            }
            d[i] = s * Mathf.Pow(1f - t, decay) * Mathf.Min(1f, i / 200f) * vol;
        }
        return d;
    }

    static float[] Whoosh(float dur, float vol)
    {
        int n = (int)(SR * dur); float[] d = new float[n]; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n, env = Mathf.Sin(t * Mathf.PI);
            lp += ((Random.value * 2f - 1f) - lp) * Mathf.Lerp(0.02f, 0.25f, env);
            d[i] = lp * env * vol * 3f;
        }
        return d;
    }

    static float[] Mix(float[] a, float[] b)
    {
        float[] d = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < d.Length; i++) d[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
        return d;
    }

    static float[] Seq(params float[][] parts)
    {
        int total = 0; foreach (float[] p in parts) total += p.Length;
        float[] d = new float[total]; int o = 0;
        foreach (float[] p in parts) { Array.Copy(p, 0, d, o, p.Length); o += p.Length; }
        return d;
    }
}