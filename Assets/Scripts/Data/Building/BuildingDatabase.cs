using System.Collections.Generic;
using UnityEngine;

public class BuildingDatabase : ScriptableObject
{
    public List<BuildingConfig> allConfigs;

    public BuildingConfig GetConfig(BuildingType searchType)
    {
        return allConfigs.Find(x => x.type == searchType);
    }
}