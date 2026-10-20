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

    public static EdictManager ResolveEdictManager(EdictManager fallback)
    {
        PlayerContain contain = Object.FindFirstObjectByType<PlayerContain>();
        if (contain != null && contain.edictManager != null)
            return contain.edictManager;

        if (fallback != null && fallback.isActiveAndEnabled)
            return fallback;

        return Object.FindFirstObjectByType<EdictManager>();
    }
}
