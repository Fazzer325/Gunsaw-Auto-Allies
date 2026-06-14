/*
 * Gunsaw Auto Allies
 * Copyright (c) 2026 Fazzer
 *
 * Created by Fazzer with AI-assisted development using OpenAI Codex.
 *
 * This mod is not affiliated with the creators of Gunsaw, BepInEx, or Harmony.
 * Do not redistribute modified versions without clear credit to the original author.
 */

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGunsawPlugin
{
    public enum AutoAllyBehaviorMode
    {
        FollowCombat = 0,
        Defensive = 1,
        FollowOnly = 2
    }

    public enum AutoAllyAimMode
    {
        Never = 0,
        WhileShooting = 1,
        Always = 2
    }

    public enum AutoAllyFormationMode
    {
        Close = 0,
        Line = 1,
        BehindMe = 2,
        Spread = 3
    }

    [BepInPlugin("com.fazzer.gunsaw.autoallies", "Gunsaw Auto Allies by Fazzer", "1.0.0")]
    public class Plugin2 : BaseUnityPlugin
    {
        internal static Plugin2 Instance;
        internal static ManualLogSource Log;
        internal static ConfigEntry<int> BotCount;
        internal static ConfigEntry<int> BehaviorMode;
        internal static ConfigEntry<float> FollowDistance;
        internal static ConfigEntry<float> RescueDistance;
        internal static ConfigEntry<bool> CollideWithPlayer;
        internal static ConfigEntry<bool> CollideBetweenAllies;
        internal static ConfigEntry<int> AimMode;
        internal static ConfigEntry<int> FormationMode;
        internal static ConfigEntry<KeyCode> AttackOrderKey;
        internal static ConfigEntry<KeyCode> HoldPositionKey;
        internal static ConfigEntry<bool> AllyMarkers;
        internal static ConfigEntry<bool> ProtectPlayerFromAllyBullets;

        private Harmony harmony;
        private bool menuOpen;
        private Rect menuRect = new Rect(20f, 80f, 350f, 560f);
        private int pendingKeyBind;

        private static bool menuStylesReady;
        private static GUIStyle menuWindowStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle valueStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle activeButtonStyle;
        private static GUIStyle toggleStyle;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            harmony = new Harmony("com.fazzer.gunsaw.autoallies");
            harmony.PatchAll(typeof(Plugin2PlayerScriptStartPatch));
            harmony.PatchAll(typeof(Plugin2PlayerScriptUpdatePatch));
            harmony.PatchAll(typeof(Plugin2WeaponScriptShootPatch));
            harmony.PatchAll(typeof(Plugin2AIScriptUpdatePatch));
            Log.LogInfo("Gunsaw Auto Allies by Fazzer loaded.");
        }

        private void BindConfig()
        {
            BotCount = Config.Bind("Auto Allies", "BotCount", 2, "Allies spawned when a level starts.");
            BehaviorMode = Config.Bind("Auto Allies", "BehaviorMode", (int)AutoAllyBehaviorMode.FollowCombat, "0 FollowCombat, 1 Defensive, 2 FollowOnly.");
            FollowDistance = Config.Bind("Auto Allies", "FollowDistance", 1.8f, "Preferred distance from the player while following.");
            RescueDistance = Config.Bind("Auto Allies", "RescueDistance", 34f, "Distance needed before stuck allies teleport back near the player.");
            CollideWithPlayer = Config.Bind("Auto Allies", "CollideWithPlayer", false, "Enable physical collision between allies and the player.");
            CollideBetweenAllies = Config.Bind("Auto Allies", "CollideBetweenAllies", false, "Enable physical collision between auto allies.");
            AimMode = Config.Bind("Auto Allies", "AimMode", (int)AutoAllyAimMode.WhileShooting, "0 Never, 1 WhileShooting, 2 Always.");
            FormationMode = Config.Bind("Auto Allies", "FormationMode", (int)AutoAllyFormationMode.Close, "0 Close, 1 Line, 2 BehindMe, 3 Spread.");
            AttackOrderKey = Config.Bind("Auto Allies", "AttackOrderKey", KeyCode.G, "Orders allies to move to your cursor point, then return if nothing is there.");
            HoldPositionKey = Config.Bind("Auto Allies", "HoldPositionKey", KeyCode.H, "Toggles hold position mode.");
            AllyMarkers = Config.Bind("Auto Allies", "AllyMarkers", true, "Draw a simple marker above auto allies.");
            ProtectPlayerFromAllyBullets = Config.Bind("Auto Allies", "ProtectPlayerFromAllyBullets", true, "Makes auto ally weapons ignore the player's colliders.");
            ClampConfig();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Period))
            {
                menuOpen = !menuOpen;
                pendingKeyBind = 0;
            }

            AutoAllyHelpers.UpdateGlobalRescue();
            AutoAllyHelpers.HandleManualOrderInput();
        }

        private void OnGUI()
        {
            EnsureMenuStyles();
            AutoAllyHelpers.DrawAllyMarkers();

            if (!menuOpen)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            menuRect = GUILayout.Window(7219, menuRect, DrawMenuWindow, "AUTO ALLIES", menuWindowStyle);
            GUI.color = oldColor;
        }

        private void DrawMenuWindow(int id)
        {
            ClampConfig();
            CapturePendingKeyBind();

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bots per level", labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(ConfiguredBotCount.ToString(), valueStyle, GUILayout.Width(42f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", buttonStyle, GUILayout.Width(42f), GUILayout.Height(24f)))
            {
                BotCount.Value = Mathf.Clamp(ConfiguredBotCount - 1, 0, 10);
            }
            GUILayout.Box(ConfiguredBotCount.ToString(), valueStyle, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            if (GUILayout.Button("+", buttonStyle, GUILayout.Width(42f), GUILayout.Height(24f)))
            {
                BotCount.Value = Mathf.Clamp(ConfiguredBotCount + 1, 0, 10);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Behavior mode", headerStyle);
            DrawBehaviorButton(AutoAllyBehaviorMode.FollowCombat, "Follow/Combat");
            DrawBehaviorButton(AutoAllyBehaviorMode.Defensive, "Defensive");
            DrawBehaviorButton(AutoAllyBehaviorMode.FollowOnly, "Follow only");
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Formation", headerStyle);
            GUILayout.BeginHorizontal();
            DrawFormationButton(AutoAllyFormationMode.Close, "Close");
            DrawFormationButton(AutoAllyFormationMode.Line, "Line");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawFormationButton(AutoAllyFormationMode.BehindMe, "Behind");
            DrawFormationButton(AutoAllyFormationMode.Spread, "Spread");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            DrawValueHeader("Follow distance", ConfiguredFollowDistance.ToString("0.0"));
            FollowDistance.Value = Mathf.Round(GUILayout.HorizontalSlider(ConfiguredFollowDistance, 0.8f, 6f) * 10f) / 10f;
            GUILayout.Space(5f);
            DrawValueHeader("Rescue distance", ConfiguredRescueDistance.ToString("0"));
            RescueDistance.Value = Mathf.Round(GUILayout.HorizontalSlider(ConfiguredRescueDistance, 8f, 80f));
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Manual orders", headerStyle);
            DrawKeyBindRow("Attack point", ConfiguredAttackOrderKey, 1);
            DrawKeyBindRow("Hold position", ConfiguredHoldPositionKey, 2);
            GUILayout.Label(AutoAllyHelpers.HoldPositionEnabled ? "Hold position is ON" : "Hold position is OFF", labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Collisions and safety", headerStyle);
            CollideWithPlayer.Value = GUILayout.Toggle(CollideWithPlayer.Value, "Collide with player", toggleStyle);
            CollideBetweenAllies.Value = GUILayout.Toggle(CollideBetweenAllies.Value, "Collide between allies", toggleStyle);
            ProtectPlayerFromAllyBullets.Value = GUILayout.Toggle(ProtectPlayerFromAllyBullets.Value, "Ally bullets ignore player", toggleStyle);
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Visuals", headerStyle);
            AllyMarkers.Value = GUILayout.Toggle(AllyMarkers.Value, "Ally markers", toggleStyle);
            GUILayout.EndVertical();

            GUILayout.Space(7f);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Cursor aim", headerStyle);
            GUILayout.BeginHorizontal();
            DrawAimButton(AutoAllyAimMode.Never, "Never");
            DrawAimButton(AutoAllyAimMode.WhileShooting, "On fire");
            DrawAimButton(AutoAllyAimMode.Always, "Always");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
            ConsumeMenuMouseEvents();
        }

        private static void ConsumeMenuMouseEvents()
        {
            if (Event.current == null)
            {
                return;
            }

            EventType type = Event.current.type;
            if (type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag || type == EventType.ScrollWheel)
            {
                Event.current.Use();
            }
        }

        private void CapturePendingKeyBind()
        {
            if (pendingKeyBind == 0 || Event.current == null || Event.current.type != EventType.KeyDown)
            {
                return;
            }

            KeyCode key = Event.current.keyCode;
            if (key != KeyCode.None)
            {
                if (key != KeyCode.Escape)
                {
                    if (pendingKeyBind == 1)
                    {
                        AttackOrderKey.Value = key;
                    }
                    else if (pendingKeyBind == 2)
                    {
                        HoldPositionKey.Value = key;
                    }
                }

                pendingKeyBind = 0;
                Event.current.Use();
            }
        }

        private static void EnsureMenuStyles()
        {
            if (menuStylesReady)
            {
                return;
            }

            menuWindowStyle = new GUIStyle(GUI.skin.window);
            menuWindowStyle.normal.background = MakeTexture(new Color(0.05f, 0.055f, 0.07f, 0.96f));
            menuWindowStyle.normal.textColor = new Color(0.92f, 0.95f, 1f);
            menuWindowStyle.fontSize = 13;
            menuWindowStyle.fontStyle = FontStyle.Bold;
            menuWindowStyle.alignment = TextAnchor.UpperCenter;
            menuWindowStyle.padding = new RectOffset(12, 12, 24, 12);

            sectionStyle = new GUIStyle(GUI.skin.box);
            sectionStyle.normal.background = MakeTexture(new Color(0.12f, 0.13f, 0.16f, 0.92f));
            sectionStyle.padding = new RectOffset(10, 10, 8, 8);
            sectionStyle.margin = new RectOffset(0, 0, 0, 0);

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.normal.textColor = new Color(0.78f, 0.86f, 1f);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = 12;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
            labelStyle.fontSize = 12;

            valueStyle = new GUIStyle(GUI.skin.box);
            valueStyle.normal.background = MakeTexture(new Color(0.07f, 0.08f, 0.1f, 0.95f));
            valueStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);
            valueStyle.alignment = TextAnchor.MiddleCenter;
            valueStyle.fontStyle = FontStyle.Bold;
            valueStyle.padding = new RectOffset(6, 6, 3, 3);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = MakeTexture(new Color(0.18f, 0.2f, 0.25f, 1f));
            buttonStyle.hover.background = MakeTexture(new Color(0.24f, 0.28f, 0.35f, 1f));
            buttonStyle.active.background = MakeTexture(new Color(0.12f, 0.15f, 0.2f, 1f));
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.fontSize = 12;

            activeButtonStyle = new GUIStyle(buttonStyle);
            activeButtonStyle.normal.background = MakeTexture(new Color(0.25f, 0.42f, 0.7f, 1f));
            activeButtonStyle.hover.background = MakeTexture(new Color(0.31f, 0.5f, 0.82f, 1f));
            activeButtonStyle.fontStyle = FontStyle.Bold;

            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
            toggleStyle.onNormal.textColor = Color.white;
            toggleStyle.hover.textColor = Color.white;
            toggleStyle.onHover.textColor = Color.white;
            toggleStyle.fontSize = 12;

            menuStylesReady = true;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DrawValueHeader(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, valueStyle, GUILayout.Width(54f), GUILayout.Height(22f));
            GUILayout.EndHorizontal();
        }

        private void DrawKeyBindRow(string label, KeyCode key, int bindId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            string buttonText = pendingKeyBind == bindId ? "Press key..." : key.ToString();
            if (GUILayout.Button(buttonText, pendingKeyBind == bindId ? activeButtonStyle : buttonStyle, GUILayout.Width(118f), GUILayout.Height(24f)))
            {
                pendingKeyBind = bindId;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawBehaviorButton(AutoAllyBehaviorMode mode, string label)
        {
            GUIStyle style = ConfiguredBehavior == mode ? activeButtonStyle : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(24f)))
            {
                BehaviorMode.Value = (int)mode;
            }
        }

        private static void DrawAimButton(AutoAllyAimMode mode, string label)
        {
            GUIStyle style = ConfiguredAim == mode ? activeButtonStyle : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(24f)))
            {
                AimMode.Value = (int)mode;
            }
        }

        private static void DrawFormationButton(AutoAllyFormationMode mode, string label)
        {
            GUIStyle style = ConfiguredFormation == mode ? activeButtonStyle : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(24f)))
            {
                FormationMode.Value = (int)mode;
            }
        }

        private static void ClampConfig()
        {
            if (BotCount != null)
            {
                BotCount.Value = Mathf.Clamp(BotCount.Value, 0, 10);
            }

            if (BehaviorMode != null)
            {
                BehaviorMode.Value = Mathf.Clamp(BehaviorMode.Value, 0, 2);
            }

            if (FollowDistance != null)
            {
                FollowDistance.Value = Mathf.Clamp(FollowDistance.Value, 0.8f, 6f);
            }

            if (RescueDistance != null)
            {
                RescueDistance.Value = Mathf.Clamp(RescueDistance.Value, 8f, 80f);
            }

            if (AimMode != null)
            {
                AimMode.Value = Mathf.Clamp(AimMode.Value, 0, 2);
            }

            if (FormationMode != null)
            {
                FormationMode.Value = Mathf.Clamp(FormationMode.Value, 0, 3);
            }
        }

        internal static int ConfiguredBotCount
        {
            get { return BotCount != null ? Mathf.Clamp(BotCount.Value, 0, 10) : 2; }
        }

        internal static AutoAllyBehaviorMode ConfiguredBehavior
        {
            get { return BehaviorMode != null ? (AutoAllyBehaviorMode)Mathf.Clamp(BehaviorMode.Value, 0, 2) : AutoAllyBehaviorMode.FollowCombat; }
        }

        internal static float ConfiguredFollowDistance
        {
            get { return FollowDistance != null ? Mathf.Clamp(FollowDistance.Value, 0.8f, 6f) : 1.8f; }
        }

        internal static float ConfiguredRescueDistance
        {
            get { return RescueDistance != null ? Mathf.Clamp(RescueDistance.Value, 8f, 80f) : 34f; }
        }

        internal static bool ConfiguredCollideWithPlayer
        {
            get { return CollideWithPlayer != null && CollideWithPlayer.Value; }
        }

        internal static bool ConfiguredCollideBetweenAllies
        {
            get { return CollideBetweenAllies != null && CollideBetweenAllies.Value; }
        }

        internal static AutoAllyAimMode ConfiguredAim
        {
            get { return AimMode != null ? (AutoAllyAimMode)Mathf.Clamp(AimMode.Value, 0, 2) : AutoAllyAimMode.WhileShooting; }
        }

        internal static AutoAllyFormationMode ConfiguredFormation
        {
            get { return FormationMode != null ? (AutoAllyFormationMode)Mathf.Clamp(FormationMode.Value, 0, 3) : AutoAllyFormationMode.Close; }
        }

        internal static KeyCode ConfiguredAttackOrderKey
        {
            get { return AttackOrderKey != null ? AttackOrderKey.Value : KeyCode.G; }
        }

        internal static KeyCode ConfiguredHoldPositionKey
        {
            get { return HoldPositionKey != null ? HoldPositionKey.Value : KeyCode.H; }
        }

        internal static bool ConfiguredAllyMarkers
        {
            get { return AllyMarkers != null && AllyMarkers.Value; }
        }

        internal static bool ConfiguredProtectPlayerFromAllyBullets
        {
            get { return ProtectPlayerFromAllyBullets == null || ProtectPlayerFromAllyBullets.Value; }
        }

        internal static bool IsPointerOverMenu
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return false;
                }

                Plugin2 plugin = Instance;
                if (plugin == null || !plugin.menuOpen)
                {
                    return false;
                }

                Vector2 mouseGuiPosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return plugin.menuRect.Contains(mouseGuiPosition);
            }
        }

        internal static bool IsMenuCapturingInput
        {
            get
            {
                Plugin2 plugin = Instance;
                return plugin != null && plugin.menuOpen && (plugin.pendingKeyBind != 0 || IsPointerOverMenu);
            }
        }

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    public static class AutoAllyHelpers
    {
        private const int ReserveAmmo = 99;
        private const float FollowWaypointRefreshTime = 0.2f;
        private const float BreadcrumbMinDistance = 0.85f;
        private const float BreadcrumbMinInterval = 0.18f;
        private const int MaxBreadcrumbs = 120;
        private const float EmergencyTeleportStuckTime = 4f;
        private const float CollisionMaintenanceInterval = 0.5f;
        private const float WeaponIgnoreMaintenanceInterval = 1f;

        private static readonly FieldInfo AiCloseToPlayerField =
            AccessTools.Field(typeof(AIScript), "closeToPlayer");

        private static readonly FieldInfo AiLineOfSightField =
            AccessTools.Field(typeof(AIScript), "lineOfSight");

        private static readonly FieldInfo AiWaitBeforeShootField =
            AccessTools.Field(typeof(AIScript), "waitBeforeShoot");

        private static readonly HashSet<AIScript> AutoAllies =
            new HashSet<AIScript>();

        private static readonly Dictionary<WeaponScript, RecoilState> AllyRecoilStates =
            new Dictionary<WeaponScript, RecoilState>();

        private static readonly Dictionary<AIScript, AllyCombatState> AllyCombatStates =
            new Dictionary<AIScript, AllyCombatState>();

        private static readonly Dictionary<AIScript, AllyFollowState> AllyFollowStates =
            new Dictionary<AIScript, AllyFollowState>();

        private static readonly Dictionary<AIScript, Vector2> AllyHoldPositions =
            new Dictionary<AIScript, Vector2>();

        private static readonly Dictionary<AIScript, float> NextCollisionMaintenanceTimes =
            new Dictionary<AIScript, float>();

        private static readonly Dictionary<AIScript, float> NextWeaponIgnoreMaintenanceTimes =
            new Dictionary<AIScript, float>();

        private static readonly List<GameObject> AllyPrefabs =
            new List<GameObject>();

        private static readonly HashSet<string> UnsafePrefabNames =
            new HashSet<string>();

        private static readonly List<Vector2> PlayerBreadcrumbs =
            new List<Vector2>();

        private static float lastBreadcrumbTime = -999f;
        private static string spawnedLevelKey;
        private static bool holdPositionEnabled;
        private static bool attackOrderActive;
        private static Vector2 attackOrderPoint;
        private static float attackOrderExpiresAt;
        private static float lastGlobalRescueTime = -999f;
        private static Texture2D markerTexture;

        public static bool HoldPositionEnabled
        {
            get { return holdPositionEnabled; }
        }

        private struct RecoilState
        {
            public BodyScript Body;
            public Rigidbody2D BodyRigidbody;
            public Vector2 BodyVelocity;
            public float BodyAngularVelocity;
            public Rigidbody2D ArmsRigidbody;
            public Vector2 ArmsVelocity;
            public float ArmsAngularVelocity;
        }

        private struct AllyCombatState
        {
            public BodyScript Target;
            public Vector2 LastKnownPosition;
            public float LastSeenTime;
        }

        private struct AllyFollowState
        {
            public Vector2 Waypoint;
            public Vector2 LastPosition;
            public float LastRefreshTime;
            public float StuckTime;
            public int PreferredSide;
        }

        public static void HandleManualOrderInput()
        {
            if (Plugin2.IsMenuCapturingInput)
            {
                return;
            }

            if (Input.GetKeyDown(Plugin2.ConfiguredAttackOrderKey))
            {
                IssueAttackOrderFromPlayer();
            }

            if (Input.GetKeyDown(Plugin2.ConfiguredHoldPositionKey))
            {
                ToggleHoldPosition();
            }
        }

        public static void UpdateGlobalRescue()
        {
            if (holdPositionEnabled)
            {
                return;
            }

            if (Time.unscaledTime - lastGlobalRescueTime < 0.25f)
            {
                return;
            }

            lastGlobalRescueTime = Time.unscaledTime;
            PruneInvalidAutoAllies(removeInactive: false);
            BodyScript player = GetPlayerBody();
            if (player == null || !player.isAlive)
            {
                return;
            }

            foreach (AIScript ally in AutoAllies)
            {
                if (ally == null || ally.body == null || !ally.body.isAlive)
                {
                    continue;
                }

                BodyScript body = ally.body;
                if (body.gameObject.scene != player.gameObject.scene)
                {
                    continue;
                }

                float distance = Vector2.Distance(body.transform.position, player.transform.position);
                bool inactiveFarAway = !body.gameObject.activeInHierarchy && distance > Plugin2.ConfiguredFollowDistance * 3f;
                bool tooFarAway = distance > Plugin2.ConfiguredRescueDistance;
                if (!inactiveFarAway && !tooFarAway)
                {
                    continue;
                }

                GameObject root = body.transform.root.gameObject;
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                }

                Vector2 targetPosition = FindSpawnPositionNearPlayer(player, GetAllyIndex(ally));
                MoveBodyToPosition(body, targetPosition);
                AllyFollowStates[ally] = new AllyFollowState
                {
                    Waypoint = targetPosition,
                    LastPosition = targetPosition,
                    LastRefreshTime = -999f,
                    StuckTime = 0f,
                    PreferredSide = GetAllyIndex(ally) % 2 == 0 ? -1 : 1
                };
            }
        }

        public static void IssueAttackOrderFromPlayer()
        {
            BodyScript player = GetPlayerBody();
            if (player == null || !player.isAlive)
            {
                return;
            }

            attackOrderPoint = player.targetLookPos;
            attackOrderActive = true;
            attackOrderExpiresAt = Time.time + 10f;
            Plugin2.Log?.LogInfo("Auto ally attack order at " + attackOrderPoint);
        }

        public static void ToggleHoldPosition()
        {
            holdPositionEnabled = !holdPositionEnabled;
            AllyHoldPositions.Clear();

            if (holdPositionEnabled)
            {
                foreach (AIScript ally in AutoAllies)
                {
                    if (ally != null && ally.body != null)
                    {
                        AllyHoldPositions[ally] = ally.body.transform.position;
                    }
                }
            }

            Plugin2.Log?.LogInfo("Auto ally hold position: " + holdPositionEnabled);
        }

        public static IEnumerator SpawnLevelAlliesWhenReady()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            float timeout = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < timeout)
            {
                BodyScript playerBody = GetPlayerBody();
                if (playerBody != null && playerBody.isAlive)
                {
                    string levelKey = GetLevelSpawnKey(playerBody, sceneName);
                    if (spawnedLevelKey == levelKey)
                    {
                        yield break;
                    }

                    SpawnLevelAllies(playerBody, sceneName, levelKey);
                    yield break;
                }

                yield return null;
            }
        }

        private static string GetLevelSpawnKey(BodyScript playerBody, string sceneName)
        {
            int playerId = playerBody != null ? playerBody.GetInstanceID() : 0;
            int sceneHandle = playerBody != null ? playerBody.gameObject.scene.handle : SceneManager.GetActiveScene().handle;
            return sceneName + ":" + sceneHandle + ":" + playerId;
        }

        private static void CleanupAutoAllyState()
        {
            PruneInvalidAutoAllies(removeInactive: true);
            AllyRecoilStates.Clear();
        }

        private static void PruneInvalidAutoAllies(bool removeInactive)
        {
            List<AIScript> deadAllies = new List<AIScript>();
            foreach (AIScript ally in AutoAllies)
            {
                if (ally == null || ally.body == null || !ally.body.isAlive ||
                    (removeInactive && !ally.body.gameObject.activeInHierarchy))
                {
                    deadAllies.Add(ally);
                }
            }

            foreach (AIScript ally in deadAllies)
            {
                AutoAllies.Remove(ally);
                AllyCombatStates.Remove(ally);
                AllyFollowStates.Remove(ally);
                AllyHoldPositions.Remove(ally);
                NextCollisionMaintenanceTimes.Remove(ally);
                NextWeaponIgnoreMaintenanceTimes.Remove(ally);
            }
        }

        private static void SpawnLevelAllies(BodyScript playerBody, string sceneName, string levelKey)
        {
            if (playerBody == null)
            {
                return;
            }

            CleanupAutoAllyState();
            EnsureAllyPrefabs();
            if (AllyPrefabs.Count == 0)
            {
                Plugin2.Log?.LogWarning("No ally prefabs found in Resources/Enemies.");
                return;
            }

            spawnedLevelKey = levelKey;
            holdPositionEnabled = false;
            attackOrderActive = false;
            AllyHoldPositions.Clear();
            ResetPlayerBreadcrumbs(playerBody);
            int allyCount = Plugin2.ConfiguredBotCount;
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(allyCount * 5, AllyPrefabs.Count * 2);
            while (spawned < allyCount && attempts < maxAttempts && AllyPrefabs.Count > 0)
            {
                attempts++;
                int prefabIndex = Random.Range(0, AllyPrefabs.Count);
                GameObject prefab = AllyPrefabs[prefabIndex];
                Vector2 spawnPosition = FindSpawnPositionNearPlayer(playerBody, spawned);
                GameObject instance = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
                instance.name = "AutoAlly_" + prefab.name;

                BodyScript body;
                AIScript ai;
                if (!TryPrepareSpawnedAlly(instance, out ai, out body))
                {
                    AllyPrefabs.RemoveAt(prefabIndex);
                    Object.Destroy(instance);
                    continue;
                }

                RegisterAutoAlly(ai, body);
                spawned++;
            }

            Plugin2.Log?.LogInfo("Spawned auto allies for scene: " + sceneName);
        }

        public static void RecordPlayerBreadcrumb(BodyScript playerBody)
        {
            if (playerBody == null || !playerBody.isAlive || !playerBody.grounded)
            {
                return;
            }

            Vector2 position = playerBody.transform.position;
            if (PlayerBreadcrumbs.Count > 0)
            {
                Vector2 last = PlayerBreadcrumbs[PlayerBreadcrumbs.Count - 1];
                if ((position - last).sqrMagnitude < BreadcrumbMinDistance * BreadcrumbMinDistance &&
                    Time.time - lastBreadcrumbTime < BreadcrumbMinInterval)
                {
                    return;
                }
            }

            PlayerBreadcrumbs.Add(position);
            lastBreadcrumbTime = Time.time;
            while (PlayerBreadcrumbs.Count > MaxBreadcrumbs)
            {
                PlayerBreadcrumbs.RemoveAt(0);
            }
        }

        private static void ResetPlayerBreadcrumbs(BodyScript playerBody)
        {
            PlayerBreadcrumbs.Clear();
            lastBreadcrumbTime = -999f;
            if (playerBody != null)
            {
                PlayerBreadcrumbs.Add(playerBody.transform.position);
                lastBreadcrumbTime = Time.time;
            }
        }

        private static void EnsureAllyPrefabs()
        {
            if (AllyPrefabs.Count > 0)
            {
                return;
            }

            GameObject[] enemyPrefabs = Resources.LoadAll<GameObject>("Enemies");
            foreach (GameObject prefab in enemyPrefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (IsPotentialAllyPrefab(prefab))
                {
                    AllyPrefabs.Add(prefab);
                }
            }

            Plugin2.Log?.LogInfo("Loaded " + AllyPrefabs.Count + " auto ally prefab candidates from Resources/Enemies.");

            if (AllyPrefabs.Count == 0)
            {
                GameObject fallback = Resources.Load<GameObject>("Enemies/Abomination");
                if (IsPotentialAllyPrefab(fallback))
                {
                    AllyPrefabs.Add(fallback);
                }
            }
        }

        private static bool IsPotentialAllyPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            if (UnsafePrefabNames.Contains(prefab.name))
            {
                return false;
            }

            BodyScript body = prefab.GetComponentInChildren<BodyScript>(true);
            AIScript ai = prefab.GetComponentInChildren<AIScript>(true);
            if (body == null || ai == null)
            {
                return false;
            }

            LimbScript[] limbs = prefab.GetComponentsInChildren<LimbScript>(true);
            if (limbs == null || limbs.Length == 0)
            {
                return false;
            }

            if (body.BodyAnimator == null || body.ArmsAnimator == null || body.Arms == null ||
                body.gunTransform == null || body.gunAnimTransform == null || body.headTransform == null)
            {
                return false;
            }

            if (body.GetComponent<Rigidbody2D>() == null || body.GetComponent<BoxCollider2D>() == null)
            {
                return false;
            }

            WeaponScript weapon = body.gunTransform.GetComponent<WeaponScript>();
            return weapon != null;
        }

        private static bool TryPrepareSpawnedAlly(GameObject instance, out AIScript ai, out BodyScript body)
        {
            body = null;
            ai = null;
            if (instance == null)
            {
                return false;
            }

            body = instance.GetComponentInChildren<BodyScript>();
            ai = instance.GetComponentInChildren<AIScript>();
            if (body == null || ai == null)
            {
                return false;
            }

            if (body.limbs == null)
            {
                body.limbs = new List<LimbScript>();
            }
            else
            {
                body.limbs.Clear();
            }

            try
            {
                body.WakeUp();
            }
            catch (System.Exception ex)
            {
                string prefabName = instance.name.Replace("AutoAlly_", string.Empty);
                UnsafePrefabNames.Add(prefabName);
                Plugin2.Log?.LogWarning("Skipped unsafe auto ally prefab '" + instance.name + "' during WakeUp: " + ex.GetType().Name + " " + ex.Message);
                instance.SetActive(false);
                return false;
            }

            if (body.limbs == null || body.limbs.Count < 15 || body.weapon == null || body.weapon.stats == null ||
                body.Arms == null || body.rb == null)
            {
                string prefabName = instance.name.Replace("AutoAlly_", string.Empty);
                UnsafePrefabNames.Add(prefabName);
                int limbCount = body.limbs != null ? body.limbs.Count : 0;
                Plugin2.Log?.LogWarning("Skipped unsupported auto ally prefab '" + instance.name + "': limbs=" + limbCount + ", weapon=" + (body.weapon != null));
                instance.SetActive(false);
                return false;
            }

            return true;
        }

        private static Vector2 FindSpawnPositionNearPlayer(BodyScript playerBody, int index)
        {
            float side = index == 0 ? -1f : 1f;
            Vector2 basePosition = playerBody.transform.position;
            Vector2 candidate = basePosition + new Vector2(1.8f * side, 0.4f);
            RaycastHit2D hit = Physics2D.Raycast(candidate + Vector2.up * 2f, Vector2.down, 5f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
            {
                candidate = hit.point + Vector2.up * 0.45f;
            }

            return candidate;
        }

        private static void RegisterAutoAlly(AIScript ai, BodyScript body)
        {
            BodyScript playerBody = GetPlayerBody();
            if (ai == null || body == null || playerBody == null)
            {
                return;
            }

            ai.body = body;
            if (ai.weapon == null)
            {
                ai.weapon = body.weapon;
            }

            ai.followPlayer = true;
            ai.targetBody = null;
            ai.seesPlayer = false;
            ai.susness = 0f;
            ai.alerted = false;
            ai.timeSinceLastSeen = 50f;
            ai.firstShotTime = 0f;
            ai.confuseTime = 0f;
            AiCloseToPlayerField?.SetValue(ai, false);
            AiLineOfSightField?.SetValue(ai, false);
            AiWaitBeforeShootField?.SetValue(ai, 0f);

            body.team = playerBody.team;
            body.wasAlly = true;
            body.isPlayer = false;
            body.isWalking = false;
            body.maxHealth = Mathf.Max(body.maxHealth, body.health * 1.5f);
            body.health = Mathf.Max(body.health, body.maxHealth);
            body.stamina = Mathf.Max(body.stamina, body.maxHealth);
            body.healthRegen = Mathf.Max(body.healthRegen, 5f);
            SetAllAmmo(body.ammoAmount, ReserveAmmo);
            int allyIndex = AutoAllies.Count;
            AllyFollowStates[ai] = new AllyFollowState
            {
                Waypoint = body.transform.position,
                LastPosition = body.transform.position,
                LastRefreshTime = -999f,
                StuckTime = 0f,
                PreferredSide = allyIndex % 2 == 0 ? -1 : 1
            };
            MaintainAllyCollisions(ai);

            AutoAllies.Add(ai);
        }

        public static void UpdateAutoAlly(AIScript ai)
        {
            if (ai == null || !AutoAllies.Contains(ai) || ai.body == null || !ai.body.isAlive)
            {
                return;
            }

            if (GameManager.main != null && GameManager.main.paused)
            {
                return;
            }

            MaintainAllyResources(ai.body);
            MaintainAllyCollisions(ai);
            MaintainAllyWeaponIgnores(ai);

            BodyScript target = GetAllyTarget(ai);
            if (target == null)
            {
                if (TryUpdateAttackOrder(ai))
                {
                    return;
                }

                if (TryUpdateHoldPosition(ai))
                {
                    return;
                }

                SetAllyFollowMode(ai);
                UpdateAllyFollowPath(ai);
                return;
            }

            AllyFollowStates.Remove(ai);
            ai.followPlayer = false;
            ai.targetBody = target;
            ai.alerted = true;
            ai.susness = 1f;
            ai.firstShotTime = -1f;
            AiWaitBeforeShootField?.SetValue(ai, -0.1f);

            WeaponScript weapon = ai.weapon != null ? ai.weapon : ai.body.weapon;
            Vector2 aimPoint = target.limbs != null && target.limbs.Count > 0 && target.limbs[0] != null
                ? (Vector2)target.limbs[0].transform.position
                : (Vector2)target.transform.position;
            bool hasLineOfSight = HasAllyLineOfSight(ai.body, target);
            AllyCombatState state = GetAllyCombatState(ai);

            if (hasLineOfSight)
            {
                state.Target = target;
                state.LastKnownPosition = target.transform.position;
                state.LastSeenTime = Time.time;
                AllyCombatStates[ai] = state;
                ai.seesPlayer = true;
                ai.timeSinceLastSeen = 0f;
                ai.lastSeen = target.transform.position;
                AiCloseToPlayerField?.SetValue(ai, Vector2.Distance(ai.body.transform.position, target.transform.position) < 3.5f);
                AiLineOfSightField?.SetValue(ai, true);
            }
            else
            {
                ai.seesPlayer = false;
                ai.timeSinceLastSeen = Mathf.Min(ai.timeSinceLastSeen, 8f);
                ai.lastSeen = state.LastKnownPosition;
                AiCloseToPlayerField?.SetValue(ai, false);
                AiLineOfSightField?.SetValue(ai, false);
            }

            ai.body.targetLookPos = aimPoint;
            ai.body.isWalking = false;
            UpdateAllyCombatMovement(ai.body, target, weapon, hasLineOfSight, state.LastKnownPosition);

            if (aimPoint.x > ai.body.transform.position.x && !ai.body.isRight)
            {
                ai.body.SwitchDir();
            }
            else if (aimPoint.x < ai.body.transform.position.x && ai.body.isRight)
            {
                ai.body.SwitchDir();
            }

            Vector2 aimVector = aimPoint - (Vector2)ai.body.Arms.position;
            float sideOffset = ai.body.isRight ? 0f : 180f;
            ai.body.armRotation = Mathf.Atan2(aimVector.x, 0f - aimVector.y) * Mathf.Rad2Deg - 90f + sideOffset;

            if (!hasLineOfSight || weapon == null || weapon.stats == null || ai.body.unarmed || weapon.dismembered ||
                Plugin2.IsMenuCapturingInput)
            {
                return;
            }

            if (weapon.ammo <= 0)
            {
                weapon.ReloadWeapon();
                return;
            }

            weapon.Shoot();
            RemoveRecoil(ai.body);
        }

        public static bool IsAutoAllyBody(BodyScript body)
        {
            if (body == null)
            {
                return false;
            }

            AIScript ai = body.GetComponentInParent<AIScript>();
            return ai != null && AutoAllies.Contains(ai);
        }

        public static void CaptureAllyRecoil(WeaponScript weapon)
        {
            if (weapon == null || weapon.body == null || !IsAutoAllyBody(weapon.body))
            {
                return;
            }

            BodyScript body = weapon.body;
            Rigidbody2D armsRigidbody = null;
            if (body.Arms != null && body.Arms.transform.parent != null)
            {
                armsRigidbody = body.Arms.transform.parent.GetComponent<Rigidbody2D>();
            }

            AllyRecoilStates[weapon] = new RecoilState
            {
                Body = body,
                BodyRigidbody = body.rb,
                BodyVelocity = body.rb != null ? body.rb.velocity : Vector2.zero,
                BodyAngularVelocity = body.rb != null ? body.rb.angularVelocity : 0f,
                ArmsRigidbody = armsRigidbody,
                ArmsVelocity = armsRigidbody != null ? armsRigidbody.velocity : Vector2.zero,
                ArmsAngularVelocity = armsRigidbody != null ? armsRigidbody.angularVelocity : 0f
            };
        }

        public static void RestoreAllyRecoil(WeaponScript weapon)
        {
            if (weapon == null)
            {
                return;
            }

            RecoilState state;
            if (!AllyRecoilStates.TryGetValue(weapon, out state))
            {
                return;
            }

            AllyRecoilStates.Remove(weapon);
            if (state.Body != null)
            {
                state.Body.currentRecoil = 0f;
            }

            if (state.BodyRigidbody != null)
            {
                state.BodyRigidbody.velocity = state.BodyVelocity;
                state.BodyRigidbody.angularVelocity = state.BodyAngularVelocity;
            }

            if (state.ArmsRigidbody != null)
            {
                state.ArmsRigidbody.velocity = state.ArmsVelocity;
                state.ArmsRigidbody.angularVelocity = state.ArmsAngularVelocity;
            }
        }

        public static bool ShouldBlockWeaponShoot(WeaponScript weapon)
        {
            if (weapon == null || weapon.body == null)
            {
                return false;
            }

            bool isAutoAlly = IsAutoAllyBody(weapon.body);
            if (isAutoAlly && IsBlockedMirroredPlayerShot(weapon.body))
            {
                return true;
            }

            return Plugin2.IsMenuCapturingInput && (weapon.body.isPlayer || isAutoAlly);
        }

        private static bool IsBlockedMirroredPlayerShot(BodyScript body)
        {
            if (Plugin2.ConfiguredAim != AutoAllyAimMode.Never || !Input.GetMouseButton(0))
            {
                return false;
            }

            AIScript ai = body.GetComponentInParent<AIScript>();
            if (ai == null || !AutoAllies.Contains(ai))
            {
                return false;
            }

            return ai.followPlayer || ai.targetBody == null;
        }

        private static void UpdateAllyCombatMovement(
            BodyScript ally,
            BodyScript target,
            WeaponScript weapon,
            bool hasLineOfSight,
            Vector2 searchPosition)
        {
            if (ally == null || target == null)
            {
                return;
            }

            Vector2 moveTarget = hasLineOfSight ? (Vector2)target.transform.position : searchPosition;
            float xDelta = moveTarget.x - ally.transform.position.x;
            float absXDelta = Mathf.Abs(xDelta);
            float desiredDistance = 5f;

            if (weapon != null && weapon.stats != null)
            {
                desiredDistance = Mathf.Clamp(weapon.stats.aiDistanceMult * 5f, 3f, 9f);
            }

            if (!hasLineOfSight)
            {
                desiredDistance = 1.2f;
            }

            if (absXDelta > desiredDistance + 1f)
            {
                ally.CurrentState = xDelta > 0f ? BodyScript.EntityState.MoveRight : BodyScript.EntityState.MoveLeft;
            }
            else if (hasLineOfSight && absXDelta < 2.2f)
            {
                ally.CurrentState = xDelta > 0f ? BodyScript.EntityState.MoveLeft : BodyScript.EntityState.MoveRight;
            }
            else
            {
                ally.CurrentState = BodyScript.EntityState.Idle;
            }

            float yDelta = moveTarget.y - ally.transform.position.y;
            if (yDelta > 1.25f)
            {
                ally.upVector = 1f;
                if (ally.grounded && ally.IsConsc())
                {
                    ally.Jump();
                }
            }
            else if (yDelta < -1.25f)
            {
                ally.upVector = -1f;
                ally.DropLedge();
            }
            else
            {
                ally.upVector = 1f;
            }
        }

        private static AllyCombatState GetAllyCombatState(AIScript ai)
        {
            AllyCombatState state;
            if (AllyCombatStates.TryGetValue(ai, out state))
            {
                if (state.Target != null && state.Target.isAlive && state.Target.team != ai.body.team)
                {
                    return state;
                }
            }

            return new AllyCombatState
            {
                Target = null,
                LastKnownPosition = ai.body != null ? (Vector2)ai.body.transform.position : Vector2.zero,
                LastSeenTime = -999f
            };
        }

        private static BodyScript GetAllyTarget(AIScript ai)
        {
            if (ai == null || ai.body == null)
            {
                return null;
            }

            if (Plugin2.ConfiguredBehavior == AutoAllyBehaviorMode.FollowOnly)
            {
                AllyCombatStates.Remove(ai);
                return null;
            }

            BodyScript visibleTarget = FindBestAllyTarget(ai.body);
            if (visibleTarget != null)
            {
                AllyCombatStates[ai] = new AllyCombatState
                {
                    Target = visibleTarget,
                    LastKnownPosition = visibleTarget.transform.position,
                    LastSeenTime = Time.time
                };
                return visibleTarget;
            }

            AllyCombatState state = GetAllyCombatState(ai);
            return state.Target != null ? state.Target : null;
        }

        private static void SetAllyFollowMode(AIScript ai)
        {
            ai.followPlayer = true;
            ai.targetBody = null;
            ai.seesPlayer = false;
            ai.susness = 0f;
            ai.alerted = false;
            ai.timeSinceLastSeen = 50f;
            ai.lastSeen = Vector2.zero;
            AiCloseToPlayerField?.SetValue(ai, false);
            AiLineOfSightField?.SetValue(ai, false);
            AllyCombatStates.Remove(ai);
        }

        private static bool TryUpdateAttackOrder(AIScript ai)
        {
            if (!attackOrderActive)
            {
                return false;
            }

            if (Time.time > attackOrderExpiresAt || AreAllAlliesNear(attackOrderPoint, 1.35f))
            {
                attackOrderActive = false;
                return false;
            }

            SetAllyManualMoveMode(ai);
            UpdateAllyManualMove(ai, attackOrderPoint, 0.9f);
            return true;
        }

        private static bool TryUpdateHoldPosition(AIScript ai)
        {
            if (!holdPositionEnabled || ai == null || ai.body == null)
            {
                return false;
            }

            Vector2 anchor;
            if (!AllyHoldPositions.TryGetValue(ai, out anchor))
            {
                anchor = ai.body.transform.position;
                AllyHoldPositions[ai] = anchor;
            }

            SetAllyManualMoveMode(ai);
            UpdateAllyManualMove(ai, anchor, 0.75f);
            return true;
        }

        private static void SetAllyManualMoveMode(AIScript ai)
        {
            ai.followPlayer = false;
            ai.targetBody = null;
            ai.seesPlayer = false;
            ai.susness = 0f;
            ai.alerted = false;
            ai.lastSeen = Vector2.zero;
            AiCloseToPlayerField?.SetValue(ai, false);
            AiLineOfSightField?.SetValue(ai, false);
        }

        private static void UpdateAllyManualMove(AIScript ai, Vector2 destination, float stopDistance)
        {
            if (ai == null || ai.body == null)
            {
                return;
            }

            BodyScript ally = ai.body;
            AllyFollowState state = GetAllyFollowState(ai, ally);
            Vector2 allyPos = ally.transform.position;
            float movedDistance = Vector2.Distance(allyPos, state.LastPosition);
            if (movedDistance < 0.05f && Vector2.Distance(allyPos, destination) > stopDistance + 0.5f && ally.CanMove())
            {
                state.StuckTime += Time.deltaTime;
            }
            else
            {
                state.StuckTime = 0f;
            }

            bool directLine = !Physics2D.Linecast(allyPos, destination, LayerMask.GetMask("Ground"));
            if (Time.time - state.LastRefreshTime > FollowWaypointRefreshTime || state.Waypoint == Vector2.zero)
            {
                state.Waypoint = directLine ? destination : FindManualWaypoint(ally, destination, state.PreferredSide);
                state.LastRefreshTime = Time.time;
            }

            ApplyMoveToPoint(ally, state.Waypoint, destination, stopDistance, state.StuckTime);
            state.LastPosition = allyPos;
            AllyFollowStates[ai] = state;
        }

        private static Vector2 FindManualWaypoint(BodyScript ally, Vector2 destination, int preferredSide)
        {
            Vector2 allyPos = ally.transform.position;
            float verticalDelta = destination.y - allyPos.y;
            float bestScore = float.MaxValue;
            Vector2 best = new Vector2(destination.x, allyPos.y);
            float[] offsets = { 0f, 1.5f, -1.5f, 3f, -3f, 5f, -5f, 8f, -8f };

            for (int i = 0; i < offsets.Length; i++)
            {
                float offset = preferredSide < 0 ? -offsets[i] : offsets[i];
                Vector2 candidate = new Vector2(destination.x + offset, allyPos.y);
                if (!HasHorizontalTravelSpace(ally, candidate.x))
                {
                    continue;
                }

                if (Mathf.Abs(verticalDelta) > 1.25f && !HasVerticalOpening(ally, candidate.x, verticalDelta))
                {
                    continue;
                }

                float score = Mathf.Abs(candidate.x - allyPos.x) + Mathf.Abs(candidate.x - destination.x) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool AreAllAlliesNear(Vector2 point, float distance)
        {
            bool any = false;
            float distanceSqr = distance * distance;
            foreach (AIScript ally in AutoAllies)
            {
                if (ally == null || ally.body == null || !ally.body.isAlive)
                {
                    continue;
                }

                any = true;
                if (((Vector2)ally.body.transform.position - point).sqrMagnitude > distanceSqr)
                {
                    return false;
                }
            }

            return any;
        }

        private static int GetAllyIndex(AIScript ai)
        {
            int index = 0;
            foreach (AIScript ally in AutoAllies)
            {
                if (ally == ai)
                {
                    return index;
                }

                index++;
            }

            return 0;
        }

        private static void UpdateAllyFollowPath(AIScript ai)
        {
            if (ai == null || ai.body == null)
            {
                return;
            }

            BodyScript ally = ai.body;
            BodyScript player = GetPlayerBody();
            if (player == null || player == ally || !player.isAlive)
            {
                return;
            }

            AllyFollowState state = GetAllyFollowState(ai, ally);
            Vector2 allyPos = ally.transform.position;
            Vector2 playerPos = player.transform.position;
            ally.isWalking = false;
            float movedDistance = Vector2.Distance(allyPos, state.LastPosition);
            float followDistance = Plugin2.ConfiguredFollowDistance;
            if (movedDistance < 0.06f && Vector2.Distance(allyPos, playerPos) > followDistance + 0.8f && ally.CanMove())
            {
                state.StuckTime += Time.deltaTime;
            }
            else
            {
                state.StuckTime = 0f;
            }

            bool directLine = !Physics2D.Linecast(allyPos, playerPos, LayerMask.GetMask("Ground"));
            if (TryEmergencyTeleportToPlayer(ally, player, state.StuckTime))
            {
                state.Waypoint = ally.transform.position;
                state.LastPosition = ally.transform.position;
                state.LastRefreshTime = Time.time;
                state.StuckTime = 0f;
                AllyFollowStates[ai] = state;
                return;
            }

            if (Time.time - state.LastRefreshTime > FollowWaypointRefreshTime || state.Waypoint == Vector2.zero)
            {
                state.Waypoint = FindBestFollowWaypoint(ally, player, state.PreferredSide, directLine, state.StuckTime, GetAllyIndex(ai));
                state.LastRefreshTime = Time.time;
            }

            ApplyFollowWaypoint(ally, player, state.Waypoint, directLine, state.StuckTime);
            state.LastPosition = allyPos;
            AllyFollowStates[ai] = state;
        }

        private static AllyFollowState GetAllyFollowState(AIScript ai, BodyScript ally)
        {
            AllyFollowState state;
            if (AllyFollowStates.TryGetValue(ai, out state))
            {
                return state;
            }

            return new AllyFollowState
            {
                Waypoint = ally != null ? (Vector2)ally.transform.position : Vector2.zero,
                LastPosition = ally != null ? (Vector2)ally.transform.position : Vector2.zero,
                LastRefreshTime = -999f,
                StuckTime = 0f,
                PreferredSide = 1
            };
        }

        private static Vector2 FindBestFollowWaypoint(BodyScript ally, BodyScript player, int preferredSide, bool directLine, float stuckTime, int allyIndex)
        {
            Vector2 allyPos = ally.transform.position;
            Vector2 playerPos = player.transform.position;
            Vector2 desiredNearPlayer = GetFormationPoint(player, allyIndex, preferredSide);

            if (directLine)
            {
                return desiredNearPlayer;
            }

            Vector2 breadcrumbWaypoint;
            if (TryFindBreadcrumbWaypoint(ally, stuckTime, out breadcrumbWaypoint))
            {
                return breadcrumbWaypoint;
            }

            float verticalDelta = playerPos.y - allyPos.y;
            float bestScore = float.MaxValue;
            Vector2 best = desiredNearPlayer;
            float[] offsets = { 0f, 2f, -2f, 4f, -4f, 6f, -6f, 9f, -9f, 12f, -12f, 16f, -16f };

            for (int i = 0; i < offsets.Length; i++)
            {
                float biasedOffset = offsets[i];
                if (preferredSide < 0)
                {
                    biasedOffset = -biasedOffset;
                }

                Vector2 candidate = new Vector2(playerPos.x + biasedOffset, allyPos.y);
                if (!HasHorizontalTravelSpace(ally, candidate.x))
                {
                    continue;
                }

                if (Mathf.Abs(verticalDelta) > 1.25f && !HasVerticalOpening(ally, candidate.x, verticalDelta))
                {
                    continue;
                }

                float score = Mathf.Abs(candidate.x - allyPos.x) + Mathf.Abs(candidate.x - playerPos.x) * 0.35f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore < float.MaxValue)
            {
                return best;
            }

            return new Vector2(desiredNearPlayer.x, allyPos.y);
        }

        private static Vector2 GetFormationPoint(BodyScript player, int allyIndex, int preferredSide)
        {
            Vector2 playerPos = player.transform.position;
            float distance = Plugin2.ConfiguredFollowDistance;
            int count = Mathf.Max(1, AutoAllies.Count);
            float center = (count - 1) * 0.5f;
            float relativeIndex = allyIndex - center;

            switch (Plugin2.ConfiguredFormation)
            {
                case AutoAllyFormationMode.Line:
                    return playerPos + new Vector2(relativeIndex * distance, 0f);
                case AutoAllyFormationMode.BehindMe:
                    return playerPos + new Vector2((player.isRight ? -1f : 1f) * distance * (1f + allyIndex * 0.45f), 0f);
                case AutoAllyFormationMode.Spread:
                    {
                        float side = allyIndex % 2 == 0 ? -1f : 1f;
                        float row = 1f + allyIndex / 2;
                        return playerPos + new Vector2(side * distance * row, Mathf.Repeat(allyIndex, 3f) * 0.25f);
                    }
                default:
                    return playerPos + new Vector2((preferredSide < 0 ? -1f : 1f) * distance, 0f);
            }
        }

        private static bool TryFindBreadcrumbWaypoint(BodyScript ally, float stuckTime, out Vector2 waypoint)
        {
            waypoint = Vector2.zero;
            if (ally == null || PlayerBreadcrumbs.Count < 2)
            {
                return false;
            }

            Vector2 allyPos = ally.transform.position;
            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < PlayerBreadcrumbs.Count; i++)
            {
                float distance = (PlayerBreadcrumbs[i] - allyPos).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            int lookAhead = stuckTime > 0.35f ? 1 : 2;
            int targetIndex = Mathf.Min(PlayerBreadcrumbs.Count - 1, nearestIndex + lookAhead);
            float skipDistance = stuckTime > 0.35f ? 0.75f : 1.15f;
            while (targetIndex < PlayerBreadcrumbs.Count - 1 &&
                   (PlayerBreadcrumbs[targetIndex] - allyPos).sqrMagnitude < skipDistance * skipDistance)
            {
                targetIndex++;
            }

            waypoint = PlayerBreadcrumbs[targetIndex];
            return true;
        }

        private static bool TryEmergencyTeleportToPlayer(BodyScript ally, BodyScript player, float stuckTime)
        {
            if (holdPositionEnabled || ally == null || player == null)
            {
                return false;
            }

            float distance = Vector2.Distance(ally.transform.position, player.transform.position);
            if (distance < Plugin2.ConfiguredRescueDistance || stuckTime < EmergencyTeleportStuckTime || IsBodyOnScreen(ally))
            {
                return false;
            }

            Vector2 targetPosition = FindSpawnPositionNearPlayer(player, Random.Range(0, 2));
            MoveBodyToPosition(ally, targetPosition);
            return true;
        }

        private static bool IsBodyOnScreen(BodyScript body)
        {
            if (body == null || Camera.main == null)
            {
                return true;
            }

            Vector2 cameraHalfSize = new Vector2(Camera.main.orthographicSize * Camera.main.aspect, Camera.main.orthographicSize);
            Vector2 cameraPosition = Camera.main.transform.position;
            Vector2 bodyPosition = body.transform.position;
            return bodyPosition.x < cameraPosition.x + cameraHalfSize.x + 3f &&
                   bodyPosition.x > cameraPosition.x - cameraHalfSize.x - 3f &&
                   bodyPosition.y < cameraPosition.y + cameraHalfSize.y + 3f &&
                   bodyPosition.y > cameraPosition.y - cameraHalfSize.y - 3f;
        }

        private static void MoveBodyToPosition(BodyScript body, Vector2 targetPosition)
        {
            if (body == null)
            {
                return;
            }

            body.transform.position = targetPosition;
            if (body.rb != null)
            {
                body.rb.velocity = Vector2.zero;
                body.rb.angularVelocity = 0f;
            }

            if (body.limbs != null)
            {
                foreach (LimbScript limb in body.limbs)
                {
                    if (limb == null)
                    {
                        continue;
                    }

                    if (limb.rb != null)
                    {
                        limb.rb.velocity = Vector2.zero;
                        limb.rb.angularVelocity = 0f;
                    }
                }
            }
        }

        private static bool HasHorizontalTravelSpace(BodyScript ally, float targetX)
        {
            Vector2 start = ally.transform.position + Vector3.up * 0.6f;
            Vector2 end = new Vector2(targetX, start.y);
            return !Physics2D.Linecast(start, end, LayerMask.GetMask("Ground"));
        }

        private static bool HasVerticalOpening(BodyScript ally, float sampleX, float verticalDelta)
        {
            float direction = Mathf.Sign(verticalDelta);
            float distance = Mathf.Clamp(Mathf.Abs(verticalDelta) + 1.5f, 1.5f, 18f);
            Vector2 start = new Vector2(sampleX, ally.transform.position.y + 0.4f * direction);
            return !Physics2D.Raycast(start, Vector2.up * direction, distance, LayerMask.GetMask("Ground"));
        }

        private static bool HasWallAhead(BodyScript ally, float direction)
        {
            if (ally == null || Mathf.Abs(direction) < 0.1f)
            {
                return false;
            }

            Vector2 origin = ally.transform.position + Vector3.up * 0.45f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * Mathf.Sign(direction), 0.85f, LayerMask.GetMask("Ground"));
            return hit.collider != null && !hit.collider.CompareTag("Platform");
        }

        private static bool ShouldMirrorPlayerAim(BodyScript player)
        {
            if (player == null || player.unarmed || player.weapon == null || player.weapon.stats == null ||
                Plugin2.IsMenuCapturingInput)
            {
                return false;
            }

            AutoAllyAimMode mode = Plugin2.ConfiguredAim;
            return mode == AutoAllyAimMode.Always || (mode == AutoAllyAimMode.WhileShooting && Input.GetMouseButton(0));
        }

        private static bool ShouldMirrorPlayerFire(BodyScript player)
        {
            if (player == null || player.unarmed || player.weapon == null || player.weapon.stats == null)
            {
                return false;
            }

            return CanMirrorPlayerFire() && Input.GetMouseButton(0);
        }

        private static bool CanMirrorPlayerFire()
        {
            return Plugin2.ConfiguredAim != AutoAllyAimMode.Never && !Plugin2.IsMenuCapturingInput;
        }

        private static Vector2 GetFollowLookPosition(BodyScript ally, BodyScript player, Vector2 waypoint, bool directLine, bool mirrorPlayerAim)
        {
            if (mirrorPlayerAim)
            {
                return player.targetLookPos;
            }

            if (ally.CurrentState == BodyScript.EntityState.MoveRight)
            {
                return (Vector2)ally.transform.position + new Vector2(3f, 0.7f);
            }

            if (ally.CurrentState == BodyScript.EntityState.MoveLeft)
            {
                return (Vector2)ally.transform.position + new Vector2(-3f, 0.7f);
            }

            if (!directLine)
            {
                return waypoint + Vector2.up * 0.8f;
            }

            return (Vector2)player.transform.position + Vector2.up * 0.9f;
        }

        private static void MirrorPlayerFire(BodyScript ally)
        {
            WeaponScript weapon = ally.weapon;
            if (weapon == null || weapon.stats == null || ally.unarmed || weapon.dismembered)
            {
                return;
            }

            if (weapon.ammo <= 0)
            {
                weapon.ReloadWeapon();
                return;
            }

            weapon.Shoot();
            RemoveRecoil(ally);
        }

        private static void ApplyMoveToPoint(BodyScript ally, Vector2 waypoint, Vector2 finalDestination, float stopDistance, float stuckTime)
        {
            Vector2 allyPos = ally.transform.position;
            float xDelta = waypoint.x - allyPos.x;
            float yDelta = finalDestination.y - allyPos.y;

            if (Mathf.Abs(finalDestination.x - allyPos.x) <= stopDistance && Mathf.Abs(yDelta) <= 0.8f)
            {
                ally.CurrentState = BodyScript.EntityState.Idle;
            }
            else if (Mathf.Abs(xDelta) > stopDistance)
            {
                ally.CurrentState = xDelta > 0f ? BodyScript.EntityState.MoveRight : BodyScript.EntityState.MoveLeft;
            }
            else
            {
                ally.CurrentState = BodyScript.EntityState.Idle;
            }

            float moveDirection = 0f;
            if (ally.CurrentState == BodyScript.EntityState.MoveRight)
            {
                moveDirection = 1f;
            }
            else if (ally.CurrentState == BodyScript.EntityState.MoveLeft)
            {
                moveDirection = -1f;
            }

            ally.targetLookPos = finalDestination + Vector2.up * 0.7f;
            if (ally.targetLookPos.x > ally.transform.position.x && !ally.isRight)
            {
                ally.SwitchDir();
            }
            else if (ally.targetLookPos.x < ally.transform.position.x && ally.isRight)
            {
                ally.SwitchDir();
            }

            Vector2 aimVector = ally.targetLookPos - (Vector2)ally.Arms.position;
            float sideOffset = ally.isRight ? 0f : 180f;
            ally.armRotation = Mathf.Atan2(aimVector.x, 0f - aimVector.y) * Mathf.Rad2Deg - 90f + sideOffset;

            if (yDelta > 1.0f)
            {
                ally.upVector = 1f;
                if ((Mathf.Abs(xDelta) < 1.25f || stuckTime > 0.3f || HasWallAhead(ally, moveDirection)) &&
                    (ally.grounded || ally.isClimbing) && ally.IsConsc())
                {
                    ally.Jump();
                }
            }
            else if (yDelta < -0.85f)
            {
                ally.upVector = -1f;
                if (Mathf.Abs(xDelta) < 1.25f || stuckTime > 0.3f)
                {
                    ally.DropLedge();
                }
            }
            else
            {
                ally.upVector = 1f;
            }

            if (stuckTime > 0.8f && ally.IsConsc())
            {
                if (yDelta < -0.5f)
                {
                    ally.DropLedge();
                }
                else
                {
                    ally.Jump();
                }
            }
            else if (stuckTime > 0.35f && moveDirection != 0f && HasWallAhead(ally, moveDirection) && ally.grounded && ally.IsConsc())
            {
                ally.Jump();
            }
        }

        private static void ApplyFollowWaypoint(BodyScript ally, BodyScript player, Vector2 waypoint, bool directLine, float stuckTime)
        {
            Vector2 allyPos = ally.transform.position;
            Vector2 playerPos = player.transform.position;
            float xDelta = waypoint.x - allyPos.x;
            float playerXDelta = playerPos.x - allyPos.x;
            float playerYDelta = playerPos.y - allyPos.y;
            float followDistance = Plugin2.ConfiguredFollowDistance;
            float stopDistance = directLine ? Mathf.Max(0.75f, followDistance * 0.75f) : 0.45f;

            if (Mathf.Abs(xDelta) > stopDistance)
            {
                ally.CurrentState = xDelta > 0f ? BodyScript.EntityState.MoveRight : BodyScript.EntityState.MoveLeft;
            }
            else if (directLine && Mathf.Abs(playerXDelta) > followDistance + 0.3f)
            {
                ally.CurrentState = playerXDelta > 0f ? BodyScript.EntityState.MoveRight : BodyScript.EntityState.MoveLeft;
            }
            else
            {
                ally.CurrentState = BodyScript.EntityState.Idle;
            }

            float moveDirection = 0f;
            if (ally.CurrentState == BodyScript.EntityState.MoveRight)
            {
                moveDirection = 1f;
            }
            else if (ally.CurrentState == BodyScript.EntityState.MoveLeft)
            {
                moveDirection = -1f;
            }

            bool mirrorPlayerAim = ShouldMirrorPlayerAim(player);
            ally.targetLookPos = GetFollowLookPosition(ally, player, waypoint, directLine, mirrorPlayerAim);
            if (ally.targetLookPos.x > ally.transform.position.x && !ally.isRight)
            {
                ally.SwitchDir();
            }
            else if (ally.targetLookPos.x < ally.transform.position.x && ally.isRight)
            {
                ally.SwitchDir();
            }

            Vector2 aimVector = ally.targetLookPos - (Vector2)ally.Arms.position;
            float sideOffset = ally.isRight ? 0f : 180f;
            ally.armRotation = Mathf.Atan2(aimVector.x, 0f - aimVector.y) * Mathf.Rad2Deg - 90f + sideOffset;

            if (ShouldMirrorPlayerFire(player))
            {
                MirrorPlayerFire(ally);
            }

            if (playerYDelta > 1.0f)
            {
                ally.upVector = 1f;
                if ((directLine || Mathf.Abs(xDelta) < 1.25f || stuckTime > 0.3f || HasWallAhead(ally, moveDirection)) &&
                    (ally.grounded || ally.isClimbing) && ally.IsConsc())
                {
                    ally.Jump();
                }
            }
            else if (playerYDelta < -0.85f)
            {
                ally.upVector = -1f;
                if (directLine || Mathf.Abs(xDelta) < 1.25f || stuckTime > 0.3f)
                {
                    ally.DropLedge();
                }
            }
            else
            {
                ally.upVector = 1f;
            }

            if (stuckTime > 0.8f && ally.IsConsc())
            {
                if (playerYDelta < -0.5f)
                {
                    ally.DropLedge();
                }
                else
                {
                    ally.Jump();
                }
            }
            else if (stuckTime > 0.35f && moveDirection != 0f && HasWallAhead(ally, moveDirection) && ally.grounded && ally.IsConsc())
            {
                ally.Jump();
            }
        }

        private static void MaintainAllyResources(BodyScript body)
        {
            if (body == null)
            {
                return;
            }

            body.healthRegen = Mathf.Max(body.healthRegen, 3f);
            float minHealth = Mathf.Min(body.maxHealth, Mathf.Max(body.dyingStateTreshold + 10f, body.maxHealth * 0.35f));
            if (body.health < minHealth)
            {
                body.health = minHealth;
            }

            if (body.stamina < minHealth)
            {
                body.stamina = minHealth;
            }

            body.temporarySlowdown = 0f;
            body.isWalking = false;
            SetAllAmmo(body.ammoAmount, ReserveAmmo);
        }

        private static BodyScript FindBestAllyTarget(BodyScript ally)
        {
            if (ally == null)
            {
                return null;
            }

            BodyScript bestTarget = null;
            float bestDistance = float.MaxValue;
            float maxTargetDistance = Plugin2.ConfiguredBehavior == AutoAllyBehaviorMode.Defensive ? 14f : 9999f;
            float maxTargetDistanceSqr = maxTargetDistance * maxTargetDistance;
            BodyScript[] bodies = Object.FindObjectsOfType<BodyScript>();
            foreach (BodyScript candidate in bodies)
            {
                if (candidate == null || candidate == ally || !candidate.isAlive || candidate.team == ally.team)
                {
                    continue;
                }

                if (!HasAllyLineOfSight(ally, candidate))
                {
                    continue;
                }

                float distance = ((Vector2)candidate.transform.position - (Vector2)ally.transform.position).sqrMagnitude;
                if (distance > maxTargetDistanceSqr)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private static bool HasAllyLineOfSight(BodyScript ally, BodyScript target)
        {
            if (ally == null || target == null)
            {
                return false;
            }

            Vector2 origin = ally.headTransform != null ? (Vector2)ally.headTransform.position : (Vector2)ally.transform.position;
            Vector2 targetPoint = target.headTransform != null ? (Vector2)target.headTransform.position : (Vector2)target.transform.position;
            return !Physics2D.Linecast(origin, targetPoint, LayerMask.GetMask("Ground"));
        }

        public static void DrawAllyMarkers()
        {
            if (!Plugin2.ConfiguredAllyMarkers || Camera.main == null)
            {
                return;
            }

            EnsureMarkerTexture();
            foreach (AIScript ally in AutoAllies)
            {
                if (ally == null || ally.body == null || !ally.body.isAlive)
                {
                    continue;
                }

                Transform markerTransform = ally.body.headTransform != null ? ally.body.headTransform : ally.body.transform;
                Vector3 screen = Camera.main.WorldToScreenPoint(markerTransform.position + Vector3.up * 0.75f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float size = 14f;
                Rect rect = new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size);
                GUI.DrawTexture(rect, markerTexture);
            }
        }

        private static void EnsureMarkerTexture()
        {
            if (markerTexture != null)
            {
                return;
            }

            int size = 24;
            markerTexture = new Texture2D(size, size);
            markerTexture.hideFlags = HideFlags.HideAndDontSave;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color color = Color.clear;
                    if (distance <= 5.5f)
                    {
                        color = new Color(0.25f, 0.72f, 1f, 0.95f);
                    }
                    else if (distance <= 8.5f)
                    {
                        color = new Color(0.85f, 0.95f, 1f, 0.85f);
                    }

                    markerTexture.SetPixel(x, y, color);
                }
            }

            markerTexture.Apply();
        }

        private static void MaintainAllyCollisions(AIScript ai)
        {
            if (ai == null || ai.body == null)
            {
                return;
            }

            float nextTime;
            if (NextCollisionMaintenanceTimes.TryGetValue(ai, out nextTime) && Time.time < nextTime)
            {
                return;
            }

            NextCollisionMaintenanceTimes[ai] = Time.time + CollisionMaintenanceInterval;
            MaintainAllyPlayerCollision(ai.body);
            MaintainAllyToAllyCollision(ai);
        }

        private static void MaintainAllyWeaponIgnores(AIScript ai)
        {
            if (ai == null)
            {
                return;
            }

            BodyScript ally = ai.body;
            if (ally == null || ally.weapon == null || ally.weapon.ignoredCols == null ||
                !Plugin2.ConfiguredProtectPlayerFromAllyBullets)
            {
                return;
            }

            float nextTime;
            if (NextWeaponIgnoreMaintenanceTimes.TryGetValue(ai, out nextTime) && Time.time < nextTime)
            {
                return;
            }

            NextWeaponIgnoreMaintenanceTimes[ai] = Time.time + WeaponIgnoreMaintenanceInterval;
            BodyScript player = GetPlayerBody();
            if (player != null)
            {
                AddIgnoredColliders(ally.weapon, player);
            }

            foreach (AIScript other in AutoAllies)
            {
                if (other != null && other.body != null && other.body != ally)
                {
                    AddIgnoredColliders(ally.weapon, other.body);
                }
            }
        }

        private static void AddIgnoredColliders(WeaponScript weapon, BodyScript body)
        {
            Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D collider in colliders)
            {
                if (collider != null && !weapon.ignoredCols.Contains(collider))
                {
                    weapon.ignoredCols.Add(collider);
                }
            }
        }

        private static void MaintainAllyPlayerCollision(BodyScript ally)
        {
            BodyScript playerBody = GetPlayerBody();
            if (ally == null || playerBody == null || ally == playerBody)
            {
                return;
            }

            SetCollisionIgnored(ally, playerBody, !Plugin2.ConfiguredCollideWithPlayer);
        }

        private static void MaintainAllyToAllyCollision(AIScript ai)
        {
            foreach (AIScript other in AutoAllies)
            {
                if (other == null || other == ai || other.body == null)
                {
                    continue;
                }

                SetCollisionIgnored(ai.body, other.body, !Plugin2.ConfiguredCollideBetweenAllies);
            }
        }

        private static void SetCollisionIgnored(BodyScript first, BodyScript second, bool ignored)
        {
            if (first == null || second == null || first == second)
            {
                return;
            }

            Collider2D[] firstColliders = first.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] secondColliders = second.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D firstCollider in firstColliders)
            {
                if (firstCollider == null)
                {
                    continue;
                }

                foreach (Collider2D secondCollider in secondColliders)
                {
                    if (secondCollider != null && secondCollider != firstCollider)
                    {
                        Physics2D.IgnoreCollision(firstCollider, secondCollider, ignored);
                    }
                }
            }
        }

        private static BodyScript GetPlayerBody()
        {
            PlayerScript player = PlayerScript.player != null ? PlayerScript.player : Object.FindObjectOfType<PlayerScript>();
            if (player != null && player.bodyScript != null)
            {
                return player.bodyScript;
            }

            BodyScript[] bodies = Object.FindObjectsOfType<BodyScript>();
            foreach (BodyScript body in bodies)
            {
                if (body != null && body.isPlayer)
                {
                    return body;
                }
            }

            return null;
        }

        private static void RemoveRecoil(BodyScript body)
        {
            if (body != null)
            {
                body.currentRecoil = 0f;
            }
        }

        private static void SetAllAmmo(IList ammoList, int value)
        {
            if (ammoList == null)
            {
                return;
            }

            for (int i = 0; i < ammoList.Count; i++)
            {
                ammoList[i] = value;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerScript), "Start")]
    public static class Plugin2PlayerScriptStartPatch
    {
        private static void Postfix(PlayerScript __instance)
        {
            if (__instance != null)
            {
                __instance.StartCoroutine(AutoAllyHelpers.SpawnLevelAlliesWhenReady());
            }
        }
    }

    [HarmonyPatch(typeof(PlayerScript), "Update")]
    public static class Plugin2PlayerScriptUpdatePatch
    {
        private static void Postfix(PlayerScript __instance)
        {
            if (__instance != null)
            {
                AutoAllyHelpers.RecordPlayerBreadcrumb(__instance.bodyScript);
            }
        }
    }

    [HarmonyPatch(typeof(WeaponScript), "Shoot")]
    public static class Plugin2WeaponScriptShootPatch
    {
        private static bool Prefix(WeaponScript __instance)
        {
            if (AutoAllyHelpers.ShouldBlockWeaponShoot(__instance))
            {
                return false;
            }

            AutoAllyHelpers.CaptureAllyRecoil(__instance);
            return true;
        }

        private static void Postfix(WeaponScript __instance)
        {
            AutoAllyHelpers.RestoreAllyRecoil(__instance);
        }
    }

    [HarmonyPatch(typeof(AIScript), "Update")]
    public static class Plugin2AIScriptUpdatePatch
    {
        private static void Postfix(AIScript __instance)
        {
            AutoAllyHelpers.UpdateAutoAlly(__instance);
        }
    }
}
