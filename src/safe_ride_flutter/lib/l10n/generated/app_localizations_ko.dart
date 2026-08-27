// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Korean (`ko`).
class AppLocalizationsKo extends AppLocalizations {
  AppLocalizationsKo([String locale = 'ko']) : super(locale);

  @override
  String get appName => 'SafeRide';

  @override
  String get language => '언어';

  @override
  String get chooseLanguage => '언어 선택';

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
  String get profileAndSettings => '프로필 및 설정';

  @override
  String get switchToDriver => '기사 모드로 전환';

  @override
  String get startReceivingTrips => '운행 요청 받기';

  @override
  String get accountSection => '계정';

  @override
  String get editProfile => '프로필 수정';

  @override
  String get linkedAccounts => '연결된 계정';

  @override
  String get registerAsDriver => '기사 등록';

  @override
  String get linked => '연결됨';

  @override
  String get notLinked => '연결되지 않음';

  @override
  String get appAndNotifications => '앱 및 알림';

  @override
  String get notificationSettings => '알림 설정';

  @override
  String get darkMode => '다크 모드';

  @override
  String get supportAndLegal => '지원 및 법률';

  @override
  String get helpCenter => '고객 지원';

  @override
  String get privacyPolicy => '개인정보 처리방침';

  @override
  String get termsOfService => '이용약관';

  @override
  String get logout => '로그아웃';

  @override
  String get logoutQuestion => '로그아웃하시겠습니까?';

  @override
  String get logoutDescription => '앱에서 로그아웃하시겠습니까?';

  @override
  String get cancel => '취소';

  @override
  String get cannotSwitchToDriver => '진행 중인 운행이 있으면 기사 모드로 전환할 수 없습니다.';

  @override
  String get cannotSwitchToCustomer => '진행 중인 운행이 있으면 고객 모드로 전환할 수 없습니다.';

  @override
  String get tripNotFound => '운행을 찾을 수 없습니다.';

  @override
  String get sessionExpired => '세션이 만료되었습니다. 다시 로그인해 주세요.';

  @override
  String get genericError => '오류가 발생했습니다. 다시 시도해 주세요.';

  @override
  String get statusPending => '대기 중';

  @override
  String get statusDriverArriving => '기사가 오는 중';

  @override
  String get statusInProgress => '진행 중';

  @override
  String get statusCompleted => '완료';

  @override
  String get statusCancelled => '취소됨';

  @override
  String get notifications => '알림';

  @override
  String get notificationsLoadFailed => '알림을 불러올 수 없습니다';

  @override
  String get retry => '다시 시도';

  @override
  String get noNotifications => '알림이 없습니다';

  @override
  String get noNotificationsDescription => '승인된 시스템 알림이 여기에 표시됩니다.';

  @override
  String get read => '읽음';

  @override
  String get unread => '읽지 않음';

  @override
  String get notificationTypePromotion => '프로모션';

  @override
  String get notificationTypeWarning => '경고';

  @override
  String get notificationTypeSystemUpdate => '시스템 업데이트';

  @override
  String get loadMoreNotifications => '알림 더 보기';

  @override
  String get success => '성공';

  @override
  String get error => '오류';

  @override
  String get warning => '경고';

  @override
  String get information => '알림';

  @override
  String get serverConnectionError => '서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.';

  @override
  String get serverConnectionErrorTitle => '서버를 일시적으로 사용할 수 없습니다';

  @override
  String get serverConnectionRestored => '서버 연결이 복구되었습니다. 데이터를 다시 불러오는 중입니다.';

  @override
  String get serverConnectionRestoredTitle => '서버에 다시 연결됨';

  @override
  String get reload => '새로고침';

  @override
  String get confirm => '확인';

  @override
  String get callStartFailed => '통화를 시작할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get callRejected => '상대방이 통화를 거절했습니다.';

  @override
  String get callEnded => '통화가 종료되었습니다.';

  @override
  String get callConnecting => '연결 중...';

  @override
  String get callRinging => '연결음 울리는 중...';

  @override
  String get microphoneOn => '마이크 켜기';

  @override
  String get microphoneOff => '마이크 끄기';

  @override
  String get endCall => '통화 종료';

  @override
  String get speaker => '스피커';

  @override
  String get earpiece => '수화기';

  @override
  String get imageSelectionFailed => '이미지를 선택할 수 없습니다.';

  @override
  String get chatTitle => '채팅';

  @override
  String get chatReadOnly => '운행이 종료되어 메시지만 확인할 수 있습니다.';

  @override
  String get noMessages => '메시지가 없습니다.';

  @override
  String get messageHint => '메시지 입력...';

  @override
  String get tripEnded => '운행 종료';

  @override
  String get driverReviews => '기사 리뷰';

  @override
  String get driverHasNoReviews => '아직 기사 리뷰가 없습니다.';

  @override
  String get allReviews => '모든 리뷰';

  @override
  String get reviews => '개 리뷰';

  @override
  String get reportIncident => '문제 신고';

  @override
  String get reportHelpQuestion => '무엇을 도와드릴까요?';

  @override
  String get tripIncident => '운행 문제';

  @override
  String get paymentIssue => '결제 문제';

  @override
  String get partyFeedback => '기사/고객 피드백';

  @override
  String get appIssue => '앱 오류';

  @override
  String get wrongRoute => '기사가 경로를 벗어남';

  @override
  String get driverLate => '기사 도착 지연';

  @override
  String get inappropriateBehavior => '부적절한 태도';

  @override
  String get other => '기타';

  @override
  String get reportTrip => '운행 신고';

  @override
  String get reportSent => '운행 신고가 전송되었습니다.';

  @override
  String get reportSendFailed => '신고를 전송할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get commonIssues => '자주 발생하는 문제';

  @override
  String get issueEncountered => '발생한 문제';

  @override
  String get issueDescriptionHint => '발생한 문제를 자세히 설명해 주세요...';

  @override
  String get reportContentRequired => '신고 내용을 입력해 주세요.';

  @override
  String get safeRideDriver => 'SafeRide 기사';

  @override
  String get sendReport => '신고 보내기';

  @override
  String get edit => '수정';

  @override
  String get delete => '삭제';

  @override
  String requiredLicense(String licenseClass) {
    return '$licenseClass 면허';
  }

  @override
  String get editVehicle => '차량 수정';

  @override
  String get addVehicle => '새 차량 추가';

  @override
  String get vehicleType => '차량 유형';

  @override
  String get motorbike => '오토바이';

  @override
  String get car => '자동차';

  @override
  String get vehicleName => '차량명';

  @override
  String get vehicleNameHint => '예: Honda Vision';

  @override
  String get engineCapacity => '배기량 (cc)';

  @override
  String get engineCapacityHint => '예: 110, 125, 150';

  @override
  String get licensePlate => '번호판';

  @override
  String get licensePlateHint => '예: 29A1 - 123.45';

  @override
  String get color => '색상';

  @override
  String get colorHint => '예: 파란색';

  @override
  String get saveChanges => '변경사항 저장';

  @override
  String get saveVehicle => '차량 저장';

  @override
  String get vehicleNameValidation => '차량명은 2~100자여야 합니다.';

  @override
  String get engineCapacityValidation =>
      'A1 또는 A 면허 요건을 확인하려면 올바른 오토바이 배기량을 입력하세요.';

  @override
  String get licensePlateLengthValidation => '번호판은 4~20자여야 합니다.';

  @override
  String get licensePlateFormatValidation =>
      '번호판에는 문자, 숫자, 마침표, 공백, 하이픈만 사용할 수 있습니다.';

  @override
  String get colorValidation => '색상은 30자를 초과할 수 없습니다.';

  @override
  String get deleteVehicleQuestion => '차량을 삭제할까요?';

  @override
  String deleteVehicleDescription(String name) {
    return '\"$name\" 차량을 삭제하시겠습니까? 이 작업은 취소할 수 없습니다.';
  }

  @override
  String get deleteNow => '지금 삭제';

  @override
  String get dismiss => '취소';

  @override
  String get requestFailed => '요청을 처리할 수 없습니다.';

  @override
  String get myVehicles => '내 차량';

  @override
  String get vehicleManagementDescription =>
      '주차 및 운전 지원 서비스에 사용할 개인 차량을 관리하세요.';

  @override
  String get noVehicles => '등록된 차량이 없습니다.';

