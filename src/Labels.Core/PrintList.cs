namespace Labels.Core;

public sealed record PrintListEntry(LabelDesign Design, int Quantity);

public sealed class PrintList
{
    public const int MaxLines = 100;
    public const int MaxLineQuantity = 1000;
    public const int MaxLabels = 1000;

    private readonly List<PrintListEntry> entries = [];

    public IReadOnlyList<PrintListEntry> Entries => entries;
    public int TotalLabels => entries.Sum(static entry => entry.Quantity);

    public int Add(LabelDesign design, int quantity)
    {
        if (design is null) throw new ArgumentException("This label is missing.");
        design.Validate();
        RequireQuantity(quantity);
        int index = entries.FindIndex(entry => entry.Design == design);
        if (index >= 0)
        {
            int combined = entries[index].Quantity + quantity;
            if (combined > MaxLineQuantity)
                throw new ArgumentException($"Each label can have at most {MaxLineQuantity} copies in one job.");
            EnsureTotal(TotalLabels + quantity);
            entries[index] = entries[index] with { Quantity = combined };
            return index;
        }

        if (entries.Count >= MaxLines)
            throw new ArgumentException($"A print list can hold at most {MaxLines} different labels.");
        EnsureTotal(TotalLabels + quantity);
        entries.Add(new PrintListEntry(design, quantity));
        return entries.Count - 1;
    }

    public void SetQuantity(int index, int quantity)
    {
        RequireIndex(index);
        RequireQuantity(quantity);
        EnsureTotal(TotalLabels - entries[index].Quantity + quantity);
        entries[index] = entries[index] with { Quantity = quantity };
    }

    public void RemoveAt(int index)
    {
        RequireIndex(index);
        entries.RemoveAt(index);
    }

    public void Move(int index, int destination)
    {
        RequireIndex(index);
        RequireIndex(destination);
        if (index == destination) return;
        var entry = entries[index];
        entries.RemoveAt(index);
        entries.Insert(destination, entry);
    }

    public void Clear() => entries.Clear();

    public LabelDesign DesignAt(int pageIndex) => DesignAt(entries, pageIndex);

    public static LabelDesign DesignAt(IReadOnlyList<PrintListEntry> entries, int pageIndex)
    {
        if (entries is null) throw new ArgumentException("This print list is missing.");
        if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        int seen = 0;
        foreach (var entry in entries)
        {
            if (pageIndex < seen + entry.Quantity) return entry.Design;
            seen += entry.Quantity;
        }
        throw new ArgumentOutOfRangeException(nameof(pageIndex));
    }

    private void RequireIndex(int index)
    {
        if ((uint)index >= (uint)entries.Count) throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static void RequireQuantity(int quantity)
    {
        if (quantity < 1 || quantity > MaxLineQuantity)
            throw new ArgumentException($"Each label needs a quantity from 1 to {MaxLineQuantity}.");
    }

    private static void EnsureTotal(int total)
    {
        if (total > MaxLabels)
            throw new ArgumentException($"A print job can include at most {MaxLabels} labels.");
    }
}
