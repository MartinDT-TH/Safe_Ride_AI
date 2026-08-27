// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appName => 'SafeRide';

  @override
  String get language => 'Language';

  @override
  String get chooseLanguage => 'Choose language';

  @override
  String get vietnamese => 'Tiếng Việt';

  @override
  String get english => 'English';

  @override
  String get korean => '한국어';

  @override
  String get japanese => '日本語';

  @override
  String get simplifiedChinese => '简体中文';

  @override
  String get profileAndSettings => 'Profile & Settings';

  @override
  String get switchToDriver => 'Switch to Driver mode';

  @override
  String get startReceivingTrips => 'Start receiving trips';

  @override
  String get accountSection => 'ACCOUNT';

  @override
  String get editProfile => 'Edit profile';

  @override
  String get linkedAccounts => 'Linked accounts';

  @override
  String get registerAsDriver => 'Register as a driver';

  @override
  String get linked => 'Linked';

  @override
  String get notLinked => 'Not linked';

  @override
  String get appAndNotifications => 'APP & NOTIFICATIONS';

  @override
  String get notificationSettings => 'Notification settings';

  @override
  String get darkMode => 'Dark mode';

  @override
  String get supportAndLegal => 'SUPPORT & LEGAL';

  @override
  String get helpCenter => 'Help center';

  @override
  String get privacyPolicy => 'Privacy policy';

  @override
  String get termsOfService => 'Terms of service';

  @override
  String get logout => 'Log out';

  @override
  String get logoutQuestion => 'Log out?';

  @override
  String get logoutDescription =>
      'Are you sure you want to log out of the app?';

  @override
  String get cancel => 'Cancel';

  @override
  String get cannotSwitchToDriver =>
      'You cannot switch to Driver mode while a trip is active.';

  @override
  String get cannotSwitchToCustomer =>
      'You cannot switch to Customer mode while a trip is active.';

  @override
  String get tripNotFound => 'Trip not found.';

  @override
  String get sessionExpired =>
      'Your session has expired. Please sign in again.';

  @override
  String get genericError => 'Something went wrong. Please try again.';

  @override
  String get statusPending => 'Pending';

  @override
  String get statusDriverArriving => 'Driver is arriving';

  @override
  String get statusInProgress => 'In progress';

  @override
  String get statusCompleted => 'Completed';

  @override
  String get statusCancelled => 'Cancelled';

  @override
  String get notifications => 'Notifications';

  @override
  String get notificationsLoadFailed => 'Unable to load notifications';

  @override
  String get retry => 'Try again';

  @override
  String get noNotifications => 'No notifications yet';

  @override
  String get noNotificationsDescription =>
      'Approved system notifications will appear here.';

  @override
  String get read => 'Read';

  @override
  String get unread => 'Unread';

  @override
  String get notificationTypePromotion => 'Promotion';

  @override
  String get notificationTypeWarning => 'Warning';

  @override
  String get notificationTypeSystemUpdate => 'System update';

  @override
  String get loadMoreNotifications => 'Load more notifications';

  @override
  String get success => 'Success';

  @override
  String get error => 'Error';

  @override
  String get warning => 'Warning';

  @override
  String get information => 'Information';

  @override
  String get serverConnectionError =>
      'Unable to connect to the server. Please try again later.';

  @override
  String get serverConnectionErrorTitle => 'Server temporarily unavailable';

  @override
  String get serverConnectionRestored =>
      'The server is available again. Data is being reloaded.';

  @override
  String get serverConnectionRestoredTitle => 'Server reconnected';

  @override
  String get reload => 'Reload';

  @override
  String get confirm => 'Confirm';

  @override
  String get callStartFailed => 'Unable to start the call. Please try again.';

  @override
  String get callRejected => 'The other person declined the call.';

  @override
  String get callEnded => 'The call has ended.';

  @override
  String get callConnecting => 'Connecting...';

  @override
  String get callRinging => 'Ringing...';

  @override
  String get microphoneOn => 'Unmute';

  @override
  String get microphoneOff => 'Mute';

  @override
  String get endCall => 'End call';

  @override
  String get speaker => 'Speaker';

  @override
  String get earpiece => 'Earpiece';

  @override
  String get imageSelectionFailed => 'Unable to select an image.';

  @override
  String get chatTitle => 'Chat';

  @override
  String get chatReadOnly =>
      'The trip has ended. You can only review the messages.';

  @override
  String get noMessages => 'No messages yet.';

  @override
  String get messageHint => 'Type a message...';

  @override
  String get tripEnded => 'Trip ended';

  @override
  String get driverReviews => 'Driver reviews';

  @override
  String get driverHasNoReviews => 'This driver has no reviews yet.';

  @override
  String get allReviews => 'All reviews';

  @override
  String get reviews => 'reviews';

  @override
  String get reportIncident => 'Report an incident';

  @override
  String get reportHelpQuestion => 'How can we help?';

  @override
  String get tripIncident => 'Trip incident';

  @override
  String get paymentIssue => 'Payment issue';

  @override
  String get partyFeedback => 'Driver/customer feedback';

  @override
  String get appIssue => 'App issue';

  @override
  String get wrongRoute => 'Driver took the wrong route';

  @override
  String get driverLate => 'Driver arrived late';

  @override
  String get inappropriateBehavior => 'Inappropriate behavior';

  @override
  String get other => 'Other';

  @override
  String get reportTrip => 'Report trip';

  @override
  String get reportSent => 'Trip report sent successfully.';

  @override
  String get reportSendFailed => 'Unable to send the report. Please try again.';

  @override
  String get commonIssues => 'Common issues';

  @override
  String get issueEncountered => 'Issue encountered';

  @override
  String get issueDescriptionHint => 'Describe the issue in detail...';

  @override
  String get reportContentRequired => 'Please enter the report details.';

  @override
  String get safeRideDriver => 'SafeRide driver';

  @override
  String get sendReport => 'Send report';

  @override
  String get edit => 'Edit';

  @override
  String get delete => 'Delete';

  @override
  String requiredLicense(String licenseClass) {
    return 'License $licenseClass';
  }

  @override
  String get editVehicle => 'Edit vehicle';

  @override
  String get addVehicle => 'Add a new vehicle';

  @override
  String get vehicleType => 'Vehicle type';

  @override
  String get motorbike => 'Motorbike';

  @override
  String get car => 'Car';

  @override
  String get vehicleName => 'Vehicle name';

  @override
  String get vehicleNameHint => 'Example: Honda Vision';

  @override
  String get engineCapacity => 'Engine capacity (cc)';

  @override
  String get engineCapacityHint => 'Example: 110, 125, 150';

  @override
  String get licensePlate => 'License plate';

  @override
  String get licensePlateHint => 'Example: 29A1 - 123.45';

  @override
  String get color => 'Color';

  @override
  String get colorHint => 'Example: Blue';

  @override
  String get saveChanges => 'Save changes';

  @override
  String get saveVehicle => 'Save vehicle';

  @override
  String get vehicleNameValidation =>
      'Vehicle name must be between 2 and 100 characters.';

  @override
  String get engineCapacityValidation =>
      'Enter a valid motorbike engine capacity to determine the A1 or A license requirement.';

  @override
  String get licensePlateLengthValidation =>
      'License plate must be between 4 and 20 characters.';

  @override
  String get licensePlateFormatValidation =>
      'License plate may only contain letters, numbers, periods, spaces, and hyphens.';

  @override
  String get colorValidation => 'Color must not exceed 30 characters.';

  @override
  String get deleteVehicleQuestion => 'Delete vehicle?';

  @override
  String deleteVehicleDescription(String name) {
    return 'Are you sure you want to delete \"$name\"? This action cannot be undone.';
  }

  @override
  String get deleteNow => 'Delete now';

  @override
  String get dismiss => 'Cancel';

  @override
  String get requestFailed => 'Unable to process the request.';

  @override
  String get myVehicles => 'My vehicles';

  @override
  String get vehicleManagementDescription =>
      'Manage your vehicles for parking and driving-assistance services.';

  @override
  String get noVehicles => 'You have not added any vehicles yet.';

  @override
  String get historyLoadFailed => 'Unable to load trip history.';

  @override
  String get noTripHistory => 'No trip data available.';

  @override
  String get tripNotRebookable =>
      'This trip does not have enough information to rebook.';

  @override
  String get loadingTrip => 'Loading trip information...';

  @override
  String get chatOpenFailed => 'Unable to open chat right now.';

  @override
  String get chat => 'Chat';

  @override
  String get viewReviews => 'View reviews';

  @override
  String get tripDetailsLoadFailed => 'Unable to load trip details.';

  @override
  String get tripDetails => 'Trip details';

  @override
  String get rebookThisTrip => 'Book this trip again';

  @override
  String get tripCode => 'Trip code';

  @override
  String bookingOrder(int id) {
    return 'Booking #$id';
  }

  @override
  String get routeMapUnavailable =>
      'A route map is not available for this trip.';

  @override
  String get route => 'Route';

  @override
  String get tripRoute => 'Trip route';

  @override
  String get pickupPoint => 'Pickup';

  @override
  String get destinationPoint => 'Destination';

  @override
  String get distance => 'Distance';

  @override
  String get duration => 'Duration';

  @override
  String minutesValue(num minutes) {
    return '$minutes min';
  }

  @override
  String get unknown => 'Unknown';

  @override
  String get driverAndVehicle => 'Driver and vehicle';

  @override
  String get driverInfoUnavailable =>
      'Driver information is not available for this trip.';

  @override
  String plateValue(String plate) {
    return 'Plate: $plate';
  }

  @override
  String vehicleColorValue(String color) {
    return 'Color: $color';
  }

  @override
  String tripCountValue(int count) {
    return '$count trips';
  }

  @override
  String experienceYearsValue(int years) {
    return '$years years of experience';
  }

  @override
  String get tripCost => 'Trip cost';

  @override
  String get unknownPaymentMethod => 'Unknown method';

  @override
  String get fare => 'Fare';

  @override
  String get discount => 'Discount';

  @override
  String get total => 'Total';

  @override
  String paidAtValue(String time) {
    return 'Paid at $time';
  }

  @override
  String get customerReview => 'Customer review';

  @override
  String get reviewAndFeedback => 'Review and feedback';

  @override
  String get customerHasNotReviewed =>
      'The customer has not reviewed this trip.';

  @override
  String get noReviewData => 'No review data is available for this trip.';

  @override
  String get tripHistory => 'Trip history';

  @override
  String get tripCompletedThanks => 'Trip completed. Thank you!';

  @override
  String get tripInfoUnavailable =>
      'Unable to identify the trip. Please try again.';

  @override
  String get returnConfirmationFailed =>
      'Unable to confirm the vehicle return. Please try again.';

  @override
  String get ratingSubmitFailed =>
      'Unable to submit your rating. Please try again.';

  @override
  String get waitForPayment => 'Please wait for payment to complete.';

  @override
  String get completeRequirementsBeforeLeaving =>
      'Confirm the vehicle return and submit your rating before leaving this screen.';

  @override
  String get tripComplete => 'Trip complete';

  @override
  String get thanksForUsingService => 'Thank you for using our service';

  @override
  String get distanceUpper => 'DISTANCE';

  @override
  String get durationUpper => 'DURATION';

  @override
  String get confirmVehicleReturned =>
      'Confirm that the driver returned the vehicle';

  @override
  String get sendRatingAndWaitPayment => 'Submit rating & wait for payment';

  @override
  String get confirmTripRateLater => 'Confirm trip & rate later';

  @override
  String get paymentDetails => 'Payment details';

  @override
  String get baseFare => 'Base fare';

  @override
  String get promotion => 'Promotion';

  @override
  String get driverRatingQuestion => 'How was your driver?';

  @override
  String get driverCommentHint => 'Comment about the driver (optional)';

  @override
  String get waitingForPayment => 'Waiting for payment';

  @override
  String get paymentWaitingInstructions =>
      'Scan the QR code on the driver\'s phone, or wait for the driver to confirm a cash payment.';

  @override
  String get cancelReasonPlanChanged => 'Plans changed';

  @override
  String get cancelReasonWaitTooLong => 'Wait time is too long';

  @override
  String get cancelReasonWrongLocation => 'Wrong location selected';

  @override
  String get cancelReasonNoLongerNeeded => 'Driver no longer needed';

  @override
  String get cancelReasonOther => 'Other reason';

  @override
  String get cancelTripQuestion => 'Cancel trip?';

  @override
  String get cancelSearchConfirmation =>
      'Are you sure you want to stop searching for a driver?';

  @override
  String cancelBookingConfirmation(int id) {
    return 'Are you sure you want to cancel trip #$id?';
  }

  @override
  String get cancelReason => 'Cancellation reason';

  @override
  String get confirmCancellation => 'Confirm cancellation';

  @override
  String get goBack => 'No, go back';

  @override
  String get cancelTripFailed => 'Unable to cancel the trip. Please try again.';

  @override
  String get tripCannotBeCancelled =>
      'This trip cannot be cancelled in its current status.';

  @override
  String get tripWaitExpired =>
      'The waiting period expired and the trip was closed.';

  @override
  String get tripCancelledSuccessfully => 'Trip cancelled successfully.';

  @override
  String get scheduledTripCancelledSuccessfully =>
      'Scheduled trip cancelled successfully.';

  @override
  String get rebook => 'Book again';

  @override
  String get noPromotions => 'No promotions available';

  @override
  String remainingUses(int count) {
    return 'Remaining uses: $count';
  }

  @override
  String get promoValidatedOnBooking =>
      'The code will be validated when you book';

  @override
  String get noAvailablePromoCodes =>
      'No promotion codes are currently available.';

  @override
  String get deselectPromo => 'Remove promotion code';

  @override
  String minimumOrder(String amount) {
    return 'Minimum order: $amount';
  }

  @override
  String remainingUseCount(int count) {
    return '$count uses remaining';
  }

  @override
  String get usageExhausted => 'No uses remaining';

  @override
  String get inUse => 'In\nuse';

  @override
  String get useNow => 'Use\nnow';

  @override
  String percentDiscount(num percent) {
    return 'Save $percent%';
  }

  @override
  String maximumDiscount(String amount) {
    return ' (up to $amount)';
  }

  @override
  String fixedDiscount(String amount) {
    return 'Save $amount';
  }

  @override
  String expiresOn(String date) {
    return 'Expires: $date';
  }

  @override
  String minimumOrderShort(String amount) {
    return 'Minimum order $amount';
  }

  @override
  String get exitAppQuestion => 'Exit the app?';

  @override
  String get exitAppDescription => 'Are you sure you want to exit SafeRide?';

  @override
  String get exit => 'Exit';

  @override
  String get activity => 'Activity';

  @override
  String get safeRideAssistant => 'SafeRide Assistant';

  @override
  String get tryAgain => 'Try again';

  @override
  String get activeTripNotice =>
      'You have an active trip. Please track it under Activity.';

  @override
  String get trackingTrip => 'Tracking trip';

  @override
  String get noActiveTripForSos => 'You do not have an active trip for SOS.';

  @override
  String get viewAll => 'View all';

  @override
  String get locatingAddress => 'Locating address...';

  @override
  String get searchPickup => 'Search pickup';

  @override
  String get searchDestination => 'Search destination';

  @override
  String get selectedPickup => 'Selected pickup';

  @override
  String get selectedDestination => 'Selected destination';

  @override
  String get searchOrTapMap => 'Search or tap the map to select a location.';

  @override
  String get confirmPickup => 'Confirm pickup';

  @override
  String get confirmDestination => 'Confirm destination';

  @override
  String get prepayment => 'Prepayment';

  @override
  String get payosPaymentAmount => 'Amount payable via PayOS';

  @override
  String get checkPayment => 'Check payment';

  @override
  String get payAfterTrip => 'Pay after the trip';

  @override
  String get prepaid => 'Prepaid';

  @override
  String get backToTrip => 'Back to trip';

  @override
  String get payosQrCreateFailed => 'Could not create the PayOS QR code.';

  @override
  String get scanQrToPay => 'Scan with your banking app to pay';

  @override
  String get cameraOpenFailed =>
      'Could not open the camera. Please check camera permission.';

  @override
  String get photoCaptureFailed =>
      'Could not take the photo. Please try again.';

  @override
  String get alignDocumentCorners =>
      'Align all four document corners inside the frame';

  @override
  String get submittedInformation => 'Submitted information';

  @override
  String get documentNumber => 'Document number';

  @override
  String get licenseClass => 'License class';

  @override
  String get issueDate => 'Issue date';

  @override
  String get expiryDate => 'Expiry date';

  @override
  String get documents => 'Documents';

  @override
  String get frontSide => 'Front';

  @override
  String get backSide => 'Back';

  @override
  String get submittedFile => 'Submitted file';

  @override
  String get documentApproved => 'Approved';

  @override
  String get documentPendingReview => 'Submitted, pending review';

  @override
  String get documentRejected => 'Rejected';

  @override
  String get documentNotSubmitted => 'Not submitted';

  @override
  String get identityVerification => 'Identity verification';

  @override
  String get completeYourProfile => 'Complete your profile';

  @override
  String get identityVerificationIntro =>
      'To start accepting trips and keep passengers safe, verify your identity and provide the required documents.';

  @override
  String get requiredDocuments => 'Required documents';

  @override
  String get submitApplicationNow => 'Submit application now';

  @override
  String get verificationTime =>
      'Verification usually takes 1–3 business days.';

  @override
  String get previousApplicationRejected => 'Previous application rejected';

  @override
  String get profileStatusLoadFailed =>
      'Could not load your application status. Please try again.';

  @override
  String get idCardOrPassport => 'ID card / Passport';

  @override
  String get frontAndBack => 'Front and back';

  @override
  String get drivingLicense => 'Driving license';

  @override
  String get licensePhotoAndInfo => 'License photo and information';

  @override
  String get criminalRecord => 'Criminal record';

  @override
  String get originalIssuedWithinSixMonths =>
      'Original issued within six months';

  @override
  String get resubmissionRequired => 'Resubmission required';

  @override
  String get submitted => 'Submitted';

  @override
  String get confirmHireDriver => 'Confirm driver';

  @override
  String get hourlyHire => 'Hourly hire';

  @override
  String get tripDetailsHeading => 'Trip details';

  @override
  String get notCreated => 'Not created';

  @override
  String get awaitingConfirmation => 'Awaiting confirmation';

  @override
  String get estimatedDuration => 'Estimated duration';

  @override
  String get updating => 'Updating';

  @override
  String get estimatedTotalPayment => 'Estimated total';

  @override
  String get missingTripToConfirmDriver =>
      'No trip is available to confirm the driver.';

  @override
  String get driverOfferNotFound => 'Driver offer information was not found.';

  @override
  String get confirmDriverFailed =>
      'Could not confirm the driver. Please try again.';

  @override
  String get driverConfirmed => 'Driver confirmed';

  @override
  String driverConfirmedMessage(String driverName, int bookingId) {
    return '$driverName will take trip #$bookingId. Waiting for dispatch...';
  }

  @override
  String get agree => 'OK';

  @override
  String driverRatingSummary(String rating, int tripCount, int years) {
    return '$rating stars • $tripCount trips • $years years';
  }

  @override
  String get confirmDriverNotice =>
      'Check the driver information carefully before confirming.';

  @override
  String get oldTripDataInvalid => 'The previous trip data is invalid.';

  @override
  String get calculatingFarePleaseWait => 'Calculating the fare. Please wait.';

  @override
  String get bookingSuccessful =>
      'Trip booked successfully. Your driver will arrive on time.';

  @override
  String get rebookTrip => 'Book this trip again';

  @override
  String get confirmPreviousInformation => 'Confirm previous details';

  @override
  String get reviewRouteAndVehicle =>
      'Review the route and vehicle for your upcoming trip.';

  @override
  String get departureTime => 'Departure time';

  @override
  String get leaveNow => 'Leave now';

  @override
  String get scheduleAhead => 'Schedule';

  @override
  String get promotionCode => 'Promotion code';

  @override
  String get oldPromoCannotBeReused =>
      'The previous promotion cannot be reused. Choose or enter a new code for this trip.';

  @override
  String get grandTotal => 'Total';

  @override
  String discountApplied(String amount) {
    return '↓ Saved $amount';
  }

  @override
  String get taxesIncluded => 'Taxes and fees included';

  @override
  String get confirmAndFindDriver => 'Confirm & find driver';

  @override
  String get addNewPromoCode => 'Add a new promotion code';

  @override
  String get completePaymentBeforeExit => 'Complete payment before leaving';

  @override
  String get completePayment => 'Please complete the payment.';

  @override
  String get tripPayment => 'Trip payment';

  @override
  String get customerPaymentAmount => 'Amount due from customer';

  @override
  String get paid => 'Paid';

  @override
  String get checkAgain => 'Check again';

  @override
  String get cashConfirmed => 'Cash confirmed';

  @override
  String get customerPaid => 'Customer paid';

  @override
  String get backToHome => 'Back to home';

  @override
  String get paymentQrCreateFailed => 'Could not create the payment QR code.';

  @override
  String get reconfirmCash => 'Confirm cash again';

  @override
  String get recreateQr => 'Create QR again';

  @override
  String get switchPaymentMethod => 'Switch payment method';

  @override
  String get customerScanQr => 'Ask the customer to scan this code';

  @override
  String get cashPaymentConfirmFailed => 'Could not confirm the cash payment.';

  @override
  String get chooseCustomerPaymentMethod =>
      'Choose the customer\'s payment method';

  @override
  String get qrPayment => 'QR payment';

  @override
  String get cashPayment => 'Cash';

  @override
  String get returnVehicleConfirmation => 'Confirm vehicle return';

  @override
  String get returnEvidenceInstruction =>
      'Take or select 1–3 photos proving that the vehicle was returned to the customer.';

  @override
  String get tapToAddPhoto => 'Tap to add a photo';

  @override
  String get optionalNote => 'Note (optional)';

  @override
  String get noteHint => 'Add a note if needed...';

  @override
  String get submitting => 'Submitting...';

  @override
  String get returnConfirmedSuccess => 'Confirmation successful';

  @override
  String get returnConfirmedMessage =>
      'Vehicle return recorded. The trip is being completed.';

  @override
  String get done => 'Done';

  @override
  String get minimumEvidencePhoto => 'At least one evidence photo is required.';

  @override
  String get maximumEvidencePhotos => 'You can upload up to three photos.';

  @override
  String get evidenceUploadFailed =>
      'Could not submit the evidence. Try again.';

  @override
  String get takePhoto => 'Take photo';

  @override
  String get chooseFromGallery => 'Choose from gallery';

  @override
  String get removePhoto => 'Remove photo';

  @override
  String get removePhotoQuestion => 'Remove this photo?';

  @override
  String photoNumber(int number) {
    return 'Photo $number';
  }

  @override
  String photoCount(int count, int max) {
    return '$count / $max photos';
  }

  @override
  String remainingPhotos(int count) {
    return '$count photos remaining';
  }

  @override
  String submitEvidenceWithCount(int count) {
    return 'Confirm return ($count photos)';
  }

  @override
  String mediaAccessFailed(String source) {
    return 'Could not access $source.';
  }

  @override
  String get camera => 'camera';

  @override
  String get gallery => 'gallery';

  @override
  String get myWallet => 'My wallet';

  @override
  String get availableBalance => 'AVAILABLE BALANCE';

  @override
  String get withdraw => 'Withdraw';

  @override
  String get topUp => 'Top up';

  @override
  String get income => 'Income';

  @override
  String get day => 'Day';

  @override
  String get week => 'Week';

  @override
  String get month => 'Month';

  @override
  String totalIncomeForPeriod(String period) {
    return 'Total income\n$period';
  }

  @override
  String get recentTransactions => 'Recent transactions';

  @override
  String get bankListLoadFailed => 'Could not load the bank list.';

  @override
  String get withdrawalRequestSent => 'Withdrawal request submitted.';

  @override
  String get withdrawalRequestFailed =>
      'Could not submit the withdrawal request.';

  @override
  String get withdrawToBank => 'Withdraw to bank';

  @override
  String get bankInfoWillBeSaved =>
      'This information will be saved for your next withdrawal.';

  @override
  String get lastBankPreFilled =>
      'Your most recent account has been pre-filled.';

  @override
  String get selectBankRequired => 'Please select a bank';

  @override
  String get bank => 'Bank';

  @override
  String get searchAndSelectBank => 'Search and select a bank';

  @override
  String get accountNumber => 'Account number';

  @override
  String get invalidAccountNumber => 'Invalid account number';

  @override
  String get accountHolderName => 'Account holder name';

  @override
  String get accountHolderRequired => 'Enter the account holder name';

  @override
  String get withdrawalAmount => 'Withdrawal amount';

  @override
  String minimumWithdrawal(String amount) {
    return 'Minimum withdrawal is $amount';
  }

  @override
  String get confirmWithdrawal => 'Confirm withdrawal';

  @override
  String get selectBank => 'Select bank';

  @override
  String get searchBankHint => 'Search by name, code, or BIN';

  @override
  String get bankNotFound => 'No bank found.';

  @override
  String get noTransactions => 'No transactions yet.';

  @override
  String get today => 'today';

  @override
  String get thisMonth => 'this month';

  @override
  String get thisWeek => 'this week';

  @override
  String get noPreviousPeriodData => 'No data for the\nprevious period';

  @override
  String periodComparison(String value) {
    return '$value% vs. the\nprevious period';
  }

  @override
  String get completed => 'Completed';

  @override
  String get home => 'Home';

  @override
  String get account => 'Account';

  @override
  String get wallet => 'Wallet';

  @override
  String get destinationQuestion => 'Where would you like to go today?';

  @override
  String get bookNow => 'Book now';

  @override
  String get bookNowDescription => 'Find the right driver for your trip';

  @override
  String get scheduleBooking => 'Schedule a trip';

  @override
  String get history => 'History';

  @override
  String get myVehiclesShort => 'My vehicles';

  @override
  String get promotions => 'Promotions';

  @override
  String get sos => 'Emergency SOS';

  @override
  String get recentTrips => 'Recent trips';

  @override
  String get friendlyUser => 'there';

  @override
  String greeting(String name) {
    return 'Hello $name,';
  }

  @override
  String get sampleRecentPickup => '123 Nguyen Van Linh, District 7';

  @override
  String get sampleRecentDestination => 'Tan Son Nhat Airport';

  @override
  String get sampleRecentTime => 'Yesterday, 14:30';

  @override
  String get driverProfile => 'Driver profile';

  @override
  String tripCountPlus(String count) {
    return '$count+ trips';
  }

  @override
  String get kycStatus => 'KYC status';

  @override
  String get kycApprovedDescription => 'Profile approved by the system';

  @override
  String get cleanCriminalRecord => 'Clear and transparent record';

  @override
  String get confirmHire => 'Confirm hire';

  @override
  String get rejectAndFindAnotherDriver => 'Reject and find another driver';

  @override
  String get rejectDriverQuestion => 'Reject this driver?';

  @override
  String get rejectDriverDescription =>
      'The system will skip this driver and continue searching for another one.';

  @override
  String get findingAnotherDriver => 'Finding another driver for you...';

  @override
  String get rejectDriverFailed => 'Could not reject the driver.';

  @override
  String get experienceUpper => 'EXPERIENCE';

  @override
  String yearsValueCapitalized(int years) {
    return '$years Years';
  }

  @override
  String get safeDriving => 'Safe driving';

  @override
  String get friendly => 'Friendly';

  @override
  String get verified => 'Verified';

  @override
  String get idCardFront => 'ID card front';

  @override
  String get idCardBack => 'ID card back';

  @override
  String get idCardCameraInstruction =>
      'Place the entire ID card inside the frame with good lighting and sharp focus.';

  @override
  String get idCardScanned => 'ID card information scanned.';

  @override
  String get ocrScanFailed => 'Could not read this image with OCR.';

  @override
  String get stepOneOfThree => 'Step 1/3';

  @override
  String get uploadIdCard => 'Upload ID card';

  @override
  String get captureIdCard => 'Capture ID card';

  @override
  String get idCardUploadInstruction =>
      'Provide clear front and back images of your ID card without glare or cropped corners.';

  @override
  String get fullName => 'Full name';

  @override
  String get idCardNameHint => 'Enter the name shown on the ID card';

  @override
  String get idCardNumber => 'ID card number';

  @override
  String get idCardNumberHint => 'Enter the ID card number';

  @override
  String get continueAction => 'Continue';

  @override
  String get idCardFieldsRequired =>
      'Capture both sides and verify the full name and ID card number.';

  @override
  String get idCardPhotoTip =>
      'Tip: Place the ID card on a dark flat surface with sufficient natural light for the best result.';

  @override
  String get ocrScanningOnDevice => 'Scanning with on-device OCR...';

  @override
  String get idCardOcrFilled => 'OCR filled in the ID card information';

  @override
  String get tapToCaptureOrUpload => 'Tap to capture or upload';

  @override
  String get licenseFront => 'License front';

  @override
  String get licenseBack => 'License back';

  @override
  String get licenseCameraInstruction =>
      'Place the entire license inside the frame with good lighting and sharp focus.';

  @override
  String get ocrMlKitScanned => 'Scanned with Google ML Kit OCR.';

  @override
  String get licenseOcrFailed => 'Could not read this license image with OCR.';

  @override
  String get licenseType => 'License type';

  @override
  String get licensePhotos => 'Driver\'s license photos';

  @override
  String get licenseNameHint => 'Enter the name shown on the license';

  @override
  String get licenseNumber => 'License number';

  @override
  String get licenseNumberHint => 'Enter the license number';

  @override
  String get selectLicenseClass => 'Select license class';

  @override
  String get unlimited => 'No expiry';

  @override
  String get licenseNoExpiry => 'This license does not expire';

  @override
  String get idAndLicenseNameMismatch =>
      'The names on the ID card and driver\'s license do not match.';

  @override
  String get stepTwoOfThree => 'Step 2/3';

  @override
  String get uploadLicense => 'Upload driver\'s license';

  @override
  String get licenseOcrFilled => 'OCR filled in the license information';

  @override
  String get criminalRecordInstruction =>
      'Provide a criminal record certificate issued within the last six months to help ensure passenger safety.';

  @override
  String get reviewWithinHours =>
      'Your application will be reviewed within 24–48 business hours.';

  @override
  String get submittingApplication => 'Submitting application...';

  @override
  String get completeAndSubmit => 'Complete & submit';

  @override
  String get stepThreeOfThree => 'Step 3/3';

  @override
  String get uploadCriminalRecord => 'Upload criminal record';

  @override
  String get uploadRequirements => 'Upload requirements';

  @override
  String get clearNoGlare => 'A clear photo without glare.';

  @override
  String get allFourCorners =>
      'All four corners of the document must be visible.';

  @override
  String get supportedDocumentFormats =>
      'Supported formats: JPG, PNG, PDF (maximum 10 MB).';

  @override
  String get tapToUploadDocument =>
      'Tap to upload or drag and drop a file here';

  @override
  String get photoOrPdfSupported =>
      'Photos and scanned PDF files are supported';

  @override
  String get chooseDocument => 'Choose document';

  @override
  String get documentSelected => 'Document selected';

  @override
  String get change => 'Change';

  @override
  String get criminalRecordOcrRead => 'OCR read the criminal record content';

  @override
  String get criminalRecordScanned => 'Criminal record scanned with OCR.';

  @override
  String get documentOcrFailed => 'Could not read this document with OCR.';

  @override
  String get applicationSubmitted => 'Application submitted!';

  @override
  String get applicationProcessing =>
      'Your application is being processed. We will notify you of the result soon.';

  @override
  String get applicationSubmitFailed =>
      'Could not submit the application. Please try again.';

  @override
  String tripEndedWithId(int id) {
    return 'Trip #$id has ended.';
  }

  @override
  String get searchingDriver => 'Finding a driver for you...';

  @override
  String get cancelling => 'Cancelling...';

  @override
  String get cancelBooking => 'Cancel trip';

  @override
  String remainingCountdown(String message, String countdown) {
    return '$message - $countdown remaining';
  }

  @override
  String get estimatedWaitTime => 'Estimated wait: ~2 minutes';

  @override
  String tripCodeWithStatus(int id, String status) {
    return 'Trip #$id • $status';
  }

  @override
  String secondsRemaining(int seconds) {
    return '$seconds seconds remaining';
  }

  @override
  String get suitableDriverReady => 'A suitable driver is ready';

  @override
  String reviewProfileAndConfirm(String countdown) {
    return 'Review the profile and confirm$countdown.';
  }

  @override
  String get viewProfile => 'View profile';

  @override
  String get waitingDriverAccept => 'Waiting for a driver to accept';

  @override
  String get appliedCode => 'Applied code';

  @override
  String promotionWithCode(String code) {
    return 'Promotion ($code):';
  }

  @override
  String currentLocationFailed(String error) {
    return 'Could not get your current location: $error';
  }

  @override
  String get callUnavailableSessionExpired =>
      'Calls are unavailable because the session has expired.';

  @override
  String get customer => 'Customer';

  @override
  String get incomingCall => 'Incoming call';

  @override
  String get customerCalling => 'The customer is calling you.';

  @override
  String get decline => 'Decline';

  @override
  String get answer => 'Answer';

  @override
  String onlineLocationFailed(String error) {
    return 'Could not get your location or go online: $error';
  }

  @override
  String get chatUnavailable => 'Chat is unavailable right now.';

  @override
  String get gpsSimulationEnabled => 'Backend GPS simulation enabled';

  @override
  String get gpsSimulationDisabled => 'GPS simulation disabled; using real GPS';

  @override
  String get activeTrip => 'Active trip';

  @override
  String get message => 'Message';

  @override
  String get callCustomer => 'Call customer';

  @override
  String get processing => 'Processing...';

  @override
  String get startPickup => 'Start pickup';

  @override
  String get driverArrived => 'Arrived at pickup';

  @override
  String get startTrip => 'Start trip';

  @override
  String get endTrip => 'End trip';

  @override
  String get waitingCustomerReturnConfirmation =>
      'Waiting for the customer to confirm vehicle return.\nIf they do not respond, you can confirm it for them.';

  @override
  String get confirmReturnWithEvidence => 'Confirm with evidence photos';

  @override
  String get returnConfirmedCompleting =>
      'Vehicle return confirmed. Completing the trip...';

  @override
  String get returnConfirmedPaymentRequired =>
      'Vehicle return confirmed. Confirm payment to complete the trip.';

  @override
  String get confirmPayment => 'Confirm payment';

  @override
  String get statusAccepted => 'Trip accepted';

  @override
  String get statusArrived => 'Arrived at pickup';

  @override
  String get waitingReturnConfirmation => 'Waiting for return confirmation';

  @override
  String get returnConfirmedStatus => 'Vehicle return confirmed';

  @override
  String get tripStatusUpdateFailed => 'Could not update the trip status.';

  @override
  String get todayIncomeUpper => 'TODAY\'S INCOME';

  @override
  String tripCountShort(int count) {
    return '$count trips';
  }

  @override
  String get waitingConfirmation => 'Waiting for confirmation';

  @override
  String get waitingCustomerDriverConfirmation =>
      'Waiting for the customer to confirm the driver. Keep the app open.';

  @override
  String get newTripAvailable => 'You have a new trip!';

  @override
  String get expectedIncomeUpper => 'EXPECTED INCOME';

  @override
  String get pickupCustomerUpper => 'PICK UP CUSTOMER';

  @override
  String get pickupPointA => 'Pickup point (A)';

  @override
  String get destinationPointB => 'Destination (B)';

  @override
  String get accept => 'Accept';

  @override
  String get selectPickupDate => 'Select pickup date';

  @override
  String get selectPickupTimeHelp => 'Select pickup time';

  @override
  String get invalidSchedule =>
      'Scheduled pickup must be at least 30 minutes from now.';

  @override
  String get selectPickupRequired => 'Please select a pickup point.';

  @override
  String get selectServiceAndVehicle => 'Please select a service and vehicle.';

  @override
  String get selectDestinationRequired => 'Please select a destination.';

  @override
  String get selectPickupTimeRequired => 'Please select a pickup time.';

  @override
  String get fareEstimateUnavailable =>
      'No fare estimate is available. Check the route and try again.';

  @override
  String get bookingFailed => 'Could not book the trip. Please try again.';

  @override
  String get bookingSuccess => 'Trip booked successfully';

  @override
  String get addVehicleFailed => 'Could not add the vehicle. Please try again.';

  @override
  String get vehicleAdded => 'New vehicle added.';

  @override
  String get selectYourVehicle => 'Select your vehicle';

  @override
  String get loadingServices => 'Loading services...';

  @override
  String get specialRequest => 'Special request (optional)';

  @override
  String get fareCalculationNote =>
      'The accepted fare is locked when you book the trip.';

  @override
  String get confirmScheduled => 'Confirm scheduled trip';

  @override
  String get confirmHourlyHire => 'Confirm hourly hire';

  @override
  String get confirmNow => 'Confirm now';

  @override
  String get selectPickup => 'Select pickup';

  @override
  String get selectDestination => 'Select destination';

  @override
  String get calculatingFare => 'Calculating estimated fare...';

  @override
  String hoursValue(int hours) {
    return '$hours hours';
  }

  @override
  String surgePricing(num multiplier) {
    return 'Higher demand pricing (x$multiplier)';
  }

  @override
  String estimatedRentalHours(int hours) {
    return 'Estimated rental: $hours hours';
  }

  @override
  String get addPromoCode => 'Add promotion code';

  @override
  String get tripService => 'Per trip';

  @override
  String get hourlyService => 'Hourly';

  @override
  String get addNewVehicle => 'Add new vehicle';

  @override
  String get saveVehicleAndContinue =>
      'Save the vehicle to your account and continue booking.';

  @override
  String get add => 'Add';

  @override
  String plateNumberLabel(String value) {
    return 'Plate: $value';
  }

  @override
  String vehicleColorLabel(String value) {
    return 'Color: $value';
  }

  @override
  String get noBookableVehicles =>
      'You do not have an eligible vehicle. Add one before booking.';

  @override
  String get mapsConfigMissing =>
      'The map is not configured. Please try again later.';

  @override
  String get serverDisconnectedRetrying =>
      'Server connection lost. Reconnecting...';

  @override
  String get tripCancelled => 'The trip was cancelled.';

  @override
  String get driverLocationTrackingRetrying =>
      'Could not connect to driver location tracking. Retrying...';

  @override
  String get safetyCheck => 'Safety check';

  @override
  String get safetyConfirmed => 'SafeRide recorded that you are safe.';

  @override
  String get iAmSafe => 'I\'m safe';

  @override
  String get callDriver => 'Call driver';

  @override
  String get activateSosQuestion => 'Activate emergency SOS?';

  @override
  String get activateSosDescription =>
      'Send an emergency signal for this trip?';

  @override
  String get activateSos => 'Activate emergency SOS';

  @override
  String get sosActivationFailed => 'Could not activate SOS. Please try again.';

  @override
  String get sosLocationFailed =>
      'Could not get your current location for SOS.';

  @override
  String get emergencyHelpMessage => 'I need emergency assistance';

  @override
  String get sosActivatedForTrip => 'SOS has been activated for this trip.';

  @override
  String get sosActivatedHelpComing =>
      'SOS activated. Help will be provided as soon as possible.';

  @override
  String get driverAtPickup => 'Driver has arrived at pickup';

  @override
  String get waitingDriverPayment => 'Waiting for driver payment';

  @override
  String driverArrivingMinutes(int minutes) {
    return 'Driver arriving • $minutes min';
  }

  @override
  String movingMinutes(int minutes) {
    return 'On the way • $minutes min';
  }

  @override
  String get onCorrectRoute => 'You are on the correct route';

  @override
  String get safeRideDriverName => 'SafeRide Driver';

  @override
  String get updatingVehicle => 'Updating vehicle';

  @override
  String get prepayWithPayos => 'Prepay with PayOS';

  @override
  String get call => 'Call';

  @override
  String get share => 'Share';

  @override
  String get payDriverToComplete => 'Pay the driver to complete the trip.';

  @override
  String get endingTrip => 'Ending trip...';

  @override
  String get tripNotReadyForPayment => 'The trip is not ready for payment.';

  @override
  String get tripNotReadyForChat => 'The trip is not ready for chat.';

  @override
  String get chatAccountUnknown => 'Could not identify the account for chat.';

  @override
  String get tripNotReadyForCall =>
      'Calls are unavailable until the trip is ready.';

  @override
  String driverCalling(String driverName) {
    return '$driverName is calling you.';
  }

  @override
  String get tripCannotEndNow => 'The trip cannot be ended right now.';

  @override
  String get tripEndFailed => 'Could not end the trip. Please try again.';

  @override
  String get sosActivated => 'Emergency SOS activated';

  @override
  String get sendingSos => 'Sending emergency SOS...';

  @override
  String get shareRoute => 'Share route';

  @override
  String get shareRouteDescription =>
      'Send the link below to family or friends so they can track your trip in real time.';

  @override
  String get linkCopied => 'Link copied';

  @override
  String get close => 'Close';

  @override
  String get enableLocationForPickup =>
      'Enable location so SafeRide can use GPS as your pickup point.';

  @override
  String get microphonePermissionRequired =>
      'Allow SafeRide to use your microphone.';

  @override
  String get voiceMessage => 'Voice message';

  @override
  String get currentGpsUnavailable =>
      'Could not get your current GPS location. Enable location and try again.';

  @override
  String get audioUploadFailed =>
      'Could not upload the recording. Please try again.';

  @override
  String get aiAssistantUnavailable =>
      'The AI assistant is unavailable. Please try again later.';

  @override
  String get aiAssistantConnectionFailed =>
      'Could not connect to the AI assistant. Please try again.';

  @override
  String get aiBookingFailed => 'Could not book the trip.';

  @override
  String get conversationOpenFailed => 'Could not open the conversation.';

  @override
  String get recording => 'Recording...';

  @override
  String get sendOrCancelRecording => 'Send or cancel the recording';

  @override
  String get aiMessageHint => 'Message the SafeRide assistant...';

  @override
  String get cancelVoice => 'Cancel voice';

  @override
  String get sendVoice => 'Send voice';

  @override
  String get voiceInput => 'Voice input';

  @override
  String vehicleSelectedByQuery(String query) {
    return 'Selected the vehicle matching “$query”.';
  }

  @override
  String vehicleQueryNotFound(String query) {
    return 'No exact vehicle match for “$query”. Select one again.';
  }

  @override
  String promoApplied(String code) {
    return 'Applied code $code.';
  }

  @override
  String promoUnavailable(String code) {
    return 'Code $code is unavailable.';
  }

  @override
  String get conversationHistoryLoadFailed =>
      'Could not load conversation history.';

  @override
  String get deleteConversationQuestion => 'Delete conversation?';

  @override
  String deleteConversationDescription(String title) {
    return '“$title” and its audio files will be permanently deleted.';
  }

  @override
  String get conversationDeleteFailed =>
      'Could not delete the conversation. Please try again.';

  @override
  String get conversationHistory => 'Conversation history';

  @override
  String get noConversations => 'No conversations yet.';

  @override
  String get deleteConversation => 'Delete conversation';

  @override
  String get safeRideAssistantTitle => 'SafeRide Assistant';

  @override
  String get aiDisclaimer => 'AI can make mistakes • Check before booking';

  @override
  String get newChat => 'New chat';

  @override
  String get back => 'Back';

  @override
  String get chooseVehicleQuestion => 'Which vehicle would you like to use?';

  @override
  String get chooseDiscountCode => 'Choose a discount code';

  @override
  String get confirmTrip => 'Confirm trip';

  @override
  String get yourVehicles => 'Your vehicles';

  @override
  String get newVehicle => 'New vehicle';

  @override
  String get noVehicleForAiBooking =>
      'You do not have a vehicle yet. Add one to continue booking.';

  @override
  String get continueChooseDiscount => 'Continue to discount codes';

  @override
  String get noDiscountAvailable =>
      'No discount codes are currently available.';

  @override
  String get noDiscount => 'Do not use a discount code';

  @override
  String get continueWithoutDiscount => 'Continue without a code';

  @override
  String usePromoCode(String code) {
    return 'Use code $code';
  }

  @override
  String get notUsed => 'Not used';

  @override
  String get confirmAndFindDriverAi => 'Confirm and find driver';

  @override
  String get aiWelcome =>
      'Hello! I can help you use SafeRide or prepare a trip.\n\nFor example: “Book a ride from FPT University to Tan Son Nhat Airport”.';

  @override
  String get slogan => 'Safe journeys you can trust';

  @override
  String get phoneNumber => 'Phone number';

  @override
  String get phoneHint => 'Enter phone number';

  @override
  String get continueOrRegister => 'Continue / Sign up';

  @override
  String get phoneRequired => 'Enter your phone number';

  @override
  String get invalidPhone => 'Invalid phone number';

  @override
  String get sendOtpFailed =>
      'Could not send the OTP. Check the phone number and try again.';

  @override
  String get or => 'OR';

  @override
  String get googleLoginFailed => 'Google sign-in failed';

  @override
  String get continueAgreement => 'By continuing, you agree to our ';

  @override
  String get and => ' and ';

  @override
  String get agreementSuffix => '.';

  @override
  String get otpTitle => 'OTP verification';

  @override
  String get resendAfter => 'Resend in ';

  @override
  String get resendOtp => 'Resend OTP';

  @override
  String get otpResent => 'OTP resent.';

  @override
  String get resendOtpFailed => 'Could not resend the OTP.';

  @override
  String get otpRequired => 'Enter all 6 OTP digits';

  @override
  String get invalidOtp => 'The OTP is incorrect or expired';

  @override
  String get otpLockedPrefix => 'Too many incorrect attempts. Try again in ';

  @override
  String get otpAttemptsExceeded =>
      'Too many incorrect OTP attempts. Request a new code.';

  @override
  String otpDescription(String phoneNumber) {
    return 'Enter the 6-digit code sent to\n$phoneNumber.';
  }

  @override
  String get welcome => 'Welcome!';

  @override
  String get selectRoleQuestion => 'Which role would you like to start with?';

  @override
  String get customerRoleTitle => 'I\'m a Customer';

  @override
  String get customerRoleDescription =>
      'Book safe rides quickly and track trips live.';

  @override
  String get driverRoleTitle => 'I\'m a Driver';

  @override
  String get driverRoleDescription =>
      'Work flexibly, increase your income, and manage trips easily.';

  @override
  String get rememberRole => 'Remember my choice';

  @override
  String get completeProfile => 'Complete profile';

  @override
  String get changeAvatar => 'Change profile photo';

  @override
  String get verifiedPhone => 'Verified phone number';

  @override
  String get updateInformationHint =>
      'Update your personal information to continue.';

  @override
  String get email => 'Email';

  @override
  String get saving => 'Saving...';

  @override
  String get saveAndContinue => 'Save and continue';

  @override
  String get uploadAvatarFailed => 'Could not upload the profile photo.';

  @override
  String get updateProfileFailed => 'Could not update your information.';

  @override
  String get invalidFullName => 'Enter a valid full name.';

  @override
  String get invalidEmail => 'Invalid email address.';

  @override
  String get emailAlreadyUsed =>
      'This email is already used by another account.';

  @override
  String get phoneNumberAlreadyUsed =>
      'This phone number is already used by another account.';

  @override
  String get phoneNumberChangeRequiresVerification =>
      'The linked phone number cannot be changed on this screen.';

  @override
  String get phoneVerificationRequired =>
      'Verify the OTP before adding a phone number.';

  @override
  String get appVersion => 'App version: 2.4.1';

  @override
  String get linkGoogleFailed => 'Could not link Google.';

  @override
  String get unlinkGoogleQuestion => 'Unlink Google?';

  @override
  String get unlinkGoogleDescription =>
      'You can still sign in with your verified phone number.';

  @override
  String get unlinkAccount => 'Unlink';

  @override
  String get unlinkGoogleFailed => 'Could not unlink Google.';

  @override
  String get logoutFailed => 'Could not sign out. Please try again.';

  @override
  String get historyFilterAll => 'All';

  @override
  String get historyFilterCancelled => 'Cancelled';

  @override
  String get historyFilterBooked => 'Booked';

  @override
  String get cancelledByCustomer => 'Cancelled by customer';

  @override
  String get reported => 'Reported';

  @override
  String get report => 'Report';

  @override
  String get aiConversationFallback => 'Conversation';

  @override
  String get chatConnectionFailed => 'Unable to connect to chat.';

  @override
  String get chatMessageSendFailed => 'Unable to send the message.';

  @override
  String get chatImageSendFailed => 'Unable to send the image.';

  @override
  String get routeUpdated => 'SafeRide updated the route.';

  @override
  String get newTripMessage => 'You have a new trip.';

  @override
  String get noInternetConnection => 'No internet connection';

  @override
  String get connectionLost => 'Connection lost';

  @override
  String get internetRestored => 'Internet connection restored';

  @override
  String get backOnline => 'Back online';

  @override
  String get calculating => 'Calculating';

  @override
  String get viewTripAfterAccept => 'Open trip details after accepting';

  @override
  String get customerCancelledDriverRequest =>
      'The customer cancelled the driver request.';

  @override
  String get onlineFailed => 'Unable to go online. Please try again.';

  @override
  String get acceptTripFailed => 'Unable to accept the trip. Please try again.';

  @override
  String get declineTripFailed =>
      'Unable to decline the trip. Please try again.';

  @override
  String get tripRequestsLoadFailed =>
      'Unable to load trip requests. Please try again.';

  @override
  String get noDestination => 'No destination yet';

  @override
  String get expiresSoon => 'Expiring soon';

  @override
  String get evidencePhotoCountError => 'Provide 1 to 3 evidence photos.';

  @override
  String get activeTripLoadFailed =>
      'Unable to load the current trip. Please try again.';

  @override
  String ratingStars(int count) {
    return '$count stars';
  }

  @override
  String get demoGpsMode => 'GPS simulation mode';

  @override
  String get serviceDisabled => 'Enable location services on your device.';

  @override
  String get permissionRequired =>
      'SafeRide needs location permission to determine the pickup point.';

  @override
  String get locationNotFound => 'No matching location found.';

  @override
  String get destinationRequired => 'Enter a destination.';

  @override
  String get statusLabel => 'Status';

  @override
  String get selectPromotion => 'Select a promotion';

  @override
  String get enterPromoCode => 'Enter promotion code';

  @override
  String get apply => 'Apply';

  @override
  String get expired => 'Expired';

  @override
  String get statusOnline => 'Online';

  @override
  String get statusOffline => 'Offline';

  @override
  String get statusBusy => 'On a trip';

  @override
  String get offerSent => 'Sent to driver';

  @override
  String get offerRejected => 'Rejected';

  @override
  String get offerCustomerConfirmed => 'Customer confirmed';

  @override
  String get preTripSafetyTitle => 'Pre-trip vehicle safety check';

  @override
  String get preTripSafetyDescription =>
      'Confirm every item before starting. Failed attempts remain in the audit history.';

  @override
  String get brakeResponse => 'Brake response';

  @override
  String get frontRearLights => 'Front and rear lights';

  @override
  String get turnSignals => 'Turn signals';

  @override
  String get visibleTires => 'Visible tire condition';

  @override
  String get dashboardWarning => 'No dashboard warning';

  @override
  String get windshieldVisibility => 'Clear windshield and mirrors';

  @override
  String get noMajorVisibleIssue => 'No major visible issue';

  @override
  String get confirmSafetyCheck => 'Confirm safety check';

  @override
  String get allChecksRequired =>
      'All safety items must pass before starting the trip.';

  @override
  String get safetyTermination => 'End for safety';

  @override
  String get safetyTerminationDescription =>
      'The trip remains cancelled. Promotion will not be used and partial fare may apply after the trip starts.';

  @override
  String get safetyTerminationReasonHint => 'Describe the safety risk';

  @override
  String get captureSafetyEvidence => 'Capture evidence photo (optional)';

  @override
  String get retakePhoto => 'Retake';

  @override
  String get reportAccident => 'Report accident';

  @override
  String get accidentDescriptionHint =>
      'Describe what happened and any immediate damage';

  @override
  String get createAccidentReport => 'Create report';

  @override
  String get accidentReported => 'Accident report created.';

  @override
  String get safetyTerminationFailed => 'Could not end the trip for safety.';

  @override
  String get preTripCheckFailed => 'Could not submit the safety check.';

  @override
  String get riskProtectionCaseTitle => 'Accident protection case';

  @override
  String get riskProtectionClaim => 'Protection claim';

  @override
  String get riskProtectionEvidence => 'Evidence';

  @override
  String get riskProtectionAssessment => 'Liability assessment';

  @override
  String get uploadAccidentEvidence => 'Add evidence photo';

  @override
  String get sendEvidencePhoto => 'Send photo';

  @override
  String get evidencePreviewFailed =>
      'Could not read the selected image. Please choose it again.';

  @override
  String get disputeLiability => 'Request liability review';

  @override
  String get disputeReasonHint =>
      'Explain why the assessment should be reviewed';

  @override
  String get liabilityDisputed => 'Your review request was submitted.';

  @override
  String get accidentEvidenceUploaded => 'Evidence photo sent.';

  @override
  String get noAccidentEvidence => 'No evidence has been uploaded.';

  @override
  String get noProtectionClaim => 'The protection claim has not been created.';

  @override
  String get driverLiabilities => 'My liabilities';

  @override
  String get noDriverLiabilities => 'You have no confirmed driver liability.';

  @override
  String get confirmedAmount => 'Confirmed amount';

  @override
  String get paidAmount => 'Paid amount';

  @override
  String get outstandingAmount => 'Outstanding amount';

  @override
  String get attributableDamage => 'Driver-attributable eligible damage';

  @override
  String get recoveryHistory => 'Recovery history';

  @override
  String get claimStatus => 'Claim status';

  @override
  String get insuranceCoverage => 'Insurance coverage';

  @override
  String get riskFundCoverage => 'Risk Fund coverage';

  @override
  String get participantLiabilities => 'Participant liabilities';

  @override
  String get accidentStatus => 'Accident status';

  @override
  String get accidentCategory => 'Accident category';

  @override
  String get accidentOccurredAt => 'Occurred at';

  @override
  String get safetyReportTitle => 'Report safety incident';

  @override
  String get unsafeCustomer => 'Unsafe customer';

  @override
  String get vehicleIssue => 'Vehicle issue';

  @override
  String get safetyReasonCode => 'Reason';

  @override
  String get safetyReportDescription => 'Describe the incident';

  @override
  String get requestSosEscalation => 'Request SOS escalation';

  @override
  String get requestSosEscalationHint =>
      'Send the current location and create a durable SOS alert';

  @override
  String get safetyReportSubmitted => 'Safety incident report submitted.';

  @override
  String get safetyReportFailed =>
      'Could not submit the safety incident report. Please try again.';

  @override
  String get vehicleFaultType => 'Vehicle fault type';

  @override
  String get otherVehicleFault => 'Other vehicle fault';

  @override
  String get optionalEvidence => 'Evidence (optional)';

  @override
  String get vehicleInsurance => 'Insurance';

  @override
  String get addInsurance => 'Add insurance';

  @override
  String get insuranceLoadFailed =>
      'Could not load insurance information. Please try again.';

  @override
  String get insuranceUpdateFailed => 'Could not update insurance.';

  @override
  String get deleteInsuranceQuestion => 'Delete insurance policy?';

  @override
  String get policyNumber => 'Policy number';

  @override
  String get optionalInsuranceEmpty =>
      'Insurance is optional. This vehicle has no policies.';

  @override
  String get addInsurancePolicy => 'Add insurance policy';

  @override
  String get editInsurancePolicy => 'Edit insurance policy';

  @override
  String get insuranceType => 'Insurance type';

  @override
  String get mandatoryTplInsurance => 'Mandatory third-party liability';

  @override
  String get physicalDamageInsurance => 'Physical damage';

  @override
  String get insuranceProvider => 'Provider';

  @override
  String get effectiveDate => 'Effective date';

  @override
  String get insuranceCoverageLimit => 'Coverage limit';

  @override
  String get insuranceDeductible => 'Deductible';

  @override
  String get optionalDocumentUrl => 'Document URL (optional)';

  @override
  String get optionalInsuranceHint =>
      'Insurance is optional. Creating or editing a policy resets it to PENDING for Staff verification.';

  @override
  String get endTripReasonTitle => 'Reason for ending trip';

  @override
  String get endTripReasonDescription =>
      'Choose the accurate reason. Safety endings must use the separate Risk Protection flow.';

  @override
  String get normalCompletionReason => 'Destination reached';

  @override
  String get normalCompletionReasonDescription => 'Uses the booked fare.';

  @override
  String get customerRequestedStopReason => 'Customer requested early stop';

  @override
  String get customerRequestedStopReasonDescription =>
      'Uses booked-route progress and the minimum service fare.';

  @override
  String get driverUnableToContinueReason => 'Driver cannot continue';

  @override
  String get startedByMistakeReason => 'Trip started by mistake';

  @override
  String get riskStatusReported => 'Reported';

  @override
  String get riskStatusEvidenceCollection => 'Collecting evidence';

  @override
  String get riskStatusUnderReview => 'Under review';

  @override
  String get riskStatusLiabilityPending => 'Awaiting responsibility assessment';

  @override
  String get riskStatusSettlement => 'Processing protection outcome';

  @override
  String get riskStatusClosed => 'Closed';

  @override
  String get riskStatusRejected => 'Rejected';

  @override
  String get riskCategoryDriverInjury => 'Driver injury';

  @override
  String get riskCategoryCustomerVehicleDamage => 'Customer vehicle damage';

  @override
  String get riskCategoryThirdPartyDamage => 'Third-party damage';

  @override
  String get riskCategoryMultiple => 'Multiple damage types';

  @override
  String get riskFaultNoFault => 'No fault';

  @override
  String get riskFaultOrdinary => 'Ordinary negligence';

  @override
  String get riskFaultGross => 'Gross negligence';

  @override
  String get riskFaultIntentional => 'Intentional misconduct';

  @override
  String get riskAssessmentDraft => 'Draft';

  @override
  String get riskAssessmentPendingConfirmation => 'Awaiting confirmation';

  @override
  String get riskAssessmentConfirmed => 'Confirmed';

  @override
  String get riskAssessmentDisputed => 'Under reconsideration';

  @override
  String get riskClaimApproved => 'Approved';

  @override
  String get riskClaimPendingFunding => 'Awaiting funding';

  @override
  String get riskClaimFunded => 'Funded';

  @override
  String get riskClaimRecovery => 'Recovery in progress';

  @override
  String get riskClaimSettled => 'Reconciled';

  @override
  String get riskLiabilityPartiallyPaid => 'Partially paid';

  @override
  String get riskLiabilityPaid => 'Paid';

  @override
  String get riskLiabilityWaived => 'Waived';

  @override
  String get riskRoleDriver => 'Driver';

  @override
  String get riskRoleCustomer => 'Customer';

  @override
  String get riskRoleThirdParty => 'Third party';

  @override
  String get riskRoleVehicle => 'Vehicle';

  @override
  String get riskRoleObjective => 'Objective factor';

  @override
  String get riskReasonDistracting => 'Causing distraction';

  @override
  String get riskReasonViolent => 'Violent behavior';

  @override
  String get riskReasonInterferingVehicle => 'Interfering with vehicle control';

  @override
  String get riskReasonUnsafeRequest => 'Unsafe request';

  @override
  String get riskReasonOther => 'Other reason';

  @override
  String get riskInsurancePending => 'Awaiting Staff verification';

  @override
  String get riskInsuranceVerified => 'Verified';

  @override
  String get riskInsuranceExpired => 'Expired';

  @override
  String get riskInsuranceOther => 'Other insurance';

  @override
  String get riskIncidentInformation => 'Incident information';

  @override
  String get riskResponsibilityResult => 'Responsibility result';

  @override
  String get riskProtectionOutcome => 'Protection outcome';

  @override
  String get riskEligibleDamage => 'Eligible damage';

  @override
  String get mandatoryTplExplanation =>
      'Mandatory third-party liability insurance mainly protects third parties; it does not automatically cover damage to the customer\'s vehicle.';

  @override
  String get physicalDamageExplanation =>
      'Physical damage insurance may cover customer vehicle damage under a verified policy.';

  @override
  String get insurerNoGuarantee =>
      'Saving a policy does not guarantee that the insurer will approve payment.';

  @override
  String get documentUrlDeferredHint =>
      'The app currently stores a document link only. Use a trusted link; direct upload will be added when shared secure storage is available.';
}
