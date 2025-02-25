using System.Collections.Concurrent;

namespace ImmersiHome_API.Infrastructure.Persistence
{
    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;
        private readonly Action<T> _objectReset;

        public ObjectPool(Func<T> objectGenerator, Action<T> objectReset = null)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objectReset = objectReset ?? (_ => { });
            _objects = new ConcurrentBag<T>();
        }

        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

        public void Return(T item)
        {
            _objectReset(item);
            _objects.Add(item);
        }
    }
}
