using UnityEngine;
using VRC.SDKBase;

namespace dev.yamachu.Kurogo.FT.Runtime
{
    /// <summary>
    /// Attach to an avatar to have KurogoFT generate an FX blendshape face-tracking layer at build time.
    /// The definition CSV must have a header row followed by rows of: OSCParameter,MeshName,BlendShapeName
    /// </summary>
    [AddComponentMenu("Kurogo FT/Kurogo FT Configuration")]
    [DisallowMultipleComponent]
    public class KurogoFTConfiguration : MonoBehaviour, IEditorOnly
    {
        [Tooltip("CSV asset with header row: OSCParameter,MeshName,BlendShapeName")]
        public TextAsset definitionCsv;

        [Tooltip("If enabled, generated parameters are registered as network-synced. " +
                 "Disabled by default because OSC face tracking input only exists on the local client, " +
                 "and syncing many Float parameters (8 bits each) can exhaust the 256-bit expression parameter budget.")]
        public bool syncParameters;
    }
}
