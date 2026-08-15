import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/shared/profile/presentation/widgets/driver_reviews_profile_menu_tile.dart';
import 'package:safe_ride/l10n/generated/app_localizations.dart';

void main() {
  Widget buildSubject({required bool isDriver, VoidCallback? onTap}) {
    return MaterialApp(
      locale: const Locale('vi'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: DriverReviewsProfileMenuTile(
          isDriver: isDriver,
          onTap: onTap ?? () {},
        ),
      ),
    );
  }

  testWidgets('shows the reviews entry in driver mode', (tester) async {
    await tester.pumpWidget(buildSubject(isDriver: true));
    await tester.pumpAndSettle();

    expect(find.byKey(DriverReviewsProfileMenuTile.tileKey), findsOneWidget);
    expect(find.text('Xem đánh giá'), findsOneWidget);
  });

  testWidgets('hides the reviews entry outside driver mode', (tester) async {
    await tester.pumpWidget(buildSubject(isDriver: false));
    await tester.pumpAndSettle();

    expect(find.byKey(DriverReviewsProfileMenuTile.tileKey), findsNothing);
    expect(find.text('Xem đánh giá'), findsNothing);
  });

  testWidgets('invokes navigation callback when tapped', (tester) async {
    var tapped = false;
    await tester.pumpWidget(
      buildSubject(isDriver: true, onTap: () => tapped = true),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Xem đánh giá'));

    expect(tapped, isTrue);
  });
}
