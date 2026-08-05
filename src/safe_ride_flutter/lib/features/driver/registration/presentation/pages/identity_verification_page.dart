import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/storage/secure_storage_service.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/datasources/identity_verification_remote_datasource.dart';
import '../../data/models/identity_document_model.dart';
import '../../data/models/identity_verification_submission.dart';
import 'identity_document_detail_page.dart';
import 'upload_cccd_page.dart';
import 'license_upload_page.dart';
import 'criminal_record_upload_page.dart';

class IdentityVerificationPage extends StatefulWidget {
  IdentityVerificationPage({super.key});

  @override
  State<IdentityVerificationPage> createState() =>
      _IdentityVerificationPageState();
}

class _IdentityVerificationPageState extends State<IdentityVerificationPage> {
  late List<IdentityDocumentModel> _documents;
  bool _isLoading = true;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    _documents = _defaultDocuments();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadDocuments();
    });
  }

  Future<void> _loadDocuments() async {
    final providerToken = context.read<AuthProvider>().token;
    final token =
        providerToken ?? await getIt<SecureStorageService>().readAccessToken();
    if (!mounted) return;

    if (token == null || token.isEmpty) {
      setState(() {
        _isLoading = false;
        _loadError = context.l10n.sessionExpired;
      });
      return;
    }

    setState(() {
      _isLoading = true;
      _loadError = null;
    });

    try {
      final remoteDocuments =
          await getIt<IdentityVerificationRemoteDatasource>().getDocuments(
        token,
      );
      if (!mounted) return;
      setState(() {
        _documents = _mergeDocuments(remoteDocuments);
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _loadError = context.l10n.profileStatusLoadFailed;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    // Check if there is any rejected document
    final rejectedDoc = _documents.cast<IdentityDocumentModel?>().firstWhere(
          (doc) => doc?.status == DocumentStatus.rejected,
          orElse: () => null,
        );

    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: Icon(Icons.arrow_back, color: Color(0xFF263238)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Text(
          context.l10n.identityVerification,
          style: TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
        actions: [
          IconButton(
            icon: Icon(Icons.help_outline, color: AppColors.primary),
            onPressed: () {},
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadDocuments,
        color: AppColors.primary,
        child: SingleChildScrollView(
          physics: AlwaysScrollableScrollPhysics(
            parent: BouncingScrollPhysics(),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 20),
        child: Column(
          children: [
            SizedBox(height: 32),
            // Header Icon
            Container(
              padding: const EdgeInsets.all(30),
              decoration: BoxDecoration(
                color: Color(0xFFF5F5F5),
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.shield_outlined,
                size: 60,
                color: AppColors.primary,
              ),
            ),
            SizedBox(height: 24),
            Text(
              context.l10n.completeYourProfile,
              style: TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.w800,
                color: Color(0xFF1F1F1F),
                letterSpacing: -0.5,
              ),
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 12),
            Text(
              context.l10n.identityVerificationIntro,
              style: TextStyle(
                fontSize: 15,
                color: Color(0xFF78909C),
                height: 1.5,
                fontWeight: FontWeight.w500,
              ),
              textAlign: TextAlign.center,
            ),

            // Rejection Alert Box
            if (rejectedDoc != null) ...[
              SizedBox(height: 32),
              _buildRejectionAlert(rejectedDoc.rejectionReason ?? ''),
            ],

            SizedBox(height: 32),
            if (_isLoading) ...[
              LinearProgressIndicator(
                color: AppColors.primary,
                minHeight: 3,
              ),
              SizedBox(height: 16),
            ],
            if (_loadError != null) ...[
              _buildLoadError(_loadError!),
              SizedBox(height: 16),
            ],
            Align(
              alignment: Alignment.centerLeft,
              child: Text(
                context.l10n.requiredDocuments,
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF1F1F1F),
                ),
              ),
            ),
            SizedBox(height: 16),

            // Document List
            ..._documents.map((doc) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _DocumentItem(
                    document: doc,
                    onTap: () {
                      if (doc.status == DocumentStatus.pending ||
                          doc.status == DocumentStatus.verified) {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => IdentityDocumentDetailPage(
                              document: doc,
                            ),
                          ),
                        );
                        return;
                      }

                      if (doc.documentType == 'ID_CARD') {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => UploadCccdPage(
                              submission: IdentityVerificationSubmission(),
                            ),
                          ),
                        );
                      } else if (doc.documentType == 'DRIVING_LICENSE') {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => LicenseUploadPage(
                              submission: IdentityVerificationSubmission(),
                            ),
                          ),
                        );
                      } else if (doc.documentType == 'CRIMINAL_RECORD') {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => CriminalRecordUploadPage(
                              submission: IdentityVerificationSubmission(),
                            ),
                          ),
                        );
                      }
                    },
                  ),
                )),

            SizedBox(height: 32),
          ],
        ),
      ),
      ),
      bottomNavigationBar: Container(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.05),
              blurRadius: 10,
              offset: Offset(0, -5),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CustomButton(
              text: context.l10n.submitApplicationNow,
              onPressed: () {
                // Navigate to the first step (CCCD Upload)
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => UploadCccdPage(
                      submission: IdentityVerificationSubmission(),
                    ),
                  ),
                );
              },
            ),
            SizedBox(height: 12),
            Text(
              context.l10n.verificationTime,
              style: TextStyle(
                fontSize: 13,
                color: Color(0xFF90A4AE),
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildRejectionAlert(String reason) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Color(0xFFFFEBEE),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Color(0xFFFFCDD2)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.error_outline, color: Color(0xFFD32F2F), size: 24),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.l10n.previousApplicationRejected,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFFB71C1C),
                  ),
                ),
                SizedBox(height: 4),
                Text(
                  reason,
                  style: TextStyle(
                    fontSize: 14,
                    color: Color(0xFFD32F2F),
                    height: 1.4,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLoadError(String message) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.08),
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        children: [
          Icon(Icons.cloud_off_rounded, size: 64, color: Colors.grey),
          SizedBox(height: 16),
          Text(
            context.l10n.serverConnectionErrorTitle,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1F1F1F),
            ),
          ),
          SizedBox(height: 8),
          Text(
            message,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 14,
              color: Color(0xFF78909C),
              height: 1.4,
            ),
          ),
          SizedBox(height: 24),
          ElevatedButton.icon(
            onPressed: _loadDocuments,
            icon: Icon(Icons.refresh_rounded),
            label: Text(
              context.l10n.tryAgain,
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 12),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
              elevation: 0,
            ),
          ),
        ],
      ),
    );
  }

  List<IdentityDocumentModel> _defaultDocuments() {
    final l10n = LocaleProvider.currentLocalizations;
    return [
      IdentityDocumentModel(
        documentType: 'ID_CARD',
        title: l10n.idCardOrPassport,
        description: l10n.frontAndBack,
        icon: Icons.badge_outlined,
      ),
      IdentityDocumentModel(
        documentType: 'DRIVING_LICENSE',
        title: l10n.drivingLicense,
        description: l10n.licensePhotoAndInfo,
        icon: Icons.directions_car_outlined,
      ),
      IdentityDocumentModel(
        documentType: 'CRIMINAL_RECORD',
        title: l10n.criminalRecord,
        description: l10n.originalIssuedWithinSixMonths,
        icon: Icons.security_outlined,
      ),
    ];
  }

  List<IdentityDocumentModel> _mergeDocuments(
    List<Map<String, dynamic>> remoteDocuments,
  ) {
    final byType = <String, Map<String, dynamic>>{};
    for (final document in remoteDocuments) {
      final type = document[ApiKeys.documentType]?.toString();
      if (type != null && type.isNotEmpty) {
        byType[type] = document;
      }
    }

    return _defaultDocuments().map((document) {
      final remote = byType[document.documentType];
      if (remote == null) {
        return document;
      }

      final status = IdentityDocumentModel.statusFromBackend(
        remote[ApiKeys.kycStatus]?.toString(),
      );
      return document.copyWith(
        status: status,
        description: _descriptionForStatus(document, status),
        rejectionReason: remote[ApiKeys.rejectionReason]?.toString(),
        documentNumber: remote[ApiKeys.documentNumber]?.toString(),
        licenseClass: remote[ApiKeys.licenseClass]?.toString(),
        frontImageUrl: remote[ApiKeys.frontImageUrl]?.toString(),
        backImageUrl: remote[ApiKeys.backImageUrl]?.toString(),
        fileUrl: remote[ApiKeys.fileUrl]?.toString(),
        issueDate: remote[ApiKeys.issueDate]?.toString(),
        expiryDate: remote[ApiKeys.expiryDate]?.toString(),
      );
    }).toList();
  }

  String _descriptionForStatus(
    IdentityDocumentModel document,
    DocumentStatus status,
  ) {
    final l10n = LocaleProvider.currentLocalizations;
    return switch (status) {
      DocumentStatus.pending => l10n.documentPendingReview,
      DocumentStatus.verified => l10n.documentApproved,
      DocumentStatus.rejected => l10n.resubmissionRequired,
      DocumentStatus.notSubmitted => document.description,
    };
  }
}

