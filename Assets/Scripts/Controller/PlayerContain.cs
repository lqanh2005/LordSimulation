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
    public void Init()
    {
        residentManager.Init();
        if (immigrationManager == null)
            immigrationManager = GetComponentInChildren<ImmigrationManager>(true);
        if (immigrationManager == null)
            immigrationManager = gameObject.AddComponent<ImmigrationManager>();
        immigrationManager.Init();
    }
}
