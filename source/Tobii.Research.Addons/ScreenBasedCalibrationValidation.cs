using System;
using System.Collections.Generic;
using System.Linq;
using Tobii.Research.Addons.Utility;

namespace Tobii.Research.Addons
{
    public sealed class CalibrationValidationResult
    {
        public List<CalibrationValidationPoint> Points { get; private set; }

        public float AverageAccuracy { get; private set; }

        public float AveragePrecision { get; private set; }

        public float AveragePrecisionRMS { get; private set; }

        public CalibrationValidationResult()
        {
            Points = new List<CalibrationValidationPoint>();
        }

        internal void UpdateResult(List<CalibrationValidationPoint> points, float averageAccuracy, float averagePrecision, float averagePrecisionRMS)
        {
            Points = points;
            AverageAccuracy = averageAccuracy;
            AveragePrecision = averagePrecision;
            AveragePrecisionRMS = averagePrecisionRMS;
        }
    }

    public sealed class CalibrationValidationPoint
    {
        public NormalizedPoint2D Coordinates { get; private set; }

        public float AccuracyLeftEye { get; private set; }

        public float PrecisionLeftEye { get; private set; }

        public float PrecisionRMSLeftEye { get; private set; }

        public float AccuracyRightEye { get; private set; }

        public float PrecisionRightEye { get; private set; }

        public float PrecisionRMSRightEye { get; private set; }

        public bool TimedOut { get; private set; }

        public GazeDataEventArgs[] GazeData { get; private set; }

        public CalibrationValidationPoint(
            NormalizedPoint2D coordinates,
            float accuracyLeftEye,
            float precisionLeftEye,
            float accuracyRightEye,
            float precisionRightEye,
            float precisionRMSLeftEye,
            float precisionRMSRightEye,
            bool timedOut,
            GazeDataEventArgs[] gazeData)
        {
            Coordinates = coordinates;
            AccuracyLeftEye = accuracyLeftEye;
            PrecisionLeftEye = precisionLeftEye;
            AccuracyRightEye = accuracyRightEye;
            PrecisionRightEye = precisionRightEye;
            PrecisionRMSLeftEye = precisionRMSLeftEye;
            PrecisionRMSRightEye = precisionRMSRightEye;
            TimedOut = timedOut;
            GazeData = gazeData;
        }
    }

    public class ScreenBasedCalibrationValidation
    {
        public enum ValidationState
        {
            NotInValidationMode,
            NotCollectingData,
            CollectingData,
        }

        private IEyeTracker _eyeTracker;
        private Queue<GazeDataEventArgs> _data;
        private List<KeyValuePair<NormalizedPoint2D, Queue<GazeDataEventArgs>>> _dataMap;
        private TimeKeeper _timeKeeper;
        private CalibrationValidationResult _latestResult;
        private NormalizedPoint2D _currentPoint;
        private readonly object _lock = new object();
        private ValidationState _state;
        private int _sampleCount;

        public ValidationState State
        {
            get
            {
                lock (_lock)
                {
                    if (_state == ValidationState.CollectingData && _timeKeeper.TimedOut)
                    {
                        // To avoid never timing out if we do not get any
                        // data callbacks from the tracker, we need to check
                        // if we have timed out here.
                        // SaveDataForPoint changes state. 
                        SaveDataForPoint();
                    }

                    return _state;
                }
            }

            private set
            {
                lock (_lock)
                {
                    _state = value;
                }
            }
        }

        public CalibrationValidationResult Result
        {
            get
            {
                return _latestResult;
            }
        }

        public ScreenBasedCalibrationValidation(IEyeTracker eyeTracker, int sampleCount = 30, int timeoutMS = 1000)
        {
            if (eyeTracker == null)
            {
                throw new ArgumentException("Eye tracker is null");
            }

            if (sampleCount < 10 || sampleCount > 3000)
            {
                throw new ArgumentException("Samples must be between 10 and 3000");
            }

            if (timeoutMS < 100 || timeoutMS > 3000)
            {
                throw new ArgumentException("Timout must be between 100 and 3000 ms");
            }

            _eyeTracker = eyeTracker;
            _sampleCount = sampleCount;
            _timeKeeper = new TimeKeeper(timeoutMS);
            State = ValidationState.NotInValidationMode;
            _latestResult = new CalibrationValidationResult();
        }