class _DocumentItem extends StatelessWidget {
  final IdentityDocumentModel document;
  final VoidCallback onTap;

  _DocumentItem({required this.document, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final Color statusColor = _statusColor(document.status);
    final Color statusBackgroundColor = _statusBackgroundColor(document.status);
    final Color statusBorderColor = _statusBorderColor(document.status);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: statusBackgroundColor,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: statusBorderColor,
            width: 1.5,
          ),
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: statusColor.withOpacity(0.12),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(
                document.icon,
                color: statusColor,
                size: 24,
              ),
            ),
            SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    document.title,
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF263238),
                    ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    document.description,
                    style: TextStyle(
                      fontSize: 13,
                      color: statusColor,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
            _buildStatusBadge(context, document.status),
          ],
        ),
      ),
    );
  }

  Color _statusColor(DocumentStatus status) {
    switch (status) {
      case DocumentStatus.notSubmitted:
        return Color(0xFF757575);
      case DocumentStatus.pending:
        return Color(0xFFFFA000);
      case DocumentStatus.verified:
        return Color(0xFF2E7D32);
      case DocumentStatus.rejected:
        return Color(0xFFD32F2F);
    }
  }

  Color _statusBackgroundColor(DocumentStatus status) {
    switch (status) {
      case DocumentStatus.notSubmitted:
        return Color(0xFFFAFAFA);
      case DocumentStatus.pending:
        return Color(0xFFFFFDF5);
      case DocumentStatus.verified:
        return Color(0xFFF1F8E9);
      case DocumentStatus.rejected:
        return Color(0xFFFFFBFA);
    }
  }

  Color _statusBorderColor(DocumentStatus status) {
    switch (status) {
      case DocumentStatus.notSubmitted:
        return Color(0xFFE0E0E0);
      case DocumentStatus.pending:
        return Color(0xFFFFE082);
      case DocumentStatus.verified:
        return Color(0xFFC8E6C9);
      case DocumentStatus.rejected:
        return Color(0xFFFFCDD2);
    }
  }

  Widget _buildStatusBadge(BuildContext context, DocumentStatus status) {
    switch (status) {
      case DocumentStatus.notSubmitted:
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Color(0xFFEEEEEE),
            borderRadius: BorderRadius.circular(100),
          ),
          child: Text(
            context.l10n.documentNotSubmitted.toUpperCase(),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Color(0xFF757575),
            ),
          ),
        );
      case DocumentStatus.rejected:
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Color(0xFFB71C1C),
            borderRadius: BorderRadius.circular(100),
          ),
          child: Text(
            context.l10n.documentRejected.toUpperCase(),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Colors.white,
            ),
          ),
        );
      case DocumentStatus.pending:
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Color(0xFFFFF8E1),
            borderRadius: BorderRadius.circular(100),
          ),
          child: Text(
            context.l10n.submitted.toUpperCase(),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Color(0xFFFFA000),
            ),
          ),
        );
      case DocumentStatus.verified:
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Color(0xFFE8F5E9),
            borderRadius: BorderRadius.circular(100),
          ),
          child: Text(
            context.l10n.documentApproved.toUpperCase(),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Color(0xFF2E7D32),
            ),
          ),
        );
    }
  }
}
