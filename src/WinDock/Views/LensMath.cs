namespace WinDock.Views;

public readonly record struct LensLayout(double[] Left, double[] Scale, double Total);

public static class LensMath
{
    public static double Influence(double distance, double radius) =>
        radius <= 0 || distance >= radius ? 0 : (1 + Math.Cos(Math.PI * distance / radius)) / 2;

    public static LensLayout Layout(IReadOnlyList<double> widths, IReadOnlyList<bool> lens, double gap,
        double pointer, double amplitude, double magnification, double radius)
    {
        var n = widths.Count;
        var baseLeft = new double[n];
        var scale = new double[n];
        var x = 0.0;
        for (var i = 0; i < n; i++)
        {
            baseLeft[i] = x;
            x += widths[i] + (i < n - 1 ? gap : 0);
            scale[i] = 1;
        }
        var baseTotal = x;
        if (n == 0 || amplitude <= 0 || double.IsNaN(pointer)) return new LensLayout(baseLeft, scale, baseTotal);

        var grow = (magnification - 1) * amplitude;
        for (var i = 0; i < n; i++)
            if (lens[i]) scale[i] = 1 + grow * Influence(Math.Abs(baseLeft[i] + widths[i] / 2 - pointer), radius);

        var left = new double[n];
        x = 0;
        for (var i = 0; i < n; i++)
        {
            left[i] = x;
            x += widths[i] * scale[i] + (i < n - 1 ? gap : 0);
        }
        var total = x;

        var offset = pointer - Magnified(pointer, widths, scale, baseLeft, left, baseTotal, total);
        for (var i = 0; i < n; i++) left[i] += offset;
        return new LensLayout(left, scale, total);
    }

    static double Magnified(double pointer, IReadOnlyList<double> widths, double[] scale, double[] baseLeft, double[] left,
        double baseTotal, double total)
    {
        if (pointer <= 0) return pointer;
        if (pointer >= baseTotal) return total + (pointer - baseTotal);
        var i = baseLeft.Length - 1;
        while (i > 0 && baseLeft[i] > pointer) i--;
        var inside = pointer - baseLeft[i];
        return inside <= widths[i]
            ? left[i] + inside * scale[i]
            : left[i] + widths[i] * scale[i] + (inside - widths[i]);
    }

    public static double Allowance(double magnification, double radius, double tile) =>
        Math.Max(0, magnification - 1) * (radius + tile);
}
