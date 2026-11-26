#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Công cụ test tải cư dân (chỉ trong Editor), gọi từ các nút Odin trên GamePlayController.
/// Cư dân được tạo bằng đúng luật nhập cư rồi AddResident như luồng game thật.
/// </summary>
public static class ResidentStressTest
{
    /// <summary>ID cư dân test bắt đầu từ đây để xoá riêng được, không đụng dân thật.</summary>
    public const int TestIdBase = 1000000;

    private const string TickMarker = "ResidentManager.TickVisualCommutes";
    private const string PathMarker = "ResidentManager.AssignPath";
    private const int MaxExamples = 3;

    private static readonly BuildingType[] TownTypes =
    {
        BuildingType.House, BuildingType.LargeHouse, BuildingType.Tent, BuildingType.Farm, BuildingType.Mine,
        BuildingType.LumberCamp, BuildingType.Clinic, BuildingType.School, BuildingType.GuardPost,
        BuildingType.Furnace, BuildingType.QuarantineWard
    };

    // ==========================================
    // SPAWN / XOÁ
    // ==========================================

    public static int Spawn(PlayerContain contain, int count, bool mixedAges, bool healthy)
    {
        ResidentManager rm = contain.residentManager;
        int nextId = Mathf.Max(TestIdBase, rm.PeekMaxResidentId() + 1);
        int added = 0;
        count = Mathf.Min(count, ResidentManager.MAX_RESIDENTS - rm.activeCount);

        for (int i = 0; i < count; i++)
        {
            ResidentData r = ImmigrationRules.CreateApplicant(nextId++);
            if (mixedAges)
            {
                // 20% trẻ em, 70% người lớn, 10% người già
                float roll = Random.value;
                r.age = (byte)(roll < 0.2f ? Random.Range(1, 18) : roll < 0.9f ? Random.Range(18, 65) : Random.Range(65, 81));
                r.professionType = ResidentProfessionRules.GetInitialProfession(in r);
            }
            if (healthy)
            {
                r.healthStatus = HealthStatus.Healthy;
                r.diseaseType = DiseaseType.None;
                r.IncubationMonths = 0;
                r.symptoms = SymptomFlags.None;
                r.bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature;
            }
            if (rm.AddResident(in r) >= 0)
                added++;
        }

        contain.globalSystemManager.GetGlobalDataRef().totalPopulation += added;
        rm.NotifyPopulationChanged();
        return added;
    }

    public static int Clear(PlayerContain contain)
    {
        ResidentManager rm = contain.residentManager;
        int removed = 0;
        for (int i = 0; i < rm.activeCount; i++)
        {
            ref ResidentData r = ref rm.allResidents[i];
            if (!r.isAlive || r.residentID < TestIdBase)
                continue;
            r.isAlive = false;
            removed++;
        }

        ref GlobalSystemData g = ref contain.globalSystemManager.GetGlobalDataRef();
        g.totalPopulation = Mathf.Max(0, g.totalPopulation - removed);
        rm.NotifyPopulationChanged();
        Reassign(contain);
        return removed;
    }

    public static void Reassign(PlayerContain contain)
    {
        ResidentManager rm = contain.residentManager;
        BuildingManager bm = contain.buildingManager;
        ResidentAssignmentSystem.ProcessMonthly(rm.allResidents, rm.activeCount, bm.allBuildings, bm.activeCount);
    }

    /// <summary>Đặt mỗi loại nhà / chỗ làm vài cái vào ô trống và cho xây xong ngay, qua đúng PlacementManager.</summary>
    public static string BuildTown(PlayerContain contain, int perType)
    {
        PlacementManager placement = Object.FindFirstObjectByType<PlacementManager>();
        GridController grid = contain.residentManager.GridController;
        if (placement == null || grid == null)
            return "Không tìm thấy PlacementManager / GridController";

        ref GlobalSystemData g = ref contain.globalSystemManager.GetGlobalDataRef();
        g.stockWood += 100000;
        g.treasuryGold += 100000;

        var sb = new StringBuilder("Đã đặt: ");
        var skipped = new StringBuilder();
        foreach (BuildingType type in TownTypes)
        {
            int placed = 0;
            for (int n = 0; n < perType && PlaceInFreeSpot(placement, grid, type); n++)
                placed++;
            if (placed > 0)
                sb.Append(type).Append(" x").Append(placed).Append(", ");
            else
                skipped.Append(type).Append(' ');
        }

        for (int m = 0; m < 6; m++)
            contain.buildingManager.ProcessMonthlyConstruction();
        Reassign(contain);
        if (skipped.Length > 0)
            sb.Append("| không đặt được (thiếu cấu hình / chưa nghiên cứu / hết chỗ): ").Append(skipped);
        return sb.ToString();
    }

