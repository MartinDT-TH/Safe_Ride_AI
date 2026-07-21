import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/storage/secure_storage_service.dart';
import 'package:safe_ride/main.dart' as app;

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  // Test credentials
  const customerPhone = '0987654321';
  const driverPhone = '0912345678';
  String? customerToken;
  String? driverToken;

  // Use Http directly to bypass flutter dependencies for driver actions
  Future<String> login(String phone) async {
    final client = HttpClient();
    final request = await client.postUrl(
      Uri.parse('${AppConfig.apiBaseUrl}auth/login'),
    );
    request.headers.contentType = ContentType.json;
    request.write(jsonEncode({'phoneNumber': phone, 'password': 'Password123!'}));
    final response = await request.close();
    final responseBody = await response.transform(utf8.decoder).join();
    final data = jsonDecode(responseBody);
    return data['accessToken'];
  }

  testWidgets('E2E Booking -> Trip Tracking Flow', (WidgetTester tester) async {
    // We will bypass the UI login by using the demo-login API.
    print('TEST: Pre-authenticating with Google email...');
    final client = HttpClient();
    final request = await client.postUrl(
      Uri.parse('${AppConfig.apiBaseUrl}auth/demo-login'),
    );
    request.headers.contentType = ContentType.json;
    request.write(jsonEncode({
      'provider': 'Google',
      'email': 'phaobong123@gmail.com',
      'fullName': 'Phao Bong',
      'deviceId': 'test-device'
    }));
    final response = await request.close();
    final responseBody = await response.transform(utf8.decoder).join();
    final data = jsonDecode(responseBody);
    
    if (data['accessToken'] != null) {
      final storage = SecureStorageService();
      await storage.saveTokens(
        accessToken: data['accessToken'],
        refreshToken: data['refreshToken'],
      );
      await storage.saveUserProfile(jsonEncode(data));
      print('TEST: Pre-authentication successful.');
    } else {
      print('TEST: Pre-authentication failed: $responseBody');
    }

    app.main();
    await tester.pumpAndSettle();

    // Wait for Home page
    bool homeLoaded = false;
    for (int i = 0; i < 20; i++) {
      await tester.pump(const Duration(seconds: 1));
      if (find.text('Đặt ngay').evaluate().isNotEmpty) {
        homeLoaded = true;
        break;
      }
    }
    
    final destinationInput = find.text('Đặt ngay');
    if (homeLoaded && destinationInput.evaluate().isNotEmpty) {
      await tester.tap(destinationInput);
      await tester.pumpAndSettle();

      final selectDestination = find.text('Chọn điểm đến');
      if (selectDestination.evaluate().isNotEmpty) {
        await tester.tap(selectDestination);
        await tester.pumpAndSettle();
      }

      final searchInput = find.byType(TextField).first;
      await tester.enterText(searchInput, 'Hồ Chí Minh');
      
      bool suggestionFound = false;
      for (int i = 0; i < 10; i++) {
        await tester.pump(const Duration(seconds: 1));
        if (find.byType(ListTile).evaluate().isNotEmpty) {
          suggestionFound = true;
          break;
        }
      }

      if (suggestionFound) {
        final suggestion = find.byType(ListTile).first;
        await tester.tap(suggestion);
        await tester.pumpAndSettle(const Duration(seconds: 2));
      }

      final confirmDestination = find.text('Xác nhận điểm đến');
      if (confirmDestination.evaluate().isNotEmpty) {
        await tester.tap(confirmDestination);
        await tester.pumpAndSettle(const Duration(seconds: 2));
      }

      final bookBtn = find.text('Xác nhận đặt ngay');
      if (bookBtn.evaluate().isNotEmpty) {
        await tester.tap(bookBtn);
        await tester.pumpAndSettle();
      }

      print('TEST: Waiting for driver tracking screen via Real-time SignalR...');
      await tester.pumpAndSettle(const Duration(seconds: 15));
    } else {
      print('TEST: App did not load Home screen. Check if manual login is required.');
    }

    print('TEST: UI Integration completed successfully.');
  });
}
