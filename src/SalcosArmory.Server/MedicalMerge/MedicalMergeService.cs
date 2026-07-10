using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace SalcosArmory.MedicalMerge;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeService(
    EventOutputHolder eventOutputHolder,
    InventoryHelper inventoryHelper,
    HttpResponseUtil httpResponseUtil,
    DatabaseService databaseService,
    ISptLogger<MedicalMergeService> logger)
{
    private const double ResourceEpsilon = 0.001d;

    public ValueTask<ItemEventRouterResponse> Merge(
        PmcData pmcData,
        MedicalMergeRequest? request,
        string sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);

        if (request?.SourceItem is null || request.TargetItem is null)
        {
            return Fail(output, "Medical merge failed: sourceItem or targetItem is missing.");
        }

        var sourceId = request.SourceItem.Value;
        var targetId = request.TargetItem.Value;

        if (sourceId == targetId)
        {
            return Fail(output, "Medical merge failed: source and target are the same item.");
        }

        var inventoryItems = pmcData.Inventory?.Items;
        var source = inventoryItems?.FirstOrDefault(item => item.Id == sourceId);
        var target = inventoryItems?.FirstOrDefault(item => item.Id == targetId);

        if (source is null || target is null)
        {
            return Fail(output, "Medical merge failed: source or target was not found in the profile inventory.");
        }

        if (source.Template != target.Template)
        {
            return Fail(output, "Medical merge failed: only identical medical items can be merged.");
        }

        if (!TryGetMaximumResource(source.Template, out var maximumResource))
        {
            return Fail(output, "Medical merge failed: the item has no valid medical resource.");
        }

        source.AddUpd();
        target.AddUpd();

        var sourceResource = GetResource(source, maximumResource);
        var targetResource = GetResource(target, maximumResource);

        if (sourceResource <= ResourceEpsilon)
        {
            return Fail(output, "Medical merge failed: the source item is empty.");
        }

        if (targetResource >= maximumResource - ResourceEpsilon)
        {
            return Fail(output, "Medical merge failed: the target item is already full.");
        }

        var requestedAmount = request.TransferAmount > 0f
            ? request.TransferAmount
            : sourceResource;

        var transferAmount = Math.Min(
            requestedAmount,
            Math.Min(sourceResource, maximumResource - targetResource)
        );

        if (!double.IsFinite(transferAmount) || transferAmount <= ResourceEpsilon)
        {
            return Fail(output, "Medical merge failed: nothing can be transferred.");
        }

        var sourceRemaining = Clamp(sourceResource - transferAmount, maximumResource);
        source.Upd!.MedKit!.HpResource = sourceRemaining <= ResourceEpsilon ? 0d : sourceRemaining;
        target.Upd!.MedKit!.HpResource = Clamp(targetResource + transferAmount, maximumResource);

        if (source.Upd.MedKit.HpResource <= ResourceEpsilon)
        {
            inventoryHelper.RemoveItem(pmcData, sourceId, sessionId);
        }

        logger.Info(Log.Line(
            $"Merged {transferAmount:0.##}/{maximumResource:0.##} medical resource for tpl '{source.Template}'."
        ));

        return ValueTask.FromResult(output);
    }

    private ValueTask<ItemEventRouterResponse> Fail(ItemEventRouterResponse output, string message)
    {
        logger.Warning(Log.Line(message));
        return ValueTask.FromResult(httpResponseUtil.AppendErrorToOutput(output, message));
    }

    private bool TryGetMaximumResource(MongoId templateId, out double maximumResource)
    {
        maximumResource = 0d;

        if (!databaseService.GetItems().TryGetValue(templateId, out var template) || template.Properties is null)
        {
            return false;
        }

        return TryReadNumber(template.Properties, out maximumResource, "MaxHpResource", "HpResource", "MaxResource")
            && maximumResource > 0d
            && double.IsFinite(maximumResource);
    }

    private static double GetResource(Item item, double maximumResource)
    {
        item.Upd!.MedKit ??= new UpdMedKit { HpResource = maximumResource };
        item.Upd.MedKit.HpResource = Clamp(item.Upd.MedKit.HpResource, maximumResource);
        return item.Upd.MedKit.HpResource ?? 0d;
    }

    private static double Clamp(double? value, double maximumResource)
    {
        var resource = value ?? 0d;
        return Math.Clamp(resource, 0d, maximumResource);
    }

    private static bool TryReadNumber(
        TemplateItemProperties properties,
        out double value,
        params string[] propertyNames)
    {
        value = 0d;
        var type = properties.GetType();

        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase
            );

            if (property?.GetValue(properties) is not { } rawValue)
            {
                continue;
            }

            try
            {
                value = Convert.ToDouble(rawValue);
                return true;
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
            catch (OverflowException)
            {
            }
        }

        return false;
    }
}
