#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

// PlayMode – xếp nhà/việc, dịch bệnh, bão tuyết, thi thể, thương nhân, thắng, phím tắt, UI, hiệu năng.

// ===== Xếp nhà / việc / sản xuất (RD-01, RD-02, PR-01) =====
public class PM_AssignmentTests : GameSessionFixture
{
    private int CountHoused()
    {
        int n = 0;
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].isAlive && rm.allResidents[i].assignedHouseID >= 0)
                n++;
        return n;
    }

    [UnityTest]
    public IEnumerator RD01_XayNhaXong_NguoiVoGiaCuDuocXep()
    {
        int before = CountHoused();
        ushort house = 0;
        yield return BuildAndComplete(BuildingType.House, id => house = id);
        yield return null;
        yield return null;

        Assert.Greater(CountHoused(), before, "Có nhà mới mà không ai được xếp vào");
        Assert.Greater(BuildingById(house).currentOccupancy, 0);
    }

    [UnityTest]
    public IEnumerator RD02_PR01_TraiGo_CoNguoiLam_RaGo()
    {
        // Mọi người lớn làm tiều phu để chắc chắn có người hợp nghề
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].GetAgeGroup() == AgeGroupType.Adult)
                rm.allResidents[i].professionType = ProfessionType.Lumberjack;

        ushort camp = 0;
        yield return BuildAndComplete(BuildingType.LumberCamp, id => camp = id);
        Contain.residentManager.RequestReassign();
        yield return null;
        yield return null;
        Assert.Greater(BuildingById(camp).currentWorkers, 0, "Trại gỗ không có ai làm");

        Data.stockFood = 1000;
        Data.currentMonth = 3;
        yield return AdvanceMonths(1);
        Assert.Greater(GlobalSystem.LastMonthNet.wood, 0, "Có tiều phu mà không ra gỗ");
    }

    [UnityTest]
    public IEnumerator BD07_TatCongTrinh_GiaiPhongCongNhan()
    {
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].GetAgeGroup() == AgeGroupType.Adult)
                rm.allResidents[i].professionType = ProfessionType.Lumberjack;

        ushort camp = 0;
        yield return BuildAndComplete(BuildingType.LumberCamp, id => camp = id);
        Contain.residentManager.RequestReassign();
        yield return null;
        yield return null;
        Assert.Greater(BuildingById(camp).currentWorkers, 0);

        BuildingById(camp).isOperational = false;
        Contain.residentManager.RequestReassign();
        yield return null;
        yield return null;
        Assert.AreEqual(0, BuildingById(camp).currentWorkers, "Tạm dừng thì công nhân phải được giải phóng");
    }
}

// ===== Dịch bệnh (DS-01) =====
public class PM_DiseaseTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator DS01_UBenh_SauMotThang_PhatBenh_RoiKetThuc()
    {
        int id = InfectFirstResident(DiseaseType.RedFever);
        Data.stockFood = 1000;
        Data.stockMedicine = 50;

        yield return AdvanceMonths(1);
        ResidentData r = FindById(id);
        if (r.isAlive)
        {
            Assert.AreNotEqual(HealthStatus.Incubating, r.healthStatus, "Sốt Đỏ ủ 1 tháng rồi phải phát bệnh");
            if (r.healthStatus == HealthStatus.ActiveInfected)
                Assert.Greater(r.bodyTemperature, 38f);
        }

        // Bệnh không kéo dài mãi: trong 8 tháng phải khỏi hoặc tử vong
        for (int m = 0; m < 8 && FindById(id).isAlive && FindById(id).healthStatus != HealthStatus.Healthy; m++)
        {
            Data.stockFood = 1000;
            Data.stockMedicine = 50;
            yield return AdvanceMonths(1);
            if (!Gameplay.IsIn(GamePlayStateID.Playing)) Assert.Ignore("Ván kết thúc giữa chừng");
        }

        r = FindById(id);
        Assert.IsTrue(!r.isAlive || r.healthStatus == HealthStatus.Healthy, $"Sau 9 tháng vẫn {r.healthStatus}");
    }

    private int InfectFirstResident(DiseaseType type)
    {
        ref ResidentData first = ref Contain.residentManager.GetResidentRef(0);
        ResidentDiseaseRules.Infect(ref first, type);
        return first.residentID;
    }

    private ResidentData FindById(int id)
    {
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].residentID == id)
                return rm.allResidents[i];
        return default;
    }
}

// ===== Bão tuyết (CR-05, CR-06) =====
public class PM_BlizzardTests : GameSessionFixture
{
    private void SetBlizzardChance(float chance)
    {
        FieldInfo f = typeof(GlobalSystemManager).GetField("blizzardChancePerWinterMonth", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, "Không tìm thấy trường blizzardChancePerWinterMonth");
        f.SetValue(GlobalSystem, chance);
    }

