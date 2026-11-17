using UnityEngine;

/// <summary>
/// Nhạc nền theo mùa + hiệu ứng âm thanh. Chưa có file âm thanh trong project: gán AudioClip vào Inspector là chạy.
/// Âm lượng đọc từ SettingsPopup (PlayerPrefs) và áp dụng ngay khi đổi.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public const string PrefMaster = "audio.master";
    public const string PrefMusic = "audio.music";
    public const string PrefSfx = "audio.sfx";

    public static MusicManager Instance { get; private set; }

    [Header("Nhạc nền")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip springMusic;
    [SerializeField] private AudioClip summerMusic;
    [SerializeField] private AudioClip autumnMusic;
    [SerializeField] private AudioClip winterMusic;
    [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

    [Header("Hiệu ứng")]
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip notice;
    [SerializeField] private AudioClip alarm;

    private AudioSource _music;
    private AudioSource _sfx;
    private AudioClip _target;
    private float _fade;

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
        ApplyVolumes();
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    private void Start()
    {
        PlayMusic(menuMusic);
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

        AudioClip clip = p.season switch
        {
            SeasonType.Spring => springMusic,
            SeasonType.Summer => summerMusic,
            SeasonType.Autumn => autumnMusic,
            _ => winterMusic
        };
        PlayMusic(clip);
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
    public void PlayNotice(bool critical) => PlaySfx(critical ? alarm : notice);

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
        }

        _music.volume = Mathf.Lerp(0f, MusicVolume, _fade);
        if (_fade >= 1f)
            _target = null;
    }
}
