using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerContain : MonoBehaviour
{
    public ResidentManager residentManager;
    public BuildingManager buildingManager;
    public EdictManager edictManager;
    public GlobalSystemManager globalSystemManager;
    public TradeManager tradeManager;
    public ImmigrationManager immigrationManager;
    public ResearchManager researchManager;
    public MerchantManager merchantManager;
    public CrisisManager crisisManager;
    public void Init()
    {
        if (crisisManager == null)
            crisisManager = GetComponentInChildren<CrisisManager>(true);
        if (crisisManager == null)
            crisisManager = gameObject.AddComponent<CrisisManager>();
        crisisManager.Init(globalSystemManager, buildingManager);

        // GDD v0.3 mục 9.4: Lệnh khẩn cấp qua thông báo sự kiện
        EmergencyManager emergency = GetComponentInChildren<EmergencyManager>(true);
        if (emergency == null)
            emergency = gameObject.AddComponent<EmergencyManager>();
        emergency.Init(this);

        if (merchantManager == null)
            merchantManager = GetComponentInChildren<MerchantManager>(true);
        if (merchantManager == null)
            merchantManager = gameObject.AddComponent<MerchantManager>();
        merchantManager.Init();

        residentManager.Init();
        if (immigrationManager == null)
            immigrationManager = GetComponentInChildren<ImmigrationManager>(true);
        if (immigrationManager == null)
            immigrationManager = gameObject.AddComponent<ImmigrationManager>();
        immigrationManager.Init();

        if (researchManager == null)
            researchManager = GetComponentInChildren<ResearchManager>(true);
        if (researchManager == null)
            researchManager = gameObject.AddComponent<ResearchManager>();
        researchManager.Init();
    }
}