    [UnityTest]
    public IEnumerator CR05_BaoTuyet_ChiMuaDong_LanhHon_TonThanGapDoi()
    {
        SetBlizzardChance(1f);
        Data.stockFood = 5000;
        Data.stockCoal = 5000;

        Data.currentMonth = 5;
        yield return AdvanceMonths(1);
        Assert.AreEqual(WeatherEvent.Clear, Data.currentWeather, "Mùa hạ không có bão tuyết");

        // GDD 9.2: bão tuyết luôn được báo trước 1 tháng -> tháng 10 có dự báo, tháng 11 mới có bão
        Data.currentMonth = 9;
        yield return AdvanceMonths(1);
        Assert.AreEqual(SeasonType.Winter, Data.CurrentSeason);
        Assert.AreEqual(WeatherEvent.Clear, Data.currentWeather, "Chưa báo trước thì chưa có bão");
        Assert.IsTrue(Data.blizzardForecast, "Phải báo trước bão cho tháng sau");

        int pop = Data.totalPopulation;
        yield return AdvanceMonths(1);
        Assert.AreEqual(WeatherEvent.Blizzard, Data.currentWeather);
        Assert.AreEqual(MarketSystem.BlizzardMagnitude, MarketSystem.GetEventMultiplier(ResourceType.Coal, in Data), 1e-4f,
            "GDD 6.6: bão tuyết -> giá than ×2");
        Assert.AreEqual(-30f, Data.environmentTemperature, 1e-3f);
        Assert.AreEqual(-2 * pop, GlobalSystem.LastMonthNet.coal, "Bão tuyết: 2 than/người/tháng");
    }

    [UnityTest]
    public IEnumerator CR06_HetMuaDong_BaoTan()
    {
        SetBlizzardChance(1f);
        Data.stockFood = 5000;
        Data.stockCoal = 5000;
        Data.currentMonth = 12;
        Data.currentWeather = WeatherEvent.Blizzard;
        Data.weatherDuration = 3;

        yield return AdvanceMonths(1);
        Assert.AreEqual(SeasonType.Spring, Data.CurrentSeason);
        Assert.AreEqual(WeatherEvent.Clear, Data.currentWeather);
        Assert.Greater(Data.environmentTemperature, 0f);
    }
}

// ===== Thu gom thi thể (CR-02) =====
public class PM_CorpseTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator CR02_LinhGacThuGom4XacMoiNguoi()
    {
        CrisisManager crisis = Contain.crisisManager;
        Assert.AreEqual(crisis.BaseCollectPerMonth, crisis.TotalCollectPerMonth, "Ván mới chỉ có sức thu gom nền");

        var post = new BuildingData
        {
            buildingType = BuildingType.GuardPost, buildingState = BuildingState.Active, isOperational = true, currentWorkers = 2
        };
        Assert.AreEqual(8, crisis.GetCollectRate(in post), "GDD v0.3 mục 7.4: 4 thi thể / người / tháng");

        post.isOperational = false;
        Assert.AreEqual(0, crisis.GetCollectRate(in post), "Tạm dừng thì không thu gom");

        var clinic = new BuildingData
        {
            buildingType = BuildingType.Clinic, buildingState = BuildingState.Active, isOperational = true, currentWorkers = 2
        };
        Assert.AreEqual(0, crisis.GetCollectRate(in clinic), "GDD v0.3: chỉ Corpse Collection Post (Trạm gác) thu gom");
        yield return null;
    }

    [UnityTest]
    public IEnumerator CR01_XacKhongThuGom_SangThangSauThanhXacThoi()
    {
        Data.stockFood = 5000;
        CrisisManager.AddCorpses(10);
        yield return AdvanceMonths(1);
        int collect = Contain.crisisManager.TotalCollectPerMonth;
        Assert.AreEqual(Mathf.Max(0, 10 - collect), Data.corpsesFresh + Data.corpsesRotting, "Thu gom đúng sức chứa");

        yield return AdvanceMonths(1);
        Assert.Greater(Data.corpsesRotting, 0, "Xác để quá 1 tháng phải chuyển sang phân hủy");
    }
}

