using UnityEditor.Animations;
using UnityEngine;

namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// Options controlling how <see cref="KurogoAnimatorGenerator"/> attaches its generated
    /// AnimatorController to the avatar.
    /// </summary>
    public sealed class KurogoGenerationOptions
    {
        /// <summary>
        /// If true (default), a host GameObject with a ModularAvatarMergeAnimator targeting the FX
        /// layer is created under the avatar root. If false, the caller is responsible for wiring the
        /// returned controller into the avatar itself.
        /// </summary>
        public bool AttachMergeAnimator = true;

        /// <summary>
        /// If true (default), a ModularAvatarParameters component is added to the host object with one
        /// entry per drive's <see cref="Drive.InputParameter"/>.
        /// </summary>
        public bool RegisterParameters = true;

        /// <summary>
        /// If true, registered parameters are network-synced (ParameterConfig.localOnly = false).
        /// Default false: most float-driven inputs (e.g. OSC face tracking) only exist locally, and
        /// syncing many Float parameters (8 bits each) can exhaust the 256-bit expression parameter budget.
        /// </summary>
        public bool SyncParameters = false;

        /// <summary>Name for the host GameObject. Defaults to the drive set's SystemName.</summary>
        public string HostObjectName = null;
    }

    /// <summary>Result of a <see cref="KurogoAnimatorGenerator.Generate"/> call.</summary>
    public sealed class KurogoGenerationResult
    {
        public AnimatorController Controller;

        /// <summary>The GameObject holding the Modular Avatar merge components, or null if
        /// <see cref="KurogoGenerationOptions.AttachMergeAnimator"/> was false.</summary>
        public GameObject HostObject;
    }
}
