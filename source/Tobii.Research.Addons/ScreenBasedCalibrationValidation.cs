using System;
using System.Collections.Generic;
using System.Linq;
using Tobii.Research.Addons.Utility;

namespace Tobii.Research.Addons
{
    /// <summary>
    /// Contains the result of the calibration validation.
    /// </summary>
    public sealed class CalibrationValidationResult
    {
        /// <summary>
        /// The results of the calibration validation per point (same points as were collected).
        /// </summary>
        public List<CalibrationValidationPoint> Points { get; private set; }

        /// <summary>
        /// The accuracy in degrees averaged over all collected points for the left eye.
        /// </summary>
        public float AverageAccuracyLeftEye { get; private set; }

        /// <summary>
        /// The precision (standard deviation) in degrees averaged over all collected points for the left eye.
        /// </summary>
        public float AveragePrecisionLeftEye { get; private set; }

        /// <summary>
        /// The precision (root mean square of sample-to-sample error) in degrees averaged over all collected points for the left eye.
        /// </summary>
        public float AveragePrecisionRMSLeftEye { get; private set; }

        /// <summary>
        /// The accuracy in degrees averaged over all collected points for the right eye.
        /// </summary>
        public float AverageAccuracyRightEye { get; private set; }

        /// <summary>
        /// The precision (standard deviation) in degrees averaged over all collected points for the right eye.
        /// </summary>
        public float AveragePrecisionRightEye { get; private set; }

        /// <summary>
        /// The precision (root mean square of sample-to-sample error) in degrees averaged over all collected points for the right eye.
        /// </summary>
        public float AveragePrecisionRMSRightEye { get; private set; }

        internal CalibrationValidationResult()
        {
            Points = new List<CalibrationValidationPoint>();
        }

        internal void UpdateResult(
            List<CalibrationValidationPoint> points,
            float averageAccuracyLeftEye,
            float averagePrecisionLeftEye,
            float averagePrecisionRMSLeftEye,
            float averageAccuracyRightEye,
            float averagePrecisionRightEye,
            float averagePrecisionRMSRightEye)
        {
            Points = points;
            AverageAccuracyLeftEye = averageAccuracyLeftEye;
            AveragePrecisionLeftEye = averagePrecisionLeftEye;
            AveragePrecisionRMSLeftEye = averagePrecisionRMSLeftEye;
            AverageAccuracyRightEye = averageAccuracyRightEye;
            AveragePrecisionRightEye = averagePrecisionRightEye;
            AveragePrecisionRMSRightEye = averagePrecisionRMSRightEye;
        }
    }

    /// <summary>
    /// Represents a collected point that goes into the calibration validation.
    /// It contains calculated values for accuracy and precision as well as
    /// the original gaze samples collected for the point.
    /// </summary>
    public sealed class CalibrationValidationPoint
    {
        /// <summary>
        /// The 2D coordinates of this point (in Active Display Coordinate System).
        /// </summary>
        public NormalizedPoint2D Coordinates { get; private set; }

        /// <summary>
        /// The accuracy in degrees for the left eye.
        /// </summary>
        public float AccuracyLeftEye { get; private set; }

        /// <summary>
        /// The precision (standard deviation) in degrees for the left eye.
        /// </summary>
        public float PrecisionLeftEye { get; private set; }

        /// <summary>
        /// The precision (root mean square of sample-to-sample error) in degrees for the left eye.
        /// </summary>
        public float PrecisionRMSLeftEye { get; private set; }

        /// <summary>
        /// The accuracy in degrees for the right eye.
        /// </summary>
        public float AccuracyRightEye { get; private set; }

        /// <summary>
        /// The precision (standard deviation) in degrees for the right eye.
        /// </summary>
        public float PrecisionRightEye { get; private set; }

        /// <summary>
        /// The precision (root mean square of sample-to-sample error) in degrees for the right eye.
        /// </summary>
        public float PrecisionRMSRightEye { get; private set; }

        /// <summary>
        /// A boolean indicating if there was a timeout while collecting data for this point.
        /// </summary>
        public bool TimedOut { get; private set; }

        /// <summary>
        /// The gaze data samples collected for this point. These samples are the base for the calculated accuracy and precision.
        /// </summary>
        public GazeDataEventArgs[] GazeData { get; private set; }

