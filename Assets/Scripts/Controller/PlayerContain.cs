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
    public void Init()
    {
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
