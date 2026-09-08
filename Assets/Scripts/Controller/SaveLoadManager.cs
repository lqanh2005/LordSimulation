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
    [SerializeField] private string gameplaySceneName = "GameplayScene";

    // Events thông báo trạng thái để UI / Audio phản hồi
    public event Action OnSaveStarted;
    public event Action OnSaveCompleted;
    public event Action<string> OnSaveFailed;

    public event Action OnLoadStarted;
    public event Action OnLoadCompleted;
    public event Action<string> OnLoadFailed;

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
        if (pauseStatus && SceneManager.GetActiveScene().name == gameplaySceneName)
        {
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
        // Tự động lưu trước khi ứng dụng đóng hoàn toàn
        if (SceneManager.GetActiveScene().name == gameplaySceneName)
        {
            SaveGame();
        }
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
            OnSaveFailed?.Invoke("Save path not initialized");
            return;
        }

        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
        {
            Debug.LogError("[SaveLoadManager] GamePlayController hoặc PlayerContain chưa sẵn sàng.");
            OnSaveFailed?.Invoke("Gameplay not ready");
            return;
        }

        OnSaveStarted?.Invoke();
        _isSaving = true;

        string tempPath = _saveFilePath + ".tmp";
        string backupPath = _saveFilePath + ".bak";

        try
        {
            PlayerContain contain = GamePlayController.Instance.playerContain;

            ref GlobalSystemData globalData = ref contain.globalSystemManager.GetGlobalDataRef();

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
                    tradeRoutes, tradeCount
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
            OnSaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            Debug.LogError($"[SaveLoadManager] Lỗi lưu game: {ex.Message}");
            OnSaveFailed?.Invoke(ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public void LoadGame()
    {
        if (string.IsNullOrEmpty(_saveFilePath))
        {
            Debug.LogError("[SaveLoadManager] Chưa Init() — không thể nạp.");
            OnLoadFailed?.Invoke("Save path not initialized");
            return;
        }

        if (!File.Exists(_saveFilePath))
        {
            Debug.LogWarning($"[SaveLoadManager] Không tìm thấy file save tại: {_saveFilePath}");
            OnLoadFailed?.Invoke("File not found");
            return;
        }

        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
        {
            Debug.LogError("[SaveLoadManager] GamePlayController hoặc PlayerContain chưa sẵn sàng.");
            OnLoadFailed?.Invoke("Gameplay not ready");
            return;
        }

        OnLoadStarted?.Invoke();

        try
        {
            PlayerContain contain = GamePlayController.Instance.playerContain;

            using (FileStream fs = new FileStream(_saveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                CitySaveSerializer.DeserializeFullGame(
                    reader,
                    out GlobalSystemData globalData,
                    contain.residentManager.allResidents, out int residentCount,
                    contain.buildingManager.allBuildings, out int buildingCount,
                    contain.edictManager.allEdicts, out int edictCount,
                    contain.tradeManager.allTradeRoutes, out int tradeCount
                );

                contain.globalSystemManager.SetGlobalData(globalData);
                contain.residentManager.activeCount = residentCount;
                contain.buildingManager.activeCount = buildingCount;
                contain.edictManager.activeCount = edictCount;
                contain.tradeManager.activeCount = tradeCount;

                ClearInactiveSlots(contain.residentManager.allResidents, residentCount);
                ClearInactiveSlots(contain.buildingManager.allBuildings, buildingCount);
                ClearInactiveSlots(contain.edictManager.allEdicts, edictCount);
                ClearInactiveSlots(contain.tradeManager.allTradeRoutes, tradeCount);
            }

            contain.residentManager.RebindAllVisualAgents();
            contain.buildingManager.RebuildVisualCity();

            Debug.Log("[SaveLoadManager] Nạp game thành công!");
            OnLoadCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Lỗi nạp game: {ex.Message}");
            OnLoadFailed?.Invoke(ex.Message);
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
