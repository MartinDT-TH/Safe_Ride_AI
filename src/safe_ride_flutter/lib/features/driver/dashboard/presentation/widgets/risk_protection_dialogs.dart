import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../../core/localization/localization_extensions.dart';

typedef EvidenceImagePicker = Future<XFile?> Function(ImageSource source);

class PreTripCheckDialogResult {
  const PreTripCheckDialogResult({
    required this.values,
    this.faultType,
    this.note,
    this.evidence,
  });

  final List<bool> values;
  final String? faultType;
  final String? note;
  final XFile? evidence;
  bool get allPassed => values.every((value) => value);
}

class SafetyTerminationDialogResult {
  const SafetyTerminationDialogResult({required this.reason, this.evidence});

  final String reason;
  final XFile? evidence;
}

const vehicleFaultTypes = <String>[
  'BRAKE_FAILURE',
  'LIGHT_FAILURE',
  'TIRE_FAILURE',
  'STEERING_FAILURE',
  'ENGINE_FAILURE',
  'ELECTRICAL_FAILURE',
  'OTHER',
];

String safetyReportReasonCodeForType(String reportType) => switch (reportType) {
  'UNSAFE_CUSTOMER' => 'UNSAFE_CUSTOMER',
  'VEHICLE_ISSUE' => 'VEHICLE_ISSUE',
  _ => throw ArgumentError.value(reportType, 'reportType'),
};

class SafetyReportDialogResult {
  const SafetyReportDialogResult({
    required this.reportType,
    required this.reasonCode,
    required this.description,
    required this.escalationRequested,
  });

  final String reportType;
  final String reasonCode;
  final String description;
  final bool escalationRequested;
}

Future<SafetyReportDialogResult?> showSafetyReportDialog(BuildContext context) {
  var reportType = 'UNSAFE_CUSTOMER';
  var description = '';
  var escalationRequested = false;
  return showDialog<SafetyReportDialogResult>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (context, setState) => AlertDialog(
        title: Text(context.l10n.safetyReportTitle),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              SegmentedButton<String>(
                segments: [
                  ButtonSegment(
                    value: 'UNSAFE_CUSTOMER',
                    label: Text(context.l10n.unsafeCustomer),
                    icon: const Icon(Icons.person_off_outlined),
                  ),
                  ButtonSegment(
                    value: 'VEHICLE_ISSUE',
                    label: Text(context.l10n.vehicleIssue),
                    icon: const Icon(Icons.car_repair_outlined),
                  ),
                ],
                selected: {reportType},
                onSelectionChanged: (selection) =>
                    setState(() => reportType = selection.single),
              ),
              const SizedBox(height: 12),
              TextField(
                minLines: 3,
                maxLines: 5,
                onChanged: (value) => setState(() => description = value),
                decoration: InputDecoration(
                  labelText: context.l10n.safetyReportDescription,
                ),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(context.l10n.requestSosEscalation),
                subtitle: Text(context.l10n.requestSosEscalationHint),
                value: escalationRequested,
                onChanged: (value) =>
                    setState(() => escalationRequested = value),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed: description.trim().isEmpty
                ? null
                : () => Navigator.pop(
                    dialogContext,
                    SafetyReportDialogResult(
                      reportType: reportType,
                      reasonCode: safetyReportReasonCodeForType(reportType),
                      description: description.trim(),
                      escalationRequested: escalationRequested,
                    ),
                  ),
            child: Text(context.l10n.report),
          ),
        ],
      ),
    ),
  );
}

