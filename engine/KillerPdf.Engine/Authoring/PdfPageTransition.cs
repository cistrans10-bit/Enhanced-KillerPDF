using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

/// <summary>Orientation used by split and blinds page transitions.</summary>
public enum PdfTransitionDimension
{
    /// <summary>Uses horizontal bands or movement.</summary>
    Horizontal,
    /// <summary>Uses vertical bands or movement.</summary>
    Vertical
}
/// <summary>Direction of motion toward or away from the page center.</summary>
public enum PdfTransitionMotion
{
    /// <summary>Moves inward toward the center.</summary>
    Inward,
    /// <summary>Moves outward from the center.</summary>
    Outward
}

/// <summary>A validated visual transition used when advancing to a PDF page.</summary>
public sealed class PdfPageTransition
{
    private PdfPageTransition(
        PdfPageTransitionStyle style, double duration,
        PdfTransitionDimension? dimension = null, PdfTransitionMotion? motion = null,
        int? direction = null, double? scale = null, bool opaque = false)
    {
        if (!double.IsFinite(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        Style = style;
        Duration = duration;
        Dimension = dimension;
        Motion = motion;
        Direction = direction;
        Scale = scale;
        Opaque = opaque;
    }

    /// <summary>Replaces the old page immediately without a visual effect.</summary>
    public static PdfPageTransition Replace(double duration = 1) =>
        new(PdfPageTransitionStyle.Replace, duration);
    /// <summary>Dissolves the old page into the new page.</summary>
    public static PdfPageTransition Dissolve(double duration = 1) =>
        new(PdfPageTransitionStyle.Dissolve, duration);
    /// <summary>Fades the old page into the new page.</summary>
    public static PdfPageTransition Fade(double duration = 1) =>
        new(PdfPageTransitionStyle.Fade, duration);
    /// <summary>Splits the page into bands moving inward or outward.</summary>
    public static PdfPageTransition Split(
        PdfTransitionDimension dimension, PdfTransitionMotion motion, double duration = 1) =>
        new(PdfPageTransitionStyle.Split, duration,
            DimensionValue(dimension), MotionValue(motion));
    /// <summary>Reveals the page through horizontal or vertical blinds.</summary>
    public static PdfPageTransition Blinds(
        PdfTransitionDimension dimension, double duration = 1) =>
        new(PdfPageTransitionStyle.Blinds, duration, DimensionValue(dimension));
    /// <summary>Uses an inward or outward rectangular box transition.</summary>
    public static PdfPageTransition Box(PdfTransitionMotion motion, double duration = 1) =>
        new(PdfPageTransitionStyle.Box, duration, motion: MotionValue(motion));
    /// <summary>Wipes the new page across the old page in a cardinal direction.</summary>
    public static PdfPageTransition Wipe(int direction, double duration = 1) =>
        new(PdfPageTransitionStyle.Wipe, duration, direction: Cardinal(direction));
    /// <summary>Uses a glitter transition in a supported direction.</summary>
    public static PdfPageTransition Glitter(int direction, double duration = 1)
    {
        if (direction is not (0 or 270 or 315))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return new(PdfPageTransitionStyle.Glitter, duration, direction: direction);
    }
    /// <summary>Flies the page into or out of view with optional scaling and opacity.</summary>
    public static PdfPageTransition Fly(
        int direction, PdfTransitionMotion motion, double scale = 1,
        bool opaque = false, double duration = 1)
    {
        if (!double.IsFinite(scale) || scale is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(scale));
        return new(PdfPageTransitionStyle.Fly, duration, motion: MotionValue(motion),
            direction: Cardinal(direction), scale: scale, opaque: opaque);
    }
    /// <summary>Pushes the old page away while the new page enters.</summary>
    public static PdfPageTransition Push(int direction, double duration = 1) =>
        new(PdfPageTransitionStyle.Push, duration, direction: Cardinal(direction));
    /// <summary>Slides the new page over the old page.</summary>
    public static PdfPageTransition Cover(int direction, double duration = 1) =>
        new(PdfPageTransitionStyle.Cover, duration, direction: Cardinal(direction));
    /// <summary>Slides the old page away to reveal the new page.</summary>
    public static PdfPageTransition Uncover(int direction, double duration = 1) =>
        new(PdfPageTransitionStyle.Uncover, duration, direction: Cardinal(direction));

    internal PdfPageTransitionStyle Style { get; }
    /// <summary>Gets the positive transition duration in seconds.</summary>
    public double Duration { get; }
    internal PdfTransitionDimension? Dimension { get; }
    internal PdfTransitionMotion? Motion { get; }
    internal int? Direction { get; }
    internal double? Scale { get; }
    internal bool Opaque { get; }

    internal PdfDictionary ToDictionary()
    {
        var entries = new List<KeyValuePair<PdfName, PdfObject>>
        {
            new(Name("S"), Name(Style == PdfPageTransitionStyle.Replace
                ? "R" : Style.ToString())),
            new(Name("D"), Number(Duration))
        };
        if (Dimension.HasValue)
            entries.Add(new(Name("Dm"), Name(
                Dimension == PdfTransitionDimension.Horizontal ? "H" : "V")));
        if (Motion.HasValue)
            entries.Add(new(Name("M"), Name(
                Motion == PdfTransitionMotion.Inward ? "I" : "O")));
        if (Direction.HasValue)
            entries.Add(new(Name("Di"), new PdfInteger(Direction.Value)));
        if (Scale.HasValue)
            entries.Add(new(Name("SS"), Number(Scale.Value)));
        if (Opaque)
            entries.Add(new(Name("B"), new PdfBoolean(true)));
        return new PdfDictionary(entries);

        static PdfName Name(string value) =>
            new(System.Text.Encoding.ASCII.GetBytes(value));
        static PdfObject Number(double value) =>
            value == Math.Truncate(value) && value is >= long.MinValue and <= long.MaxValue
                ? new PdfInteger((long)value) : new PdfReal(value);
    }

    private static int Cardinal(int direction)
    {
        if (direction is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return direction;
    }
    private static PdfTransitionDimension DimensionValue(PdfTransitionDimension value) =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static PdfTransitionMotion MotionValue(PdfTransitionMotion value) =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
}

internal enum PdfPageTransitionStyle
{
    Split, Blinds, Box, Wipe, Dissolve, Glitter, Replace, Fly, Push, Cover, Uncover, Fade
}
