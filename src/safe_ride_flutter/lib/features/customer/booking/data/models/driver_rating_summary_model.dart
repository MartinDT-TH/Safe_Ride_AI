class DriverRatingSummaryModel {
  final double averageRating;
  final int totalReviews;
  final Map<int, double> ratingPercentages;

  const DriverRatingSummaryModel({
    required this.averageRating,
    required this.totalReviews,
    required this.ratingPercentages,
  });
}
