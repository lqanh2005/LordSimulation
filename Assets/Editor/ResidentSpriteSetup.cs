using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Setup sprite cư dân (Kid / Young / Old) + animation Idle/Walk.
/// Chạy lại được bất cứ lúc nào: Tools > Resident > Setup Sprites & Animations.
/// </summary>
public static class ResidentSpriteSetup
{
    private const string TextureRoot = "Assets/Texture/Resident";
    private const string AnimRoot = "Assets/Animations/Resident";
    private const string PrefabPath = "Assets/Sprites/Resident_Normal.prefab";

    // Tile 1x0.5 unit -> người lớn (~176px) cao ~0.55 unit, to hơn cửa nhà một chút cho dễ nhìn
    private const float PixelsPerUnit = 320f;
    private const float IdleFps = 8f;
    private const float WalkFps = 12f;

    // Thư mục thực tế đặt tên không đồng nhất (Idle/IDLE/idle, walk/WALK/Walk) nên dò theo tên không phân biệt hoa thường
    private struct AgeSet
    {
        public string folder;
        public string clipPrefix;
        public float pivotY; // chân nhân vật tính từ đáy canvas 320px
    }

    private static readonly AgeSet[] Ages =
    {
        new AgeSet { folder = "Kid", clipPrefix = "Kid", pivotY = 89f / 320f },
        new AgeSet { folder = "Young", clipPrefix = "Young", pivotY = 72f / 320f },
        new AgeSet { folder = "Old", clipPrefix = "Old", pivotY = 90f / 320f },
    };

    [MenuItem("Tools/Resident/Setup Sprites & Animations")]
    public static void Run()
    {
        EnsureFolder(AnimRoot);

        AnimationClip baseIdle = null, baseWalk = null;
        var overrides = new Dictionary<string, (AnimationClip idle, AnimationClip walk)>();

        foreach (AgeSet age in Ages)
        {
            string ageDir = $"{TextureRoot}/{age.folder}";
            string idleDir = FindSubFolder(ageDir, "idle");
            string walkDir = FindSubFolder(ageDir, "walk");
            if (idleDir == null || walkDir == null)
            {
                Debug.LogError($"[ResidentSpriteSetup] Thiếu thư mục idle/walk trong {ageDir}");
                continue;
            }

            Sprite[] idleFrames = ImportFrames(idleDir, age.pivotY);
            Sprite[] walkFrames = ImportFrames(walkDir, age.pivotY);

            AnimationClip idle = BuildClip($"{AnimRoot}/{age.clipPrefix}_Idle.anim", idleFrames, IdleFps);
            AnimationClip walk = BuildClip($"{AnimRoot}/{age.clipPrefix}_Walk.anim", walkFrames, WalkFps);
            overrides[age.clipPrefix] = (idle, walk);

            if (age.clipPrefix == "Young")
            {
                baseIdle = idle;
                baseWalk = walk;
            }
        }

        if (baseIdle == null || baseWalk == null)
        {
            Debug.LogError("[ResidentSpriteSetup] Không tạo được clip Young — dừng.");
            return;
        }

        AnimatorController baseController = BuildBaseController(baseIdle, baseWalk);
        var kid = BuildOverride("Kid", baseController, baseIdle, baseWalk, overrides["Kid"]);
        var young = BuildOverride("Young", baseController, baseIdle, baseWalk, overrides["Young"]);
        var old = BuildOverride("Old", baseController, baseIdle, baseWalk, overrides["Old"]);

        SetupPrefab(kid, young, old);
        SetupStatusIcons();

        AssetDatabase.SaveAssets();
        Debug.Log("[ResidentSpriteSetup] Hoàn tất setup sprite + animation cư dân.");
    }

    // ===== Icon trạng thái trên đầu (Cold / Flu / Homeless / Sick) =====
    // Art 1920x1080 có nhiều khoảng trống -> cắt sát icon, pivot ở mũi nhọn bong bóng.
    // Icon rộng ~600px -> ~0.3 unit trên map.
    private const float StatusIconPixelsPerUnit = 2000f;

    private static readonly (string file, string field)[] StatusIcons =
    {
        ("Sick", "sickIcon"),
        ("Flu", "fluIcon"),
        ("Cold", "coldIcon"),
        ("Homeless", "homelessIcon"),
    };

    [MenuItem("Tools/Resident/Setup Status Icons")]
    public static void SetupStatusIcons()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ResidentBase view = root.GetComponent<ResidentBase>();
            var so = new SerializedObject(view);
            foreach (var (file, field) in StatusIcons)
            {
                Sprite sprite = ImportStatusIcon($"{TextureRoot}/{file}.png");
                if (sprite == null)
                    Debug.LogError($"[ResidentSpriteSetup] Không import được icon {file}.png");
                so.FindProperty(field).objectReferenceValue = sprite;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bỏ overlay chữ tên cũ nếu đã lưu trong prefab
            Transform oldOverlay = root.transform.Find("Overlay");
            if (oldOverlay != null)
                Object.DestroyImmediate(oldOverlay.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ResidentSpriteSetup] Đã gán icon trạng thái cho cư dân.");
    }

