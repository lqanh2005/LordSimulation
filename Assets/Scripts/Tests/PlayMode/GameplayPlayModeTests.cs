#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayMode: chạy GamePlayScene thật. Mỗi class ứng với một nhóm kịch bản trong Docs/KichBanTest.md.

// ===== SM / TM: khởi động & thời gian =====
public class PM_TimeTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator SM01_VanMoi_KhoiDongDung()
    {
        Assert.AreEqual(1, Data.currentYear);
        Assert.AreEqual(1, Data.currentMonth);
        Assert.Greater(Data.totalPopulation, 0, "Có dân khởi đầu");
        Assert.Greater(CountAliveResidents(), 0);
        Assert.Greater(Data.stockFood, 0);
        Assert.Greater(Data.stockWood, 0);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TM01_TickThang_TieuThuLuongThuc()
    {
        Data.stockFood = 1000;
        int pop = Data.totalPopulation;
        yield return AdvanceMonths(1);

        Assert.AreEqual(2, Data.currentMonth);
        // Tháng 2 là mùa xuân: ăn 1/người, có thể có thêm sản xuất -> kiểm bằng LastMonthNet thay vì số tuyệt đối
        Assert.AreEqual(Data.stockFood - (1000 + GlobalSystem.LastMonthNet.food), 0,
            "LastMonthNet phải khớp thay đổi kho trong tháng");
        Assert.Greater(pop, 0);
    }

    [UnityTest]
    public IEnumerator TM02_HetNam_SangNamMoi()
    {
        Data.currentMonth = 12;
        yield return AdvanceMonths(1);
        Assert.AreEqual(2, Data.currentYear);
        Assert.AreEqual(1, Data.currentMonth);
    }

    [UnityTest]
    public IEnumerator TM02_VaoMuaDong_NhietDoAm()
    {
        Data.currentMonth = 9;
        yield return AdvanceMonths(1);
        Assert.AreEqual(SeasonType.Winter, Data.CurrentSeason);
        Assert.Less(Data.environmentTemperature, 0f);
    }

    [UnityTest]
    public IEnumerator TM03_TamDung_ThoiGianKhongTroi()
    {
        GlobalSystem.SetGameSpeed(GameSpeed.Pause);
        Data.timeTickProgress = 0.999f;
        for (int i = 0; i < 30; i++)
            yield return null;

        Assert.AreEqual(1, Data.currentMonth);
        Assert.AreEqual(0.999f, Data.timeTickProgress, 1e-5f);
    }

    [UnityTest]
    public IEnumerator TM04_TocDo_HeSo()
    {
        GlobalSystem.SetGameSpeed(GameSpeed.Speed3x);
        Assert.AreEqual(3f, GlobalSystem.SpeedMultiplier);
        GlobalSystem.SetGameSpeed(GameSpeed.Speed2x);
        Assert.AreEqual(2f, GlobalSystem.SpeedMultiplier);
        GlobalSystem.SetGameSpeed(GameSpeed.Pause);
        Assert.AreEqual(0f, GlobalSystem.SpeedMultiplier);
        yield return null;
    }
}

