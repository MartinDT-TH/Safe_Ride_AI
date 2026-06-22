using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IMapGeocodingService
{
    Task<IReadOnlyList<MapSuggestionDto>> AutocompleteAsync(MapAutocompleteRequest request, CancellationToken cancellationToken = default);
    Task<MapPlaceDto?> GetPlaceDetailAsync(string providerPlaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapPlaceDto>> GeocodeAsync(MapGeocodeRequest request, CancellationToken cancellationToken = default);
    Task<MapPlaceDto?> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default);
}
