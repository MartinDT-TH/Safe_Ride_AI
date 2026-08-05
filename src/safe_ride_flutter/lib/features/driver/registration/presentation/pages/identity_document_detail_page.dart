import 'package:flutter/material.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../data/models/identity_document_model.dart';

class IdentityDocumentDetailPage extends StatelessWidget {
  IdentityDocumentDetailPage({super.key, required this.document});

  final IdentityDocumentModel document;

  @override
  Widget build(BuildContext context) {
    final statusColor = _statusColor(document.status);

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
          document.title,
          style: TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        physics: BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _StatusSummary(document: document, statusColor: statusColor),
            SizedBox(height: 24),
            _InfoSection(
              title: context.l10n.submittedInformation,
              rows: [
                if (_hasText(document.documentNumber))
                  _InfoRow(context.l10n.documentNumber, document.documentNumber!),
                if (_hasText(document.licenseClass))
                  _InfoRow(context.l10n.licenseClass, document.licenseClass!),
                if (_hasText(document.issueDate))
                  _InfoRow(context.l10n.issueDate, document.issueDate!),
                if (_hasText(document.expiryDate))
                  _InfoRow(context.l10n.expiryDate, document.expiryDate!),
              ],
            ),
            SizedBox(height: 24),
            Text(
              context.l10n.documents,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w800,
                color: Color(0xFF1F1F1F),
              ),
            ),
            SizedBox(height: 12),
            if (_hasText(document.frontImageUrl))
              _DocumentPreview(
                label: context.l10n.frontSide,
                url: document.frontImageUrl!,
              ),
            if (_hasText(document.backImageUrl)) ...[
              SizedBox(height: 12),
              _DocumentPreview(
                label: context.l10n.backSide,
                url: document.backImageUrl!,
              ),
            ],
            if (_hasText(document.fileUrl)) ...[
              if (_hasText(document.frontImageUrl) ||
                  _hasText(document.backImageUrl))
                SizedBox(height: 12),
              _DocumentPreview(
                label: context.l10n.submittedFile,
                url: document.fileUrl!,
              ),
            ],
          ],
        ),
      ),
    );
  }

  static bool _hasText(String? value) =>
      value != null && value.trim().isNotEmpty;

  static Color _statusColor(DocumentStatus status) {
    return switch (status) {
      DocumentStatus.verified => Color(0xFF2E7D32),
      DocumentStatus.pending => Color(0xFFFFA000),
      DocumentStatus.rejected => Color(0xFFD32F2F),
      DocumentStatus.notSubmitted => Color(0xFF757575),
    };
  }
}

class _StatusSummary extends StatelessWidget {
  _StatusSummary({required this.document, required this.statusColor});

  final IdentityDocumentModel document;
  final Color statusColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: statusColor.withOpacity(0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: statusColor.withOpacity(0.25)),
      ),
      child: Row(
        children: [
          Icon(document.icon, color: statusColor, size: 28),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _statusLabel(context, document.status),
                  style: TextStyle(
                    color: statusColor,
                    fontWeight: FontWeight.w800,
                    fontSize: 15,
                  ),
                ),
                SizedBox(height: 4),
                Text(
                  document.description,
                  style: TextStyle(
                    color: Color(0xFF455A64),
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(BuildContext context, DocumentStatus status) {
    return switch (status) {
      DocumentStatus.verified => context.l10n.documentApproved,
      DocumentStatus.pending => context.l10n.documentPendingReview,
      DocumentStatus.rejected => context.l10n.documentRejected,
      DocumentStatus.notSubmitted => context.l10n.documentNotSubmitted,
    };
  }
}

class _InfoSection extends StatelessWidget {
  _InfoSection({required this.title, required this.rows});

  final String title;
  final List<_InfoRow> rows;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w800,
            color: Color(0xFF1F1F1F),
          ),
        ),
        SizedBox(height: 12),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Color(0xFFFAFAFA),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: Color(0xFFE0E0E0)),
          ),
          child: Column(
            children: rows
                .map(
                  (row) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SizedBox(
                          width: 110,
                          child: Text(
                            row.label,
                            style: TextStyle(
                              color: Color(0xFF78909C),
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                        Expanded(
                          child: Text(
                            row.value,
                            style: TextStyle(
                              color: Color(0xFF263238),
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
        ),
      ],
    );
  }
}

class _InfoRow {
  _InfoRow(this.label, this.value);

  final String label;
  final String value;
}

class _DocumentPreview extends StatelessWidget {
  _DocumentPreview({required this.label, required this.url});

  final String label;
  final String url;

  @override
  Widget build(BuildContext context) {
    final resolvedUrl = _resolveUrl(url);
    final isPdf = resolvedUrl.toLowerCase().contains('.pdf');

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Color(0xFFFAFAFA),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Color(0xFFE0E0E0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: Color(0xFF263238),
              fontWeight: FontWeight.w800,
            ),
          ),
          SizedBox(height: 10),
          if (isPdf)
            Row(
              children: [
                Icon(Icons.picture_as_pdf, color: Color(0xFFD32F2F)),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    resolvedUrl,
                    style: TextStyle(
                      color: Color(0xFF455A64),
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            )
          else
            ClipRRect(
              borderRadius: BorderRadius.circular(10),
              child: Image.network(
                resolvedUrl,
                width: double.infinity,
                fit: BoxFit.cover,
                loadingBuilder: (context, child, loadingProgress) {
                  if (loadingProgress == null) {
                    return child;
                  }
                  return SizedBox(
                    height: 180,
                    child: Center(
                      child: CircularProgressIndicator(
                        color: AppColors.primary,
                      ),
                    ),
                  );
                },
                errorBuilder: (context, error, stackTrace) {
                  return Container(
                    height: 120,
                    alignment: Alignment.center,
                    child: Text(
                      resolvedUrl,
                      style: TextStyle(
                        color: Color(0xFF455A64),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }

  String _resolveUrl(String value) {
    final trimmed = value.trim();
    if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
      return trimmed;
    }

    final apiBase = AppConfig.apiBaseUrl.endsWith('/')
        ? AppConfig.apiBaseUrl.substring(0, AppConfig.apiBaseUrl.length - 1)
        : AppConfig.apiBaseUrl;
    final origin = apiBase.endsWith('/api')
        ? apiBase.substring(0, apiBase.length - 4)
        : apiBase;
    final path = trimmed.startsWith('/') ? trimmed : '/$trimmed';
    return '$origin$path';
  }
}
