using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace AllRelicsBecomeOneRelic;

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
internal static class RelicCmdObtainPatch
{
    private static void Prefix(ref RelicModel relic, Player player)
    {
        relic = RelicReplacementService.ReplaceObtainedRelic(relic, player, "RelicCmd.Obtain");
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.CreateForNewRun), new[] { typeof(CharacterModel), typeof(UnlockState), typeof(ulong) })]
internal static class PlayerCreateForNewRunPatch
{
    private static void Postfix(Player __result)
    {
        RelicReplacementService.ReplaceStarterRelics(__result);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics))]
internal static class RunManagerFinalizeStartingRelicsPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        __result = RelicReplacementService.FinalizeStartingRelicsSafely(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Ready))]
internal static class NGameReadyPatch
{
    private static void Postfix(NGame __instance)
    {
        RelicReplacementOverlay.EnsureAttached(__instance);
    }
}

[HarmonyPatch(typeof(NPotionContainer), "GrowPotionHolders")]
internal static class NPotionContainerGrowPotionHoldersPatch
{
    private static void Postfix(NPotionContainer __instance, int newMaxPotionSlots)
    {
        PotionLayoutCompat.ApplyAdaptiveLayout(__instance, newMaxPotionSlots);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class RunManagerEnterActPatch
{
    private static void Postfix(int currentActIndex, ref Task __result)
    {
        if (currentActIndex == 0)
        {
            __result = RunAfterEnterAct(__result);
        }
    }

    private static async Task RunAfterEnterAct(Task originalTask)
    {
        await originalTask;
        await RelicReplacementService.RunDeferredStarterEffects();
    }
}

[HarmonyPatch(typeof(NPotionContainer), nameof(NPotionContainer.Initialize))]
internal static class NPotionContainerInitializePatch
{
    private static void Postfix(NPotionContainer __instance, IRunState runState)
    {
        Player? player = MegaCrit.Sts2.Core.Context.LocalContext.GetMe(runState);
        if (player != null)
        {
            PotionLayoutCompat.ApplyAdaptiveLayout(__instance, player.MaxPotionCount);
        }
    }
}

[HarmonyPatch(typeof(HatchRestSiteOption), nameof(HatchRestSiteOption.OnSelect))]
internal static class HatchRestSiteOptionPatch
{
    private static readonly PropertyInfo RestSiteOwnerProperty =
        AccessTools.Property(typeof(RestSiteOption), "Owner")!;

    private static bool Prefix(HatchRestSiteOption __instance, ref Task<bool> __result)
    {
        __result = ConsumeEggThenHatch(__instance);
        return false;
    }

    private static async Task<bool> ConsumeEggThenHatch(HatchRestSiteOption option)
    {
        Player owner = (Player)RestSiteOwnerProperty.GetValue(option)!;
        List<CardModel> eggs = owner.Deck.Cards.Where(static card => card is ByrdonisEgg).ToList();
        if (eggs.Count == 0)
        {
            return true;
        }

        foreach (CardModel egg in eggs)
        {
            egg.RemoveFromState();
            owner.RunState.RemoveCard(egg);
        }

        await RelicCmd.Obtain<Byrdpip>(owner);
        return true;
    }
}

[HarmonyPatch]
internal static class RoomFullOfCheeseSearchPatch
{
    private static readonly PropertyInfo EventOwnerProperty =
        AccessTools.Property(typeof(EventModel), "Owner")!;

    private static readonly MethodInfo SetEventFinishedMethod =
        AccessTools.Method(typeof(EventModel), "SetEventFinished")!;

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(RoomFullOfCheese), "Search")!;
    }

    private static bool Prefix(RoomFullOfCheese __instance, ref Task __result)
    {
        __result = RunSearchCompat(__instance);
        return false;
    }

    private static async Task RunSearchCompat(RoomFullOfCheese roomFullOfCheese)
    {
        Player owner = (Player)EventOwnerProperty.GetValue(roomFullOfCheese)!;
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            roomFullOfCheese.DynamicVars.Damage,
            null,
            null
        );

