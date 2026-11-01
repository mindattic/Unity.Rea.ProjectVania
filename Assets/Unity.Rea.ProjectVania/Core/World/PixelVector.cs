namespace Unity.Rea.ProjectVania.Core.World
{
    /// <summary>
    /// A position in the original's pixels, y down (XNA's <c>Vector2</c>). A mutable value with public fields, as the
    /// original's was, so a query can snap it through <c>ref</c> exactly where the original did.
    /// </summary>
    public struct PixelVector
    {
        public float X;
        public float Y;

        public PixelVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
