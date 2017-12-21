using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tobii.Research.Addons.Utility
{
    internal static class Extensions
    {
        internal static double Magnitude(float x, float y, float z)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        internal static double Magnitude(this Point3D vec)
        {
            return Magnitude(vec.X, vec.Y, vec.Z);
        }

        internal static double Magnitude(float x, float y)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }

        internal static double Magnitude(this NormalizedPoint2D vec)
        {
            return Magnitude(vec.X, vec.Y);
        }

        internal static Point3D Add(this Point3D me, Point3D other)
        {
            return new Point3D(me.X + other.X, me.Y + other.Y, me.Z + other.Z);
        }

        internal static Point3D Sub(this Point3D me, Point3D other)
        {
            return new Point3D(me.X - other.X, me.Y - other.Y, me.Z - other.Z);
        }

        internal static Point3D Mul(this Point3D me, double other)
        {
            return new Point3D(me.X * (float)other, me.Y * (float)other, me.Z * (float)other);
        }

        internal static Point3D Div(this Point3D me, double other)
        {
            return new Point3D(me.X / (float)other, me.Y / (float)other, me.Z / (float)other);
        }

        internal static Point3D Normalize(this Point3D vec)
        {
            var mag = (float)Magnitude(vec.X, vec.Y, vec.Z);
            return new Point3D(vec.X / mag, vec.Y / mag, vec.Z / mag);
        }

        internal static Point3D NormalizedDirection(this Point3D start, Point3D end)
        {
            return Normalize(Direction(start, end));
        }

        internal static Point3D Direction(this Point3D start, Point3D end)
        {
            var x = end.X - start.X;
            var y = end.Y - start.Y;
            var z = end.Z - start.Z;
            return new Point3D(x, y, z);
        }

        internal static double DotProduct(this Point3D me, Point3D other)
        {
            return me.X * other.X + me.Y * other.Y + me.Z * other.Z;
        }

        private static double Clamp(this double me, double low = -1d, double high = 1d)
        {
            me = Math.Min(high, me);
            me = Math.Max(low, me);
            return me;
        }

        internal static double Angle(this Point3D me, Point3D other)
        {
            return ((Math.Acos((me.DotProduct(other) / (me.Magnitude() * other.Magnitude())).Clamp()) * 180 / Math.PI) + 360) % 360;
        }

        public static double MeanAngle(this double[] angles)
        {
            var x = angles.Sum(a => Math.Cos(a * Math.PI / 180)) / angles.Length;
            var y = angles.Sum(a => Math.Sin(a * Math.PI / 180)) / angles.Length;
            return Math.Atan2(y, x) * 180.0 / Math.PI;
        }

        internal static Point3D Average(this Queue<GazeDataEventArgs> queue, Func<GazeDataEventArgs, Point3D> selector)
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

        internal static Point3D NormalizedPoint2DToPoint3D(this NormalizedPoint2D point2D, DisplayArea displayArea)
        {
            var dx = displayArea.TopRight.Sub(displayArea.TopLeft).Mul(point2D.X);
            var dy = displayArea.BottomLeft.Sub(displayArea.TopLeft).Mul(point2D.Y);
            return displayArea.TopLeft.Add(dx.Add(dy));
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