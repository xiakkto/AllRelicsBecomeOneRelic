using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace AllRelicsBecomeOneRelic;

internal static class PotionLayoutCompat
{
    private static readonly AccessTools.FieldRef<NPotionContainer, Control> PotionHoldersRef =
        AccessTools.FieldRefAccess<NPotionContainer, Control>("_potionHolders");

    private static readonly AccessTools.FieldRef<NPotionHolder, Vector2> PotionScaleRef =
        AccessTools.FieldRefAccess<NPotionHolder, Vector2>("_potionScale");

    private static readonly AccessTools.FieldRef<NPotionHolder, TextureRect> EmptyIconRef =
        AccessTools.FieldRefAccess<NPotionHolder, TextureRect>("_emptyIcon");

    private static readonly Dictionary<ulong, Vector2> OriginalHolderSize = new();

    private static readonly Dictionary<ulong, Vector2> OriginalPotionScale = new();

    private static readonly Dictionary<ulong, int> OriginalSeparation = new();

    internal static void ApplyAdaptiveLayout(NPotionContainer container, int slotCount)
    {
        TaskHelper.RunSafely(ApplyAdaptiveLayoutDeferred(container, slotCount));
    }

    private static async Task ApplyAdaptiveLayoutDeferred(NPotionContainer container, int slotCount)
    {
        if (!GodotObject.IsInstanceValid(container))
        {
            return;
        }

        await container.ToSignal(container.GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(container))
        {
            return;
        }

        Control holders = PotionHoldersRef(container);
        if (!GodotObject.IsInstanceValid(holders))
        {
            return;
        }

        List<NPotionHolder> children = holders.GetChildren().OfType<NPotionHolder>().ToList();
        if (children.Count == 0)
        {
            return;
        }

        Control? holderParent = holders.GetParent() as Control;
        float availableWidth = holderParent?.Size.X ?? holders.Size.X;
        if (availableWidth <= 0f)
        {
            availableWidth = holders.GetViewportRect().Size.X * 0.55f;
        }

        int baseSeparation = 0;
        if (holders is BoxContainer box)
        {
            ulong holdersId = holders.GetInstanceId();
            if (!OriginalSeparation.TryGetValue(holdersId, out baseSeparation))
            {
                baseSeparation = box.GetThemeConstant("separation");
                OriginalSeparation[holdersId] = baseSeparation;
            }
        }

        Vector2 firstHolderBaseSize = GetOriginalHolderSize(children[0]);
        float widthRatio = (availableWidth - baseSeparation * Mathf.Max(0, slotCount - 1)) / (firstHolderBaseSize.X * slotCount);
        widthRatio = Mathf.Clamp(widthRatio, 0.32f, 1f);

        holders.Scale = Vector2.One;
        if (holders is BoxContainer scaledBox)
        {
            scaledBox.AddThemeConstantOverride("separation", Mathf.RoundToInt(baseSeparation * widthRatio));
        }

        foreach (NPotionHolder holder in children)
        {
            Vector2 baseSize = GetOriginalHolderSize(holder);
            Vector2 basePotionScale = GetOriginalPotionScale(holder);
            float visualScale = Mathf.Clamp(widthRatio * 1.08f, 0.4f, 1f);

            holder.CustomMinimumSize = baseSize * new Vector2(widthRatio, widthRatio);
            holder.Scale = Vector2.One;
            PotionScaleRef(holder) = basePotionScale * visualScale;

            if (holder.Potion != null)
            {
                holder.Potion.Scale = PotionScaleRef(holder);
                holder.Potion.PivotOffset = holder.Potion.Size * 0.5f;
            }

            TextureRect emptyIcon = EmptyIconRef(holder);
            if (GodotObject.IsInstanceValid(emptyIcon))
            {
                emptyIcon.Scale = PotionScaleRef(holder);
            }
        }

        if (holders is Container containerNode)
        {
            containerNode.QueueSort();
        }
    }

    private static Vector2 GetOriginalHolderSize(NPotionHolder holder)
    {
        ulong id = holder.GetInstanceId();
        if (OriginalHolderSize.TryGetValue(id, out Vector2 size))
        {
            return size;
        }

        size = holder.CustomMinimumSize;
        if (size.X <= 0.01f || size.Y <= 0.01f)
        {
            size = holder.Size;
        }
        if (size.X <= 0.01f || size.Y <= 0.01f)
        {
            size = holder.GetCombinedMinimumSize();
        }
        if (size.X <= 0.01f || size.Y <= 0.01f)
        {
            size = new Vector2(78f, 118f);
        }

        OriginalHolderSize[id] = size;
        return size;
    }

    private static Vector2 GetOriginalPotionScale(NPotionHolder holder)
    {
        ulong id = holder.GetInstanceId();
        if (OriginalPotionScale.TryGetValue(id, out Vector2 scale))
        {
            return scale;
        }

        scale = PotionScaleRef(holder);
        if (scale == Vector2.Zero)
        {
            scale = Vector2.One * 0.9f;
        }

        OriginalPotionScale[id] = scale;
        return scale;
    }
}
