using UnityEngine;

public class ResidentBase : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Index theo ProfessionType: None, Farmer, Miner, Lumberjack, Craftsman, Doctor")]
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

    public int DataIndex { get; private set; } = -1;

    public bool IsBound => DataIndex >= 0;

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

        name = $"Resident_{data.residentID}_{data.professionType}";
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
}