        public void StartCollectingData(NormalizedPoint2D calibrationPointCoordinates)
        {
            if (State == ValidationState.CollectingData)
            {
                throw new InvalidOperationException("Already in collecting data state");
            }

            _currentPoint = calibrationPointCoordinates;
            _timeKeeper.Restart();
            State = ValidationState.CollectingData;
        }

        public void DiscardData(NormalizedPoint2D calibrationPointCoordinates)
        {
            if (State == ValidationState.NotInValidationMode)
            {
                throw new InvalidOperationException("Not in validation mode. No points to discard.");
            }

            lock (_lock)
            {
                if (_dataMap == null)
                {
                    throw new ArgumentException("Attempt to discard non-collected point.");
                }

                var count = _dataMap.Count;

                _dataMap = _dataMap.Where(kv => kv.Key != calibrationPointCoordinates).ToList();

                if (count == _dataMap.Count)
                {
                    throw new ArgumentException("Attempt to discard non-collected point.");
                }
            }
        }

        public void EnterValidationMode()
        {
            if (State != ValidationState.NotInValidationMode)
            {
                throw new InvalidOperationException("Validation mode already entered");
            }

            _dataMap = new List<KeyValuePair<NormalizedPoint2D, Queue<GazeDataEventArgs>>>();
            _latestResult.UpdateResult(new List<CalibrationValidationPoint>(), -1, -1, -1);
            State = ValidationState.NotCollectingData;
            _eyeTracker.GazeDataReceived += OnGazeDataReceived;
        }

        public void LeaveValidationMode()
        {
            if (State == ValidationState.NotInValidationMode)
            {
                throw new InvalidOperationException("Not in validation mode");
            }

            _eyeTracker.GazeDataReceived -= OnGazeDataReceived;
            _currentPoint = null;
            State = ValidationState.NotInValidationMode;
        }

