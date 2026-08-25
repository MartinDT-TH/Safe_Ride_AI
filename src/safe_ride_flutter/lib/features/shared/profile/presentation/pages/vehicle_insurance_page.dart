import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/utils/currency_formatter.dart';
import '../../data/models/vehicle_model.dart';
import '../providers/vehicle_provider.dart';

class VehicleInsurancePage extends StatefulWidget {
  const VehicleInsurancePage({required this.vehicle, super.key});

  final VehicleModel vehicle;

  @override
  State<VehicleInsurancePage> createState() => _VehicleInsurancePageState();
}

class _VehicleInsurancePageState extends State<VehicleInsurancePage> {
  List<VehicleInsurancePolicyModel> _items = const [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await context.read<VehicleProvider>().loadInsurancePolicies(
        widget.vehicle.id,
      );
      if (mounted) {
        setState(() => _items = items);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = context.l10n.insuranceLoadFailed);
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _edit([VehicleInsurancePolicyModel? current]) async {
    final result = await _showPolicyDialog(context, widget.vehicle.id, current);
    if (!mounted || result == null) return;
    final saved = await context.read<VehicleProvider>().saveInsurancePolicy(
      result,
    );
    if (!mounted) return;
    if (saved) {
      await _load();
    } else {
      _showError();
    }
  }

  Future<void> _delete(VehicleInsurancePolicyModel item) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(context.l10n.deleteInsuranceQuestion),
        content: Text('${context.l10n.policyNumber} ${item.policyNumber}.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(context.l10n.delete),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    final deleted = await context.read<VehicleProvider>().deleteInsurancePolicy(
      widget.vehicle.id,
      item.id,
    );
    if (!mounted) return;
    if (deleted) {
      await _load();
    } else {
      _showError();
    }
  }

  void _showError() => ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(
        context.read<VehicleProvider>().errorMessage ??
            context.l10n.insuranceUpdateFailed,
      ),
    ),
  );

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(
        '${context.l10n.vehicleInsurance} · ${widget.vehicle.plateNumber}',
      ),
    ),
    floatingActionButton: FloatingActionButton.extended(
      onPressed: context.watch<VehicleProvider>().isMutating
          ? null
          : () => _edit(),
      icon: const Icon(Icons.add),
      label: Text(context.l10n.addInsurance),
    ),
    body: RefreshIndicator(
      onRefresh: _load,
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
          ? ListView(
              children: [
                const SizedBox(height: 120),
                Icon(Icons.cloud_off, size: 56, color: Colors.grey.shade500),
                const SizedBox(height: 12),
                Center(child: Text(_error!)),
                Center(
                  child: TextButton.icon(
                    onPressed: _load,
                    icon: const Icon(Icons.refresh),
                    label: Text(context.l10n.retry),
                  ),
                ),
              ],
            )
          : _items.isEmpty
          ? ListView(
              children: [
                const SizedBox(height: 120),
                const Icon(
                  Icons.verified_user_outlined,
                  size: 56,
                  color: Colors.grey,
                ),
                const SizedBox(height: 12),
                Center(child: Text(context.l10n.optionalInsuranceEmpty)),
              ],
            )
          : ListView.builder(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
              itemCount: _items.length,
              itemBuilder: (context, index) {
                final item = _items[index];
                return Card(
                  child: ListTile(
                    leading: const Icon(Icons.health_and_safety_outlined),
                    title: Text('${item.provider} · ${item.policyNumber}'),
                    subtitle: Text(
                      '${item.insuranceType}\n${DateFormat('dd/MM/yyyy').format(item.effectiveFromUtc.toLocal())} – ${DateFormat('dd/MM/yyyy').format(item.expiresAtUtc.toLocal())}\n${context.l10n.insuranceCoverageLimit} ${formatVnd(item.coverageAmount)} · ${context.l10n.insuranceDeductible} ${formatVnd(item.deductible)}',
                    ),
                    isThreeLine: true,
                    trailing: PopupMenuButton<String>(
                      tooltip:
                          '${context.l10n.statusLabel}: ${item.verificationStatus}',
                      onSelected: (value) =>
                          value == 'edit' ? _edit(item) : _delete(item),
                      itemBuilder: (_) => [
                        PopupMenuItem(
                          value: 'edit',
                          child: Text(context.l10n.edit),
                        ),
                        PopupMenuItem(
                          value: 'delete',
                          child: Text(context.l10n.delete),
                        ),
                      ],
                      child: Chip(label: Text(item.verificationStatus)),
                    ),
                  ),
                );
              },
            ),
    ),
  );
}

