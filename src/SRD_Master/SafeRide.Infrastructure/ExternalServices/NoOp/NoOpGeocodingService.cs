using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.ExternalServices.NoOp;

internal sealed class NoOpGeocodingService : IMapGeocodingService
{
    public Task<IReadOnlyList<MapSuggestionDto>> AutocompleteAsync(MapAutocompleteRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MapSuggestionDto>>(Array.Empty<MapSuggestionDto>());
    }

    public Task<MapPlaceDto?> GetPlaceDetailAsync(string providerPlaceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<MapPlaceDto?>(null);
    }

    public Task<IReadOnlyList<MapPlaceDto>> GeocodeAsync(MapGeocodeRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MapPlaceDto>>(Array.Empty<MapPlaceDto>());
    }

    public Task<MapPlaceDto?> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<MapPlaceDto?>(null);
    }
}
