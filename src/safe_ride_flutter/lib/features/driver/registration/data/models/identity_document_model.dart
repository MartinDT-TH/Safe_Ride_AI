import 'package:flutter/cupertino.dart';

enum DocumentStatus {
  notSubmitted,
  pending,
  verified,
  rejected,
}

class IdentityDocumentModel {
  final String title;
  final String description;
  final IconData icon;
  final DocumentStatus status;
  final String? rejectionReason;

  const IdentityDocumentModel({
    required this.title,
    required this.description,
    required this.icon,
    this.status = DocumentStatus.notSubmitted,
    this.rejectionReason,
  });
}
