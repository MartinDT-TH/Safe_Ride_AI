import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../data/models/risk_protection_models.dart';
import '../providers/risk_protection_provider.dart';

class AccidentDetailsPage extends StatelessWidget {
  const AccidentDetailsPage({required this.accidentId, super.key});

  final int accidentId;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (_) => getIt<RiskProtectionProvider>()..loadAccident(accidentId),
    child: _AccidentDetailsView(accidentId: accidentId),
  );
}

class _AccidentDetailsView extends StatelessWidget {
  const _AccidentDetailsView({required this.accidentId});

  final int accidentId;

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<RiskProtectionProvider>();
    final accident = provider.accident;
    return Scaffold(
      appBar: AppBar(title: Text(context.l10n.riskProtectionCaseTitle)),
      body: RefreshIndicator(
        onRefresh: () => provider.loadAccident(accidentId),
        child: accident == null
            ? _buildEmptyState(context, provider)
            : ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
                children: [
                  _AccidentCard(accident: accident),
                  const SizedBox(height: 16),
                  _EvidenceCard(accident: accident),
                  const SizedBox(height: 16),
                  _AssessmentCard(accident: accident),
                  const SizedBox(height: 16),
                  _ClaimCard(claim: accident.claim),
                  if (provider.errorMessage != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      provider.errorMessage!,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ],
                ],
              ),
      ),
      floatingActionButton: accident == null
          ? null
          : FloatingActionButton.extended(
              onPressed: provider.isMutating
                  ? null
                  : () => _uploadEvidence(context, provider),
              icon: const Icon(Icons.add_a_photo_outlined),
              label: Text(context.l10n.uploadAccidentEvidence),
            ),
    );
  }

  Widget _buildEmptyState(
    BuildContext context,
    RiskProtectionProvider provider,
  ) {
    if (provider.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(24),
      children: [
        const SizedBox(height: 120),
        Icon(
          Icons.report_gmailerrorred_outlined,
          size: 54,
          color: Colors.grey.shade500,
        ),
        const SizedBox(height: 12),
        Text(
          provider.errorMessage ?? context.l10n.genericError,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 12),
        Center(
          child: FilledButton.tonal(
            onPressed: () => provider.loadAccident(accidentId),
            child: Text(context.l10n.retry),
          ),
        ),
      ],
    );
  }

  Future<void> _uploadEvidence(
    BuildContext context,
    RiskProtectionProvider provider,
  ) async {
    final file = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      imageQuality: 85,
      maxWidth: 2400,
    );
    if (file == null || !context.mounted) return;
    final description = await _textDialog(
      context,
      title: context.l10n.uploadAccidentEvidence,
      hint: context.l10n.optionalNote,
      isRequired: false,
    );
    if (description == null || !context.mounted) return;
    final success = await provider.uploadEvidence(
      accidentId: accidentId,
      file: file,
      description: description,
    );
    if (!context.mounted) return;
    _showMessage(
      context,
      success
          ? context.l10n.accidentEvidenceUploaded
          : provider.errorMessage ?? context.l10n.evidenceUploadFailed,
    );
  }
}

class _AccidentCard extends StatelessWidget {
  const _AccidentCard({required this.accident});
  final RiskProtectionAccident accident;

  @override
  Widget build(BuildContext context) => _SectionCard(
    title: '#${accident.id} · Trip #${accident.tripId}',
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Row(label: context.l10n.accidentStatus, value: accident.status),
        _Row(label: context.l10n.accidentCategory, value: accident.category),
        _Row(
          label: context.l10n.accidentOccurredAt,
          value: DateFormat('dd/MM/yyyy HH:mm').format(accident.occurredAt),
        ),
        const Divider(),
        Text(accident.description),
        if (accident.policeReportReference?.isNotEmpty == true) ...[
          const SizedBox(height: 8),
          Text(
            accident.policeReportReference!,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ],
    ),
  );
}

class _EvidenceCard extends StatelessWidget {
  const _EvidenceCard({required this.accident});
  final RiskProtectionAccident accident;

  @override
  Widget build(BuildContext context) => _SectionCard(
    title: context.l10n.riskProtectionEvidence,
    child: accident.evidence.isEmpty
        ? Text(context.l10n.noAccidentEvidence)
        : Column(
            children: accident.evidence
                .map(
                  (item) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: item.fileUrl.isEmpty
                        ? const Icon(Icons.insert_drive_file_outlined)
                        : ClipRRect(
                            borderRadius: BorderRadius.circular(8),
                            child: Image.network(
                              item.fileUrl,
                              width: 52,
                              height: 52,
                              fit: BoxFit.cover,
                              errorBuilder: (context, error, stackTrace) =>
                                  const Icon(Icons.broken_image_outlined),
                            ),
                          ),
                    title: Text(item.originalFileName ?? item.type),
                    subtitle: Text(
                      item.description?.isNotEmpty == true
                          ? item.description!
                          : DateFormat(
                              'dd/MM/yyyy HH:mm',
                            ).format(item.createdAt),
                    ),
                  ),
                )
                .toList(growable: false),
          ),
  );
}

