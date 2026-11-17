using System;

public enum BuildingType : byte
{
    None = 0, House = 1, QuarantineWard = 2, Farm = 3, Mine = 4, Clinic = 5, Furnace = 6, School = 7, GuardPost = 8, LumberCamp = 9, Tent = 10,
    CityHall = 11, WaterWell = 12, CoalStore = 13, Campfire = 14, CoalCart = 15, HerbFarm = 16,
    LargeHouse = 17, Forge = 18,
    Road = 19   // GDD: Road Connectivity — đường đất 1x1, đi qua được
}
public enum CropType : byte
{
    None = 0, Rice = 1, Drug = 2
}
public enum BuildingState : byte
{
    None = 0, Constructing = 1, Active = 2, Destroyed = 3
}
[Serializable]
public struct BuildingData
{
    public ushort buildingID;  // ID của tòa nhà
    public BuildingType buildingType;
    public ushort coordX;  // Tọa độ X trên bản đồ
    public ushort coordY;
    public byte sizeFootprint;  // Footprint đóng gói: 4 bit thấp = X, 4 bit cao = Y (0 = vuông, dữ liệu cũ). Dùng BuildingRules.GetFootprint
    public byte tierLevel;  // Cấp độ của tòa nhà
    public BuildingState buildingState;
    public float constructionProgress;  // Tiến độ xây dựng: 0.0 - 1.0
    public byte priorityLevel;  // Mức độ ưu tiên: 0 - 100
    public bool isOperational;
    public bool isAllocated;  // Cho biết tòa nhà đã được phân bổ cho cư dân hay chưa
    public byte currentOccupancy;  // Số lượng cư dân hiện tại trong tòa nhà
    public byte maxOccupancy;
    public byte currentWorkers;
    public byte maxWorkers;
    public float efficiency;  // Hiệu suất làm việc: 0.0 - 1.0
    public float productionProgress; // Chu kỳ sản xuất
    public ushort fuelStored;  // Lượng nhiên liệu hiện có trong kho
    public ushort outputStored;  // Lượng sản phẩm làm ra
    public float durability;  // Độ bền của tòa nhà: 0.0 - 1.0
    public float insulationValue;  // Hệ số giữ nhiệt: 0.0 - 1.0
    public float contamination; // Mức độ ô nhiễm: 0.0 - 100
    public bool isHeated;  // Cho biết tòa nhà có được sưởi ấm hay không
    public CropType cropType;  // Farm: loại cây đang trồng (None = đất trống)
    public byte cropStage;  // Farm: giai đoạn lớn 0..FarmCropRules.StageCount-1
}
