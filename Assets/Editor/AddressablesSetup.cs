using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tạo / cập nhật group Addressables cho dữ liệu nạp theo nhu cầu:
///  - UI:    UISkin (+ sprite HUD nó tham chiếu), ảnh nền Cổng (nạp khi mở Cổng, giải phóng khi đóng)
///  - Music: mỗi bài một bundle riêng (PackSeparately) để nạp mùa nào chỉ tải bài đó
/// Address lấy từ GameAssets. Chạy lại an toàn. Build game sẽ tự build Addressables (BuildWithPlayer).
/// </summary>
public static class AddressablesSetup
{
    private const string UIGroup = "UI";
    private const string MusicGroup = "Music";

    private static readonly (string path, string key)[] MusicEntries =
    {
        ("Assets/Audio/Music/Menu_Shakkar.ogg", GameAssets.MusicMenuKey),
        ("Assets/Audio/Music/Spring_TheOldTowerInn_Loop.wav", GameAssets.MusicSpringKey),
        ("Assets/Audio/Music/Summer_KingsFeast_Loop.wav", GameAssets.MusicSummerKey),
        ("Assets/Audio/Music/Autumn_HarvestSeason.mp3", GameAssets.MusicAutumnKey),
        ("Assets/Audio/Music/Winter_LongWinter.mp3", GameAssets.MusicWinterKey),
    };

    [MenuItem("Tools/LordSimulation/Addressables/Setup Groups")]
    public static void SetupGroups()
    {
        UISkinSetup.CreateOrUpdateSkin(); // đảm bảo UISkin đã nằm ở thư mục Addressables

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

        AddressableAssetGroup ui = EnsureGroup(settings, UIGroup, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
        AddEntry(settings, ui, UISkinSetup.SkinPath, GameAssets.UISkinKey);
        AddEntry(settings, ui, "Assets/UI/Gate/Back_Gate.png", GameAssets.GateBackgroundKey);

        AddressableAssetGroup music = EnsureGroup(settings, MusicGroup, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
        foreach ((string path, string key) in MusicEntries)
        {
            ConfigureMusicImport(path);
            AddEntry(settings, music, path, key);
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        ClearOldMusicReferences();
        Debug.Log("[Addressables] Đã cấu hình group UI + Music.");
    }

    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, BundledAssetGroupSchema.BundlePackingMode packing)
    {
        AddressableAssetGroup group = settings.FindGroup(name);
        if (group == null)
            group = settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

        BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
        if (bundled != null)
        {
            bundled.BundleMode = packing;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
        }
        return group;
    }

    private static void AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning("[Addressables] Không thấy asset " + path);
            return;
        }
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.SetAddress(address, false);
    }

    /// <summary>Nhạc nền dài: nén Vorbis, nạp nền, không nạp sẵn khi mở bundle.</summary>
    private static void ConfigureMusicImport(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            return;

        AudioImporterSampleSettings sample = importer.defaultSampleSettings;
        sample.loadType = AudioClipLoadType.CompressedInMemory;
        sample.compressionFormat = AudioCompressionFormat.Vorbis;
        sample.quality = 0.6f;
        sample.preloadAudioData = false;
        importer.defaultSampleSettings = sample;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// MusicManager không còn trường AudioClip cho nhạc nền; lưu lại LoadingScene để bỏ hẳn tham chiếu cũ
    /// (nếu còn, các bài nhạc vẫn bị nạp cùng scene và mất tác dụng của Addressables).
    /// </summary>
    private static void ClearOldMusicReferences()
    {
        const string loadingScene = "Assets/Scenes/LoadingScene.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.OpenScene(loadingScene, OpenSceneMode.Single);
        foreach (MusicManager music in Object.FindObjectsByType<MusicManager>(FindObjectsSortMode.None))
            EditorUtility.SetDirty(music);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
