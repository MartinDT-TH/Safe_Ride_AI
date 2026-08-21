import 'package:flutter/foundation.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../../core/session/session_manager.dart';
import '../../data/datasources/risk_protection_remote_datasource.dart';
import '../../data/models/risk_protection_models.dart';

class RiskProtectionProvider extends ChangeNotifier {
  RiskProtectionProvider(this._datasource, this._sessionManager);

  final RiskProtectionRemoteDatasource _datasource;
  final SessionManager _sessionManager;

  RiskProtectionAccident? accident;
  List<DriverLiabilityItem> liabilities = const [];
  bool isLoading = false;
  bool isMutating = false;
  String? errorMessage;

  Future<void> loadAccident(int accidentId) async {
    await _runLoading(() async {
      accident = await _datasource.getAccident(await _token(), accidentId);
    });
  }

  Future<void> loadDriverLiabilities() async {
    await _runLoading(() async {
      liabilities = await _datasource.getDriverLiabilities(await _token());
    });
  }

  Future<bool> uploadEvidence({
    required int accidentId,
    required XFile file,
    String? description,
  }) => _runMutation(() async {
    await _datasource.uploadEvidence(
      await _token(),
      accidentId,
      file: file,
      evidenceType: 'PHOTO',
      description: description,
    );
    accident = await _datasource.getAccident(await _token(), accidentId);
  });

  Future<bool> disputeLiability(
    int accidentId,
    String reason,
    List<int> evidenceIds,
  ) =>
      _runMutation(() async {
        await _datasource.disputeLiability(
          await _token(),
          accidentId,
          reason,
          evidenceIds,
        );
        accident = await _datasource.getAccident(await _token(), accidentId);
      });

  Future<void> _runLoading(Future<void> Function() action) async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      await action();
    } catch (error) {
      errorMessage = error.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> _runMutation(Future<void> Function() action) async {
    if (isMutating) return false;
    isMutating = true;
    errorMessage = null;
    notifyListeners();
    try {
      await action();
      return true;
    } catch (error) {
      errorMessage = error.toString();
      return false;
    } finally {
      isMutating = false;
      notifyListeners();
    }
  }

  Future<String> _token() async {
    final token = await _sessionManager.getValidAccessToken();
    if (token == null || token.isEmpty) {
      throw const RiskProtectionException(
        'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
      );
    }
    return token;
  }
}