  @override
  String get historyLoadFailed => '운행 내역을 불러올 수 없습니다.';

  @override
  String get noTripHistory => '운행 내역이 없습니다.';

  @override
  String get tripNotRebookable => '다시 예약하기에 필요한 운행 정보가 부족합니다.';

  @override
  String get loadingTrip => '운행 정보를 불러오는 중...';

  @override
  String get chatOpenFailed => '지금은 채팅을 열 수 없습니다.';

  @override
  String get chat => '채팅';

  @override
  String get viewReviews => '리뷰 보기';

  @override
  String get tripDetailsLoadFailed => '운행 정보를 불러올 수 없습니다.';

  @override
  String get tripDetails => '운행 상세';

  @override
  String get rebookThisTrip => '이 운행 다시 예약';

  @override
  String get tripCode => '운행 코드';

  @override
  String bookingOrder(int id) {
    return '예약 #$id';
  }

  @override
  String get routeMapUnavailable => '이 운행의 경로 지도가 없습니다.';

  @override
  String get route => '경로';

  @override
  String get tripRoute => '운행 경로';

  @override
  String get pickupPoint => '출발지';

  @override
  String get destinationPoint => '목적지';

  @override
  String get distance => '거리';

  @override
  String get duration => '시간';

  @override
  String minutesValue(num minutes) {
    return '$minutes분';
  }

  @override
  String get unknown => '알 수 없음';

  @override
  String get driverAndVehicle => '기사 및 차량';

  @override
  String get driverInfoUnavailable => '이 운행의 기사 정보가 없습니다.';

  @override
  String plateValue(String plate) {
    return '번호판: $plate';
  }

  @override
  String vehicleColorValue(String color) {
    return '차량 색상: $color';
  }

  @override
  String tripCountValue(int count) {
    return '$count회 운행';
  }

  @override
  String experienceYearsValue(int years) {
    return '경력 $years년';
  }

  @override
  String get tripCost => '운행 요금';

  @override
  String get unknownPaymentMethod => '결제 수단 미확인';

  @override
  String get fare => '요금';

  @override
  String get discount => '할인';

  @override
  String get total => '합계';

  @override
  String paidAtValue(String time) {
    return '$time 결제';
  }

  @override
  String get customerReview => '고객 리뷰';

  @override
  String get reviewAndFeedback => '리뷰 및 피드백';

  @override
  String get customerHasNotReviewed => '고객이 아직 이 운행을 평가하지 않았습니다.';

  @override
  String get noReviewData => '이 운행의 리뷰 정보가 없습니다.';

  @override
  String get tripHistory => '운행 내역';

  @override
  String get tripCompletedThanks => '운행이 완료되었습니다. 감사합니다!';

  @override
  String get tripInfoUnavailable => '운행 정보를 확인할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get returnConfirmationFailed => '차량 반환을 확인할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get ratingSubmitFailed => '평가를 제출할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get waitForPayment => '결제가 완료될 때까지 기다려 주세요.';

  @override
  String get completeRequirementsBeforeLeaving =>
      '이 화면을 나가기 전에 차량 반환을 확인하고 평가를 제출하세요.';

  @override
  String get tripComplete => '운행 완료';

  @override
  String get thanksForUsingService => '서비스를 이용해 주셔서 감사합니다';

  @override
  String get distanceUpper => '거리';

  @override
  String get durationUpper => '시간';

  @override
  String get confirmVehicleReturned => '기사가 차량을 반환했음을 확인';

  @override
  String get sendRatingAndWaitPayment => '평가 제출 및 결제 대기';

  @override
  String get confirmTripRateLater => '운행 확인 및 나중에 평가';

  @override
  String get paymentDetails => '결제 상세';

  @override
  String get baseFare => '기본 요금';

  @override
  String get promotion => '프로모션';

  @override
  String get driverRatingQuestion => '기사 서비스는 어떠셨나요?';

  @override
  String get driverCommentHint => '기사에 대한 의견 (선택 사항)';

  @override
  String get waitingForPayment => '결제 대기 중';

  @override
  String get paymentWaitingInstructions =>
      '기사의 휴대전화에서 QR 코드를 스캔하거나 현금 결제 확인을 기다려 주세요.';

  @override
  String get cancelReasonPlanChanged => '일정 변경';

  @override
  String get cancelReasonWaitTooLong => '대기 시간이 너무 김';

  @override
  String get cancelReasonWrongLocation => '장소를 잘못 선택함';

  @override
  String get cancelReasonNoLongerNeeded => '기사가 더 이상 필요하지 않음';

  @override
  String get cancelReasonOther => '기타 사유';

  @override
  String get cancelTripQuestion => '운행을 취소할까요?';

  @override
  String get cancelSearchConfirmation => '기사 검색을 취소하시겠습니까?';

  @override
  String cancelBookingConfirmation(int id) {
    return '#$id 운행을 취소하시겠습니까?';
  }

  @override
  String get cancelReason => '취소 사유';

  @override
  String get confirmCancellation => '취소 확인';

  @override
  String get goBack => '아니요, 돌아가기';

  @override
  String get cancelTripFailed => '운행을 취소할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get tripCannotBeCancelled => '이 여정은 현재 상태에서 취소할 수 없습니다.';

  @override
  String get tripWaitExpired => '대기 시간이 만료되어 운행이 종료되었습니다.';

  @override
  String get tripCancelledSuccessfully => '운행이 취소되었습니다.';

  @override
  String get scheduledTripCancelledSuccessfully => '예약 여정이 취소되었습니다.';

  @override
  String get rebook => '다시 예약';

  @override
  String get noPromotions => '현재 프로모션이 없습니다';

  @override
  String remainingUses(int count) {
    return '남은 횟수: $count';
  }

  @override
  String get promoValidatedOnBooking => '예약 시 코드가 확인됩니다';

  @override
  String get noAvailablePromoCodes => '사용 가능한 프로모션 코드가 없습니다.';

  @override
  String get deselectPromo => '프로모션 코드 선택 해제';

  @override
  String minimumOrder(String amount) {
    return '최소 주문: $amount';
  }

  @override
  String remainingUseCount(int count) {
    return '$count회 남음';
  }

  @override
  String get usageExhausted => '사용 횟수 소진';

  @override
  String get inUse => '사용\n중';

  @override
  String get useNow => '지금\n사용';

  @override
  String percentDiscount(num percent) {
    return '$percent% 할인';
  }

  @override
  String maximumDiscount(String amount) {
    return ' (최대 $amount)';
  }

  @override
  String fixedDiscount(String amount) {
    return '$amount 할인';
  }

  @override
  String expiresOn(String date) {
    return '만료일: $date';
  }

  @override
  String minimumOrderShort(String amount) {
    return '최소 주문 $amount';
  }

  @override
  String get exitAppQuestion => '앱을 종료할까요?';

  @override
  String get exitAppDescription => 'SafeRide를 종료하시겠습니까?';

  @override
  String get exit => '종료';

  @override
  String get activity => '이용 중';

  @override
  String get safeRideAssistant => 'SafeRide 도우미';

  @override
  String get tryAgain => '다시 시도';

  @override
  String get activeTripNotice => '진행 중인 여정이 있습니다. 이용 중 메뉴에서 확인해 주세요.';

  @override
  String get trackingTrip => '여정 추적 중';

  @override
  String get noActiveTripForSos => 'SOS를 사용할 수 있는 진행 중인 여정이 없습니다.';

  @override
  String get viewAll => '모두 보기';

  @override
  String get locatingAddress => '주소 확인 중...';

  @override
  String get searchPickup => '출발지 검색';

  @override
  String get searchDestination => '도착지 검색';

  @override
  String get selectedPickup => '선택한 출발지';

  @override
  String get selectedDestination => '선택한 도착지';

  @override
  String get searchOrTapMap => '검색하거나 지도를 눌러 선택하세요.';

  @override
  String get confirmPickup => '출발지 확인';

  @override
  String get confirmDestination => '도착지 확인';

  @override
  String get prepayment => '선결제';

  @override
  String get payosPaymentAmount => 'PayOS 결제 금액';

  @override
  String get checkPayment => '결제 확인';

  @override
  String get payAfterTrip => '여정 후 결제';

  @override
  String get prepaid => '선결제 완료';

  @override
  String get backToTrip => '여정으로 돌아가기';

