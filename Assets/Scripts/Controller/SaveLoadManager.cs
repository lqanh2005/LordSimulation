using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadManager : MonoBehaviour
{
    [Header("Cấu Hình Tệp Tin")]
    [SerializeField] private string saveSlotFileName = "city_save_slot_1.dat";
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveIntervalMinutes = 5f;

    [Header("Tên Scene Gameplay")]
    [SerializeField] private string gameplaySceneName = "GamePlayScene";

    private string _saveFilePath;
    private float _autoSaveTimer;
    private bool _isSaving;

    public void Init()
    {
        _saveFilePath = Path.Combine(Application.persistentDataPath, saveSlotFileName);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Tự động lưu khi người chơi ẩn app / home ra ngoài
        if (pauseStatus && CanAutoSave())
        {
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
        // Tự động lưu trước khi ứng dụng đóng hoàn toàn
        if (CanAutoSave())
        {
            SaveGame();
        }
    }

    public void TickAutoSave(float deltaTime)
    {
        if (!enableAutoSave || autoSaveIntervalMinutes <= 0f)
            return;

        _autoSaveTimer += deltaTime;
        if (_autoSaveTimer < autoSaveIntervalMinutes * 60f)
            return;

        _autoSaveTimer = 0f;
        SaveGame();
    }

    public void ResetAutoSaveTimer()
    {
        _autoSaveTimer = 0f;
    }

    private bool CanAutoSave()
    {
        GamePlayController gameplay = GamePlayController.Instance;
        if (gameplay == null
            || !gameplay.IsIn(GamePlayStateID.Playing) && !gameplay.IsIn(GamePlayStateID.Paused))
            return false;

        if (GameController.Instance != null)
            return GameController.Instance.IsIn(AppStateID.GamePlay);

        return SceneManager.GetActiveScene().name == gameplaySceneName;
    }

    public void SaveGame()
    {
        if (_isSaving)
        {
            Debug.LogWarning("[SaveLoadManager] Đang lưu, bỏ qua yêu cầu trùng lặp.");
            return;
        }

        if (string.IsNullOrEmpty(_saveFilePath))
        {
            Debug.LogError("[SaveLoadManager] Chưa Init() — không thể lưu.");
            GameEvents.Post(EventID.SaveFailed, "Save path not initialized");
            return;
        }

        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
        {
            Debug.LogError("[SaveLoadManager] GamePlayController hoặc PlayerContain chưa sẵn sàng.");
            GameEvents.Post(EventID.SaveFailed, "Gameplay not ready");
            return;
        }

        GameEvents.Post(EventID.SaveStarted);
        _isSaving = true;

        string tempPath = _saveFilePath + ".tmp";
        string backupPath = _saveFilePath + ".bak";

        try
        {
            PlayerContain contain = GamePlayController.Instance.playerContain;

            GlobalSystemData globalData = contain.globalSystemManager.GetGlobalDataRef();
            globalData.currentGameSpeed = GamePlayController.Instance.ResolveSaveSpeed(globalData.currentGameSpeed);

            ResidentData[] residents = contain.residentManager.allResidents;
            int residentCount = contain.residentManager.activeCount;

            BuildingData[] buildings = contain.buildingManager.allBuildings;
            int buildingCount = contain.buildingManager.activeCount;

            EdictRuleData[] edicts = contain.edictManager.allEdicts;
            int edictCount = contain.edictManager.activeCount;

            TradeRouteData[] tradeRoutes = contain.tradeManager.allTradeRoutes;
            int tradeCount = contain.tradeManager.activeCount;

            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                CitySaveSerializer.SerializeFullGame(
                    writer,
                    in globalData,
                    residents, residentCount,
                    buildings, buildingCount,
                    edicts, edictCount,
                    tradeRoutes, tradeCount,
                    contain.immigrationManager.waitingApplicants,
                    contain.immigrationManager.waitingCount,
                    contain.researchManager != null ? contain.researchManager.GetSaveData() : default,
                    contain.merchantManager != null ? contain.merchantManager.GetSaveData() : default
                );
            }

            if (File.Exists(_saveFilePath))
            {
                File.Replace(tempPath, _saveFilePath, backupPath);
            }
            else
            {
                File.Move(tempPath, _saveFilePath);
            }

            Debug.Log($"[SaveLoadManager] Lưu game thành công vào: {_saveFilePath}");
            GameEvents.Post(EventID.SaveCompleted);
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            Debug.LogError($"[SaveLoadManager] Lỗi lưu game: {ex.Message}");
            GameEvents.Post(EventID.SaveFailed, ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public bool LoadGame()
    {
        if (string.IsNullOrEmpty(_saveFilePath))
        {
            Debug.LogError("[SaveLoadManager] Chưa Init() — không thể nạp.");
            GameEvents.Post(EventID.LoadFailed, "Save path not initialized");
            return false;
        }

        if (!File.Exists(_saveFilePath))
        {
            Debug.LogWarning($"[SaveLoadManager] Không tìm thấy file save tại: {_saveFilePath}");
            GameEvents.Post(EventID.LoadFailed, "File not found");
            return false;
        }

        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
        {
            Debug.LogError("[SaveLoadManager] GamePlayController hoặc PlayerContain chưa sẵn sàng.");
            GameEvents.Post(EventID.LoadFailed, "Gameplay not ready");
            return false;
        }

        GameEvents.Post(EventID.LoadStarted);
        PlayerContain contain = GamePlayController.Instance.playerContain;
        if (contain.immigrationManager != null)
            contain.immigrationManager.SetSuppressArrivals(true);
        if (contain.edictManager != null)
            contain.edictManager.SetSuppressEffects(true);

        try
        {
            // Đọc vào mảng tạm: file hỏng giữa chừng sẽ không làm bẩn dữ liệu đang chạy
            ResidentData[] residentBuffer = new ResidentData[contain.residentManager.allResidents.Length];
            BuildingData[] buildingBuffer = new BuildingData[contain.buildingManager.allBuildings.Length];
            EdictRuleData[] edictBuffer = new EdictRuleData[contain.edictManager.allEdicts.Length];
            TradeRouteData[] tradeBuffer = new TradeRouteData[contain.tradeManager.allTradeRoutes.Length];
            ResidentData[] immigrantBuffer = new ResidentData[contain.immigrationManager.waitingApplicants.Length];

            using (FileStream fs = new FileStream(_saveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                CitySaveSerializer.DeserializeFullGame(
                    reader,
                    out GlobalSystemData globalData,
                    residentBuffer, out int residentCount,
                    buildingBuffer, out int buildingCount,
                    edictBuffer, out int edictCount,
                    tradeBuffer, out int tradeCount,
                    immigrantBuffer, out int immigrantCount,
                    out ResearchSaveData researchData,
                    out MerchantSaveData merchantData
                );

                Array.Copy(residentBuffer, contain.residentManager.allResidents, residentBuffer.Length);
                Array.Copy(buildingBuffer, contain.buildingManager.allBuildings, buildingBuffer.Length);
                Array.Copy(edictBuffer, contain.edictManager.allEdicts, edictBuffer.Length);
                Array.Copy(tradeBuffer, contain.tradeManager.allTradeRoutes, tradeBuffer.Length);
                Array.Copy(immigrantBuffer, contain.immigrationManager.waitingApplicants, immigrantBuffer.Length);

                contain.globalSystemManager.SetGlobalData(globalData);
                contain.residentManager.activeCount = residentCount;
                contain.buildingManager.activeCount = buildingCount;
                contain.buildingManager.InvalidateIdCache();
                contain.edictManager.activeCount = edictCount;
                contain.tradeManager.activeCount = tradeCount;
                contain.immigrationManager.waitingCount = immigrantCount;
                if (contain.researchManager != null)
                    contain.researchManager.ApplySaveData(in researchData);
                if (contain.merchantManager != null)
                    contain.merchantManager.ApplySaveData(in merchantData);

                ClearInactiveSlots(contain.residentManager.allResidents, residentCount);
                ClearInactiveSlots(contain.buildingManager.allBuildings, buildingCount);
                ClearInactiveSlots(contain.edictManager.allEdicts, edictCount);
                ClearInactiveSlots(contain.tradeManager.allTradeRoutes, tradeCount);
                ClearInactiveSlots(contain.immigrationManager.waitingApplicants, immigrantCount);
            }

            contain.residentManager.RebindAllVisualAgents();
            contain.buildingManager.RebuildVisualCity();
            contain.immigrationManager.RefreshAfterLoad();
            contain.edictManager.RefreshAfterLoad();

            _autoSaveTimer = 0f;
            ResidentSocialSystem.ResetStats();
            Debug.Log("[SaveLoadManager] Nạp game thành công!");
            GameEvents.Post(EventID.LoadCompleted);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Lỗi nạp game: {ex.Message}");
            GameEvents.Post(EventID.LoadFailed, ex.Message);
            return false;
        }
        finally
        {
            if (contain.immigrationManager != null)
                contain.immigrationManager.SetSuppressArrivals(false);
            if (contain.edictManager != null)
                contain.edictManager.SetSuppressEffects(false);
        }
    }

    public bool HasSaveFile() => !string.IsNullOrEmpty(_saveFilePath) && File.Exists(_saveFilePath);

    public void DeleteSaveFile()
    {
        if (HasSaveFile())
        {
            File.Delete(_saveFilePath);
            Debug.Log("[SaveLoadManager] Đã xóa file save hiện tại.");
        }
    }

    private static void ClearInactiveSlots<T>(T[] array, int activeCount) where T : struct
    {
        if (activeCount >= array.Length)
            return;

        Array.Clear(array, activeCount, array.Length - activeCount);
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveLoadManager] Không xóa được file tạm {path}: {ex.Message}");
        }
    }
}
