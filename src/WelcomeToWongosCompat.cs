using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace AllRelicsBecomeOneRelic;

internal static class WelcomeToWongosCompat
{
    private static readonly MethodInfo SetEventFinishedMethod =
        AccessTools.Method(typeof(EventModel), "SetEventFinished")!;

    private static readonly PropertyInfo EventOwnerProperty =
        AccessTools.Property(typeof(EventModel), "Owner")!;

    private static readonly AccessTools.FieldRef<WelcomeToWongos, RelicModel?> FeaturedItemRef =
        AccessTools.FieldRefAccess<WelcomeToWongos, RelicModel?>("_featuredItem");

    internal static async Task RunBuyBargainBin(WelcomeToWongos evt)
    {
        Player owner = GetOwner(evt);
        await PlayerCmd.LoseGold(evt.DynamicVars["BargainBinCost"].BaseValue, owner, GoldLossType.Spent);
        RelicModel relic = RelicFactory.PullNextRelicFromFront(owner, RelicRarity.Common).ToMutable();
        (LocString description, bool shouldGrantBadge) = PrepareAfterBuyState(evt, 32);
        FinishEvent(evt, description);
        TaskHelper.RunSafely(GrantDeferredRewards(owner, relic, shouldGrantBadge));
    }

    internal static async Task RunBuyFeaturedItem(WelcomeToWongos evt)
    {
        Player owner = GetOwner(evt);
        await PlayerCmd.LoseGold(evt.DynamicVars["FeaturedItemCost"].BaseValue, owner, GoldLossType.Spent);
        RelicModel relic = FeaturedItemRef(evt)?.ToMutable() ?? RelicFactory.PullNextRelicFromFront(owner, RelicRarity.Rare).ToMutable();
        (LocString description, bool shouldGrantBadge) = PrepareAfterBuyState(evt, 16);
        FinishEvent(evt, description);
        TaskHelper.RunSafely(GrantDeferredRewards(owner, relic, shouldGrantBadge));
    }

    internal static async Task RunBuyMysteryBox(WelcomeToWongos evt)
    {
        Player owner = GetOwner(evt);
        await PlayerCmd.LoseGold(evt.DynamicVars["MysteryBoxCost"].BaseValue, owner, GoldLossType.Spent);
        (LocString description, bool shouldGrantBadge) = PrepareAfterBuyState(evt, 8);
        FinishEvent(evt, description);
        TaskHelper.RunSafely(GrantDeferredRewards(owner, ModelDb.Relic<WongosMysteryTicket>().ToMutable(), shouldGrantBadge));
    }

    private static async Task GrantDeferredRewards(
        Player owner,
        RelicModel primaryRelic,
        bool shouldGrantBadge
    )
    {
        await Task.Yield();

        await RelicCmd.Obtain(primaryRelic, owner);

        if (shouldGrantBadge)
        {
            await RelicCmd.Obtain<WongoCustomerAppreciationBadge>(owner);
        }
    }

    private static (LocString description, bool shouldGrantBadge) PrepareAfterBuyState(
        WelcomeToWongos evt,
        int pointsEarned
    )
    {
        int totalWongoPoints = SaveManager.Instance.Progress.WongoPoints;
        int currentCyclePoints = totalWongoPoints % 2000;
        int newCyclePoints = currentCyclePoints + pointsEarned;
        int totalAfterPurchase = totalWongoPoints + pointsEarned;

        evt.DynamicVars["WongoPointAmount"].BaseValue = newCyclePoints;
        evt.DynamicVars["RemainingWongoPointAmount"].BaseValue = 2000 - newCyclePoints;
        evt.DynamicVars["TotalWongoBadgeAmount"].BaseValue = totalAfterPurchase / 2000;
        GetOwner(evt).ExtraFields.WongoPoints = pointsEarned;

        if (newCyclePoints >= 2000)
        {
            return (new LocString("events", "WELCOME_TO_WONGOS.pages.AFTER_BUY_RECEIVE_BADGE.description"), true);
        }

        if (evt.DynamicVars["TotalWongoBadgeAmount"].BaseValue > 0m)
        {
            return (new LocString("events", "WELCOME_TO_WONGOS.pages.AFTER_BUY_BADGE_COUNTER.description"), false);
        }

        return (new LocString("events", "WELCOME_TO_WONGOS.pages.AFTER_BUY.description"), false);
    }

    private static Player GetOwner(EventModel evt)
    {
        return (Player)EventOwnerProperty.GetValue(evt)!;
    }

    private static void FinishEvent(EventModel evt, LocString description)
    {
        SetEventFinishedMethod.Invoke(evt, new object[] { description });
    }
}
