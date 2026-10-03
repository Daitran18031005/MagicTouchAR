namespace MagicTouch.Recognition
{
    /// <summary>
    /// The 16 target gesture symbols for Magic Touch AR.
    /// Includes both single-stroke and multi-stroke patterns.
    /// </summary>
    public enum GestureSymbol
    {
        None = 0,
        V,
        Caret,          // ^ (Inverted V)
        HorizontalLine, // -
        VerticalLine,   // |
        Teardrop,       // Loop with cross at top
        O,              // Circle
        Z,              // Zigzag
        VerticalLoop,   // Ribbon / vertical cursive loop
        PretzelKnot,    // Horizontal double-loop ribbon
        W,
        Figure8,        // Vertical figure-8 / infinity
        E,
        X,              // Multi-stroke cross
        SwirlG,         // Spiral / cursive G
        Checkmark,      // Short down, long up
        S,
        Unknown         // Garbage / ambiguous / rejected drawing
    }
}
