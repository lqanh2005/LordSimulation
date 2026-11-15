using System.Collections.Generic;
using UnityEngine;

public class ResidentBase : MonoBehaviour
{
    public const string IsWalkingParam = "IsWalking";
    private static readonly int IsWalkingHash = Animator.StringToHash(IsWalkingParam);
    public const string IsBackParam = "IsBack";
    private static readonly int IsBackHash = Animator.StringToHash(IsBackParam);
    private const float FacingThreshold = 0.0005f;
    private const float WalkThreshold = 0.0005f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation theo nhóm tuổi (Idle/Walk)")]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController childController;
    [SerializeField] private RuntimeAnimatorController adultController;
    [SerializeField] private RuntimeAnimatorController elderlyController;
    [Tooltip("Art gốc quay mặt sang phải? (sprite hiện tại quay sang trái)")]
    [SerializeField] private bool spritesFaceRight;
    [Tooltip("Art quay lưng (clip *_Back) quay sang phải? Mặc định giả định quay lên-trái")]
    [SerializeField] private bool backSpritesFaceRight;
    [Tooltip("Tới nơi / đứng yên thì quay mặt về camera")]
    [SerializeField] private bool faceFrontWhenIdle = true;

    private bool _facingBack;
    private bool _facingRight;

    [Tooltip("Index theo ProfessionType: None, Farmer, Miner, Lumberjack, Craftsman, Doctor, Student, Teacher, Guard")]
    [SerializeField] private Sprite[] professionSprites;

    [Header("Màu theo trạng thái sức khỏe")]
    [SerializeField] private Color healthyColor = Color.white;
    [SerializeField] private Color incubatingColor = new Color(1f, 0.9f, 0.5f);
    [SerializeField] private Color infectedColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color treatedColor = new Color(0.6f, 0.9f, 1f);

    [Header("Scale theo nhóm tuổi")]
    [SerializeField] private float childScale = 1f;
    [SerializeField] private float adultScale = 1f;
    [SerializeField] private float elderlyScale = 1f;

    [Header("Overlay")]
    [SerializeField] private Vector3 overlayOffset = new Vector3(0f, 0.68f, 0f);
    [SerializeField] private Color overlayColor = new Color(1f, 0.95f, 0.8f);

    public int DataIndex { get; private set; } = -1;

    public bool IsBound => DataIndex >= 0;

    private TextMesh _overlay;

