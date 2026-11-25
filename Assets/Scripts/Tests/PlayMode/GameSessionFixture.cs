#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Nền cho PlayMode test: mỗi test nạp GamePlayScene thật (ván mới, vì không có GameController
/// từ LoadingScene) và chờ tới trạng thái Playing. Có sẵn hàm tua tháng, đặt công trình, dựng người nhập cư.
/// Kịch bản tương ứng: Docs/KichBanTest.md.
/// </summary>
public abstract class GameSessionFixture
{
    protected const string SceneName = "GamePlayScene";
    private const int MaxWaitFrames = 600;

    protected GamePlayController Gameplay => GamePlayController.Instance;
    protected PlayerContain Contain => Gameplay.playerContain;
    protected GlobalSystemManager GlobalSystem => Contain.globalSystemManager;
    protected ref GlobalSystemData Data => ref GlobalSystem.GetGlobalDataRef();
    protected PlacementManager Placement { get; private set; }

    [UnitySetUp]
    public IEnumerator LoadGameScene()
    {
        // Instance của test trước còn sống thêm 1 frame sau LoadScene -> phải chờ instance MỚI
        GamePlayController previous = GamePlayController.Instance;
        SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

        GridController grid = null;
        for (int i = 0; i < MaxWaitFrames; i++)
        {
            GamePlayController current = GamePlayController.Instance;
            if (current != null && current != previous && current.IsIn(GamePlayStateID.Playing))
            {
                grid = Object.FindFirstObjectByType<GridController>();
                if (grid != null && grid.IsGenerated)
                    break;
            }
            yield return null;
        }

        Assert.IsNotNull(GamePlayController.Instance, "Không thấy GamePlayController trong scene");
        Assert.AreNotSame(previous, GamePlayController.Instance, "Scene mới chưa nạp xong");
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Playing), $"Scene không vào được Playing (đang {Gameplay.State})");
        Assert.IsTrue(grid != null && grid.IsGenerated, "Lưới bản đồ chưa dựng xong");

        Placement = Object.FindFirstObjectByType<PlacementManager>();
        // Đợi thêm vài frame cho các Start/LateUpdate (xếp nhà, dựng lưới) chạy xong
        for (int i = 0; i < 5; i++)
            yield return null;
    }

    /// <summary>Tua đúng 1 tháng bằng vòng thời gian thật của game (Update của GlobalSystemManager).</summary>
    protected IEnumerator AdvanceMonths(int months = 1)
    {
        for (int m = 0; m < months; m++)
        {
            int year = Data.currentYear;
            byte month = Data.currentMonth;
            if (Data.currentGameSpeed == GameSpeed.Pause)
                GlobalSystem.SetGameSpeed(GameSpeed.Speed1x);
            Data.timeTickProgress = 1f;

            int guard = 0;
            while (Data.currentYear == year && Data.currentMonth == month)
            {
                Assert.Less(guard++, MaxWaitFrames, "Tháng không trôi");
                yield return null;
            }

            // Một frame nữa để LateUpdate (thi thể, thắng/thua, xếp nhà) chạy
            yield return null;
            if (!Gameplay.IsIn(GamePlayStateID.Playing) && !Gameplay.IsIn(GamePlayStateID.Paused))
                yield break;   // Đã thắng / thua thì dừng tua
        }
    }

    /// <summary>Đặt công trình vào ô trống đầu tiên tìm được (đủ gỗ / vàng). Trả về id, 0 nếu thất bại.</summary>
    protected ushort PlaceAnywhere(BuildingType type)
    {
        Assert.IsNotNull(Placement, "Không thấy PlacementManager");
        Data.stockWood = Mathf.Max(Data.stockWood, 5000);
        Data.treasuryGold = Mathf.Max(Data.treasuryGold, 5000);

        GridController grid = Object.FindFirstObjectByType<GridController>();
        int w = grid != null ? grid.Width : 128;
        int h = grid != null ? grid.Height : 128;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var origin = new Vector2Int(x, y);
                if (!Placement.CheckAreaAvailable(origin, 3))
                    continue;
                if (Placement.PlaceAt(origin, type))
                    return Placement.GetBuildingIdAt(origin);
            }
        }
        return 0;
    }

    protected ref BuildingData BuildingById(ushort id)
    {
        int index = Contain.buildingManager.FindBuildingIndexById(id);
        Assert.GreaterOrEqual(index, 0, $"Không tìm thấy công trình {id}");
        return ref Contain.buildingManager.GetBuildingRef(index);
    }

    protected int CountBuildings(BuildingType type, bool activeOnly = false)
    {
        int n = 0;
        var bm = Contain.buildingManager;
        for (int i = 0; i < bm.activeCount; i++)
        {
            ref BuildingData b = ref bm.allBuildings[i];
            if (b.buildingType == type && b.isAllocated && (!activeOnly || b.buildingState == BuildingState.Active))
                n++;
        }
        return n;
    }

    protected int CountAliveResidents()
    {
        int n = 0;
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].isAlive)
                n++;
        return n;
    }

    /// <summary>Thay hàng chờ ở Cổng bằng đúng 1 người do test dựng.</summary>
    protected void SetSingleApplicant(ResidentData applicant)
    {
        var im = Contain.immigrationManager;
        im.waitingApplicants[0] = applicant;
        im.waitingCount = 1;
        im.OpenInspection();   // kẹp InspectIndex về 0
    }

    /// <summary>Đánh dấu đã nghiên cứu xong các công nghệ (giữ nguyên những cái đã có).</summary>
    protected void GrantResearch(params ResearchID[] ids)
    {
        ResearchSaveData save = Contain.researchManager.GetSaveData();
        foreach (ResearchID id in ids)
            save.completedMask |= 1UL << (int)id;
        Contain.researchManager.ApplySaveData(in save);
    }

    /// <summary>Đặt công trình rồi tua tháng tới khi xây xong (tối đa 12 tháng, luôn đủ lương thực để không thua).</summary>
    protected IEnumerator BuildAndComplete(BuildingType type, System.Action<ushort> onDone)
    {
        ushort id = PlaceAnywhere(type);
        Assert.AreNotEqual(0, id, $"Không đặt được {type}");
        for (int m = 0; m < 12 && BuildingById(id).buildingState != BuildingState.Active; m++)
        {
            Data.stockFood = Mathf.Max(Data.stockFood, 1000);
            yield return AdvanceMonths(1);
        }
        Assert.AreEqual(BuildingState.Active, BuildingById(id).buildingState, $"{type} không xây xong sau 12 tháng");
        onDone(id);
    }

    /// <summary>Chỉ số cư dân có residentID lớn nhất (người vừa được nhận vào).</summary>
    protected int NewestResidentIndex()
    {
        var rm = Contain.residentManager;
        int best = -1;
        for (int i = 0; i < rm.activeCount; i++)
            if (best < 0 || rm.allResidents[i].residentID > rm.allResidents[best].residentID)
                best = i;
        return best;
    }

    protected static ResidentData HealthyApplicant(byte wealth = 0)
    {
        ResidentData a = ImmigrationRules.CreateApplicant(9000);
        a.wealth = wealth;
        a.originRegion = OriginRegion.YellowZone;
        a.factionType = FactionType.Commoner;
        a.professionType = ProfessionType.Farmer;
        a.healthStatus = HealthStatus.Healthy;
        a.diseaseType = DiseaseType.None;
        a.IncubationMonths = 0;
        a.symptoms = SymptomFlags.None;
        a.bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature;
        return a;
    }
}
#endif
