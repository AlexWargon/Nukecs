using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private T1* _p1;
        private T2* _p2;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly bool IsType2Component = QueryParamInfo<T2>.IsComponent;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++;
            if(IsType2Component) _p2++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            if(IsType2Component) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            if(IsType2Component) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2)
        {
            c1 = _p1;
            c2 = _p2;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private T1* _p1;
        private T2* _p2;
        private T3* _p3;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T3>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++;
            _p2++;
            if(IsOptionComponent) _p3++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            if(IsOptionComponent) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            if(IsOptionComponent) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2)
        {
            c1 = _p1;
            c2 = _p2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        private T1* _p1;
        private T2* _p2;
        private T3* _p3;
        private T4* _p4;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T4>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++;
            _p2++;
            _p3++;
            if(IsOptionComponent) _p4++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1 = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            if(IsOptionComponent)_p4 = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            if(IsOptionComponent)_p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4, T5> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        private T1* _p1;
        private T2* _p2;
        private T3* _p3;
        private T4* _p4;
        private T5* _p5;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T5>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++; _p2++; _p3++; _p4++;
            if (IsOptionComponent) _p5++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            if (IsOptionComponent)
                _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            if (IsOptionComponent)
                _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4, T5, T6> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
    {
        private T1* _p1; private T2* _p2; private T3* _p3;
        private T4* _p4; private T5* _p5; private T6* _p6;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T6>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++; _p2++; _p3++; _p4++; _p5++;
            if (IsOptionComponent) _p6++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            if (IsOptionComponent)
                _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            
            if (IsOptionComponent)
                _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5)
        {
            c1 = _p1; c2 = _p2; c3 = _p3;
            c4 = _p4; c5 = _p5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5, out T6* c6)
        {
            c1 = _p1; c2 = _p2; c3 = _p3;
            c4 = _p4; c5 = _p5; c6 = _p6;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4, T5, T6, T7> : IComponentTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        where T7 : unmanaged
    {
        private T1* _p1; private T2* _p2; private T3* _p3;
        private T4* _p4; private T5* _p5; private T6* _p6; private T7* _p7;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T7>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++; _p2++; _p3++; _p4++; _p5++; _p6++;
            if (IsOptionComponent) _p7++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));

            if (IsOptionComponent)
                _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;

            if (IsOptionComponent)
                _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3,
            out T4* c4, out T5* c5, out T6* c6)
        {
            c1 = _p1; c2 = _p2; c3 = _p3;
            c4 = _p4; c5 = _p5; c6 = _p6;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3,
            out T4* c4, out T5* c5, out T6* c6, out T7* c7)
        {
            c1 = _p1; c2 = _p2; c3 = _p3;
            c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8> : IComponentTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        where T7 : unmanaged where T8 : unmanaged
    {
        private T1* _p1; private T2* _p2; private T3* _p3; private T4* _p4;
        private T5* _p5; private T6* _p6; private T7* _p7; private T8* _p8;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;
        private static readonly int Type8 = ComponentType<T8>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T8>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++; _p2++; _p3++; _p4++;
            _p5++; _p6++; _p7++;
            if (IsOptionComponent) _p8++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));

            if (IsOptionComponent)
                _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;

            if (IsOptionComponent)
                _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3, out T4* c4,
            out T5* c5, out T6* c6, out T7* c7)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4;
            c5 = _p5; c6 = _p6; c7 = _p7;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3, out T4* c4,
            out T5* c5, out T6* c6, out T7* c7, out T8* c8)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4;
            c5 = _p5; c6 = _p6; c7 = _p7; c8 = _p8;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9> : IComponentTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
    {
        private T1* _p1; private T2* _p2; private T3* _p3; 
        private T4* _p4; private T5* _p5; private T6* _p6; 
        private T7* _p7; private T8* _p8; private T9* _p9;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;
        private static readonly int Type8 = ComponentType<T8>.Index;
        private static readonly int Type9 = ComponentType<T9>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T9>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1++; _p2++; _p3++; _p4++;
            _p5++; _p6++; _p7++; _p8++;
            if (IsOptionComponent) _p9++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));

            if (IsOptionComponent)
                _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;
            _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8))) + localStart;
            if (IsOptionComponent)
                _p9 = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type9))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3, out T4* c4,
            out T5* c5, out T6* c6, out T7* c7, out T8* c8)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4;
            c5 = _p5; c6 = _p6; c7 = _p7; c8 = _p8;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out T1* c1, out T2* c2, out T3* c3, 
            out T4* c4, out T5* c5, out T6* c6, 
            out T7* c7, out T8* c8, out T9* c9)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; 
            c4 = _p4; c5 = _p5; c6 = _p6; 
            c7 = _p7; c8 = _p8; c9 = _p9;
        }
    }
}