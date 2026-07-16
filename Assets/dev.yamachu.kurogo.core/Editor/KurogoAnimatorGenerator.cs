using System;
using System.Linq;
using AnimatorAsCode.V1;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// Builds a single Direct-blend-tree AnimatorController from a <see cref="KurogoDriveSet"/> and,
    /// by default, attaches it non-destructively to the avatar's FX layer via Modular Avatar's Merge
    /// Animator. This is a plain library, not an NDMF plugin: it only runs when a caller's own NDMF
    /// pass invokes <see cref="Generate"/>, so each product keeps its own QualifiedName, build phase
    /// and pass ordering. Everything this produces is parented into <c>ctx.AssetContainer</c> and the
    /// avatar build clone, so it is cleaned up automatically at the end of the NDMF build - callers
    /// never need to clean anything up themselves.
    /// </summary>
    public static class KurogoAnimatorGenerator
    {
        public static KurogoGenerationResult Generate(
            BuildContext ctx, KurogoDriveSet set, KurogoGenerationOptions options = null)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (string.IsNullOrEmpty(set.SystemName))
                throw new ArgumentException("KurogoDriveSet.SystemName must be set", nameof(set));
            if (set.Drives == null || set.Drives.Count == 0)
                throw new ArgumentException("KurogoDriveSet must have at least one Drive", nameof(set));

            foreach (var drive in set.Drives)
            {
                if (string.IsNullOrEmpty(drive.InputParameter))
                    throw new ArgumentException("Drive.InputParameter must be set");
                if (drive.Targets == null || drive.Targets.Count == 0)
                    throw new ArgumentException($"Drive '{drive.InputParameter}' has no targets");
                if (drive.Smoothing != null && (drive.Smoothing.LocalSmoothness < 0f || drive.Smoothing.LocalSmoothness >= 1f))
                    throw new ArgumentException(
                        $"Drive '{drive.InputParameter}': Smoothing.LocalSmoothness must be in [0, 1) - a value of 1 or greater would freeze the smoothed value forever.");
            }

            if (options == null) options = new KurogoGenerationOptions();

            var controller = BuildController(ctx, set);

            var result = new KurogoGenerationResult { Controller = controller };

            if (options.AttachMergeAnimator)
            {
                result.HostObject = AttachModularAvatar(ctx, set, controller, options);
            }

            return result;
        }

        private static UnityEditor.Animations.AnimatorController BuildController(BuildContext ctx, KurogoDriveSet set)
        {
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = set.SystemName,
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.Everything,
                AssetKey = set.SystemName,
                DefaultsProvider = new AacDefaultsProvider(writeDefaults: true)
            });

            var flController = aac.NewAnimatorController(set.SystemName);
            var layer = flController.NewLayer();

            // Constant-1 weight parameter feeding every child of the Direct blend tree below.
            var one = layer.FloatParameter($"{set.SystemName}/One");
            layer.OverrideValue(one, 1f);

            var direct = aac.NewBlendTree().Direct();

            foreach (var drive in set.Drives)
            {
                var sanitizedParam = drive.InputParameter.Replace('/', '_');
                var clipMin = aac.NewClip($"{set.SystemName}_{sanitizedParam}_min").Animating(edit =>
                {
                    foreach (var target in drive.Targets)
                    {
                        edit.Animates(target.Binding.Path, target.Binding.ComponentType, target.Binding.PropertyName)
                            .WithFixedSeconds(0.01f, target.MinValue);
                    }
                });
                var clipMax = aac.NewClip($"{set.SystemName}_{sanitizedParam}_max").Animating(edit =>
                {
                    foreach (var target in drive.Targets)
                    {
                        edit.Animates(target.Binding.Path, target.Binding.ComponentType, target.Binding.PropertyName)
                            .WithFixedSeconds(0.01f, target.MaxValue);
                    }
                });

                var blendParam = drive.Smoothing != null
                    ? BuildSmoothingFeedback(aac, layer, direct, one, set, drive, sanitizedParam)
                    : layer.FloatParameter(drive.InputParameter);

                var tree1D = aac.NewBlendTree().Simple1D(blendParam)
                    .WithAnimation(clipMin, drive.InputMin)
                    .WithAnimation(clipMax, drive.InputMax);

                direct.WithAnimation(tree1D, one);
            }

            layer.NewState($"{set.SystemName} (Direct)")
                .WithAnimation(direct)
                .WithWriteDefaultsSetTo(true); // Direct blend trees require Write Defaults ON.

            return flController.AnimatorController;
        }

        /// <summary>
        /// Builds an AAP (Animated Animator Parameter) feedback loop that exponentially smooths
        /// <see cref="Drive.InputParameter"/> and returns the smoothed parameter to blend from instead.
        ///
        /// This technique - and the choice to expose a single 0..1 "smoothness" factor - is
        /// reimplemented (not copied verbatim) from OSCmooth's
        /// <c>OSCmoothAnimationHandler.CreateParameterSmoothingBlendTree</c>
        /// (https://github.com/regzo2/OSCmooth, MIT License, Copyright (c) 2022 Mitchell Taylor).
        /// See ../THIRD_PARTY_NOTICES.md for the full license text and attribution.
        ///
        /// Three Simple1D blend trees form the feedback loop:
        ///   - falseTree: blends on the raw input parameter, between two clips that write -1/+1 into
        ///     the smoothed AAP parameter. Because the clips' values exactly match their thresholds,
        ///     the blended output reconstructs the raw input's value verbatim (for inputs within
        ///     [-1, 1]) - this copies the raw input into the smoothed parameter.
        ///   - trueTree: the same two clips, but blended on the smoothed parameter itself, reading back
        ///     its own value from the previous frame - i.e. "remembering" the last smoothed value.
        ///   - feedbackRoot: blends between falseTree (weight 0) and trueTree (weight 1) using a
        ///     constant smoothness factor, so every frame:
        ///       Smoothed[t] = factor * Smoothed[t-1] + (1 - factor) * Raw[t]
        ///     which is a standard exponential moving average. This is frame-rate dependent (the
        ///     factor is a per-frame decay, not a time constant), matching the original technique.
        /// </summary>
        private static AacFlFloatParameter BuildSmoothingFeedback(
            AacFlBase aac, AacFlLayer layer, AacFlBlendTreeDirect direct, AacFlFloatParameter one,
            KurogoDriveSet set, Drive drive, string sanitizedParam)
        {
            var rawParam = layer.FloatParameter(drive.InputParameter);
            var smoothed = layer.FloatParameter($"{set.SystemName}/{sanitizedParam}/Smoothed");
            var smoothFactor = layer.FloatParameter($"{set.SystemName}/{sanitizedParam}/SmoothFactor");
            layer.OverrideValue(smoothFactor, drive.Smoothing.LocalSmoothness);

            var passNeg1 = aac.NewClip($"{set.SystemName}_{sanitizedParam}_smooth_neg1").Animating(edit =>
                edit.AnimatesAnimator(smoothed).WithFixedSeconds(0.01f, -1f));
            var passPos1 = aac.NewClip($"{set.SystemName}_{sanitizedParam}_smooth_pos1").Animating(edit =>
                edit.AnimatesAnimator(smoothed).WithFixedSeconds(0.01f, 1f));

            var falseTree = aac.NewBlendTree().Simple1D(rawParam)
                .WithAnimation(passNeg1, -1f)
                .WithAnimation(passPos1, 1f);
            var trueTree = aac.NewBlendTree().Simple1D(smoothed)
                .WithAnimation(passNeg1, -1f)
                .WithAnimation(passPos1, 1f);

            var feedbackRoot = aac.NewBlendTree().Simple1D(smoothFactor)
                .WithAnimation(falseTree, 0f)
                .WithAnimation(trueTree, 1f);

            direct.WithAnimation(feedbackRoot, one);

            return smoothed;
        }

        private static GameObject AttachModularAvatar(
            BuildContext ctx, KurogoDriveSet set, UnityEditor.Animations.AnimatorController controller,
            KurogoGenerationOptions options)
        {
            // This host object only ever exists on the NDMF build clone of the avatar, so it - and the
            // components below - never touch the original scene asset.
            var host = new GameObject(options.HostObjectName ?? set.SystemName);
            host.transform.SetParent(ctx.AvatarRootTransform, false);

            var merge = host.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = controller;
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.pathMode = MergeAnimatorPathMode.Absolute; // clip bindings above are avatar-root-relative
            merge.deleteAttachedAnimator = false;
            merge.matchAvatarWriteDefaults = false; // keep the Direct blend tree's forced Write Defaults ON

            if (options.RegisterParameters)
            {
                var maParams = host.AddComponent<ModularAvatarParameters>();
                maParams.parameters = set.Drives.Select(drive => new ParameterConfig
                {
                    nameOrPrefix = drive.InputParameter,
                    syncType = ParameterSyncType.Float,
                    localOnly = !options.SyncParameters,
                    saved = false,
                    defaultValue = 0f
                }).ToList();
            }

            return host;
        }
    }
}