// ===== Thương nhân (MC-01, MC-02) + Thắng (GO-05) =====
public class PM_ScheduleTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator MC01_VangLau_ChacChanToi_O2ThangRoiDi()
    {
        Contain.merchantManager.ApplySaveData(new MerchantSaveData { isPresent = false, monthsSinceVisit = 3 });
        Data.stockFood = 1000;

        yield return AdvanceMonths(1);
        Assert.IsTrue(Contain.merchantManager.IsPresent, "Vắng đủ 4 tháng thì chắc chắn ghé");
        Assert.That(Contain.merchantManager.GetMerchantStock(ResourceType.Coal), Is.InRange(30, 80));

        yield return AdvanceMonths(1);
        Assert.IsTrue(Contain.merchantManager.IsPresent, "Còn ở tháng thứ 2");
        yield return AdvanceMonths(1);
        Assert.IsFalse(Contain.merchantManager.IsPresent, "Ở 2 tháng rồi đi");
    }

    [UnityTest]
    public IEnumerator GO05_DaThang_KhongHienLaiPopupThang()
    {
        Data.currentYear = 3;
        Data.totalPopulation = 60;
        Data.stockFood = 5000;
        Data.stockCoal = 5000;
        Data.riotRiskMeter = 0f;
        Data.victoryAchieved = true;

        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Playing), $"Đã thắng trước đó mà lại vào {Gameplay.State}");
    }

    [UnityTest]
    public IEnumerator GO_ChoiTiepSauKhiThang()
    {
        Data.currentYear = 2;
        Data.totalPopulation = 60;
        Data.stockFood = 5000;
        Data.stockCoal = 5000;
        Data.riotRiskMeter = 0f;
        yield return AdvanceMonths(1);
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Victory));
        Assert.AreEqual(GameSpeed.Pause, Data.currentGameSpeed, "Màn thắng phải dừng game");

        Gameplay.ContinueAfterVictory();
        yield return null;
        Assert.IsTrue(Gameplay.IsIn(GamePlayStateID.Playing));
        Assert.AreNotEqual(GameSpeed.Pause, Data.currentGameSpeed, "Chơi tiếp phải chạy lại thời gian");
    }
}

// ===== Thông báo (UI-01, WA-02) =====
public class PM_NotificationTests : GameSessionFixture
{
    private static bool HistoryContains(string text, NoticeLevel level)
    {
        if (NotificationFeed.Instance == null) return false;
        foreach (var e in NotificationFeed.Instance.History)
            if (e.level == level && e.message != null && e.message.Contains(text))
                return true;
        return false;
    }

    [UnityTest]
    public IEnumerator UI01_ThongBao_LuuLichSu_VaClickChayDuoc()
    {
        Assert.IsNotNull(NotificationFeed.Instance, "Chưa có NotificationFeed");
        bool clicked = false;
        NotificationFeed.Show("Thông báo thử nghiệm", NoticeLevel.Warning, "pm-test", () => clicked = true);
        yield return null;

        Assert.IsTrue(HistoryContains("Thông báo thử nghiệm", NoticeLevel.Warning));
        foreach (var e in NotificationFeed.Instance.History)
            if (e.message == "Thông báo thử nghiệm") e.onClick?.Invoke();
        Assert.IsTrue(clicked, "Bấm vào thông báo phải gọi hành động đi tới đối tượng");
    }

    [UnityTest]
    public IEnumerator WA02_ThieuNuocNang_ThongBaoCritical()
    {
        Data.stockFood = 5000;
        Data.totalPopulation = 300;   // 0 giếng -> khát ~97%
        yield return AdvanceMonths(1);
        yield return null;
        Assert.IsTrue(HistoryContains("nước", NoticeLevel.Critical), "Thiếu nước >= 34% phải báo mức Critical");
    }
}

// ===== Phím tắt (UI-04, UI-05) bằng bàn phím ảo của Input System =====
public class PM_HotkeyTests : GameSessionFixture
{
    private Keyboard _kb;
    private InputSettings _originalSettings;

