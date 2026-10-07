using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Wargon.Nukecs
{
    /// <summary>
    /// Version bridge for Unity object identifiers. Unity 6.4 introduced EntityId and later
    /// versions turned the int instance-ID API into compile errors. Files that store an id
    /// declare the same <c>ObjectId</c> alias (EntityId on 6.4+, int before) and go through here.
    /// <c>default</c> means "no object" in both cases.
    /// </summary>
    public static class UnityObjectId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ObjectId Of(Object obj)
        {
            if (obj == null) return default;
#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId();
#else
            return obj.GetInstanceID();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNone(ObjectId id) => id.Equals(default(ObjectId));

        public static Object ToObject(ObjectId id)
        {
            if (IsNone(id)) return null;
#if UNITY_6000_4_OR_NEWER
            return Resources.EntityIdToObject(id);
#else
            return Resources.InstanceIDToObject(id);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(ObjectId id)
        {
#if UNITY_6000_4_OR_NEWER
            return Resources.EntityIdIsValid(id);
#else
            return Resources.InstanceIDIsValid(id);
#endif
        }

        public static void ToObjectList(NativeArray<ObjectId> ids, List<Object> objects)
        {
#if UNITY_6000_4_OR_NEWER
            Resources.EntityIdsToObjectList(ids, objects);
#else
            Resources.InstanceIDToObjectList(ids, objects);
#endif
        }

#if UNITY_EDITOR
        /// <summary>Editor lookup: unlike <see cref="ToObject"/>, also loads assets that are not in memory yet.</summary>
        public static Object EditorToObject(ObjectId id)
        {
            if (IsNone(id)) return null;
#if UNITY_6000_4_OR_NEWER
            return UnityEditor.EditorUtility.EntityIdToObject(id);
#else
            return UnityEditor.EditorUtility.InstanceIDToObject(id);
#endif
        }
#endif
    }
}