    private static Sprite ImportStatusIcon(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return null;

        Rect rect = FindIconRect(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = StatusIconPixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // Giữ spriteID cũ để không mất reference khi chạy lại
        var existing = provider.GetSpriteRects();
        var spriteRect = existing.Length > 0 ? existing[0] : new SpriteRect { spriteID = GUID.Generate() };
        spriteRect.name = Path.GetFileNameWithoutExtension(path);
        spriteRect.rect = rect;
        spriteRect.alignment = SpriteAlignment.Custom;
        spriteRect.pivot = new Vector2(0.5f, 0f);
        provider.SetSpriteRects(new[] { spriteRect });

        var nameIdProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        nameIdProvider?.SetNameFileIdPairs(new[]
        {
            new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID)
        });

        provider.Apply();
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    /// <summary>
    /// Tìm khung icon trong canvas lớn, bỏ qua chấm lẻ (vd. vết bẩn trong Flu.png).
    /// Cột/hàng "đặc" (>= 40px đục) cho biên trái/phải/trên; đáy lấy tới mũi nhọn mảnh của bong bóng.
    /// </summary>
    private static Rect FindIconRect(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            Color32[] px = tex.GetPixels32();

            var colCount = new int[w];
            var rowCount = new int[h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a > 128)
                {
                    colCount[x]++;
                    rowCount[y]++;
                }
            }

            const int Solid = 40;
            int minX = System.Array.FindIndex(colCount, c => c >= Solid);
            int maxX = System.Array.FindLastIndex(colCount, c => c >= Solid);
            int maxY = System.Array.FindLastIndex(rowCount, c => c >= Solid); // đỉnh (Texture2D: y=0 ở đáy)
            if (minX < 0 || maxY < 0)
                return new Rect(0, 0, w, h);

            // Đáy: hàng thấp nhất còn pixel đục trong khoảng cột icon (mũi nhọn)
            int minY = maxY;
            for (int y = 0; y < maxY; y++)
            {
                bool any = false;
                for (int x = minX; x <= maxX && !any; x++)
                    any = px[y * w + x].a > 10;
                if (any)
                {
                    minY = y;
                    break;
                }
            }

            const int Pad = 4;
            minX = Mathf.Max(0, minX - Pad);
            minY = Mathf.Max(0, minY - Pad);
            maxX = Mathf.Min(w - 1, maxX + Pad);
            maxY = Mathf.Min(h - 1, maxY + Pad);
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }

    private static string FindSubFolder(string parent, string nameLower)
    {
        return AssetDatabase.GetSubFolders(parent)
            .FirstOrDefault(p => Path.GetFileName(p).ToLowerInvariant() == nameLower);
    }

    private static Sprite[] ImportFrames(string folder, float pivotY)
    {
        string[] paths = Directory.GetFiles(folder, "*.png")
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(FrameNumber)
            .ToArray();

        var sprites = new List<Sprite>();
        foreach (string path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    // "Asset 10.png" phải đứng sau "Asset 9.png"
    private static int FrameNumber(string path)
    {
        string digits = new string(Path.GetFileNameWithoutExtension(path).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int n) ? n : 0;
    }

    private static AnimationClip BuildClip(string path, Sprite[] frames, float fps)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        // Giữ frame cuối đủ 1 nhịp trước khi loop
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController BuildBaseController(AnimationClip idle, AnimationClip walk)
    {
        string path = $"{AnimRoot}/Resident.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter(ResidentBase.IsWalkingParam, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle");
        idleState.motion = idle;
        AnimatorState walkState = sm.AddState("Walk");
        walkState.motion = walk;
        sm.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, ResidentBase.IsWalkingParam);

        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, ResidentBase.IsWalkingParam);

        return controller;
    }

    private static AnimatorOverrideController BuildOverride(
        string age,
        AnimatorController baseController,
        AnimationClip baseIdle,
        AnimationClip baseWalk,
        (AnimationClip idle, AnimationClip walk) clips)
    {
        string path = $"{AnimRoot}/Resident_{age}.overrideController";
        var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (aoc == null)
        {
            aoc = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(aoc, path);
        }

        aoc.runtimeAnimatorController = baseController;
        aoc[baseIdle] = clips.idle;
        aoc[baseWalk] = clips.walk;
        EditorUtility.SetDirty(aoc);
        return aoc;
    }

    private static void SetupPrefab(
        RuntimeAnimatorController kid,
        RuntimeAnimatorController young,
        RuntimeAnimatorController old)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            SpriteRenderer sr = root.GetComponentInChildren<SpriteRenderer>();
            AnimationClip youngIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimRoot}/Young_Idle.anim");
            Sprite firstFrame = AnimationUtility
                .GetObjectReferenceCurve(youngIdle, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"))[0]
                .value as Sprite;
            sr.sprite = firstFrame;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            Animator animator = sr.GetComponent<Animator>();
            if (animator == null)
                animator = sr.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = young;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;

            ResidentBase view = root.GetComponent<ResidentBase>();
            var so = new SerializedObject(view);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("childController").objectReferenceValue = kid;
            so.FindProperty("adultController").objectReferenceValue = young;
            so.FindProperty("elderlyController").objectReferenceValue = old;
            // Kích thước từng nhóm tuổi đã có sẵn trong art -> không scale thêm
            so.FindProperty("childScale").floatValue = 1f;
            so.FindProperty("adultScale").floatValue = 1f;
            so.FindProperty("elderlyScale").floatValue = 1f;
            so.FindProperty("overlayOffset").vector3Value = new Vector3(0f, 0.68f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
