using Windows.Devices.Geolocation;
using System.Diagnostics.CodeAnalysis;

namespace Glass.Platform.Windows.Location;

public enum LocationAccessResult { Granted, Denied, Unavailable }

public sealed record OneShotLocationResult(
    LocationAccessResult Access,
    double Latitude,
    double Longitude);

public sealed class OneShotLocationService
{
    [SuppressMessage("Performance", "CA1822", Justification =
        "The instance service is injected as the explicit user-initiated location boundary.")]
    public async ValueTask<OneShotLocationResult> RequestAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync().AsTask(cancellationToken);
            if (access != GeolocationAccessStatus.Allowed)
                return new OneShotLocationResult(LocationAccessResult.Denied, 0, 0);
            var position = await new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
            }.GetGeopositionAsync().AsTask(cancellationToken);
            return new OneShotLocationResult(
                LocationAccessResult.Granted,
                position.Coordinate.Point.Position.Latitude,
                position.Coordinate.Point.Position.Longitude);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            TimeoutException or NotImplementedException)
        {
            return new OneShotLocationResult(LocationAccessResult.Unavailable, 0, 0);
        }
    }
}
