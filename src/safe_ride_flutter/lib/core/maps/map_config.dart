enum MapRenderProvider {
  googleMaps,
  vietMap,
}

class MapConfig {
  /// The active map render provider for the entire application.
  /// Hardcoded based on user request.
  static const MapRenderProvider activeMapProvider = MapRenderProvider.vietMap;
}
