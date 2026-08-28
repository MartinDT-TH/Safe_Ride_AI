import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../data/models/risk_protection_models.dart';
import '../providers/risk_protection_provider.dart';
import '../risk_protection_labels.dart';
import 'accident_details_page.dart';

class DriverLiabilitiesPage extends StatelessWidget {
  const DriverLiabilitiesPage({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (_) => getIt<RiskProtectionProvider>()..loadDriverLiabilities(),
    child: const _DriverLiabilitiesView(),
  );
}

class _DriverLiabilitiesView extends StatelessWidget {
  const _DriverLiabilitiesView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<RiskProtectionProvider>();
    return Scaffold(
      appBar: AppBar(title: Text(context.l10n.driverLiabilities)),
      body: RefreshIndicator(
        onRefresh: provider.loadDriverLiabilities,
        child: provider.isLoading && provider.liabilities.isEmpty
            ? const Center(child: CircularProgressIndicator())
            : ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                children: [
                  if (provider.errorMessage != null)
                    _ErrorCard(
                      message: provider.errorMessage!,
                      onRetry: provider.loadDriverLiabilities,
                    )
                  else if (provider.liabilities.isEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 120),
                      child: Column(
                        children: [
                          Icon(
                            Icons.verified_user_outlined,
                            size: 58,
                            color: Colors.grey.shade500,
                          ),
                          const SizedBox(height: 12),
                          Text(
                            context.l10n.noDriverLiabilities,
                            textAlign: TextAlign.center,
                          ),
                        ],
                      ),
                    )
                  else
                    ...provider.liabilities.map(
                      (item) => _LiabilityCard(item: item),
                    ),
                ],
              ),
      ),
    );
  }
}

class _LiabilityCard extends StatelessWidget {
  const _LiabilityCard({required this.item});
  final DriverLiabilityItem item;

  @override
  Widget build(BuildContext context) => Card(
    margin: const EdgeInsets.only(bottom: 14),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Claim #${item.claimId}',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              Chip(
                label: Text(
                  driverLiabilityStatusLabel(context.l10n, item.status),
                ),
              ),
            ],
          ),
          Text(
            '${context.l10n.claimStatus}: ${claimStatusLabel(context.l10n, item.claimStatus)}',
          ),
          const Divider(height: 24),
          _AmountRow(
            label: context.l10n.attributableDamage,
            value: item.attributableDamage,
          ),
          _AmountRow(
            label: context.l10n.confirmedAmount,
            value: item.confirmedAmount,
          ),
          _AmountRow(label: context.l10n.paidAmount, value: item.paidAmount),
          _AmountRow(
            label: context.l10n.outstandingAmount,
            value: item.outstandingAmount,
            emphasize: true,
          ),
          const SizedBox(height: 6),
          Text(
            driverFaultLevelLabel(context.l10n, item.faultLevel),
            style: Theme.of(context).textTheme.bodySmall,
          ),
          if (item.recoveries.isNotEmpty) ...[
            const Divider(height: 24),
            Text(
              context.l10n.recoveryHistory,
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 6),
            ...item.recoveries.map(
              (recovery) => ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: Text(_money(recovery.amount)),
                subtitle: Text(
                  '${DateFormat('dd/MM/yyyy HH:mm').format(recovery.recordedAt)} · ${recovery.maskedReference}',
                ),
              ),
            ),
          ],
          if (item.accidentId != null) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) =>
                        AccidentDetailsPage(accidentId: item.accidentId!),
                  ),
                ),
                icon: const Icon(Icons.open_in_new),
                label: Text(context.l10n.riskProtectionCaseTitle),
              ),
            ),
          ],
        ],
      ),
    ),
  );
}

class _AmountRow extends StatelessWidget {
  const _AmountRow({
    required this.label,
    required this.value,
    this.emphasize = false,
  });
  final String label;
  final double value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      children: [
        Expanded(child: Text(label)),
        Text(
          _money(value),
          style: TextStyle(
            fontWeight: emphasize ? FontWeight.w800 : FontWeight.w600,
            color: emphasize && value > 0
                ? Theme.of(context).colorScheme.error
                : null,
          ),
        ),
      ],
    ),
  );
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});
  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          FilledButton.tonal(
            onPressed: onRetry,
            child: Text(context.l10n.retry),
          ),
        ],
      ),
    ),
  );
}

String _money(num value) => NumberFormat.currency(
  locale: 'vi_VN',
  symbol: '₫',
  decimalDigits: 0,
).format(value);