    [UnitySetUp]
    public IEnumerator AddVirtualKeyboard()
    {
        // Cho phím ảo vào game kể cả khi Game view không được focus (chỉ trong test, bản sao settings)
        _originalSettings = InputSystem.settings;
        InputSettings copy = Object.Instantiate(_originalSettings);
        copy.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        copy.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = copy;

        _kb = InputSystem.AddDevice<Keyboard>("PlayModeTestKeyboard");
        _kb.MakeCurrent();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RemoveVirtualKeyboard()
    {
        if (_kb != null && _kb.added)
            InputSystem.RemoveDevice(_kb);
        if (_originalSettings != null)
        {
            InputSettings temp = InputSystem.settings;
            InputSystem.settings = _originalSettings;
            if (temp != _originalSettings) Object.Destroy(temp);
        }
        yield return null;
    }

    private IEnumerator Press(Key key)
    {
        _kb.MakeCurrent();
        InputSystem.QueueStateEvent(_kb, new KeyboardState(key));
        yield return null;   // frame có wasPressedThisFrame
        InputSystem.QueueStateEvent(_kb, new KeyboardState());
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator UI04_Space_TamDungVaChayLai()
    {
        Assert.AreEqual(GameSpeed.Speed1x, Data.currentGameSpeed);
        yield return Press(Key.Space);
        Assert.AreEqual(GameSpeed.Pause, Data.currentGameSpeed, "Space phải tạm dừng");
        yield return Press(Key.Space);
        Assert.AreNotEqual(GameSpeed.Pause, Data.currentGameSpeed, "Space lần nữa phải chạy lại");
    }

    [UnityTest]
    public IEnumerator UI04_Tab_DoiTocDo()
    {
        GlobalSystem.SetGameSpeed(GameSpeed.Speed1x);
        yield return Press(Key.Tab);
        Assert.AreEqual(GameSpeed.Speed2x, Data.currentGameSpeed);
        yield return Press(Key.Tab);
        Assert.AreEqual(GameSpeed.Speed3x, Data.currentGameSpeed);
        yield return Press(Key.Tab);
        Assert.AreEqual(GameSpeed.Speed1x, Data.currentGameSpeed);
    }

    [UnityTest]
    public IEnumerator UI04_G_MoCong_Esc_Dong()
    {
        var gate = Object.FindFirstObjectByType<ImmigrationPopupUI>(FindObjectsInactive.Include);
        Assert.IsNotNull(gate);
        yield return Press(Key.G);
        yield return new WaitForSecondsRealtime(0.4f);   // hiệu ứng mở popup
        Assert.IsTrue(gate.IsOpen, "G phải mở Cổng");

        yield return Press(Key.Escape);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse(gate.IsOpen, "Esc phải đóng cửa sổ trên cùng");
    }

    [UnityTest]
    public IEnumerator UI04_T_MoCayNghienCuu()
    {
        var research = Object.FindFirstObjectByType<ResearchTreePopupController>(FindObjectsInactive.Include);
        Assert.IsNotNull(research);
        yield return Press(Key.T);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsTrue(UIPopupAnim.IsVisible(research.gameObject), "T phải mở cây nghiên cứu");
        yield return Press(Key.T);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse(UIPopupAnim.IsVisible(research.gameObject), "T lần nữa phải đóng");
    }

    [UnityTest]
    public IEnumerator UI04_Delete_BatTatCheDoPha()
    {
        Assert.IsFalse(Placement.IsDemolishing);
        yield return Press(Key.Delete);
        Assert.IsTrue(Placement.IsDemolishing, "Delete phải bật chế độ phá");
        yield return Press(Key.Delete);
        Assert.IsFalse(Placement.IsDemolishing);
    }

    [UnityTest]
    public IEnumerator UI05_Esc_KhongCoCuaSo_MoPauseMenu()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        Assert.IsNotNull(pause);
        yield return Press(Key.Escape);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsTrue(pause.IsOpen, "Esc khi không có cửa sổ nào phải mở Pause Menu");

        yield return Press(Key.Escape);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse(pause.IsOpen, "Esc lần nữa phải đóng Pause Menu");
    }
}

// ===== Hiệu năng (PF) =====
public class PM_PerformanceTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator PF02_ThanhNhieuCongTrinh_KhongGiat()
    {
        BuildingType[] types = { BuildingType.House, BuildingType.LumberCamp, BuildingType.WaterWell, BuildingType.Farm };
        int placed = 0;
        for (int i = 0; i < 60; i++)
            if (PlaceAnywhere(types[i % types.Length]) != 0)
                placed++;
        Assert.Greater(placed, 20, "Bản đồ không đủ chỗ cho bài thử hiệu năng");

        for (int i = 0; i < 10; i++)
            yield return null;

        // Thời gian frame trung bình khi chạy bình thường
        float start = Time.realtimeSinceStartup;
        const int frames = 120;
        for (int i = 0; i < frames; i++)
            yield return null;
        float avgMs = (Time.realtimeSinceStartup - start) * 1000f / frames;

        // Thời gian của frame mô phỏng tháng
        Data.stockFood = 5000;
        float tickStart = Time.realtimeSinceStartup;
        yield return AdvanceMonths(1);
        float tickMs = (Time.realtimeSinceStartup - tickStart) * 1000f;

        Debug.Log($"[PF02] {placed} công trình: frame TB {avgMs:0.0} ms, tick tháng {tickMs:0.0} ms");
        Assert.Less(avgMs, 50f, "Frame trung bình > 50 ms (< 20 FPS) trong Editor");
        Assert.Less(tickMs, 500f, "Mô phỏng 1 tháng mất > 0.5 s");
    }
}
#endif
