using System.Collections.Generic;

namespace Wargon.Nukecs {
    public class UnityObjectsStorage {
        private static bool created;
        private static UnityObjectsStorage singletone;

        public static UnityObjectsStorage Singletone {
            get {
                if (created != false) return singletone;
                singletone = new UnityObjectsStorage();
                created = true;
                return singletone;
            }
        }

        private Dictionary<int, UnityEngine.Object> map = new();

        public int Add<T>(T obj) where T : UnityEngine.Object {
            var id = obj.GetInstanceID();
            map[id] = obj;
            return id;
        }

        public T Get<T>(int guid) where T : UnityEngine.Object {
            return (T) map[guid];
        }
    }

    public struct UnityRef<T> where T : UnityEngine.Object {
        private int _guid;

        public T Value {
            get => UnityObjectsStorage.Singletone.Get<T>(_guid);
            set => _guid = UnityObjectsStorage.Singletone.Add(value);
        }
    }
}