Future<PreTripCheckDialogResult?> showPreTripSafetyCheckDialog(
  BuildContext context,
) {
  final labels = [
    context.l10n.brakeResponse,
    context.l10n.frontRearLights,
    context.l10n.turnSignals,
    context.l10n.visibleTires,
    context.l10n.dashboardWarning,
    context.l10n.windshieldVisibility,
    context.l10n.noMajorVisibleIssue,
  ];
  final values = List<bool>.filled(labels.length, false);
  String? faultType;
  var note = '';
  XFile? evidence;

  return showDialog<PreTripCheckDialogResult>(
    context: context,
    barrierDismissible: false,
    builder: (dialogContext) => StatefulBuilder(
      builder: (context, setState) {
        final allPassed = values.every((value) => value);
        return AlertDialog(
          title: Text(context.l10n.preTripSafetyTitle),
          content: SizedBox(
            width: 520,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(context.l10n.preTripSafetyDescription),
                  const SizedBox(height: 12),
                  for (var index = 0; index < labels.length; index++)
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      controlAffinity: ListTileControlAffinity.leading,
                      title: Text(labels[index]),
                      value: values[index],
                      onChanged: (value) =>
                          setState(() => values[index] = value == true),
                    ),
                  TextField(
                    maxLines: 2,
                    onChanged: (value) => note = value,
                    decoration: InputDecoration(
                      labelText: context.l10n.optionalNote,
                    ),
                  ),
                  if (!allPassed) ...[
                    const SizedBox(height: 10),
                    DropdownButtonFormField<String>(
                      initialValue: faultType,
                      decoration: InputDecoration(
                        labelText: context.l10n.vehicleFaultType,
                      ),
                      items: vehicleFaultTypes
                          .map(
                            (value) => DropdownMenuItem(
                              value: value,
                              child: Text(_faultTypeLabel(context, value)),
                            ),
                          )
                          .toList(growable: false),
                      onChanged: (value) => setState(() => faultType = value),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      context.l10n.allChecksRequired,
                      style: const TextStyle(color: Colors.redAccent),
                    ),
                  ],
                  const SizedBox(height: 10),
                  OutlinedButton.icon(
                    onPressed: () async {
                      final picked = await ImagePicker().pickImage(
                        source: ImageSource.gallery,
                        imageQuality: 85,
                      );
                      if (picked != null) setState(() => evidence = picked);
                    },
                    icon: const Icon(Icons.attach_file),
                    label: Text(
                      evidence == null
                          ? context.l10n.optionalEvidence
                          : evidence!.name,
                    ),
                  ),
                  if (evidence != null)
                    TextButton.icon(
                      onPressed: () => setState(() => evidence = null),
                      icon: const Icon(Icons.close),
                      label: Text(context.l10n.removePhoto),
                    ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: Text(context.l10n.cancel),
            ),
            FilledButton(
              onPressed: !allPassed && faultType == null
                  ? null
                  : () => Navigator.of(dialogContext).pop(
                      PreTripCheckDialogResult(
                        values: List<bool>.from(values),
                        faultType: allPassed ? null : faultType,
                        note: note.trim().isEmpty ? null : note.trim(),
                        evidence: evidence,
                      ),
                    ),
              child: Text(context.l10n.confirmSafetyCheck),
            ),
          ],
        );
      },
    ),
  );
}

