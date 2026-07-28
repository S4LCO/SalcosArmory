using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace SalcosArmory.Client.VitalSurgery;

internal sealed class BoneVitalRestorePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(ActiveHealthController),
            nameof(ActiveHealthController.RestoreBodyPart),
            new[] { typeof(EBodyPart), typeof(float) }
        );
    }

    [PatchTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> originalInstructions)
    {
        var instructions = new List<CodeInstruction>(originalInstructions);
        const int guardLength = 6;

        for (var index = 0; index <= instructions.Count - guardLength; index++)
        {
            if (!IsVitalPartGuard(instructions, index))
            {
                continue;
            }

            var rejectLabel = instructions[index + 1].operand;
            var continueLabel = instructions[index + 4].operand;
            var firstInstruction = new CodeInstruction(OpCodes.Ldarg_0);
            firstInstruction.labels.AddRange(instructions[index].labels);
            firstInstruction.blocks.AddRange(instructions[index].blocks);

            var replacement = new[]
            {
                firstInstruction,
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(
                        typeof(BoneVitalSurgery),
                        nameof(BoneVitalSurgery.CanPassRestoreGuard)
                    )
                ),
                new CodeInstruction(OpCodes.Brtrue, continueLabel),
                new CodeInstruction(OpCodes.Br, rejectLabel)
            };

            instructions.RemoveRange(index, guardLength);
            instructions.InsertRange(index, replacement);
            BoneVitalSurgery.MarkRestorePatchReady(true);
            MedicalMergePlugin.Log.LogInfo(
                "B.O.N.E. vital-surgery guard installed for head and chest."
            );
            return instructions;
        }

        BoneVitalSurgery.MarkRestorePatchReady(false);
        MedicalMergePlugin.Log.LogError(
            "B.O.N.E. vital surgery was disabled: the expected game method was not found. "
            + "Runtime IL begins with: "
            + string.Join(", ", instructions.Take(12).Select(instruction => instruction.opcode.Name))
        );
        return instructions;
    }

    private static bool IsVitalPartGuard(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return instructions[index].opcode == OpCodes.Ldarg_1
            && IsBranchFalse(instructions[index + 1].opcode)
            && instructions[index + 2].opcode == OpCodes.Ldarg_1
            && instructions[index + 3].opcode == OpCodes.Ldc_I4_1
            && IsBranchNotEqual(instructions[index + 4].opcode)
            && IsUnconditionalBranch(instructions[index + 5].opcode);
    }

    private static bool IsBranchFalse(OpCode opcode)
    {
        return opcode == OpCodes.Brfalse || opcode == OpCodes.Brfalse_S;
    }

    private static bool IsBranchNotEqual(OpCode opcode)
    {
        return opcode == OpCodes.Bne_Un || opcode == OpCodes.Bne_Un_S;
    }

    private static bool IsUnconditionalBranch(OpCode opcode)
    {
        return opcode == OpCodes.Br || opcode == OpCodes.Br_S;
    }
}

internal sealed class BoneVitalBodyPartSelectionPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var healthControllerBaseType = typeof(ActiveHealthController).BaseType
            ?? throw new System.MissingMemberException(
                "The ActiveHealthController base type was not found."
            );

        return healthControllerBaseType.GetMethod(
            nameof(ActiveHealthController.TryGetBodyPartToApply),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            null,
            new[] { typeof(Item), typeof(EBodyPart), typeof(EBodyPart?).MakeByRefType() },
            null
        ) ?? throw new System.MissingMethodException(
            healthControllerBaseType.FullName,
            nameof(ActiveHealthController.TryGetBodyPartToApply)
        );
    }

    [PatchPostfix]
    private static void Postfix(
        object __instance,
        Item item,
        ref EBodyPart? damagedBodyPart,
        ref bool __result)
    {
        if (!BoneVitalSurgery.IsBone(item)
            || !(__instance is ActiveHealthController controller)
            || !BoneVitalSurgery.TrySelectDestroyedVitalPart(controller, out var vitalPart))
        {
            return;
        }

        damagedBodyPart = vitalPart;
        __result = true;
        MedicalMergePlugin.Log.LogInfo(
            $"B.O.N.E. selected blacked-out {vitalPart} for vital surgery."
        );
    }
}

internal sealed class BoneVitalMedEffectContextPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var medEffectType = typeof(ActiveHealthController).GetNestedType(
            "MedEffect",
            BindingFlags.NonPublic
        );

        if (medEffectType == null || !BoneVitalSurgery.ConfigureMedEffectAccess(medEffectType))
        {
            throw new System.MissingMemberException(
                "The expected ActiveHealthController.MedEffect fields were not found."
            );
        }

        return AccessTools.Method(medEffectType, "Residue");
    }

    [PatchPrefix]
    private static void Prefix(
        object __instance,
        out BoneVitalSurgery.TreatmentContext __state)
    {
        __state = BoneVitalSurgery.EnterMedEffect(__instance);
    }

    [PatchFinalizer]
    private static System.Exception Finalizer(
        System.Exception __exception,
        BoneVitalSurgery.TreatmentContext __state)
    {
        BoneVitalSurgery.ExitMedEffect(__state);
        return __exception;
    }
}