        public CalibrationValidationResult Compute()
        {
            if (State == ValidationState.CollectingData)
            {
                throw new InvalidOperationException("Compute called while collecting data");
            }

            var points = new List<CalibrationValidationPoint>();

            foreach (var kv in _dataMap)
            {
                var targetPoint2D = kv.Key;
                var samples = kv.Value;

                var targetPoint3D = targetPoint2D.NormalizedPoint2DToPoint3D(_eyeTracker.GetDisplayArea());

                if (samples.Count < _sampleCount)
                {
                    // We timed out before collecting enough valid samples.
                    // Set the timeout flag and continue.
                    points.Add(new CalibrationValidationPoint(targetPoint2D, -1, -1, -1, -1, -1, -1, true, samples.ToArray()));
                    continue;
                }

                var gazePointAverageLeft = samples.Average(s => s.LeftEye.GazePoint.PositionInUserCoordinates);
                var gazePointAverageRight = samples.Average(s => s.RightEye.GazePoint.PositionInUserCoordinates);
                var gazeOriginAverageLeft = samples.Average(s => s.LeftEye.GazeOrigin.PositionInUserCoordinates);
                var gazeOriginAverageRight = samples.Average(s => s.RightEye.GazeOrigin.PositionInUserCoordinates);

                var directionGazePointLeft = gazeOriginAverageLeft.NormalizedDirection(gazePointAverageLeft);
                var directionTargetLeft = gazeOriginAverageLeft.NormalizedDirection(targetPoint3D);
                var accuracyLeftEye = directionTargetLeft.Angle(directionGazePointLeft);

                var directionGazePointRight = gazeOriginAverageRight.NormalizedDirection(gazePointAverageRight);
                var directionTargetRight = gazeOriginAverageRight.NormalizedDirection(targetPoint3D);
                var accuracyRightEye = directionTargetRight.Angle(directionGazePointRight);

                var varianceLeft = samples.Select(s => Math.Pow(s
                    .LeftEye.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(s.LeftEye.GazePoint.PositionInUserCoordinates)
                    .Angle(s.LeftEye.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(gazePointAverageLeft)), 2)).Average();

                var varianceRight = samples.Select(s => Math.Pow(s
                    .RightEye.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(s.RightEye.GazePoint.PositionInUserCoordinates)
                    .Angle(s.RightEye.GazeOrigin.PositionInUserCoordinates.NormalizedDirection(gazePointAverageRight)), 2)).Average();

                var precisionLeftEye = varianceLeft > 0 ? Math.Sqrt(varianceLeft) : 0;
                var precisionRightEye = varianceRight > 0 ? Math.Sqrt(varianceRight) : 0;
                var precisionRMSLeftEye = samples.RootMeanSquare(s => s.LeftEye);
                var precisionRMSRightEye = samples.RootMeanSquare(s => s.RightEye);

                points.Add(new CalibrationValidationPoint(
                    targetPoint2D,
                    (float)accuracyLeftEye,
                    (float)precisionLeftEye,
                    (float)accuracyRightEye,
                    (float)precisionRightEye,
                    (float)precisionRMSLeftEye,
                    (float)precisionRMSRightEye,
                    false,
                    samples.ToArray()));
            }

            if (points.Count == 0)
            {
                _latestResult.UpdateResult(points, 0, 0, 0);
            }
            else
            {
                var validPoints = points.Where(p => !p.TimedOut);

                if (validPoints.Count() == 0)
                {
                    _latestResult.UpdateResult(points, 0, 0, 0);
                }
                else
                {
                    var avaragePrecisionLeftEye = validPoints.Select(p => p.PrecisionLeftEye).Average();
                    var avaragePrecisionRightEye = validPoints.Select(p => p.PrecisionRightEye).Average();
                    var avarageAccuracyLeftEye = validPoints.Select(p => p.AccuracyLeftEye).Average();
                    var avarageAccuracyRightEye = validPoints.Select(p => p.AccuracyRightEye).Average();
                    var averagePrecisionRMSLeftEye = validPoints.Select(p => p.PrecisionRMSLeftEye).Average();
                    var averagePrecisionRMSRightEye = validPoints.Select(p => p.PrecisionRMSRightEye).Average();

                    _latestResult.UpdateResult(
                        points,
                        (avarageAccuracyLeftEye + avarageAccuracyRightEye) / 2.0f,
                        (avaragePrecisionLeftEye + avaragePrecisionRightEye) / 2.0f,
                        (averagePrecisionRMSLeftEye + averagePrecisionRMSRightEye) / 2.0f);
                }
            }

            return _latestResult;
        }

        private void OnGazeDataReceived(object sender, GazeDataEventArgs e)
        {
            switch (State)
            {
                case ValidationState.NotInValidationMode:
                    break;

                case ValidationState.NotCollectingData:
                    break;

                case ValidationState.CollectingData:
                    if (_data == null)
                    {
                        _data = new Queue<GazeDataEventArgs>();
                    }

                    if (_timeKeeper.TimedOut)
                    {
                        // If timeout is detected in this callback thread, save data.
                        // SaveDataForPointLocked changes state.
                        SaveDataForPointLocked();
                    }
                    else if (_data.Count < _sampleCount)
                    {
                        // We are only interested in valid samples. Here we consider both eyes.
                        if (e.LeftEye.GazePoint.Validity == Validity.Valid && e.RightEye.GazePoint.Validity == Validity.Valid)
                        {
                            _data.Enqueue(e);
                        }

                        // We have reached our count. SaveDataForPointLocked changes state.
                        if (_data.Count >= _sampleCount)
                        {
                            SaveDataForPointLocked();
                        }
                    }

                    break;

                default:
                    break;
            }
        }

        private void SaveDataForPointLocked()
        {
            lock (_lock)
            {
                SaveDataForPoint();
            }
        }

        private void SaveDataForPoint()
        {
            _dataMap.Add(new KeyValuePair<NormalizedPoint2D, Queue<GazeDataEventArgs>>(_currentPoint, _data ?? new Queue<GazeDataEventArgs>()));
            _data = null;
            _state = ValidationState.NotCollectingData;
        }
    }
}