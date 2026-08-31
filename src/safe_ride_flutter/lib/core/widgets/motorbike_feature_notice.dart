import 'package:flutter/material.dart';

import '../localization/localization_extensions.dart';

class MotorbikeFeatureNotice {
  static Future<void> show(BuildContext context) {
    return showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        icon: const Icon(
          Icons.two_wheeler_rounded,
          color: Color(0xFF006B70),
          size: 32,
        ),
        title: Text(
          context.l10n.motorbikeFeatureSuspendedTitle,
          textAlign: TextAlign.center,
        ),
        content: Text(
          context.l10n.motorbikeFeatureSuspendedMessage,
          textAlign: TextAlign.center,
        ),
        actionsAlignment: MainAxisAlignment.center,
        actions: [
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext),
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFF006B70),
            ),
            child: Text(context.l10n.acknowledge),
          ),
        ],
      ),
    );
  }
}
