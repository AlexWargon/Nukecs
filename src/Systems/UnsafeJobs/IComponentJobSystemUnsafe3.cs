﻿namespace Wargon.Nukecs {
    public unsafe interface IComponentJobSystemUnsafe3 {
        public void OnUpdate(ref Entity entity, void* c1, void* c2, void* c3, ref State state);
    }
}