using UnityEngine;

public static class HUDLocator
{
    public static GlobalSystemManager ResolveGlobalSystem(GlobalSystemManager fallback)
    {
        PlayerContain contain = Object.FindFirstObjectByType<PlayerContain>();
        if (contain != null && contain.globalSystemManager != null)
            return contain.globalSystemManager;

        if (fallback != null && fallback.isActiveAndEnabled)
            return fallback;

        return Object.FindFirstObjectByType<GlobalSystemManager>();
    }
}
