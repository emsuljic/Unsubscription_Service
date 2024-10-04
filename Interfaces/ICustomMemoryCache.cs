 namespace UnsubscribeService.Interfaces
{
    public interface ICustomMemoryCache
    {
        void Set<T>(string key, T value, TimeSpan duration);
        bool TryGetValue<T>(string key, out T value);
        void Remove(string key);
    }
}
