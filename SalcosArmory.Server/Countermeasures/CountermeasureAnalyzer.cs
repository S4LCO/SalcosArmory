using SalcosArmory.Config;

namespace SalcosArmory.Countermeasures;

public static class CountermeasureAnalyzer
{
    public static CountermeasureAnalysis Analyze(
        CountermeasureProfileState state,
        CountermeasureProtocolSettings settings)
    {
        var raids = state.Raids.TakeLast(settings.HistorySize).ToArray();
        if (raids.Length < settings.MinimumRaids)
        {
            return CountermeasureAnalysis.Inactive(raids.Length);
        }

        var weightedRaids = 0d;
        var weightedNight = 0d;
        var weightedSuppressor = 0d;
        var weightedHeavyArmor = 0d;
        var weightedSurvival = 0d;
        var weightedKills = 0d;
        var weightedHeadshots = 0d;
        var weightedKillDistance = 0d;

        for (var index = raids.Length - 1; index >= 0; index--)
        {
            var age = raids.Length - 1 - index;
            var weight = Math.Pow(settings.HistoryDecay, age);
            var raid = raids[index];

            weightedRaids += weight;
            weightedNight += raid.NightRaid ? weight : 0d;
            weightedSuppressor += raid.UsedSuppressor ? weight : 0d;
            weightedHeavyArmor += raid.UsedHeavyArmor ? weight : 0d;
            weightedSurvival += raid.Survived ? weight : 0d;
            weightedKills += raid.Kills * weight;
            weightedHeadshots += raid.HeadshotKills * weight;
            weightedKillDistance += raid.TotalKillDistance * weight;
        }

        var nightRatio = SafeDivide(weightedNight, weightedRaids);
        var suppressorRatio = SafeDivide(weightedSuppressor, weightedRaids);
        var heavyArmorRatio = SafeDivide(weightedHeavyArmor, weightedRaids);
        var survivalRate = SafeDivide(weightedSurvival, weightedRaids);
        var headshotRatio = SafeDivide(weightedHeadshots, weightedKills);
        var averageKillDistance = SafeDivide(weightedKillDistance, weightedKills);

        var active = new List<CountermeasureKind>();
        var pressureParts = new List<double>();

        AddSignal(
            settings.EnableNightVision,
            nightRatio,
            settings.NightRaidThreshold,
            CountermeasureKind.NightVision);

        AddSignal(
            settings.EnableFaceProtection,
            headshotRatio,
            settings.HeadshotRatioThreshold,
            CountermeasureKind.FaceProtection);

        AddSignal(
            settings.EnableLongRangeOptics,
            NormalizeDistance(averageKillDistance, settings.LongRangeDistanceThreshold),
            1d,
            CountermeasureKind.LongRangeOptic,
            distanceSignal: true);

        AddSignal(
            settings.EnableHearingProtection,
            suppressorRatio,
            settings.SuppressorUsageThreshold,
            CountermeasureKind.HearingProtection);

        AddSignal(
            settings.EnableArmorPiercingAmmo,
            heavyArmorRatio,
            settings.HeavyArmorUsageThreshold,
            CountermeasureKind.ArmorPiercingAmmo);

        if (survivalRate >= settings.SurvivalRateThreshold)
        {
            pressureParts.Add(NormalizeAboveThreshold(survivalRate, settings.SurvivalRateThreshold));
        }

        if (active.Count == 0)
        {
            return new CountermeasureAnalysis(
                false,
                raids.Length,
                0d,
                0,
                [],
                nightRatio,
                headshotRatio,
                averageKillDistance,
                suppressorRatio,
                heavyArmorRatio,
                survivalRate,
                0d
            );
        }

        var pressure = pressureParts.Count == 0 ? 0d : pressureParts.Average();
        var affectedPercent = settings.MinimumAffectedPercent
            + ((settings.MaximumAffectedPercent - settings.MinimumAffectedPercent) * pressure);

        var maximumMeasures = Math.Min(settings.MaximumCountermeasuresPerBot, active.Count);
        var measuresPerBot = maximumMeasures <= 1 || pressure < 0.5d ? 1 : maximumMeasures;

        return new CountermeasureAnalysis(
            true,
            raids.Length,
            Math.Clamp(affectedPercent / 100d, 0d, 1d),
            measuresPerBot,
            active,
            nightRatio,
            headshotRatio,
            averageKillDistance,
            suppressorRatio,
            heavyArmorRatio,
            survivalRate,
            pressure
        );

        void AddSignal(
            bool enabled,
            double value,
            double threshold,
            CountermeasureKind kind,
            bool distanceSignal = false)
        {
            if (!enabled)
            {
                return;
            }

            var activeNow = distanceSignal ? value >= 1d : value >= threshold;
            if (!activeNow)
            {
                return;
            }

            active.Add(kind);
            pressureParts.Add(distanceSignal
                ? Math.Clamp(value - 1d, 0d, 1d)
                : NormalizeAboveThreshold(value, threshold));
        }
    }

    private static double NormalizeDistance(double distance, double threshold)
    {
        if (threshold <= 0d)
        {
            return distance > 0d ? 2d : 0d;
        }

        return Math.Clamp(distance / threshold, 0d, 2d);
    }

    private static double NormalizeAboveThreshold(double value, double threshold)
    {
        if (threshold >= 1d)
        {
            return value >= threshold ? 1d : 0d;
        }

        return Math.Clamp((value - threshold) / (1d - threshold), 0d, 1d);
    }

    private static double SafeDivide(double numerator, double denominator)
    {
        return denominator > 0.0001d ? numerator / denominator : 0d;
    }
}