Future<VehicleInsurancePolicyModel?> _showPolicyDialog(
  BuildContext context,
  int vehicleId,
  VehicleInsurancePolicyModel? current,
) {
  var type = current?.insuranceType ?? 'MANDATORY_TPL';
  var provider = current?.provider ?? '';
  var number = current?.policyNumber ?? '';
  var effective = current?.effectiveFromUtc.toLocal() ?? DateTime.now();
  var expires =
      current?.expiresAtUtc.toLocal() ??
      DateTime.now().add(const Duration(days: 365));
  var coverage = current?.coverageAmount.toStringAsFixed(0) ?? '';
  var deductible = current?.deductible.toStringAsFixed(0) ?? '0';
  var documentUrl = current?.documentUrl ?? '';
  return showDialog<VehicleInsurancePolicyModel>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (context, setState) => AlertDialog(
        title: Text(
          current == null
              ? context.l10n.addInsurancePolicy
              : context.l10n.editInsurancePolicy,
        ),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: type,
                decoration: InputDecoration(
                  labelText: context.l10n.insuranceType,
                ),
                items: [
                  DropdownMenuItem(
                    value: 'MANDATORY_TPL',
                    child: Text(context.l10n.mandatoryTplInsurance),
                  ),
                  DropdownMenuItem(
                    value: 'PHYSICAL_DAMAGE',
                    child: Text(context.l10n.physicalDamageInsurance),
                  ),
                  DropdownMenuItem(
                    value: 'OTHER',
                    child: Text(context.l10n.otherVehicleFault),
                  ),
                ],
                onChanged: (value) => setState(() => type = value ?? type),
              ),
              TextFormField(
                initialValue: provider,
                decoration: InputDecoration(
                  labelText: context.l10n.insuranceProvider,
                ),
                onChanged: (value) => setState(() => provider = value),
              ),
              TextFormField(
                initialValue: number,
                decoration: InputDecoration(
                  labelText: context.l10n.policyNumber,
                ),
                onChanged: (value) => setState(() => number = value),
              ),
              _DateButton(
                label: context.l10n.effectiveDate,
                value: effective,
                onChanged: (value) => setState(() => effective = value),
              ),
              _DateButton(
                label: context.l10n.expiryDate,
                value: expires,
                onChanged: (value) => setState(() => expires = value),
              ),
              TextFormField(
                initialValue: coverage,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: context.l10n.insuranceCoverageLimit,
                ),
                onChanged: (value) => setState(() => coverage = value),
              ),
              TextFormField(
                initialValue: deductible,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: context.l10n.insuranceDeductible,
                ),
                onChanged: (value) => setState(() => deductible = value),
              ),
              TextFormField(
                initialValue: documentUrl,
                keyboardType: TextInputType.url,
                decoration: InputDecoration(
                  labelText: context.l10n.optionalDocumentUrl,
                ),
                onChanged: (value) => documentUrl = value,
              ),
              const SizedBox(height: 8),
              Text(context.l10n.optionalInsuranceHint),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed:
                provider.trim().isEmpty ||
                    number.trim().isEmpty ||
                    !expires.isAfter(effective) ||
                    double.tryParse(coverage) == null ||
                    double.tryParse(deductible) == null
                ? null
                : () => Navigator.pop(
                    dialogContext,
                    VehicleInsurancePolicyModel(
                      id: current?.id ?? 0,
                      vehicleId: vehicleId,
                      insuranceType: type,
                      provider: provider.trim(),
                      policyNumber: number.trim(),
                      effectiveFromUtc: effective.toUtc(),
                      expiresAtUtc: expires.toUtc(),
                      coverageAmount: double.parse(coverage),
                      deductible: double.parse(deductible),
                      documentUrl: documentUrl.trim().isEmpty
                          ? null
                          : documentUrl.trim(),
                      verificationStatus: 'PENDING',
                    ),
                  ),
            child: Text(context.l10n.saveChanges),
          ),
        ],
      ),
    ),
  );
}

class _DateButton extends StatelessWidget {
  const _DateButton({
    required this.label,
    required this.value,
    required this.onChanged,
  });
  final String label;
  final DateTime value;
  final ValueChanged<DateTime> onChanged;

  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    title: Text(label),
    subtitle: Text(DateFormat('dd/MM/yyyy').format(value)),
    trailing: const Icon(Icons.calendar_month),
    onTap: () async {
      final picked = await showDatePicker(
        context: context,
        initialDate: value,
        firstDate: DateTime.now().subtract(const Duration(days: 3650)),
        lastDate: DateTime.now().add(const Duration(days: 3650)),
      );
      if (picked != null) onChanged(picked);
    },
  );
}
