using System;
using UnityEngine;

public class TradeManager : MonoBehaviour
{
    public const int MAX_TRADE_ROUTES = 32;

    [Header("Tuyến Đường Thương Mại")]
    public TradeRouteData[] allTradeRoutes = new TradeRouteData[MAX_TRADE_ROUTES];
    public int activeCount = 0;
    public int AddTradeRoute(in TradeRouteData newRoute)
    {
        if (activeCount >= MAX_TRADE_ROUTES)
        {
            Debug.LogError("[TradeManager] Đã đạt giới hạn tuyến thương mại!");
            return -1;
        }

        int index = activeCount;
        allTradeRoutes[index] = newRoute;
        allTradeRoutes[index].isAllocated = true;
        activeCount++;
        return index;
    }

    public ref TradeRouteData GetTradeRouteRef(int index)
    {
        if ((uint)index >= (uint)activeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ref allTradeRoutes[index];
    }
}