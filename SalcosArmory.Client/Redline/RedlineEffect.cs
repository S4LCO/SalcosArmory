using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using UnityEngine;

namespace SalcosArmory.Client.Redline;

internal static class RedlineEffect
{
    internal const string TemplateId = "01bdffafa9276a56b4384b2c";
    internal const float HealthBonusFraction = 0.15f;
    internal const float DurationSeconds = 45f;

    private static readonly EBodyPart[] BodyParts =
    {
        EBodyPart.Head,
        EBodyPart.Chest,
        EBodyPart.Stomach,
        EBodyPart.LeftArm,
        EBodyPart.RightArm,
        EBodyPart.LeftLeg,
        EBodyPart.RightLeg
    };

    private static readonly Dictionary<EBodyPart, float> OriginalMaximums = new();

    private static ActiveHealthController _controller;
    private static float _expiresAt;
    private static bool _active;

    internal static bool IsRedline(Item item)
    {
        return item != null
            && string.Equals(item.TemplateId, TemplateId, StringComparison.OrdinalIgnoreCase);
    }

    internal static void TryActivate(Item item, ActiveHealthController controller)
    {
        if (!IsRedline(item)
            || controller == null
            || !controller.IsAlive
            || controller.Player == null
            || !controller.Player.IsYourPlayer)
        {
            return;
        }

        if (_active && ReferenceEquals(controller, _controller))
        {
            SalcosArmoryPlugin.Log.LogWarning(
                "E.F.-1 REDLINE was used while its health buffer was already active. "
                + "The buffer does not stack or refresh."
            );
            return;
        }

        if (_active)
        {
            Restore("the active health controller changed");
        }

        OriginalMaximums.Clear();

        try
        {
            foreach (var bodyPart in BodyParts)
            {
                if (!controller.BodyState.TryGetValue(bodyPart, out var state)
                    || state?.Health == null
                    || state.Health.Maximum <= 0f)
                {
                    continue;
                }

                OriginalMaximums[bodyPart] = state.Health.Maximum;
            }

            if (OriginalMaximums.Count != BodyParts.Length)
            {
                throw new InvalidOperationException(
                    $"Expected {BodyParts.Length} body parts, found {OriginalMaximums.Count}."
                );
            }

            foreach (var entry in OriginalMaximums)
            {
                var state = controller.BodyState[entry.Key];
                var originalMaximum = entry.Value;
                var boostedMaximum = originalMaximum * (1f + HealthBonusFraction);
                var currentHealth = state.Health.Current;

                state.Health.Value.Maximum = boostedMaximum;

                // Preserve the body's current health percentage without reviving blacked parts.
                if (currentHealth > 0f)
                {
                    state.Health.Value.Current = Math.Min(
                        boostedMaximum,
                        currentHealth * (boostedMaximum / originalMaximum)
                    );
                }

                controller.NetworkSyncBodyHealth(entry.Key);
            }

            _controller = controller;
            _expiresAt = Time.realtimeSinceStartup + DurationSeconds;
            _active = true;

            SalcosArmoryPlugin.Log.LogInfo(
                $"E.F.-1 REDLINE health buffer activated: +{HealthBonusFraction:P0} maximum health "
                + $"for {DurationSeconds:0} seconds."
            );
        }
        catch (Exception ex)
        {
            _controller = controller;
            _active = OriginalMaximums.Count > 0;
            Restore("activation failed");
            SalcosArmoryPlugin.Log.LogError($"E.F.-1 REDLINE activation failed: {ex}");
        }
    }

    internal static void Update()
    {
        if (!_active)
        {
            return;
        }

        if (_controller == null || !_controller.IsAlive)
        {
            Restore("the player died");
            return;
        }

        if (!Singleton<GameWorld>.Instantiated
            || Singleton<GameWorld>.Instance?.MainPlayer == null
            || !ReferenceEquals(Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController, _controller))
        {
            Restore("the raid ended");
            return;
        }

        if (Time.realtimeSinceStartup >= _expiresAt)
        {
            Restore("the 45-second duration expired");
        }
    }

    internal static void Shutdown()
    {
        Restore("the client plugin was unloaded");
    }

    private static void Restore(string reason)
    {
        var controller = _controller;
        var restoredParts = 0;

        if (controller != null)
        {
            foreach (var entry in OriginalMaximums)
            {
                if (!controller.BodyState.TryGetValue(entry.Key, out var state)
                    || state?.Health == null)
                {
                    continue;
                }

                state.Health.Value.Maximum = entry.Value;

                // Expiry removes only the temporary buffer. It never inflicts direct damage.
                if (state.Health.Current > entry.Value)
                {
                    state.Health.Value.Current = entry.Value;
                }

                try
                {
                    controller.NetworkSyncBodyHealth(entry.Key);
                }
                catch (Exception ex)
                {
                    SalcosArmoryPlugin.Log.LogWarning(
                        $"E.F.-1 REDLINE could not sync {entry.Key} while restoring health: {ex.Message}"
                    );
                }

                restoredParts++;
            }
        }

        OriginalMaximums.Clear();
        _controller = null;
        _expiresAt = 0f;
        _active = false;

        if (restoredParts > 0)
        {
            SalcosArmoryPlugin.Log.LogInfo(
                $"E.F.-1 REDLINE health buffer removed ({reason}); restored {restoredParts} body parts."
            );
        }
    }
}
