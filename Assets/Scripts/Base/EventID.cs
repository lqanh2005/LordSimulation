using System;

public enum EventID
{
    None = 0,

    MonthChanged = 100,
    YearChanged = 101,
    OnWeatherChanged = 102,

    ResourcesChanged = 200,
    HungerResolved = 201,
    ColderResolved = 202,
    DiseasePressure = 203,
    PopulationChanged = 204,
    ResidentsDied = 205,            // param: ResidentsDiedPayload
    CrisisReport = 206,             // param: CrisisReportPayload (thi thể / Miasma mỗi tháng)

    SaveStarted = 300,
    SaveCompleted = 301,
    SaveFailed = 302,
    LoadStarted = 303,
    LoadCompleted = 304,
    LoadFailed = 305,

    ImmigrationQueueChanged = 400,
    WaitingYardReport = 401,        // param: WaitingYardReportPayload (mỗi tháng)

    BuildingPlaced = 500,
    BuildingPlaceFailed = 501,
    BuildingCompleted = 502,
    BuildingClicked = 503,
    FarmCropChanged = 504,
    BuildingDemolished = 505,       // param: BuildingDemolishedPayload
    DemolishModeChanged = 506,      // param: bool (đang ở chế độ phá dỡ?)

    EdictsChanged = 600,
    EdictTriggered = 601,

    AppStateChanged = 700,
    GamePlayStateChanged = 701,
    GameOver = 702,                 // param: GameOverPayload
    Victory = 703,                  // param: VictoryPayload

    ResearchChanged = 800,          // tiến độ / lựa chọn nghiên cứu thay đổi
    ResearchCompleted = 801,        // param: ResearchID vừa hoàn thành

    MerchantArrived = 900,          // param: bool (true = vừa tới trong game, false = khôi phục từ save)
    MerchantLeft = 901,             // param: bool (true = vừa rời đi trong game)
    MerchantTraded = 902
}

public struct AppStateChangedPayload
{
    public AppStateID from;
    public AppStateID to;
}

public struct GamePlayStateChangedPayload
{
    public GamePlayStateID from;
    public GamePlayStateID to;
}

public struct BuildingDemolishedPayload
{
    public ushort buildingId;
    public BuildingType type;
    public int refundWood;
    public int refundGold;
}

public enum BuildingPlaceFailReason : byte
{
    None = 0,
    Blocked = 1,
    NotEnoughResources = 2,
    CapacityFull = 3,
    AlreadyBuilt = 4,
    NotResearched = 5
}

public struct BuildingPlaceFailedPayload
{
    public BuildingType type;
    public BuildingPlaceFailReason reason;
    public int missingWood;
    public int missingGold;
}

public struct MonthChangedPayload
{
    public int year;
    public byte month;
    public SeasonType season;
    public bool isSimulationTick;
}

public struct WaitingYardReportPayload
{
    public int waiting;          // Số người còn chờ sau khi xử lý tháng
    public int foodEaten;
    public int hungry;           // Không được ăn tháng này
    public int newlyInfected;    // Bị lây trong hàng chờ
    public int newlySymptomatic; // Ủ bệnh -> phát bệnh trong lúc chờ
    public int left;             // Bỏ đi vì đói
    public int died;             // Chết bệnh trong hàng chờ (để lại thi thể ở Cổng)
    public float riotAdded;
}

public enum DeathCause : byte
{
    OldAge = 0,
    Starvation = 1,
    Cold = 2,
    Disease = 3
}

public struct ResidentsDiedPayload
{
    public DeathCause cause;
    public int count;               // Số người chết quy ra dân số thật (macro)
}

public enum GameOverReason : byte
{
    None = 0,
    Extinction = 1,     // Toàn bộ dân chết
    Riot = 2            // Bạo loạn chạm đỉnh -> lãnh chúa bị lật đổ
}

public struct GameOverPayload
{
    public GameOverReason reason;
    public int year;
    public byte month;
}

public struct VictoryPayload
{
    public int year;
    public byte month;
    public int population;
    public int wintersSurvived;
}

public struct WeatherChangedPayload
{
    public WeatherEvent weather;
    public float temperature;
}

public static class GameEvents
{
    public static void Post(EventID eventId, object param = null)
    {
        EventDispatcher.EventDispatcher.Instance.PostEvent(eventId, param);
    }

    public static void Listen(EventID eventId, Action<object> callback)
    {
        EventDispatcher.EventDispatcher.Instance.RegisterListener(eventId, callback);
    }

    public static void Unlisten(EventID eventId, Action<object> callback)
    {
        if (!EventDispatcher.EventDispatcher.HasInstance())
            return;

        EventDispatcher.EventDispatcher.Instance.RemoveListener(eventId, callback);
    }
}
