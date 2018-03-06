# prosdk-addons-dotnet - Calibration Validation

## What is it
Addons for the Tobii Pro SDK.

[![Tobii Pro SDK](https://www.tobiipro.com/imagevault/publishedmedia/6rkt3jb83qlottsfh1ts/Tobii-Pro-SDK-with-VR-3_1-banner.jpg)](https://www.tobiipro.com/product-listing/tobii-pro-sdk/)

[The Tobii Pro SDK can be found here.](https://www.tobiipro.com/product-listing/tobii-pro-sdk/ "Tobii Pro SDK")<br/>
[The Tobii Pro SDK .Net documentation can be found here.](http://developer.tobiipro.com/dotnet.html "Tobii Pro SDK .Net documentation")

As of this writing the addon contains functionality for calibration validation.
Do not hesitate to contribute to this project and create issues if you find something that might be wrong or could be improved.
### Get the addon
Either [download the addon](https://github.com/tobiipro/prosdk-addons-dotnet/archive/master.zip "Download the addon") and unzip it, or clone the repository:
```sh
git clone https://github.com/tobiipro/prosdk-addons-dotnet.git
```
### Building
* Open the Tobii.Research.Addons.sln in Visual Studio. The solution is located in the source folder.
* Add the Tobii Pro SDK to the project via NuGet. For this example we use the 32 bit version **Tobii.Research.x86**.
* Build the addons assembly. In this example we build the debug version. It ends up in `\source\Tobii.Research.Addons\bin\Debug\Tobii.Research.Addons.dll`.
### Usage example
The following is an example test application that makes use of the calibration validation implementation in the addons library. **Note:** this example does not display any points to perform a proper calibration validation, it only shows how to use the addons library.
* In Visual Studio, create a `Console App (.Net Framework)` for Visual C#.
* Add the Tobii Pro SDK to the project via NuGet. For this example we use the 32 bit version **Tobii.Research.x86**.
* Add a reference to the **Tobii.Research.Addons.dll**, for example from the ***Building*** section above.
* Add the necessary `using` statements to be able to use the Tobii Pro SDK and the addons library:
```csharp
using Tobii.Research;
using Tobii.Research.Addons;
```
* Get a reference to your eye tracker. Here we assume that we have only one connected and get the first one found. Print the address to the console to verify that we got hold of it:
```csharp
var eyeTracker = EyeTrackingOperations.FindAllEyeTrackers().FirstOrDefault();
Console.WriteLine("Found eye tracker {0}", eyeTracker.Address);
```
* Create a `ScreenBasedCalibrationValidation` object and provide the eye tracker reference as an argument:
```csharp
var calibrationValidation = new ScreenBasedCalibrationValidation(eyeTracker);
```
* The constructor also accepts arguments for the number of samples to collect for each point, default 30, and the timeout in milliseconds, default 1000.
* Create the points to be used for validation:
```csharp
var points = new NormalizedPoint2D[] {
    new NormalizedPoint2D(0.1f, 0.1f),
    new NormalizedPoint2D(0.1f, 0.9f),
    new NormalizedPoint2D(0.5f, 0.5f),
    new NormalizedPoint2D(0.9f, 0.1f),
    new NormalizedPoint2D(0.9f, 0.9f)
};
```
* Enter the validation mode. This makes the calibration validation object start listening to gaze data from the eye tracker:
```csharp
calibrationValidation.EnterValidationMode();
```
* Loop through the points and provide them to the validation object. For each point, wait for the data collection to finish. In a real application here is where the points would be displayed on the screen:
```csharp
foreach (var point in points)
{
    Console.WriteLine("Collecting for point {0}, {1}", point.X, point.Y);

    calibrationValidation.StartCollectingData(point);
    while (calibrationValidation.State == ScreenBasedCalibrationValidation.ValidationState.CollectingData)
    {
        System.Threading.Thread.Sleep(25);
    }
}
```
* Compute the result:
```csharp
var result = calibrationValidation.Compute();
```
* The result object contains the calculated average accuracy and precision and the list of points where each point contains the accuracy and precision for that point plus the data collected for that point. The results are in degrees. In this example we simply print the results:
```csharp
Console.WriteLine(calibrationValidation);
```
* When satisfied with the results, call `LeaveValidationMode`. This clears all collected data and makes the calibration object stop listening to the eye tracker.
```csharp
calibrationValidation.LeaveValidationMode();
```
* All implementation details for the **ScreenBasedCalibrationValidation** class can be viewed in the [source code](https://github.com/tobiipro/prosdk-addons-dotnet/blob/master/source/Tobii.Research.Addons/ScreenBasedCalibrationValidation.cs).
* Here is the entire example at once:
```csharp
using System;
using System.Linq;
using Tobii.Research;
using Tobii.Research.Addons;

namespace TestAddons
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var eyeTracker = EyeTrackingOperations.FindAllEyeTrackers().FirstOrDefault();
            Console.WriteLine("Found eye tracker {0}", eyeTracker.Address);

            var calibrationValidation = new ScreenBasedCalibrationValidation(eyeTracker);

            var points = new NormalizedPoint2D[] {
                new NormalizedPoint2D(0.1f, 0.1f),
                new NormalizedPoint2D(0.1f, 0.9f),
                new NormalizedPoint2D(0.5f, 0.5f),
                new NormalizedPoint2D(0.9f, 0.1f),
                new NormalizedPoint2D(0.9f, 0.9f)
            };

            calibrationValidation.EnterValidationMode();

            foreach (var point in points)
            {
                Console.WriteLine("Collecting for point {0}, {1}", point.X, point.Y);

                calibrationValidation.StartCollectingData(point);
                while (calibrationValidation.State == ScreenBasedCalibrationValidation.ValidationState.CollectingData)
                {
                    System.Threading.Thread.Sleep(25);
                }
            }

            var result = calibrationValidation.Compute();
            Console.WriteLine(calibrationValidation);
            calibrationValidation.LeaveValidationMode();
        }
    }
}
```
