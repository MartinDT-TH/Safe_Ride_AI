import 'package:flutter/material.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../data/models/identity_document_model.dart';

class IdentityDocumentDetailPage extends StatelessWidget {
  const IdentityDocumentDetailPage({super.key, required this.document});

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
          icon: const Icon(Icons.arrow_back, color: Color(0xFF263238)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Text(
          document.title,
          style: const TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _StatusSummary(document: document, statusColor: statusColor),
            const SizedBox(height: 24),
            _InfoSection(
              title: 'Thông tin đã gửi',
              rows: [
                if (_hasText(document.documentNumber))
                  _InfoRow('Số giấy tờ', document.documentNumber!),
                if (_hasText(document.licenseClass))
                  _InfoRow('Hạng bằng', document.licenseClass!),
                if (_hasText(document.issueDate))
                  _InfoRow('Ngày cấp', document.issueDate!),
                if (_hasText(document.expiryDate))
                  _InfoRow('Ngày hết hạn', document.expiryDate!),
              ],
            ),
            const SizedBox(height: 24),
            const Text(
              'Tài liệu',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w800,
                color: Color(0xFF1F1F1F),
              ),
            ),
            const SizedBox(height: 12),
            if (_hasText(document.frontImageUrl))
              _DocumentPreview(
                label: 'Mặt trước',
                url: document.frontImageUrl!,
              ),
            if (_hasText(document.backImageUrl)) ...[
              const SizedBox(height: 12),
              _DocumentPreview(
                label: 'Mặt sau',
                url: document.backImageUrl!,
              ),
            ],
            if (_hasText(document.fileUrl)) ...[
              if (_hasText(document.frontImageUrl) ||
                  _hasText(document.backImageUrl))
                const SizedBox(height: 12),
              _DocumentPreview(
                label: 'Tệp đã nộp',
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
      DocumentStatus.verified => const Color(0xFF2E7D32),
      DocumentStatus.pending => const Color(0xFFFFA000),
      DocumentStatus.rejected => const Color(0xFFD32F2F),
      DocumentStatus.notSubmitted => const Color(0xFF757575),
    };
  }
}

class _StatusSummary extends StatelessWidget {
  const _StatusSummary({required this.document, required this.statusColor});

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
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _statusLabel(document.status),
                  style: TextStyle(
                    color: statusColor,
                    fontWeight: FontWeight.w800,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  document.description,
                  style: const TextStyle(
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

  String _statusLabel(DocumentStatus status) {
    return switch (status) {
      DocumentStatus.verified => 'Đã duyệt',
      DocumentStatus.pending => 'Đã nộp, đang chờ duyệt',
      DocumentStatus.rejected => 'Bị từ chối',
      DocumentStatus.notSubmitted => 'Chưa nộp',
    };
  }
}

class _InfoSection extends StatelessWidget {
  const _InfoSection({required this.title, required this.rows});

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
          style: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w800,
            color: Color(0xFF1F1F1F),
          ),
        ),
        const SizedBox(height: 12),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFFAFAFA),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFE0E0E0)),
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
                            style: const TextStyle(
                              color: Color(0xFF78909C),
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                        Expanded(
                          child: Text(
                            row.value,
                            style: const TextStyle(
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
  const _InfoRow(this.label, this.value);

  final String label;
  final String value;
}

class _DocumentPreview extends StatelessWidget {
  const _DocumentPreview({required this.label, required this.url});

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
        color: const Color(0xFFFAFAFA),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE0E0E0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(
              color: Color(0xFF263238),
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          if (isPdf)
            Row(
              children: [
                const Icon(Icons.picture_as_pdf, color: Color(0xFFD32F2F)),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    resolvedUrl,
                    style: const TextStyle(
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
                  return const SizedBox(
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
                      style: const TextStyle(
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