// ===== GT: Cổng =====
public class PM_GateTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator GT04_ChoVao_TangDanSo()
    {
        int pop = Data.totalPopulation;
        int samples = Contain.residentManager.activeCount;
        SetSingleApplicant(HealthyApplicant());

        Contain.immigrationManager.AcceptCurrent();
        yield return null;

        Assert.AreEqual(pop + 1, Data.totalPopulation);
        Assert.AreEqual(samples + 1, Contain.residentManager.activeCount);
        Assert.AreEqual(0, Contain.immigrationManager.waitingCount);
    }

    [UnityTest]
    public IEnumerator GT05_TuChoiNguoiKhoe_TruUyTin()
    {
        Data.reputation = 50f;
        SetSingleApplicant(HealthyApplicant(wealth: 0));
        Contain.immigrationManager.DenyCurrent();
        yield return null;

        Assert.AreEqual(50f + StabilityRules.DenyReputation, Data.reputation, 1e-4f);
        Assert.AreEqual(0, Contain.immigrationManager.waitingCount);
    }

    [UnityTest]
    public IEnumerator GT06_TuChoiTreEm_TruHaiUyTin()
    {
        // GDD v0.3 mục 9.6: Deny −1, −2 nếu có trẻ em
        Data.reputation = 50f;
        ResidentData child = HealthyApplicant();
        child.age = 1;
        SetSingleApplicant(child);

        Contain.immigrationManager.DenyCurrent();
        yield return null;
        Assert.AreEqual(50f + StabilityRules.DenyChildReputation, Data.reputation, 1e-4f);
    }

    [UnityTest]
    public IEnumerator GT08_CachLyKhiKhongCoTram_BiChan()
    {
        if (CountBuildings(BuildingType.QuarantineWard) > 0)
            Assert.Ignore("Bản đồ khởi đầu đã có Trạm cách ly");

        int pop = Data.totalPopulation;
        SetSingleApplicant(HealthyApplicant());
        Contain.immigrationManager.QuarantineCurrent();
        yield return null;

        Assert.AreEqual(1, Contain.immigrationManager.waitingCount, "Người vẫn còn ở Cổng");
        Assert.AreEqual(pop, Data.totalPopulation);
    }

    [UnityTest]
    public IEnumerator GT03_NguoiToiMoiThang_KhongVuotHangCho()
    {
        for (int i = 0; i < 6; i++)
            yield return AdvanceMonths(1);
        Assert.LessOrEqual(Contain.immigrationManager.waitingCount, ImmigrationRules.MaxWaiting);
    }
}

