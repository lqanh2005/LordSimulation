using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Khởi tạo dữ liệu ở màn Loading (chạy một lần khi mở game), báo tiến trình cho thanh loading.
/// Mỗi bước có trọng số; Progress = phần đã xong (0..1), Status = chữ hiển thị dưới logo.
/// </summary>
public class GameDataPreloader
{
    // Bảng chữ cần sẵn trong font (sinh glyph SDF trước để lúc chơi không bị giật khi gặp chữ mới)
    private const string VietnameseChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?%/+-–—()[]\"'·•×°<>=" +
        "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴĐÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸ" +
        "àáạảãâầấậẩẫăằắặẳẵđèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹ";

    private const string MapResourcePath = "Map/MapData";

    // Giữ tham chiếu để asset đã nạp không bị giải phóng trước khi vào gameplay
    private static readonly List<UnityEngine.Object> Retained = new List<UnityEngine.Object>();

    private struct Step
    {
        public string status;
        public float weight;
        public Func<IEnumerator> run;
    }

    private readonly List<Step> _steps = new List<Step>();
    private float _totalWeight;
    private float _doneWeight;
    private float _stepFraction;
    private float _currentWeight;

    public float Progress => _totalWeight > 0f ? Mathf.Clamp01((_doneWeight + _currentWeight * _stepFraction) / _totalWeight) : 1f;
    public string Status { get; private set; } = "Đang khởi động...";
    public bool IsDone { get; private set; }

    public GameDataPreloader(GameController owner)
    {
        Add("Đang đọc cài đặt...", 1f, ApplySettings);
        Add("Đang kiểm tra bản lưu...", 1f, () => ScanSaves(owner.saveLoadManager));
        Add("Đang nạp giao diện...", 2f, LoadSkin);
        Add("Đang chuẩn bị phông chữ...", 4f, WarmFonts);
        Add("Đang nạp bản đồ...", 3f, LoadMap);
        Add("Đang nạp dữ liệu thành trì...", 1f, WarmCatalogs);
        Add("Đang nạp âm thanh...", 2f, () => LoadAudio(owner.musicManager));
    }

    private void Add(string status, float weight, Func<IEnumerator> run)
    {
        _steps.Add(new Step { status = status, weight = weight, run = run });
        _totalWeight += weight;
    }

    public IEnumerator Run()
    {
        foreach (Step step in _steps)
        {
            Status = step.status;
            _currentWeight = step.weight;
            _stepFraction = 0f;
            yield return null; // cho UI kịp vẽ dòng trạng thái mới

            IEnumerator routine = null;
            try
            {
                routine = step.run();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Preloader] Lỗi ở bước \"{step.status}\": {ex.Message}");
            }

            while (routine != null)
            {
                bool hasNext;
                try
                {
                    hasNext = routine.MoveNext();
                }
                catch (Exception ex)
                {
                    // Một bước lỗi không được chặn cả game: ghi log rồi đi tiếp
                    Debug.LogError($"[Preloader] Lỗi ở bước \"{step.status}\": {ex.Message}");
                    break;
                }
                if (!hasNext)
                    break;
                yield return routine.Current;
            }

            _doneWeight += step.weight;
            _stepFraction = 0f;
        }

        Status = "Hoàn tất";
        IsDone = true;
    }

    // ==========================================
    // CÁC BƯỚC
    // ==========================================

    private static IEnumerator ApplySettings()
    {
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(SettingsPopup.PrefVSync, 0);
        Application.targetFrameRate = PlayerPrefs.GetInt(SettingsPopup.PrefFpsLimit, 60);
        AudioListener.volume = MusicManager.MasterVolume;
        yield break;
    }

    private IEnumerator ScanSaves(SaveLoadManager saves)
    {
        if (saves == null)
            yield break;

        saves.Init();
        for (int i = 0; i < SaveLoadManager.SlotCount; i++)
        {
            saves.GetSlotInfo(i);
            _stepFraction = (i + 1f) / SaveLoadManager.SlotCount;
        }
        yield return null;
    }

    private IEnumerator LoadSkin()
    {
        // Addressables: skin + sprite HUD nằm trong group UI; giữ handle suốt game (GameAssets)
        AsyncOperationHandle<UISkin> handle = GameAssets.LoadAsync<UISkin>(GameAssets.UISkinKey);
        while (!handle.IsDone)
        {
            _stepFraction = handle.PercentComplete;
            yield return null;
        }
        if (handle.Status != AsyncOperationStatus.Succeeded)
            Debug.LogWarning("[Preloader] Không nạp được UISkin qua Addressables — dùng dự phòng.");
    }

    private IEnumerator WarmFonts()
    {
        UISkin skin = UISkin.Instance;
        var fonts = new List<TMP_FontAsset>();
        if (skin != null && skin.titleFont != null) fonts.Add(skin.titleFont);
        if (skin != null && skin.bodyFont != null) fonts.Add(skin.bodyFont);
        // Font Static (LiberationSans) đã có sẵn glyph, không thêm được -> bỏ qua
        fonts.RemoveAll(f => f.atlasPopulationMode != AtlasPopulationMode.Dynamic);
        if (fonts.Count == 0)
            yield break;

        for (int i = 0; i < fonts.Count; i++)
        {
            fonts[i].TryAddCharacters(VietnameseChars, out _);
            _stepFraction = (i + 1f) / fonts.Count;
            yield return null; // tách từng font ra từng frame để thanh loading còn chạy
        }
    }

    private IEnumerator LoadMap()
    {
        ResourceRequest request = Resources.LoadAsync<MapData>(MapResourcePath);
        while (!request.isDone)
        {
            _stepFraction = request.progress;
            yield return null;
        }
        if (request.asset != null)
            Retained.Add(request.asset);
        else
            Debug.LogWarning("[Preloader] Không tìm thấy Resources/" + MapResourcePath);
    }

    private static IEnumerator WarmCatalogs()
    {
        // Chạm vào các bảng tĩnh để static constructor chạy ở đây thay vì giữa ván chơi
        _ = ResearchCatalog.All.Length;
        _ = NameDatabase.GetFullName(0, 0);
        _ = EdictRuleCatalog.GetLinkName(LogicalLink.AND);
        yield break;
    }

    private IEnumerator LoadAudio(MusicManager music)
    {
        if (music == null)
            yield break;

        // Chỉ nạp nhạc menu (phát ngay ở Home); nhạc mùa nạp khi tới mùa đó
        AsyncOperationHandle<AudioClip> menu = GameAssets.LoadAsync<AudioClip>(music.MenuMusicKey);
        while (!menu.IsDone)
        {
            _stepFraction = menu.PercentComplete * 0.8f;
            yield return null;
        }

        List<AudioClip> sfx = music.GetSfxClips();
        for (int i = 0; i < sfx.Count; i++)
        {
            AudioClip clip = sfx[i];
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;
            _stepFraction = 0.8f + 0.2f * (i + 1f) / sfx.Count;
        }
    }
}
