class PromoModel {
  final String code;
  final String description;
  final String expiry;
  final bool isExpiringSoon;

  const PromoModel({
    required this.code,
    required this.description,
    required this.expiry,
    this.isExpiringSoon = false,
  });
}
