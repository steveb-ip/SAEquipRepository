namespace VLB
{
    public enum ColorMode
    {
        Flat,       // Apply a flat/plain/single color
        Gradient    // Apply a gradient
    }

    public enum AttenuationEquation
    {
        Linear = 0,     // Simple linear attenuation.
        Quadratic = 1,  // Quadratic attenuation, which usually gives more realistic results.
        Blend = 2       // Custom blending mix between linear and quadratic attenuation formulas. Use attenuationEquation property to tweak the mix.
    }

    public enum BlendingMode
    {
        Additive,
        SoftAdditive,
        TraditionalTransparency,
    }

    public enum MeshType
    {
        Shared, // Use the global shared mesh (recommended setting, since it will save a lot on memory). Will use the geometry properties set on Config.
        Custom, // Use a custom mesh instead. Will use the geometry properties set on the beam.
    }

    public enum RenderQueue
    {
        /// Specify a custom render queue.
        Custom = 0,

        /// This render queue is rendered before any others.
        Background = 1000,

        /// Opaque geometry uses this queue.
        Geometry = 2000,

        /// Alpha tested geometry uses this queue.
        AlphaTest = 2450,

        /// Last render queue that is considered "opaque".
        GeometryLast = 2500,

        /// This render queue is rendered after Geometry and AlphaTest, in back-to-front order.
        Transparent = 3000,

        /// This render queue is meant for overlay effects.
        Overlay = 4000,
    }

    public enum PlaneAlignment
    {
        /// <summary>Align the plane to the surface normal which blocks the beam. Works better for large occluders such as floors and walls.</summary>
        Surface,
        /// <summary>Keep the plane aligned with the beam direction. Workds better with more complex occluders or with corners.</summary>
        Beam
    }
}
