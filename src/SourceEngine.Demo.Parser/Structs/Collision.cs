namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// Oriented bounding box (OBB) of collideable entities.
    /// </summary>
    /// <remarks>
    /// The Source Engine stores the bounds in a <c>CCollisionProperty</c> on the entity via <c>m_Collision</c>.
    ///
    /// It may be an axis-aligned bounding box (AABB) depending on <c>m_nSolidType</c> and <c>m_nSolidType</c>.
    /// This object isn't aware of such flags; it simply stores the bounds as given. The bounds may have been modified
    /// externally from the <c>CCollisionProperty</c>'s <c>m_vecMins</c> and <c>m_vecMaxs</c>. For example, currently
    /// bomb site bounds are adjusted to be relative to world-space before constructing this object.
    ///
    /// The engine can compute the surrounding bounds in various ways based on the value of <c>m_nSurroundType</c>.
    /// For VPhysics-enabled entities, the default computes collision from bounds derived from the VPhysics object.
    /// Otherwise, the default computes a box in world-space surrounding the collision bounds.
    ///
    /// There is a special case for triggers (<c>FSOLID_USE_TRIGGER_BOUNDS</c> is set) which adds <c>m_triggerBloat</c>
    /// to the computer bounds. The bloat value is halved when added to Z.
    /// </remarks>
    public class BoundingBox
    {
        /// <summary>
        /// The minimum extent of the bounds.
        /// </summary>
        public Vector Min { get; set; }

        /// <summary>
        /// The maximum extent of the bounds.
        /// </summary>
        public Vector Max { get; set; }

        /// <summary>
        /// Check whether a point lies within the bounds.
        /// </summary>
        /// <remarks>
        /// The point is assumed to be relative to the same space as the bounds (e.g. both world-space).
        /// </remarks>
        /// <param name="point">The point to check.</param>
        public bool Contains(Vector point)
        {
            return point.X >= Min.X && point.X <= Max.X
                && point.Y >= Min.Y && point.Y <= Max.Y
                && point.Z >= Min.Z && point.Z <= Max.Z;
        }
    }
}