  @override
  String get payosQrCreateFailed => 'PayOS QR 코드를 만들 수 없습니다.';

  @override
  String get scanQrToPay => '은행 앱으로 스캔하여 결제하세요';

  @override
  String get cameraOpenFailed => '카메라를 열 수 없습니다. 권한을 확인해 주세요.';

  @override
  String get photoCaptureFailed => '사진을 촬영할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get alignDocumentCorners => '문서의 네 모서리를 프레임 안에 맞추세요';

  @override
  String get submittedInformation => '제출 정보';

  @override
  String get documentNumber => '문서 번호';

  @override
  String get licenseClass => '면허 등급';

  @override
  String get issueDate => '발급일';

  @override
  String get expiryDate => '만료일';

  @override
  String get documents => '서류';

  @override
  String get frontSide => '앞면';

  @override
  String get backSide => '뒷면';

  @override
  String get submittedFile => '제출 파일';

  @override
  String get documentApproved => '승인됨';

  @override
  String get documentPendingReview => '제출됨, 검토 대기 중';

  @override
  String get documentRejected => '거부됨';

  @override
  String get documentNotSubmitted => '미제출';

  @override
  String get identityVerification => '본인 인증';

  @override
  String get completeYourProfile => '프로필을 완성하세요';

  @override
  String get identityVerificationIntro =>
      '운행을 시작하고 승객의 안전을 보장하려면 본인을 인증하고 필요한 서류를 제출해 주세요.';

  @override
  String get requiredDocuments => '필수 제출 서류';

  @override
  String get submitApplicationNow => '지금 신청서 제출';

  @override
  String get verificationTime => '인증에는 일반적으로 영업일 기준 1~3일이 소요됩니다.';

  @override
  String get previousApplicationRejected => '이전 신청이 거부되었습니다';

  @override
  String get profileStatusLoadFailed => '신청 상태를 불러올 수 없습니다. 다시 시도해 주세요.';

  @override
  String get idCardOrPassport => '신분증 / 여권';

  @override
  String get frontAndBack => '앞면 및 뒷면';

  @override
  String get drivingLicense => '운전면허증';

  @override
  String get licensePhotoAndInfo => '면허증 사진 및 정보';

  @override
  String get criminalRecord => '범죄경력증명서';

  @override
  String get originalIssuedWithinSixMonths => '6개월 이내 발급 원본';

  @override
  String get resubmissionRequired => '재제출 필요';

  @override
  String get submitted => '제출됨';

  @override
  String get confirmHireDriver => '기사 확정';

  @override
  String get hourlyHire => '시간제 대여';

  @override
  String get tripDetailsHeading => '여정 상세';

  @override
  String get notCreated => '미생성';

  @override
  String get awaitingConfirmation => '확인 대기';

  @override
  String get estimatedDuration => '예상 시간';

  @override
  String get updating => '업데이트 중';

  @override
  String get estimatedTotalPayment => '예상 총 결제액';

  @override
  String get missingTripToConfirmDriver => '기사를 확정할 여정이 없습니다.';

  @override
  String get driverOfferNotFound => '기사 제안 정보를 찾을 수 없습니다.';

  @override
  String get confirmDriverFailed => '기사를 확정할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get driverConfirmed => '기사 확정 완료';

  @override
  String driverConfirmedMessage(String driverName, int bookingId) {
    return '$driverName 기사가 #$bookingId 여정을 담당합니다. 배차를 기다리는 중입니다...';
  }

  @override
  String get agree => '확인';

  @override
  String driverRatingSummary(String rating, int tripCount, int years) {
    return '별점 $rating • $tripCount회 운행 • 경력 $years년';
  }

  @override
  String get confirmDriverNotice => '확정하기 전에 기사 정보를 꼼꼼히 확인하세요.';

  @override
  String get oldTripDataInvalid => '이전 여정 데이터가 올바르지 않습니다.';

  @override
  String get calculatingFarePleaseWait => '요금을 계산 중입니다. 잠시 기다려 주세요.';

  @override
  String get bookingSuccessful => '예약이 완료되었습니다. 기사가 정시에 도착합니다.';

  @override
  String get rebookTrip => '이 여정 다시 예약';

  @override
  String get confirmPreviousInformation => '이전 정보 확인';

  @override
  String get reviewRouteAndVehicle => '예정된 여정의 경로와 차량을 확인해 주세요.';

  @override
  String get departureTime => '출발 시간';

  @override
  String get leaveNow => '지금 출발';

  @override
  String get scheduleAhead => '예약';

  @override
  String get promotionCode => '프로모션 코드';

  @override
  String get oldPromoCannotBeReused =>
      '이전 프로모션은 다시 사용할 수 없습니다. 새 코드를 선택하거나 입력하세요.';

  @override
  String get grandTotal => '총액';

  @override
  String discountApplied(String amount) {
    return '↓ $amount 할인';
  }

  @override
  String get taxesIncluded => '세금 및 수수료 포함';

  @override
  String get confirmAndFindDriver => '확인 후 기사 찾기';

  @override
  String get addNewPromoCode => '새 프로모션 코드 추가';

  @override
  String get completePaymentBeforeExit => '나가기 전에 결제를 완료해 주세요';

  @override
  String get completePayment => '결제를 완료해 주세요.';

  @override
  String get tripPayment => '여정 결제';

  @override
  String get customerPaymentAmount => '고객 결제 금액';

  @override
  String get paid => '결제 완료';

  @override
  String get checkAgain => '다시 확인';

  @override
  String get cashConfirmed => '현금 확인 완료';

  @override
  String get customerPaid => '고객 결제 완료';

  @override
  String get backToHome => '홈으로';

  @override
  String get paymentQrCreateFailed => '결제 QR 코드를 만들 수 없습니다.';

  @override
  String get reconfirmCash => '현금 다시 확인';

  @override
  String get recreateQr => 'QR 다시 만들기';

  @override
  String get switchPaymentMethod => '다른 결제 수단';

  @override
  String get customerScanQr => '고객에게 이 코드를 스캔해 달라고 안내하세요';

  @override
  String get cashPaymentConfirmFailed => '현금 결제를 확인할 수 없습니다.';

  @override
  String get chooseCustomerPaymentMethod => '고객 결제 수단 선택';

  @override
  String get qrPayment => 'QR 결제';

  @override
  String get cashPayment => '현금';

  @override
  String get returnVehicleConfirmation => '차량 반환 확인';

  @override
  String get returnEvidenceInstruction =>
      '고객에게 차량을 반환한 증거 사진을 1~3장 촬영하거나 선택하세요.';

  @override
  String get tapToAddPhoto => '사진을 추가하려면 누르세요';

  @override
  String get optionalNote => '메모 (선택)';

  @override
  String get noteHint => '필요한 경우 메모를 입력하세요...';

  @override
  String get submitting => '제출 중...';

  @override
  String get returnConfirmedSuccess => '확인 완료';

  @override
  String get returnConfirmedMessage => '차량 반환이 기록되었습니다. 여정을 완료하는 중입니다.';

  @override
  String get done => '완료';

  @override
  String get minimumEvidencePhoto => '증거 사진이 최소 1장 필요합니다.';

  @override
  String get maximumEvidencePhotos => '사진은 최대 3장까지 업로드할 수 있습니다.';

  @override
  String get evidenceUploadFailed => '증거를 제출할 수 없습니다. 다시 시도하세요.';

  @override
  String get takePhoto => '사진 촬영';

  @override
  String get chooseFromGallery => '갤러리에서 선택';

  @override
  String get removePhoto => '사진 삭제';

  @override
  String get removePhotoQuestion => '이 사진을 삭제할까요?';

  @override
  String photoNumber(int number) {
    return '사진 $number';
  }

  @override
  String photoCount(int count, int max) {
    return '$count / $max장';
  }

  @override
  String remainingPhotos(int count) {
    return '$count장 남음';
  }

  @override
  String submitEvidenceWithCount(int count) {
    return '차량 반환 확인 ($count장)';
  }

  @override
  String mediaAccessFailed(String source) {
    return '$source에 접근할 수 없습니다.';
  }

  @override
  String get camera => '카메라';

  @override
  String get gallery => '갤러리';

  @override
  String get myWallet => '내 지갑';

  @override
  String get availableBalance => '사용 가능 잔액';

  @override
  String get withdraw => '출금';