    // Layer gốc của prefab (nằm trên layer map) — dành cho overlay chữ để luôn đọc được
    private int _overlaySortingLayerId;
    private int _depthOrder = int.MinValue;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (spriteRenderer != null)
            _overlaySortingLayerId = spriteRenderer.sortingLayerID;
    }

    /// <summary>
    /// Đặt resident vào cùng sorting layer với building/cây và order theo độ sâu isometric,
    /// để vật phía trước che resident và ngược lại.
    /// </summary>
    public void SetDepthSorting(int sortingLayerId, int order)
    {
        if (spriteRenderer == null)
            return;
        if (spriteRenderer.sortingLayerID != sortingLayerId)
            spriteRenderer.sortingLayerID = sortingLayerId;
        if (_depthOrder != order)
        {
            _depthOrder = order;
            spriteRenderer.sortingOrder = order;
        }
    }

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    public void Bind(int dataIndex, in ResidentData data, Vector3 worldPosition)
    {
        DataIndex = dataIndex;
        transform.position = worldPosition;
        ClearPath();
        SetWalking(false);
        _facingBack = false;
        _facingRight = spritesFaceRight;
        SetFacingBack(false);
        ApplyFlip();
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
        ClearPath();
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
        AgeGroupType ageGroup = data.GetAgeGroup();
        ApplyAgeAnimation(ageGroup);
        ApplyAgeScale(ageGroup);
        ApplyOverlay(in data);

        string fullName = NameDatabase.GetFullName(data.firstNameID, data.lastNameID);
        name = $"Resident_{data.residentID}_{fullName}";
    }

    // ===== Đi theo đường A* =====
    private const float GoalChangeThresholdSqr = 0.0001f;

    private readonly List<Vector3> _path = new List<Vector3>(32);
    private int _pathIndex;
    private Vector3 _pathGoal;
    private bool _hasPath;
    private int _pathGridVersion;

    public bool HasPath => _hasPath;
    public IReadOnlyList<Vector3> PathPoints => _path;
    public int PathIndex => _pathIndex;
    public bool IsMoving => _hasPath && _pathIndex < _path.Count;

    /// <summary>Cần tìm đường mới khi đích đổi, hoặc lưới đổi trong lúc đang đi.</summary>
    public bool NeedsPath(Vector3 goal, int gridVersion)
    {
        if (!_hasPath)
            return true;
        if ((goal - _pathGoal).sqrMagnitude > GoalChangeThresholdSqr)
            return true;
        return IsMoving && gridVersion != _pathGridVersion;
    }

    /// <summary>Gán đường đi mới (waypoint world, phần tử cuối là đích chính xác).</summary>
    public void SetPath(List<Vector3> waypoints, Vector3 goal, int gridVersion)
    {
        _path.Clear();
        _path.AddRange(waypoints);
        _pathIndex = 0;
        _pathGoal = goal;
        _pathGridVersion = gridVersion;
        _hasPath = true;

        // Bỏ waypoint đầu nếu đang đứng sát nó (ô xuất phát)
        if (_path.Count > 1)
        {
            Vector3 first = _path[0];
            first.z = 0f;
            Vector3 pos = transform.position;
            pos.z = 0f;
            if ((first - pos).sqrMagnitude < 0.04f)
                _pathIndex = 1;
        }
    }

    public void ClearPath()
    {
        _path.Clear();
        _pathIndex = 0;
        _hasPath = false;
    }

    /// <summary>Di chuyển dọc waypoint với quãng đường <paramref name="step"/> trong frame này.</summary>
    public void TickPath(float step)
    {
        Vector3 start = transform.position;
        Vector3 current = start;
        current.z = 0f;

        float remaining = step;
        while (remaining > 0f && _pathIndex < _path.Count)
        {
            Vector3 target = _path[_pathIndex];
            target.z = 0f;
            float dist = Vector3.Distance(current, target);
            if (dist <= remaining)
            {
                current = target;
                remaining -= dist;
                _pathIndex++;
            }
            else
            {
                current = Vector3.MoveTowards(current, target, remaining);
                remaining = 0f;
            }
        }

        transform.position = current;

        Vector3 delta = current - start;
        delta.z = 0f;
        bool walking = IsMoving || delta.sqrMagnitude > WalkThreshold * WalkThreshold;
        SetWalking(walking);

        // Hướng mặt: đi lên màn hình → quay lưng; đi xuống → mặt trước;
        // đi ngang thuần giữ hướng cũ để không nhấp nháy. Đứng yên → quay mặt trước (tuỳ chọn).
        if (walking)
        {
            if (delta.y > FacingThreshold)
                _facingBack = true;
            else if (delta.y < -FacingThreshold)
                _facingBack = false;
        }
        else if (faceFrontWhenIdle)
        {
            _facingBack = false;
        }
        SetFacingBack(_facingBack);

        if (delta.x > FacingThreshold)
            _facingRight = true;
        else if (delta.x < -FacingThreshold)
            _facingRight = false;

        if (spriteRenderer == null)
            return;

        ApplyFlip();
    }

    private void ApplyFlip()
    {
        if (spriteRenderer == null)
            return;

        bool artFacesRight = _facingBack ? backSpritesFaceRight : spritesFaceRight;
        spriteRenderer.flipX = _facingRight != artFacesRight;
    }

    private void SetWalking(bool walking)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (animator.GetBool(IsWalkingHash) != walking)
            animator.SetBool(IsWalkingHash, walking);
    }

    private void SetFacingBack(bool back)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (animator.GetBool(IsBackHash) != back)
            animator.SetBool(IsBackHash, back);
    }

    private void ApplyAgeAnimation(AgeGroupType ageGroup)
    {
        if (animator == null)
            return;

        RuntimeAnimatorController controller = ageGroup switch
        {
            AgeGroupType.Child => childController,
            AgeGroupType.Elderly => elderlyController,
            _ => adultController
        };

        if (controller != null && animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;
    }

    private void ApplyProfessionSprite(ProfessionType profession)
    {
        if (spriteRenderer == null || professionSprites == null || professionSprites.Length == 0)
            return;
        // Animator ghi đè sprite mỗi frame -> sprite nghề chỉ dùng khi không có animation
        if (animator != null && animator.runtimeAnimatorController != null)
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

        // Chỉ hiện tên trên đầu
        string fullName = NameDatabase.GetFullName(data.firstNameID, data.lastNameID);
        if (_overlay.text != fullName)
            _overlay.text = fullName;
    }

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

        // Chữ thông tin luôn nằm trên map/building để đọc được
        MeshRenderer meshRenderer = overlayGo.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerID = _overlaySortingLayerId;
            meshRenderer.sortingOrder = 20;
        }
    }
}
