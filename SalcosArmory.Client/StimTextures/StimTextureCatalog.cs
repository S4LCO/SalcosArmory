using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SalcosArmory.Client.StimTextures;

internal static class StimTextureCatalog
{
    private static readonly IReadOnlyDictionary<string, string> TextureFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["6a46c6bd54c2760498b84f47"] = "stimulator_bone_d.png",
            ["6a46c750a758d8abcbb100c2"] = "stimulator_dinner_d.png",
            ["6a46c75c9b26627eb77350fa"] = "stimulator_msj6_d.png",
            ["6a46c77a6744eaae5caca0a8"] = "stimulator_ptg_d.png"
        };

    private static readonly Dictionary<string, Texture2D> Textures =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private static MethodInfo _loadImageMethod;
    private static bool _initialized;

    public static bool Initialize(string pluginAssemblyPath)
    {
        if (_initialized)
        {
            return Textures.Count > 0;
        }

        _initialized = true;
        var pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath);
        var textureDirectory = string.IsNullOrWhiteSpace(pluginDirectory)
            ? null
            : Path.Combine(pluginDirectory, "textures");

        if (textureDirectory == null || !Directory.Exists(textureDirectory))
        {
            SalcosArmoryPlugin.Log.LogError(
                $"Stim texture folder was not found: {textureDirectory ?? "<unknown>"}"
            );
            return false;
        }

        _loadImageMethod = ResolveLoadImageMethod();
        if (_loadImageMethod == null)
        {
            SalcosArmoryPlugin.Log.LogError(
                "Unity's PNG loader could not be found. Custom stimulant hand textures cannot be loaded."
            );
            return false;
        }

        foreach (var textureFile in TextureFiles)
        {
            LoadTexture(textureDirectory, textureFile.Key, textureFile.Value);
        }

        return Textures.Count > 0;
    }

    public static bool TryGet(string templateId, out Texture2D texture)
    {
        texture = null;
        return !string.IsNullOrWhiteSpace(templateId)
            && Textures.TryGetValue(templateId, out texture)
            && texture != null;
    }

    private static void LoadTexture(string directory, string templateId, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            SalcosArmoryPlugin.Log.LogWarning(
                $"Stim hand texture is missing for {templateId}: {path}"
            );
            return;
        }

        Texture2D texture = null;

        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"SALCO_Stim_{templateId}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var loaded = _loadImageMethod.Invoke(
                null,
                new object[] { texture, File.ReadAllBytes(path), false }
            );

            if (!(loaded is bool success) || !success)
            {
                throw new InvalidDataException("Unity rejected the PNG data.");
            }

            Textures[templateId] = texture;
        }
        catch (Exception ex)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            SalcosArmoryPlugin.Log.LogError(
                $"Stim hand texture could not be loaded from '{path}': {ex.Message}"
            );
        }
    }

    private static MethodInfo ResolveLoadImageMethod()
    {
        const string imageConversionTypeName =
            "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule";

        var imageConversionType = Type.GetType(imageConversionTypeName, false);
        if (imageConversionType == null)
        {
            try
            {
                Assembly.Load("UnityEngine.ImageConversionModule");
                imageConversionType = Type.GetType(imageConversionTypeName, false);
            }
            catch
            {
                return null;
            }
        }

        return imageConversionType?.GetMethod(
            "LoadImage",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
            null
        );
    }
}