  @override
  String get topUp => '충전';

  @override
  String get income => '수입';

  @override
  String get day => '일';

  @override
  String get week => '주';

  @override
  String get month => '월';

  @override
  String totalIncomeForPeriod(String period) {
    return '총 수입\n$period';
  }

  @override
  String get recentTransactions => '최근 거래';

  @override
  String get bankListLoadFailed => '은행 목록을 불러올 수 없습니다.';

  @override
  String get withdrawalRequestSent => '출금 요청을 제출했습니다.';

  @override
  String get withdrawalRequestFailed => '출금 요청을 제출할 수 없습니다.';

  @override
  String get withdrawToBank => '은행으로 출금';

  @override
  String get bankInfoWillBeSaved => '다음 출금을 위해 이 정보를 저장합니다.';

  @override
  String get lastBankPreFilled => '최근 계좌 정보를 미리 입력했습니다.';

  @override
  String get selectBankRequired => '은행을 선택하세요';

  @override
  String get bank => '은행';

  @override
  String get searchAndSelectBank => '은행 검색 및 선택';

  @override
  String get accountNumber => '계좌번호';

  @override
  String get invalidAccountNumber => '유효하지 않은 계좌번호';

  @override
  String get accountHolderName => '예금주';

  @override
  String get accountHolderRequired => '예금주 이름을 입력하세요';

  @override
  String get withdrawalAmount => '출금 금액';

  @override
  String minimumWithdrawal(String amount) {
    return '최소 출금액은 $amount입니다';
  }

  @override
  String get confirmWithdrawal => '출금 확인';

  @override
  String get selectBank => '은행 선택';

  @override
  String get searchBankHint => '이름, 코드 또는 BIN으로 검색';

  @override
  String get bankNotFound => '은행을 찾을 수 없습니다.';

  @override
  String get noTransactions => '거래 내역이 없습니다.';

  @override
  String get today => '오늘';

  @override
  String get thisMonth => '이번 달';

  @override
  String get thisWeek => '이번 주';

  @override
  String get noPreviousPeriodData => '이전 기간\n데이터 없음';

  @override
  String periodComparison(String value) {
    return '이전 기간 대비\n$value%';
  }

  @override
  String get completed => '완료';

  @override
  String get home => '홈';

  @override
  String get account => '계정';

  @override
  String get wallet => '지갑';

  @override
  String get destinationQuestion => '오늘 어디로 가시나요?';

  @override
  String get bookNow => '지금 예약';

  @override
  String get bookNowDescription => '여정에 맞는 기사를 찾아드립니다';

  @override
  String get scheduleBooking => '예약하기';

  @override
  String get history => '이용 내역';

  @override
  String get myVehiclesShort => '내 차량';

  @override
  String get promotions => '프로모션';

  @override
  String get sos => '긴급 SOS';

  @override
  String get recentTrips => '최근 여정';

  @override
  String get friendlyUser => '고객님';

  @override
  String greeting(String name) {
    return '안녕하세요, $name';
  }

  @override
  String get sampleRecentPickup => '응우옌반린 123, 7군';

  @override
  String get sampleRecentDestination => '떤선녓 공항';

  @override
  String get sampleRecentTime => '어제 14:30';

  @override
  String get driverProfile => '기사 프로필';

  @override
  String tripCountPlus(String count) {
    return '$count+회 운행';
  }

  @override
  String get kycStatus => 'KYC 상태';

  @override
  String get kycApprovedDescription => '시스템에서 프로필을 승인했습니다';

  @override
  String get cleanCriminalRecord => '깨끗하고 투명한 기록';

  @override
  String get confirmHire => '기사 확정';

  @override
  String get rejectAndFindAnotherDriver => '거절하고 다른 기사 찾기';

  @override
  String get rejectDriverQuestion => '이 기사를 거절할까요?';

  @override
  String get rejectDriverDescription => '이 기사를 건너뛰고 다른 기사를 계속 찾습니다.';

  @override
  String get findingAnotherDriver => '다른 기사를 찾는 중...';

  @override
  String get rejectDriverFailed => '기사를 거절할 수 없습니다.';

  @override
  String get experienceUpper => '경력';

  @override
  String yearsValueCapitalized(int years) {
    return '$years년';
  }

  @override
  String get safeDriving => '안전 운전';

  @override
  String get friendly => '친절함';

  @override
  String get verified => '인증됨';

  @override
  String get idCardFront => '신분증 앞면';

  @override
  String get idCardBack => '신분증 뒷면';

  @override
  String get idCardCameraInstruction => '신분증 전체가 프레임 안에 들어오도록 밝고 선명하게 촬영하세요.';

  @override
  String get idCardScanned => '신분증 정보를 스캔했습니다.';

  @override
  String get ocrScanFailed => '이 이미지를 OCR로 읽을 수 없습니다.';

  @override
  String get stepOneOfThree => '1/3단계';

  @override
  String get uploadIdCard => '신분증 업로드';

  @override
  String get captureIdCard => '신분증 촬영';

  @override
  String get idCardUploadInstruction =>
      '빛 반사나 잘린 모서리 없이 선명한 신분증 앞뒷면 이미지를 제출하세요.';

  @override
  String get fullName => '성명';

  @override
  String get idCardNameHint => '신분증에 표시된 이름 입력';

  @override
  String get idCardNumber => '신분증 번호';

  @override
  String get idCardNumberHint => '신분증 번호 입력';

  @override
  String get continueAction => '계속';

  @override
  String get idCardFieldsRequired => '양면을 촬영하고 성명과 신분증 번호를 확인하세요.';

  @override
  String get idCardPhotoTip => '팁: 어두운 평면 위에서 충분한 자연광으로 촬영하면 가장 좋습니다.';

  @override
  String get ocrScanningOnDevice => '기기에서 OCR 스캔 중...';

  @override
  String get idCardOcrFilled => 'OCR이 신분증 정보를 자동 입력했습니다';

  @override
  String get tapToCaptureOrUpload => '눌러서 촬영 또는 업로드';

  @override
  String get licenseFront => '면허증 앞면';

  @override
  String get licenseBack => '면허증 뒷면';

  @override
  String get licenseCameraInstruction => '면허증 전체가 프레임 안에 들어오도록 밝고 선명하게 촬영하세요.';

  @override
  String get ocrMlKitScanned => 'Google ML Kit OCR로 스캔했습니다.';

  @override
  String get licenseOcrFailed => '이 면허증 이미지를 OCR로 읽을 수 없습니다.';

  @override
  String get licenseType => '면허 종류';

  @override
  String get licensePhotos => '운전면허증 사진';

  @override
  String get licenseNameHint => '면허증에 표시된 이름 입력';

  @override
  String get licenseNumber => '면허증 번호';

  @override
  String get licenseNumberHint => '면허증 번호 입력';

  @override
  String get selectLicenseClass => '면허 등급 선택';

  @override
  String get unlimited => '만료 없음';

  @override
  String get licenseNoExpiry => '이 면허증은 만료일이 없습니다';

  @override
  String get idAndLicenseNameMismatch => '신분증과 운전면허증의 이름이 일치하지 않습니다.';

  @override
  String get stepTwoOfThree => '2/3단계';

  @override
  String get uploadLicense => '운전면허증 업로드';

  @override
  String get licenseOcrFilled => 'OCR이 면허증 정보를 자동 입력했습니다';

  @override
  String get criminalRecordInstruction =>
      '승객의 안전을 위해 6개월 이내에 발급된 범죄경력증명서를 제출하세요.';

  @override
  String get reviewWithinHours => '신청서는 영업일 기준 24~48시간 이내에 검토됩니다.';

  @override
  String get submittingApplication => '신청서 제출 중...';

  @override
  String get completeAndSubmit => '완료 및 제출';

  @override
  String get stepThreeOfThree => '3/3단계';

  @override
  String get uploadCriminalRecord => '범죄경력증명서 업로드';

  @override
  String get uploadRequirements => '업로드 요구 사항';

  @override
  String get clearNoGlare => '빛 반사 없이 선명한 사진이어야 합니다.';

  @override
  String get allFourCorners => '문서의 네 모서리가 모두 보여야 합니다.';

  @override
  String get supportedDocumentFormats => '지원 형식: JPG, PNG, PDF (최대 10MB).';

