import 'package:flutter/widgets.dart';

enum DocumentStatus {
  notSubmitted,
  pending,
  verified,
  rejected,
}

class IdentityDocumentModel {
  final String documentType;
  final String title;
  final String description;
  final IconData icon;
  final DocumentStatus status;
  final String? rejectionReason;
  final String? documentNumber;
  final String? licenseClass;
  final String? frontImageUrl;
  final String? backImageUrl;
  final String? fileUrl;
  final String? issueDate;
  final String? expiryDate;

  const IdentityDocumentModel({
    required this.documentType,
    required this.title,
    required this.description,
    required this.icon,
    this.status = DocumentStatus.notSubmitted,
    this.rejectionReason,
    this.documentNumber,
    this.licenseClass,
    this.frontImageUrl,
    this.backImageUrl,
    this.fileUrl,
    this.issueDate,
    this.expiryDate,
  });

  IdentityDocumentModel copyWith({
    String? description,
    DocumentStatus? status,
    String? rejectionReason,
    String? documentNumber,
    String? licenseClass,
    String? frontImageUrl,
    String? backImageUrl,
    String? fileUrl,
    String? issueDate,
    String? expiryDate,
  }) {
    return IdentityDocumentModel(
      documentType: documentType,
      title: title,
      description: description ?? this.description,
      icon: icon,
      status: status ?? this.status,
      rejectionReason: rejectionReason ?? this.rejectionReason,
      documentNumber: documentNumber ?? this.documentNumber,
      licenseClass: licenseClass ?? this.licenseClass,
      frontImageUrl: frontImageUrl ?? this.frontImageUrl,
      backImageUrl: backImageUrl ?? this.backImageUrl,
      fileUrl: fileUrl ?? this.fileUrl,
      issueDate: issueDate ?? this.issueDate,
      expiryDate: expiryDate ?? this.expiryDate,
    );
  }

  static DocumentStatus statusFromBackend(String? value) {
    return switch (value) {
      'Approved' => DocumentStatus.verified,
      'Rejected' => DocumentStatus.rejected,
      'Pending' => DocumentStatus.pending,
      _ => DocumentStatus.notSubmitted,
    };
  }
}
