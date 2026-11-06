// =====================================================================
//  EARTHQUAKE RESCUE  -  standalone game, ONE file (Unity 6, 3D)
//
//  HOW TO USE
//   1. Copy this file to your Assets folder (it does not need other files).
//   2. Open an EMPTY scene (File > New Scene > Basic) and press Play.
//      Everything (menu, city, people, fire, water, weather, sound, HUD)
//      is created by code. No prefabs, audio files or tags needed.
//   Works with the new Input System package AND the old Input Manager.
//
//  Aftershocks drop boulders (stay off the red markers), buildings are broken and fires burn in the ruins.
//
//  SEVERAL GAMES IN ONE PROJECT?  Name the scene  Earthquake / Flood /
//  Wildfire / Hurricane  and only that game starts in that scene.
//
//  CONTROLS
//   WASD / Arrows  move          Shift   sprint (stamina)     Space  jump
//   Mouse          look          E       pick up / drop survivor
//   Left Mouse / X hold: spray extinguisher (puts out fires)
//   F              medkit (stabilize an injured survivor, else heal yourself)
//   Q              flashlight    M       mute                 Esc    pause
//   R              restart       Enter   next level           Tab    main menu
//   Menu: keys 5-7 pick difficulty, Enter starts.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
namespace DRS_Earthquake
{
    /// <summary>Adds the shaders this game needs to Graphics Settings > Always Included Shaders,
    /// so builds don't turn pink/magenta.</summary>
    [UnityEditor.InitializeOnLoad]
    static class EarthquakeShaderKeeper
    {
        static EarthquakeShaderKeeper() { UnityEditor.EditorApplication.delayCall += Run; }

        static void Run()
        {
            string[] names =
            {
                "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Particles/Unlit",
                "HDRP/Lit", "Standard", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive",
                "Skybox/Procedural", "Sprites/Default"
            };
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (asset == null) return;
            var so = new UnityEditor.SerializedObject(asset);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            bool changed = false;
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (has) continue;
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[EarthquakeGame] Added game shaders to Always Included Shaders. Rebuild the game.");
            }
        }
    }
}
#endif

namespace DRS_Earthquake
{
    /// <summary>Input wrapper that works with both input systems.</summary>
    public static class Inp
    {
#if ENABLE_INPUT_SYSTEM
        static readonly Key[] Digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
        static bool Down(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }
        static bool Held(Key k) { var kb = Keyboard.current; return kb != null && kb[k].isPressed; }
        public static float Horizontal() { return (Held(Key.D) || Held(Key.RightArrow) ? 1f : 0f) - (Held(Key.A) || Held(Key.LeftArrow) ? 1f : 0f); }
        public static float Vertical() { return (Held(Key.W) || Held(Key.UpArrow) ? 1f : 0f) - (Held(Key.S) || Held(Key.DownArrow) ? 1f : 0f); }
        public static Vector2 MouseDelta() { return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.05f : Vector2.zero; }
        public static bool Sprint() { return Held(Key.LeftShift) || Held(Key.RightShift); }
        public static bool Spray() { return Held(Key.X) || (Mouse.current != null && Mouse.current.leftButton.isPressed); }
        public static bool Jump() { return Down(Key.Space); }
        public static bool Use() { return Down(Key.E); }
        public static bool Medkit() { return Down(Key.F); }
        public static bool Flash() { return Down(Key.Q); }
        public static bool Mute() { return Down(Key.M); }
        public static bool Pause() { return Down(Key.Escape); }
        public static bool Restart() { return Down(Key.R); }
        public static bool MenuKey() { return Down(Key.Tab); }
        public static bool Next() { return Down(Key.Enter) || Down(Key.NumpadEnter); }
        public static int Num() { for (int i = 0; i < Digits.Length; i++) if (Down(Digits[i])) return i + 1; return 0; }
#else
        public static float Horizontal() { return Input.GetAxisRaw("Horizontal"); }
        public static float Vertical() { return Input.GetAxisRaw("Vertical"); }
        public static Vector2 MouseDelta() { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        public static bool Sprint() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }
        public static bool Spray() { return Input.GetKey(KeyCode.X) || Input.GetMouseButton(0); }
        public static bool Jump() { return Input.GetKeyDown(KeyCode.Space); }
        public static bool Use() { return Input.GetKeyDown(KeyCode.E); }
        public static bool Medkit() { return Input.GetKeyDown(KeyCode.F); }
        public static bool Flash() { return Input.GetKeyDown(KeyCode.Q); }
        public static bool Mute() { return Input.GetKeyDown(KeyCode.M); }
        public static bool Pause() { return Input.GetKeyDown(KeyCode.Escape); }
        public static bool Restart() { return Input.GetKeyDown(KeyCode.R); }
        public static bool MenuKey() { return Input.GetKeyDown(KeyCode.Tab); }
        public static bool Next() { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); }
        public static int Num() { for (int i = 0; i < 7; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i + 1; return 0; }
#endif
    }