  @override
  String get tapToUploadDocument => '눌러서 업로드하거나 파일을 여기로 끌어다 놓으세요';

  @override
  String get photoOrPdfSupported => '사진 또는 스캔 PDF 파일 지원';

  @override
  String get chooseDocument => '문서 선택';

  @override
  String get documentSelected => '문서 선택됨';

  @override
  String get change => '변경';

  @override
  String get criminalRecordOcrRead => 'OCR이 범죄경력증명서 내용을 읽었습니다';

  @override
  String get criminalRecordScanned => '범죄경력증명서를 OCR로 스캔했습니다.';

  @override
  String get documentOcrFailed => '이 문서를 OCR로 읽을 수 없습니다.';

  @override
  String get applicationSubmitted => '신청서 제출 완료!';

  @override
  String get applicationProcessing => '신청서를 처리 중입니다. 결과를 곧 알려드리겠습니다.';

  @override
  String get applicationSubmitFailed => '신청서를 제출할 수 없습니다. 다시 시도해 주세요.';

  @override
  String tripEndedWithId(int id) {
    return '여정 #$id이 종료되었습니다.';
  }

  @override
  String get searchingDriver => '기사를 찾고 있습니다...';

  @override
  String get cancelling => '취소 중...';

  @override
  String get cancelBooking => '여정 취소';

  @override
  String remainingCountdown(String message, String countdown) {
    return '$message - $countdown 남음';
  }

  @override
  String get estimatedWaitTime => '예상 대기 시간: 약 2분';

  @override
  String tripCodeWithStatus(int id, String status) {
    return '여정 #$id • $status';
  }

  @override
  String secondsRemaining(int seconds) {
    return '$seconds초 남음';
  }

  @override
  String get suitableDriverReady => '적합한 기사가 준비되었습니다';

  @override
  String reviewProfileAndConfirm(String countdown) {
    return '프로필을 확인하고 확정하세요$countdown.';
  }

  @override
  String get viewProfile => '프로필 보기';

  @override
  String get waitingDriverAccept => '기사 수락 대기 중';

  @override
  String get appliedCode => '적용된 코드';

  @override
  String promotionWithCode(String code) {
    return '프로모션 ($code):';
  }

  @override
  String currentLocationFailed(String error) {
    return '현재 위치를 가져올 수 없습니다: $error';
  }

  @override
  String get callUnavailableSessionExpired => '세션이 만료되어 통화할 수 없습니다.';

  @override
  String get customer => '고객';

  @override
  String get incomingCall => '수신 전화';

  @override
  String get customerCalling => '고객이 전화하고 있습니다.';

  @override
  String get decline => '거절';

  @override
  String get answer => '받기';

  @override
  String onlineLocationFailed(String error) {
    return '위치를 가져오거나 온라인 상태로 전환할 수 없습니다: $error';
  }

  @override
  String get chatUnavailable => '지금은 채팅을 열 수 없습니다.';

  @override
  String get gpsSimulationEnabled => '백엔드 GPS 시뮬레이션이 켜졌습니다';

  @override
  String get gpsSimulationDisabled => 'GPS 시뮬레이션을 끄고 실제 GPS를 사용합니다';

  @override
  String get activeTrip => '진행 중인 여정';

  @override
  String get message => '메시지';

  @override
  String get callCustomer => '고객에게 전화';

  @override
  String get processing => '처리 중...';

  @override
  String get startPickup => '픽업 출발';

  @override
  String get driverArrived => '픽업 장소 도착';

  @override
  String get startTrip => '여정 시작';

  @override
  String get endTrip => '여정 종료';

  @override
  String get waitingCustomerReturnConfirmation =>
      '고객의 차량 반환 확인을 기다리고 있습니다.\n고객이 응답하지 않으면 대신 확인할 수 있습니다.';

  @override
  String get confirmReturnWithEvidence => '증거 사진으로 대신 확인';

  @override
  String get returnConfirmedCompleting => '차량 반환 확인 완료. 여정을 완료하는 중...';

  @override
  String get returnConfirmedPaymentRequired =>
      '차량 반환 확인 완료. 여정을 끝내려면 결제를 확인하세요.';

  @override
  String get confirmPayment => '결제 확인';

  @override
  String get statusAccepted => '여정 수락';

  @override
  String get statusArrived => '픽업 장소 도착';

  @override
  String get waitingReturnConfirmation => '차량 반환 확인 대기';

  @override
  String get returnConfirmedStatus => '차량 반환 확인됨';

  @override
  String get tripStatusUpdateFailed => '여정 상태를 업데이트할 수 없습니다.';

  @override
  String get todayIncomeUpper => '오늘 수입';

  @override
  String tripCountShort(int count) {
    return '$count회 운행';
  }

  @override
  String get waitingConfirmation => '확인 대기 중';

  @override
  String get waitingCustomerDriverConfirmation =>
      '고객의 기사 확인을 기다리고 있습니다. 앱을 종료하지 마세요.';

  @override
  String get newTripAvailable => '새 여정이 도착했습니다!';

  @override
  String get expectedIncomeUpper => '예상 수입';

  @override
  String get pickupCustomerUpper => '고객 픽업';

  @override
  String get pickupPointA => '픽업 장소 (A)';

  @override
  String get destinationPointB => '도착지 (B)';

  @override
  String get accept => '수락';

  @override
  String get selectPickupDate => '픽업 날짜 선택';

  @override
  String get selectPickupTimeHelp => '픽업 시간 선택';

  @override
  String get invalidSchedule => '예약 시간은 현재보다 최소 30분 이후여야 합니다.';

  @override
  String get selectPickupRequired => '픽업 장소를 선택하세요.';

  @override
  String get selectServiceAndVehicle => '서비스와 차량을 선택하세요.';

  @override
  String get selectDestinationRequired => '도착지를 선택하세요.';

  @override
  String get selectPickupTimeRequired => '픽업 시간을 선택하세요.';

  @override
  String get fareEstimateUnavailable => '예상 요금이 없습니다. 경로를 확인하고 다시 시도하세요.';

  @override
  String get bookingFailed => '여정을 예약할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get bookingSuccess => '예약 완료';

  @override
  String get addVehicleFailed => '차량을 추가할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get vehicleAdded => '새 차량을 추가했습니다.';

  @override
  String get selectYourVehicle => '차량 선택';

  @override
  String get loadingServices => '서비스 정보 불러오는 중...';

  @override
  String get specialRequest => '특별 요청 (선택)';

  @override
  String get fareCalculationNote => '예약 확정 시 승인된 운임이 고정됩니다.';

  @override
  String get confirmScheduled => '예약 여정 확인';

  @override
  String get confirmHourlyHire => '시간제 대여 확인';

  @override
  String get confirmNow => '지금 예약 확인';

  @override
  String get selectPickup => '픽업 장소 선택';

  @override
  String get selectDestination => '도착지 선택';

  @override
  String get calculatingFare => '예상 요금 계산 중...';

  @override
  String hoursValue(int hours) {
    return '$hours시간';
  }

  @override
  String surgePricing(num multiplier) {
    return '높은 수요로 요금 상승 (x$multiplier)';
  }

  @override
  String estimatedRentalHours(int hours) {
    return '예상 대여 시간: $hours시간';
  }

  @override
  String get addPromoCode => '프로모션 코드 추가';

  @override
  String get tripService => '여정별';

  @override
  String get hourlyService => '시간제';

  @override
  String get addNewVehicle => '새 차량 추가';

  @override
  String get saveVehicleAndContinue => '계정에 차량을 저장하고 예약을 계속하세요.';

  @override
  String get add => '추가';

  @override
  String plateNumberLabel(String value) {
    return '번호판: $value';
  }

  @override
  String vehicleColorLabel(String value) {
    return '색상: $value';
  }

  @override
  String get noBookableVehicles => '예약 가능한 차량이 없습니다. 예약 전에 차량을 추가하세요.';

  @override
  String get mapsConfigMissing => '지도가 구성되지 않았습니다. 나중에 다시 시도하세요.';

  @override
  String get serverDisconnectedRetrying => '서버 연결이 끊어졌습니다. 다시 연결 중...';

  @override
  String get tripCancelled => '여정이 취소되었습니다.';

  @override
  String get driverLocationTrackingRetrying =>
      '기사 위치 추적에 연결할 수 없습니다. 다시 시도 중...';