// ===== BD: Xây dựng =====
public class PM_BuildingTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator BD02_DatNha_TruTaiNguyen()
    {
        Data.stockWood = 5000;
        Data.treasuryGold = 5000;
        int count = Contain.buildingManager.activeCount;

        ushort id = PlaceAnywhere(BuildingType.House);
        yield return null;

        Assert.AreNotEqual(0, id, "Không đặt được Nhà ở ô trống nào");
        Assert.AreEqual(count + 1, Contain.buildingManager.activeCount);
        Assert.IsTrue(Data.stockWood < 5000 || Data.treasuryGold < 5000, "Phải trừ gỗ hoặc vàng");
    }

    [UnityTest]
    public IEnumerator BD01_DatChong_BiChan()
    {
        ushort id = PlaceAnywhere(BuildingType.House);
        Assert.AreNotEqual(0, id);
        BuildingData b = BuildingById(id);
        var origin = new Vector2Int(b.coordX, b.coordY);
        int count = Contain.buildingManager.activeCount;

        Assert.IsFalse(Placement.PlaceAt(origin, BuildingType.House), "Không được xây chồng");
        Assert.AreEqual(count, Contain.buildingManager.activeCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BD02_ThieuTaiNguyen_BiChan()
    {
        ushort first = PlaceAnywhere(BuildingType.House);
        Assert.AreNotEqual(0, first);
        Data.stockWood = 0;
        Data.treasuryGold = 0;
        int count = Contain.buildingManager.activeCount;

        // Tìm ô trống rồi thử đặt khi không có tài nguyên
        bool placed = false;
        for (int y = 0; y < 128 && !placed; y++)
            for (int x = 0; x < 128 && !placed; x++)
            {
                var o = new Vector2Int(x, y);
                if (Placement.CheckAreaAvailable(o, 3))
                {
                    placed = Placement.PlaceAt(o, BuildingType.House);
                    break;
                }
            }

        Assert.IsFalse(placed);
        Assert.AreEqual(count, Contain.buildingManager.activeCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BD03_ThiCong_XongSauVaiThang()
    {
        ushort id = PlaceAnywhere(BuildingType.House);
        Assert.AreNotEqual(0, id);

        for (int m = 0; m < 12 && BuildingById(id).buildingState != BuildingState.Active; m++)
        {
            Data.stockFood = 1000;   // tránh đói làm thua giữa chừng
            yield return AdvanceMonths(1);
        }

        Assert.AreEqual(BuildingState.Active, BuildingById(id).buildingState, "Nhà không xây xong sau 12 tháng");
        Assert.IsTrue(BuildingById(id).isOperational);
    }

    [UnityTest]
    public IEnumerator BD06_PhaCongTrinh()
    {
        ushort id = PlaceAnywhere(BuildingType.House);
        Assert.AreNotEqual(0, id);
        int count = CountBuildings(BuildingType.House);

        Assert.IsTrue(Placement.DemolishBuilding(id));
        yield return null;
        Assert.AreEqual(count - 1, CountBuildings(BuildingType.House));
    }
}

// ===== RE / WA / CR: tài nguyên, nước, khủng hoảng =====
public class PM_SurvivalTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator RE02_HetLuongThuc_CoNguoiDoi()
    {
        Data.stockFood = 0;
        yield return AdvanceMonths(1);
        Assert.Greater(GlobalSystem.LastHungryRate, 0f);
        Assert.AreEqual(0, Data.stockFood, "Kho không xuống âm");
    }

    [UnityTest]
    public IEnumerator RE04_MuaDongHetThan_CoNguoiLanh()
    {
        Data.currentMonth = 9;
        Data.stockCoal = 0;
        Data.stockFood = 5000;
        Data.totalPopulation = 200;   // vượt sức sưởi của lửa trại
        yield return AdvanceMonths(1);

        Assert.AreEqual(SeasonType.Winter, Data.CurrentSeason);
        Assert.Greater(GlobalSystem.LastColdRate, 0f);
        Assert.AreEqual(0, Data.stockCoal);
    }

    [UnityTest]
    public IEnumerator RE03_MuaDong_TieuThuThan()
    {
        Data.currentMonth = 9;
        Data.stockCoal = 5000;
        Data.stockFood = 5000;
        yield return AdvanceMonths(1);
        Assert.AreEqual(0f, GlobalSystem.LastColdRate, 1e-4f, "Đủ than thì không ai lạnh");
    }

    [UnityTest]
    public IEnumerator WA02_DanVuotSucCapNuoc_BiKhat()
    {
        Data.stockFood = 5000;
        Data.totalPopulation = 300;
        yield return AdvanceMonths(1);
        Assert.Greater(GlobalSystem.LastThirstyRate, 0f);
        Assert.AreEqual(GlobalSystem.CurrentWaterCapacity, WaterRules.GetCapacity(CountBuildings(BuildingType.WaterWell, true)),
            "Sức cấp nước = 10 + 15 x giếng đang hoạt động");
    }

    [UnityTest]
    public IEnumerator CR03_XacKhongThuGom_SinhMiasma()
    {
        Data.stockFood = 5000;
        CrisisManager.AddCorpses(80);
        yield return null;
        yield return AdvanceMonths(2);

        Assert.Greater(Data.corpsesRotting + Data.corpsesFresh, 0, "Không thể thu gom hết 80 xác trong 2 tháng");
        Assert.Greater(Data.miasma, 0f);
    }
}

// ===== ED / RC / MC =====
public class PM_SystemsTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator ED01_SacLenhBiKhoaDenKhiCoBoLuat()
    {
        Assert.IsFalse(EdictRules.EdictsUnlocked, "Ván mới chưa có Bộ luật");

        var save = Contain.researchManager.GetSaveData();
        save.completedMask |= 1UL << (int)ResearchID.CodeOfLaw;
        Contain.researchManager.ApplySaveData(in save);
        yield return null;

        Assert.IsTrue(EdictRules.EdictsUnlocked);
    }

    [UnityTest]
    public IEnumerator GT13_ThanNhietBiKhoaDenKhiCoNhietKe()
    {
        Assert.IsFalse(EdictRules.IsUnlocked(RuleVariable.ApplicantBodyTemp));
        var save = Contain.researchManager.GetSaveData();
        save.completedMask |= 1UL << (int)ResearchID.Thermometer;
        Contain.researchManager.ApplySaveData(in save);
        yield return null;
        Assert.IsTrue(EdictRules.IsUnlocked(RuleVariable.ApplicantBodyTemp));
    }

    [UnityTest]
    public IEnumerator RC03_BatDauNghienCuu_TruVang()
    {
        Data.treasuryGold = 1000;
        int cost = ResearchCatalog.GetGoldCost(ResearchCatalog.Get(ResearchID.CropRotation));

        Assert.IsTrue(Contain.researchManager.StartResearch(ResearchID.CropRotation));
        yield return null;
        Assert.AreEqual(1000 - cost, Data.treasuryGold);
    }

    [UnityTest]
    public IEnumerator RC03_ThieuVang_KhongBatDauDuoc()
    {
        Data.treasuryGold = 0;
        Assert.IsFalse(Contain.researchManager.StartResearch(ResearchID.CropRotation));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RC02_ThieuTienQuyet_KhongBatDauDuoc()
    {
        Data.treasuryGold = 10000;
        Assert.IsFalse(Contain.researchManager.StartResearch(ResearchID.Herbalism), "Thảo dược cần Luân canh trước");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MC03_MuaThanTuThuongNhan()
    {
        Contain.merchantManager.ApplySaveData(new MerchantSaveData
        {
            isPresent = true, monthsLeft = 2, budget = 100,
            foodStock = 50, coalStock = 50, woodStock = 50,
            foodPrice = 2, coalPrice = 3, woodPrice = 2
        });
        Data.treasuryGold = 100;
        int coal = Data.stockCoal;
        int price = Contain.merchantManager.GetBuyPrice(ResourceType.Coal);
        Assert.AreEqual(UnityEngine.Mathf.CeilToInt(MarketSystem.GetPrice(ResourceType.Coal, in Data) * 1.25f - 0.001f), price,
            "GDD 6.6: thương nhân bán ×1,25 giá thị trường");

        int bought = Contain.merchantManager.Buy(ResourceType.Coal, 10, out string error);
        yield return null;

        Assert.IsNull(error);
        Assert.AreEqual(10, bought);
        Assert.AreEqual(coal + 10, Data.stockCoal);
        Assert.AreEqual(100 - 10 * price, Data.treasuryGold);
        Assert.AreEqual(40, Contain.merchantManager.GetMerchantStock(ResourceType.Coal));
    }

    [UnityTest]
    public IEnumerator MC03_KhongDuVang_MuaDuocItHon()
    {
        Contain.merchantManager.ApplySaveData(new MerchantSaveData
        {
            isPresent = true, monthsLeft = 2, budget = 100,
            coalStock = 50, coalPrice = 3, foodPrice = 2, woodPrice = 2
        });
        Data.treasuryGold = 10;
        int price = Contain.merchantManager.GetBuyPrice(ResourceType.Coal);
        int bought = Contain.merchantManager.Buy(ResourceType.Coal, 10, out _);
        yield return null;
        Assert.AreEqual(10 / price, bought);
        Assert.AreEqual(10 - (10 / price) * price, Data.treasuryGold);
    }

    [UnityTest]
    public IEnumerator MC01_ThuongNhanVang_KhongMuaDuoc()
    {
        Contain.merchantManager.ApplySaveData(default);
        Assert.AreEqual(0, Contain.merchantManager.Buy(ResourceType.Food, 5, out string error));
        Assert.IsNotNull(error);
        yield return null;
    }
}

// ===== GO: Thắng / Thua =====
public class PM_GameOverTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator GO03_HetDan_Thua()
    {
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            rm.allResidents[i].isAlive = false;
        Data.totalPopulation = 0;

        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.GameOver), $"Đang {Gameplay.State}");
        Assert.AreEqual(GameOverReason.Extinction, Gameplay.LastGameOverReason);
    }

    [UnityTest]
    public IEnumerator GO04_BaoLoan100_Thua()
    {
        Data.riotRiskMeter = 100f;
        Data.stockFood = 0;   // đói giữ bạo loạn ở trần
        // GDD 9.6: Riot theo Happiness (tiến dần 30%/tháng) — dân đang rất bất mãn thì Riot không tự hạ trong 1 tháng
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            rm.allResidents[i].happiness = 0.1f;
        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.GameOver), $"Đang {Gameplay.State}, bạo loạn {Data.riotRiskMeter}");
        Assert.AreEqual(GameOverReason.Riot, Gameplay.LastGameOverReason);
    }

    [UnityTest]
    public IEnumerator GO01_QuaMuaDong_DuDan_Thang()
    {
        Data.currentYear = 2;
        Data.currentMonth = 1;
        Data.totalPopulation = 60;
        Data.stockFood = 5000;
        Data.stockCoal = 5000;
        Data.riotRiskMeter = 0f;
        Data.victoryAchieved = false;

        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Victory), $"Đang {Gameplay.State}");
        Assert.IsTrue(Data.victoryAchieved);
    }

    [UnityTest]
    public IEnumerator GO02_QuaMuaDong_ThieuDan_KhongThang()
    {
        Data.currentYear = 2;
        Data.currentMonth = 1;
        Data.stockFood = 5000;
        Data.stockCoal = 5000;
        Data.riotRiskMeter = 0f;
        if (Data.totalPopulation >= 20)
            Assert.Ignore("Dân khởi đầu đã >= 20");

        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Playing), $"Đang {Gameplay.State}");
    }
}

