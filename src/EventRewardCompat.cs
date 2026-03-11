using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllRelicsBecomeOneRelic;

internal static class EventRewardCompat
{
    private static readonly MethodInfo SetEventFinishedMethod =
        AccessTools.Method(typeof(EventModel), "SetEventFinished")!;

    private static readonly PropertyInfo EventOwnerProperty =
        AccessTools.Property(typeof(EventModel), "Owner")!;

    internal static async Task RunTeaMasterBoneTea(TeaMaster evt)
    {
        Player owner = GetOwner(evt);
        await PlayerCmd.LoseGold(evt.DynamicVars["BoneTeaCost"].BaseValue, owner, GoldLossType.Spent);
        FinishEvent(evt, new LocString("events", "TEA_MASTER.pages.DONE.description"));
        TaskHelper.RunSafely(GrantRelicLater<BoneTea>(owner));
    }

    internal static async Task RunTeaMasterEmberTea(TeaMaster evt)
    {
        Player owner = GetOwner(evt);
        await PlayerCmd.LoseGold(evt.DynamicVars["EmberTeaCost"].BaseValue, owner, GoldLossType.Spent);
        FinishEvent(evt, new LocString("events", "TEA_MASTER.pages.DONE.description"));
        TaskHelper.RunSafely(GrantRelicLater<EmberTea>(owner));
    }

    internal static Task RunTeaMasterDiscourtesy(TeaMaster evt)
    {
        Player owner = GetOwner(evt);
        FinishEvent(evt, new LocString("events", "TEA_MASTER.pages.TEA_OF_DISCOURTESY.description"));
        TaskHelper.RunSafely(GrantRelicLater<TeaOfDiscourtesy>(owner));
        return Task.CompletedTask;
    }

    internal static async Task RunColossalFlowerPollinousCore(ColossalFlower evt)
    {
        Player owner = GetOwner(evt);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            7m,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable | MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
            null,
            null
        );
        FinishEvent(evt, new LocString("events", "COLOSSAL_FLOWER.pages.POLLINOUS_CORE.description"));
        TaskHelper.RunSafely(GrantRelicLater<PollinousCore>(owner));
    }

    internal static Task RunHungryForMushroomsBig(HungryForMushrooms evt)
    {
        Player owner = GetOwner(evt);
        FinishEvent(evt, new LocString("events", "HUNGRY_FOR_MUSHROOMS.pages.BIG_MUSHROOM.description"));
        TaskHelper.RunSafely(GrantRelicLater<BigMushroom>(owner));
        return Task.CompletedTask;
    }

    internal static Task RunHungryForMushroomsFragrant(HungryForMushrooms evt)
    {
        Player owner = GetOwner(evt);
        FinishEvent(evt, new LocString("events", "HUNGRY_FOR_MUSHROOMS.pages.FRAGRANT_MUSHROOM.description"));
        TaskHelper.RunSafely(GrantRelicLater<FragrantMushroom>(owner));
        return Task.CompletedTask;
    }

    internal static async Task RunRoundTeaPartyEnjoyTea(RoundTeaParty evt)
    {
        Player owner = GetOwner(evt);
        var creature = owner.Creature;
        FinishEvent(evt, new LocString("events", "ROUND_TEA_PARTY.pages.ENJOY_TEA.description"));
        await CreatureCmd.Heal(creature, creature.MaxHp - creature.CurrentHp);
        TaskHelper.RunSafely(GrantRelicLater<RoyalPoison>(owner));
    }

    internal static async Task RunRoundTeaPartyContinueFight(RoundTeaParty evt)
    {
        Player owner = GetOwner(evt);
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), owner.Creature, evt.DynamicVars.Damage, null, null);
        FinishEvent(evt, new LocString("events", "ROUND_TEA_PARTY.pages.CONTINUE_FIGHT.description"));
        TaskHelper.RunSafely(GrantRandomRelicLater(owner));
    }

    internal static Task RunSunkenStatueGrabSword(SunkenStatue evt)
    {
        Player owner = GetOwner(evt);
        FinishEvent(evt, new LocString("events", "SUNKEN_STATUE.pages.GRAB_SWORD.description"));
        TaskHelper.RunSafely(GrantRelicLater<SwordOfStone>(owner));
        return Task.CompletedTask;
    }

    internal static Task RunGraveOfTheForgottenAccept(GraveOfTheForgotten evt)
    {
        Player owner = GetOwner(evt);
        FinishEvent(evt, new LocString("events", "GRAVE_OF_THE_FORGOTTEN.pages.ACCEPT.description"));
        TaskHelper.RunSafely(GrantRelicLater<ForgottenSoul>(owner));
        return Task.CompletedTask;
    }

    private static async Task GrantRelicLater<TRelic>(Player owner) where TRelic : RelicModel
    {
        await Task.Yield();
        await RelicCmd.Obtain<TRelic>(owner);
    }

    private static async Task GrantRandomRelicLater(Player owner)
    {
        await Task.Yield();
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(owner).ToMutable(), owner);
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
