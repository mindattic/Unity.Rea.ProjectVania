using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania.Tests
{
    /// <summary>
    /// The world through the original's effects (U.VAN-US-A15, RECONSTRUCTION 6.1): every world sprite but the cells (which
    /// Core paints through the same effect) draws with the WorldEffect material at effects.json's preset, filter, and its
    /// parameters; the GUI does not; the other four presets exist as materials nothing selects; the names' colours pass
    /// through the effect. <c>unity.ps1 capture</c> checks the shader's pixels against Core's.
    /// </summary>
    public class WorldEffectTests
    {
        static string SettingsFolder => Path.Combine(Application.dataPath, "StreamingAssets", GameSettings.Folder);

        GameHost _host;

        [SetUp]
        public void SetUp()
        {
            _host = Bootstrap.Run(new DirectorySettingsSource(SettingsFolder));
            Assert.That(_host, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            var root = GameObject.Find(Bootstrap.RootName);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void AssertFilter(Material material, string what)
        {
            Assert.That(material, Is.Not.Null, what);
            Assert.That(material.shader.name, Is.EqualTo(WorldEffectMaterial.ShaderName), what);
            Assert.That(material.IsKeywordEnabled("_EFFECT_FILTER"), Is.True, what);
            foreach (var other in WorldEffectMaterial.Keywords.Skip(1))
            {
                Assert.That(material.IsKeywordEnabled(other), Is.False, $"{what}: {other}");
            }

            Assert.That(material.GetFloat("_Burn"), Is.EqualTo(0.01f), what);
            Assert.That(material.GetFloat("_Saturation"), Is.EqualTo(1f), what);
            Assert.That(material.GetVector("_Gain"), Is.EqualTo(new Vector4(1f, 1f, 1f, 0f)), what);
            Assert.That(material.GetFloat("_Brite"), Is.EqualTo(0f), what);
        }

        [Test]
        public void EveryWorldSpriteDrawsThroughTheFilterPerSprite()
        {
            var map = _host.GetComponentInChildren<MapView>();
            foreach (var renderer in map.Outline.Concat(map.Layers))
            {
                AssertFilter(renderer.sharedMaterial, renderer.name);
            }

            foreach (var view in _host.GetComponentsInChildren<CharacterView>())
            {
                AssertFilter(view.Renderer.sharedMaterial, view.name);
            }

            var cursor = _host.GetComponentInChildren<EditorCursorView>();
            AssertFilter(cursor.Renderer.sharedMaterial, "the editor's world hand");

            // One shared material: the effect is the world's, not a per-sprite copy.
            var materials = map.Outline.Concat(map.Layers).Select(r => r.sharedMaterial)
                .Concat(_host.GetComponentsInChildren<CharacterView>().Select(v => v.Renderer.sharedMaterial))
                .Append(cursor.Renderer.sharedMaterial).Distinct().ToList();
            Assert.That(materials.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheCellsArePaintedThroughTheSameEffectAndDrawnPlain()
        {
            var map = _host.GetComponentInChildren<MapView>();
            Assert.That(map.Effect, Is.Not.Null);
            Assert.That(map.Effect.Preset, Is.EqualTo(EffectPreset.Filter));
            Assert.That(map.Cells.sharedMaterial == null || map.Cells.sharedMaterial.shader.name != WorldEffectMaterial.ShaderName,
                "Core already shaded them; drawing them through the shader again would vignette the whole canvas");
        }

        [Test]
        public void EveryPresetIsAMaterialThatNothingSelects()
        {
            var data = _host.Settings.Effects;
            Assert.That(data.World, Is.EqualTo(EffectPreset.Filter));
            foreach (EffectPreset preset in Enum.GetValues(typeof(EffectPreset)))
            {
                var material = WorldEffectMaterial.Create(preset, data);
                try
                {
                    Assert.That(material.shader.name, Is.EqualTo(WorldEffectMaterial.ShaderName));
                    Assert.That(material.IsKeywordEnabled(WorldEffectMaterial.KeywordOf(preset)), Is.True, $"{preset}");
                    Assert.That(WorldEffectMaterial.Keywords.Count(material.IsKeywordEnabled), Is.EqualTo(1), $"{preset}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            var bloom = WorldEffectMaterial.Create(EffectPreset.Bloom, data);
            Assert.That(bloom.GetFloat("_BloomV"), Is.EqualTo(0.01f));
            Assert.That(bloom.GetFloat("_BloomA"), Is.EqualTo(0.2f));
            Assert.That(bloom.GetFloat("_Horizon"), Is.EqualTo(0.5f));
            UnityEngine.Object.DestroyImmediate(bloom);
        }

        [Test]
        public void TheNamesColoursPassThroughTheEffect()
        {
            var data = _host.Settings.Effects;
            var filter = new SpriteEffect(EffectPreset.Filter, data);
            Assert.That(WorldEffectMaterial.NameColour(filter, new Rgba(255, 255, 0, 255)), Is.EqualTo(new Rgba(255, 255, 0, 255)),
                "at the defaults the filter leaves a name's colour as it is");
            var negative = new SpriteEffect(EffectPreset.Negative, data);
            Assert.That(WorldEffectMaterial.NameColour(negative, new Rgba(255, 255, 0, 255)), Is.EqualTo(new Rgba(0, 0, 255, 255)));
            Assert.That(WorldEffectMaterial.NameColour(null, new Rgba(1, 2, 3, 4)), Is.EqualTo(new Rgba(1, 2, 3, 4)));
        }
    }
}