    public class EarthquakeGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // Several rescue games in one project: the scene name decides which one runs.
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();
            if (scene.Contains("flood") || scene.Contains("wildfire") || scene.Contains("hurricane")) return;            // scene belongs to another game
            if (!scene.Contains("earthquake"))
            {
                foreach (var t in new[] { "DRS_Flood.FloodGame", "DRS_Wildfire.WildfireGame", "DRS_Hurricane.HurricaneGame" })
                    if (System.Type.GetType(t) != null)
                    {
                        Debug.LogWarning("[EarthquakeGame] More than one rescue game is in the project. Name the scene 'Earthquake' to run this one, or keep only one game file.");
                        return;
                    }
            }
            if (FindFirstObjectByType<EarthquakeGame>() != null) return;
            new GameObject("EarthquakeGame").AddComponent<EarthquakeGame>();
        }

        // ---------------- Tuning ----------------
        const float WorldHalf = 55f, SafeR = 5f;
        const float WalkSpeed = 6f, SprintSpeed = 10.5f, JumpHeight = 1.5f, Gravity = -24f;
        const float MaxHealth = 100f, MaxStamina = 100f, MaxWater = 100f;
        const float PickupRange = 2.8f, MouseSens = 3f, CamDist = 7f;
        const int SR = 22050;

        enum State { Menu, Playing, Paused, LevelDone, GameOver }
        enum Disaster { Earthquake, Flood, Wildfire, Hurricane }

        static readonly string[] DNames = { "EARTHQUAKE", "FLOOD", "WILDFIRE", "HURRICANE" };
        // menu texts (this game only shows its own disaster)
        static readonly string[] DDesc = { "The city has collapsed. Aftershocks rain debris, fires burn in the ruins and gas lines blaze.", "Floodwater keeps rising and a strong current pulls you. Dodge floating logs, survivors wait on platforms.", "A fast-spreading wildfire. Put fires out before they swallow the town. Smoke and embers everywhere.", "Hurricane winds shove you around. Heavy rain, lightning and flying debris. Stay low and keep moving." };
        static readonly string[] DiffNames = { "EASY", "NORMAL", "HARD" };
        static readonly string[] KindNames = { "Adult", "Child", "Elderly", "Injured" };
        static readonly int[] KindPoints = { 100, 150, 120, 200 };
        static readonly float[] KindCarry = { 0.7f, 0.85f, 0.65f, 0.6f };
        static readonly Color[] KindColors =
        {
            new Color(1f, 0.92f, 0.2f), new Color(0.3f, 0.9f, 1f), new Color(0.8f, 0.6f, 1f), new Color(1f, 0.2f, 0.2f)
        };

        class Rig { public Transform root, lArm, rArm, lLeg, rLeg; }
        class Survivor
        {
            public Rig rig; public Transform beacon; public AudioSource voice;
            public bool rescued, lost, stab;
            public int kind; public float phase, crit, helpT;
        }
        class Hazard
        {
            public Transform t; public float phase, radius = 2.2f, hp = 100f, scale = 1f;
            public bool isOut;
            public ParticleSystem fire, smoke, embers;
            public Light light; public AudioSource audio; public GameObject core;
        }
        class Pickup { public Transform t; public int kind; public bool taken; }
        class Flyer { public Transform t; public Vector3 vel; public float life, spin; public bool log; }

        // ---------------- State ----------------
        State state = State.Menu;
        Disaster disaster = Disaster.Earthquake;
        int difficulty = 1;
        float timeMul = 1f, dmgMul = 1f;
        int maxLost = 3;

        Transform world;
        GameObject player;
        CharacterController cc;
        Rig prig;
        Camera cam;
        Light sun, flashlight;
        ParticleSystem sprayPS, weatherPS;
        Transform heliRotor, patrolHeli;
        Material skyMat;

        float yaw, pitch = 18f, turnVel, vy, walkPhase;
        float health, stamina, timeLeft, timeTotal, water = MaxWater, boostT;
        int score, level = 1, medkits = 2, rescuedThisLevel, totalThisLevel, lostCount, combo, firesOut;
        int[] hiScores = new int[4];
        float comboTimer;
        bool night, flashOn, wasGrounded = true;
        int lastStep;
        Survivor carried;

        // (disaster specific fields are in Earthquake.cs / Flood.cs / Wildfire.cs / Hurricane.cs)
        float sunBase;
        int stars;

        readonly List<Survivor> survivors = new List<Survivor>();
        readonly List<Hazard> hazards = new List<Hazard>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly List<Bounds> blocks = new List<Bounds>();
        readonly List<Light> lamps = new List<Light>();
        readonly List<Material> levelMats = new List<Material>();
        readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        Shader baseShader;
        Material pmatAdd, pmatAlpha;
        Texture2D softTex, windowTex, groundTex, waterTex;

        float hurt, toastT, animT, lastHurtSfx, fovCur = 62f, menuAngle;
        int lastBeepSec = -1;
        string toast = "", endMessage = "";
        GUIStyle txt;

        // Audio
        AudioSource sfx, ambWind, ambMusic, ambRumble, ambSpray, ambHeart, ambRain, ambWater, ambRoar;
        AudioClip cStep, cJump, cLand, cPickup, cRescue, cHurt, cMedkit, cFire, cWind, cRumble, cBoom, cSiren, cBeep,
                  cHeart, cFanfare, cGameOver, cHiss, cClick, cSprayLoop, cMusic, cChop, cSplash, cRain, cThunder,
                  cWaterLoop, cRoar, cHelp;

        // =================================================================
        //  Setup
        // =================================================================
        void Start()
        {
            for (int i = 0; i < 4; i++) hiScores[i] = PlayerPrefs.GetInt("DRS_HI_" + i, 0);

            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera");
                cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.farClipPlane = 500f;
            cam.fieldOfView = 62f;

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var lg = new GameObject("Sun");
                sun = lg.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            QualitySettings.shadowDistance = 90f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.sun = sun;

            BuildTextures();
            BuildAudio();

            // Player
            player = new GameObject("Player");
            cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.4f; cc.skinWidth = 0.05f;
            prig = MakePerson("PlayerModel", new Color(0.15f, 0.45f, 0.95f), new Color(0.12f, 0.15f, 0.3f), new Color(0.95f, 0.78f, 0.62f), 1f);
            prig.root.SetParent(player.transform, false);
            Prim(PrimitiveType.Sphere, "Helmet", prig.root, new Vector3(0, 1.78f, 0), new Vector3(0.46f, 0.3f, 0.46f), new Color(1f, 0.85f, 0.1f), false);
            Prim(PrimitiveType.Cylinder, "Tank", prig.root, new Vector3(0, 1.15f, -0.28f), new Vector3(0.22f, 0.32f, 0.22f), new Color(0.85f, 0.1f, 0.1f), false);
            Prim(PrimitiveType.Sphere, "TankTop", prig.root, new Vector3(0, 1.47f, -0.28f), new Vector3(0.22f, 0.15f, 0.22f), new Color(0.3f, 0.3f, 0.3f), false);

            // Flashlight
            var fg = new GameObject("Flashlight");
            fg.transform.SetParent(player.transform, false);
            fg.transform.localPosition = new Vector3(0, 1.6f, 0.3f);
            flashlight = fg.AddComponent<Light>();
            flashlight.type = LightType.Spot; flashlight.range = 38f; flashlight.spotAngle = 60f; flashlight.intensity = 4f;
            flashlight.shadows = LightShadows.None;
            flashlight.enabled = false;

            // Water spray
            sprayPS = MakePS(player.transform, "Spray", new Vector3(0, 1.2f, 0.55f), Quaternion.identity);
            var sm = sprayPS.main; sm.loop = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(12f, 16f); sm.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            sm.startColor = new Color(0.7f, 0.85f, 1f, 0.8f); sm.gravityModifier = 0.9f; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 400;
            var se = sprayPS.emission; se.rateOverTime = 0f;
            var ss = sprayPS.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 3.5f; ss.radius = 0.04f;
            sprayPS.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat(false);
            sprayPS.Play();

            // Weather particles (rain / ash / dust), follows the player
            weatherPS = MakePS(transform, "Weather", Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            GoMenu();
        }

        void ApplyDifficulty()
        {
            float[] tm = { 1.35f, 1f, 0.8f };
            float[] dm = { 0.7f, 1f, 1.35f };
            int[] ml = { 5, 3, 2 };
            timeMul = tm[difficulty]; dmgMul = dm[difficulty]; maxLost = ml[difficulty];
        }

        void GoMenu()
        {
            carried = null;
            player.SetActive(false);
            state = State.Menu;
            ApplyDifficulty();
            BuildLevel(true);
            SetCursor(false);
        }

        void StartGame()
        {
            level = 1; score = 0; medkits = 2; health = MaxHealth; water = MaxWater;
            combo = 0; comboTimer = 0f; firesOut = 0; boostT = 0f;
            endMessage = "";
            ApplyDifficulty();
            player.SetActive(true);
            BuildLevel(false);
        }

        void SelectDisaster(int i)
        {
            if ((int)disaster == i) return;
            disaster = (Disaster)i;
            Sfx(cClick, 0.6f);
            BuildLevel(true);
        }

        // =================================================================
        //  Level construction
        // =================================================================
        Vector3 SpawnPoint() { return new Vector3(0f, 0.1f, -8f); }

        void BuildLevel(bool menu)
        {
            if (carried != null) { Destroy(carried.rig.root.gameObject); carried = null; }
            if (world != null) Destroy(world.gameObject);
            foreach (var lm in levelMats) if (lm != null) Destroy(lm);
            levelMats.Clear();
            survivors.Clear(); hazards.Clear(); pickups.Clear(); flyers.Clear(); blocks.Clear(); lamps.Clear();
            patrolHeli = null; heliRotor = null;
            world = new GameObject("World").transform;
            Random.InitState(1000 + level * 17 + (int)disaster * 101);
            night = !menu && level % 3 == 0;
            flashOn = night;
            ResetEarthquake();
            lostCount = 0; rescuedThisLevel = 0; boostT = 0f;

            BuildGround();
            BuildBase();
            BuildProps();
            BuildEarthquakeCity();
            BuildRubble(40);
            SpawnHazards();
            SpawnSurvivors();
            SpawnPickups();
            ApplyEnvironment();

            // Player reset
            cc.enabled = false;
            player.transform.position = SpawnPoint();
            player.transform.rotation = Quaternion.identity;
            cc.enabled = true;
            yaw = 0f; vy = 0f;
            stamina = MaxStamina; water = MaxWater;
            if (!menu && level > 1) health = Mathf.Min(MaxHealth, health + 30f);
            timeTotal = (80f + 16f * totalThisLevel) * timeMul;
            timeLeft = timeTotal;
            if (disaster == Disaster.Earthquake) ArmEarthquake();
            if (!menu)
            {
                state = State.Playing;
                SetCursor(true);
                ShowToast(DNames[(int)disaster] + (night ? " (NIGHT)" : "") + " - LEVEL " + level + ": rescue " + totalThisLevel + " people!", 4f);
            }
        }

        void BuildGround()
        {
            var ground = Prim(PrimitiveType.Cube, "Ground", world, new Vector3(0, -0.5f, 0), new Vector3(WorldHalf * 2f + 20f, 1f, WorldHalf * 2f + 20f), Color.white, true);
            Color gc = DisasterGroundTint();
            ground.GetComponent<Renderer>().sharedMaterial = TexMat(gc, groundTex, new Vector2(24f, 24f));
            Color road = new Color(0.14f, 0.14f, 0.16f);
            Prim(PrimitiveType.Cube, "RoadX", world, new Vector3(0, 0.01f, 0), new Vector3(WorldHalf * 2f, 0.02f, 8f), road, false);
            Prim(PrimitiveType.Cube, "RoadZ", world, new Vector3(0, 0.012f, 0), new Vector3(8f, 0.02f, WorldHalf * 2f), road, false);
            Color walk = new Color(0.6f, 0.6f, 0.58f);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, "WalkX", world, new Vector3(0, 0.04f, s * 5.2f), new Vector3(WorldHalf * 2f, 0.08f, 2f), walk, false);
                Prim(PrimitiveType.Cube, "WalkZ", world, new Vector3(s * 5.2f, 0.045f, 0), new Vector3(2f, 0.08f, WorldHalf * 2f), walk, false);
            }
            for (float v = -WorldHalf + 3f; v < WorldHalf - 2f; v += 6f)
            {
                if (Mathf.Abs(v) < SafeR + 2f) continue;
                Prim(PrimitiveType.Cube, "DashX", world, new Vector3(v, 0.03f, 0), new Vector3(2.5f, 0.02f, 0.22f), new Color(0.95f, 0.8f, 0.2f), false);
                Prim(PrimitiveType.Cube, "DashZ", world, new Vector3(0, 0.032f, v), new Vector3(0.22f, 0.02f, 2.5f), new Color(0.95f, 0.8f, 0.2f), false);
            }
            Color fence = new Color(0.25f, 0.25f, 0.28f);
            Prim(PrimitiveType.Cube, "FenceN", world, new Vector3(0, 3, WorldHalf + 0.5f), new Vector3(WorldHalf * 2f + 2f, 6, 1), fence, true);
            Prim(PrimitiveType.Cube, "FenceS", world, new Vector3(0, 3, -WorldHalf - 0.5f), new Vector3(WorldHalf * 2f + 2f, 6, 1), fence, true);
            Prim(PrimitiveType.Cube, "FenceE", world, new Vector3(WorldHalf + 0.5f, 3, 0), new Vector3(1, 6, WorldHalf * 2f + 2f), fence, true);
            Prim(PrimitiveType.Cube, "FenceW", world, new Vector3(-WorldHalf - 0.5f, 3, 0), new Vector3(1, 6, WorldHalf * 2f + 2f), fence, true);
        }

        void BuildBase()
        {
            float by = 0f;
            Prim(PrimitiveType.Cylinder, "SafeZone", world, new Vector3(0, by + 0.04f, 0), new Vector3(SafeR * 2f, 0.02f, SafeR * 2f), new Color(0.15f, 0.8f, 0.3f), false);
            Prim(PrimitiveType.Cube, "CrossH", world, new Vector3(0, by + 0.07f, 0), new Vector3(4f, 0.02f, 1.1f), Color.white, false);
            Prim(PrimitiveType.Cube, "CrossV", world, new Vector3(0, by + 0.07f, 0), new Vector3(1.1f, 0.02f, 4f), Color.white, false);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 pp = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (SafeR - 0.3f) + Vector3.up * by;
                Prim(PrimitiveType.Cylinder, "Pole", world, pp + Vector3.up * 2.5f, new Vector3(0.12f, 2.5f, 0.12f), new Color(0.9f, 0.9f, 0.9f), false);
                Prim(PrimitiveType.Cube, "Flag", world, pp + new Vector3(0.45f, 4.5f, 0), new Vector3(0.9f, 0.55f, 0.05f), new Color(0.15f, 0.8f, 0.3f), false);
            }
            var tent = Prim(PrimitiveType.Cube, "Tent", world, new Vector3(9f, 1.25f, 7f), new Vector3(5f, 2.5f, 4f), new Color(0.92f, 0.92f, 0.95f), true);
            Prim(PrimitiveType.Cube, "TentRoof", world, new Vector3(9f, 2.7f, 7f), new Vector3(5.4f, 0.3f, 4.4f), new Color(0.85f, 0.15f, 0.15f), false);
            Prim(PrimitiveType.Cube, "TentCrossA", world, new Vector3(9f, 1.6f, 4.95f), new Vector3(1.6f, 0.4f, 0.05f), new Color(0.85f, 0.1f, 0.1f), false);
            Prim(PrimitiveType.Cube, "TentCrossB", world, new Vector3(9f, 1.6f, 4.94f), new Vector3(0.4f, 1.6f, 0.05f), new Color(0.85f, 0.1f, 0.1f), false);
            blocks.Add(tent.GetComponent<Renderer>().bounds);
            Prim(PrimitiveType.Cylinder, "HeliPad", world, new Vector3(-12f, 0.04f, 10f), new Vector3(10f, 0.04f, 10f), new Color(0.2f, 0.2f, 0.22f), false);
            Prim(PrimitiveType.Cube, "H1", world, new Vector3(-13.2f, 0.08f, 10f), new Vector3(0.5f, 0.02f, 3f), Color.white, false);
            Prim(PrimitiveType.Cube, "H2", world, new Vector3(-10.8f, 0.08f, 10f), new Vector3(0.5f, 0.02f, 3f), Color.white, false);
            Prim(PrimitiveType.Cube, "H3", world, new Vector3(-12f, 0.08f, 10f), new Vector3(2.4f, 0.02f, 0.5f), Color.white, false);
            BuildHeli(new Vector3(-12f, 0f, 10f), false);
            blocks.Add(new Bounds(new Vector3(-12f, 1f, 10f), new Vector3(10f, 2f, 10f)));
            patrolHeli = BuildHeli(new Vector3(0f, 32f, 0f), true);
        }

        void BuildProps()
        {
            // power poles with wires
            float zp = -6.9f;
            for (float x = -45f; x <= 45f; x += 15f)
            {
                Prim(PrimitiveType.Cylinder, "PowerPole", world, new Vector3(x, 4f, zp), new Vector3(0.28f, 4f, 0.28f), new Color(0.3f, 0.2f, 0.12f), false);
                Prim(PrimitiveType.Cube, "CrossArm", world, new Vector3(x, 7.6f, zp), new Vector3(0.2f, 0.2f, 2.4f), new Color(0.25f, 0.17f, 0.1f), false);
                if (x < 45f)
                    for (int w = -1; w <= 1; w += 2)
                        Prim(PrimitiveType.Cube, "Wire", world, new Vector3(x + 7.5f, 7.45f - 0.2f, zp + w * 0.9f), new Vector3(15f, 0.04f, 0.04f), new Color(0.05f, 0.05f, 0.05f), false);
            }
            // traffic lights at the crossroads
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? 1 : -1) * 5.6f, sz = (i < 2 ? 1 : -1) * 5.6f;
                Prim(PrimitiveType.Cylinder, "TLPole", world, new Vector3(sx, 2.2f, sz), new Vector3(0.12f, 2.2f, 0.12f), new Color(0.2f, 0.2f, 0.22f), false);
                Prim(PrimitiveType.Cube, "TLBox", world, new Vector3(sx, 4.6f, sz), new Vector3(0.4f, 1.1f, 0.4f), new Color(0.08f, 0.08f, 0.08f), false);
                Prim(PrimitiveType.Sphere, "TLRed", world, new Vector3(sx, 4.95f, sz), new Vector3(0.28f, 0.28f, 0.28f), new Color(0.9f, 0.1f, 0.1f), false);
                Prim(PrimitiveType.Sphere, "TLYellow", world, new Vector3(sx, 4.6f, sz), new Vector3(0.28f, 0.28f, 0.28f), new Color(0.35f, 0.3f, 0.05f), false);
                Prim(PrimitiveType.Sphere, "TLGreen", world, new Vector3(sx, 4.25f, sz), new Vector3(0.28f, 0.28f, 0.28f), new Color(0.05f, 0.3f, 0.1f), false);
            }
            // street lamps
            for (float x = -45f; x <= 45f; x += 18f)
            {
                if (Mathf.Abs(x) < 8f) continue;
                MakeLamp(new Vector3(x, 0, 5.2f));
                MakeLamp(new Vector3(x + 9f, 0, -5.2f));
            }
            // hydrants, benches, dumpsters
            for (int i = 0; i < 8; i++)
            {
                Vector3 hp = new Vector3(Random.Range(-48f, 48f), 0f, (i % 2 == 0 ? 6.4f : -6.4f));
                if (Mathf.Abs(hp.x) < 7f) continue;
                Prim(PrimitiveType.Cylinder, "Hydrant", world, hp + Vector3.up * 0.4f, new Vector3(0.3f, 0.4f, 0.3f), new Color(0.85f, 0.1f, 0.1f), false);
                Prim(PrimitiveType.Sphere, "HydrantCap", world, hp + Vector3.up * 0.85f, new Vector3(0.32f, 0.2f, 0.32f), new Color(0.85f, 0.1f, 0.1f), false);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 bp = FreeSpot(10f, WorldHalf - 3f, 1f);
                float by = Random.Range(0f, 360f);
                var bench = new GameObject("Bench").transform;
                bench.SetParent(world, false); bench.position = bp; bench.rotation = Quaternion.Euler(0, by, 0);
                Prim(PrimitiveType.Cube, "Seat", bench, new Vector3(0, 0.5f, 0), new Vector3(1.8f, 0.1f, 0.5f), new Color(0.4f, 0.27f, 0.15f), false);
                Prim(PrimitiveType.Cube, "Back", bench, new Vector3(0, 0.85f, -0.22f), new Vector3(1.8f, 0.5f, 0.08f), new Color(0.4f, 0.27f, 0.15f), false);
                Prim(PrimitiveType.Cube, "Legs", bench, new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.5f, 0.1f), new Color(0.15f, 0.15f, 0.15f), false);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 dp = FreeSpot(12f, WorldHalf - 4f, 2f);
                var d = Prim(PrimitiveType.Cube, "Dumpster", world, new Vector3(dp.x, 0.6f, dp.z), new Vector3(2.4f, 1.2f, 1.3f), new Color(0.12f, 0.35f, 0.18f), true);
                d.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                blocks.Add(d.GetComponent<Renderer>().bounds);
            }
        }

        void BuildBuildings(int count, int houses)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = FreeSpot(14f, WorldHalf - 6f, 3f);
                float w = Random.Range(5f, 10f), h = Random.Range(4f, 16f), d = Random.Range(5f, 10f);
                Color c = Color.HSVToRGB(Random.Range(0.02f, 0.13f), Random.Range(0.1f, 0.35f), Random.Range(0.55f, 0.95f));
                var b = Prim(PrimitiveType.Cube, "Building", world, new Vector3(p.x, h / 2f, p.z), new Vector3(w, h, d), c, true);
                b.GetComponent<Renderer>().sharedMaterial = TexMat(c, windowTex, new Vector2(Mathf.Max(1f, w / 10f), Mathf.Max(1f, h / 10f)));
                float yawB = Random.Range(0f, 90f);
                b.transform.rotation = Quaternion.Euler(0f, yawB, disaster == Disaster.Earthquake ? EarthquakeBuildingTilt() : 0f);
                blocks.Add(b.GetComponent<Renderer>().bounds);
                Prim(PrimitiveType.Cube, "RoofBox", b.transform, new Vector3(Random.Range(-0.25f, 0.25f), 0.53f, Random.Range(-0.25f, 0.25f)), new Vector3(0.25f, 0.08f, 0.25f), new Color(0.25f, 0.25f, 0.27f), false);
                if (disaster == Disaster.Earthquake) EarthquakeDamageBuilding(p, w, h, d, yawB);
            }
            for (int i = 0; i < houses; i++)
            {
                Vector3 p = FreeSpot(14f, WorldHalf - 6f, 3f);
                MakeHouse(p, Random.Range(0, 4) * 90f, Color.HSVToRGB(Random.value, Random.Range(0.1f, 0.35f), Random.Range(0.7f, 0.95f)));
            }
        }

        void MakeHouse(Vector3 p, float yawDeg, Color wall)
        {
            var root = new GameObject("House").transform;
            root.SetParent(world, false); root.position = p; root.rotation = Quaternion.Euler(0, yawDeg, 0);
            var body = Prim(PrimitiveType.Cube, "Walls", root, new Vector3(0, 1.75f, 0), new Vector3(7f, 3.5f, 6f), wall, true);
            body.GetComponent<Renderer>().sharedMaterial = TexMat(wall, windowTex, new Vector2(1.1f, 0.4f));
            Color roof = new Color(0.45f, 0.18f, 0.12f);
            var l = Prim(PrimitiveType.Cube, "RoofL", root, new Vector3(-1.75f, 4.45f, 0), new Vector3(4.1f, 0.2f, 6.6f), roof, false);
            l.transform.localRotation = Quaternion.Euler(0, 0, 28f);
            var r = Prim(PrimitiveType.Cube, "RoofR", root, new Vector3(1.75f, 4.45f, 0), new Vector3(4.1f, 0.2f, 6.6f), roof, false);
            r.transform.localRotation = Quaternion.Euler(0, 0, -28f);
            Prim(PrimitiveType.Cube, "Door", root, new Vector3(0, 1f, 3.02f), new Vector3(1.2f, 2f, 0.08f), new Color(0.25f, 0.15f, 0.08f), false);
            Prim(PrimitiveType.Cube, "Chimney", root, new Vector3(2f, 5.1f, -1.5f), new Vector3(0.7f, 1.6f, 0.7f), new Color(0.4f, 0.3f, 0.28f), false);
            blocks.Add(body.GetComponent<Renderer>().bounds);
        }

        void BuildTrees(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = FreeSpot(12f, WorldHalf - 3f, 2f);
                bool burnt = Random.value < 0.25f;
                float s = Random.Range(0.8f, 1.5f);
                Prim(PrimitiveType.Cylinder, "Trunk", world, new Vector3(p.x, 1.2f * s, p.z), new Vector3(0.45f * s, 1.2f * s, 0.45f * s), new Color(0.3f, 0.2f, 0.12f), true);
                Color leaf = burnt ? new Color(0.12f, 0.1f, 0.08f) : new Color(0.12f + Random.Range(0f, 0.1f), 0.4f + Random.Range(0f, 0.2f), 0.12f);
                Prim(PrimitiveType.Sphere, "Leaves", world, new Vector3(p.x, 3.2f * s, p.z), new Vector3(3.2f, 2.8f, 3.2f) * s, leaf, false);
                Prim(PrimitiveType.Sphere, "LeavesTop", world, new Vector3(p.x, 4.6f * s, p.z), new Vector3(2.0f, 1.8f, 2.0f) * s, leaf, false);
            }
        }

        void BuildCars(int n, int burning)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = FreeSpot(12f, WorldHalf - 4f, 2.5f);
                MakeCar(p, Random.Range(0f, 360f), i < burning);
            }
        }

        void BuildRubble(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = FreeSpot(8f, WorldHalf - 2f, 0f);
                var r = Prim(PrimitiveType.Cube, "Rubble", world, new Vector3(p.x, 0.25f, p.z), new Vector3(Random.Range(0.5f, 1.8f), Random.Range(0.3f, 0.8f), Random.Range(0.5f, 1.8f)), new Color(0.4f, 0.38f, 0.35f), false);
                r.transform.rotation = Random.rotation;
            }
        }

        float weatherLift = 5f;

        void SpawnHazards()
        {
            int n = 0;
            n = EarthquakeFireCount();
            for (int i = 0; i < n; i++)
            {
                Vector3 p = FreeSpot(10f, WorldHalf - 3f, 2f);
                MakeFire(p, Random.Range(0.9f, 1.4f), i < 6, true);
            }
        }

        void SpawnSurvivors()
        {
            totalThisLevel = Mathf.Min(24, 6 + level * 2);
            bool flood = disaster == Disaster.Flood;
            float[] scales = { 0.9f, 0.62f, 0.86f, 0.9f };
            float[] pitches = { 1f, 1.5f, 0.8f, 0.65f };
            for (int i = 0; i < totalThisLevel; i++)
            {
                float rv = Random.value;
                int kind = rv < 0.5f ? 0 : (rv < 0.72f ? 1 : (rv < 0.85f ? 2 : 3));
                Vector3 p = flood ? FreeSpot(16f, WorldHalf - 5f, 3f) : FreeSpot(14f, WorldHalf - 4f, 2.5f);
                float by = 0f;
                if (flood)
                {
                    by = 0f;
                }
                Color shirt = Color.HSVToRGB(Random.value, 0.6f, 0.9f);
                Color skin = Color.Lerp(new Color(0.95f, 0.8f, 0.65f), new Color(0.5f, 0.32f, 0.2f), Random.value);
                var rig = MakePerson("Survivor" + (i + 1), shirt, new Color(0.25f, 0.28f, 0.4f), skin, scales[kind]);
                rig.root.SetParent(world, false);
                float yawS = Random.Range(0f, 360f);
                if (kind == 3)
                {
                    rig.root.position = new Vector3(p.x, by + 0.22f, p.z);
                    rig.root.rotation = Quaternion.Euler(90f, yawS, 0f);
                    Prim(PrimitiveType.Cube, "Bandage", rig.root, new Vector3(0, 1.78f, 0), new Vector3(0.42f, 0.08f, 0.42f), Color.white, false);
                    Prim(PrimitiveType.Cube, "CrossA", rig.root, new Vector3(0, 1.12f, 0.18f), new Vector3(0.28f, 0.08f, 0.03f), new Color(0.9f, 0.1f, 0.1f), false);
                    Prim(PrimitiveType.Cube, "CrossB", rig.root, new Vector3(0, 1.12f, 0.18f), new Vector3(0.08f, 0.28f, 0.03f), new Color(0.9f, 0.1f, 0.1f), false);
                }
                else
                {
                    rig.root.position = new Vector3(p.x, by, p.z);
                    rig.root.rotation = Quaternion.Euler(0f, yawS, 0f);
                    if (kind == 2) Prim(PrimitiveType.Sphere, "Hair", rig.root, new Vector3(0, 1.86f, 0), new Vector3(0.42f, 0.2f, 0.42f), Color.white, false);
                }
                var beacon = Prim(PrimitiveType.Cylinder, "Beacon", world, new Vector3(p.x, by + 5f, p.z), new Vector3(0.12f, 5f, 0.12f), KindColors[kind], false);
                var va = rig.root.gameObject.AddComponent<AudioSource>();
                va.clip = cHelp; va.loop = false; va.spatialBlend = 1f; va.rolloffMode = AudioRolloffMode.Linear;
                va.minDistance = 3f; va.maxDistance = 40f; va.volume = 0.8f; va.pitch = pitches[kind]; va.playOnAwake = false;
                survivors.Add(new Survivor
                {
                    rig = rig, beacon = beacon.transform, voice = va, kind = kind, phase = Random.value * 6f,
                    crit = (110f - 6f * Mathf.Min(level, 6)) * timeMul, helpT = Random.Range(1f, 8f)
                });
            }
        }

        void SpawnPickups()
        {
            for (int i = 0; i < 8; i++)
            {
                int kind = i % 4;
                Vector3 p = FreeSpot(10f, WorldHalf - 4f, 1.5f);
                var root = new GameObject("Pickup" + kind).transform;
                root.SetParent(world, false); root.position = p + Vector3.up * 0.9f;
                Color glow;
                if (kind == 0)
                {
                    Prim(PrimitiveType.Cube, "Box", root, Vector3.zero, new Vector3(0.7f, 0.5f, 0.7f), Color.white, false);
                    Prim(PrimitiveType.Cube, "CrossA", root, Vector3.zero, new Vector3(0.72f, 0.14f, 0.2f), new Color(0.9f, 0.1f, 0.1f), false);
                    Prim(PrimitiveType.Cube, "CrossB", root, Vector3.zero, new Vector3(0.2f, 0.14f, 0.72f), new Color(0.9f, 0.1f, 0.1f), false);
                    glow = new Color(1f, 0.4f, 0.4f);
                }
                else if (kind == 1)
                {
                    Prim(PrimitiveType.Sphere, "Clock", root, Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f), new Color(0.2f, 0.6f, 1f), false);
                    glow = new Color(0.3f, 0.6f, 1f);
                }
                else if (kind == 2)
                {
                    Prim(PrimitiveType.Cylinder, "Can", root, Vector3.zero, new Vector3(0.35f, 0.4f, 0.35f), new Color(0.2f, 0.85f, 0.3f), false);
                    Prim(PrimitiveType.Cylinder, "Band", root, Vector3.zero, new Vector3(0.37f, 0.12f, 0.37f), Color.white, false);
                    glow = new Color(0.3f, 1f, 0.4f);
                }
                else
                {
                    Prim(PrimitiveType.Cylinder, "Canister", root, Vector3.zero, new Vector3(0.4f, 0.45f, 0.4f), new Color(0.2f, 0.45f, 0.95f), false);
                    Prim(PrimitiveType.Sphere, "Cap", root, new Vector3(0, 0.45f, 0), new Vector3(0.2f, 0.2f, 0.2f), new Color(0.85f, 0.85f, 0.9f), false);
                    glow = new Color(0.5f, 0.75f, 1f);
                }
                var gl = new GameObject("Glow").AddComponent<Light>();
                gl.transform.SetParent(root, false); gl.type = LightType.Point; gl.range = 6f; gl.intensity = 1.5f; gl.color = glow;
                pickups.Add(new Pickup { t = root, kind = kind });
            }
        }

        // =================================================================
        //  Environment (sky, fog, light, weather)
        // =================================================================
        /// <summary>Sky / fog / light values; every disaster file fills one in.</summary>
        struct Env
        {
            public Color skyTint, fog, sunCol, aSky, aEq, aGr;
            public float fogD, sunI, atmos, expo, sunPitch;
            public bool lamps;   // street lamps on even in daytime
        }

        void ApplyEnvironment()
        {
            Env v = EarthquakeEnv();
            Color skyTint = v.skyTint, fog = v.fog, sunCol = v.sunCol, aSky = v.aSky, aEq = v.aEq, aGr = v.aGr;
            float fogD = v.fogD, sunI = v.sunI, atmos = v.atmos, expo = v.expo, sunPitch = v.sunPitch;
            if (night)
            {
                skyTint = new Color(0.1f, 0.12f, 0.25f); fog = new Color(0.03f, 0.04f, 0.08f); fogD *= 1.5f; sunI = 0.12f;
                sunCol = new Color(0.4f, 0.5f, 0.9f); expo = 0.08f; sunPitch = 35f;
                aSky = new Color(0.08f, 0.1f, 0.2f); aEq = new Color(0.06f, 0.07f, 0.12f); aGr = new Color(0.03f, 0.03f, 0.05f);
            }
            if (skyMat == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null) skyMat = new Material(sh);
            }
            if (skyMat != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = skyMat;
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", skyTint);
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", fog);
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", atmos);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", expo);
                if (skyMat.HasProperty("_SunSize")) skyMat.SetFloat("_SunSize", 0.05f);
            }
            else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = skyTint; }
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogD;
            RenderSettings.ambientSkyColor = aSky;
            RenderSettings.ambientEquatorColor = aEq;
            RenderSettings.ambientGroundColor = aGr;
            sun.intensity = sunI; sunBase = sunI;
            sun.color = sunCol;
            sun.transform.rotation = Quaternion.Euler(sunPitch, -30f, 0f);
            bool lampsOn = night || v.lamps;
            foreach (var l in lamps) l.enabled = lampsOn;
            ConfigWeather();
        }

        void ConfigWeather()
        {
            weatherPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = weatherPS.main; var e = weatherPS.emission; var sh = weatherPS.shape;
            var f = weatherPS.forceOverLifetime; var r = weatherPS.GetComponent<ParticleSystemRenderer>();
            m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = 0f;
            sh.shapeType = ParticleSystemShapeType.Box;
            f.enabled = true; f.space = ParticleSystemSimulationSpace.World; f.x = 0f; f.y = 0f; f.z = 0f;
            r.sharedMaterial = PMat(false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            EarthquakeWeather(m, e, sh, r);
            weatherPS.Play();
        }

        /// <summary>Falling rain streaks, shared by flood (light) and hurricane (heavy).</summary>
        void ConfigRain(ParticleSystem.MainModule m, ParticleSystem.EmissionModule e, ParticleSystem.ShapeModule sh, ParticleSystemRenderer r, float rate)
        {
            m.maxParticles = 5000; m.startLifetime = 0.55f; m.startSpeed = new ParticleSystem.MinMaxCurve(24f, 28f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f); m.startColor = new Color(0.75f, 0.8f, 0.9f, 0.4f);
            e.rateOverTime = rate;
            sh.scale = new Vector3(45f, 45f, 1f); weatherLift = 14f;
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 2.5f;
        }

        // =================================================================
        //  Main loop
        // =================================================================
        void Update()
        {
            if (Inp.Mute()) AudioListener.volume = AudioListener.volume > 0.5f ? 0f : 1f;
            AudioListener.pause = state == State.Paused;

            switch (state)
            {
                case State.Menu:
                    {
                        int n = Inp.Num();
                        if (n >= 5 && n <= 7) { difficulty = n - 5; ApplyDifficulty(); Sfx(cClick, 0.6f); }
                        if (Inp.Next()) { Sfx(cClick, 0.6f); StartGame(); }
                        break;
                    }
                case State.Playing:
                    if (Inp.Pause()) { state = State.Paused; SetCursor(false); }
                    else Tick();
                    break;
                case State.Paused:
                    if (Inp.Pause()) { state = State.Playing; SetCursor(true); }
                    if (Inp.Restart()) StartGame();
                    if (Inp.MenuKey()) GoMenu();
                    break;
                case State.LevelDone:
                    if (Inp.Next()) { level++; BuildLevel(false); }
                    if (Inp.MenuKey()) GoMenu();
                    break;
                case State.GameOver:
                    if (Inp.Restart()) StartGame();
                    if (Inp.MenuKey()) GoMenu();
                    break;
            }
            AnimateWorld();
            UpdateAmbientAudio();
        }

        void Tick()
        {
            float dt = Time.deltaTime;
            Look();
            Move(dt);
            Extinguisher(dt);
            Interact();
            UpdateHazards(dt);
            UpdatePickups();
            UpdateDisaster(dt);
            UpdateFlyers(dt);
            UpdateSurvivors(dt);
            if (state != State.Playing) return;

            if (Inp.Flash()) { flashOn = !flashOn; Sfx(cClick, 0.5f); }
            if (Inp.Medkit()) UseMedkit();

            if (comboTimer > 0f) { comboTimer -= dt; if (comboTimer <= 0f) combo = 0; }
            if (boostT > 0f) boostT -= dt;
            timeLeft -= dt;
            if (timeLeft < 10f && timeLeft > 0f)
            {
                int secLeft = Mathf.CeilToInt(timeLeft);
                if (secLeft != lastBeepSec) { lastBeepSec = secLeft; Sfx(cBeep, 0.5f); }
            }
            if (timeLeft <= 0f) { timeLeft = 0f; Lose("Time ran out."); }
            if (hurt > 0f) hurt -= dt * 1.5f;
            if (toastT > 0f) toastT -= dt;
        }

        void Look()
        {
            Vector2 d = Inp.MouseDelta();
            yaw += d.x * MouseSens;
            pitch = Mathf.Clamp(pitch - d.y * MouseSens, -5f, 60f);
        }

        void Move(float dt)
        {
            Vector3 input = new Vector3(Inp.Horizontal(), 0f, Inp.Vertical());
            if (input.sqrMagnitude > 1f) input.Normalize();
            bool moving = input.sqrMagnitude > 0.01f;
            bool sprint = Inp.Sprint() && moving && stamina > 1f;
            float depth = 0f;
            float speed = sprint ? SprintSpeed : WalkSpeed;
            if (carried != null) speed *= KindCarry[carried.kind];
            if (boostT > 0f) speed *= 1.25f;

            float regen = 18f;
            stamina = sprint ? Mathf.Max(0f, stamina - 25f * dt) : Mathf.Min(MaxStamina, stamina + regen * dt);

            Vector3 horiz = Vector3.zero;
            if (moving)
            {
                float target = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg + yaw;
                float ang = Mathf.SmoothDampAngle(player.transform.eulerAngles.y, target, ref turnVel, 0.07f);
                player.transform.rotation = Quaternion.Euler(0f, ang, 0f);
                horiz = Quaternion.Euler(0f, target, 0f) * Vector3.forward * speed * input.magnitude;
            }
            Vector3 push = DisasterPush(depth);

            if (cc.isGrounded && vy < 0f) vy = -2f;
            if (cc.isGrounded && Inp.Jump()) { vy = Mathf.Sqrt(JumpHeight * -2f * Gravity); Sfx(cJump, 0.5f); }
            vy += Gravity * dt;
            float prevVy = vy;
            cc.Move((horiz + push + Vector3.up * vy) * dt);
            bool g = cc.isGrounded;
            if (g && !wasGrounded && prevVy < -6f) Sfx(depth > 0.2f ? cSplash : cLand, 0.6f);
            wasGrounded = g;

            float amount = moving ? (sprint ? 1.25f : 0.8f) : 0f;
            walkPhase += dt * (sprint ? 13f : 9f) * (moving ? 1f : 0f);
            Animate(prig, walkPhase, amount, carried != null);

            int stepIdx = Mathf.FloorToInt(walkPhase / Mathf.PI);
            if (moving && g && stepIdx != lastStep) { lastStep = stepIdx; Sfx(depth > 0.2f ? cSplash : cStep, sprint ? 0.5f : 0.32f); }

            fovCur = Mathf.Lerp(fovCur, sprint ? 72f : 62f, 1f - Mathf.Exp(-6f * dt));
            cam.fieldOfView = fovCur;
        }

        void Extinguisher(float dt)
        {
            bool inBase = Flat(player.transform.position, Vector3.zero) < SafeR;
            if (inBase && water < MaxWater) water = Mathf.Min(MaxWater, water + 45f * dt);

            bool spraying = Inp.Spray() && water > 0f && carried == null;
            var em = sprayPS.emission; em.rateOverTime = spraying ? 140f : 0f;
            if (!spraying) return;

            water = Mathf.Max(0f, water - 28f * dt);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 aim = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 pp = player.transform.position;
            foreach (var h in hazards)
            {
                if (h.isOut) continue;
                Vector3 to = h.t.position - pp; to.y = 0f;
                float dist = to.magnitude;
                if (dist < 10f && dist > 0.1f && Vector3.Dot(to / dist, aim) > 0.82f)
                {
                    h.hp -= 45f * dt;
                    if (h.hp <= 0f) PutOut(h);
                }
            }
        }

        void PutOut(Hazard h)
        {
            h.isOut = true; h.hp = 0f;
            var e1 = h.fire.emission; e1.enabled = false;
            var e2 = h.embers.emission; e2.enabled = false;
            var e3 = h.smoke.emission; e3.rateOverTime = 4f;
            if (h.light != null) h.light.enabled = false;
            if (h.audio != null) h.audio.Stop();
            if (h.core != null) h.core.SetActive(false);
            score += 25; firesOut++;
            ShowToast("Fire extinguished!  +25", 2f);
            AudioSource.PlayClipAtPoint(cHiss, h.t.position, 0.9f);
            Burst(h.t.position + Vector3.up * 0.8f, new Color(0.9f, 0.9f, 0.95f, 0.6f), 30, 3f, 1.4f, false);
        }

        void UseMedkit()
        {
            Vector3 pp = player.transform.position;
            Survivor tgt = null;
            if (carried != null && carried.kind == 3 && !carried.stab) tgt = carried;
            else
            {
                float bd = 3.4f;
                foreach (var s in survivors)
                {
                    if (s.rescued || s.lost || s.kind != 3 || s.stab || s == carried) continue;
                    float d = Vector3.Distance(pp, s.rig.root.position);
                    if (d < bd) { bd = d; tgt = s; }
                }
            }
            if (tgt != null)
            {
                if (medkits <= 0) { ShowToast("No medkits to stabilize the injured!", 2f); return; }
                medkits--; tgt.stab = true;
                ShowToast("Injured survivor stabilized!", 2.5f);
                Sfx(cMedkit, 0.8f);
                return;
            }
            if (medkits > 0 && health < MaxHealth) { medkits--; health = Mathf.Min(MaxHealth, health + 40f); ShowToast("Medkit used (+40 HP)", 2f); Sfx(cMedkit, 0.7f); }
            else ShowToast(medkits <= 0 ? "No medkits left!" : "Health already full", 1.5f);
        }

        void Interact()
        {
            if (!Inp.Use()) return;
            Vector3 pp = player.transform.position;

            if (carried != null)
            {
                if (Flat(pp, Vector3.zero) <= SafeR + 1f)
                {
                    var done = carried; carried = null;
                    if (comboTimer > 0f) combo++; else combo = 0;
                    int pts = KindPoints[done.kind] + 50 * combo;
                    comboTimer = 45f;
                    rescuedThisLevel++;
                    score += pts;
                    done.rescued = true;
                    Destroy(done.rig.root.gameObject);
                    Destroy(done.beacon.gameObject);
                    ShowToast(KindNames[done.kind] + " rescued!  +" + pts + (combo > 0 ? "  (COMBO x" + (combo + 1) + ")" : ""), 2.5f);
                    Sfx(cRescue, 0.8f);
                    Burst(pp + Vector3.up * 1.5f, new Color(0.4f, 1f, 0.5f, 0.9f), 40, 5f, 0.35f, true);
                    CheckEnd();
                }
                else
                {
                    var drop = carried; carried = null;
                    drop.rig.root.SetParent(world, true);
                    Vector3 dp = pp + player.transform.forward * 1.6f;
                    if (drop.kind == 3)
                    {
                        drop.rig.root.position = new Vector3(dp.x, pp.y + 0.22f, dp.z);
                        drop.rig.root.rotation = Quaternion.Euler(90f, yaw, 0f);
                    }
                    else
                    {
                        drop.rig.root.position = new Vector3(dp.x, pp.y, dp.z);
                        drop.rig.root.rotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                    drop.beacon.position = drop.rig.root.position + Vector3.up * 5f;
                    drop.beacon.gameObject.SetActive(true);
                    ShowToast("Survivor dropped - take them to the Safe Zone", 2.5f);
                    Sfx(cLand, 0.4f);
                }
                return;
            }

            Survivor best = null; float bd2 = PickupRange;
            foreach (var s in survivors)
            {
                if (s.rescued || s.lost) continue;
                float d = Vector3.Distance(pp, s.rig.root.position);
                if (d < bd2) { bd2 = d; best = s; }
            }
            if (best != null)
            {
                carried = best;
                best.beacon.gameObject.SetActive(false);
                best.rig.root.SetParent(player.transform, false);
                best.rig.root.localPosition = new Vector3(0f, 1.25f, -0.55f);
                best.rig.root.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ShowToast(KindNames[best.kind] + " picked up! Head to the green Safe Zone", 3f);
                Sfx(cPickup, 0.7f);
            }
        }

        void CheckEnd()
        {
            if (rescuedThisLevel + lostCount < totalThisLevel) return;
            if (rescuedThisLevel > 0) CompleteLevel();
            else Lose("Nobody was rescued.");
        }

        void LoseSurvivor(Survivor s)
        {
            s.lost = true; lostCount++;
            if (s.beacon != null) Destroy(s.beacon.gameObject);
            if (carried == s) { carried = null; Destroy(s.rig.root.gameObject); }
            else if (s.kind != 3)
            {
                s.rig.root.rotation = Quaternion.Euler(90f, s.rig.root.eulerAngles.y, 0f);
                s.rig.root.position += Vector3.up * 0.22f;
            }
            ShowToast("A survivor did not make it...  (" + lostCount + "/" + (maxLost + 1) + " lost)", 3.5f);
            Sfx(cGameOver, 0.45f);
            if (lostCount > maxLost) { Lose("Too many casualties."); return; }
            CheckEnd();
        }

        void UpdateSurvivors(float dt)
        {
            Vector3 pp = player.transform.position;
            for (int i = 0; i < survivors.Count; i++)
            {
                var s = survivors[i];
                if (s.rescued || s.lost) continue;
                if (s.kind == 3 && !s.stab)
                {
                    s.crit -= dt * (carried == s ? 0.5f : 1f);
                    if (s.crit <= 0f) { LoseSurvivor(s); if (state != State.Playing) return; continue; }
                }
                if (s != carried)
                {
                    s.helpT -= dt;
                    if (s.helpT <= 0f)
                    {
                        s.helpT = Random.Range(6f, 10f);
                        if (Vector3.Distance(pp, s.rig.root.position) < 38f && s.voice != null && !s.voice.isPlaying) s.voice.Play();
                    }
                }
            }
        }

        void UpdateHazards(float dt)
        {
            Vector3 pp = player.transform.position;
            foreach (var h in hazards)
            {
                if (h.isOut) continue;
                if (Flat(pp, h.t.position) < h.radius * Mathf.Lerp(0.6f, 1f, h.hp / 100f) && pp.y < 2.4f + h.t.position.y) Damage(24f * dt);
            }
        }

        void UpdatePickups()
        {
            Vector3 pp = player.transform.position;
            foreach (var p in pickups)
            {
                if (p.taken) continue;
                if (Flat(pp, p.t.position) < 1.5f && Mathf.Abs(pp.y + 0.9f - p.t.position.y) < 2f)
                {
                    p.taken = true; p.t.gameObject.SetActive(false);
                    if (p.kind == 0) { medkits++; ShowToast("Medkit collected (+1)", 2f); }
                    else if (p.kind == 1) { timeLeft += 20f; timeTotal += 20f; ShowToast("+20 seconds!", 2f); }
                    else if (p.kind == 2) { stamina = MaxStamina; boostT = 12f; ShowToast("Energy boost! Faster for 12s", 2.5f); }
                    else { water = MaxWater; ShowToast("Extinguisher refilled", 2f); }
                    Sfx(cPickup, 0.8f);
                }
            }
        }

        void Damage(float a)
        {
            health -= a * dmgMul;
            hurt = Mathf.Max(hurt, 0.35f);
            if (Time.time - lastHurtSfx > 0.45f) { lastHurtSfx = Time.time; Sfx(cHurt, 0.6f); }
            if (health <= 0f && state == State.Playing) { health = 0f; Lose("You were overwhelmed by the disaster."); }
        }

        void CompleteLevel()
        {
            int bonus = Mathf.CeilToInt(timeLeft) * 3;
            score += bonus;
            stars = 1 + (lostCount == 0 ? 1 : 0) + (timeLeft > timeTotal * 0.25f ? 1 : 0);
            endMessage = "Time bonus: +" + bonus;
            SaveHi();
            state = State.LevelDone;
            Sfx(cFanfare, 0.9f);
            SetCursor(false);
        }

        void Lose(string why)
        {
            if (state == State.GameOver) return;
            endMessage = why;
            SaveHi();
            state = State.GameOver;
            Sfx(cGameOver, 0.9f);
            SetCursor(false);
        }

        void SaveHi()
        {
            int d = (int)disaster;
            if (score > hiScores[d]) { hiScores[d] = score; PlayerPrefs.SetInt("DRS_HI_" + d, score); PlayerPrefs.Save(); }
        }

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Sfx(AudioClip c, float vol) { if (sfx != null && c != null) sfx.PlayOneShot(c, vol); }

        // =================================================================
        //  Disaster simulation
        // =================================================================
        readonly List<Transform> rotors = new List<Transform>();

        void UpdateDisaster(float dt)
        {
            UpdateAftershocks(dt);
        }

        // =================================================================
        //  Disaster hooks (this game only has one disaster)
        // =================================================================
        /// <summary>Per-frame ambient loop volumes that the disaster can ask for.</summary>
        struct Amb { public float rumble, rain, water, roar, wind; }

        Color DisasterGroundTint() { return EarthquakeGroundTint(); }

        /// <summary>Extra push on the player (wind, river current).</summary>
        Vector3 DisasterPush(float waterDepth) { return Vector3.zero; }

        /// <summary>Per-frame visuals that also run in the menu.</summary>
        void AnimateDisaster(float dt, bool menu) {  }

        Vector3 DisasterCameraShake() { return EarthquakeCameraShake(); }

        void DisasterAmbient(ref Amb a) { EarthquakeAmbient(ref a); }

        /// <summary>Full-screen effect under the HUD.</summary>
        void DisasterScreenFx(float W, float H) {  }

        /// <summary>Big warning text under the timer.</summary>
        void DisasterBanner(float W) { EarthquakeBanner(W); }

        void UpdateFlyers(float dt)
        {
            Vector3 pp = player.transform.position;
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                if (f.t == null) { flyers.RemoveAt(i); continue; }
                f.life -= dt;
                Vector3 pos = f.t.position + f.vel * dt;
                f.t.Rotate(f.spin * dt, f.spin * 0.6f * dt, 0f);
                f.t.position = pos;
                float d = Flat(pp, pos);
                float dy = Mathf.Abs(pos.y - (pp.y + 0.9f));
                if (d < 1.3f && dy < 1.6f)
                {
                    Damage(f.log ? 12f : 14f);
                    Sfx(cLand, 0.7f);
                    Destroy(f.t.gameObject); flyers.RemoveAt(i);
                    continue;
                }
                if (f.life <= 0f || Mathf.Abs(pos.x) > WorldHalf + 12f || Mathf.Abs(pos.z) > WorldHalf + 12f)
                {
                    Destroy(f.t.gameObject); flyers.RemoveAt(i);
                }
            }
        }

        // =================================================================
        //  Visual animation
        // =================================================================
        void AnimateWorld()
        {
            float dt = Time.deltaTime;
            animT += dt;
            bool menu = state == State.Menu;

            AnimateDisaster(dt, menu);

            foreach (var h in hazards)
            {
                if (h.isOut) continue;
                float hpf = h.hp / 100f;
                float n = Mathf.PerlinNoise(animT * 7f, h.phase);
                var em = h.fire.emission; em.rateOverTime = 42f * h.scale * hpf;
                var em2 = h.embers.emission; em2.rateOverTime = 9f * hpf;
                var em3 = h.smoke.emission; em3.rateOverTime = 6f * hpf + 2f;
                if (h.light != null) h.light.intensity = (2.2f + 2f * n) * hpf * h.scale;
                if (h.audio != null) h.audio.volume = 0.6f * hpf;
                if (h.core != null) h.core.transform.localScale = new Vector3(0.9f, 0.7f + n * 0.7f, 0.9f) * h.scale * Mathf.Max(0.2f, hpf);
            }
            foreach (var p in pickups)
            {
                if (p.taken) continue;
                p.t.Rotate(0f, 90f * dt, 0f);
                var pos = p.t.position; pos.y = 0.9f + Mathf.Sin(animT * 2.5f) * 0.15f; p.t.position = pos;
            }
            foreach (var s in survivors)
            {
                if (s.rescued || s.lost || s == carried) continue;
                float w = Mathf.Sin(animT * (s.kind == 1 ? 8f : 6f) + s.phase);
                if (s.kind == 3)
                {
                    s.rig.rArm.localRotation = Quaternion.Euler(-60f + w * 15f, 0f, 0f);
                    s.rig.lArm.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                }
                else
                {
                    s.rig.rArm.localRotation = Quaternion.Euler(0f, 0f, 165f + w * 18f);
                    s.rig.lArm.localRotation = Quaternion.Euler(0f, 0f, -8f);
                }
                if (s.beacon != null) s.beacon.localScale = new Vector3(0.12f, 5f * (1f + Mathf.Sin(animT * 4f + s.phase) * 0.15f), 0.12f);
            }
            rotors.RemoveAll(r => r == null);
            foreach (var r in rotors) r.Rotate(0f, 900f * dt, 0f);
            if (patrolHeli != null)
            {
                float a = animT * 0.15f;
                patrolHeli.position = new Vector3(Mathf.Cos(a) * 42f, 32f + Mathf.Sin(animT * 0.7f) * 1.5f, Mathf.Sin(a) * 42f);
                patrolHeli.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            if (flashlight != null)
            {
                flashlight.enabled = flashOn && state != State.Menu;
                if (cam != null) flashlight.transform.rotation = cam.transform.rotation;
            }
            if (weatherPS != null)
            {
                Vector3 c = menu ? Vector3.zero : player.transform.position;
                weatherPS.transform.position = c + Vector3.up * weatherLift;
            }
        }

        void UpdateAmbientAudio()
        {
            if (ambSpray == null) return;
            float dt = Time.unscaledDeltaTime;
            bool menu = state == State.Menu;
            bool spraying = state == State.Playing && Inp.Spray() && water > 0f && carried == null;
            ambSpray.volume = Mathf.MoveTowards(ambSpray.volume, spraying ? 0.45f : 0f, dt * 4f);
            ambHeart.volume = (state == State.Playing && health < 30f) ? 0.7f : 0f;
            ambMusic.volume = menu ? 0.45f : (night ? 0.45f : 0.3f);

            // loops that depend on the disaster (see the XxxAmbient methods)
            Amb a = new Amb { wind = 0.3f };
            DisasterAmbient(ref a);
            ambRumble.volume = Mathf.MoveTowards(ambRumble.volume, a.rumble, dt * 2f);
            ambRain.volume = Mathf.MoveTowards(ambRain.volume, a.rain, dt);
            ambWater.volume = a.water;
            ambRoar.volume = a.roar;
            ambWind.volume = a.wind;
        }

        // =================================================================
        //  Camera
        // =================================================================
        void LateUpdate()
        {
            if (cam == null || player == null) return;
            if (state == State.Menu)
            {
                menuAngle += Time.deltaTime * 0.07f;
                Vector3 mp = new Vector3(Mathf.Cos(menuAngle) * 52f, 26f, Mathf.Sin(menuAngle) * 52f);
                cam.transform.position = Vector3.Lerp(cam.transform.position, mp, 1f - Mathf.Exp(-3f * Time.deltaTime));
                cam.transform.LookAt(new Vector3(0f, 3f, 0f));
                cam.fieldOfView = 62f;
                return;
            }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = player.transform.position + Vector3.up * 1.7f;
            Vector3 desired = focus - rot * Vector3.forward * CamDist;
            RaycastHit hit;
            if (Physics.Linecast(focus, desired, out hit) && hit.collider.gameObject != player)
                desired = hit.point + hit.normal * 0.25f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-18f * Time.deltaTime));
            if (state == State.Playing) cam.transform.position += DisasterCameraShake();
            cam.transform.LookAt(focus);
        }

        // =================================================================
        //  GUI: menu + HUD
        // =================================================================
        void OnGUI()
        {
            if (player == null) return;
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s, H = 720f;
            if (state == State.Menu) { DrawMenu(W, H); return; }
            DrawHud(W, H);
            if (state != State.Playing) DrawOverlay(W, H);
        }

        bool Btn(Rect r, string label, bool selected, int size)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, selected ? new Color(0.95f, 0.4f, 0.1f, 0.92f) : new Color(0.08f, 0.09f, 0.12f, hover ? 0.9f : 0.72f));
            Txt(new Rect(r.x, r.y + (r.height - size) / 2f - 3f, r.width, r.height), label, size, Color.white, TextAnchor.UpperCenter);
            return GUI.Button(r, "", GUIStyle.none);
        }

        void DrawMenu(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            Txt(new Rect(0, 30, W, 70), "EARTHQUAKE RESCUE", 54, Color.white, TextAnchor.UpperCenter);
            Txt(new Rect(0, 100, W, 30), "Pick a difficulty and save as many lives as you can", 19, new Color(1f, 0.85f, 0.5f), TextAnchor.UpperCenter);

            float cw = 270f, ch = 200f, gap = 18f;
            float x0 = (W - cw) / 2f, y0 = 160f;
            for (int i = (int)disaster; i == (int)disaster; i++)
            {
                Rect r = new Rect(x0, y0, cw, ch);
                bool sel = (int)disaster == i;
                bool hover = r.Contains(Event.current.mousePosition);
                Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), sel ? new Color(1f, 0.6f, 0.2f, 0.95f) : new Color(1, 1, 1, 0.08f));
                Fill(r, new Color(0.07f, 0.08f, 0.11f, hover ? 0.95f : 0.85f));
                Txt(new Rect(r.x + 12, r.y + 8, cw - 20, 34), DNames[i], 24, sel ? new Color(1f, 0.75f, 0.3f) : Color.white);
                Txt(new Rect(r.x + 12, r.y + 48, cw - 24, 110), DDesc[i], 15, new Color(0.85f, 0.88f, 0.95f), TextAnchor.UpperLeft, true);
                Txt(new Rect(r.x + 12, r.y + ch - 30, cw - 20, 24), "Best score: " + hiScores[i], 15, new Color(1f, 0.9f, 0.4f));
            }

            Txt(new Rect(0, 382, W, 28), "DIFFICULTY", 18, new Color(1, 1, 1, 0.8f), TextAnchor.UpperCenter);
            float bw = 150f, bg = 14f, bx = (W - (3f * bw + 2f * bg)) / 2f;
            for (int d = 0; d < 3; d++)
            {
                if (Btn(new Rect(bx + d * (bw + bg), 410f, bw, 40f), "[" + (5 + d) + "] " + DiffNames[d], difficulty == d, 18))
                { difficulty = d; ApplyDifficulty(); Sfx(cClick, 0.6f); }
            }
            if (Btn(new Rect(W / 2f - 190f, 475f, 380f, 64f), "START MISSION  (Enter)", false, 26))
            { Sfx(cClick, 0.6f); StartGame(); }

            Fill(new Rect(W / 2f - 420f, 560f, 840f, 128f), new Color(0f, 0f, 0f, 0.5f));
            Txt(new Rect(0, 566, W, 24), "HOW TO PLAY", 16, new Color(1f, 0.8f, 0.4f), TextAnchor.UpperCenter);
            Txt(new Rect(0, 590, W, 22), "WASD move | Shift sprint | Space jump | E pick up / drop survivor | Mouse look", 15, Color.white, TextAnchor.UpperCenter);
            Txt(new Rect(0, 612, W, 22), "LMB / X spray water on fires | F medkit (heal yourself or stabilize an INJURED survivor) | Q flashlight", 15, Color.white, TextAnchor.UpperCenter);
            Txt(new Rect(0, 634, W, 22), "Carry survivors to the green SAFE ZONE. Injured survivors (red) have a bleed-out timer!", 15, new Color(1f, 0.7f, 0.5f), TextAnchor.UpperCenter);
            Txt(new Rect(0, 656, W, 22), "Yellow = adult, Cyan = child, Purple = elderly, Red = injured      M mute  |  Tab main menu", 14, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter);
        }

        void DrawHud(float W, float H)
        {
            if (hurt > 0f) Fill(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, Mathf.Clamp01(hurt) * 0.5f));
            DisasterScreenFx(W, H);

            Fill(new Rect(14, 14, 290, 232), new Color(0, 0, 0, 0.45f));
            Bar(new Rect(24, 24, 270, 20), health / MaxHealth, new Color(0.9f, 0.2f, 0.2f));
            Txt(new Rect(28, 24, 240, 20), "HEALTH " + Mathf.CeilToInt(health), 14, Color.white);
            Bar(new Rect(24, 50, 270, 12), stamina / MaxStamina, new Color(0.95f, 0.8f, 0.2f));
            Bar(new Rect(24, 66, 270, 12), water / MaxWater, new Color(0.3f, 0.65f, 1f));
            Txt(new Rect(24, 80, 280, 22), "Medkits (F): " + medkits + "     Water (LMB/X)", 15, Color.white);
            Txt(new Rect(24, 104, 280, 24), "Rescued: " + rescuedThisLevel + " / " + totalThisLevel + "    Lost: " + lostCount + "/" + (maxLost + 1), 17, Color.white);
            Txt(new Rect(24, 128, 280, 24), "Score: " + score + "   Best: " + Mathf.Max(hiScores[(int)disaster], score), 16, Color.white);
            Txt(new Rect(24, 152, 280, 22), "Fires out: " + firesOut, 15, new Color(1f, 0.7f, 0.4f));
            float minCrit = float.MaxValue;
            foreach (var sv in survivors)
                if (!sv.rescued && !sv.lost && sv.kind == 3 && !sv.stab && sv.crit < minCrit) minCrit = sv.crit;
            if (minCrit < float.MaxValue)
                Txt(new Rect(24, 172, 280, 22), "CRITICAL: " + Mathf.CeilToInt(minCrit) + "s  (F near them)", 15, minCrit < 25f ? Color.red : new Color(1f, 0.5f, 0.4f));
            if (combo > 0) Txt(new Rect(24, 192, 280, 22), "COMBO x" + (combo + 1) + "  (" + Mathf.CeilToInt(comboTimer) + "s)", 16, new Color(0.4f, 1f, 0.5f));
            if (boostT > 0f) Txt(new Rect(24, 212, 280, 22), "BOOST " + Mathf.CeilToInt(boostT) + "s", 15, new Color(0.4f, 1f, 0.4f));
            else if (carried != null) Txt(new Rect(24, 212, 280, 22), "Carrying " + KindNames[carried.kind].ToLower() + "!", 15, new Color(1f, 0.85f, 0.2f));

            int m = Mathf.FloorToInt(timeLeft / 60f), sec = Mathf.FloorToInt(timeLeft % 60f);
            Txt(new Rect(0, 12, W, 30), DNames[(int)disaster] + (night ? " (NIGHT)" : "") + "  -  LEVEL " + level, 20, Color.white, TextAnchor.UpperCenter);
            Txt(new Rect(0, 36, W, 40), string.Format("{0:00}:{1:00}", m, sec), 34, timeLeft < 30f ? Color.red : Color.white, TextAnchor.UpperCenter);
            if (toastT > 0f) Txt(new Rect(0, 86, W, 30), toast, 22, new Color(1f, 0.9f, 0.4f), TextAnchor.UpperCenter);
            DisasterBanner(W);

            Radar(W);
            DisasterPanel(W);
            GuideArrow(W);

            if (state == State.Playing)
            {
                string prompt = "";
                if (carried != null)
                    prompt = Flat(player.transform.position, Vector3.zero) <= SafeR + 1f ? "Press E to hand over survivor" : "Take the survivor to the green Safe Zone  (E to drop)";
                else
                    foreach (var sv in survivors)
                    {
                        if (sv.rescued || sv.lost) continue;
                        if (Vector3.Distance(player.transform.position, sv.rig.root.position) < PickupRange)
                        {
                            prompt = "Press E to pick up (" + KindNames[sv.kind] + ")" + (sv.kind == 3 && !sv.stab ? "  |  F to stabilize" : "");
                            break;
                        }
                    }
                if (prompt.Length > 0) Txt(new Rect(0, H - 120, W, 30), prompt, 22, Color.white, TextAnchor.UpperCenter);
                Txt(new Rect(14, H - 28, 1200, 22), "WASD move | Shift sprint | Space jump | E pick up/drop | LMB/X spray | F medkit | Q flashlight | M mute | Esc pause", 13, new Color(1, 1, 1, 0.7f));
            }
        }

        void DisasterPanel(float W)
        {
            float x = W - 166f, y = 190f;
            Fill(new Rect(x, y, 150, 62), new Color(0, 0, 0, 0.5f));
            EarthquakePanel(x, y);
        }

        void DrawOverlay(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            string title, l1, l2;
            Color tc = Color.white;
            if (state == State.Paused) { title = "PAUSED"; l1 = "Esc - resume     R - restart"; l2 = "Tab - main menu"; }
            else if (state == State.LevelDone) { title = "LEVEL " + level + " COMPLETE"; l1 = endMessage + "     Score: " + score + "     Lost: " + lostCount; l2 = "Press Enter for the next level   |   Tab - main menu"; tc = new Color(0.3f, 1f, 0.4f); }
            else { title = "MISSION FAILED"; l1 = endMessage + "     Final score: " + score + "   Level: " + level; l2 = "Press R to restart   |   Tab - main menu"; tc = new Color(1f, 0.3f, 0.3f); }
            Txt(new Rect(0, H / 2f - 110, W, 60), title, 48, tc, TextAnchor.UpperCenter);
            Txt(new Rect(0, H / 2f - 40, W, 30), l1, 22, Color.white, TextAnchor.UpperCenter);
            Txt(new Rect(0, H / 2f + 70, W, 30), l2, 20, Color.white, TextAnchor.UpperCenter);
            if (state == State.LevelDone)
            {
                for (int i = 0; i < 3; i++)
                    Fill(new Rect(W / 2f - 75f + i * 52f, H / 2f + 4f, 42f, 42f), i < stars ? new Color(1f, 0.85f, 0.15f) : new Color(0.3f, 0.3f, 0.3f));
                Txt(new Rect(0, H / 2f + 48f, W, 22), "Rating: " + stars + " / 3", 16, Color.white, TextAnchor.UpperCenter);
            }
            if (state != State.Paused) Txt(new Rect(0, H / 2f + 100f, W, 30), "Best score (" + DNames[(int)disaster] + "): " + hiScores[(int)disaster], 18, new Color(1f, 0.9f, 0.4f), TextAnchor.UpperCenter);
        }

        void GuideArrow(float W)
        {
            Vector3 pp = player.transform.position, target = Vector3.zero;
            string label = "BASE";
            if (carried == null)
            {
                float bd = float.MaxValue; bool found = false;
                foreach (var sv in survivors)
                {
                    if (sv.rescued || sv.lost) continue;
                    float d = Flat(pp, sv.rig.root.position);
                    if (d < bd) { bd = d; target = sv.rig.root.position; found = true; }
                }
                if (!found) return;
                label = "SURVIVOR";
            }
            Vector3 to = target - pp; to.y = 0f;
            float ang = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - yaw;
            float cx = W / 2f, cy = 190f;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy));
            Color c = carried != null ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.9f, 0.2f);
            for (int i = 0; i < 6; i++) Fill(new Rect(cx - (2 + i * 2.5f), cy - 24 + i * 4, 4 + i * 5f, 4), c);
            Fill(new Rect(cx - 3, cy, 6, 18), c);
            GUI.matrix = saved;
            Txt(new Rect(cx - 100, cy + 24, 200, 22), label + "  " + Mathf.RoundToInt(to.magnitude) + " m", 15, Color.white, TextAnchor.UpperCenter);
        }

        void Radar(float W)
        {
            float size = 150f, x = W - size - 16f, y = 16f, range = 45f;
            Fill(new Rect(x, y, size, size), new Color(0, 0, 0, 0.55f));
            Fill(new Rect(x + size / 2f - 0.5f, y, 1f, size), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, y + size / 2f - 0.5f, size, 1f), new Color(1, 1, 1, 0.12f));
            Vector3 pp = player.transform.position;
            RadarDot(pp, Vector3.zero, new Color(0.2f, 1f, 0.4f), 8f, x, y, size, range);
            foreach (var h in hazards) if (!h.isOut) RadarDot(pp, h.t.position, new Color(1f, 0.25f, 0.1f), 5f, x, y, size, range);
            foreach (var p in pickups) if (!p.taken) RadarDot(pp, p.t.position, p.kind == 0 ? Color.white : (p.kind == 1 ? new Color(0.3f, 0.7f, 1f) : (p.kind == 2 ? new Color(0.3f, 1f, 0.4f) : new Color(0.5f, 0.6f, 1f))), 5f, x, y, size, range);
            foreach (var sv in survivors) if (!sv.rescued && !sv.lost && sv != carried) RadarDot(pp, sv.rig.root.position, KindColors[sv.kind], 7f, x, y, size, range);
            Fill(new Rect(x + size / 2f - 4f, y + size / 2f - 4f, 8f, 8f), Color.cyan);
            Txt(new Rect(x, y + size, size, 18), "RADAR (N up)", 12, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter);
        }

        void RadarDot(Vector3 pp, Vector3 wp, Color c, float sz, float x, float y, float size, float range)
        {
            float half = size / 2f - sz / 2f;
            float dx = Mathf.Clamp((wp.x - pp.x) / range * (size / 2f), -half, half);
            float dz = Mathf.Clamp(-(wp.z - pp.z) / range * (size / 2f), -half, half);
            Fill(new Rect(x + size / 2f + dx - sz / 2f, y + size / 2f + dz - sz / 2f, sz, sz), c);
        }

        void Bar(Rect r, float frac, Color c)
        {
            Fill(r, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), c);
        }

        void Fill(Rect r, Color c)
        {
            Color o = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = o;
        }

        void Txt(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool wrap = false)
        {
            if (txt == null) { txt = new GUIStyle(GUI.skin.label); txt.fontStyle = FontStyle.Bold; }
            txt.fontSize = size; txt.alignment = a; txt.wordWrap = wrap;
            Rect sh = r; sh.x += 1.5f; sh.y += 1.5f;
            txt.normal.textColor = Color.black; GUI.Label(sh, s, txt);
            txt.normal.textColor = c; GUI.Label(r, s, txt);
        }

        void ShowToast(string t, float secs) { toast = t; toastT = secs; }

        // =================================================================
        //  Fire, smoke and particle effects
        // =================================================================
        ParticleSystem MakePS(Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static Gradient Fade(Color[] cols, float[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[cols.Length];
            var ak = new GradientAlphaKey[cols.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                float t = cols.Length == 1 ? 0f : i / (float)(cols.Length - 1);
                ck[i] = new GradientColorKey(cols[i], t);
                ak[i] = new GradientAlphaKey(alphas[i], t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        Hazard MakeFire(Vector3 p, float scale, bool withLight, bool charBase)
        {
            var root = new GameObject("Hazard_Fire").transform;
            root.SetParent(world, false); root.position = p;
            if (charBase) Prim(PrimitiveType.Cube, "Base", root, new Vector3(0, 0.25f, 0), new Vector3(1.9f * scale, 0.5f, 1.9f * scale), new Color(0.1f, 0.07f, 0.05f), false);
            var core = Prim(PrimitiveType.Cube, "Core", root, new Vector3(0, 0.8f * scale, 0), new Vector3(0.9f, 1f, 0.9f) * scale, new Color(1f, 0.5f, 0.08f), false);

            var fire = MakePS(root, "Flames", new Vector3(0, 0.4f, 0), Quaternion.Euler(-90f, 0f, 0f));
            var fm = fire.main; fm.loop = true; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f); fm.startSize = new ParticleSystem.MinMaxCurve(0.9f * scale, 1.7f * scale);
            fm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            fm.gravityModifier = -0.15f; fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.maxParticles = 250;
            var fe = fire.emission; fe.rateOverTime = 42f * scale;
            var fs = fire.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 14f; fs.radius = 0.55f * scale;
            var fc = fire.colorOverLifetime; fc.enabled = true;
            fc.color = Fade(new[] { new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0.1f), new Color(0.9f, 0.2f, 0.05f), new Color(0.2f, 0.05f, 0.02f) }, new[] { 0.9f, 0.8f, 0.5f, 0f });
            var fz = fire.sizeOverLifetime; fz.enabled = true; fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat(true);
            fire.Play();

            var smoke = MakePS(root, "Smoke", new Vector3(0, 1.4f * scale, 0), Quaternion.Euler(-90f, 0f, 0f));
            var sm = smoke.main; sm.loop = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); sm.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            sm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            sm.gravityModifier = -0.05f; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 120;
            var se = smoke.emission; se.rateOverTime = 8f;
            var ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 18f; ss.radius = 0.4f;
            var sc = smoke.colorOverLifetime; sc.enabled = true;
            sc.color = Fade(new[] { new Color(0.12f, 0.12f, 0.12f), new Color(0.25f, 0.25f, 0.27f), new Color(0.4f, 0.4f, 0.42f) }, new[] { 0.5f, 0.35f, 0f });
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat(false);
            smoke.Play();

            var emb = MakePS(root, "Embers", new Vector3(0, 0.6f, 0), Quaternion.Euler(-90f, 0f, 0f));
            var em = emb.main; em.loop = true; em.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f); em.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            em.startColor = new Color(1f, 0.7f, 0.2f, 1f); em.gravityModifier = -0.05f; em.simulationSpace = ParticleSystemSimulationSpace.World; em.maxParticles = 80;
            var ee = emb.emission; ee.rateOverTime = 9f;
            var es = emb.shape; es.shapeType = ParticleSystemShapeType.Cone; es.angle = 28f; es.radius = 0.5f * scale;
            var ec = emb.colorOverLifetime; ec.enabled = true;
            ec.color = Fade(new[] { new Color(1f, 0.9f, 0.4f), new Color(1f, 0.4f, 0.05f) }, new[] { 1f, 0f });
            emb.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat(true);
            emb.Play();

            Light lt = null;
            if (withLight)
            {
                var lg = new GameObject("FireLight");
                lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0, 2f, 0);
                lt = lg.AddComponent<Light>();
                lt.type = LightType.Point; lt.color = new Color(1f, 0.55f, 0.2f); lt.range = 12f * scale; lt.intensity = 3f;
            }

            var a = root.gameObject.AddComponent<AudioSource>();
            a.clip = cFire; a.loop = true; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 2f; a.maxDistance = 28f; a.volume = 0.6f; a.playOnAwake = false;
            a.time = Random.value * 1.9f;
            a.Play();

            var h = new Hazard { t = root, phase = Random.value * 6f, radius = 2.2f * scale, scale = scale, fire = fire, smoke = smoke, embers = emb, light = lt, audio = a, core = core };
            hazards.Add(h);
            return h;
        }

        void Burst(Vector3 pos, Color col, int count, float speed, float size, bool additive)
        {
            var g = new GameObject("Burst");
            g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = false; m.playOnAwake = false; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            m.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            m.startColor = col; m.gravityModifier = 0.35f; m.maxParticles = count + 10;
            var e = ps.emission; e.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var cl = ps.colorOverLifetime; cl.enabled = true;
            cl.color = Fade(new[] { Color.white, Color.white }, new[] { 1f, 0f });
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = PMat(additive);
            ps.Play();
            ps.Emit(count);
            Destroy(g, 3.5f);
        }

        Material PMat(bool additive)
        {
            Material m = additive ? pmatAdd : pmatAlpha;
            if (m != null) return m;
            Shader sh = null;
            if (additive)
            {
                sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh == null) sh = Shader.Find("Mobile/Particles/Additive");
            }
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) { Mat(Color.white); sh = baseShader; }
            m = new Material(sh);
            m.mainTexture = softTex;
            if (additive) pmatAdd = m; else pmatAlpha = m;
            return m;
        }

        // =================================================================
        //  Procedural textures
        // =================================================================
        void BuildTextures()
        {
            softTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            softTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            softTex.Apply();

            windowTex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            windowTex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16;
                    float noise = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    Color c = new Color(0.8f, 0.78f, 0.75f) * noise; c.a = 1f;
                    bool win = lx >= 4 && lx <= 11 && ly >= 5 && ly <= 12;
                    if (win)
                    {
                        float hh = Mathf.Abs(Mathf.Sin(cx * 12.9898f + cy * 78.233f));
                        c = hh > 0.8f ? new Color(1f, 0.85f, 0.45f) : new Color(0.12f, 0.15f, 0.2f);
                        if (ly == 12 || lx == 4) c *= 0.7f;
                        c.a = 1f;
                    }
                    windowTex.SetPixel(x, y, c);
                }
            windowTex.Apply();

            groundTex = new Texture2D(128, 128, TextureFormat.RGBA32, true);
            groundTex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.5f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.5f;
                    float v = 0.55f + n * 0.4f;
                    groundTex.SetPixel(x, y, new Color(v, v * 0.97f, v * 0.92f, 1f));
                }
            groundTex.Apply();

            waterTex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            waterTex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.6f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.4f;
                    float v = 0.65f + n * 0.35f;
                    waterTex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            waterTex.Apply();
        }

        Material TexMat(Color tint, Texture2D tex, Vector2 tile)
        {
            if (baseShader == null) Mat(Color.white);
            var m = new Material(baseShader);
            m.color = tint;
            m.mainTexture = tex;
            m.mainTextureScale = tile;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tile); }
            levelMats.Add(m);
            return m;
        }

        // =================================================================
        //  Procedural audio (no audio files needed)
        // =================================================================
        AudioClip Synth(string name, float dur, System.Func<float, float> f)
        {
            int n = (int)(SR * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static float Note(float t, float freq, float t0, float len)
        {
            float x = t - t0;
            if (x < 0f || x > len) return 0f;
            return Mathf.Sin(2f * Mathf.PI * freq * x) * Mathf.Exp(-x * (5f / len)) * 0.5f;
        }

        AudioSource Loop(AudioClip c, float vol)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = true; a.volume = vol; a.playOnAwake = false; a.spatialBlend = 0f;
            a.Play();
            return a;
        }

        void BuildAudio()
        {
            float twoPi = 2f * Mathf.PI;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;

            cStep = Synth("step", 0.12f, t => (Random.Range(-1f, 1f) * 0.5f + Mathf.Sin(twoPi * 110f * t) * 0.5f) * Mathf.Exp(-t * 35f) * 0.7f);
            cJump = Synth("jump", 0.18f, t => Mathf.Sin(twoPi * (220f + 900f * t) * t) * Mathf.Exp(-t * 12f) * 0.4f);
            cLand = Synth("land", 0.25f, t => (Mathf.Sin(twoPi * 70f * t) * 0.8f + Random.Range(-1f, 1f) * 0.2f) * Mathf.Exp(-t * 16f));
            cPickup = Synth("pickup", 0.4f, t => Note(t, 880f, 0f, 0.15f) + Note(t, 1320f, 0.08f, 0.3f));
            cRescue = Synth("rescue", 0.8f, t => Note(t, 523f, 0f, 0.3f) + Note(t, 659f, 0.12f, 0.3f) + Note(t, 784f, 0.24f, 0.3f) + Note(t, 1047f, 0.36f, 0.4f));
            cMedkit = Synth("medkit", 0.45f, t => Mathf.Sin(twoPi * (500f + 900f * t) * t) * Mathf.Exp(-t * 6f) * 0.5f);
            cHurt = Synth("hurt", 0.3f, t => (Random.Range(-1f, 1f) * 0.6f + Mathf.Sin(twoPi * 140f * t) * 0.5f) * Mathf.Exp(-t * 14f));
            cBoom = Synth("boom", 0.9f, t => Mathf.Sin(twoPi * 55f * t) * Mathf.Exp(-t * 6f) + Random.Range(-1f, 1f) * Mathf.Exp(-t * 20f) * 0.6f);
            cBeep = Synth("beep", 0.12f, t => Mathf.Sin(twoPi * 1000f * t) * Mathf.Exp(-t * 12f) * 0.5f);
            cClick = Synth("click", 0.06f, t => Random.Range(-1f, 1f) * Mathf.Exp(-t * 80f) * 0.6f);
            cFanfare = Synth("fanfare", 1.5f, t => Note(t, 523f, 0f, 0.4f) + Note(t, 659f, 0.15f, 0.4f) + Note(t, 784f, 0.3f, 0.4f) + Note(t, 1047f, 0.45f, 0.5f) + Note(t, 784f, 0.7f, 0.4f) + Note(t, 1047f, 0.85f, 0.6f));
            cGameOver = Synth("gameover", 1.7f, t => Note(t, 440f, 0f, 0.5f) + Note(t, 392f, 0.3f, 0.5f) + Note(t, 349f, 0.6f, 0.5f) + Note(t, 294f, 0.9f, 0.8f));
            float lpH = 0f;
            cHiss = Synth("hiss", 0.9f, t => { lpH = lpH * 0.5f + Random.Range(-1f, 1f) * 0.5f; return lpH * Mathf.Exp(-t * 3f) * 0.6f; });
            float lpSp = 0f;
            cSplash = Synth("splash", 0.3f, t => { lpSp = lpSp * 0.6f + Random.Range(-1f, 1f) * 0.4f; return lpSp * Mathf.Exp(-t * 10f) * 0.9f; });
            cHeart = Synth("heart", 0.9f, t =>
            {
                float t2 = Mathf.Max(0f, t - 0.3f);
                return (Mathf.Sin(twoPi * 60f * t) * Mathf.Exp(-t * 20f) + (t > 0.3f ? Mathf.Sin(twoPi * 55f * t2) * Mathf.Exp(-t2 * 20f) : 0f)) * 0.8f;
            });
            float phH = 0f;
            cHelp = Synth("help", 0.8f, t =>
            {
                float fr = 300f + 250f * Mathf.Sin(Mathf.PI * t / 0.8f) + 8f * Mathf.Sin(twoPi * 6f * t);
                phH += twoPi * fr / SR;
                float env = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t / 0.8f)));
                return (Mathf.Sin(phH) + 0.5f * Mathf.Sin(2f * phH) + 0.25f * Mathf.Sin(3f * phH)) * env * 0.28f;
            });
            float lpT = 0f;
            cThunder = Synth("thunder", 3.5f, t =>
            {
                lpT = lpT * 0.99f + Random.Range(-1f, 1f) * 0.01f;
                float env = Mathf.Exp(-t * 0.9f) * Mathf.Min(1f, t / 0.1f);
                return lpT * 22f * env * 0.7f + Random.Range(-1f, 1f) * Mathf.Exp(-t * 10f) * 0.5f;
            });

            // loops
            float lpF = 0f, pop = 0f;
            cFire = Synth("fire", 2f, t =>
            {
                lpF = lpF * 0.9f + Random.Range(-1f, 1f) * 0.1f;
                if (Random.value < 0.002f) pop = 1f;
                pop *= 0.995f;
                return lpF * 1.6f * 0.6f + Random.Range(-1f, 1f) * pop * 0.5f;
            });
            float lpW = 0f;
            cWind = Synth("wind", 4f, t =>
            {
                lpW = lpW * 0.985f + Random.Range(-1f, 1f) * 0.015f;
                return lpW * 9f * (0.6f + 0.4f * Mathf.Sin(twoPi * 0.5f * t)) * 0.5f;
            });
            float lpR = 0f;
            cRumble = Synth("rumble", 2f, t =>
            {
                lpR = lpR * 0.995f + Random.Range(-1f, 1f) * 0.005f;
                return lpR * 25f * 0.8f + Mathf.Sin(twoPi * 38f * t) * 0.3f;
            });
            float ph = 0f;
            cSiren = Synth("siren", 4f, t =>
            {
                float fr = 650f + 250f * Mathf.Sin(twoPi * t / 4f);
                ph += twoPi * fr / SR;
                return Mathf.Sin(ph) * 0.25f;
            });
            float lpS = 0f;
            cSprayLoop = Synth("spray", 1f, t => { lpS = lpS * 0.6f + Random.Range(-1f, 1f) * 0.4f; return lpS * 0.35f; });
            cMusic = Synth("music", 8f, t =>
            {
                float trem = 0.7f + 0.3f * Mathf.Sin(twoPi * 0.5f * t);
                return (Mathf.Sin(twoPi * 55f * t) * 0.25f + Mathf.Sin(twoPi * 82.5f * t) * 0.15f + Mathf.Sin(twoPi * 110f * t) * 0.08f + Mathf.Sin(twoPi * 131.25f * t) * 0.05f) * trem;
            });
            float lpC = 0f;
            cChop = Synth("chop", 0.5f, t =>
            {
                float env = Mathf.Exp(-(t % 0.25f) * 30f);
                lpC = lpC * 0.8f + Random.Range(-1f, 1f) * 0.2f;
                return (lpC * 1.5f + Mathf.Sin(twoPi * 60f * t) * 0.5f) * env * 0.8f;
            });
            float lpRn = 0f;
            cRain = Synth("rain", 2f, t => { lpRn = lpRn * 0.3f + Random.Range(-1f, 1f) * 0.7f; return lpRn * 0.35f; });
            float lpWt = 0f;
            cWaterLoop = Synth("waterloop", 3f, t =>
            {
                lpWt = lpWt * 0.92f + Random.Range(-1f, 1f) * 0.08f;
                return lpWt * 2.2f * (0.7f + 0.3f * Mathf.Sin(twoPi * 2f * t / 3f)) * 0.5f;
            });
            float lpRo = 0f;
            cRoar = Synth("roar", 3f, t =>
            {
                lpRo = lpRo * 0.97f + Random.Range(-1f, 1f) * 0.03f;
                return lpRo * 8f * (0.8f + 0.2f * Mathf.Sin(twoPi * 2f * t / 3f));
            });

            ambWind = Loop(cWind, 0.3f);
            ambMusic = Loop(cMusic, 0.4f);
            ambRumble = Loop(cRumble, 0f);
            ambSpray = Loop(cSprayLoop, 0f);
            ambHeart = Loop(cHeart, 0f);
            ambRain = Loop(cRain, 0f);
            ambWater = Loop(cWaterLoop, 0f);
            ambRoar = Loop(cRoar, 0f);
        }

        // =================================================================
        //  Helpers: primitives, people, vehicles, placement
        // =================================================================
        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>Picks a shader that exists in builds (URP / HDRP / Built-in).</summary>
        static Shader FindLitShader()
        {
            string[] names = { "Universal Render Pipeline/Lit", "HDRP/Lit", "Standard", "Universal Render Pipeline/Simple Lit", "Sprites/Default" };
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null && s.isSupported) return s;
            }
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fb = tmp.GetComponent<Renderer>().sharedMaterial.shader;
            Object.DestroyImmediate(tmp);
            return fb;
        }

        Material Mat(Color c)
        {
            Material m;
            if (mats.TryGetValue(c, out m)) return m;
            if (baseShader == null) baseShader = FindLitShader();
            m = new Material(baseShader);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            mats[c] = m;
            return m;
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color c, bool collider)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            if (!collider) { var col = g.GetComponent<Collider>(); if (col != null) DestroyImmediate(col); }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return g;
        }

        Transform Limb(Transform root, string n, Vector3 pivot, Vector3 size, Color c)
        {
            var p = new GameObject(n).transform;
            p.SetParent(root, false);
            p.localPosition = pivot;
            Prim(PrimitiveType.Cube, n + "Mesh", p, new Vector3(0f, -size.y / 2f, 0f), size, c, false);
            return p;
        }

        Rig MakePerson(string name, Color shirt, Color pants, Color skin, float scale)
        {
            var r = new Rig();
            r.root = new GameObject(name).transform;
            Prim(PrimitiveType.Cube, "Torso", r.root, new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.7f, 0.35f), shirt, false);
            Prim(PrimitiveType.Sphere, "Head", r.root, new Vector3(0, 1.72f, 0), new Vector3(0.4f, 0.4f, 0.4f), skin, false);
            r.lArm = Limb(r.root, "LArm", new Vector3(-0.42f, 1.42f, 0), new Vector3(0.18f, 0.62f, 0.18f), shirt);
            r.rArm = Limb(r.root, "RArm", new Vector3(0.42f, 1.42f, 0), new Vector3(0.18f, 0.62f, 0.18f), shirt);
            r.lLeg = Limb(r.root, "LLeg", new Vector3(-0.15f, 0.75f, 0), new Vector3(0.24f, 0.75f, 0.26f), pants);
            r.rLeg = Limb(r.root, "RLeg", new Vector3(0.15f, 0.75f, 0), new Vector3(0.24f, 0.75f, 0.26f), pants);
            r.root.localScale = Vector3.one * scale;
            return r;
        }

        void Animate(Rig r, float phase, float amount, bool armsForward)
        {
            float sw = Mathf.Sin(phase) * 45f * amount;
            r.lLeg.localRotation = Quaternion.Euler(sw, 0f, 0f);
            r.rLeg.localRotation = Quaternion.Euler(-sw, 0f, 0f);
            if (armsForward)
            {
                r.lArm.localRotation = Quaternion.Euler(-75f, 0f, 0f);
                r.rArm.localRotation = Quaternion.Euler(-75f, 0f, 0f);
            }
            else
            {
                r.lArm.localRotation = Quaternion.Euler(-sw * 0.9f, 0f, 0f);
                r.rArm.localRotation = Quaternion.Euler(sw * 0.9f, 0f, 0f);
            }
        }

        void MakeCar(Vector3 p, float yawDeg, bool burning)
        {
            var root = new GameObject(burning ? "BurningCar" : "Car").transform;
            root.SetParent(world, false);
            root.position = p;
            root.rotation = Quaternion.Euler(0, yawDeg, 0);
            Color body = burning ? new Color(0.08f, 0.07f, 0.07f) : Color.HSVToRGB(Random.value, 0.65f, 0.8f);
            Prim(PrimitiveType.Cube, "Body", root, new Vector3(0, 0.65f, 0), new Vector3(2f, 0.8f, 4.2f), body, true);
            Prim(PrimitiveType.Cube, "Cabin", root, new Vector3(0, 1.3f, -0.2f), new Vector3(1.7f, 0.65f, 2.2f), burning ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.55f, 0.7f, 0.85f), false);
            Vector3[] wheels = { new Vector3(-1.0f, 0.35f, 1.3f), new Vector3(1.0f, 0.35f, 1.3f), new Vector3(-1.0f, 0.35f, -1.3f), new Vector3(1.0f, 0.35f, -1.3f) };
            foreach (var wp in wheels)
            {
                var wh = Prim(PrimitiveType.Cylinder, "Wheel", root, wp, new Vector3(0.7f, 0.12f, 0.7f), new Color(0.05f, 0.05f, 0.05f), false);
                wh.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            blocks.Add(new Bounds(p, new Vector3(5f, 2f, 5f)));
            if (burning) { var hz = MakeFire(p + Vector3.up * 0.9f, 1f, true, false); hz.radius = 3f; }
        }

        void MakeLamp(Vector3 p)
        {
            Prim(PrimitiveType.Cylinder, "LampPole", world, p + Vector3.up * 3f, new Vector3(0.15f, 3f, 0.15f), new Color(0.2f, 0.2f, 0.22f), false);
            Prim(PrimitiveType.Cube, "LampHead", world, p + new Vector3(0, 6.05f, 0), new Vector3(0.7f, 0.15f, 0.4f), new Color(1f, 0.95f, 0.7f), false);
            var lg = new GameObject("LampLight");
            lg.transform.SetParent(world, false);
            lg.transform.position = p + new Vector3(0, 5.8f, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 20f; l.intensity = 2.5f; l.color = new Color(1f, 0.9f, 0.65f);
            l.enabled = false;
            lamps.Add(l);
        }

        Transform BuildHeli(Vector3 p, bool patrol)
        {
            var root = new GameObject(patrol ? "PatrolHeli" : "Helicopter").transform;
            root.SetParent(world, false); root.position = p; root.rotation = Quaternion.Euler(0, 40f, 0);
            Color red = patrol ? new Color(0.9f, 0.9f, 0.92f) : new Color(0.85f, 0.12f, 0.12f);
            Prim(PrimitiveType.Sphere, "Body", root, new Vector3(0, 1.5f, 0), new Vector3(2.2f, 2f, 4f), red, false);
            Prim(PrimitiveType.Sphere, "Glass", root, new Vector3(0, 1.8f, 1.2f), new Vector3(1.8f, 1.4f, 1.6f), new Color(0.5f, 0.75f, 0.9f), false);
            Prim(PrimitiveType.Cube, "Tail", root, new Vector3(0, 1.8f, -3.5f), new Vector3(0.35f, 0.4f, 3.5f), red, false);
            Prim(PrimitiveType.Cube, "TailFin", root, new Vector3(0, 2.3f, -5.2f), new Vector3(0.15f, 1f, 0.8f), red, false);
            Prim(PrimitiveType.Cube, "SkidL", root, new Vector3(-0.9f, 0.1f, 0), new Vector3(0.12f, 0.12f, 3.4f), new Color(0.15f, 0.15f, 0.15f), false);
            Prim(PrimitiveType.Cube, "SkidR", root, new Vector3(0.9f, 0.1f, 0), new Vector3(0.12f, 0.12f, 3.4f), new Color(0.15f, 0.15f, 0.15f), false);
            Prim(PrimitiveType.Cylinder, "Mast", root, new Vector3(0, 2.7f, 0), new Vector3(0.15f, 0.3f, 0.15f), new Color(0.2f, 0.2f, 0.2f), false);
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(root, false); rotor.localPosition = new Vector3(0, 3.05f, 0);
            Prim(PrimitiveType.Cube, "Blade1", rotor, Vector3.zero, new Vector3(9f, 0.06f, 0.35f), new Color(0.12f, 0.12f, 0.12f), false);
            Prim(PrimitiveType.Cube, "Blade2", rotor, Vector3.zero, new Vector3(0.35f, 0.06f, 9f), new Color(0.12f, 0.12f, 0.12f), false);
            rotors.Add(rotor);
            var a = root.gameObject.AddComponent<AudioSource>();
            a.clip = cChop; a.loop = true; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 4f; a.maxDistance = patrol ? 90f : 45f; a.volume = patrol ? 0.5f : 0.35f; a.Play();
            if (!patrol)
            {
                var s = root.gameObject.AddComponent<AudioSource>();
                s.clip = cSiren; s.loop = true; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f; s.maxDistance = 70f; s.volume = 0.18f; s.Play();
            }
            return root;
        }

        /// <summary>Random point on the map outside existing buildings and at least minR from the base.</summary>
        Vector3 FreeSpot(float minR, float maxR, float margin)
        {
            for (int i = 0; i < 80; i++)
            {
                Vector3 p = new Vector3(Random.Range(-maxR, maxR), 0f, Random.Range(-maxR, maxR));
                if (p.magnitude < minR) continue;
                bool ok = true;
                for (int b = 0; b < blocks.Count; b++)
                {
                    Bounds bb = blocks[b]; bb.Expand(margin * 2f);
                    if (p.x > bb.min.x && p.x < bb.max.x && p.z > bb.min.z && p.z < bb.max.z) { ok = false; break; }
                }
                if (ok) return p;
            }
            return new Vector3(minR + 2f, 0f, minR + 2f);
        }

        // =================================================================
        //  EARTHQUAKE  (everything specific to this disaster)
        // =================================================================
        const string EarthquakeDesc = "The city has collapsed. Aftershocks rain debris, fires burn in the ruins and gas lines blaze.";

        class Strike { public Vector3 pos; public float time; public GameObject marker, rock; }

        readonly List<Strike> strikes = new List<Strike>();
        float shakeTimer, nextShake, strikeTimer;

        // =================================================================
        //  Level setup
        // =================================================================
        void ResetEarthquake()
        {
            strikes.Clear();
            shakeTimer = 0f; strikeTimer = 0f;
        }

        /// <summary>Schedules the first aftershock (called once the level is built).</summary>
        void ArmEarthquake()
        {
            nextShake = Random.Range(14f, 22f) * (1f - 0.04f * Mathf.Min(level, 8));
        }

        void BuildEarthquakeCity()
        {
            BuildBuildings(26 + level * 3, 8); BuildTrees(18); BuildCars(12, 3); BuildCracks();
        }

        int EarthquakeFireCount() { return Mathf.Min(12, 3 + level * 2); }

        Color EarthquakeGroundTint() { return new Color(0.75f, 0.72f, 0.68f); }

        /// <summary>Buildings lean a few degrees after the quake.</summary>
        float EarthquakeBuildingTilt() { return Random.Range(-4f, 4f); }

        /// <summary>Half of the buildings get a broken, tilted chunk on the roof.</summary>
        void EarthquakeDamageBuilding(Vector3 p, float w, float h, float d, float yawB)
        {
            if (Random.value >= 0.5f) return;
            var chunk = Prim(PrimitiveType.Cube, "BrokenTop", world, new Vector3(p.x, h + 0.7f, p.z), new Vector3(w * 0.5f, 1.4f, d * 0.5f), new Color(0.45f, 0.42f, 0.4f), false);
            chunk.transform.rotation = Quaternion.Euler(Random.Range(8f, 22f), yawB, Random.Range(-15f, 15f));
        }

        void BuildCracks()
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 p = new Vector3(Random.Range(-45f, 45f), 0.025f, Random.Range(-45f, 45f));
                var c = Prim(PrimitiveType.Cube, "Crack", world, p, new Vector3(0.3f, 0.02f, Random.Range(8f, 22f)), new Color(0.04f, 0.04f, 0.04f), false);
                c.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 180f), 0);
            }
        }

        // =================================================================
        //  Sky, fog and dust
        // =================================================================
        Env EarthquakeEnv()
        {
            return new Env
            {
                skyTint = new Color(0.55f, 0.5f, 0.45f), fog = new Color(0.68f, 0.64f, 0.6f), fogD = 0.008f,
                sunI = 1.15f, sunCol = new Color(1f, 0.92f, 0.8f), atmos = 1.3f, expo = 1.1f, sunPitch = 48f,
                aSky = new Color(0.65f, 0.72f, 0.85f), aEq = new Color(0.5f, 0.5f, 0.5f), aGr = new Color(0.3f, 0.27f, 0.25f),
                lamps = false
            };
        }

        void EarthquakeWeather(ParticleSystem.MainModule m, ParticleSystem.EmissionModule e, ParticleSystem.ShapeModule sh, ParticleSystemRenderer r)
        {
            // drifting dust
            m.maxParticles = 800; m.startLifetime = 6f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); m.startColor = new Color(0.7f, 0.65f, 0.55f, 0.3f);
            e.rateOverTime = 35f; sh.scale = new Vector3(30f, 30f, 1f); weatherLift = 3f;
        }

        // =================================================================
        //  Aftershocks
        // =================================================================
        void UpdateAftershocks(float dt)
        {
            nextShake -= dt;
            if (shakeTimer <= 0f && nextShake <= 0f)
            {
                shakeTimer = 3.5f;
                nextShake = Random.Range(16f, 26f) * (1f - 0.04f * Mathf.Min(level, 8));
                ShowToast("AFTERSHOCK!  Avoid the red markers!", 3f);
            }
            if (shakeTimer > 0f)
            {
                shakeTimer -= dt; strikeTimer -= dt;
                if (strikeTimer <= 0f)
                {
                    strikeTimer = 0.4f;
                    Vector3 pos = player.transform.position + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                    pos.x = Mathf.Clamp(pos.x, -WorldHalf + 2f, WorldHalf - 2f);
                    pos.z = Mathf.Clamp(pos.z, -WorldHalf + 2f, WorldHalf - 2f);
                    pos.y = 0.05f;
                    var mk = Prim(PrimitiveType.Cylinder, "Marker", world, pos, new Vector3(4.2f, 0.02f, 4.2f), new Color(1f, 0.1f, 0.1f), false);
                    float sz = Random.Range(1.4f, 2.4f);
                    var rock = Prim(PrimitiveType.Cube, "FallingRock", world, pos + Vector3.up * 25f, new Vector3(sz, sz * 0.7f, sz), new Color(0.45f, 0.42f, 0.4f), false);
                    rock.transform.rotation = Random.rotation;
                    strikes.Add(new Strike { pos = pos, time = 1.3f, marker = mk, rock = rock });
                }
            }
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                var st = strikes[i];
                st.time -= dt;
                float k = Mathf.Clamp01(1f - st.time / 1.3f);
                st.rock.transform.position = Vector3.Lerp(st.pos + Vector3.up * 25f, st.pos + Vector3.up * 0.6f, k * k);
                if (st.time > 0f) continue;
                if (Flat(player.transform.position, st.pos) < 2.2f && player.transform.position.y < 1.2f) Damage(26f);
                Destroy(st.marker);
                AudioSource.PlayClipAtPoint(cBoom, st.pos, 0.9f);
                Burst(st.pos + Vector3.up * 0.4f, new Color(0.6f, 0.55f, 0.5f, 0.7f), 35, 4f, 1.6f, false);
                strikes.RemoveAt(i);
            }
        }

        // =================================================================
        //  Camera, audio and HUD
        // =================================================================
        Vector3 EarthquakeCameraShake()
        {
            return shakeTimer > 0f ? Random.insideUnitSphere * 0.18f : Vector3.zero;
        }

        void EarthquakeAmbient(ref Amb a)
        {
            a.rumble = shakeTimer > 0f ? 0.9f : 0f;
        }

        void EarthquakeBanner(float W)
        {
            if (shakeTimer > 0f) Txt(new Rect(0, 120, W, 30), "!! AFTERSHOCK !!", 26, Color.red, TextAnchor.UpperCenter);
        }

        void EarthquakePanel(float x, float y)
        {
            Txt(new Rect(x + 8, y + 4, 140, 20), "AFTERSHOCKS", 15, new Color(1f, 0.7f, 0.4f));
            Txt(new Rect(x + 8, y + 28, 140, 30), shakeTimer > 0f ? "HAPPENING NOW!" : "next in ~" + Mathf.CeilToInt(Mathf.Max(0f, nextShake)) + "s", 13, shakeTimer > 0f ? Color.red : Color.white);
        }
    }
}
