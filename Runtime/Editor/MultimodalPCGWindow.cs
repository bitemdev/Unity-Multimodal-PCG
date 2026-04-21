using System;
using System.IO;
using System.Linq;
using PCG.Core;
using PCG.Modules.Audio;
using PCG.Modules.Entities;
using PCG.Modules.Environment;
using PCG.Modules.Tools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PCG.Editor
{
    public class MultimodalPCGWindow : EditorWindow
    {
        private const string WindowTitle = "Multimodal PCG";
        private Vector2 _scrollPosition;

        [MenuItem("Window/Multimodal PCG")]
        public static void ShowWindow()
        {
            MultimodalPCGWindow window = GetWindow<MultimodalPCGWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnHierarchyChange()
        {
            Repaint();
        }

        private void OnProjectChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            MultimodalPCGSetupContext context = MultimodalPCGSceneBuilder.FindExistingSetup();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Unity Multimodal PCG", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a ready-to-test demo hierarchy in the current scene with a local writable config, runtime UI, environment generation, audio, and entity spawning already wired.",
                MessageType.Info);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSceneStatus(context);

            EditorGUILayout.Space(8f);
            DrawPrimaryActions(context);

            EditorGUILayout.Space(8f);
            DrawExistingSetupActions(context);

            EditorGUILayout.Space(8f);
            DrawNotes();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSceneStatus(MultimodalPCGSetupContext context)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Scene", activeScene.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(activeScene.path) ? "<Unsaved Scene>" : activeScene.path);
            EditorGUILayout.LabelField("PCG Root", context.Root != null ? context.Root.name : "Not found");
            EditorGUILayout.LabelField("Local Config", context.LocalConfig != null ? AssetDatabase.GetAssetPath(context.LocalConfig) : "Not created yet");
            EditorGUILayout.LabelField("Main Camera", context.MainCamera != null ? context.MainCamera.name : "Will be created if missing");
            EditorGUILayout.LabelField("Event System", context.EventSystem != null ? context.EventSystem.name : "Will be created if missing");
        }

        private void DrawPrimaryActions(MultimodalPCGSetupContext context)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(context.Root != null);
                if (GUILayout.Button("Create Demo Setup", GUILayout.Height(36f)))
                {
                    MultimodalPCGSceneBuilder.CreateDemoSetup();
                    Repaint();
                }
                EditorGUI.EndDisabledGroup();

                if (context.Root != null)
                {
                    EditorGUILayout.HelpBox(
                        "This scene already contains a Multimodal PCG setup. The tool keeps the flow safe by refusing to create duplicates.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawExistingSetupActions(MultimodalPCGSetupContext context)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(context.Root == null);

                if (GUILayout.Button("Select Existing Setup"))
                {
                    Selection.activeGameObject = context.Root;
                    EditorGUIUtility.PingObject(context.Root);
                }

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(context.EnvironmentManager == null);
                if (GUILayout.Button("Generate Level Now"))
                {
                    context.EnvironmentManager.GenerateLevel();
                    Selection.activeGameObject = context.Root;
                }

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(context.LocalConfig == null);
                if (GUILayout.Button("Ping Local Config"))
                {
                    EditorGUIUtility.PingObject(context.LocalConfig);
                    Selection.activeObject = context.LocalConfig;
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void DrawNotes()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("- The setup appends safely to the current scene and does not touch unrelated objects.");
                EditorGUILayout.LabelField("- The created runtime UI leaves generation manual so users can inspect the hierarchy before Play.");
                EditorGUILayout.LabelField("- The local config is stored under Assets/MultimodalPCG/Generated and can be edited freely.");
            }
        }
    }

    internal sealed class MultimodalPCGSetupContext
    {
        public GameObject Root;
        public EnvironmentManager EnvironmentManager;
        public PCGConfiguration LocalConfig;
        public Camera MainCamera;
        public EventSystem EventSystem;
    }

    internal static class MultimodalPCGSceneBuilder
    {
        private const string RootName = "---- PCG ----";
        private const string EntitiesRootName = "---- ENTITIES ----";
        private const string EnvironmentRootName = "---- ENVIRONMENT ----";
        private const string AudioRootName = "---- AUDIO ----";
        private const string CanvasName = "Main Canvas";
        private const string GenerateButtonName = "GenerateLevelButton";
        private const string ConfigFolderRoot = "Assets/MultimodalPCG";
        private const string ConfigFolderGenerated = "Assets/MultimodalPCG/Generated";
        private const string ConfigAssetPath = "Assets/MultimodalPCG/Generated/MultimodalPCG-DemoConfig.asset";
        private const int FallbackFloorLayer = 2; // Ignore Raycast is a stable built-in layer in every Unity project.

        private static readonly float[] DefaultPentatonicScale =
        {
            65.41f, 77.78f, 87.31f, 98.00f, 116.54f,
            130.81f, 155.56f, 174.61f, 196.00f, 233.08f,
            261.63f, 311.13f, 349.23f, 392.00f, 466.16f,
            523.25f
        };

        public static MultimodalPCGSetupContext FindExistingSetup()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            EnvironmentManager environmentManager = root != null ? root.GetComponentInChildren<EnvironmentManager>(true) : UnityEngine.Object.FindFirstObjectByType<EnvironmentManager>();

            return new MultimodalPCGSetupContext
            {
                Root = root,
                EnvironmentManager = environmentManager,
                LocalConfig = AssetDatabase.LoadAssetAtPath<PCGConfiguration>(ConfigAssetPath),
                MainCamera = Camera.main,
                EventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>()
            };
        }

        public static void CreateDemoSetup()
        {
            MultimodalPCGSetupContext existing = FindExistingSetup();
            if (existing.Root != null)
            {
                Selection.activeGameObject = existing.Root;
                EditorGUIUtility.PingObject(existing.Root);
                Debug.LogWarning("[Multimodal PCG] Demo setup already exists in this scene. Reusing the existing hierarchy instead of creating a duplicate.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Multimodal PCG Demo Setup");

            try
            {
                PCGConfiguration config = EnsureLocalConfigAsset();
                int floorLayer = ResolveFloorLayer();
                LayerMask floorLayerMask = 1 << floorLayer;

                Material levelMaterial = LoadRequiredAssetByPathSuffix<Material>("Runtime/Art/Shaders/Mat_Level.mat");
                GameObject playerPrefab = LoadRequiredAssetByPathSuffix<GameObject>("Runtime/Prefabs/Player.prefab");
                GameObject enemyPrefab = LoadRequiredAssetByPathSuffix<GameObject>("Runtime/Prefabs/Enemy.prefab");
                GameObject objectPrefab = LoadRequiredAssetByPathSuffix<GameObject>("Runtime/Prefabs/Object.prefab");
                GameObject exitPrefab = LoadRequiredAssetByPathSuffix<GameObject>("Runtime/Prefabs/Exit.prefab");

                GameObject root = CreateGameObject(RootName, null);
                GameObject entitiesRoot = CreateGameObject(EntitiesRootName, root.transform);
                GameObject environmentRoot = CreateGameObject(EnvironmentRootName, root.transform);
                GameObject audioRoot = CreateGameObject(AudioRootName, root.transform);

                GameObject environmentManagerObject = CreateGameObject("EnvironmentManager", environmentRoot.transform);
                EnvironmentManager environmentManager = Undo.AddComponent<EnvironmentManager>(environmentManagerObject);
                RuntimeNavMeshBuilder navMeshBuilder = Undo.AddComponent<RuntimeNavMeshBuilder>(environmentManagerObject);
                NavMeshSurface navMeshSurface = Undo.AddComponent<NavMeshSurface>(environmentManagerObject);
                Undo.AddComponent<MassiveBenchmarkRunner>(environmentManagerObject);

                GameObject floorObject = CreateMeshHolder("Floor", environmentManagerObject.transform, floorLayer);
                GameObject wallsObject = CreateMeshHolder("Walls", environmentManagerObject.transform, 0);

                GameObject enemyPoolObject = CreateGameObject("EnemyPool", entitiesRoot.transform);
                EntityPool enemyPool = Undo.AddComponent<EntityPool>(enemyPoolObject);

                GameObject objectPoolObject = CreateGameObject("ObjectPool", entitiesRoot.transform);
                EntityPool objectPool = Undo.AddComponent<EntityPool>(objectPoolObject);

                GameObject entityManagerObject = CreateGameObject("EntityManager", entitiesRoot.transform);
                EntityManager entityManager = Undo.AddComponent<EntityManager>(entityManagerObject);

                GameObject synthObject = CreateGameObject("Synth", audioRoot.transform);
                AudioSource synthAudioSource = Undo.AddComponent<AudioSource>(synthObject);
                ProceduralAudioSource synth = Undo.AddComponent<ProceduralAudioSource>(synthObject);
                ConfigureSynthAudioSource(synthAudioSource);

                GameObject musicManagerObject = CreateGameObject("MusicManager", audioRoot.transform);
                MusicGenerator musicGenerator = Undo.AddComponent<MusicGenerator>(musicManagerObject);

                Camera mainCamera = EnsureMainCamera();
                EnsureDirectionalLight();
                EnsureEventSystem();
                Canvas canvas = CreateCanvas(root.transform);
                CreateGenerateButton(canvas.transform, environmentManager);

                ConfigureEnvironmentManager(
                    environmentManager,
                    config,
                    navMeshBuilder,
                    floorObject,
                    wallsObject,
                    levelMaterial);

                ConfigureNavMeshBuilder(navMeshBuilder, navMeshSurface, floorLayerMask);
                ConfigureEntityPool(enemyPool, config, enemyPrefab, EntityType.Enemy);
                ConfigureEntityPool(objectPool, config, objectPrefab, EntityType.Object);
                ConfigureEntityManager(entityManager, environmentManager, playerPrefab, exitPrefab, enemyPool, objectPool);
                ConfigureMusicGenerator(musicGenerator, environmentManager, config, synth);

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = root;
                EditorGUIUtility.PingObject(root);

                Debug.Log(
                    $"[Multimodal PCG] Demo setup created successfully in scene '{SceneManager.GetActiveScene().name}'. " +
                    $"Config: {AssetDatabase.GetAssetPath(config)}. " +
                    $"Camera: {(mainCamera != null ? mainCamera.name : "reused existing scene camera")}.");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void ConfigureEnvironmentManager(
            EnvironmentManager environmentManager,
            PCGConfiguration config,
            RuntimeNavMeshBuilder navMeshBuilder,
            GameObject floorObject,
            GameObject wallsObject,
            Material levelMaterial)
        {
            SerializedObject serializedObject = new SerializedObject(environmentManager);
            serializedObject.FindProperty("_config").objectReferenceValue = config;
            serializedObject.FindProperty("_navMeshBuilder").objectReferenceValue = navMeshBuilder;
            serializedObject.FindProperty("_algorithmType").enumValueIndex = (int)GenerationAlgorithm.Maze_Backtracker;
            serializedObject.FindProperty("_floorMeshFilter").objectReferenceValue = floorObject.GetComponent<MeshFilter>();
            serializedObject.FindProperty("_floorMeshRenderer").objectReferenceValue = floorObject.GetComponent<MeshRenderer>();
            serializedObject.FindProperty("_floorMeshCollider").objectReferenceValue = floorObject.GetComponent<MeshCollider>();
            serializedObject.FindProperty("_floorMaterial").objectReferenceValue = levelMaterial;
            serializedObject.FindProperty("_wallsMeshFilter").objectReferenceValue = wallsObject.GetComponent<MeshFilter>();
            serializedObject.FindProperty("_wallsMeshRenderer").objectReferenceValue = wallsObject.GetComponent<MeshRenderer>();
            serializedObject.FindProperty("_wallsMaterial").objectReferenceValue = levelMaterial;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNavMeshBuilder(RuntimeNavMeshBuilder navMeshBuilder, NavMeshSurface navMeshSurface, LayerMask floorLayerMask)
        {
            SerializedObject serializedObject = new SerializedObject(navMeshBuilder);
            serializedObject.FindProperty("_floorLayer").intValue = floorLayerMask.value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            navMeshSurface.layerMask = floorLayerMask;
            navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        }

        private static void ConfigureEntityPool(EntityPool pool, PCGConfiguration config, GameObject prefab, EntityType entityType)
        {
            SerializedObject serializedObject = new SerializedObject(pool);
            serializedObject.FindProperty("_prefab").objectReferenceValue = prefab;
            serializedObject.FindProperty("_entityType").enumValueIndex = (int)entityType;
            serializedObject.FindProperty("_config").objectReferenceValue = config;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEntityManager(
            EntityManager entityManager,
            EnvironmentManager environmentManager,
            GameObject playerPrefab,
            GameObject exitPrefab,
            EntityPool enemyPool,
            EntityPool objectPool)
        {
            SerializedObject serializedObject = new SerializedObject(entityManager);
            serializedObject.FindProperty("_environmentManager").objectReferenceValue = environmentManager;
            serializedObject.FindProperty("_globalHeightOffset").floatValue = 0.2f;
            serializedObject.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
            serializedObject.FindProperty("_exitPrefab").objectReferenceValue = exitPrefab;
            serializedObject.FindProperty("_enemyPool").objectReferenceValue = enemyPool;
            serializedObject.FindProperty("_objectPool").objectReferenceValue = objectPool;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMusicGenerator(
            MusicGenerator musicGenerator,
            EnvironmentManager environmentManager,
            PCGConfiguration config,
            ProceduralAudioSource synth)
        {
            SerializedObject serializedObject = new SerializedObject(musicGenerator);
            serializedObject.FindProperty("_environmentManager").objectReferenceValue = environmentManager;
            serializedObject.FindProperty("_pcgConfig").objectReferenceValue = config;
            serializedObject.FindProperty("_synth").objectReferenceValue = synth;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            GameObject canvasObject = CreateUIObject(CanvasName, parent);
            RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Undo.AddComponent<CanvasScaler>(canvasObject);
            Undo.AddComponent<GraphicRaycaster>(canvasObject);

            return canvas;
        }

        private static void CreateGenerateButton(Transform canvasTransform, EnvironmentManager environmentManager)
        {
            GameObject buttonObject = CreateUIObject(GenerateButtonName, canvasTransform);
            buttonObject.layer = LayerMask.NameToLayer("UI");

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -24f);
            buttonRect.sizeDelta = new Vector2(300f, 50f);

            Image image = Undo.AddComponent<Image>(buttonObject);
            image.color = new Color(0.88f, 0.89f, 0.92f, 0.96f);

            Button button = Undo.AddComponent<Button>(buttonObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            UnityEventTools.AddPersistentListener(button.onClick, environmentManager.GenerateLevel);

            GameObject textObject = CreateUIObject("Text", buttonObject.transform);
            textObject.layer = LayerMask.NameToLayer("UI");

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = Undo.AddComponent<Text>(textObject);
            text.text = "Generate Level";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.11f, 0.16f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 24;
        }

        private static void ConfigureSynthAudioSource(AudioSource audioSource)
        {
            audioSource.playOnAwake = true;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        private static EventSystem EnsureEventSystem()
        {
            EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                ConfigureEventSystemInputModule(eventSystem.gameObject);
                return eventSystem;
            }

            GameObject eventSystemObject = CreateGameObject("EventSystem", null);
            eventSystem = Undo.AddComponent<EventSystem>(eventSystemObject);
            ConfigureEventSystemInputModule(eventSystemObject);
            return eventSystem;
        }

        private static void ConfigureEventSystemInputModule(GameObject eventSystemObject)
        {
            Type inputSystemModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModuleType != null)
            {
                StandaloneInputModule standaloneModule = eventSystemObject.GetComponent<StandaloneInputModule>();
                if (standaloneModule != null)
                {
                    Undo.DestroyObjectImmediate(standaloneModule);
                }

                if (eventSystemObject.GetComponent(inputSystemModuleType) == null)
                {
                    Undo.AddComponent(eventSystemObject, inputSystemModuleType);
                }

                return;
            }

            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                Undo.AddComponent<StandaloneInputModule>(eventSystemObject);
            }
        }

        private static Camera EnsureMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            GameObject cameraObject = CreateGameObject("Main Camera", null);
            cameraObject.tag = "MainCamera";

            Camera camera = Undo.AddComponent<Camera>(cameraObject);
            Undo.AddComponent<AudioListener>(cameraObject);
            camera.transform.position = new Vector3(10f, 30f, 10f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return camera;
        }

        private static void EnsureDirectionalLight()
        {
            Light existingLight = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(light => light.type == LightType.Directional && light.enabled);

            if (existingLight != null)
            {
                return;
            }

            GameObject lightObject = CreateGameObject("Directional Light", null);
            Light light = Undo.AddComponent<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GameObject CreateMeshHolder(string name, Transform parent, int layer)
        {
            GameObject gameObject = CreateGameObject(name, parent);
            gameObject.layer = layer;
            Undo.AddComponent<MeshFilter>(gameObject);
            Undo.AddComponent<MeshRenderer>(gameObject);
            Undo.AddComponent<MeshCollider>(gameObject);
            return gameObject;
        }

        private static GameObject CreateGameObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");

            if (parent != null)
            {
                Undo.SetTransformParent(gameObject.transform, parent, $"Parent {name}");
                gameObject.transform.localPosition = Vector3.zero;
                gameObject.transform.localRotation = Quaternion.identity;
                gameObject.transform.localScale = Vector3.one;
            }

            return gameObject;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");

            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                Undo.SetTransformParent(rectTransform, parent, $"Parent {name}");
                rectTransform.localPosition = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.localScale = Vector3.one;
            }

            return gameObject;
        }

        private static PCGConfiguration EnsureLocalConfigAsset()
        {
            PCGConfiguration existingConfig = AssetDatabase.LoadAssetAtPath<PCGConfiguration>(ConfigAssetPath);
            if (existingConfig != null)
            {
                return existingConfig;
            }

            EnsureFolder("Assets", "MultimodalPCG");
            EnsureFolder(ConfigFolderRoot, "Generated");

            PCGConfiguration config = ScriptableObject.CreateInstance<PCGConfiguration>();
            config.name = "MultimodalPCG-DemoConfig";

            SerializedObject serializedObject = new SerializedObject(config);
            serializedObject.FindProperty("_seed").intValue = 1337;
            serializedObject.FindProperty("_width").intValue = 40;
            serializedObject.FindProperty("_height").intValue = 40;
            serializedObject.FindProperty("_initialEnemyCount").intValue = 8;
            serializedObject.FindProperty("_initialObjectCount").intValue = 5;
            serializedObject.FindProperty("_bpm").intValue = 118;

            SerializedProperty scaleProperty = serializedObject.FindProperty("_pentatonicScale");
            scaleProperty.arraySize = DefaultPentatonicScale.Length;
            for (int i = 0; i < DefaultPentatonicScale.Length; i++)
            {
                scaleProperty.GetArrayElementAtIndex(i).floatValue = DefaultPentatonicScale[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            return config;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string target = $"{parent}/{child}";
            if (AssetDatabase.IsValidFolder(target))
            {
                return;
            }

            AssetDatabase.CreateFolder(parent, child);
        }

        private static int ResolveFloorLayer()
        {
            int customFloorLayer = LayerMask.NameToLayer("PCG_Floor");
            return customFloorLayer >= 0 ? customFloorLayer : FallbackFloorLayer;
        }

        private static T LoadRequiredAssetByPathSuffix<T>(string relativePath) where T : UnityEngine.Object
        {
            string normalizedSuffix = relativePath.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(normalizedSuffix);
            string guid = AssetDatabase.FindAssets(fileName)
                .FirstOrDefault(candidateGuid =>
                {
                    string candidatePath = AssetDatabase.GUIDToAssetPath(candidateGuid).Replace('\\', '/');
                    return candidatePath.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase);
                });

            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException($"[Multimodal PCG] Could not locate required asset at '{relativePath}'.");
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                throw new InvalidOperationException($"[Multimodal PCG] Failed to load asset at '{path}'.");
            }

            return asset;
        }
    }
}