Future<SafetyTerminationDialogResult?> showSafetyTerminationDialog(
  BuildContext context, {
  EvidenceImagePicker? pickEvidenceImage,
}) {
  var reason = '';
  XFile? evidence;
  Uint8List? evidenceBytes;
  String? mediaError;
  final picker =
      pickEvidenceImage ??
      (source) => ImagePicker().pickImage(
        source: source,
        preferredCameraDevice: CameraDevice.rear,
        imageQuality: 85,
      );
  return showDialog<SafetyTerminationDialogResult>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (context, setState) => AlertDialog(
        title: Text(context.l10n.safetyTermination),
        content: SizedBox(
          width: 420,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(context.l10n.safetyTerminationDescription),
                const SizedBox(height: 14),
                TextField(
                  autofocus: true,
                  maxLines: 4,
                  onChanged: (value) => setState(() => reason = value),
                  decoration: InputDecoration(
                    hintText: context.l10n.safetyTerminationReasonHint,
                  ),
                ),
                const SizedBox(height: 10),
                if (evidence != null) ...[
                  ClipRRect(
                    borderRadius: BorderRadius.circular(12),
                    child: evidenceBytes == null
                        ? Container(
                            height: 150,
                            width: double.infinity,
                            color: Theme.of(
                              context,
                            ).colorScheme.surfaceContainer,
                            alignment: Alignment.center,
                            child: const Icon(Icons.image_outlined, size: 44),
                          )
                        : Image.memory(
                            evidenceBytes!,
                            height: 180,
                            width: double.infinity,
                            fit: BoxFit.cover,
                            errorBuilder: (context, error, stackTrace) =>
                                Container(
                                  height: 150,
                                  color: Theme.of(
                                    context,
                                  ).colorScheme.surfaceContainer,
                                  alignment: Alignment.center,
                                  child: const Icon(
                                    Icons.broken_image_outlined,
                                  ),
                                ),
                          ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      const Icon(
                        Icons.check_circle,
                        color: Colors.green,
                        size: 18,
                      ),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          evidence!.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                ],
                OutlinedButton.icon(
                  onPressed: () async {
                    setState(() => mediaError = null);
                    try {
                      final picked = await picker(ImageSource.camera);
                      if (picked == null || !context.mounted) return;
                      Uint8List? previewBytes;
                      try {
                        previewBytes = await picked.readAsBytes();
                      } catch (_) {
                        previewBytes = null;
                      }
                      if (!context.mounted) return;
                      setState(() {
                        evidence = picked;
                        evidenceBytes = previewBytes;
                      });
                    } on PlatformException {
                      if (!context.mounted) return;
                      setState(
                        () => mediaError = context.l10n.mediaAccessFailed(
                          context.l10n.camera,
                        ),
                      );
                    } catch (_) {
                      if (!context.mounted) return;
                      setState(
                        () => mediaError = context.l10n.mediaAccessFailed(
                          context.l10n.camera,
                        ),
                      );
                    }
                  },
                  icon: const Icon(Icons.camera_alt_outlined),
                  label: Text(
                    evidence == null
                        ? context.l10n.captureSafetyEvidence
                        : context.l10n.retakePhoto,
                  ),
                ),
                if (mediaError != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    mediaError!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                if (evidence != null)
                  TextButton.icon(
                    onPressed: () => setState(() {
                      evidence = null;
                      evidenceBytes = null;
                      mediaError = null;
                    }),
                    icon: const Icon(Icons.close),
                    label: Text(context.l10n.removePhoto),
                  ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed: reason.trim().isEmpty
                ? null
                : () => Navigator.of(dialogContext).pop(
                    SafetyTerminationDialogResult(
                      reason: reason.trim(),
                      evidence: evidence,
                    ),
                  ),
            child: Text(context.l10n.safetyTermination),
          ),
        ],
      ),
    ),
  );
}

Future<String?> showAccidentReportDialog(BuildContext context) =>
    _showReasonDialog(
      context,
      title: context.l10n.reportAccident,
      description: context.l10n.accidentDescriptionHint,
      hint: context.l10n.accidentDescriptionHint,
      confirmLabel: context.l10n.createAccidentReport,
    );

Future<String?> _showReasonDialog(
  BuildContext context, {
  required String title,
  required String description,
  required String hint,
  required String confirmLabel,
}) {
  var reason = '';
  return showDialog<String>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (context, setState) => AlertDialog(
        title: Text(title),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(description),
            const SizedBox(height: 14),
            TextField(
              autofocus: true,
              maxLines: 4,
              onChanged: (value) => setState(() => reason = value),
              decoration: InputDecoration(hintText: hint),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: Text(context.l10n.cancel),
          ),
          FilledButton(
            onPressed: reason.trim().isEmpty
                ? null
                : () => Navigator.of(dialogContext).pop(reason.trim()),
            child: Text(confirmLabel),
          ),
        ],
      ),
    ),
  );
}

String _faultTypeLabel(BuildContext context, String value) => switch (value) {
  'BRAKE_FAILURE' => context.l10n.brakeResponse,
  'LIGHT_FAILURE' => context.l10n.frontRearLights,
  'TIRE_FAILURE' => context.l10n.visibleTires,
  'STEERING_FAILURE' => context.l10n.otherVehicleFault,
  'ENGINE_FAILURE' => context.l10n.dashboardWarning,
  'ELECTRICAL_FAILURE' => context.l10n.turnSignals,
  _ => context.l10n.otherVehicleFault,
};
