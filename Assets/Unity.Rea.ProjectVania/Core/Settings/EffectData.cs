namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>The five shaders <c>EffectManager</c> loaded (<c>Content\Effects\*.fx</c>, RECONSTRUCTION 6.1).</summary>
    public enum EffectPreset
    {
        Filter,
        Negative,
        Bloom,
        Pause,
        Water,
    }

    /// <summary>
    /// effects.json: the effect every world sprite is drawn through (<c>SceneManager</c>'s <c>effect = "filter"</c>; the
    /// <c>"negative"</c> line was commented out, so nothing selects another) and each shader's own parameters at the
    /// defaults the .fx files declared (every <c>Parameters[...]</c> assignment of the original was commented out). Figures
    /// the shaders wrote as literals (pause's 0.005 spread, water's 0.35 darkening and its ripple) stay in
    /// <see cref="Display.SpriteEffect"/>, as the shaders kept them.
    /// </summary>
    public sealed class EffectData
    {
        /// <summary>The preset the world draws through (<c>Filter</c>); the others exist and nothing selects them.</summary>
        public EffectPreset World { get; private set; }

        public FilterEffectData Filter { get; private set; }
        public BloomEffectData Bloom { get; private set; }
        public WaterEffectData Water { get; private set; }

        internal static EffectData Read(SettingsReader r) => new EffectData
        {
            World = r.Choice<EffectPreset>("world"),
            Filter = r.Section("filter", FilterEffectData.Read),
            Bloom = r.Section("bloom", BloomEffectData.Read),
            Water = r.Section("water", WaterEffectData.Read),
        };
    }

    /// <summary>
    /// <c>filter.fx</c>: <c>burn</c> darkens by the distance from the texture's centre, <c>saturation</c> mixes each channel
    /// with the grey average, <c>r g b</c> scale the channels and <c>brite</c> is added.
    /// </summary>
    public sealed class FilterEffectData
    {
        public float Burn { get; private set; }
        public float Saturation { get; private set; }
        public float R { get; private set; }
        public float G { get; private set; }
        public float B { get; private set; }
        public float Brite { get; private set; }

        internal static FilterEffectData Read(SettingsReader r) => new FilterEffectData
        {
            Burn = r.Number("burn", -100f, 100f),
            Saturation = r.Number("saturation", -100f, 100f),
            R = r.Number("r", -100f, 100f),
            G = r.Number("g", -100f, 100f),
            B = r.Number("b", -100f, 100f),
            Brite = r.Number("brite", -100f, 100f),
        };
    }

    /// <summary><c>bloom.fx</c>: <c>v</c> scales the 12 Poisson taps (in texture coordinates) and <c>a</c> is the alpha written.</summary>
    public sealed class BloomEffectData
    {
        public float V { get; private set; }
        public float A { get; private set; }

        internal static BloomEffectData Read(SettingsReader r) => new BloomEffectData
        {
            V = r.Number("v", -100f, 100f),
            A = r.Number("a", 0f, 1f),
        };
    }

    /// <summary><c>water.fx</c>: the reflection's <c>horizon</c> and the ripple's <c>delta</c> and <c>theta</c> phases (never set, so 0).</summary>
    public sealed class WaterEffectData
    {
        public float Horizon { get; private set; }
        public float Delta { get; private set; }
        public float Theta { get; private set; }

        internal static WaterEffectData Read(SettingsReader r) => new WaterEffectData
        {
            Horizon = r.Number("horizon", -100f, 100f),
            Delta = r.Number("delta", -100000f, 100000f),
            Theta = r.Number("theta", -100000f, 100000f),
        };
    }
}
