using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace AllRelicsBecomeOneRelic;

internal static class RelicReplacementService
{
    private static readonly HashSet<RelicModel> DeferredStarterRelics =
        new(ReferenceEqualityComparer.Instance);

    private static bool _isRunningDeferredStarterEffects;

    private static readonly AccessTools.FieldRef<EventOption, LocString> EventOptionTitleRef =
        AccessTools.FieldRefAccess<EventOption, LocString>("<Title>k__BackingField");

    private static readonly AccessTools.FieldRef<EventOption, LocString> EventOptionHistoryNameRef =
        AccessTools.FieldRefAccess<EventOption, LocString>("<HistoryName>k__BackingField");

    private static readonly AccessTools.FieldRef<EventOption, RelicModel?> EventOptionRelicRef =
        AccessTools.FieldRefAccess<EventOption, RelicModel?>("<Relic>k__BackingField");

    internal const int PreservedRelicProducerCount = 1;

    internal static RelicModel ReplaceObtainedRelic(RelicModel original, Player player, string source)
    {
        return ReplaceCore(original, source, mutable: true, forceFreshInstance: false, player.NetId);
    }

    internal static RelicModel ReplacePreviewCanonical(RelicModel original, string source)
    {
        return ReplaceCore(original, source, mutable: false, forceFreshInstance: false, null);
    }

    internal static RelicModel ReplacePreviewMutable(RelicModel original, string source)
    {
        return ReplaceCore(original, source, mutable: true, forceFreshInstance: false, null);
    }

    internal static RelicModel ReplacePreviewDistinctMutable(RelicModel original, string source)
    {
        return ReplaceCore(original, source, mutable: true, forceFreshInstance: true, null);
    }

    internal static IReadOnlyList<RelicModel> ReplacePreviewChoiceList(
        IReadOnlyList<RelicModel> relics,
        string source
    )
    {
        if (relics.Count == 0)
        {
            return relics;
        }

        List<RelicModel> replaced = new(relics.Count);
        foreach (RelicModel relic in relics)
        {
            replaced.Add(ReplacePreviewDistinctMutable(relic, source));
        }

        return replaced;
    }

    internal static void RewriteEventOptionPreview(
        EventOption option,
        string? descriptionRelicNameVariable,
        string source
    )
    {
        RelicModel? configuredTarget = ResolveConfiguredTarget();
        if (configuredTarget == null)
        {
            return;
        }

        RelicModel preview = configuredTarget.ToMutable();
        EventOptionTitleRef(option) = preview.Title;
        EventOptionHistoryNameRef(option) = preview.Title;
        EventOptionRelicRef(option) = preview;
        option.HoverTips = preview.HoverTips;

        if (!string.IsNullOrWhiteSpace(descriptionRelicNameVariable))
        {
            option.Description.Add(descriptionRelicNameVariable, preview.Title);
        }

        if (ModEntry.Config.LogEveryReplacement)
        {
            ModLog.Info($"Rewrote event option preview to '{preview.Id.Entry}' during {source}.");
        }
    }

    internal static void ReplaceTreasurePreviewRelics(List<RelicModel>? relics, string source)
    {
        if (relics == null || relics.Count == 0)
        {
            return;
        }

        for (int i = 0; i < relics.Count; i++)
        {
            relics[i] = ReplacePreviewDistinctMutable(relics[i], source);
        }
    }

    internal static bool TryReturnMutableSelf(RelicModel relic, out RelicModel result)
    {
        if (relic.IsMutable)
        {
            result = relic;
            return true;
        }

        result = null!;
        return false;
    }

    internal static void ReplaceStarterRelics(Player player)
    {
        DeferredStarterRelics.Clear();

        RelicModel? configuredTarget = ResolveConfiguredTarget();
        if (configuredTarget == null || player.Relics.Count == 0)
        {
            return;
        }

        List<RelicModel> originals = player.Relics.ToList();
        foreach (RelicModel relic in originals)
        {
            player.RemoveRelicInternal(relic, silent: true);
        }

        for (int i = 0; i < originals.Count; i++)
        {
            RelicModel replacement = configuredTarget.ToMutable();
            replacement.FloorAddedToDeck = 1;
            player.AddRelicInternal(replacement, -1, silent: true);
            if (replacement.HasUponPickupEffect)
            {
                DeferredStarterRelics.Add(replacement);
            }
        }

        ModLog.Info($"Replaced {originals.Count} starter relic(s) with '{configuredTarget.Id.Entry}'.");
    }