  @override
  String get safetyCheck => '안전 확인';

  @override
  String get safetyConfirmed => 'SafeRide에서 안전 상태를 확인했습니다.';

  @override
  String get iAmSafe => '안전합니다';

  @override
  String get callDriver => '기사에게 전화';

  @override
  String get activateSosQuestion => '긴급 SOS를 활성화할까요?';

  @override
  String get activateSosDescription => '이 여정에 긴급 신호를 보내시겠습니까?';

  @override
  String get activateSos => '긴급 SOS 활성화';

  @override
  String get sosActivationFailed => 'SOS를 활성화할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get sosLocationFailed => 'SOS를 위한 현재 위치를 가져올 수 없습니다.';

  @override
  String get emergencyHelpMessage => '긴급 지원이 필요합니다';

  @override
  String get sosActivatedForTrip => '이 여정에 SOS가 활성화되었습니다.';

  @override
  String get sosActivatedHelpComing => 'SOS가 활성화되었습니다. 최대한 빨리 지원하겠습니다.';

  @override
  String get driverAtPickup => '기사가 픽업 장소에 도착했습니다';

  @override
  String get waitingDriverPayment => '기사 결제 대기';

  @override
  String driverArrivingMinutes(int minutes) {
    return '기사 도착 중 • $minutes분';
  }

  @override
  String movingMinutes(int minutes) {
    return '이동 중 • $minutes분';
  }

  @override
  String get onCorrectRoute => '올바른 경로로 이동 중입니다';

  @override
  String get safeRideDriverName => 'SafeRide 기사';

  @override
  String get updatingVehicle => '차량 업데이트 중';

  @override
  String get prepayWithPayos => 'PayOS로 선결제';

  @override
  String get call => '전화';

  @override
  String get share => '공유';

  @override
  String get payDriverToComplete => '여정을 완료하려면 기사에게 결제하세요.';

  @override
  String get endingTrip => '여정 종료 중...';

  @override
  String get tripNotReadyForPayment => '여정이 아직 결제 준비가 되지 않았습니다.';

  @override
  String get tripNotReadyForChat => '여정이 아직 채팅 준비가 되지 않았습니다.';

  @override
  String get chatAccountUnknown => '채팅할 계정을 확인할 수 없습니다.';

  @override
  String get tripNotReadyForCall => '여정 준비 전에는 통화할 수 없습니다.';

  @override
  String driverCalling(String driverName) {
    return '$driverName 기사가 전화하고 있습니다.';
  }

  @override
  String get tripCannotEndNow => '지금은 이 여정을 종료할 수 없습니다.';

  @override
  String get tripEndFailed => '여정을 종료할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get sosActivated => '긴급 SOS 활성화됨';

  @override
  String get sendingSos => '긴급 SOS 전송 중...';

  @override
  String get shareRoute => '경로 공유';

  @override
  String get shareRouteDescription =>
      '가족이나 친구가 실시간으로 여정을 추적할 수 있도록 아래 링크를 보내세요.';

  @override
  String get linkCopied => '링크 복사됨';

  @override
  String get close => '닫기';

  @override
  String get enableLocationForPickup => 'SafeRide가 GPS를 픽업 장소로 사용하도록 위치를 켜세요.';

  @override
  String get microphonePermissionRequired => 'SafeRide의 마이크 사용을 허용하세요.';

  @override
  String get voiceMessage => '음성 메시지';

  @override
  String get currentGpsUnavailable => '현재 GPS 위치를 가져올 수 없습니다. 위치를 켜고 다시 시도하세요.';

  @override
  String get audioUploadFailed => '녹음 파일을 업로드할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get aiAssistantUnavailable => 'AI 도우미를 사용할 수 없습니다. 나중에 다시 시도하세요.';

  @override
  String get aiAssistantConnectionFailed => 'AI 도우미에 연결할 수 없습니다. 다시 시도하세요.';

  @override
  String get aiBookingFailed => '여정을 예약할 수 없습니다.';

  @override
  String get conversationOpenFailed => '대화를 열 수 없습니다.';

  @override
  String get recording => '녹음 중...';

  @override
  String get sendOrCancelRecording => '녹음을 보내거나 취소하세요';

  @override
  String get aiMessageHint => 'SafeRide 도우미에게 메시지 보내기...';

  @override
  String get cancelVoice => '음성 취소';

  @override
  String get sendVoice => '음성 전송';

  @override
  String get voiceInput => '음성 입력';

  @override
  String vehicleSelectedByQuery(String query) {
    return '“$query”에 맞는 차량을 선택했습니다.';
  }

  @override
  String vehicleQueryNotFound(String query) {
    return '“$query”와 정확히 일치하는 차량을 찾지 못했습니다. 다시 선택하세요.';
  }

  @override
  String promoApplied(String code) {
    return '$code 코드를 적용했습니다.';
  }

  @override
  String promoUnavailable(String code) {
    return '$code 코드를 사용할 수 없습니다.';
  }

  @override
  String get conversationHistoryLoadFailed => '대화 기록을 불러올 수 없습니다.';

  @override
  String get deleteConversationQuestion => '대화를 삭제할까요?';

  @override
  String deleteConversationDescription(String title) {
    return '“$title” 및 관련 음성 파일이 영구 삭제됩니다.';
  }

  @override
  String get conversationDeleteFailed => '대화를 삭제할 수 없습니다. 다시 시도하세요.';

  @override
  String get conversationHistory => '대화 기록';

  @override
  String get noConversations => '대화가 없습니다.';

  @override
  String get deleteConversation => '대화 삭제';

  @override
  String get safeRideAssistantTitle => 'SafeRide 도우미';

  @override
  String get aiDisclaimer => 'AI는 실수할 수 있습니다 • 예약 전 확인하세요';

  @override
  String get newChat => '새 채팅';

  @override
  String get back => '뒤로';

  @override
  String get chooseVehicleQuestion => '어떤 차량을 이용하시겠어요?';

  @override
  String get chooseDiscountCode => '할인 코드 선택';

  @override
  String get confirmTrip => '여정 확인';

  @override
  String get yourVehicles => '내 차량';

  @override
  String get newVehicle => '새 차량';

  @override
  String get noVehicleForAiBooking => '차량이 없습니다. 예약을 계속하려면 차량을 추가하세요.';

  @override
  String get continueChooseDiscount => '할인 코드 선택 계속';

  @override
  String get noDiscountAvailable => '현재 사용 가능한 할인 코드가 없습니다.';

  @override
  String get noDiscount => '할인 코드 사용 안 함';

  @override
  String get continueWithoutDiscount => '코드 없이 계속';

  @override
  String usePromoCode(String code) {
    return '$code 코드 사용';
  }

  @override
  String get notUsed => '사용 안 함';

  @override
  String get confirmAndFindDriverAi => '확인 후 기사 찾기';

  @override
  String get aiWelcome =>
      '안녕하세요! SafeRide 이용이나 여정 준비를 도와드릴 수 있습니다.\n\n예: “FPT 대학교에서 떤선녓 공항까지 예약해 줘”.';

  @override
  String get slogan => '안전하고 믿을 수 있는 여정';

  @override
  String get phoneNumber => '전화번호';

  @override
  String get phoneHint => '전화번호 입력';

  @override
  String get continueOrRegister => '계속 / 가입';

  @override
  String get phoneRequired => '전화번호를 입력하세요';

  @override
  String get invalidPhone => '유효하지 않은 전화번호';

  @override
  String get sendOtpFailed => 'OTP를 보낼 수 없습니다. 전화번호를 확인하고 다시 시도하세요.';

  @override
  String get or => '또는';

  @override
  String get googleLoginFailed => 'Google 로그인 실패';

  @override
  String get continueAgreement => '계속하면 당사의 ';

  @override
  String get and => ' 및 ';

  @override
  String get agreementSuffix => '에 동의하게 됩니다.';

  @override
  String get otpTitle => 'OTP 인증';

  @override
  String get resendAfter => '재전송까지 ';

  @override
  String get resendOtp => 'OTP 재전송';

  @override
  String get otpResent => 'OTP를 다시 보냈습니다.';

  @override
  String get resendOtpFailed => 'OTP를 다시 보낼 수 없습니다.';

  @override
  String get otpRequired => 'OTP 6자리를 모두 입력하세요';

