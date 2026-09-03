using System;

public enum DestinationNode : byte
{
    None = 0, OutpostNorth = 1, CoalMineWest = 2, OldHospitalRuins = 3, CapitalBorder = 4
}

public enum TransportType : byte { Foot = 0, Cart = 1, SteamTruck = 2 }
public enum ResourceType : byte { None = 0, Food = 1, Wood = 2, Iron = 3, Coal = 4, Medicine = 5, Machinery = 6 }
public enum RouteStatus : byte { Idle = 0, EnRoute = 1, Ambushed = 2, Completed = 3 }
[Serializable]
public struct TradeRouteData
{
    public byte routeID;
    public DestinationNode destinationNode;
    public TransportType transportType;
    public byte travelMonths;
    public byte currentProgress;
    public byte safetyRating;
    public byte guardCount;
    public ResourceType exportItem;
    public ushort exportAmount;
    public ResourceType importItem;
    public ushort importAmount;
    public RouteStatus routeStatus;
    public byte outpostInfectionRisk;
    public bool isRepeat;
    public bool isAllocated;
}