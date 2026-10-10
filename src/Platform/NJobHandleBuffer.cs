using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Reusable persistent buffer for combining many job handles. In Unity it is the
    /// <c>NativeArray&lt;JobHandle&gt;</c> passed to <c>JobHandle.CombineDependencies</c>.
    /// Grows on demand; Dispose when the owner is destroyed.
    /// </summary>
    public struct NJobHandleBuffer : IDisposable
    {
#if UNITY_5_3_OR_NEWER
        private Unity.Collections.NativeArray<Unity.Jobs.JobHandle> _handles;

        public bool IsCreated => _handles.IsCreated;

        /// <summary>Ensures room for <paramref name="count"/> handles; the contents are not kept.</summary>
        public void EnsureCapacity(int count)
        {
            if (_handles.IsCreated && _handles.Length >= count) return;
            if (_handles.IsCreated) _handles.Dispose();
            _handles = new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(count, AllocatorHandle.Persistent);
        }

        public NJobHandle this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _handles[index] = value.value;
        }

        /// <summary>Handle that completes when the first <paramref name="count"/> handles complete.</summary>
        public NJobHandle Combine(int count) =>
            Unity.Jobs.JobHandle.CombineDependencies(_handles.GetSubArray(0, count));

        public void Dispose()
        {
            if (_handles.IsCreated) _handles.Dispose();
        }
#else
        private NJobHandle[] _handles;

        public bool IsCreated => _handles != null;

        public void EnsureCapacity(int count)
        {
            if (_handles == null || _handles.Length < count) _handles = new NJobHandle[count];
        }

        public NJobHandle this[int index]
        {
            set => _handles[index] = value;
        }

        public NJobHandle Combine(int count)
        {
            var combined = default(NJobHandle);
            for (var i = 0; i < count; i++) combined = NJobHandle.Combine(combined, _handles[i]);
            return combined;
        }

        public void Dispose() => _handles = null;
#endif
    }
}
