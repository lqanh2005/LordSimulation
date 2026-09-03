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
        OnSaveStarted?.Invoke();

        string tempPath = _saveFilePath + ".tmp";
        string backupPath = _saveFilePath + ".bak";

        try
        {
            // 1. Gom con trỏ dữ liệu từ các Manager trong Scene
            ref GlobalSystemData globalData = ref GamePlayController.Instance.playerContain.globalSystemManager.GetGlobalDataRef();

            ResidentData[] residents = GamePlayController.Instance.playerContain.residentManager.allResidents;
            int residentCount = GamePlayController.Instance.playerContain.residentManager.activeCount;

            BuildingData[] buildings = GamePlayController.Instance.playerContain.buildingManager.allBuildings;
            int buildingCount = GamePlayController.Instance.playerContain.buildingManager.activeCount;

            EdictRuleData[] edicts = GamePlayController.Instance.playerContain.edictManager.allEdicts;
            int edictCount = GamePlayController.Instance.playerContain.edictManager.activeCount;

            TradeRouteData[] tradeRoutes = GamePlayController.Instance.playerContain.tradeManager.allTradeRoutes;
            int tradeCount = GamePlayController.Instance.playerContain.tradeManager.activeCount;

            // 2. Ghi nhị phân vào file tạm (.tmp)
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

            // 3. Cơ chế Atomic Swap: Hoán đổi file an toàn chống crash
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
            Debug.LogError($"[SaveLoadManager] Lỗi lưu game: {ex.Message}");
            OnSaveFailed?.Invoke(ex.Message);
        }
    }
    public void LoadGame()
    {
        if (!File.Exists(_saveFilePath))
        {
            Debug.LogWarning($"[SaveLoadManager] Không tìm thấy file save tại: {_saveFilePath}");
            OnLoadFailed?.Invoke("File not found");
            return;
        }

        OnLoadStarted?.Invoke();

        try
        {
            // 1. Nạp thẳng dữ liệu nhị phân vào mảng tĩnh có sẵn của các Manager
            using (FileStream fs = new FileStream(_saveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                CitySaveSerializer.DeserializeFullGame(
                    reader,
                    out GlobalSystemData globalData,
                    GamePlayController.Instance.playerContain.residentManager.allResidents, out int residentCount,
                    GamePlayController.Instance.playerContain.buildingManager.allBuildings, out int buildingCount,
                    GamePlayController.Instance.playerContain.edictManager.allEdicts, out int edictCount,
                    GamePlayController.Instance.playerContain.tradeManager.allTradeRoutes, out int tradeCount
                );

                // 2. Cập nhật số lượng active cho từng Manager
                GamePlayController.Instance.playerContain.globalSystemManager.SetGlobalData(globalData);
                GamePlayController.Instance.playerContain.residentManager.activeCount = residentCount;
                GamePlayController.Instance.playerContain.buildingManager.activeCount = buildingCount;
                GamePlayController.Instance.playerContain.edictManager.activeCount = edictCount;
                GamePlayController.Instance.playerContain.tradeManager.activeCount = tradeCount;
            }

            // 3. Tái tạo lại hiển thị thị giác (Visual Views)
            GamePlayController.Instance.playerContain.residentManager.RebindAllVisualAgents();
            GamePlayController.Instance.playerContain.buildingManager.RebuildVisualCity();

            Debug.Log($"[SaveLoadManager] Nạp game thành công!");
            OnLoadCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Lỗi nạp game: {ex.Message}");
            OnLoadFailed?.Invoke(ex.Message);
        }
    }

    public bool HasSaveFile() => File.Exists(_saveFilePath);

    public void DeleteSaveFile()
    {
        if (File.Exists(_saveFilePath))
        {
            File.Delete(_saveFilePath);
            Debug.Log("[SaveLoadManager] Đã xóa file save hiện tại.");
        }
    }
}