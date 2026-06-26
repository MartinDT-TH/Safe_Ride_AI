import 'dart:io';

class IdentityVerificationSubmission {
  File? cccdFrontImage;
  File? cccdBackImage;
  String? cccdNumber;
  String? cccdFullName;

  File? licenseFrontImage;
  File? licenseBackImage;
  String? licenseNumber;
  String? licenseFullName;
  String? licenseClass;
  DateTime? licenseIssueDate;
  DateTime? licenseExpiryDate;
  bool licenseHasNoExpiryDate = false;

  File? criminalRecordFile;
}
