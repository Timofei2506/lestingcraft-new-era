using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

public sealed class MobWorkbenchWindow : EditorWindow
{
    const string StageRootName = "[LestingCraft Стенд Мобов]";
    const string OverridesAssetPath = "Assets/Resources/CustomMobs/MobOverrides.json";
    const string CustomFolder = "Assets/Resources/CustomMobs";

    int selectedSpecies = 3; // Волк по умолчанию
    bool showBlockWire = true;
    bool showMobBounds = true;
    bool showPlayerRef = true;
    bool previewWalk;
    float previewWalkSpeed = 3.2f;
    float targetHeightBlocks = 1.0f;
    Vector2 scroll;

    float overallScale = 1f;
    float yOffset;
    Vector3 headScale = Vector3.one;
    Vector3 bodyScale = Vector3.one;
    Vector3 legScale = Vector3.one;
    Vector3 tailScale = Vector3.one;
    Vector3 headOffset = Vector3.zero;
    Vector3 bodyOffset = Vector3.zero;

    GameObject externalModelAsset;
    GameObject currentMobHost;
    AnimalVisual currentVisual;
    double lastAnimTick;

    string[] speciesLabels;

    [MenuItem("LestingCraft/Стенд и редактор мобов")]
    public static void Open()
    {
        var win = GetWindow<MobWorkbenchWindow>("Редактор мобов");
        win.minSize = new Vector2(400, 560);
        win.Show();
    }

    void OnEnable()
    {
        BuildLabels();
        LoadOverrideIntoFields(selectedSpecies);
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
        lastAnimTick = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
    }

    void BuildLabels()
    {
        var names = AnimalSculpts.SpeciesNames;
        speciesLabels = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
            speciesLabels[i] = string.Format("[{0:00}] {1}", i, names[i]);
    }

    void LoadOverrideIntoFields(int kind)
    {
        AnimalVisual.ReloadOverridesCache();
        var ov = AnimalVisual.GetOverride(kind);
        if (ov != null)
        {
            overallScale = ov.overallScale <= 0f ? 1f : ov.overallScale;
            yOffset = ov.yOffset;
            headScale = ov.headScale == Vector3.zero ? Vector3.one : ov.headScale;
            bodyScale = ov.bodyScale == Vector3.zero ? Vector3.one : ov.bodyScale;
            legScale = ov.legScale == Vector3.zero ? Vector3.one : ov.legScale;
            tailScale = ov.tailScale == Vector3.zero ? Vector3.one : ov.tailScale;
            headOffset = ov.headOffset;
            bodyOffset = ov.bodyOffset;
        }
        else
        {
            ResetSlidersOnly();
        }
    }

    void ResetSlidersOnly()
    {
        overallScale = 1f;
        yOffset = 0f;
        headScale = Vector3.one;
        bodyScale = Vector3.one;
        legScale = Vector3.one;
        tailScale = Vector3.one;
        headOffset = Vector3.zero;
        bodyOffset = Vector3.zero;
    }

    MobOverrideEntry CurrentEntry()
    {
        return new MobOverrideEntry
        {
            species = selectedSpecies,
            overallScale = overallScale,
            yOffset = yOffset,
            headScale = headScale,
            bodyScale = bodyScale,
            legScale = legScale,
            tailScale = tailScale,
            headOffset = headOffset,
            bodyOffset = bodyOffset
        };
    }

