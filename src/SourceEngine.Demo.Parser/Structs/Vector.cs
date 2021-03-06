using System;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// 3-dimensional Euclidean vector.
    /// </summary>
    /// <remarks>
    /// Represents a line with a direction and length, starting at the current origin.
    /// There is no inherit orientation. Typically the vector is interpreted as world axis aligned,
    /// but it is sometimes object axis aligned instead.
    /// </remarks>
    public class Vector
    {
        /// <summary>
        /// Scalar component on the x axis.
        /// </summary>
        /// <remarks>
        /// Positive values go forwards and negative values go backwards.
        /// </remarks>
        public float X { get; set; }

        /// <summary>
        /// Scalar component on the y axis.
        /// </summary>
        /// <remarks>
        /// Positive values go left and negative values go right.
        /// </remarks>
        public float Y { get; set; }

        /// <summary>
        /// Scalar component on the z axis.
        /// </summary>
        /// <remarks>
        /// Positive values go up and negative values go down.
        /// </remarks>
        public float Z { get; set; }

        /// <summary>
        /// Angle of the first two dimensions (y and x) of the vector.
        /// </summary>
        public double Angle2D => Math.Atan2(Y, X);

        /// <summary>
        /// Absolute value of the vector.
        /// </summary>
        public double Absolute => Math.Sqrt(AbsoluteSquared);

        /// <summary>
        /// Square of the absolute value of the vector.
        /// </summary>
        public double AbsoluteSquared => X * X + Y * Y + Z * Z;

        /// <summary>
        /// Parse a raw data stream into a new <see cref="Vector"/>.
        /// </summary>
        /// <param name="reader">The data stream to parse.</param>
        /// <returns>A <see cref="Vector"/> containing values from the parsed stream.</returns>
        public static Vector Parse(IBitStream reader)
        {
            return new()
            {
                X = reader.ReadFloat(),
                Y = reader.ReadFloat(),
                Z = reader.ReadFloat(),
            };
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="Vector"/> class that represents the zero vector.
        /// </summary>
        public Vector() { }

        /// <summary>
        /// Initialises a new instance of the <see cref="Vector"/> class with the specified scalar components.
        /// </summary>
        /// <param name="x">The scalar component on the x axis of the new <see cref="Vector"/>.</param>
        /// <param name="y">The scalar component on the y axis of the new <see cref="Vector"/>.</param>
        /// <param name="z">The scalar component on the z axis of the new <see cref="Vector"/>.</param>
        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Returns a copy of the current instance.
        /// </summary>
        /// <remarks>
        /// To save the position of a player at a specific point in time, use this.
        /// </remarks>
        /// <returns>A copy of the current instance.</returns>
        public Vector Copy()
        {
            return new(X, Y, Z);
        }

        /// <summary>
        /// Adds two <see cref="Vector"/>s.
        /// </summary>
        /// <param name="a">The augend.</param>
        /// <param name="b">The addend.</param>
        /// <returns>The sum of the two <see cref="Vector"/>s.</returns>
        public static Vector operator +(Vector a, Vector b)
        {
            return new()
            {
                X = a.X + b.X,
                Y = a.Y + b.Y,
                Z = a.Z + b.Z,
            };
        }

        /// <summary>
        /// Subtracts two <see cref="Vector"/>s.
        /// </summary>
        /// <param name="a">The minuend.</param>
        /// <param name="b">The subtrahend.</param>
        /// <returns>The differences of the two <see cref="Vector"/>s.</returns>
        public static Vector operator -(Vector a, Vector b)
        {
            return new()
            {
                X = a.X - b.X,
                Y = a.Y - b.Y,
                Z = a.Z - b.Z,
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{{X: {X}, Y: {Y}, Z: {Z} }}";
        }
    }

    /// <summary>
    /// 3-dimensional Euler angle.
    /// </summary>
    internal class QAngle
    {
        /// <summary>
        /// Pitch angle in degrees.
        /// </summary>
        /// <remarks>
        /// Positive values go down and negative values go up.
        /// </remarks>
        public float X { get; private set; }

        /// <summary>
        /// Yaw angle in degrees.
        /// </summary>
        /// <remarks>
        /// Positive values go left and negative values go right.
        /// </remarks>
        public float Y { get; private set; }

        /// <summary>
        /// Roll angle in degrees.
        /// </summary>
        /// <remarks>
        /// Positive values go right and negative values go left.
        /// </remarks>
        public float Z { get; private set; }

        /// <summary>
        /// Parse a raw data stream into a new <see cref="QAngle"/>.
        /// </summary>
        /// <param name="reader">The data stream to parse.</param>
        /// <returns>A <see cref="QAngle"/> containing values from the parsed stream.</returns>
        public static QAngle Parse(IBitStream reader)
        {
            return new()
            {
                X = reader.ReadFloat(),
                Y = reader.ReadFloat(),
                Z = reader.ReadFloat(),
            };
        }
    }
}
