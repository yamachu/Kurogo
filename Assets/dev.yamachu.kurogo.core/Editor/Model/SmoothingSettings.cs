namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// Placeholder for a future input-smoothing feature. Shipped now (unused) so that adding
    /// smoothing later only requires <see cref="KurogoAnimatorGenerator"/> to start honoring an
    /// already-existing model field, rather than a breaking change to <see cref="Drive"/>.
    /// Not supported yet: <see cref="KurogoAnimatorGenerator"/> throws if this is non-null.
    /// </summary>
    public sealed class SmoothingSettings
    {
        /// <summary>0 = no smoothing, 1 = maximum smoothing. Exact curve/algorithm TBD.</summary>
        public float LocalSmoothness = 0f;
    }
}
