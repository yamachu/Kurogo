using System.Collections.Generic;
using dev.yamachu.Kurogo.Core;
using dev.yamachu.Kurogo.FT.Editor;
using dev.yamachu.Kurogo.FT.Runtime;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(KurogoFTPlugin))]

namespace dev.yamachu.Kurogo.FT.Editor
{
    /// <summary>
    /// Non-destructive NDMF plugin: reads a <see cref="KurogoFTConfiguration"/> CSV definition and, at
    /// build time only, asks Kurogo.Core to generate a Direct-blend-tree FX layer that drives
    /// blendshapes from OSC-style float parameters. This file owns everything specific to the CSV
    /// input format (parsing, mesh/blendshape resolution, user-facing error messages) - all animator
    /// generation and Modular Avatar wiring lives in Kurogo.Core so other plugins can reuse it.
    /// </summary>
    public class KurogoFTPlugin : Plugin<KurogoFTPlugin>
    {
        public override string QualifiedName => "dev.yamachu.kurogo.ft";
        public override string DisplayName => "Kurogo FT";

        protected override void Configure()
        {
            // Runs in Generating, strictly before Modular Avatar's Merge Animator / Parameters passes
            // (both declared in Transforming), so no explicit ordering constraint is required.
            InPhase(BuildPhase.Generating)
                .Run("Generate FT blendshape animator", Generate);
        }

        private static readonly Localizer L = new Localizer("en-US", () =>
            new List<(string, System.Func<string, string>)>
            {
                ("en-US", key => Strings.TryGetValue(key, out var v) ? v : null)
            });

        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            { "kurogoft.error.no_csv", "KurogoFT: no definition CSV assigned" },
            { "kurogoft.error.no_csv:description", "The KurogoFTConfiguration component on this avatar has no definitionCsv assigned. No FT layer will be generated." },
            { "kurogoft.error.no_rows", "KurogoFT: definition CSV has no usable rows" },
            { "kurogoft.error.no_rows:description", "Every row in the assigned CSV was skipped (see console warnings). No FT layer will be generated." },
        };

        private static void Generate(BuildContext ctx)
        {
            var config = ctx.AvatarRootObject.GetComponentInChildren<KurogoFTConfiguration>(true);
            if (config == null) return; // this avatar does not use KurogoFT

            if (config.definitionCsv == null)
            {
                ErrorReport.ReportError(L, ErrorSeverity.NonFatal, "kurogoft.error.no_csv");
                return;
            }

            var avatarRoot = ctx.AvatarRootTransform;
            var meshesByName = KurogoMeshResolver.BuildLookup(avatarRoot, duplicateName =>
                Debug.LogWarning($"[KurogoFT] multiple SkinnedMeshRenderers named '{duplicateName}' under the avatar; the first one found will be used"));

            var parsedDrives = CsvDefinitionParser.Parse(config.definitionCsv.text,
                (line, message) => Debug.LogWarning($"[KurogoFT] {config.definitionCsv.name}: line {line}: {message}"));

            var driveSet = new KurogoDriveSet { SystemName = "KurogoFT" };

            foreach (var parsed in parsedDrives)
            {
                var drive = new Drive
                {
                    InputParameter = parsed.OscParameter,
                    InputMin = parsed.InputMin,
                    InputMax = parsed.InputMax
                };

                // A value of 0 is equivalent to no smoothing, so skip building the feedback network for it.
                if (parsed.Smoothing.HasValue && parsed.Smoothing.Value > 0f)
                {
                    drive.Smoothing = new SmoothingSettings { LocalSmoothness = parsed.Smoothing.Value };
                }

                foreach (var target in parsed.Targets)
                {
                    if (!meshesByName.TryGetValue(target.MeshName, out var renderer))
                    {
                        Debug.LogWarning($"[KurogoFT] {config.definitionCsv.name}: '{target.MeshName}' has no SkinnedMeshRenderer under the avatar; target for '{parsed.OscParameter}' skipped");
                        continue;
                    }

                    if (renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(target.BlendShapeName) < 0)
                    {
                        Debug.LogWarning($"[KurogoFT] {config.definitionCsv.name}: mesh '{target.MeshName}' has no blend shape '{target.BlendShapeName}'; target for '{parsed.OscParameter}' skipped");
                        continue;
                    }

                    var path = AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot);
                    drive.Targets.Add(new DriveTarget
                    {
                        Binding = PropertyBinding.BlendShape(path, target.BlendShapeName),
                        MinValue = target.MinValue,
                        MaxValue = target.MaxValue
                    });
                }

                if (drive.Targets.Count > 0) driveSet.Drives.Add(drive);
            }

            if (driveSet.Drives.Count == 0)
            {
                ErrorReport.ReportError(L, ErrorSeverity.NonFatal, "kurogoft.error.no_rows");
                return;
            }

            KurogoAnimatorGenerator.Generate(ctx, driveSet, new KurogoGenerationOptions
            {
                SyncParameters = config.syncParameters
            });
        }
    }
}
