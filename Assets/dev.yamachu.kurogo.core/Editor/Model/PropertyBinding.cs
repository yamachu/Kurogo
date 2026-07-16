using System;

namespace dev.yamachu.Kurogo.Core
{
    /// <summary>
    /// A single float-animatable property, mirroring Unity's <c>EditorCurveBinding</c> triple
    /// (path / component type / property name). This is deliberately a flat struct rather than an
    /// abstract target hierarchy: AAC's clip-authoring API (<c>Animates(path, type, propertyName)</c>)
    /// is already fully general over any float property, so blendshapes, material floats, or transform
    /// properties are all just different (type, propertyName) pairs through the same generation code
    /// path. New factory methods can be added later without changing this type or breaking callers.
    /// </summary>
    public readonly struct PropertyBinding
    {
        /// <summary>Avatar-root-relative transform path (as computed by AnimationUtility.CalculateTransformPath).</summary>
        public readonly string Path;

        public readonly Type ComponentType;
        public readonly string PropertyName;

        public PropertyBinding(string path, Type componentType, string propertyName)
        {
            Path = path;
            ComponentType = componentType;
            PropertyName = propertyName;
        }

        /// <summary>Binds a SkinnedMeshRenderer blend shape ("blendShape.&lt;name&gt;").</summary>
        public static PropertyBinding BlendShape(string path, string blendShapeName)
        {
            return new PropertyBinding(path, typeof(UnityEngine.SkinnedMeshRenderer), "blendShape." + blendShapeName);
        }

        // Future factories (no model change needed to add these):
        //   public static PropertyBinding MaterialFloat(string path, Type rendererType, string propertyName)
        //   public static PropertyBinding TransformProperty(string path, string propertyName)
    }
}