        SetEventFinishedMethod.Invoke(
            roomFullOfCheese,
            new object[] { new MegaCrit.Sts2.Core.Localization.LocString("events", "ROOM_FULL_OF_CHEESE.pages.SEARCH.description") }
        );

        TaskHelper.RunSafely(GrantCheeseRewardAfterEvent(owner));
    }

    private static async Task GrantCheeseRewardAfterEvent(Player owner)
    {
        await Task.Yield();
        await RelicCmd.Obtain<ChosenCheese>(owner);
    }
}

[HarmonyPatch]
internal static class TeaMasterBoneTeaPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(TeaMaster), "BoneTea")!;
    }

    private static bool Prefix(TeaMaster __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunTeaMasterBoneTea(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class TeaMasterEmberTeaPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(TeaMaster), "EmberTea")!;
    }

    private static bool Prefix(TeaMaster __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunTeaMasterEmberTea(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class TeaMasterDiscourtesyPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(TeaMaster), "TeaOfDiscourtesy")!;
    }

    private static bool Prefix(TeaMaster __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunTeaMasterDiscourtesy(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class ColossalFlowerPollinousCorePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(ColossalFlower), "ObtainPollinousCore")!;
    }

    private static bool Prefix(ColossalFlower __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunColossalFlowerPollinousCore(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class GraveOfTheForgottenGenerateInitialOptionsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(GraveOfTheForgotten), "GenerateInitialOptions")!;
    }

    private static void Postfix(GraveOfTheForgotten __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__result.Count < 2)
        {
            return;
        }

        __result = new EventOption[]
        {
            __result[0],
            new EventOption(
                __instance,
                () => EventRewardCompat.RunGraveOfTheForgottenAccept(__instance),
                "GRAVE_OF_THE_FORGOTTEN.pages.INITIAL.options.ACCEPT",
                HoverTipFactory.FromRelic<ForgottenSoul>()
            )
        };
    }
}

[HarmonyPatch]
internal static class HungryForMushroomsGenerateInitialOptionsPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(HungryForMushrooms), "GenerateInitialOptions")!;
    }

    private static bool Prefix(HungryForMushrooms __instance, ref IReadOnlyList<EventOption> __result)
    {
        RelicModel bigMushroom = ModelDb.Relic<BigMushroom>().ToMutable();
        bigMushroom.Owner = __instance.Owner;

        RelicModel fragrantMushroom = ModelDb.Relic<FragrantMushroom>().ToMutable();
        fragrantMushroom.Owner = __instance.Owner;

        __result = new EventOption[]
        {
            EventOption.FromRelic(
                bigMushroom,
                __instance,
                () => EventRewardCompat.RunHungryForMushroomsBig(__instance),
                "HUNGRY_FOR_MUSHROOMS.pages.INITIAL.options.BIG_MUSHROOM"
            ),
            EventOption.FromRelic(
                fragrantMushroom,
                __instance,
                () => EventRewardCompat.RunHungryForMushroomsFragrant(__instance),
                "HUNGRY_FOR_MUSHROOMS.pages.INITIAL.options.FRAGRANT_MUSHROOM"
            ).ThatDoesDamage(15m)
        };

        return false;
    }
}

