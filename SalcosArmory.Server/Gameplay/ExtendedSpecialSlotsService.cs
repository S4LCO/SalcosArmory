using System.Security.Cryptography;
using System.Text;
using SalcosArmory.Infrastructure;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Gameplay;

[Injectable(InjectionType.Singleton)]
public sealed class ExtendedSpecialSlotsService(
    TemplateTable templateTable,
    ISptLogger<ExtendedSpecialSlotsService> logger)
{
    private const int FirstAddedSlot = 4;
    private const int LastAddedSlot = 6;
    private const string ModuleName = "Extended special slots";

    public ModuleResult Load()
    {
        try
        {
            var pocketTemplates = templateTable.Items.Values
                .Where(IsSpecialSlotPocketTemplate)
                .ToArray();

            if (pocketTemplates.Length == 0)
            {
                return ModuleResult.Failed(
                    ModuleName,
                    "No compatible Pockets template with the three vanilla special slots was found.");
            }

            var addedSlots = 0;
            foreach (var pockets in pocketTemplates)
            {
                addedSlots += AddMissingSlots(pockets);
            }

            var message = addedSlots == 0
                ? $"All {pocketTemplates.Length} compatible Pockets template(s) already contain six special slots."
                : $"Added {addedSlots} special slot(s) across {pocketTemplates.Length} compatible Pockets template(s).";

            return ModuleResult.Ok(ModuleName, message);
        }
        catch (Exception ex)
        {
            logger.Error(Log.Line($"{ModuleName} failed: {ex}"));
            return ModuleResult.Failed(ModuleName, ex.Message);
        }
    }

    private static bool IsSpecialSlotPocketTemplate(TemplateItem item)
    {
        var slots = item.Properties?.Slots?.ToArray();
        if (slots is null
            || !string.Equals(item.Properties?.Name, "Pockets", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var specialSlotNumbers = slots
            .Where(slot => slot?.Name?.Contains("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true)
            .Select(slot => ReadTrailingNumber(slot!.Name!))
            .ToHashSet();

        return specialSlotNumbers.Contains(1)
            && specialSlotNumbers.Contains(2)
            && specialSlotNumbers.Contains(3);
    }

    private static int AddMissingSlots(TemplateItem pockets)
    {
        var slots = pockets.Properties?.Slots?.Where(slot => slot is not null).ToList();
        if (slots is null)
        {
            return 0;
        }

        var existingNumbers = slots
            .Where(slot => slot.Name?.Contains("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true)
            .Select(slot => ReadTrailingNumber(slot.Name!))
            .ToHashSet();

        var sourceSlot = slots.FirstOrDefault(slot =>
            string.Equals(slot.Name, "SpecialSlot3", StringComparison.OrdinalIgnoreCase));

        if (sourceSlot?.Properties?.Filters is null)
        {
            throw new InvalidOperationException(
                $"Pockets template {pockets.Id} has no usable SpecialSlot3 filter to clone.");
        }

        var added = 0;
        for (var number = FirstAddedSlot; number <= LastAddedSlot; number++)
        {
            if (existingNumbers.Contains(number))
            {
                continue;
            }

            slots.Add(CloneSlot(sourceSlot, pockets.Id, number));
            added++;
        }

        pockets.Properties!.Slots = slots;
        return added;
    }

    private static Slot CloneSlot(Slot source, MongoId parentId, int number)
    {
        return new Slot
        {
            Name = $"SpecialSlot{number}",
            Id = CreateStableSlotId(parentId, number),
            Parent = parentId,
            MaxCount = source.MaxCount,
            Required = source.Required,
            MergeSlotWithChildren = source.MergeSlotWithChildren,
            Prototype = source.Prototype,
            Properties = new SlotProperties
            {
                MaxStackCount = source.Properties?.MaxStackCount,
                Filters = source.Properties?.Filters?.Select(CloneFilter).ToArray()
            }
        };
    }

    private static SlotFilter CloneFilter(SlotFilter source)
    {
        return new SlotFilter
        {
            Shift = source.Shift,
            Locked = source.Locked,
            Plate = source.Plate,
            ArmorColliders = source.ArmorColliders?.ToArray(),
            ArmorPlateColliders = source.ArmorPlateColliders?.ToArray(),
            Filter = source.Filter is null ? null : [.. source.Filter],
            AnimationIndex = source.AnimationIndex,
            MaxStackCount = source.MaxStackCount,
            BluntDamageReduceFromSoftArmor = source.BluntDamageReduceFromSoftArmor
        };
    }

    private static MongoId CreateStableSlotId(MongoId parentId, int number)
    {
        var input = Encoding.UTF8.GetBytes($"{ArmoryInfo.Guid}:{parentId}:SpecialSlot{number}");
        return new MongoId(Convert.ToHexString(SHA256.HashData(input))[..24].ToLowerInvariant());
    }

    private static int ReadTrailingNumber(string value)
    {
        var digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}
