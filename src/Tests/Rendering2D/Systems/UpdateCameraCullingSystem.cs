using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public struct UpdateCameraCullingSystem : ISystem, IOnCreate {
        private Camera _camera;
        public void OnCreate(ref World world) {
            _camera = Camera.main;
        }
        public void OnUpdate(ref State state) {
            CullingData.instance.Update(_camera);
        }
    }
}