[HarmonyPatch]
internal static class HungryForMushroomsBigMushroomPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(HungryForMushrooms), "BigMushroom")!;
    }

    private static bool Prefix(HungryForMushrooms __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunHungryForMushroomsBig(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class HungryForMushroomsFragrantMushroomPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(HungryForMushrooms), "FragrantMushroom")!;
    }

    private static bool Prefix(HungryForMushrooms __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunHungryForMushroomsFragrant(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class RoundTeaPartyEnjoyTeaPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(RoundTeaParty), "EnjoyTea")!;
    }

    private static bool Prefix(RoundTeaParty __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunRoundTeaPartyEnjoyTea(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class RoundTeaPartyContinueFightPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(RoundTeaParty), "ContinueFight")!;
    }

    private static bool Prefix(RoundTeaParty __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunRoundTeaPartyContinueFight(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class SunkenStatueGrabSwordPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(SunkenStatue), "GrabSword")!;
    }

    private static bool Prefix(SunkenStatue __instance, ref Task __result)
    {
        __result = EventRewardCompat.RunSunkenStatueGrabSword(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
internal static class NRestSiteRoomUpdateRestSiteOptionsPatch
{
    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ChoicesContainerRef =
        AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesContainer");

    private static readonly PropertyInfo RestSiteOwnerProperty =
        AccessTools.Property(typeof(RestSiteOption), "Owner")!;

    private static void Postfix(NRestSiteRoom __instance)
    {
        Control choicesContainer = ChoicesContainerRef(__instance);
        List<NRestSiteButton> buttons = choicesContainer.GetChildren().OfType<NRestSiteButton>().ToList();
        foreach (NRestSiteButton button in buttons)
        {
            if (button.Option is HatchRestSiteOption hatchOption && !HasByrdonisEgg((Player)RestSiteOwnerProperty.GetValue(hatchOption)!))
            {
                button.QueueFree();
            }
        }
    }

    private static bool HasByrdonisEgg(Player player)
    {
        return player.Deck.Cards.Any(static card => card is ByrdonisEgg);
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom.AfterSelectingOption))]
internal static class NRestSiteRoomAfterSelectingOptionPatch
{
    private static readonly MethodInfo UpdateRestSiteOptionsMethod =
        AccessTools.Method(typeof(NRestSiteRoom), "UpdateRestSiteOptions")!;

    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ChoicesContainerRef =
        AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesContainer");

    private static void Postfix(NRestSiteRoom __instance, RestSiteOption option)
    {
        if (option is HatchRestSiteOption)
        {
            TaskHelper.RunSafely(CleanupHatchUiAndOptions(__instance));
        }
    }

    private static async Task CleanupHatchUiAndOptions(NRestSiteRoom room)
    {
        if (!room.IsInsideTree())
        {
            return;
        }

        await room.ToSignal(room.GetTree(), SceneTree.SignalName.ProcessFrame);
        IReadOnlyList<RestSiteOption> options = RunManager.Instance.RestSiteSynchronizer.GetLocalOptions();
        if (options is List<RestSiteOption> mutableOptions)
        {
            mutableOptions.RemoveAll(static option => option is HatchRestSiteOption);
        }

        UpdateRestSiteOptionsMethod.Invoke(room, null);

        Control choicesContainer = ChoicesContainerRef(room);
        foreach (NRestSiteButton button in choicesContainer.GetChildren().OfType<NRestSiteButton>().ToList())
        {
            if (button.Option is HatchRestSiteOption)
            {
                button.QueueFree();
            }
        }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class NGameInputPatch
{
    private static void Postfix(NGame __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode == Key.F8)
        {
            RelicReplacementOverlay.Toggle(__instance);
            __instance.GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.Escape)
        {
            if (RelicReplacementOverlay.ToggleEscape(__instance))
            {
                __instance.GetViewport().SetInputAsHandled();
            }
        }
    }
}

[HarmonyPatch]
internal static class WelcomeToWongosBuyBargainBinPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(WelcomeToWongos), "BuyBargainBin")!;
    }

    private static bool Prefix(WelcomeToWongos __instance, ref Task __result)
    {
        __result = WelcomeToWongosCompat.RunBuyBargainBin(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class WelcomeToWongosBuyFeaturedItemPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(WelcomeToWongos), "BuyFeaturedItem")!;
    }

    private static bool Prefix(WelcomeToWongos __instance, ref Task __result)
    {
        __result = WelcomeToWongosCompat.RunBuyFeaturedItem(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class WelcomeToWongosBuyMysteryBoxPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(WelcomeToWongos), "BuyMysteryBox")!;
    }

    private static bool Prefix(WelcomeToWongos __instance, ref Task __result)
    {
        __result = WelcomeToWongosCompat.RunBuyMysteryBox(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(RelicFactory), nameof(RelicFactory.PullNextRelicFromFront), new[] { typeof(Player), typeof(RelicRarity) })]
