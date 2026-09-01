import 'dart:convert';

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

  test('safety report reason codes match each backend enum contract', () {
    expect(safetyReportReasonCodesForType('UNSAFE_CUSTOMER'), const [
      'DISTRACTING',
      'VIOLENT',
      'INTERFERING_WITH_VEHICLE',
      'UNSAFE_REQUEST',
      'OTHER',
    ]);
    expect(safetyReportReasonCodesForType('VEHICLE_ISSUE'), vehicleFaultTypes);
  });

  testWidgets('safety report reason dropdown fits a narrow trip screen', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(320, 640));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: Builder(
            builder: (context) => FilledButton(
              onPressed: () => showSafetyReportDialog(context),
              child: const Text('open safety report'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('open safety report'));
    await tester.pumpAndSettle();
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
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

  testWidgets('pre-trip keeps camera primary and gallery available', (
    tester,
  ) async {
    final requestedSources = <ImageSource>[];
    final evidence = XFile.fromData(
      base64Decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
      ),
      name: 'pretrip-camera.png',
      mimeType: 'image/png',
    );

    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: Builder(
            builder: (context) => FilledButton(
              onPressed: () => showPreTripSafetyCheckDialog(
                context,
                pickEvidenceImage: (source) async {
                  requestedSources.add(source);
                  return evidence;
                },
              ),
              child: const Text('open pre-trip'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('open pre-trip'));
    await tester.pumpAndSettle();
    final captureButton = find.text('Chụp ảnh', skipOffstage: false);
    await tester.dragUntilVisible(
      captureButton,
      find.byType(SingleChildScrollView),
      const Offset(0, -250),
    );
    await tester.tap(captureButton);
    await tester.pumpAndSettle();

    expect(requestedSources, [ImageSource.camera]);
    expect(find.byType(Image, skipOffstage: false), findsOneWidget);
    expect(find.text('Chụp lại', skipOffstage: false), findsOneWidget);

    final galleryButton = find.widgetWithText(
      OutlinedButton,
      'Chọn từ thư viện',
    );
    await tester.ensureVisible(galleryButton);
    await tester.tap(galleryButton);
    await tester.pumpAndSettle();
    expect(requestedSources, [ImageSource.camera, ImageSource.gallery]);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'safety termination uses camera and camera cancellation keeps the reason',
    (tester) async {
      SafetyTerminationDialogResult? result;
      ImageSource? requestedSource;

      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('vi'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: Scaffold(
            body: Builder(
              builder: (context) => FilledButton(
                onPressed: () async {
                  result = await showSafetyTerminationDialog(
                    context,
                    pickEvidenceImage: (source) async {
                      requestedSource = source;
                      return null;
                    },
                  );
                },
                child: const Text('open safety termination'),
              ),
            ),
          ),
        ),
      );

      await tester.tap(find.text('open safety termination'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byType(TextField),
        'Khách có hành vi nguy hiểm',
      );
      await tester.tap(find.text('Chụp ảnh bằng chứng (tùy chọn)'));
      await tester.pumpAndSettle();

      expect(requestedSource, ImageSource.camera);
      expect(find.text('Khách có hành vi nguy hiểm'), findsOneWidget);

      await tester.tap(
        find.widgetWithText(FilledButton, 'Kết thúc vì an toàn'),
      );
      await tester.pumpAndSettle();

      expect(result?.reason, 'Khách có hành vi nguy hiểm');
      expect(result?.evidence, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('captured safety evidence is shown and can be submitted', (
    tester,
  ) async {
    SafetyTerminationDialogResult? result;
    final evidence = XFile.fromData(
      base64Decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
      ),
      name: 'camera.png',
      mimeType: 'image/png',
    );

    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: Builder(
            builder: (context) => FilledButton(
              onPressed: () async {
                result = await showSafetyTerminationDialog(
                  context,
                  pickEvidenceImage: (_) async => evidence,
                );
              },
              child: const Text('open capture'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('open capture'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Sự cố an toàn');
    await tester.tap(find.text('Chụp ảnh bằng chứng (tùy chọn)'));
    await tester.pumpAndSettle();

    expect(find.text('1 / 3 ảnh', skipOffstage: false), findsOneWidget);
    expect(find.text('Chụp ảnh', skipOffstage: false), findsOneWidget);

    final addPhotoButton = find.text('Chụp ảnh', skipOffstage: false);
    await tester.ensureVisible(addPhotoButton);
    await tester.pumpAndSettle();
    await tester.tap(addPhotoButton);
    await tester.pumpAndSettle();

    expect(find.text('2 / 3 ảnh', skipOffstage: false), findsOneWidget);

    final submitButton = find.widgetWithText(
      FilledButton,
      'Kết thúc vì an toàn',
    );
    await tester.ensureVisible(submitButton);
    await tester.pumpAndSettle();
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(result?.evidence, hasLength(2));
    expect(result?.evidence, everyElement(same(evidence)));
    expect(tester.takeException(), isNull);
  });
}
