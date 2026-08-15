import 'package:flutter/material.dart';

import '../../../../../core/localization/localization_extensions.dart';
import 'profile_menu_tile.dart';

class DriverReviewsProfileMenuTile extends StatelessWidget {
  const DriverReviewsProfileMenuTile({
    super.key,
    required this.isDriver,
    required this.onTap,
  });

  static const tileKey = Key('driver_reviews_profile_menu_tile');

  final bool isDriver;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    if (!isDriver) {
      return const SizedBox.shrink();
    }

    return ProfileMenuTile(
      key: tileKey,
      icon: Icons.star_outline_rounded,
      title: context.l10n.viewReviews,
      onTap: onTap,
    );
  }
}
