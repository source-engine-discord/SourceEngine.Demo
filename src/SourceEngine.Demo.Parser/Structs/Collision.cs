namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// This contains information about Collideables (specific edicts), mostly used for bombsites.
    /// </summary>
    public class BoundingBoxInformation
    {
        public Vector Min { get; set; }

        public Vector Max { get; set; }

        /// <summary>
        /// Checks whether a point lies within the BoundingBox.
        /// </summary>
        /// <param name="point">The point to check</param>
        public bool Contains(Vector point)
        {
            return point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y && point.Z >= Min.Z
                && point.Z <= Max.Z;
        }
    }
}
