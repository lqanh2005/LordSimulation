using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Phím tắt gameplay theo GDD (mục 6). Gom về một chỗ để dễ đổi phím và tránh xung đột.
///   Space      Dừng / tiếp tục          Tab     Đổi tốc độ x1 → x2 → x3
///   F5         Lưu nhanh                Delete  Bật/tắt chế độ phá (X vẫn dùng được)
///   G          Cổng thành (kiểm duyệt)  H       Đưa camera về Tòa thị chính
///   T          Cây nghiên cứu           E       Sắc lệnh
///   1..N       Mở nhóm xây dựng trên thanh dưới
///   Esc        Đóng cửa sổ đang mở; không còn cửa sổ nào thì mở Pause Menu
/// WASD / chuột giữa / cuộn chuột do IsoTestCamera xử lý.
/// Chạy trước PlacementManager để Esc/chuột phải huỷ đặt nhà được ưu tiên đúng.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameHotkeys : MonoBehaviour
{
    private PlacementManager _placement;
    private GameSpeedHUDController _speedHud;
    private PauseMenuController _pauseMenu;
    private BuildMenuController _buildMenu;
    private ResearchTreePopupController _research;
    private EdictPopupController _edicts;
    private ImmigrationPopupUI _immigrationUi;
    private ResidentInfoPopup _residentPopup;
    private BuildingInfoPopup _buildingPopup;
    private FarmCropPopup _farmPopup;
    private MerchantPopup _merchantPopup;
    private bool _quickSaving;

    public static void EnsureInstance(PlacementManager placement)
    {
        if (FindFirstObjectByType<GameHotkeys>() != null)
            return;

        GameObject go = new GameObject("GameHotkeys");
        go.AddComponent<GameHotkeys>()._placement = placement;
    }

    /// <summary>Đang gõ chữ vào ô nhập (vd. tên sắc lệnh) -> bỏ qua phím tắt.</summary>
    public static bool IsTypingInUI()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused)
            return true;

        UnityEngine.UI.InputField legacy = selected.GetComponent<UnityEngine.UI.InputField>();
        return legacy != null && legacy.isFocused;
    }

    private void Start()
    {
        ResolveAll();
        // Esc do hotkeys điều phối để đóng cửa sổ trước khi mở Pause Menu
        if (_pauseMenu != null)
            _pauseMenu.ToggleWithEscape = false;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.SaveCompleted, OnSaveCompleted);
        GameEvents.Listen(EventID.SaveFailed, OnSaveFailed);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.SaveCompleted, OnSaveCompleted);
        GameEvents.Unlisten(EventID.SaveFailed, OnSaveFailed);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || IsTypingInUI())
            return;

        if (kb.escapeKey.wasPressedThisFrame)
            HandleEscape();

        GamePlayController gameplay = GamePlayController.Instance;
        // Các phím còn lại chỉ dùng khi đang chơi (không ở Pause Menu / Game Over / Chiến thắng)
        if (gameplay == null || !gameplay.IsIn(GamePlayStateID.Playing))
            return;

        if (kb.spaceKey.wasPressedThisFrame)
        {
            // Bỏ chọn nút UI để Space không "bấm lại" nút vừa click
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            TogglePause();
        }
        if (kb.tabKey.wasPressedThisFrame) CycleSpeed();
        if (kb.f5Key.wasPressedThisFrame) QuickSave();
        if (kb.deleteKey.wasPressedThisFrame && _placement != null) _placement.ToggleDemolish();
        if (kb.gKey.wasPressedThisFrame) ToggleGate();
        if (kb.hKey.wasPressedThisFrame) FocusCityHall();
        if (kb.tKey.wasPressedThisFrame) ToggleExclusive(_research != null ? _research.gameObject : null, () => _research.Toggle());
        if (kb.eKey.wasPressedThisFrame) ToggleExclusive(_edicts != null ? _edicts.gameObject : null, () => _edicts.Toggle());

        HandleBuildCategoryKeys(kb);
    }

    // ==========================================
    // ESC
    // ==========================================

    private void HandleEscape()
    {
        // Đang đặt / phá công trình: PlacementManager tự huỷ
        if (_placement != null && (_placement.isPlacing || _placement.IsDemolishing))
            return;

        if (ConfirmDialog.IsOpen)
        {
            ConfirmDialog.Close();
            return;
        }

        if (SaveLoadWindow.IsOpen)
        {
            SaveLoadWindow.Close();
            return;
        }

        if (NoticeWindow.IsOpen)
        {
            NoticeWindow.Close();
            return;
        }

        if (SettingsPopup.AnyOpen)
        {
            SettingsPopup.CloseIfOpen();
            return;
        }

        if (_pauseMenu != null && _pauseMenu.IsOpen)
        {
            _pauseMenu.Resume();
            return;
        }

        if (CloseTopWindow())
            return;

        GamePlayController gameplay = GamePlayController.Instance;
        if (_pauseMenu != null && gameplay != null && gameplay.IsIn(GamePlayStateID.Playing))
            _pauseMenu.Show();
    }

    /// <summary>Đóng cửa sổ trên cùng (popup nhỏ trước, cửa sổ lớn sau). Trả về true nếu đã đóng.</summary>
    private bool CloseTopWindow()
    {
        if (_residentPopup != null && _residentPopup.IsOpen) { _residentPopup.Hide(); return true; }
        if (_buildingPopup != null && _buildingPopup.IsOpen) { _buildingPopup.Hide(); return true; }
        if (_farmPopup != null && _farmPopup.IsOpen) { _farmPopup.Hide(); return true; }
        if (_immigrationUi != null && _immigrationUi.IsOpen) { _immigrationUi.Hide(); return true; }
        if (_merchantPopup != null && _merchantPopup.IsOpen) { _merchantPopup.Hide(); return true; }
        if (_research != null && UIPopupAnim.IsVisible(_research.gameObject)) { _research.Hide(); return true; }
        if (_edicts != null && UIPopupAnim.IsVisible(_edicts.gameObject)) { _edicts.Hide(); return true; }
        if (_buildMenu != null && _buildMenu.IsOpen) { _buildMenu.Close(); return true; }
        return false;
    }

    // ==========================================
    // TỐC ĐỘ
    // ==========================================

    private void TogglePause()
    {
        if (_speedHud != null)
        {
            _speedHud.TogglePause();
            return;
        }

        GlobalSystemManager global = HUDLocator.ResolveGlobalSystem(null);
        if (global == null)
            return;
        GameSpeed current = global.GetGlobalDataRef().currentGameSpeed;
        global.SetGameSpeed(current == GameSpeed.Pause ? GameSpeed.Speed1x : GameSpeed.Pause);
    }

    private void CycleSpeed()
    {
        GlobalSystemManager global = HUDLocator.ResolveGlobalSystem(null);
        if (global == null)
            return;

        GameSpeed next = global.GetGlobalDataRef().currentGameSpeed switch
        {
            GameSpeed.Speed1x => GameSpeed.Speed2x,
            GameSpeed.Speed2x => GameSpeed.Speed3x,
            _ => GameSpeed.Speed1x      // x3 -> x1, và đang dừng -> chạy x1
        };

        if (_speedHud != null)
            _speedHud.SetSpeed(next);
        else
            global.SetGameSpeed(next);
    }

    // ==========================================
    // LƯU NHANH
    // ==========================================

    private void QuickSave()
    {
        SaveLoadManager saveLoad = GameController.Instance != null
            ? GameController.Instance.saveLoadManager
            : FindFirstObjectByType<SaveLoadManager>();
        if (saveLoad == null)
        {
            NotificationFeed.Show("Không tìm thấy hệ thống lưu game", NoticeLevel.Warning, "quicksave");
            return;
        }

        _quickSaving = true;
        saveLoad.SaveGame();
    }

    private void OnSaveCompleted(object param)
    {
        if (!_quickSaving)
            return;
        _quickSaving = false;
        NotificationFeed.Show("Đã lưu nhanh (F5)", NoticeLevel.Info, "quicksave");
    }

    private void OnSaveFailed(object param)
    {
        if (!_quickSaving)
            return;
        _quickSaving = false;
        NotificationFeed.Show("Lưu nhanh thất bại", NoticeLevel.Warning, "quicksave");
    }

    // ==========================================
    // CỬA SỔ
    // ==========================================

    private void ToggleGate()
    {
        if (_immigrationUi != null && _immigrationUi.IsOpen)
        {
            _immigrationUi.Hide();
            return;
        }

        PlayerContain contain = FindFirstObjectByType<PlayerContain>();
        ImmigrationManager immigration = contain != null ? contain.immigrationManager : null;
        if (immigration == null)
            immigration = FindFirstObjectByType<ImmigrationManager>();
        if (immigration == null)
            return;

        CloseLargeWindows();
        immigration.OpenInspection();
        if (_immigrationUi == null)
            _immigrationUi = FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
    }

    /// <summary>Bật/tắt một cửa sổ lớn, đóng các cửa sổ lớn khác để không chồng nhau.</summary>
    private void ToggleExclusive(GameObject window, System.Action toggle)
    {
        if (window == null)
            return;

        if (!UIPopupAnim.IsVisible(window))
            CloseLargeWindows();
        toggle();
    }

    private void CloseLargeWindows()
    {
        if (_research != null && UIPopupAnim.IsVisible(_research.gameObject)) _research.Hide();
        if (_edicts != null && UIPopupAnim.IsVisible(_edicts.gameObject)) _edicts.Hide();
        if (_immigrationUi != null && _immigrationUi.IsOpen) _immigrationUi.Hide();
        if (_merchantPopup != null && _merchantPopup.IsOpen) _merchantPopup.Hide();
    }

    private void HandleBuildCategoryKeys(Keyboard kb)
    {
        if (_buildMenu == null)
            return;

        int count = Mathf.Min(_buildMenu.CategoryCount, 9);
        for (int i = 0; i < count; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame)
            {
                if (_placement != null)
                {
                    _placement.CancelPlacement();
                    _placement.CancelDemolish();
                }
                _buildMenu.ToggleCategory(i);
                return;
            }
        }
    }

    // ==========================================
    // CAMERA
    // ==========================================

    private void FocusCityHall()
    {
        Camera cam = Camera.main;
        if (cam == null || _placement == null || _placement.buildingManager == null)
            return;

        BuildingManager buildings = _placement.buildingManager;
        for (int i = 0; i < buildings.activeCount; i++)
        {
            ref BuildingData b = ref buildings.GetBuildingRef(i);
            if (b.buildingType != BuildingType.CityHall)
                continue;

            BuildingView view = _placement.GetBuildingView(b.buildingID);
            if (view == null)
                break;

            Vector3 target = view.transform.position;
            Vector2Int size = view.Footprint;
            target.y += (size.x + size.y) * 0.125f;
            target.z = cam.transform.position.z;
            cam.transform.position = target;
            return;
        }

        NotificationFeed.Show("Chưa có Tòa thị chính", NoticeLevel.Info, "focus-cityhall");
    }

    private void ResolveAll()
    {
        if (_placement == null) _placement = FindFirstObjectByType<PlacementManager>();
        _speedHud = FindFirstObjectByType<GameSpeedHUDController>(FindObjectsInactive.Include);
        _pauseMenu = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        _buildMenu = FindFirstObjectByType<BuildMenuController>(FindObjectsInactive.Include);
        _research = FindFirstObjectByType<ResearchTreePopupController>(FindObjectsInactive.Include);
        _edicts = FindFirstObjectByType<EdictPopupController>(FindObjectsInactive.Include);
        _immigrationUi = FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
        _residentPopup = FindFirstObjectByType<ResidentInfoPopup>();
        _buildingPopup = FindFirstObjectByType<BuildingInfoPopup>();
        _farmPopup = FindFirstObjectByType<FarmCropPopup>();
        _merchantPopup = FindFirstObjectByType<MerchantPopup>();
    }
}
