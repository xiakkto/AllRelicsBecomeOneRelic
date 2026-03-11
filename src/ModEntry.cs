using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AllRelicsBecomeOneRelic;

[ModInitializer(nameof(Initialize))]
internal static class ModEntry
{
    internal const string ModId = "codex.all_relics_become_one_relic";

    internal const string ModFileStem = "AllRelicsBecomeOneRelic";

    internal static RelicReplacementConfig Config { get; private set; } = RelicReplacementConfig.Default;

    internal static string ConfigFilePath =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModFileStem}.json");

    public static void Initialize()
    {
        Config = RelicReplacementConfig.Load(ConfigFilePath);
        Config.ReplaceStarterRelics = true;
        Config.Save(ConfigFilePath);
        new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
        ModLog.Info(
            $"Initialized with target_relic_id='{Config.TargetRelicId}', replace_starter_relics={Config.ReplaceStarterRelics}, preserve_relic_producers={Config.PreserveRelicProducers}."
        );
    }

    internal static void UpdateConfig(RelicReplacementConfig config)
    {
        Config = config;
        Config.Save(ConfigFilePath);
        ModLog.Info(
            $"Updated config: target_relic_id='{Config.TargetRelicId}', replace_starter_relics={Config.ReplaceStarterRelics}, preserve_relic_producers={Config.PreserveRelicProducers}."
        );
    }
}
