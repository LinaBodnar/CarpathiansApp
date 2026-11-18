namespace Carpathians.Platform.Aggregator.Services;

/// <summary>
/// Потокобезпечний набір попереджень агрегації (паралельні виклики), без дублікатів:
/// недоступний необов'язковий сервіс не «валить» відповідь, а додає сюди попередження.
/// </summary>
public sealed class AggregationWarnings
{
    private readonly List<string> _items = new();

    public int Count
    {
        get
        {
            lock (_items)
                return _items.Count;
        }
    }

    public void Add(string warning)
    {
        lock (_items)
        {
            if (!_items.Contains(warning))
                _items.Add(warning);
        }
    }

    public List<string> ToList()
    {
        lock (_items)
            return _items.ToList();
    }
}
