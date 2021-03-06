using System;

using SourceEngine.Demo.Parser.BitStream;

namespace SourceEngine.Demo.Parser.Structs
{
    /// <summary>
    /// And Source-Engine Vector.
    /// </summary>
    public class Vector
    {
        public Vector() { }

        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public double Angle2D => Math.Atan2(Y, X);

        public double Absolute => Math.Sqrt(AbsoluteSquared);

        public double AbsoluteSquared => X * X + Y * Y + Z * Z;

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
        /// Copy this instance. So if you want to permanently store the position of a player at a point in time,
        /// COPY it.
        /// </summary>
        public Vector Copy()
        {
            return new(X, Y, Z);
        }

        public static Vector operator +(Vector a, Vector b)
        {
            return new()
            {
                X = a.X + b.X,
                Y = a.Y + b.Y,
                Z = a.Z + b.Z,
            };
        }

        public static Vector operator -(Vector a, Vector b)
        {
            return new()
            {
                X = a.X - b.X,
                Y = a.Y - b.Y,
                Z = a.Z - b.Z,
            };
        }

        public override string ToString()
        {
            return "{X: " + X + ", Y: " + Y + ", Z: " + Z + " }";
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
