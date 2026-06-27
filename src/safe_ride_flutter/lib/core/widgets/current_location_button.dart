import 'package:flutter/material.dart';

import '../constants/app_colors.dart';

class CurrentLocationButton extends StatelessWidget {
  final VoidCallback? onPressed;
  final bool isLoading;
  final Color? iconColor;
  final String heroTag;

  const CurrentLocationButton({
    super.key,
    required this.onPressed,
    this.isLoading = false,
    this.iconColor,
    this.heroTag = 'current-location',
  });

  @override
  Widget build(BuildContext context) {
    final color = iconColor ?? AppColors.primary;
    return FloatingActionButton.small(
      heroTag: heroTag,
      onPressed: isLoading ? null : onPressed,
      backgroundColor: Colors.white,
      foregroundColor: color,
      child: isLoading
          ? SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2.0,
                color: color,
              ),
            )
          : const Icon(Icons.my_location),
    );
  }
}
