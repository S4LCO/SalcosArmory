using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SalcosArmory.Client.StimTextures;

internal sealed class StimInHandsTexturePatch : ModulePatch
{
    private static readonly ConditionalWeakTable<Player.MedsController, AppliedTextureState> States =
        new ConditionalWeakTable<Player.MedsController, AppliedTextureState>();

    private static bool _runtimeErrorLogged;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(Player.MedsController),
            nameof(Player.MedsController.ManualUpdate),
            new[] { typeof(float) }
        );
    }

    [PatchPostfix]
    private static void Postfix(Player.MedsController __instance)
    {
        try
        {
            var state = States.GetValue(__instance, _ => new AppliedTextureState());
            var item = __instance?.GetItem();

            if (item == null
                || !StimTextureCatalog.TryGet(item.TemplateId, out var customTexture))
            {
                state.Restore();
                return;
            }

            state.Apply(
                __instance.ControllerGameObject,
                item.TemplateId,
                customTexture
            );
        }
        catch (Exception ex)
        {
            if (_runtimeErrorLogged)
            {
                return;
            }

            _runtimeErrorLogged = true;
            SalcosArmoryPlugin.Log.LogError(
                $"A stimulant hand texture could not be applied: {ex}"
            );
        }
    }

    private sealed class AppliedTextureState
    {
        private static readonly HashSet<string> SyringeRendererNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "syringe_LOD0",
                "syringe_needle_LOD0",
                "syringe_cap_LOD0"
            };

        private readonly Dictionary<Material, Texture> _originalTextures =
            new Dictionary<Material, Texture>();

        private int _controllerObjectId;
        private string _templateId;
        private Texture2D _customTexture;

        public void Apply(GameObject controllerObject, string templateId, Texture2D customTexture)
        {
            if (controllerObject == null || customTexture == null)
            {
                Restore();
                return;
            }

            var controllerObjectId = controllerObject.GetInstanceID();
            if (_controllerObjectId != controllerObjectId
                || !string.Equals(_templateId, templateId, StringComparison.OrdinalIgnoreCase)
                || _customTexture != customTexture)
            {
                Restore();
                _controllerObjectId = controllerObjectId;
                _templateId = templateId;
                _customTexture = customTexture;
            }

            RemoveDestroyedMaterials();
            if (_originalTextures.Count == 0)
            {
                FindSyringeMaterials(controllerObject);
            }

            foreach (var material in _originalTextures.Keys)
            {
                if (material != null && material.mainTexture != customTexture)
                {
                    material.mainTexture = customTexture;
                }
            }
        }

        public void Restore()
        {
            foreach (var materialAndTexture in _originalTextures)
            {
                var material = materialAndTexture.Key;
                if (material != null && material.mainTexture == _customTexture)
                {
                    material.mainTexture = materialAndTexture.Value;
                }
            }

            _originalTextures.Clear();
            _controllerObjectId = 0;
            _templateId = null;
            _customTexture = null;
        }

        private void FindSyringeMaterials(GameObject controllerObject)
        {
            foreach (var renderer in controllerObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !SyringeRendererNames.Contains(renderer.name))
                {
                    continue;
                }

                foreach (var material in renderer.materials)
                {
                    if (material != null && !_originalTextures.ContainsKey(material))
                    {
                        _originalTextures.Add(material, material.mainTexture);
                    }
                }
            }
        }

        private void RemoveDestroyedMaterials()
        {
            if (_originalTextures.Count == 0)
            {
                return;
            }

            var destroyedMaterials = new List<Material>();
            foreach (var material in _originalTextures.Keys)
            {
                if (material == null)
                {
                    destroyedMaterials.Add(material);
                }
            }

            foreach (var material in destroyedMaterials)
            {
                _originalTextures.Remove(material);
            }
        }
    }
}
