namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The last <c>capacity</c> samples of a periodic reading, the oldest dropped first.
/// </summary>
internal sealed class SampleRing<T>(int capacity)
{
    private readonly Queue<T> _samples = new(capacity);

    public int Count => _samples.Count;

    public void Add(T sample)
    {
        if (_samples.Count == capacity)
        {
            _samples.Dequeue();
        }

        _samples.Enqueue(sample);
    }

    /// <summary>
    /// The latest sample and the one <paramref name="steps"/> before it, or the oldest when
    /// fewer were taken; null with fewer than two samples.
    /// </summary>
    public (T Earlier, T Latest)? Span(int steps)
    {
        if (_samples.Count < 2)
        {
            return null;
        }

        var back = Math.Min(steps, _samples.Count - 1);
        return (_samples.ElementAt(_samples.Count - 1 - back), _samples.Last());
    }

    public void Clear() => _samples.Clear();
}
