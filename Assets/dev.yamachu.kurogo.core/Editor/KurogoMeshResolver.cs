using System;
using System.Collections.Generic;
using UnityEngine;

namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// Resolves SkinnedMeshRenderers under an avatar by GameObject name. This is a convenience for
    /// input formats (like KurogoFT's CSV) that address meshes by name; it is not required by
    /// <see cref="KurogoAnimatorGenerator"/> itself, which only ever consumes already-resolved
    /// <see cref="PropertyBinding"/> paths.
    /// </summary>
    public static class KurogoMeshResolver
    {
        /// <summary>
        /// Builds a name -&gt; renderer lookup for every SkinnedMeshRenderer under <paramref name="avatarRoot"/>.
        /// The first renderer found for a given name wins; subsequent duplicates are reported via
        /// <paramref name="reportDuplicate"/> and skipped.
        /// </summary>
        public static Dictionary<string, SkinnedMeshRenderer> BuildLookup(
            Transform avatarRoot, Action<string> reportDuplicate = null)
        {
            var lookup = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (var renderer in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var name = renderer.gameObject.name;
                if (lookup.ContainsKey(name))
                {
                    reportDuplicate?.Invoke(name);
                    continue;
                }

                lookup.Add(name, renderer);
            }

            return lookup;
        }
    }
}
