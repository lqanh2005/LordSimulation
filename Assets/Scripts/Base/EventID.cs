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

    SaveStarted = 300,
    SaveCompleted = 301,
    SaveFailed = 302,
    LoadStarted = 303,
    LoadCompleted = 304,
    LoadFailed = 305,

    ImmigrationQueueChanged = 400,

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
    GameOver = 702,

    ResearchChanged = 800,          // tiến độ / lựa chọn nghiên cứu thay đổi
    ResearchCompleted = 801         // param: ResearchID vừa hoàn thành
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
