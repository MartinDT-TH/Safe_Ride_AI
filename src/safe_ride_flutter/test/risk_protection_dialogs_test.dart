import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/widgets/risk_protection_dialogs.dart';
import 'package:safe_ride/l10n/generated/app_localizations.dart';

void main() {
  test('vehicle fault values match the backend enum contract', () {
    expect(vehicleFaultTypes, const [
      'BRAKE_FAILURE',
      'LIGHT_FAILURE',
      'TIRE_FAILURE',
      'STEERING_FAILURE',
      'ENGINE_FAILURE',
      'ELECTRICAL_FAILURE',
      'OTHER',
    ]);
  });

  test(
    'PNG evidence keeps its content type when picker omits MIME metadata',
    () {
      expect(imageContentTypeForEvidence(XFile('evidence.png')), 'image/png');
    },
  );

  testWidgets('accident dialog keeps its input alive until the route is gone', (
    tester,
  ) async {
    String? result;

    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: Builder(
            builder: (context) => FilledButton(
              onPressed: () async {
                result = await showAccidentReportDialog(context);
              },
              child: const Text('open'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Va cham nhe');
    await tester.pump();
    await tester.tap(find.byType(FilledButton).last);
    await tester.pumpAndSettle();

    expect(result, 'Va cham nhe');
    expect(tester.takeException(), isNull);
  });
}