    private static bool PlaceInFreeSpot(PlacementManager placement, GridController grid, BuildingType type)
    {
        BuildingConfig config = placement.database != null ? placement.database.GetConfig(type) : null;
        if (config == null || !placement.IsResearched(type) || placement.IsUniqueLimitReached(type))
            return false;

        // Chừa 1 ô quanh công trình để cư dân còn lối đi
        Vector2Int size = config.Footprint + new Vector2Int(2, 2);
        for (int y = 0; y + size.y <= grid.Height; y++)
            for (int x = 0; x + size.x <= grid.Width; x++)
                if (placement.CheckAreaAvailable(new Vector2Int(x, y), size))
                    return placement.PlaceAt(new Vector2Int(x + 1, y + 1), type);
        return false;
    }

    // ==========================================
    // KIỂM TRA LOGIC
    // ==========================================

    private sealed class Report
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<string>> _examples = new Dictionary<string, List<string>>();

        public int ErrorCount => _counts.Count;

        public void Error(string rule, string example)
        {
            _counts.TryGetValue(rule, out int n);
            _counts[rule] = n + 1;
            if (!_examples.TryGetValue(rule, out List<string> list))
                _examples[rule] = list = new List<string>();
            if (list.Count < MaxExamples)
                list.Add(example);
        }

        public void Info(string line) => _sb.AppendLine(line);

