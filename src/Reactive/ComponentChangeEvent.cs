namespace Wargon.Nukecs.Reactive
{
    public static class ComponentChangeEvent<T>
    {
        private static ReactDelegate<T> _onChange;
        public static void Invoke(ref T value, ref Entity entity)
        {
            _onChange?.Invoke(ref value, ref entity);
        }
        public static void Subscribe(ReactDelegate<T> callback)
        {
            _onChange += callback;
        }
        public static void Unsubscribe(ReactDelegate<T> callback)
        {
            _onChange -= callback;
        }
    }
}