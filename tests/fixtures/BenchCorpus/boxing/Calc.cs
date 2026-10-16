namespace Mini;

public sealed class Calc
{
    public decimal Add(decimal a, decimal b) => a + b;

    public string Describe(decimal total) => string.Format("Total: {0}", total); // BENCH: BoxingAllocation
}
