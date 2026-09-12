using System.Text;
using UnityEngine;

public class ResidentBase : MonoBehaviour
{
    private static readonly StringBuilder OverlayBuilder = new StringBuilder(64);
    private static readonly SymptomFlags[] SymptomOrder =
    {
        SymptomFlags.Fever,
        SymptomFlags.Cough,
        SymptomFlags.Fatigue,
        SymptomFlags.Rash,
        SymptomFlags.Nausea,
        SymptomFlags.Headache,
        SymptomFlags.ShortnessOfBreath,
        SymptomFlags.Dizziness
    };

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Index theo ProfessionType: None, Farmer, Miner, Lumberjack, Craftsman, Doctor, Student, Teacher, Guard")]
    [SerializeField] private Sprite[] professionSprites;

    [Header("Màu theo trạng thái sức khỏe")]
    [SerializeField] private Color healthyColor = Color.white;
    [SerializeField] private Color incubatingColor = new Color(1f, 0.9f, 0.5f);
    [SerializeField] private Color infectedColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color treatedColor = new Color(0.6f, 0.9f, 1f);

    [Header("Scale theo nhóm tuổi")]
    [SerializeField] private float childScale = 0.75f;
    [SerializeField] private float adultScale = 1f;
    [SerializeField] private float elderlyScale = 0.9f;

    [Header("Overlay")]
    [SerializeField] private Vector3 overlayOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private Color overlayColor = new Color(1f, 0.95f, 0.8f);

    public int DataIndex { get; private set; } = -1;

    public bool IsBound => DataIndex >= 0;

    private TextMesh _overlay;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Bind(int dataIndex, in ResidentData data, Vector3 worldPosition)
    {
        DataIndex = dataIndex;
        transform.position = worldPosition;
        gameObject.SetActive(true);
        SyncFromData(in data);
    }

    public void RebindIndex(int dataIndex)
    {
        DataIndex = dataIndex;
    }

    public void Unbind()
    {
        DataIndex = -1;
        gameObject.SetActive(false);
    }

    public void SyncFromManager(ResidentManager manager)
    {
        if (!IsBound || manager == null)
            return;

        SyncFromData(in manager.GetResidentRef(DataIndex));
    }

    public void SyncFromData(in ResidentData data)
    {
        if (!data.isAlive)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyProfessionSprite(data.professionType);
        ApplyHealthTint(data.healthStatus);
        ApplyAgeScale(data.GetAgeGroup());
        ApplyOverlay(in data);

        string fullName = NameDatabase.GetFullName(data.firstNameID, data.lastNameID);
        name = $"Resident_{data.residentID}_{fullName}";
    }

    public void TickCommute(Vector3 target, float step)
    {
        Vector3 current = transform.position;
        Vector3 next = Vector3.MoveTowards(current, target, step);
        transform.position = next;

        if (spriteRenderer == null)
            return;

        float dx = target.x - current.x;
        if (dx > 0.02f)
            spriteRenderer.flipX = false;
        else if (dx < -0.02f)
            spriteRenderer.flipX = true;
    }

    private void ApplyProfessionSprite(ProfessionType profession)
    {
        if (spriteRenderer == null || professionSprites == null || professionSprites.Length == 0)
            return;

        int index = (int)profession;
        if ((uint)index >= (uint)professionSprites.Length)
            return;

        Sprite sprite = professionSprites[index];
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void ApplyHealthTint(HealthStatus status)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = status switch
        {
            HealthStatus.Incubating => incubatingColor,
            HealthStatus.ActiveInfected => infectedColor,
            HealthStatus.Treated => treatedColor,
            _ => healthyColor
        };
    }

    private void ApplyAgeScale(AgeGroupType ageGroup)
    {
        float scale = ageGroup switch
        {
            AgeGroupType.Child => childScale,
            AgeGroupType.Elderly => elderlyScale,
            _ => adultScale
        };

        transform.localScale = Vector3.one * scale;
    }

    private void ApplyOverlay(in ResidentData data)
    {
        EnsureOverlay();
        if (_overlay == null)
            return;

        OverlayBuilder.Clear();
        OverlayBuilder.Append(NameDatabase.GetFullName(data.firstNameID, data.lastNameID));
        OverlayBuilder.Append(data.gender == GenderType.Female ? " (F)" : " (M)");
        OverlayBuilder.Append('\n');
        OverlayBuilder.Append(data.bodyTemperature.ToString("0.0"));
        OverlayBuilder.Append("° ");
        AppendSymptoms(data.symptoms);
        _overlay.text = OverlayBuilder.ToString();
    }

    private static void AppendSymptoms(SymptomFlags symptoms)
    {
        if (symptoms == SymptomFlags.None)
        {
            OverlayBuilder.Append("Khoe");
            return;
        }

        bool first = true;
        for (int i = 0; i < SymptomOrder.Length; i++)
        {
            SymptomFlags flag = SymptomOrder[i];
            if ((symptoms & flag) == 0)
                continue;

            if (!first)
                OverlayBuilder.Append(", ");
            OverlayBuilder.Append(SymptomLabel(flag));
            first = false;
        }
    }

    private static string SymptomLabel(SymptomFlags flag) => flag switch
    {
        SymptomFlags.Fever => "Sot",
        SymptomFlags.Cough => "Ho",
        SymptomFlags.Fatigue => "Met",
        SymptomFlags.Rash => "Ban",
        SymptomFlags.Nausea => "Buon non",
        SymptomFlags.Headache => "Dau dau",
        SymptomFlags.ShortnessOfBreath => "Kho tho",
        SymptomFlags.Dizziness => "Chong mat",
        _ => ""
    };

    private void EnsureOverlay()
    {
        if (_overlay != null)
            return;

        Transform existing = transform.Find("Overlay");
        GameObject overlayGo;
        if (existing != null)
        {
            overlayGo = existing.gameObject;
            _overlay = overlayGo.GetComponent<TextMesh>();
        }
        else
        {
            overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(transform, false);
        }

        overlayGo.transform.localPosition = overlayOffset;
        overlayGo.transform.localRotation = Quaternion.identity;
        overlayGo.transform.localScale = Vector3.one;

        if (_overlay == null)
            _overlay = overlayGo.GetComponent<TextMesh>();
        if (_overlay == null)
            _overlay = overlayGo.AddComponent<TextMesh>();

        _overlay.anchor = TextAnchor.LowerCenter;
        _overlay.alignment = TextAlignment.Center;
        _overlay.fontSize = 24;
        _overlay.characterSize = 0.045f;
        _overlay.color = overlayColor;
        _overlay.lineSpacing = 0.85f;

        MeshRenderer meshRenderer = overlayGo.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.sortingOrder = 20;
    }
}
