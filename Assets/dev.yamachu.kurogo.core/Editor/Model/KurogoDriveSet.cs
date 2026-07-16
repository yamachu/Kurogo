using System.Collections.Generic;

namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// Root input to <see cref="KurogoAnimatorGenerator"/>. <see cref="SystemName"/> namespaces every
    /// asset, layer, GameObject and animator parameter this drive set produces, so that multiple
    /// independent systems (built by different NDMF plugins) can call the generator against the same
    /// avatar without colliding.
    /// </summary>
    public sealed class KurogoDriveSet
    {
        /// <summary>Required. Used as the AAC SystemName/AssetKey and as a prefix for internal parameters.</summary>
        public string SystemName;

        public List<Drive> Drives = new List<Drive>();
    }

    /// <summary>
    /// A single input float parameter driving one or more animated properties.
    /// </summary>
    public sealed class Drive
    {
        /// <summary>Animator float parameter name (an OSC address, for face-tracking use cases).</summary>
        public string InputParameter;

        /// <summary>Input value corresponding to each target's <see cref="DriveTarget.MinValue"/>.</summary>
        public float InputMin = 0f;

        /// <summary>Input value corresponding to each target's <see cref="DriveTarget.MaxValue"/>.</summary>
        public float InputMax = 1f;

        /// <summary>One or more properties this single input parameter drives together.</summary>
        public List<DriveTarget> Targets = new List<DriveTarget>();

        /// <summary>
        /// Reserved for a future input-smoothing feature (exponential smoothing via an AAP layer).
        /// Must be null in this version - <see cref="KurogoAnimatorGenerator"/> throws
        /// <see cref="System.NotSupportedException"/> if set, so the model slot is visible without
        /// silently ignoring caller intent.
        /// </summary>
        public SmoothingSettings Smoothing;
    }

    /// <summary>
    /// One animated property, and the output value range it should be driven across as its
    /// <see cref="Drive"/>'s input moves from <see cref="Drive.InputMin"/> to <see cref="Drive.InputMax"/>.
    /// </summary>
    public sealed class DriveTarget
    {
        public PropertyBinding Binding;

        /// <summary>Property value when the drive's input is at <see cref="Drive.InputMin"/>.</summary>
        public float MinValue = 0f;

        /// <summary>Property value when the drive's input is at <see cref="Drive.InputMax"/>.</summary>
        public float MaxValue = 100f;
    }
}
