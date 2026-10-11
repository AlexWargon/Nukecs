// Unity's job safety system reads these attributes; outside Unity they have no meaning and
// this file declares them so the core source compiles unchanged. Empty in Unity.
#if !UNITY_5_3_OR_NEWER
using System;

namespace Unity.Collections.LowLevel.Unsafe
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeDisableUnsafePtrRestrictionAttribute : Attribute { }
}
#endif