  @override
  String get invalidOtp => 'OTP가 올바르지 않거나 만료되었습니다';

  @override
  String get otpLockedPrefix => '잘못된 시도가 너무 많습니다. 다음 시간 후 다시 시도: ';

  @override
  String get otpAttemptsExceeded => 'OTP를 너무 많이 잘못 입력했습니다. 새 코드를 요청하세요.';

  @override
  String otpDescription(String phoneNumber) {
    return '$phoneNumber(으)로 전송된\n6자리 코드를 입력하세요.';
  }

  @override
  String get welcome => '환영합니다!';

  @override
  String get selectRoleQuestion => '어떤 역할로 시작하시겠어요?';

  @override
  String get customerRoleTitle => '고객입니다';

  @override
  String get customerRoleDescription => '안전한 차량을 빠르게 예약하고 여정을 실시간으로 추적하세요.';

  @override
  String get driverRoleTitle => '기사입니다';

  @override
  String get driverRoleDescription => '유연하게 일하고 수입을 늘리며 여정을 쉽게 관리하세요.';

  @override
  String get rememberRole => '선택 기억';

  @override
  String get completeProfile => '프로필 완성';

  @override
  String get changeAvatar => '프로필 사진 변경';

  @override
  String get verifiedPhone => '인증된 전화번호';

  @override
  String get updateInformationHint => '계속하려면 개인 정보를 업데이트하세요.';

  @override
  String get email => '이메일';

  @override
  String get saving => '저장 중...';

  @override
  String get saveAndContinue => '저장 후 계속';

  @override
  String get uploadAvatarFailed => '프로필 사진을 업로드할 수 없습니다.';

  @override
  String get updateProfileFailed => '정보를 업데이트할 수 없습니다.';

  @override
  String get invalidFullName => '유효한 성명을 입력하세요.';

  @override
  String get invalidEmail => '유효하지 않은 이메일 주소입니다.';

  @override
  String get emailAlreadyUsed => '다른 계정에서 이미 사용하는 이메일입니다.';

  @override
  String get phoneNumberAlreadyUsed => '다른 계정에서 이미 사용하는 전화번호입니다.';

  @override
  String get phoneNumberChangeRequiresVerification =>
      '이 화면에서는 연결된 전화번호를 변경할 수 없습니다.';

  @override
  String get phoneVerificationRequired => '전화번호를 추가하기 전에 OTP를 인증하세요.';

  @override
  String get appVersion => '앱 버전: 2.4.1';

  @override
  String get linkGoogleFailed => 'Google을 연결할 수 없습니다.';

  @override
  String get unlinkGoogleQuestion => 'Google 연결을 해제할까요?';

  @override
  String get unlinkGoogleDescription => '인증된 전화번호로 계속 로그인할 수 있습니다.';

  @override
  String get unlinkAccount => '연결 해제';

  @override
  String get unlinkGoogleFailed => 'Google 연결을 해제할 수 없습니다.';

  @override
  String get logoutFailed => '로그아웃할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get historyFilterAll => '전체';

  @override
  String get historyFilterCancelled => '취소됨';

  @override
  String get historyFilterBooked => '예약됨';

  @override
  String get cancelledByCustomer => '고객이 취소함';

  @override
  String get reported => '신고 완료';

  @override
  String get report => '신고';

  @override
  String get aiConversationFallback => '대화';

  @override
  String get chatConnectionFailed => '채팅에 연결할 수 없습니다.';

  @override
  String get chatMessageSendFailed => '메시지를 보낼 수 없습니다.';

  @override
  String get chatImageSendFailed => '이미지를 보낼 수 없습니다.';

  @override
  String get routeUpdated => 'SafeRide가 경로를 업데이트했습니다.';

  @override
  String get newTripMessage => '새 운행이 있습니다.';

  @override
  String get noInternetConnection => '인터넷에 연결되어 있지 않습니다';

  @override
  String get connectionLost => '연결 끊김';

  @override
  String get internetRestored => '인터넷 연결이 복원되었습니다';

  @override
  String get backOnline => '온라인 상태';

  @override
  String get calculating => '계산 중';

  @override
  String get viewTripAfterAccept => '수락 후 운행 상세 보기';

  @override
  String get customerCancelledDriverRequest => '고객이 기사 요청을 취소했습니다.';

  @override
  String get onlineFailed => '온라인 상태로 전환할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get acceptTripFailed => '운행을 수락할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get declineTripFailed => '운행을 거절할 수 없습니다. 다시 시도해 주세요.';

  @override
  String get tripRequestsLoadFailed => '운행 요청을 불러올 수 없습니다. 다시 시도해 주세요.';

  @override
  String get noDestination => '목적지 없음';

  @override
  String get expiresSoon => '곧 만료됨';

  @override
  String get evidencePhotoCountError => '증빙 사진을 1~3장 제공해 주세요.';

  @override
  String get activeTripLoadFailed => '현재 운행을 불러올 수 없습니다. 다시 시도해 주세요.';

  @override
  String ratingStars(int count) {
    return '별 $count개';
  }

  @override
  String get demoGpsMode => 'GPS 시뮬레이션 모드';

  @override
  String get serviceDisabled => '기기에서 위치 서비스를 켜 주세요.';

  @override
  String get permissionRequired => '픽업 위치를 확인하려면 SafeRide에 위치 권한이 필요합니다.';

  @override
  String get locationNotFound => '일치하는 위치를 찾을 수 없습니다.';

  @override
  String get destinationRequired => '목적지를 입력해 주세요.';

  @override
  String get statusLabel => '상태';

  @override
  String get selectPromotion => '프로모션 선택';

  @override
  String get enterPromoCode => '프로모션 코드 입력';

  @override
  String get apply => '적용';

  @override
  String get expired => '만료됨';

  @override
  String get statusOnline => '온라인';

  @override
  String get statusOffline => '오프라인';

  @override
  String get statusBusy => '운행 중';

  @override
  String get offerSent => '기사에게 전송됨';

  @override
  String get offerRejected => '거절됨';

  @override
  String get offerCustomerConfirmed => '고객 확인 완료';

  @override
  String get preTripSafetyTitle => '운행 전 차량 안전 점검';

  @override
  String get preTripSafetyDescription =>
      '운행 시작 전에 모든 항목을 확인하세요. 실패 기록도 감사 목적으로 보관됩니다.';

  @override
  String get brakeResponse => '브레이크 반응';

  @override
  String get frontRearLights => '전조등 및 후미등';

  @override
  String get turnSignals => '방향 지시등';

  @override
  String get visibleTires => '타이어 외관';

  @override
  String get dashboardWarning => '계기판 경고 없음';

  @override
  String get windshieldVisibility => '유리와 거울 시야';

  @override
  String get noMajorVisibleIssue => '중대한 외관 문제 없음';

  @override
  String get confirmSafetyCheck => '안전 점검 확인';

  @override
  String get allChecksRequired => '운행 시작 전에 모든 안전 항목을 통과해야 합니다.';

  @override
  String get safetyTermination => '안전 사유로 종료';

  @override
  String get safetyTerminationDescription =>
      '운행은 취소 상태로 유지됩니다. 프로모션은 사용되지 않으며 시작 후에는 부분 요금이 적용될 수 있습니다.';

  @override
  String get safetyTerminationReasonHint => '안전 위험을 설명하세요';

  @override
  String get captureSafetyEvidence => '증거 사진 촬영(선택)';

  @override
  String get retakePhoto => '다시 촬영';

  @override
  String get reportAccident => '사고 신고';

  @override
  String get accidentDescriptionHint => '사고 경위와 초기 피해를 설명하세요';

  @override
  String get createAccidentReport => '신고서 생성';

  @override
  String get accidentReported => '사고 신고서를 생성했습니다.';

  @override
  String get safetyTerminationFailed => '안전 사유로 운행을 종료할 수 없습니다.';

  @override
  String get preTripCheckFailed => '안전 점검을 제출할 수 없습니다.';

  @override
  String get riskProtectionCaseTitle => '사고 보호 사건';

  @override
  String get riskProtectionClaim => '보호 청구';

  @override
  String get riskProtectionEvidence => '증거';

  @override
  String get riskProtectionAssessment => '책임 평가';

  @override
  String get uploadAccidentEvidence => '증거 사진 추가';

  @override
  String get sendEvidencePhoto => '사진 보내기';

