using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    // Extension overloads let the compiler select Entity by value for a concrete
    // first Entity slot. Generic component tuples retain their ref deconstruction.
    public static class QueryRuntimeDeconstruction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1>(this in QueryRuntimeRefs<T1> tuple, out Ref<T1> c0)
            where T1 : unmanaged, IComponent
        {
            tuple.DeconstructRefs(out c0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct(this in QueryRuntimeRefs<Entity> tuple, out Entity c0)
        {
            tuple.DeconstructRefs(out var entity);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2>(this in QueryRuntimeRefs<T1, T2> tuple, out Ref<T1> c0, out Ref<T2> c1)
            where T1 : unmanaged, IComponent where T2 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2>(this in QueryRuntimeRefs<Entity, T2> tuple, out Entity c0, out Ref<T2> c1)
            where T2 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2>(this in QueryRuntimeRefs<T1, T2> tuple, out Ref<T1> c0)
            where T1 : unmanaged, IComponent where T2 : unmanaged
        {
            tuple.DeconstructRefs(out c0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2>(this in QueryRuntimeRefs<Entity, T2> tuple, out Entity c0)
            where T2 : unmanaged
        {
            tuple.DeconstructRefs(out var entity);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3>(this in QueryRuntimeRefs<T1, T2, T3> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3>(this in QueryRuntimeRefs<Entity, T2, T3> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2)
            where T2 : unmanaged where T3 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3>(this in QueryRuntimeRefs<T1, T2, T3> tuple, out Ref<T1> c0, out Ref<T2> c1)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3>(this in QueryRuntimeRefs<Entity, T2, T3> tuple, out Entity c0, out Ref<T2> c1)
            where T2 : unmanaged where T3 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4>(this in QueryRuntimeRefs<T1, T2, T3, T4> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4>(this in QueryRuntimeRefs<Entity, T2, T3, T4> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4>(this in QueryRuntimeRefs<T1, T2, T3, T4> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4>(this in QueryRuntimeRefs<Entity, T2, T3, T4> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5, out c6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5, out c6);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7, T8>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7, T8> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5, out c6, out c7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7, T8>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7, T8> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5, out c6, out c7);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7, T8>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7, T8> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5, out c6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7, T8>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7, T8> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5, out c6);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7, T8, T9> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7, out Ref<T9> c8)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5, out c6, out c7, out c8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7, T8, T9>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7, T8, T9> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7, out Ref<T9> c8)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5, out c6, out c7, out c8);
            c0 = entity.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this in QueryRuntimeRefs<T1, T2, T3, T4, T5, T6, T7, T8, T9> tuple, out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7)
            where T1 : unmanaged, IComponent where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
            tuple.DeconstructRefs(out c0, out c1, out c2, out c3, out c4, out c5, out c6, out c7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deconstruct<T2, T3, T4, T5, T6, T7, T8, T9>(this in QueryRuntimeRefs<Entity, T2, T3, T4, T5, T6, T7, T8, T9> tuple, out Entity c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4, out Ref<T6> c5, out Ref<T7> c6, out Ref<T8> c7)
            where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
            tuple.DeconstructRefs(out var entity, out c1, out c2, out c3, out c4, out c5, out c6, out c7);
            c0 = entity.Read;
        }

    }
}
