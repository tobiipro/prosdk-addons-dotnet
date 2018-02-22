using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tobii.Research.Addons.Utility
{
    /// <summary>
    /// Extensions with some operations on <see cref="Point3D"/> and <see cref="NormalizedPoint2D"/> among other things.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Get the length of the vector.
        /// </summary>
        /// <param name="x">The x parameter of the vector.</param>
        /// <param name="y">The y parameter of the vector.</param>
        /// <param name="z">The z parameter of the vector.</param>
        /// <returns>The length (magnitude) of the vector.</returns>
        public static double Magnitude(float x, float y, float z)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        /// <summary>
        /// Get the length of the vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The length (magnitude) of the vector.</returns>
        public static double Magnitude(this Point3D vector)
        {
            return Magnitude(vector.X, vector.Y, vector.Z);
        }

        /// <summary>
        /// Get the length of the vector.
        /// </summary>
        /// <param name="x">The x parameter of the vector.</param>
        /// <param name="y">The y parameter of the vector.</param>
        /// <returns>The length (magnitude) of the vector.</returns>
        public static double Magnitude(float x, float y)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }

        /// <summary>
        /// Get the length of the vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The length (magnitude) of the vector.</returns>
        public static double Magnitude(this NormalizedPoint2D vector)
        {
            return Magnitude(vector.X, vector.Y);
        }

        /// <summary>
        /// Add another vector to this vector.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The other vector.</param>
        /// <returns>A <see cref="Point3D"/> with the result of the addition.</returns>
        public static Point3D Add(this Point3D me, Point3D other)
        {
            return new Point3D(me.X + other.X, me.Y + other.Y, me.Z + other.Z);
        }

        /// <summary>
        /// Subtract another vector from this vector.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The other vector.</param>
        /// <returns>A <see cref="Point3D"/> with the result of the subtraction.</returns>
        public static Point3D Sub(this Point3D me, Point3D other)
        {
            return new Point3D(me.X - other.X, me.Y - other.Y, me.Z - other.Z);
        }

        /// <summary>
        /// Multiply this vector by a scalar.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The scalar</param>
        /// <returns>A <see cref="Point3D"/> with the result of the multiplication.</returns>
        public static Point3D Mul(this Point3D me, double other)
        {
            return new Point3D(me.X * (float)other, me.Y * (float)other, me.Z * (float)other);
        }

        /// <summary>
        /// Divide this vector by a scalar.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The scalar.</param>
        /// <returns>A <see cref="Point3D"/> with the result of the division.</returns>
        public static Point3D Div(this Point3D me, double other)
        {
            return new Point3D(me.X / (float)other, me.Y / (float)other, me.Z / (float)other);
        }

        /// <summary>
        /// Get a vector in the same direction but with the length 1.
        /// </summary>
        /// <param name="vector">The vector to normalize.</param>
        /// <returns>The normalized <see cref="Point3D"/> vector.</returns>
        public static Point3D Normalize(this Point3D vector)
        {
            var mag = (float)Magnitude(vector.X, vector.Y, vector.Z);
            return new Point3D(vector.X / mag, vector.Y / mag, vector.Z / mag);
        }

        /// <summary>
        /// Get the normalized direction from this point to another point.
        /// </summary>
        /// <param name="start">This point, the origin.</param>
        /// <param name="end">The end point.</param>
        /// <returns>A <see cref="Point3D"/> direction vector with the length 1.</returns>
        public static Point3D NormalizedDirection(this Point3D start, Point3D end)
        {
            return Normalize(Direction(start, end));
        }

        /// <summary>
        /// Get the direction from this point to another point.
        /// </summary>
        /// <param name="start">This point, the origin.</param>
        /// <param name="end">The end point.</param>
        /// <returns>A <see cref="Point3D"/> direction vector.</returns>
        public static Point3D Direction(this Point3D start, Point3D end)
        {
            var x = end.X - start.X;
            var y = end.Y - start.Y;
            var z = end.Z - start.Z;
            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Get the dot product between this vector and another vector.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The other vector.</param>
        /// <returns>The dot product.</returns>
        public static double DotProduct(this Point3D me, Point3D other)
        {
            return me.X * other.X + me.Y * other.Y + me.Z * other.Z;
        }

        /// <summary>
        /// Clamp a value between two other values.
        /// </summary>
        /// <param name="me">The value to clamp.</param>
        /// <param name="low">The minimum value. Default -1.</param>
        /// <param name="high">The maximum value. Default 1.</param>
        /// <returns>The value clamped between low and high.</returns>
        private static double Clamp(this double me, double low = -1d, double high = 1d)
        {
            me = Math.Min(high, me);
            me = Math.Max(low, me);
            return me;
        }

        /// <summary>
        /// Get the angle in degrees between this vector and another vector.
        /// </summary>
        /// <param name="me">This vector.</param>
        /// <param name="other">The other vector.</param>
        /// <returns>The angle between the vectors in degrees.</returns>
        public static double Angle(this Point3D me, Point3D other)
        {
            return ((Math.Acos((me.DotProduct(other) / (me.Magnitude() * other.Magnitude())).Clamp()) * 180 / Math.PI) + 360) % 360;
        }

        /// <summary>
        /// Get the average <see cref="Point3D"/> from a queue of <see cref="GazeDataEventArgs"/>.
        /// The points to average is selected by the provided selector.
        /// </summary>
        /// <param name="queue">The queue of <see cref="GazeDataEventArgs"/></param>
        /// <param name="selector">The point selector.</param>
        /// <returns>The average <see cref="Point3D"/> point.</returns>
        public static Point3D Average(this Queue<GazeDataEventArgs> queue, Func<GazeDataEventArgs, Point3D> selector)
        {
            var x = 0.0;
            var y = 0.0;
            var z = 0.0;

            foreach (var item in queue)
            {
                var p = selector(item);
                x += p.X;
                y += p.Y;
                z += p.Z;
            }

            return new Point3D((float)(x / queue.Count), (float)(y / queue.Count), (float)(z / queue.Count));
        }

        /// <summary>
        /// Get the 3D gaze point representation based on the normalized 2D point and the <see cref="DisplayArea"/> information.
        /// </summary>
        /// <param name="point2D">The <see cref="NormalizedPoint2D"/> point.</param>
        /// <param name="displayArea">The <see cref="DisplayArea"/> object.</param>
        /// <returns>The <see cref="Point3D"/> gaze point.</returns>
        public static Point3D NormalizedPoint2DToPoint3D(this NormalizedPoint2D point2D, DisplayArea displayArea)
        {
            var dx = displayArea.TopRight.Sub(displayArea.TopLeft).Mul(point2D.X);
            var dy = displayArea.BottomLeft.Sub(displayArea.TopLeft).Mul(point2D.Y);
            return displayArea.TopLeft.Add(dx.Add(dy));
        }

        /// <summary>
        /// Get the root mean square precision in degrees from a queue of <see cref="GazeDataEventArgs"/>.
        /// The eye is selected by the provided selector.
        /// </summary>
        /// <param name="queue">The queue of <see cref="GazeDataEventArgs"/>.</param>
        /// <param name="selector">The eye selector.</param>
        /// <returns>The precision root mean square in degrees for the selected eye.</returns>
        public static double PrecisionRMS(this Queue<GazeDataEventArgs> queue, Func<GazeDataEventArgs, EyeData> selector)
        {
            if (queue.Count < 2)
            {
                throw new ArgumentException("Can't calculate RMS on queue of less than 2 items.");
            }

            var ret = 0.0;
            var array = queue.ToArray();

            for (int i = 0; i < array.Length - 1; i++)
            {
                var curr = selector(array[i]);
                var next = selector(array[i + 1]);
                var dirCurr = curr.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(curr.GazePoint.PositionInUserCoordinates);
                var dirNext = next.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(next.GazePoint.PositionInUserCoordinates);
                ret += Math.Pow(dirCurr.Angle(dirNext), 2);
            }

            return Math.Sqrt(ret / (array.Length - 1));
        }
    }

    internal class TimeKeeper
    {
        private readonly object _lock = new object();
        private Stopwatch _stopwatch = new Stopwatch();
        private int _timeoutMS;

        public bool TimedOut
        {
            get
            {
                lock (_lock)
                {
                    return _stopwatch.ElapsedMilliseconds >= _timeoutMS;
                }
            }
        }

        public void Restart()
        {
            lock (_lock)
            {
                _stopwatch.Reset();
                _stopwatch.Start();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopwatch.Stop();
            }
        }

        public TimeKeeper(int timeoutMS)
        {
            _timeoutMS = timeoutMS;
        }
    }
}