  @override
  String get evidencePreviewFailed => '선택한 이미지를 읽을 수 없습니다. 다시 선택해 주세요.';

  @override
  String get disputeLiability => '책임 재검토 요청';

  @override
  String get disputeReasonHint => '재검토가 필요한 이유를 입력하세요';

  @override
  String get liabilityDisputed => '재검토 요청을 제출했습니다.';

  @override
  String get accidentEvidenceUploaded => '증거 사진을 보냈습니다.';

  @override
  String get noAccidentEvidence => '업로드된 증거가 없습니다.';

  @override
  String get noProtectionClaim => '보호 청구가 아직 생성되지 않았습니다.';

  @override
  String get driverLiabilities => '나의 책임';

  @override
  String get noDriverLiabilities => '확정된 운전자 책임이 없습니다.';

  @override
  String get confirmedAmount => '확정 금액';

  @override
  String get paidAmount => '납부 금액';

  @override
  String get outstandingAmount => '미납 금액';

  @override
  String get attributableDamage => '운전자 귀책 적격 손해';

  @override
  String get recoveryHistory => '회수 내역';

  @override
  String get claimStatus => '청구 상태';

  @override
  String get insuranceCoverage => '보험 보장';

  @override
  String get riskFundCoverage => '위험 기금 보장';

  @override
  String get participantLiabilities => '당사자 책임';

  @override
  String get accidentStatus => '사고 상태';

  @override
  String get accidentCategory => '사고 유형';

  @override
  String get accidentOccurredAt => '발생 시각';

  @override
  String get safetyReportTitle => '안전 사고 신고';

  @override
  String get unsafeCustomer => '위험한 고객';

  @override
  String get vehicleIssue => '차량 문제';

  @override
  String get safetyReasonCode => '사유';

  @override
  String get safetyReportDescription => '상황을 설명하세요';

  @override
  String get requestSosEscalation => 'SOS 에스컬레이션 요청';

  @override
  String get requestSosEscalationHint => '현재 위치를 보내고 지속 SOS 알림을 생성합니다';

  @override
  String get safetyReportSubmitted => '안전 사고 신고를 제출했습니다.';

  @override
  String get safetyReportFailed => '안전 사고 신고를 제출할 수 없습니다. 다시 시도하세요.';

  @override
  String get vehicleFaultType => '차량 결함 유형';

  @override
  String get otherVehicleFault => '기타 차량 결함';

  @override
  String get optionalEvidence => '증거 자료(선택)';

  @override
  String get vehicleInsurance => '보험';

  @override
  String get addInsurance => '보험 추가';

  @override
  String get insuranceLoadFailed => '보험 정보를 불러올 수 없습니다. 다시 시도해 주세요.';

  @override
  String get insuranceUpdateFailed => '보험을 업데이트할 수 없습니다.';

  @override
  String get deleteInsuranceQuestion => '보험 계약을 삭제할까요?';

  @override
  String get policyNumber => '증권 번호';

  @override
  String get optionalInsuranceEmpty => '보험은 선택 사항입니다. 이 차량에는 계약이 없습니다.';

  @override
  String get addInsurancePolicy => '보험 계약 추가';

  @override
  String get editInsurancePolicy => '보험 계약 수정';

  @override
  String get insuranceType => '보험 유형';

  @override
  String get mandatoryTplInsurance => '의무 제3자 책임보험';

  @override
  String get physicalDamageInsurance => '차량 손해';

  @override
  String get insuranceProvider => '보험사';

  @override
  String get effectiveDate => '시작일';

  @override
  String get insuranceCoverageLimit => '보장 한도';

  @override
  String get insuranceDeductible => '자기부담금';

  @override
  String get optionalDocumentUrl => '문서 URL(선택)';

  @override
  String get optionalInsuranceHint =>
      '보험은 선택 사항입니다. 생성 또는 수정하면 직원 확인을 위해 PENDING 상태로 돌아갑니다.';

  @override
  String get endTripReasonTitle => '운행 종료 사유';

  @override
  String get endTripReasonDescription =>
      '정확한 사유를 선택하세요. 안전 종료는 별도의 Risk Protection 절차를 사용해야 합니다.';

  @override
  String get normalCompletionReason => '목적지 도착';

  @override
  String get normalCompletionReasonDescription => '예약된 운임을 적용합니다.';

  @override
  String get customerRequestedStopReason => '고객의 조기 종료 요청';

  @override
  String get customerRequestedStopReasonDescription =>
      '예약 경로 진행률과 최소 서비스 운임을 사용합니다.';

  @override
  String get driverUnableToContinueReason => '기사가 계속 운행할 수 없음';

  @override
  String get startedByMistakeReason => '실수로 운행 시작';

  @override
  String get riskStatusReported => '접수됨';

  @override
  String get riskStatusEvidenceCollection => '증거 수집 중';

  @override
  String get riskStatusUnderReview => '검토 중';

  @override
  String get riskStatusLiabilityPending => '책임 판정 대기';

  @override
  String get riskStatusSettlement => '보호 처리 중';

  @override
  String get riskStatusClosed => '종료됨';

  @override
  String get riskStatusRejected => '거절됨';

  @override
  String get riskCategoryDriverInjury => '기사 부상';

  @override
  String get riskCategoryCustomerVehicleDamage => '고객 차량 손해';

  @override
  String get riskCategoryThirdPartyDamage => '제3자 손해';

  @override
  String get riskCategoryMultiple => '복수 손해';

  @override
  String get riskFaultNoFault => '과실 없음';

  @override
  String get riskFaultOrdinary => '일반 과실';

  @override
  String get riskFaultGross => '중과실';

  @override
  String get riskFaultIntentional => '고의 행위';

  @override
  String get riskAssessmentDraft => '초안';

  @override
  String get riskAssessmentPendingConfirmation => '확인 대기';

  @override
  String get riskAssessmentConfirmed => '확인됨';

  @override
  String get riskAssessmentDisputed => '재검토 중';

  @override
  String get riskClaimApproved => '승인됨';

  @override
  String get riskClaimPendingFunding => '자금 지급 대기';

  @override
  String get riskClaimFunded => '자금 지급됨';

  @override
  String get riskClaimRecovery => '회수 진행 중';

  @override
  String get riskClaimSettled => '정산됨';

  @override
  String get riskLiabilityPartiallyPaid => '일부 납부됨';

  @override
  String get riskLiabilityPaid => '납부됨';

  @override
  String get riskLiabilityWaived => '면제됨';

  @override
  String get riskRoleDriver => '기사';

  @override
  String get riskRoleCustomer => '고객';

  @override
  String get riskRoleThirdParty => '제3자';

  @override
  String get riskRoleVehicle => '차량';

  @override
  String get riskRoleObjective => '객관적 요인';

  @override
  String get riskReasonDistracting => '주의 산만 유발';

  @override
  String get riskReasonViolent => '폭력적 행동';

  @override
  String get riskReasonInterferingVehicle => '차량 조작 방해';

  @override
  String get riskReasonUnsafeRequest => '안전하지 않은 요구';

  @override
  String get riskReasonOther => '기타 사유';

  @override
  String get riskInsurancePending => '직원 확인 대기';

  @override
  String get riskInsuranceVerified => '확인됨';

  @override
  String get riskInsuranceExpired => '만료됨';

  @override
  String get riskInsuranceOther => '기타 보험';

  @override
  String get riskIncidentInformation => '사고 정보';

  @override
  String get riskResponsibilityResult => '책임 판정 결과';

  @override
  String get riskProtectionOutcome => '보호 처리 결과';

  @override
  String get riskEligibleDamage => '보호 대상 손해';

  @override
  String get mandatoryTplExplanation =>
      '의무 제3자 책임보험은 주로 제3자를 보호하며 고객 차량 손해를 자동으로 보장하지 않습니다.';

  @override
  String get physicalDamageExplanation =>
      '차량 손해보험은 확인된 계약 조건에 따라 고객 차량 손해에 적용될 수 있습니다.';

  @override
  String get insurerNoGuarantee => '계약 저장이 보험사의 지급 승인을 보장하지는 않습니다.';

  @override
  String get documentUrlDeferredHint =>
      '현재는 문서 링크만 저장합니다. 신뢰할 수 있는 링크를 사용하세요. 안전한 공용 저장소가 마련되면 직접 업로드를 추가합니다.';
}