    void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - lastAnimTick), 0f, 0.05f);
        lastAnimTick = now;
        if (!previewWalk || !currentVisual || !currentMobHost) return;

        // Simulate horizontal travel so AnimalVisual.Animate drives full gait & breathing in Scene View.
        Vector3 saved = currentMobHost.transform.position;
        currentMobHost.transform.position = saved + Vector3.forward * (previewWalkSpeed * dt);
        currentVisual.Animate(dt, 0f);
        currentMobHost.transform.position = saved;
        SceneView.RepaintAll();
    }

    void OnGUI()
    {
        if (speciesLabels == null || speciesLabels.Length == 0) BuildLabels();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Масштаб игры LestingCraft:\n" +
            "• 1 блок игры = ровно 1.0 × 1.0 × 1.0 метр Unity (Cube со Scale 1, 1, 1)\n" +
            "• Высота игрока = 2.00 блока (ширина 0.60 блока)",
            MessageType.Info);

        // --- SECTION 1: SELECT & SPAWN ---
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("1. Выбор моба и стенд сравнения с блоком", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int nextSpecies = EditorGUILayout.Popup("Моб (34 вида)", selectedSpecies, speciesLabels);
        if (EditorGUI.EndChangeCheck())
        {
            selectedSpecies = nextSpecies;
            LoadOverrideIntoFields(selectedSpecies);
            if (GameObject.Find(StageRootName)) SpawnSingleStage(false);
        }

        string customPrefabPath = string.Format("{0}/mob_{1:00}.prefab", CustomFolder, selectedSpecies);
        bool hasCustomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(customPrefabPath) != null;
        if (hasCustomPrefab)
            EditorGUILayout.HelpBox("Активен кастомный Prefab: " + customPrefabPath, MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🧱 Поставить моба + блок 1×1×1", GUILayout.Height(30)))
            SpawnSingleStage(true);
        if (GUILayout.Button("🐾 Все 34 моба в ряд", GUILayout.Height(30)))
            SpawnLineupStage();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        showBlockWire = GUILayout.Toggle(showBlockWire, "Контур 1×1×1 на мобе");
        showMobBounds = GUILayout.Toggle(showMobBounds, "Габариты моба");
        showPlayerRef = GUILayout.Toggle(showPlayerRef, "Игрок (2.0 бл.)");
        if (GUILayout.Button("Убрать стенд", GUILayout.Width(95)))
            ClearStage();
        EditorGUILayout.EndHorizontal();

        // --- LIVE BOUNDS READOUT ---
        Bounds b;
        if (TryGetMobBounds(out b))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(string.Format(
                CultureInfo.InvariantCulture,
                "📏 Размер в блоках:  Высота {0:F2}  |  Ширина {1:F2}  |  Длина {2:F2}  (низ Y = {3:F2})",
                b.size.y, b.size.x, b.size.z, b.min.y), EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            targetHeightBlocks = EditorGUILayout.FloatField("Нужная высота (бл.):", Mathf.Max(0.1f, targetHeightBlocks));
            if (GUILayout.Button("Подогнать высоту"))
            {
                if (b.size.y > 0.01f)
                {
                    overallScale *= targetHeightBlocks / b.size.y;
                    RebuildPreviewMob();
                    SnapFeetToGround();
                }
            }
            if (GUILayout.Button("Лапы на пол (Y=0)"))
                SnapFeetToGround();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // --- SECTION 2: SLIDERS & ANIMATION ---
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("2. Быстрая настройка пропорций и анимации", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        overallScale = EditorGUILayout.Slider("Общий масштаб", overallScale, 0.2f, 4.0f);
        yOffset = EditorGUILayout.Slider("Смещение по Y (бл.)", yOffset, -1.5f, 1.5f);
        headScale = EditorGUILayout.Vector3Field("Масштаб головы (head)", headScale);
        headOffset = EditorGUILayout.Vector3Field("Сдвиг головы (head)", headOffset);
        bodyScale = EditorGUILayout.Vector3Field("Масштаб тела (body)", bodyScale);
        bodyOffset = EditorGUILayout.Vector3Field("Сдвиг тела (body)", bodyOffset);
        legScale = EditorGUILayout.Vector3Field("Масштаб ног (leg0..N)", legScale);
        tailScale = EditorGUILayout.Vector3Field("Масштаб хвоста (tail)", tailScale);
        if (EditorGUI.EndChangeCheck())
            RebuildPreviewMob();

        EditorGUILayout.BeginHorizontal();
        bool nextWalk = GUILayout.Toggle(previewWalk, "▶ Тест анимации бега в сцене", "Button", GUILayout.Height(24));
        if (nextWalk != previewWalk)
        {
            previewWalk = nextWalk;
            if (!previewWalk && currentVisual) currentVisual.ResetMotion();
        }
        previewWalkSpeed = EditorGUILayout.Slider(previewWalkSpeed, 0.2f, 6.0f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 Сохранить пропорции в игру", GUILayout.Height(28)))
            SaveOverrideToJson();
        if (GUILayout.Button("Сбросить ползунки", GUILayout.Height(28), GUILayout.Width(135)))
        {
            ResetSlidersOnly();
            RemoveOverrideFromJson(selectedSpecies);
            RebuildPreviewMob();
        }
        EditorGUILayout.EndHorizontal();

        // --- SECTION 3: 3D MODEL / BLENDER / BLOCKBENCH / PREFAB ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("3. Своя 3D-модель (Unity / Blockbench / Blender)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Хочешь переделать форму моба вручную?\n" +
            "• Нажми «Экспорт в .OBJ» — откроешь моба в Blender/Blockbench уже в масштабе 1 ед. = 1 блок.\n" +
            "• Или измени части моба прямо в Hierarchy на стенде / перетащи свой .fbx/.glb ниже и нажми «Сохранить как кастомный Prefab».\n" +
            "• Если внутри модели назвать части head, body, leg0..leg3, tail, wingL, wingR — игра сама их анимирует!",
            MessageType.None);

        if (GUILayout.Button("📤 Экспортировать текущего моба в .OBJ (+ куб 1×1×1)", GUILayout.Height(26)))
            ExportCurrentMobToObj();

        EditorGUILayout.Space(3);
        externalModelAsset = (GameObject)EditorGUILayout.ObjectField(
            "Своя модель (.fbx/.glb/.prefab)", externalModelAsset, typeof(GameObject), false);

        EditorGUILayout.BeginHorizontal();
        if (externalModelAsset != null)
        {
            if (GUILayout.Button("🧩 Заменить моба этой моделью", GUILayout.Height(28)))
                SaveExternalModelAsCustomMob(externalModelAsset);
        }
        else
        {
            if (GUILayout.Button("🧩 Сохранить моба со стенда как Prefab", GUILayout.Height(28)))
                SaveStageMobAsCustomPrefab();
        }

        if (hasCustomPrefab && GUILayout.Button("↩ Удалить кастомный Prefab", GUILayout.Height(28), GUILayout.Width(175)))
        {
            AssetDatabase.DeleteAsset(customPrefabPath);
            AssetDatabase.DeleteAsset(string.Format("{0}/mob_{1:00}_meshes.asset", CustomFolder, selectedSpecies));
            AssetDatabase.Refresh();
            SpawnSingleStage(false);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("📝 Открыть код скульптора (AnimalSculpts.cs)"))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Gameplay/AnimalSculpts.cs");
            if (script) AssetDatabase.OpenAsset(script, 1);
        }

        EditorGUILayout.EndScrollView();
        SceneView.RepaintAll();
    }

    GameObject EnsureStageRoot()
    {
        ClearStage();
        PresentationAssets.Ensure();
        var root = new GameObject(StageRootName);
        root.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        BuildReferenceEnvironment(root.transform, 5);
        return root;
    }

    void BuildReferenceEnvironment(Transform root, int halfWidth)
    {
        int grassLayer = Mathf.Max(0, Array.IndexOf(SurfaceLayers.Names, "grass-top"));
        int stoneLayer = Mathf.Max(0, Array.IndexOf(SurfaceLayers.Names, "stone"));
        int planksLayer = Mathf.Max(0, Array.IndexOf(SurfaceLayers.Names, "planks"));

        // Floor of 1x1x1 blocks so top surface sits strictly at Y = 0.0
        var floorPart = new AnimalVisual.Part();
        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                int layer = ((x + z) & 1) == 0 ? grassLayer : stoneLayer;
                Color tint = layer == grassLayer ? new Color(0.55f, 0.78f, 0.42f) : Color.white;
                floorPart.Box(new Vector3(x, -0.5f, z), Vector3.one, tint, layer);
            }
        }
        var floorGo = floorPart.Build("Пол_из_блоков_1x1x1 (Y=0)", root, Vector3.zero);
        floorGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        // Reference 1x1x1 block right next to the mob at X = -1.5, Y = 0..1, Z = 0
        var refBlock = new AnimalVisual.Part();
        refBlock.Box(new Vector3(0f, 0.5f, 0f), Vector3.one, Color.white, planksLayer);
        var blockGo = refBlock.Build("Эталонный_Блок_Игры_1x1x1", root, new Vector3(-1.5f, 0f, 0f));
        blockGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        // Ensure there is directional light if an empty untitled scene was opened
        if (FindFirstObjectByType<Light>() == null)
        {
            var lightGo = new GameObject("Свет_Стенда", typeof(Light));
            lightGo.transform.SetParent(root, false);
            lightGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.25f;
            lightGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }
    }

    void SpawnSingleStage(bool frameCamera)
    {
        var root = EnsureStageRoot();
        currentMobHost = new GameObject(string.Format("Моб_{0:00}_{1}", selectedSpecies, AnimalSculpts.SpeciesNames[selectedSpecies]));
        currentMobHost.transform.SetParent(root.transform, false);
        currentMobHost.transform.localPosition = Vector3.zero;
        currentMobHost.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        currentVisual = currentMobHost.AddComponent<AnimalVisual>();
        currentVisual.Build(selectedSpecies, true);
        currentVisual.ApplyOverride(CurrentEntry());

        Selection.activeGameObject = currentVisual.RigRoot ? currentVisual.RigRoot.gameObject : currentMobHost;
        if (frameCamera && SceneView.lastActiveSceneView != null)
        {
            Bounds b;
            if (!TryGetMobBounds(out b)) b = new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(3f, 2f, 3f));
            b.Encapsulate(new Bounds(new Vector3(-1.5f, 0.5f, 0f), Vector3.one));
            SceneView.lastActiveSceneView.LookAt(b.center, Quaternion.Euler(20f, -145f, 0f), Mathf.Max(2.6f, b.extents.magnitude * 2.1f));
        }
        SceneView.RepaintAll();
    }

    void SpawnLineupStage()
    {
        previewWalk = false;
        ClearStage();
        PresentationAssets.Ensure();
        var root = new GameObject(StageRootName);
        root.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        int count = AnimalSculpts.SpeciesNames.Length;
        float spacing = 2.4f;
        int planksLayer = Mathf.Max(0, Array.IndexOf(SurfaceLayers.Names, "planks"));
        int stoneLayer = Mathf.Max(0, Array.IndexOf(SurfaceLayers.Names, "stone"));

        var floorPart = new AnimalVisual.Part();
        for (int i = 0; i < count; i++)
        {
            float x = i * spacing;
            floorPart.Box(new Vector3(x, -0.5f, 0f), new Vector3(2.2f, 1f, 3f), Color.white, stoneLayer);
            // Reference 1x1x1 block behind each mob!
            floorPart.Box(new Vector3(x, 0.5f, -1.25f), Vector3.one, Color.white, planksLayer);
        }
        var floorGo = floorPart.Build("Подиум_и_Блоки_1x1x1", root.transform, Vector3.zero);
        floorGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        for (int i = 0; i < count; i++)
        {
            var host = new GameObject(string.Format("[{0:00}] {1}", i, AnimalSculpts.SpeciesNames[i]));
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = new Vector3(i * spacing, 0f, 0.2f);
            host.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            var vis = host.AddComponent<AnimalVisual>();
            vis.Build(i, true);
        }

        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(7f, 1.0f, 0f), Quaternion.Euler(22f, -155f, 0f), 12f);
        SceneView.RepaintAll();
    }

    void RebuildPreviewMob()
    {
        if (!GameObject.Find(StageRootName)) return;
        SpawnSingleStage(false);
    }

    void ClearStage()
    {
        currentMobHost = null;
        currentVisual = null;
        var existing = GameObject.Find(StageRootName);
        while (existing != null)
        {
            DestroyImmediate(existing);
            existing = GameObject.Find(StageRootName);
        }
        SceneView.RepaintAll();
    }

    bool TryGetMobBounds(out Bounds bounds)
    {
        bounds = default;
        if (!currentMobHost) return false;
        var renderers = currentMobHost.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    void SnapFeetToGround()
    {
        Bounds b;
        if (!TryGetMobBounds(out b)) return;
        yOffset -= b.min.y;
        RebuildPreviewMob();
    }

    void SaveOverrideToJson()
    {
        EnsureCustomMobsFolder();
        var catalog = LoadCatalogFromDisk();
        catalog.entries.RemoveAll(e => e.species == selectedSpecies);
        catalog.entries.Add(CurrentEntry());
        catalog.entries.Sort((a, b) => a.species.CompareTo(b.species));
        File.WriteAllText(OverridesAssetPath, JsonUtility.ToJson(catalog, true), Encoding.UTF8);
        AssetDatabase.ImportAsset(OverridesAssetPath);
        AnimalVisual.ReloadOverridesCache();
        ShowNotification(new GUIContent("Сохранено для " + AnimalSculpts.SpeciesNames[selectedSpecies]));
    }

    void RemoveOverrideFromJson(int kind)
    {
        if (!File.Exists(OverridesAssetPath)) return;
        var catalog = LoadCatalogFromDisk();
        catalog.entries.RemoveAll(e => e.species == kind);
        File.WriteAllText(OverridesAssetPath, JsonUtility.ToJson(catalog, true), Encoding.UTF8);
        AssetDatabase.ImportAsset(OverridesAssetPath);
        AnimalVisual.ReloadOverridesCache();
    }

    static MobOverrideCatalog LoadCatalogFromDisk()
    {
        if (!File.Exists(OverridesAssetPath)) return new MobOverrideCatalog();
        var txt = File.ReadAllText(OverridesAssetPath, Encoding.UTF8);
        return JsonUtility.FromJson<MobOverrideCatalog>(txt) ?? new MobOverrideCatalog();
    }

    static void EnsureCustomMobsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(CustomFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "CustomMobs");
    }

    void SaveExternalModelAsCustomMob(GameObject modelAsset)
    {
        EnsureCustomMobsFolder();
        string prefabPath = string.Format("{0}/mob_{1:00}.prefab", CustomFolder, selectedSpecies);
        var temp = Instantiate(modelAsset);
        temp.name = string.Format("mob_{0:00}", selectedSpecies);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        }
        finally
        {
            DestroyImmediate(temp);
        }
        AssetDatabase.Refresh();
        SpawnSingleStage(true);
        ShowNotification(new GUIContent("Кастомная модель сохранена: " + prefabPath));
    }

    void SaveStageMobAsCustomPrefab()
    {
        if (!currentVisual || !currentVisual.RigRoot)
        {
            SpawnSingleStage(true);
            if (!currentVisual || !currentVisual.RigRoot) return;
        }
        EnsureCustomMobsFolder();
        PresentationAssets.Ensure();

        string meshContainerPath = string.Format("{0}/mob_{1:00}_meshes.asset", CustomFolder, selectedSpecies);
        string prefabPath = string.Format("{0}/mob_{1:00}.prefab", CustomFolder, selectedSpecies);
        var sharedVoxelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Generated/VoxelObjects.mat");

        var copy = Instantiate(currentVisual.RigRoot.gameObject);
        copy.name = string.Format("mob_{0:00}", selectedSpecies);
        foreach (var tr in copy.GetComponentsInChildren<Transform>(true))
            tr.gameObject.hideFlags = HideFlags.None;
        var sculptComp = copy.GetComponent<BossSculpture>();
        if (sculptComp) DestroyImmediate(sculptComp);
        foreach (var life in copy.GetComponentsInChildren<AnimalMeshLifetime>(true))
            DestroyImmediate(life);

        try
        {
            AssetDatabase.DeleteAsset(meshContainerPath);
            bool createdContainer = false;
            int idx = 0;
            foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                if (!AssetDatabase.Contains(mf.sharedMesh))
                {
                    Mesh persisted = Instantiate(mf.sharedMesh);
                    persisted.name = string.Format("mob_{0:00}_part_{1}_{2}", selectedSpecies, idx++, mf.gameObject.name);
                    if (!createdContainer)
                    {
                        AssetDatabase.CreateAsset(persisted, meshContainerPath);
                        createdContainer = true;
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(persisted, meshContainerPath);
                    }
                    mf.sharedMesh = persisted;
                }
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr && sharedVoxelMat && (!mr.sharedMaterial || !AssetDatabase.Contains(mr.sharedMaterial)))
                    mr.sharedMaterial = sharedVoxelMat;
            }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
        }
        finally
        {
            DestroyImmediate(copy);
        }

        // Reset override scale to 1 since baked into prefab transform
        ResetSlidersOnly();
        RemoveOverrideFromJson(selectedSpecies);
        AssetDatabase.Refresh();
        SpawnSingleStage(false);
        ShowNotification(new GUIContent("Prefab сохранён: " + prefabPath));
    }

    void ExportCurrentMobToObj()
    {
        if (!currentMobHost) SpawnSingleStage(false);
        if (!currentMobHost) return;

        string defaultName = string.Format("mob_{0:00}.obj", selectedSpecies);
        string path = EditorUtility.SaveFilePanel("Экспорт моба в OBJ (1 ед. = 1 блок игры)", "", defaultName, "obj");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder(65536);
        sb.AppendLine("# LestingCraft Mob Export (1.0 unit = 1x1x1 game block)");
        sb.AppendLine("# Species: " + selectedSpecies + " (" + AnimalSculpts.SpeciesNames[selectedSpecies] + ")");
        int vertexOffset = 1;
        Matrix4x4 rootInv = currentMobHost.transform.worldToLocalMatrix;

        foreach (var mf in currentMobHost.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;
            sb.AppendLine("o " + mf.gameObject.name);
            Matrix4x4 mat = rootInv * mf.transform.localToWorldMatrix;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            var uvs = mesh.uv;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = mat.MultiplyPoint3x4(verts[i]);
                sb.AppendFormat(CultureInfo.InvariantCulture, "v {0:F5} {1:F5} {2:F5}\n", -v.x, v.y, v.z);
            }
            for (int i = 0; i < uvs.Length; i++)
                sb.AppendFormat(CultureInfo.InvariantCulture, "vt {0:F5} {1:F5}\n", uvs[i].x, uvs[i].y);
            for (int i = 0; i < norms.Length; i++)
            {
                Vector3 n = mat.MultiplyVector(norms[i]).normalized;
                sb.AppendFormat(CultureInfo.InvariantCulture, "vn {0:F5} {1:F5} {2:F5}\n", -n.x, n.y, n.z);
            }
            var tris = mesh.triangles;
            bool hasUv = uvs.Length == verts.Length;
            bool hasN = norms.Length == verts.Length;
            for (int i = 0; i < tris.Length; i += 3)
            {
                // Flip winding because X was mirrored for right-handed OBJ tools (Blender/Blockbench)
                int a = tris[i] + vertexOffset;
                int b = tris[i + 2] + vertexOffset;
                int c = tris[i + 1] + vertexOffset;
                if (hasUv && hasN)
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", a, b, c);
                else
                    sb.AppendFormat("f {0} {1} {2}\n", a, b, c);
            }
            vertexOffset += verts.Length;
        }

        // Also append a 1x1x1 reference block at X = 1.5 in OBJ so Blender/Blockbench shows exact block size!
        sb.AppendLine("o Reference_Game_Block_1x1x1");
        Vector3[] cubeV =
        {
            new Vector3(1f, 0f, -0.5f), new Vector3(2f, 0f, -0.5f), new Vector3(2f, 1f, -0.5f), new Vector3(1f, 1f, -0.5f),
            new Vector3(1f, 0f,  0.5f), new Vector3(2f, 0f,  0.5f), new Vector3(2f, 1f,  0.5f), new Vector3(1f, 1f,  0.5f)
        };
        for (int i = 0; i < cubeV.Length; i++)
            sb.AppendFormat(CultureInfo.InvariantCulture, "v {0:F5} {1:F5} {2:F5}\n", cubeV[i].x, cubeV[i].y, cubeV[i].z);
        int o = vertexOffset;
        int[,] quads = { {0,1,2,3}, {5,4,7,6}, {4,0,3,7}, {1,5,6,2}, {3,2,6,7}, {4,5,1,0} };
        for (int q = 0; q < 6; q++)
        {
            sb.AppendFormat("f {0} {1} {2} {3}\n", o + quads[q,0], o + quads[q,1], o + quads[q,2], o + quads[q,3]);
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        EditorUtility.RevealInFinder(path);
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!GameObject.Find(StageRootName)) return;

        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(1f, 0.92f, 0.35f) },
            fontSize = 12
        };

        // 1x1x1 outline around the reference block at (-1.5, 0.5, 0)
        Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        Handles.DrawWireCube(new Vector3(-1.5f, 0.5f, 0f), Vector3.one);
        Handles.Label(new Vector3(-1.5f, 1.18f, 0f), "1 БЛОК ИГРЫ\n(1.0 × 1.0 × 1.0 м)", labelStyle);

        // Vertical block height ruler at X = -2.25
        Handles.color = new Color(0.35f, 0.85f, 1f, 0.85f);
        Vector3 rBase = new Vector3(-2.25f, 0f, 0f);
        Handles.DrawLine(rBase, rBase + Vector3.up * 3f);
        for (int h = 0; h <= 3; h++)
        {
            Vector3 p = rBase + Vector3.up * h;
            Handles.DrawLine(p + Vector3.left * 0.12f, p + Vector3.right * 0.12f);
            Handles.Label(p + Vector3.left * 0.55f, h + " бл.");
        }

        // Optional 1x1x1 wireframe right around origin (0, 0.5, 0) where the mob stands
        if (showBlockWire && currentMobHost)
        {
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            Handles.DrawWireCube(new Vector3(0f, 0.5f, 0f), Vector3.one);
        }

        // Optional player height silhouette (2.00 blocks tall, 0.60 wide) at X = +1.5
        if (showPlayerRef && currentMobHost)
        {
            Handles.color = new Color(0.3f, 1f, 0.55f, 0.75f);
            Handles.DrawWireCube(new Vector3(1.5f, 1.0f, 0f), new Vector3(0.6f, 2.0f, 0.6f));
            Handles.Label(new Vector3(1.5f, 2.15f, 0f), "Игрок (2.00 бл.)", labelStyle);
        }

        // Live mob bounding box in blocks
        Bounds b;
        if (showMobBounds && TryGetMobBounds(out b))
        {
            Handles.color = new Color(1f, 0.45f, 0.25f, 0.9f);
            Handles.DrawWireCube(b.center, b.size);
            Handles.Label(
                new Vector3(b.center.x, b.max.y + 0.18f, b.center.z),
                string.Format(CultureInfo.InvariantCulture, "{0}\nВ: {1:F2} × Ш: {2:F2} × Д: {3:F2} бл.",
                    AnimalSculpts.SpeciesNames[selectedSpecies], b.size.y, b.size.x, b.size.z),
                labelStyle);
        }
    }
}