class _AssessmentCard extends StatelessWidget {
  const _AssessmentCard({required this.accident});
  final RiskProtectionAccident accident;

  @override
  Widget build(BuildContext context) {
    final assessment = accident.assessment;
    return _SectionCard(
      title: context.l10n.riskProtectionAssessment,
      action: assessment?.status == 'CONFIRMED'
          ? TextButton(
              onPressed: () => _dispute(context, accident),
              child: Text(context.l10n.disputeLiability),
            )
          : null,
      child: assessment == null
          ? Text(context.l10n.statusPending)
          : Column(
              children: [
                _Row(label: context.l10n.statusLabel, value: assessment.status),
                _Row(
                  label: 'Driver',
                  value:
                      '${assessment.driverFaultPercentage.toStringAsFixed(0)}% · ${assessment.driverFaultLevel}',
                ),
                _Row(
                  label: 'Customer',
                  value:
                      '${assessment.customerFaultPercentage.toStringAsFixed(0)}%',
                ),
                _Row(
                  label: 'Third party',
                  value:
                      '${assessment.thirdPartyFaultPercentage.toStringAsFixed(0)}%',
                ),
                _Row(
                  label: 'Vehicle',
                  value:
                      '${assessment.vehicleFailurePercentage.toStringAsFixed(0)}%',
                ),
                _Row(
                  label: 'Objective',
                  value:
                      '${assessment.objectiveCausePercentage.toStringAsFixed(0)}%',
                ),
                if (assessment.disputeReason?.isNotEmpty == true)
                  Text(
                    assessment.disputeReason!,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
              ],
            ),
    );
  }

  Future<void> _dispute(
    BuildContext context,
    RiskProtectionAccident accident,
  ) async {
    if (accident.evidence.isEmpty) {
      _showMessage(context, context.l10n.noAccidentEvidence);
      return;
    }
    final reason = await _textDialog(
      context,
      title: context.l10n.disputeLiability,
      hint: context.l10n.disputeReasonHint,
      isRequired: true,
    );
    if (reason == null || !context.mounted) return;
    final provider = context.read<RiskProtectionProvider>();
    final success = await provider.disputeLiability(
      accident.id,
      reason,
      accident.evidence.map((item) => item.id).toList(growable: false),
    );
    if (!context.mounted) return;
    _showMessage(
      context,
      success
          ? context.l10n.liabilityDisputed
          : provider.errorMessage ?? context.l10n.genericError,
    );
  }
}

class _ClaimCard extends StatelessWidget {
  const _ClaimCard({required this.claim});
  final ProtectionClaimSummary? claim;

  @override
  Widget build(BuildContext context) => _SectionCard(
    title: context.l10n.riskProtectionClaim,
    child: claim == null
        ? Text(context.l10n.noProtectionClaim)
        : Column(
            children: [
              _Row(label: context.l10n.claimStatus, value: claim!.status),
              _Row(
                label: context.l10n.insuranceCoverage,
                value: _money(claim!.insuranceCoveredAmount),
              ),
              _Row(
                label: context.l10n.riskFundCoverage,
                value: _money(
                  claim!.riskFundAdvanceAmount +
                      claim!.riskFundPermanentLossAmount,
                ),
              ),
              _Row(
                label: context.l10n.paidAmount,
                value: _money(claim!.totalPaidToClaimant),
              ),
              _Row(
                label: context.l10n.outstandingAmount,
                value: _money(claim!.outstandingRecoveryAmount),
              ),
            ],
          ),
  );
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.title, required this.child, this.action});
  final String title;
  final Widget child;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              action ?? const SizedBox.shrink(),
            ],
          ),
          const SizedBox(height: 12),
          child,
        ],
      ),
    ),
  );
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(label, style: TextStyle(color: Colors.grey.shade700)),
        ),
        const SizedBox(width: 12),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
        ),
      ],
    ),
  );
}

Future<String?> _textDialog(
  BuildContext context, {
  required String title,
  required String hint,
  required bool isRequired,
}) async {
  final controller = TextEditingController();
  try {
    return await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: TextField(
          controller: controller,
          minLines: 3,
          maxLines: 6,
          maxLength: 1000,
          decoration: InputDecoration(hintText: hint),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed: () {
              final value = controller.text.trim();
              if (!isRequired || value.isNotEmpty) {
                Navigator.pop(dialogContext, value);
              }
            },
            child: Text(context.l10n.confirm),
          ),
        ],
      ),
    );
  } finally {
    controller.dispose();
  }
}

void _showMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

String _money(num value) => NumberFormat.currency(
  locale: 'vi_VN',
  symbol: '₫',
  decimalDigits: 0,
).format(value);