        internal CalibrationValidationPoint(
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

    /// <summary>
    /// Provides methods and properties for managing calibration validation for screen based eye trackers.
    /// </summary>
    public class ScreenBasedCalibrationValidation : IDisposable
    {
        /// <summary>
        /// <see cref="ValidationState.NotInValidationMode"/> - <see cref="EnterValidationMode"/> must be called starting to collect data.
        /// <see cref="ValidationState.NotCollectingData"/> - Ready to start collecting data or computing result.
        /// <see cref="ValidationState.CollectingData"/> - Currently collecting data. Will finish after the sample count is reached or a timeout.
        /// </summary>
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

        /// <summary>
        /// Get the current state of the validation object.
        /// </summary>
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

        /// <summary>
        /// Get the current <see cref="CalibrationValidationResult"/> with the latest computed accuracy and precision.
        /// <see cref="Compute"/> must have been called for this to contain valid data.
        /// </summary>
        public CalibrationValidationResult Result
        {
            get
            {
                return _latestResult;
            }
        }

        /// <summary>
        /// Create a calibration validation object for screen based eye trackers.
        /// </summary>
        /// <param name="eyeTracker">An <see cref="IEyeTracker"/> instance.</param>
        /// <param name="sampleCount">The number of samples to collect. Default 30, minimum 10, maximum 3000.</param>
        /// <param name="timeoutMS">Timeout in milliseconds. Default 1000, minimum 100, maximum 3000.</param>
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
            _latestResult = new CalibrationValidationResult();
            State = ValidationState.NotInValidationMode;
        }

        /// <summary>
        /// Starts collecting data for a calibration validation point.The argument used is the point the user
        /// is assumed to be looking at and is given in the active display area coordinate system.
        /// Please check State property to know when data collection is completed (or timed out).
        /// </summary>
        /// <param name="calibrationPointCoordinates">The normalized 2D point on the display area</param>
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

        /// <summary>
        /// Removes the collected data for a specific calibration validation point.
        /// </summary>
        /// <param name="calibrationPointCoordinates">The calibration point to remove.</param>
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

        /// <summary>
        /// Enter the calibration validation mode and starts subscribing to gaze data from the eye tracker.
        /// </summary>
        public void EnterValidationMode()
        {
            if (State != ValidationState.NotInValidationMode)
            {
                throw new InvalidOperationException("Validation mode already entered");
            }

            _dataMap = new List<KeyValuePair<NormalizedPoint2D, Queue<GazeDataEventArgs>>>();
            _latestResult.UpdateResult(new List<CalibrationValidationPoint>(), -1, -1, -1, -1, -1, -1);
            State = ValidationState.NotCollectingData;
            _eyeTracker.GazeDataReceived += OnGazeDataReceived;
        }

        /// <summary>
        /// Leaves the calibration validation mode, clears all collected data, and unsubscribes from the eye tracker.
        /// </summary>
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

        /// <summary>
        /// Uses the collected data and tries to compute accuracy and precision values for all points.
        /// If the calculation is successful, the result is returned, and stored in the Result property
        /// of the CalibrationValidation object. If there is insufficient data to compute the results
        /// for a certain point that CalibrationValidationPoint will contain invalid data (NaN) for the
        /// results. Gaze data will still be untouched. If there is no valid data for any point, the
        /// average results of CalibrationValidationResult will be invalid (NaN) as well.
        /// </summary>
        /// <returns>The latest <see cref="CalibrationValidationResult"/></returns>
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
                    points.Add(new CalibrationValidationPoint(targetPoint2D, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, true, samples.ToArray()));
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
                var precisionRMSLeftEye = samples.PrecisionRMS(s => s.LeftEye);
                var precisionRMSRightEye = samples.PrecisionRMS(s => s.RightEye);

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
                _latestResult.UpdateResult(points, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
            }
            else
            {
                var validPoints = points.Where(p => !p.TimedOut);

                if (validPoints.Count() == 0)
                {
                    _latestResult.UpdateResult(points, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
                }
                else
                {
                    var averageAccuracyLeftEye = validPoints.Select(p => p.AccuracyLeftEye).Average();
                    var averageAccuracyRightEye = validPoints.Select(p => p.AccuracyRightEye).Average();
                    var averagePrecisionLeftEye = validPoints.Select(p => p.PrecisionLeftEye).Average();
                    var averagePrecisionRightEye = validPoints.Select(p => p.PrecisionRightEye).Average();
                    var averagePrecisionRMSLeftEye = validPoints.Select(p => p.PrecisionRMSLeftEye).Average();
                    var averagePrecisionRMSRightEye = validPoints.Select(p => p.PrecisionRMSRightEye).Average();

                    _latestResult.UpdateResult(
                        points,
                        averageAccuracyLeftEye,
                        averagePrecisionLeftEye,
                        averagePrecisionRMSLeftEye,
                        averageAccuracyRightEye,
                        averagePrecisionRightEye,
                        averagePrecisionRMSRightEye);
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

        /// <summary>
        /// Dispose will unsubscribe to gaze data and exit validation mode, if the object is not already in <see cref="ValidationState.NotInValidationMode"/>
        /// </summary>
        public void Dispose()
        {
            if (State != ValidationState.NotInValidationMode)
            {
                LeaveValidationMode();
            }
        }
    }
}