internal static class RelicFactoryFrontPatch
{
    private static void Postfix(ref RelicModel __result)
    {
        __result = RelicReplacementService.ReplacePreviewCanonical(__result, "RelicFactory.PullNextRelicFromFront");
    }
}

[HarmonyPatch(typeof(RelicFactory), nameof(RelicFactory.PullNextRelicFromBack), new[] { typeof(Player), typeof(RelicRarity), typeof(IEnumerable<RelicModel>) })]
internal static class RelicFactoryBackPatch
{
    private static void Postfix(ref RelicModel __result)
    {
        __result = RelicReplacementService.ReplacePreviewCanonical(__result, "RelicFactory.PullNextRelicFromBack");
    }
}

[HarmonyPatch(typeof(RelicReward), MethodType.Constructor, new[] { typeof(RelicModel), typeof(Player) })]
internal static class RelicRewardConstructorPatch
{
    private static void Prefix(ref RelicModel relic)
    {
        relic = RelicReplacementService.ReplacePreviewMutable(relic, "RelicReward..ctor");
    }
}

[HarmonyPatch(typeof(MerchantRelicEntry), "SetModel")]
internal static class MerchantRelicEntrySetModelPatch
{
    private static void Prefix(ref RelicModel model)
    {
        model = RelicReplacementService.ReplacePreviewMutable(model, "MerchantRelicEntry.SetModel");
    }
}

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class TreasureRoomRelicSynchronizerPatch
{
    private static readonly AccessTools.FieldRef<TreasureRoomRelicSynchronizer, List<RelicModel>?> CurrentRelicsRef =
        AccessTools.FieldRefAccess<TreasureRoomRelicSynchronizer, List<RelicModel>?>("_currentRelics");

    private static void Postfix(TreasureRoomRelicSynchronizer __instance)
    {
        RelicReplacementService.ReplaceTreasurePreviewRelics(
            CurrentRelicsRef(__instance),
            "TreasureRoomRelicSynchronizer.BeginRelicPicking"
        );
    }
}

[HarmonyPatch(typeof(RelicSelectCmd), nameof(RelicSelectCmd.FromChooseARelicScreen))]
internal static class RelicSelectCmdPatch
{
    private static void Prefix(ref IReadOnlyList<RelicModel> relics)
    {
        relics = RelicReplacementService.ReplacePreviewChoiceList(
            relics,
            "RelicSelectCmd.FromChooseARelicScreen"
        );
    }
}

[HarmonyPatch(typeof(HoverTipFactory), nameof(HoverTipFactory.FromRelic), new[] { typeof(RelicModel) })]
internal static class HoverTipFactoryFromRelicPatch
{
    private static void Prefix(ref RelicModel relic)
    {
        relic = RelicReplacementService.ReplacePreviewCanonical(relic, "HoverTipFactory.FromRelic");
    }
}

[HarmonyPatch(typeof(HoverTipFactory), nameof(HoverTipFactory.FromRelicExcludingItself), new[] { typeof(RelicModel) })]
internal static class HoverTipFactoryFromRelicExcludingItselfPatch
{
    private static void Prefix(ref RelicModel relic)
    {
        relic = RelicReplacementService.ReplacePreviewCanonical(
            relic,
            "HoverTipFactory.FromRelicExcludingItself"
        );
    }
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.FromRelic))]
internal static class EventOptionFromRelicPatch
{
    private static void Prefix(ref RelicModel relic)
    {
        relic = RelicReplacementService.ReplacePreviewMutable(relic, "EventOption.FromRelic");
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.ToMutable))]
internal static class RelicModelToMutablePatch
{
    private static bool Prefix(RelicModel __instance, ref RelicModel __result)
    {
        if (RelicReplacementService.TryReturnMutableSelf(__instance, out RelicModel result))
        {
            __result = result;
            return false;
        }

        return true;
    }
}

[HarmonyPatch]
internal static class DollRoomOptionFromChoicePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(DollRoom), "OptionFromChoice")!;
    }

    private static void Postfix(ref EventOption __result)
    {
        RelicReplacementService.RewriteEventOptionPreview(__result, "RelicName", "DollRoom.OptionFromChoice");
    }
}
