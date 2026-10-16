namespace Shop.Services;

public sealed class PriceCache
{
    private static readonly Dictionary<int, decimal> Cache = new();

    public async Task<decimal> GetAsync(int productId, Func<int, Task<decimal>> load)
    {
        if (Cache.TryGetValue(productId, out var price)) return price;
        price = await load(productId).ConfigureAwait(false);
        Cache[productId] = price; // BENCH: NonThreadSafeCollection
        return price;
    }
}
