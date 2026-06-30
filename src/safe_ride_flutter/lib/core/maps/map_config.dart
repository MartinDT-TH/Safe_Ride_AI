import '../services/mobile_config_service.dart';
import '../../dependency_injection/injection.dart';

enum MapRenderProvider { googleMaps, vietMap }

class MapConfig {
  static MapRenderProvider get activeMapProvider {
    final features = getIt<MobileConfigService>().config.features;
    final configuredProvider = features.mapProvider.toLowerCase();
    if (configuredProvider == 'vietmap' && features.enableVietMap) {
      return MapRenderProvider.vietMap;
    }
    if (configuredProvider == 'googlemaps' && features.enableGoogleMap) {
      return MapRenderProvider.googleMaps;
    }
    if (features.enableGoogleMap) {
      return MapRenderProvider.googleMaps;
    }
    return MapRenderProvider.vietMap;
  }

  static bool get enableGoogleMap =>
      getIt<MobileConfigService>().config.features.enableGoogleMap;

  static bool get enableVietMap =>
      getIt<MobileConfigService>().config.features.enableVietMap;
}
