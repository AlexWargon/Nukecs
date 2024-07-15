using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class ComponentSerializationTest : MonoBehaviour {
        private View deserializedView;
        public int Hash;
        public GameObject go;
        public UnityObjectRef<GameObject> reference;
        private void Start() {
            Hash = go.GetHashCode();
            Debug.Log(Hash);
            Debug.Log(go.GetInstanceID());
        }
    }
}