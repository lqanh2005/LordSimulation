using UnityEngine;

/// <summary>
/// Nhạc nền theo mùa + hiệu ứng âm thanh. Âm lượng đọc từ SettingsPopup (PlayerPrefs).
/// Nhạc nền nạp qua Addressables theo address (GameAssets.Music*), chỉ giữ bài đang phát trong bộ nhớ:
/// đổi bài thì nạp bài mới, phát xong phần chuyển thì Release bài cũ. Hiệu ứng (nhỏ) gán thẳng.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public const string PrefMaster = "audio.master";
    public const string PrefMusic = "audio.music";
    public const string PrefSfx = "audio.sfx";

    public static MusicManager Instance { get; private set; }

    [Header("Nhạc nền (address Addressables)")]
    [SerializeField] private string menuMusicKey = GameAssets.MusicMenuKey;
    [SerializeField] private string springMusicKey = GameAssets.MusicSpringKey;
    [SerializeField] private string summerMusicKey = GameAssets.MusicSummerKey;
    [SerializeField] private string autumnMusicKey = GameAssets.MusicAutumnKey;
    [SerializeField] private string winterMusicKey = GameAssets.MusicWinterKey;
    [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

    [Header("Hiệu ứng")]
    [SerializeField] private AudioClip uiClick;
    [Tooltip("Thông báo mức Info (nhẹ)")]
    [SerializeField] private AudioClip alertInfo;
    [Tooltip("Thông báo mức Warning")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("notice")] private AudioClip alertWarning;
    [Tooltip("Thông báo mức Critical")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("alarm")] private AudioClip alertCritical;

    private AudioSource _music;
    private AudioSource _sfx;
    private AudioClip _target;
    private float _fade;

    private string _playingKey;   // bài đang phát (đang giữ handle)
    private string _targetKey;    // bài đang nạp / chuẩn bị chuyển sang

    public string MenuMusicKey => menuMusicKey;

    /// <summary>Clip hiệu ứng (nhỏ, gán thẳng) — màn Loading nạp trước dữ liệu âm thanh.</summary>
    public System.Collections.Generic.List<AudioClip> GetSfxClips()
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (AudioClip clip in new[] { uiClick, alertInfo, alertWarning, alertCritical })
        {
            if (clip != null && !clips.Contains(clip))
                clips.Add(clip);
        }
        return clips;
    }

    public static float MasterVolume => PlayerPrefs.GetFloat(PrefMaster, 1f);
    public static float MusicVolume => PlayerPrefs.GetFloat(PrefMusic, 0.7f);
    public static float SfxVolume => PlayerPrefs.GetFloat(PrefSfx, 0.8f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _music = gameObject.AddComponent<AudioSource>();
        _music.loop = true;
        _music.playOnAwake = false;
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        // Tiếng click cho mọi nút UI (không cần gắn listener từng nút)
        if (GetComponent<UIClickSound>() == null)
            gameObject.AddComponent<UIClickSound>();
        ApplyVolumes();
    }

    private void OnEnable()
    {
        Subscribe();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// EventDispatcher bị huỷ (và xoá hết listener) mỗi lần đổi scene, còn MusicManager sống suốt game
    /// (nằm trên GameController DontDestroyOnLoad) → phải đăng ký lại sau mỗi lần nạp scene.
    /// </summary>
    private void Subscribe()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Subscribe();

        // Về màn hình chính / màn tải -> nhạc menu; vào gameplay thì nhạc mùa do MonthChanged quyết định
        GameController app = GameController.Instance;
        if (app == null || scene.name != app.GameplaySceneName)
            PlayMusicKey(menuMusicKey);
    }

    private void Start()
    {
        PlayMusicKey(menuMusicKey);
    }

    public void ApplyVolumes()
    {
        AudioListener.volume = MasterVolume;
        if (_music != null && _target == null)
            _music.volume = MusicVolume;
    }

    private void OnMonthChanged(object param)
    {
        if (!(param is MonthChangedPayload p))
            return;

        string key = p.season switch
        {
            SeasonType.Spring => springMusicKey,
            SeasonType.Summer => summerMusicKey,
            SeasonType.Autumn => autumnMusicKey,
            _ => winterMusicKey
        };
        PlayMusicKey(key);
    }

    /// <summary>Nạp bài theo address (bất đồng bộ) rồi chuyển nhạc có fade.</summary>
    public void PlayMusicKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key == _playingKey || key == _targetKey)
            return;

        // Đang nạp dở một bài khác mà chưa phát: bỏ bài đó
        if (!string.IsNullOrEmpty(_targetKey) && _targetKey != _playingKey)
            GameAssets.Release(_targetKey);

        _targetKey = key;
        GameAssets.LoadAsync<AudioClip>(key).Completed += handle =>
        {
            if (_targetKey != key)
                return; // đã đổi ý sang bài khác trong lúc nạp
            if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[MusicManager] Không nạp được nhạc '{key}'.");
                GameAssets.Release(key);
                _targetKey = null;
                return;
            }
            PlayMusic(handle.Result);
        };
    }

    /// <summary>Chuyển nhạc có fade (bỏ qua nếu clip null hoặc đang phát).</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || _music == null || _music.clip == clip)
            return;
        _target = clip;
        _fade = 0f;
    }

    public void PlayUiClick() => PlaySfx(uiClick);
    /// <summary>Âm báo theo mức thông báo (Info / Warning / Critical).</summary>
    /// Nhiều thông báo ra cùng lúc (vd. sang tháng mới) thì chỉ kêu một lần, trừ khi mức mới cao hơn.
    public void PlayAlert(NoticeLevel level)
    {
        float now = Time.unscaledTime;
        if (now - _lastAlertTime < AlertCooldown && level <= _lastAlertLevel)
            return;
        _lastAlertTime = now;
        _lastAlertLevel = level;

        PlaySfx(level switch
        {
            NoticeLevel.Critical => alertCritical,
            NoticeLevel.Warning => alertWarning,
            _ => alertInfo
        });
    }

    private const float AlertCooldown = 0.5f;
    private float _lastAlertTime = -10f;
    private NoticeLevel _lastAlertLevel;

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && _sfx != null)
            _sfx.PlayOneShot(clip, SfxVolume);
    }

    private void Update()
    {
        if (_target == null || _music == null)
            return;

        // Fade out nhạc cũ rồi fade in nhạc mới (dùng unscaled để chạy cả khi Pause)
        _fade += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
        if (_music.clip != _target)
        {
            _music.volume = Mathf.Lerp(MusicVolume, 0f, _fade);
            if (_fade < 1f && _music.isPlaying)
                return;
            _music.clip = _target;
            _music.Play();
            _fade = 0f;

            // Bài cũ đã dừng hẳn -> trả bộ nhớ
            if (!string.IsNullOrEmpty(_targetKey) && _targetKey != _playingKey)
            {
                if (!string.IsNullOrEmpty(_playingKey))
                    GameAssets.Release(_playingKey);
                _playingKey = _targetKey;
                _targetKey = null;
            }
        }

        _music.volume = Mathf.Lerp(0f, MusicVolume, _fade);
        if (_fade >= 1f)
            _target = null;
    }
}
