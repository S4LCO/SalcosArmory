using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SalcosArmory.Runtime;

internal static class ItemSlotRules
{
    public static bool Allows(Slot slot, MongoId itemTpl)
    {
        var filters = slot.Properties?.Filters;
        if (filters is null || !filters.Any())
        {
            return true;
        }

        return filters.Any(filter => filter?.Filter?.Contains(itemTpl) == true);
    }
}
