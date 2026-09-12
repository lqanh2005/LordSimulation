using UnityEngine;

public static class ResidentSocialSystem
{
    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        GlobalSystemManager global)
    {
        if (residents == null)
            return;

        float riotAdd = 0f;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            ResidentSocialRules.EnsureFaction(ref r);
            ResidentSocialRules.ApplyWealthDelta(ref r, ResidentSocialRules.GetMonthlyWealthDelta(in r));
            ResidentProfessionEffectRules.AddHappiness(ref r, ResidentSocialRules.GetMonthlyHappinessDelta(in r));
            riotAdd += ResidentSocialRules.GetRiotContribution(in r);
        }

        if (global == null || riotAdd <= 0f)
            return;

        if (riotAdd > ResidentSocialRules.MaxMonthlyRiotFromSocial)
            riotAdd = ResidentSocialRules.MaxMonthlyRiotFromSocial;

        ref GlobalSystemData data = ref global.GetGlobalDataRef();
        data.riotRiskMeter = Mathf.Min(100f, data.riotRiskMeter + riotAdd);
    }
}
