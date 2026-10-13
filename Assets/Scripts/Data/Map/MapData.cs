using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct MapDecorEntry
{
    public int x;
    public int y;
    public string decorId;
    public DecorLayer layer;
    public Vector2 offset;
    public float scale;
    public bool flipX;
}

[CreateAssetMenu(fileName = "MapData", menuName = "LordSimulation/Map Data")]
public class MapData : ScriptableObject
{
    public int width;
    public int height;
    public int seed;
    public List<MapDecorEntry> decors = new List<MapDecorEntry>();

    public bool HasData => width > 0 && height > 0;

    public void Clear()
    {
        width = 0;
        height = 0;
        seed = 0;
        decors.Clear();
    }
}