        public string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine(ErrorCount == 0 ? "[Test cư dân] Logic ĐẠT" : $"[Test cư dân] Có {ErrorCount} loại SAI LOGIC");
            foreach (var pair in _counts)
                sb.AppendLine($"  SAI x{pair.Value}: {pair.Key} (vd: {string.Join("; ", _examples[pair.Key])})");
            sb.Append(_sb);
            return sb.ToString();
        }
    }

    public static string Validate(PlayerContain contain, out int errors)
    {
        var report = new Report();
        ResidentManager rm = contain.residentManager;
        BuildingManager bm = contain.buildingManager;
        ref GlobalSystemData g = ref contain.globalSystemManager.GetGlobalDataRef();

        int alive = 0, homeless = 0, housed = 0, working = 0, studying = 0, unemployed = 0, sick = 0;
        var ids = new HashSet<int>();
        var occupancy = new int[BuildingManager.MAX_BUILDINGS];
        var workers = new int[BuildingManager.MAX_BUILDINGS];

        for (int i = 0; i < rm.activeCount; i++)
        {
            ref ResidentData r = ref rm.allResidents[i];
            string who = $"id {r.residentID}";
            if (!r.isAlive)
            {
                report.Error("Người chết vẫn nằm trong danh sách sống", who);
                continue;
            }

            alive++;
            if (!ids.Add(r.residentID))
                report.Error("Trùng residentID", who);
            if (r.healthStatus == HealthStatus.ActiveInfected)
                sick++;

            if (r.assignedHouseID < 0)
                homeless++;
            else
            {
                housed++;
                int h = bm.FindBuildingIndexById((ushort)r.assignedHouseID);
                if (h < 0)
                    report.Error("Nhà được gán không tồn tại", $"{who} nhà {r.assignedHouseID}");
                else
                {
                    occupancy[h]++;
                    if (!ResidentAssignmentRules.IsAllowedHousingType(in r, bm.allBuildings[h].buildingType))
                        report.Error("Ở sai loại nhà", $"{who} ở {bm.allBuildings[h].buildingType}");
                    if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in bm.allBuildings[h]))
                        report.Error("Ở trong nhà chưa xây xong / ngừng hoạt động", $"{who} nhà {r.assignedHouseID}");
                }
            }

            bool patient = ResidentAssignmentRules.IsClinicPatient(in r);
            bool canWork = ResidentAssignmentRules.CanWork(in r);
            bool canStudy = ResidentAssignmentRules.CanStudy(in r);
            if (r.assignedWorkID < 0)
            {
                if (canWork)
                    unemployed++;
                continue;
            }

            int w = bm.FindBuildingIndexById((ushort)r.assignedWorkID);
            if (w < 0)
            {
                report.Error("Chỗ làm được gán không tồn tại", $"{who} chỗ làm {r.assignedWorkID}");
                continue;
            }

            BuildingType type = bm.allBuildings[w].buildingType;
            if (patient || r.professionType == ProfessionType.Student)
                occupancy[w]++;
            else
                workers[w]++;

            if (patient)
            {
                if (type != BuildingType.Clinic)
                    report.Error("Người bệnh vẫn đi làm / đi học", $"{who} ở {type}");
            }
            else if (canStudy)
            {
                studying++;
                if (type != BuildingType.School)
                    report.Error("Học sinh không ở trường", $"{who} ở {type}");
            }
            else if (canWork)
            {
                working++;
                if (!ResidentAssignmentRules.IsWorkplaceFor(r.professionType, type))
                    report.Error("Làm sai nghề", $"{who} nghề {r.professionType} ở {type}");
            }
            else
            {
                report.Error("Không đủ điều kiện làm việc mà vẫn có chỗ làm", $"{who} tuổi {r.age} nghề {r.professionType}");
            }
        }

        for (int b = 0; b < bm.activeCount; b++)
        {
            ref BuildingData d = ref bm.allBuildings[b];
            string name = $"{d.buildingType} #{d.buildingID}";
            if (d.currentOccupancy != occupancy[b])
                report.Error("Số người ở ghi trên công trình lệch số thật", $"{name}: {d.currentOccupancy} / thật {occupancy[b]}");
            if (d.currentWorkers != workers[b])
                report.Error("Số công nhân ghi trên công trình lệch số thật", $"{name}: {d.currentWorkers} / thật {workers[b]}");

            int cap = d.buildingType == BuildingType.Clinic ? ResidentAssignmentRules.GetClinicPatientCapacity(in d) : d.maxOccupancy;
            if (occupancy[b] > cap)
                report.Error("Vượt sức chứa", $"{name}: {occupancy[b]}/{cap}");
            if (workers[b] > d.maxWorkers)
                report.Error("Vượt số công nhân", $"{name}: {workers[b]}/{d.maxWorkers}");
        }

        if (g.totalPopulation < alive)
            report.Error("Dân số vĩ mô nhỏ hơn số cư dân mẫu", $"{g.totalPopulation} < {alive}");

        ValidateVisuals(rm, bm, alive, report);
        report.Info($"  Dân {alive} (vĩ mô {g.totalPopulation}) · có nhà {housed} · vô gia cư {homeless} · đi làm {working} · đi học {studying} · thất nghiệp {unemployed} · đang bệnh {sick} · công trình {bm.activeCount}");
        errors = report.ErrorCount;
        return report.Build();
    }

    private static void ValidateVisuals(ResidentManager rm, BuildingManager bm, int alive, Report report)
    {
        GridController grid = rm.GridController;
        var bound = new HashSet<int>();
        int moving = 0, atGoal = 0;

        foreach (ResidentBase view in rm.ActiveVisuals)
        {
            if (view == null || !view.IsBound)
            {
                report.Error("Hình cư dân không gắn dữ liệu", "-");
                continue;
            }

            string who = view.name;
            if ((uint)view.DataIndex >= (uint)rm.activeCount)
            {
                report.Error("Hình cư dân trỏ ra ngoài mảng dữ liệu", who);
                continue;
            }
            if (!bound.Add(view.DataIndex))
                report.Error("Hai hình cùng một cư dân", who);

            ref ResidentData r = ref rm.allResidents[view.DataIndex];
            if (!view.name.StartsWith($"Resident_{r.residentID}_"))
                report.Error("Hình gắn nhầm cư dân", $"{who} <> id {r.residentID}");

            if (view.IsMoving)
                moving++;
            if (grid == null)
                continue;

            Vector3 goal = rm.DebugGetGoal(view.DataIndex, out ushort goalBuilding);
            Vector2Int cell = grid.WorldToGrid(view.transform.position);
            if (!grid.IsInside(cell.x, cell.y))
                report.Error("Cư dân đứng ngoài bản đồ", $"{who} ô {cell}");

            Vector2 toGoal = goal - view.transform.position;
            bool arrived = toGoal.sqrMagnitude < 0.01f;
            if (arrived)
                atGoal++;

            ushort occupant = grid.GetOccupantId(cell.x, cell.y);
            if (arrived && occupant != 0 && occupant != goalBuilding)
                report.Error("Đứng yên bên trong công trình không phải của mình", $"{who} ô {cell} công trình {occupant}");
        }

        int expected = Mathf.Min(alive, rm.MaxVisualAgents);
        if (rm.ActiveVisuals.Count != expected)
            report.Error("Số hình hiển thị sai", $"{rm.ActiveVisuals.Count}, đúng ra {expected}");
        report.Info($"  Hình hiển thị {rm.ActiveVisuals.Count}/{alive} (giới hạn {rm.MaxVisualAgents}) · đang đi {moving} · đã tới đích {atGoal}{(rm.DebugFreeRoam ? $" · đi tự do, tìm đường thất bại {rm.DebugPathFailures} lần" : "")}");
    }

    // ==========================================
    // ĐO HIỆU NĂNG
    // ==========================================

    /// <summary>Theo dõi cư dân bước VÀO công trình không phải điểm đi / điểm đến (đi xuyên nhà).</summary>
    private sealed class WalkThroughTracker
    {
        private readonly Dictionary<ResidentBase, ushort> _last = new Dictionary<ResidentBase, ushort>();
        public int Violations;

        public void Sample(ResidentManager rm)
        {
            GridController grid = rm.GridController;
            if (grid == null)
                return;
            foreach (ResidentBase view in rm.ActiveVisuals)
            {
                if (view == null || !view.IsBound || (uint)view.DataIndex >= (uint)rm.activeCount)
                    continue;
                Vector2Int cell = grid.WorldToGrid(view.transform.position);
                ushort occ = grid.GetOccupantId(cell.x, cell.y);
                rm.DebugGetGoal(view.DataIndex, out ushort goalBuilding);
                if (_last.TryGetValue(view, out ushort before) && occ != 0 && occ != before && occ != goalBuilding)
                    Violations++;
                _last[view] = occ;
            }
        }
    }

    public static IEnumerator Benchmark(PlayerContain contain, int[] levels, bool showAll, float seconds, System.Action<string> done)
    {
        ResidentManager rm = contain.residentManager;
        int oldCap = rm.MaxVisualAgents;
        int oldVSync = QualitySettings.vSyncCount;
        int oldTarget = Application.targetFrameRate;
        bool oldBackground = Application.runInBackground;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;

        var sb = new StringBuilder("[Test cư dân] Đo hiệu năng (Editor, chậm hơn bản build)\n");
        sb.AppendLine("Dân | Hình | FPS TB | Khung TB ms | Khung xấu 1% ms | Di chuyển ms | Tìm đường ms | GC KB/khung | Tính tháng ms | Đi xuyên nhà");

        var samples = new List<float>(4096);
        try
        {
            foreach (int level in levels)
            {
                Clear(contain);
                rm.MaxVisualAgents = showAll ? Mathf.Max(oldCap, level) : oldCap;
                Spawn(contain, Mathf.Max(0, level - rm.activeCount), true, true);
                Reassign(contain);
                FeedEveryone(contain);
                yield return null;
                yield return new WaitForSecondsRealtime(1f);

                // Nửa đầu đi về nhà, nửa sau đi làm: ép cả làng đổi ca để đo lúc nặng nhất
                contain.globalSystemManager.GetGlobalDataRef().timeTickProgress = 0.05f;

                samples.Clear();
                double tickNs = 0, pathNs = 0, gcBytes = 0;
                var tracker = new WalkThroughTracker();
                ProfilerRecorder tick = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, TickMarker);
                ProfilerRecorder path = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, PathMarker);
                ProfilerRecorder gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    samples.Add(Time.unscaledDeltaTime * 1000f);
                    tickNs += tick.LastValue;
                    pathNs += path.LastValue;
                    gcBytes += gc.LastValue;
                    // Không lấy mẫu mỗi khung để phép đo ít ảnh hưởng kết quả
                    if (samples.Count % 5 == 0)
                        tracker.Sample(rm);
                }
                tick.Dispose();
                path.Dispose();
                gc.Dispose();

                Stopwatch sw = Stopwatch.StartNew();
                contain.globalSystemManager.DebugAdvanceMonth();
                sw.Stop();

                int n = Mathf.Max(1, samples.Count);
                float sum = 0f;
                foreach (float s in samples)
                    sum += s;
                float avg = sum / n;
                samples.Sort();
                float worst = samples.Count > 0 ? samples[Mathf.Min(n - 1, Mathf.FloorToInt(n * 0.99f))] : 0f;
                sb.AppendLine($"{rm.activeCount} | {rm.ActiveVisuals.Count} | {(avg > 0f ? 1000f / avg : 0f):0} | {avg:0.0} | {worst:0.0} | {tickNs / n / 1e6:0.00} | {pathNs / n / 1e6:0.00} | {gcBytes / n / 1024:0.0} | {sw.Elapsed.TotalMilliseconds:0.0} | {tracker.Violations}");
            }
        }
        finally
        {
            rm.MaxVisualAgents = oldCap;
            QualitySettings.vSyncCount = oldVSync;
            Application.targetFrameRate = oldTarget;
            Application.runInBackground = oldBackground;
        }

        done?.Invoke(sb.ToString());
    }

    /// <summary>Đủ ăn, đủ than để dân test không chết hàng loạt làm sai phép đo.</summary>
    public static void FeedEveryone(PlayerContain contain)
    {
        ref GlobalSystemData g = ref contain.globalSystemManager.GetGlobalDataRef();
        g.stockFood = Mathf.Max(g.stockFood, g.totalPopulation * 24);
        g.stockCoal = Mathf.Max(g.stockCoal, g.totalPopulation * 12);
    }
}
#endif