    internal static Task FinalizeStartingRelicsSafely(RunManager runManager)
    {
        return FinalizeStartingRelicsSafelyInternal(runManager);
    }

    internal static Task RunDeferredStarterEffects()
    {
        return RunDeferredStarterEffectsInternal();
    }

    internal static RelicModel? DebugResolveConfiguredTarget(string rawValue)
    {
        return ResolveConfiguredTarget(rawValue);
    }

    private static RelicModel ReplaceCore(
        RelicModel original,
        string source,
        bool mutable,
        bool forceFreshInstance,
        ulong? playerId
    )
    {
        RelicModel? configuredTarget = ResolveConfiguredTarget();
        if (configuredTarget == null)
        {
            return original;
        }

        if (ModEntry.Config.PreserveRelicProducers && IsRelicProducer(original))
        {
            if (ModEntry.Config.LogEveryReplacement)
            {
                ModLog.Info($"Keeping relic '{original.Id.Entry}' during {source} because it can produce more relics later.");
            }

            return PreserveOriginalRelic(original, mutable);
        }

        bool alreadyTarget = original.Id == configuredTarget.Id;
        if (alreadyTarget && !forceFreshInstance)
        {
            return original;
        }

        RelicModel replacement = mutable ? configuredTarget.ToMutable() : configuredTarget;
        if (mutable)
        {
            TryCopyOwner(original, replacement);
        }

        if (ModEntry.Config.LogEveryReplacement)
        {
            string scope = playerId.HasValue ? $" for player {playerId.Value}" : string.Empty;
            ModLog.Info($"Replacing relic '{original.Id.Entry}' with '{configuredTarget.Id.Entry}'{scope} during {source}.");
        }

        return replacement;
    }

    private static RelicModel PreserveOriginalRelic(RelicModel original, bool mutable)
    {
        if (!mutable)
        {
            return original;
        }

        if (original.IsMutable)
        {
            return original;
        }

        return original.ToMutable();
    }

    private static async Task FinalizeStartingRelicsSafelyInternal(RunManager runManager)
    {
        RunState? state = runManager.DebugOnlyGetState();
        if (state == null)
        {
            return;
        }

        foreach (Player player in state.Players)
        {
            foreach (RelicModel relic in player.Relics)
            {
                if (DeferredStarterRelics.Contains(relic))
                {
                    continue;
                }

                await relic.AfterObtained();
            }
        }
    }

    private static async Task RunDeferredStarterEffectsInternal()
    {
        if (_isRunningDeferredStarterEffects || DeferredStarterRelics.Count == 0)
        {
            return;
        }

        _isRunningDeferredStarterEffects = true;
        try
        {
            List<RelicModel> pending = DeferredStarterRelics.ToList();
            DeferredStarterRelics.Clear();
            foreach (RelicModel relic in pending)
            {
                await relic.AfterObtained();
            }
        }
        finally
        {
            _isRunningDeferredStarterEffects = false;
        }
    }

    private static void TryCopyOwner(RelicModel original, RelicModel replacement)
    {
        if (!original.IsMutable)
        {
            return;
        }

        Player? owner = original.Owner;
        if (owner != null)
        {
            replacement.Owner = owner;
        }
    }

    private static bool IsRelicProducer(RelicModel relic)
    {
        return relic is WongosMysteryTicket;
    }

    private static RelicModel? ResolveConfiguredTarget()
    {
        return ResolveConfiguredTarget(ModEntry.Config.TargetRelicId?.Trim() ?? string.Empty);
    }

    private static RelicModel? ResolveConfiguredTarget(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            ModLog.Warn("target_relic_id is empty. Leaving relics unchanged.");
            return null;
        }

        string normalized = rawValue.ToUpperInvariant();
        int separatorIndex = normalized.IndexOf('.');
        if (separatorIndex >= 0 && separatorIndex < normalized.Length - 1)
        {
            normalized = normalized[(separatorIndex + 1)..];
        }

        List<RelicModel> candidates = ModelDb.AllRelics
            .Where(relic => relic.Id.Entry.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();

        RelicModel? match = candidates.FirstOrDefault(relic =>
                string.Equals(relic.Id.Entry, normalized, StringComparison.OrdinalIgnoreCase)
            )
            ?? candidates.FirstOrDefault(relic =>
                relic.Id.Entry.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
            )
            ?? candidates.FirstOrDefault();

        if (match == null)
        {
            ModLog.Warn($"Could not find a relic matching '{rawValue}'. Leaving relics unchanged.");
        }

        return match;
    }
}
