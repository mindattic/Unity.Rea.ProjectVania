using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>
    /// The material every world sprite is drawn with (RECONSTRUCTION 6.1): <c>Resources/Shaders/WorldEffect</c> with the
    /// keyword of effects.json's preset and every shader parameter from its data, so each sprite is shaded in its own
    /// texture coordinates as <c>SceneManager</c> wrapped each one in <c>filter</c>. The map's cells are painted through
    /// Core's <see cref="SpriteEffect"/> (the same arithmetic) and are drawn without it.
    /// </summary>
    public static class WorldEffectMaterial
    {
        public const string ShaderPath = "Shaders/WorldEffect";
        public const string ShaderName = "ProjectVania/WorldEffect";

        /// <summary>Each preset's shader keyword, in <see cref="EffectPreset"/> order.</summary>
        public static readonly string[] Keywords = { "_EFFECT_FILTER", "_EFFECT_NEGATIVE", "_EFFECT_BLOOM", "_EFFECT_PAUSE", "_EFFECT_WATER" };

        public static string KeywordOf(EffectPreset preset) => Keywords[(int)preset];

        /// <summary>A new material drawing <paramref name="preset"/> with <paramref name="data"/>'s parameters, or null (logged) without the shader.</summary>
        public static Material Create(EffectPreset preset, EffectData data)
        {
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[ProjectVania] The world effect shader {ShaderPath} is missing; the world draws without it.");
                return null;
            }

            var material = new Material(shader) { name = $"WorldEffect ({preset})" };
            foreach (var keyword in Keywords)
            {
                material.DisableKeyword(keyword);
            }

            material.EnableKeyword(KeywordOf(preset));
            material.SetFloat("_Burn", data.Filter.Burn);
            material.SetFloat("_Saturation", data.Filter.Saturation);
            material.SetVector("_Gain", new Vector4(data.Filter.R, data.Filter.G, data.Filter.B, 0f));
            material.SetFloat("_Brite", data.Filter.Brite);
            material.SetFloat("_BloomV", data.Bloom.V);
            material.SetFloat("_BloomA", data.Bloom.A);
            material.SetFloat("_Horizon", data.Water.Horizon);
            material.SetFloat("_Delta", data.Water.Delta);
            material.SetFloat("_Theta", data.Water.Theta);
            return material;
        }

        /// <summary>The material of the effect effects.json names for the world.</summary>
        public static Material World(EffectData data) => Create(data.World, data);

        /// <summary>
        /// A name's colour through <paramref name="effect"/>: the names are UI Toolkit text in Unity's own font, not the
        /// original's sprite-font atlas, so a glyph has no place in that atlas to vignette by; each name is shaded as one
        /// white texel at the middle of its texture, which applies every colour term of the effect to its tint.
        /// </summary>
        public static Rgba NameColour(SpriteEffect effect, Rgba tint)
        {
            if (effect == null)
            {
                return tint;
            }

            var white = new PixelImage(1, 1);
            white[0, 0] = Rgba.White;
            return effect.Shade(white, 0.5f, 0.5f, tint);
        }
    }
}