// ===== SV: Lưu / Tải bằng SaveLoadManager thật (thư mục tạm, không đụng save của người chơi) =====
public class PM_SaveLoadTests : GameSessionFixture
{
    private string _dir;
    private GameObject _saveGo;

    [SetUp]
    public void UseTempSaveDirectory()
    {
        _dir = Path.Combine(Application.temporaryCachePath, "PlayModeTestSaves");
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
        Directory.CreateDirectory(_dir);
        SaveLoadManager.DirectoryOverride = _dir;
    }

    [TearDown]
    public void RestoreSaveDirectory()
    {
        SaveLoadManager.DirectoryOverride = null;
        if (_saveGo != null)
            Object.Destroy(_saveGo);
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private SaveLoadManager CreateSaveManager()
    {
        _saveGo = new GameObject("TestSaveLoad");
        var sl = _saveGo.AddComponent<SaveLoadManager>();
        sl.Init();
        sl.SetActiveSlot(0);
        return sl;
    }

    [UnityTest]
    public IEnumerator SV01_LuuRoiTai_GiuNguyen()
    {
        var sl = CreateSaveManager();
        Data.treasuryGold = 777;
        Data.stockCoal = 321;
        Data.currentMonth = 7;
        Data.reputation = 12.5f;
        ushort houseId = PlaceAnywhere(BuildingType.House);
        Data.treasuryGold = 777;
        int residents = Contain.residentManager.activeCount;
        int buildings = Contain.buildingManager.activeCount;

        sl.SaveGame();
        yield return null;
        Assert.IsTrue(sl.HasSaveFile(), "Không ghi được file");
        Assert.IsTrue(sl.GetSlotInfo(0).exists);

        // Làm bẩn trạng thái rồi tải lại
        Data.treasuryGold = 1;
        Data.stockCoal = 0;
        Data.currentMonth = 1;
        Placement.DemolishBuilding(houseId);
        yield return null;

        Assert.IsTrue(sl.LoadGame());
        yield return null;

        Assert.AreEqual(777, Data.treasuryGold);
        Assert.AreEqual(321, Data.stockCoal);
        Assert.AreEqual(7, Data.currentMonth);
        Assert.AreEqual(12.5f, Data.reputation, 1e-4f);
        Assert.AreEqual(residents, Contain.residentManager.activeCount);
        Assert.AreEqual(buildings, Contain.buildingManager.activeCount);
        Assert.GreaterOrEqual(Contain.buildingManager.FindBuildingIndexById(houseId), 0, "Nhà đã lưu phải quay lại");
    }

    /// <summary>Giữ lại log lỗi trong bộ nhớ (không in ra Console) để test kiểm tra.</summary>
    private sealed class CaptureLogHandler : ILogHandler
    {
        public readonly System.Text.StringBuilder Errors = new System.Text.StringBuilder();

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (logType == LogType.Error || logType == LogType.Exception)
                Errors.AppendLine(args != null && args.Length > 0 ? string.Format(format, args) : format);
        }

        public void LogException(System.Exception exception, Object context) => Errors.AppendLine(exception.Message);
    }

    [UnityTest]
    public IEnumerator SV05_FileHong_KhongLamBanVanDangChoi()
    {
        var sl = CreateSaveManager();
        File.WriteAllBytes(sl.GetSlotPath(0), new byte[] { 1, 2, 3, 4, 5 });
        Data.treasuryGold = 555;

        // File hỏng -> LoadGame PHẢI ghi lỗi (hành vi mong muốn). Bắt lại log thay vì in ra Console cho sạch.
        var capture = new CaptureLogHandler();
        ILogHandler original = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = capture;
        bool ok;
        try { ok = sl.LoadGame(); }
        finally { Debug.unityLogger.logHandler = original; }
        yield return null;

        Assert.IsFalse(ok);
        StringAssert.Contains("[SaveLoadManager] Lỗi nạp game", capture.Errors.ToString(), "Phải báo lỗi khi file hỏng");
        Assert.AreEqual(555, Data.treasuryGold, "Dữ liệu đang chơi không bị ghi đè");
    }
}
#endif
