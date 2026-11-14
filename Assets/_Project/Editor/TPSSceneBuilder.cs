using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TPSShooter.CameraSystem;
using TPSShooter.Combat;
using TPSShooter.Enemies;
using TPSShooter.Player;
using TPSShooter.UI;
using TPSShooter.Weapons;

namespace TPSShooter.EditorTools
{
    /// <summary>
    /// Menú: Tools > TPS Shooter > Crear escena de prueba
    /// Genera materiales, el prefab del jugador (personaje + arma + cámara), el prefab del HUD
    /// (incluye controles táctiles), el prefab de enemigo y una escena de pruebas con bots,
    /// dianas, cajas físicas y obstáculos.
    /// </summary>
    public static class TPSSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string MaterialsPath = Root + "/Materials";
        private const string PrefabsPath = Root + "/Prefabs";
        private const string ScenesPath = Root + "/Scenes";
        private const string ScenePath = ScenesPath + "/TPS_TestScene.unity";
        private const int IgnoreRaycastLayer = 2; // el jugador vive aquí para que su propio rayo y la cámara no lo detecten

        private class Mats
        {
            public Material ground, wall, crate, target, player, visor, weapon, tracer, impact, enemy, enemyTracer;
        }

        [MenuItem("Tools/TPS Shooter/Crear escena de prueba")]
        public static void BuildTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MaterialsPath);
            EnsureFolder(PrefabsPath);
            EnsureFolder(ScenesPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mats mats = CreateMaterials();

            CreateLighting();
            CreateEnvironment(mats);
            GameObject rig = CreatePlayerRig(mats, out RaycastWeapon weapon);
            Transform playerTransform = rig.transform.Find("Player");
            GameObject hud = CreateHUD(weapon, playerTransform.GetComponent<PlayerHealth>());
            CreateEventSystem();

            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, PrefabsPath + "/TPS_PlayerRig.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, PrefabsPath + "/TPS_HUD.prefab", InteractionMode.AutomatedAction);
            CreateEnemies(mats, playerTransform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = rig;
            Debug.Log("[TPS Shooter] Escena creada en " + ScenePath + ". Pulsa Play para probar.");
        }

        // ==================================================================
        // Materiales
        // ==================================================================
        private static Mats CreateMaterials()
        {
            return new Mats
            {
                ground = GetOrCreateMat("M_Ground", new Color(0.35f, 0.38f, 0.35f)),
                wall = GetOrCreateMat("M_Wall", new Color(0.55f, 0.55f, 0.6f)),
                crate = GetOrCreateMat("M_Crate", new Color(0.7f, 0.5f, 0.25f)),
                target = GetOrCreateMat("M_Target", new Color(0.85f, 0.2f, 0.2f)),
                player = GetOrCreateMat("M_Player", new Color(0.2f, 0.45f, 0.85f)),
                visor = GetOrCreateMat("M_Visor", new Color(0.1f, 0.1f, 0.1f)),
                weapon = GetOrCreateMat("M_Weapon", new Color(0.15f, 0.15f, 0.15f)),
                impact = GetOrCreateMat("M_Impact", new Color(1f, 0.85f, 0.3f)),
                tracer = GetOrCreateMat("M_Tracer", new Color(1f, 0.9f, 0.5f), unlit: true),
                enemy = GetOrCreateMat("M_Enemy", new Color(0.55f, 0.2f, 0.65f)),
                enemyTracer = GetOrCreateMat("M_EnemyTracer", new Color(1f, 0.3f, 0.25f), unlit: true),
            };
        }

        private static Material GetOrCreateMat(string name, Color color, bool unlit = false)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(unlit ? UnlitShader() : LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Shader LitShader()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultShader != null) return rp.defaultShader; // URP/HDRP
            return Shader.Find("Standard");                                     // Built-in
        }

        private static Shader UnlitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s != null ? s : Shader.Find("Sprites/Default");
        }

        // ==================================================================
        // Escena
        // ==================================================================
        private static void CreateLighting()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateEnvironment(Mats m)
        {
            var env = new GameObject("Environment").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env);
            ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100 x 100 m
            ground.GetComponent<Renderer>().sharedMaterial = m.ground;

            // Coberturas / paredes
            Block(env, "Wall_A", new Vector3(-6f, 1f, 10f), new Vector3(6f, 2f, 0.5f), m.wall);
            Block(env, "Wall_B", new Vector3(6f, 0.6f, 8f), new Vector3(4f, 1.2f, 0.5f), m.wall);
            Block(env, "Wall_C", new Vector3(0f, 2f, 30f), new Vector3(30f, 4f, 1f), m.wall);
            Block(env, "Pillar", new Vector3(-2f, 2f, 4f), new Vector3(1f, 4f, 1f), m.wall);

            // Plataforma para probar el salto y rampa
            Block(env, "Platform", new Vector3(10f, 0.5f, -4f), new Vector3(3f, 1f, 3f), m.wall);
            var ramp = Block(env, "Ramp", new Vector3(-10f, 0.9f, -2f), new Vector3(3f, 0.4f, 8f), m.wall);
            ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

            // Cajas con físicas (reaccionan a los disparos)
            var crates = new GameObject("Crates").transform;
            crates.SetParent(env);
            for (int i = 0; i < 6; i++)
            {
                var crate = Block(crates, $"Crate_{i}", new Vector3(3f + (i % 3) * 1.1f, 0.5f + (i / 3) * 1.05f, 14f), Vector3.one, m.crate);
                var rb = crate.AddComponent<Rigidbody>();
                rb.mass = 5f;
            }

            // Dianas
            var targets = new GameObject("Targets").transform;
            targets.SetParent(env);
            Vector3[] positions =
            {
                new Vector3(-8f, 1f, 18f), new Vector3(-3f, 1f, 22f), new Vector3(2f, 1f, 20f),
                new Vector3(8f, 1f, 24f), new Vector3(12f, 1f, 16f),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                t.name = $"Target_{i}";
                t.transform.SetParent(targets);
                t.transform.position = positions[i];
                t.GetComponent<Renderer>().sharedMaterial = m.target;
                t.AddComponent<Health>();
                t.AddComponent<TargetDummy>();
            }
        }

        private static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ==================================================================
        // Jugador + arma + cámara  (=> prefab TPS_PlayerRig)
        // ==================================================================
        private static GameObject CreatePlayerRig(Mats m, out RaycastWeapon weapon)
        {
            var rig = new GameObject("TPS_PlayerRig");

            // --- Personaje ---
            var player = new GameObject("Player");
            player.transform.SetParent(rig.transform);
            player.tag = "Player";

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            var input = player.AddComponent<PlayerInputHandler>();
            var controller = player.AddComponent<ThirdPersonController>();
            player.AddComponent<PlayerHealth>();

            // Cuerpo visual (sin collider: el CharacterController ya colisiona)
            var body = Visual(PrimitiveType.Capsule, "Body", player.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), m.player);
            Visual(PrimitiveType.Cube, "Visor", body.transform, new Vector3(0f, 0.7f, 0.4f), new Vector3(0.6f, 0.15f, 0.25f), m.visor);

            // --- Arma ---
            var weaponGO = new GameObject("Weapon");
            weaponGO.transform.SetParent(player.transform);
            weaponGO.transform.localPosition = new Vector3(0.32f, 1.25f, 0.35f);
            Visual(PrimitiveType.Cube, "Model", weaponGO.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.08f, 0.12f, 0.6f), m.weapon);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weaponGO.transform);
            muzzle.localPosition = new Vector3(0f, 0.02f, 0.47f);

            var flash = new GameObject("MuzzleFlash").AddComponent<Light>();
            flash.transform.SetParent(muzzle, false);
            flash.type = LightType.Point;
            flash.range = 4f;
            flash.intensity = 4f;
            flash.color = new Color(1f, 0.75f, 0.35f);
            flash.enabled = false;

            var lr = weaponGO.AddComponent<LineRenderer>();
            lr.sharedMaterial = m.tracer;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.025f;
            lr.endWidth = 0.01f;
            lr.startColor = new Color(1f, 0.9f, 0.5f, 1f);
            lr.endColor = new Color(1f, 0.9f, 0.5f, 0.2f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;

            weapon = weaponGO.AddComponent<RaycastWeapon>();

            SetLayerRecursively(player, IgnoreRaycastLayer);

            // --- Cámara ---
            var camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(rig.transform);
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0.5f, 2f, -3.2f);
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.fieldOfView = 60f;
            camGO.AddComponent<AudioListener>();
            var tpc = camGO.AddComponent<ThirdPersonCamera>();

            // --- Referencias ---
            SetRef(controller, "cameraTransform", camGO.transform);
            SetRef(tpc, "target", player.transform);
            SetRef(tpc, "input", input);
            SetRef(weapon, "input", input);
            SetRef(weapon, "aimCamera", cam);
            SetRef(weapon, "cameraRig", tpc);
            SetRef(weapon, "muzzle", muzzle);
            SetRef(weapon, "tracer", lr);
            SetRef(weapon, "muzzleFlash", flash);
            SetRef(weapon, "impactMaterial", m.impact);

            return rig;
        }

        private static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ==================================================================
        // Enemigos  (=> prefab TPS_Enemy)
        // ==================================================================
        private static void CreateEnemies(Mats m, Transform player)
        {
            var parent = new GameObject("Enemies").transform;
            Vector3[] positions =
            {
                new Vector3(-12f, 0f, 22f), new Vector3(0f, 0f, 26f), new Vector3(14f, 0f, 20f),
            };

            GameObject first = CreateEnemy(m, player);
            first.transform.SetParent(parent);
            first.transform.SetPositionAndRotation(positions[0], Quaternion.Euler(0f, 180f, 0f));
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(first, PrefabsPath + "/TPS_Enemy.prefab", InteractionMode.AutomatedAction);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject enemy = i == 0 ? first : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                enemy.name = $"Enemy_{i}";
                enemy.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0f, 180f, 0f));
                SetRef(enemy.GetComponent<EnemyAI>(), "player", player); // referencia al jugador de la escena
            }
        }

        private static GameObject CreateEnemy(Mats m, Transform player)
        {
            var enemy = new GameObject("Enemy");

            // CharacterController = collider + movimiento sin NavMesh
            var cc = enemy.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            enemy.AddComponent<Health>();
            var ai = enemy.AddComponent<EnemyAI>();

            var body = Visual(PrimitiveType.Capsule, "Body", enemy.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), m.enemy);
            Visual(PrimitiveType.Cube, "Visor", body.transform, new Vector3(0f, 0.7f, 0.4f), new Vector3(0.6f, 0.15f, 0.25f), m.target);

            var gun = new GameObject("Weapon").transform;
            gun.SetParent(enemy.transform, false);
            gun.localPosition = new Vector3(0.32f, 1.25f, 0.35f);
            Visual(PrimitiveType.Cube, "Model", gun, new Vector3(0f, 0f, 0.15f), new Vector3(0.08f, 0.12f, 0.6f), m.weapon);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = new Vector3(0f, 0.02f, 0.47f);

            var flash = new GameObject("MuzzleFlash").AddComponent<Light>();
            flash.transform.SetParent(muzzle, false);
            flash.type = LightType.Point;
            flash.range = 4f;
            flash.intensity = 4f;
            flash.color = new Color(1f, 0.5f, 0.3f);
            flash.enabled = false;

            var lr = gun.gameObject.AddComponent<LineRenderer>();
            lr.sharedMaterial = m.enemyTracer;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.025f;
            lr.endWidth = 0.01f;
            lr.startColor = new Color(1f, 0.35f, 0.3f, 1f);
            lr.endColor = new Color(1f, 0.35f, 0.3f, 0.2f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;

            SetRef(ai, "muzzle", muzzle);
            SetRef(ai, "tracer", lr);
            SetRef(ai, "muzzleFlash", flash);
            return enemy;
        }

        // ==================================================================
        // HUD + controles táctiles  (=> prefab TPS_HUD)
        // ==================================================================
        private static GameObject CreateHUD(RaycastWeapon weapon, PlayerHealth playerHealth)
        {
            var canvasGO = new GameObject("TPS_HUD");
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var hud = canvasGO.AddComponent<HUDController>();

            Transform root = canvasGO.transform;
            Font font = GetFont();
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Vector2 center = new Vector2(0.5f, 0.5f);

            // --- Controles táctiles (primero, para que el resto del HUD quede por encima) ---
            var mobile = Rect("MobileControls", root, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(mobile, Vector2.zero, Vector2.one);
            mobile.gameObject.AddComponent<MobileControlsPanel>();

            var lookImg = Img("LookArea", mobile, center, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            Stretch(lookImg.rectTransform, new Vector2(0.4f, 0f), Vector2.one);
            var lookArea = lookImg.gameObject.AddComponent<TouchLookArea>();

            var joyBg = Img("Joystick", mobile, Vector2.zero, new Vector2(230f, 230f), new Vector2(260f, 260f), new Color(1f, 1f, 1f, 0.25f), knob);
            var joyHandle = Img("Handle", joyBg.transform, center, Vector2.zero, new Vector2(120f, 120f), new Color(1f, 1f, 1f, 0.7f), knob);
            joyHandle.raycastTarget = false;
            var joystick = joyBg.gameObject.AddComponent<VirtualJoystick>();
            SetRef(joystick, "background", joyBg.rectTransform);
            SetRef(joystick, "handle", joyHandle.rectTransform);

            Vector2 br = new Vector2(1f, 0f);
            MakeButton(mobile, "Btn_Fire", "FUEGO", MobileAction.Fire, br, new Vector2(-230f, 230f), 190f, new Color(0.9f, 0.25f, 0.2f, 0.55f), knob, font, lookArea, false);
            MakeButton(mobile, "Btn_Jump", "SALTAR", MobileAction.Jump, br, new Vector2(-450f, 130f), 130f, new Color(1f, 1f, 1f, 0.3f), knob, font, null, false);
            MakeButton(mobile, "Btn_Aim", "APUNTAR", MobileAction.Aim, br, new Vector2(-450f, 330f), 125f, new Color(1f, 1f, 1f, 0.3f), knob, font, null, true);
            MakeButton(mobile, "Btn_Reload", "RECARGAR", MobileAction.Reload, br, new Vector2(-230f, 460f), 115f, new Color(1f, 1f, 1f, 0.3f), knob, font, null, false);
            MakeButton(mobile, "Btn_Sprint", "CORRER", MobileAction.Sprint, Vector2.zero, new Vector2(230f, 480f), 115f, new Color(1f, 1f, 1f, 0.3f), knob, font, null, false);

            // --- Flash rojo de daño (pantalla completa, no bloquea toques) ---
            var flashImg = NoRaycast(Img("DamageFlash", root, center, Vector2.zero, Vector2.zero, new Color(0.8f, 0f, 0f, 0f)));
            Stretch(flashImg.rectTransform, Vector2.zero, Vector2.one);

            // --- Barra de vida (arriba a la izquierda) ---
            var barBg = NoRaycast(Img("HealthBar", root, new Vector2(0f, 1f), new Vector2(220f, -95f), new Vector2(360f, 28f), new Color(0f, 0f, 0f, 0.5f)));
            var barFill = NoRaycast(Img("Fill", barBg.transform, center, Vector2.zero, Vector2.zero, new Color(0.3f, 0.85f, 0.35f, 0.9f)));
            Stretch(barFill.rectTransform, Vector2.zero, Vector2.one);
            barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            var healthText = Txt("HealthText", barBg.transform, center, Vector2.zero, new Vector2(360f, 28f), "100", 20, TextAnchor.MiddleCenter, font);

            // --- Mira ---
            var crosshair = Rect("Crosshair", root, center, Vector2.zero, new Vector2(40f, 40f));
            Color ch = new Color(1f, 1f, 1f, 0.9f);
            NoRaycast(Img("Dot", crosshair, center, Vector2.zero, new Vector2(4f, 4f), ch));
            NoRaycast(Img("Up", crosshair, center, new Vector2(0f, 12f), new Vector2(2f, 10f), ch));
            NoRaycast(Img("Down", crosshair, center, new Vector2(0f, -12f), new Vector2(2f, 10f), ch));
            NoRaycast(Img("Left", crosshair, center, new Vector2(-12f, 0f), new Vector2(10f, 2f), ch));
            NoRaycast(Img("Right", crosshair, center, new Vector2(12f, 0f), new Vector2(10f, 2f), ch));

            // --- Marcador de impacto (X) ---
            var hitMarker = Rect("HitMarker", root, center, Vector2.zero, new Vector2(40f, 40f));
            var hmGroup = hitMarker.gameObject.AddComponent<CanvasGroup>();
            hmGroup.blocksRaycasts = false;
            hmGroup.interactable = false;
            var a = NoRaycast(Img("A", hitMarker, center, Vector2.zero, new Vector2(3f, 30f), Color.white));
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var b = NoRaycast(Img("B", hitMarker, center, Vector2.zero, new Vector2(3f, 30f), Color.white));
            b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

            // --- Textos ---
            var ammo = Txt("AmmoText", root, new Vector2(1f, 1f), new Vector2(-170f, -70f), new Vector2(300f, 80f), "30 / 120", 56, TextAnchor.MiddleRight, font);
            ammo.fontStyle = FontStyle.Bold;
            var reload = Txt("ReloadText", root, center, new Vector2(0f, -90f), new Vector2(600f, 50f), "", 30, TextAnchor.MiddleCenter, font);
            Txt("HelpText", root, new Vector2(0f, 1f), new Vector2(420f, -40f), new Vector2(800f, 60f),
                "WASD mover · Shift correr · Espacio saltar · Clic izq. disparar · Clic der. apuntar · R recargar · Esc soltar ratón",
                18, TextAnchor.UpperLeft, font).color = new Color(1f, 1f, 1f, 0.7f);

            SetRef(hud, "weapon", weapon);
            SetRef(hud, "ammoText", ammo);
            SetRef(hud, "reloadText", reload);
            SetRef(hud, "hitMarker", hmGroup);
            SetRef(hud, "playerHealth", playerHealth);
            SetRef(hud, "healthText", healthText);
            SetRef(hud, "healthBarFill", barFill.rectTransform);
            SetRef(hud, "damageFlash", flashImg);

            SetLayerRecursively(canvasGO, LayerMask.NameToLayer("UI"));
            return canvasGO;
        }

        private static void MakeButton(Transform parent, string name, string label, MobileAction action, Vector2 anchor,
                                       Vector2 pos, float size, Color color, Sprite sprite, Font font,
                                       TouchLookArea dragToLook, bool toggle)
        {
            var img = Img(name, parent, anchor, pos, new Vector2(size, size), color, sprite);
            var btn = img.gameObject.AddComponent<MobileButton>();
            var so = new SerializedObject(btn);
            so.FindProperty("action").enumValueIndex = (int)action;
            so.FindProperty("toggleMode").boolValue = toggle;
            so.FindProperty("dragToLook").objectReferenceValue = dragToLook;
            so.ApplyModifiedPropertiesWithoutUndo();

            var t = Txt("Label", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), label,
                        Mathf.RoundToInt(size * 0.16f), TextAnchor.MiddleCenter, font);
            t.raycastTarget = false;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // ==================================================================
        // Utilidades de UI
        // ==================================================================
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            var img = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            return img;
        }

        private static Image NoRaycast(Image img)
        {
            img.raycastTarget = false;
            return img;
        }

        private static Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text,
                                int fontSize, TextAnchor align, Font font)
        {
            var t = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (font != null) t.font = font;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        private static Font GetFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 2022.2+
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // ==================================================================
        // Utilidades generales
        // ==================================================================
        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[TPS Shooter] Campo '{field}' no encontrado en {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
