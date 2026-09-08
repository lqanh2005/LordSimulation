using System;

public enum EventID
{
    None = 0,

    MonthChanged = 100,
    YearChanged = 101,
    WeatherChanged = 102,

    ResourcesChanged = 200,
    HungerResolved = 201,
    ColderResolved = 202,
    DiseasePressure = 203,

    SaveStarted = 300,
    SaveCompleted = 301,
    SaveFailed = 302,
    LoadStarted = 303,
    LoadCompleted = 304,
    LoadFailed = 305
}

public struct MonthChangedPayload
{
    public int year;
    public byte month;
    public SeasonType season;
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
