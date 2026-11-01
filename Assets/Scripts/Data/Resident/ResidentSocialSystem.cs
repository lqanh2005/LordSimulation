using UnityEngine;

public static class ResidentSocialSystem
{
    public static int LastMonthTax { get; private set; }

    public static void ResetStats()
    {
        LastMonthTax = 0;
    }

    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        GlobalSystemManager global)
    {
        LastMonthTax = 0;
        if (residents == null)
            return;

        float riotAdd = 0f;
        int alive = 0;
        int taxSample = 0;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            alive++;
            ResidentSocialRules.EnsureFaction(ref r);
            ResidentSocialRules.ApplyWealthDelta(ref r, ResidentSocialRules.GetMonthlyWealthDelta(in r));

            int tax = ResidentSocialRules.GetMonthlyTax(in r, out int wealthCost);
            if (wealthCost > 0)
                ResidentSocialRules.ApplyWealthDelta(ref r, -wealthCost);
            taxSample += tax;

            ResidentProfessionEffectRules.AddHappiness(ref r, ResidentSocialRules.GetMonthlyHappinessDelta(in r));
            riotAdd += ResidentSocialRules.GetRiotContribution(in r);
        }

        if (global == null)
            return;

        ref GlobalSystemData data = ref global.GetGlobalDataRef();

        if (taxSample > 0)
        {
            float scale = alive > 0 && data.totalPopulation > alive ? (float)data.totalPopulation / alive : 1f;
            LastMonthTax = Mathf.RoundToInt(taxSample * scale);
            global.ModifyGold(LastMonthTax);
        }

        if (riotAdd <= 0f)
            return;

        if (riotAdd > ResidentSocialRules.MaxMonthlyRiotFromSocial)
            riotAdd = ResidentSocialRules.MaxMonthlyRiotFromSocial;

        data.riotRiskMeter = Mathf.Min(100f, data.riotRiskMeter + riotAdd);
    }
}
