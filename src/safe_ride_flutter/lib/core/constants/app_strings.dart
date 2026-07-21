abstract final class AppStrings {
  static const appName = 'SafeRide';
  static const confirm = 'Xác nhận';
  static const cancel = 'Hủy';
  static const genericError = 'Đã xảy ra lỗi. Vui lòng thử lại.';
}

abstract final class AuthStrings {
  static const slogan = 'Chuyến đi an toàn, tin cậy tuyệt đối';
  static const phoneNumber = 'Số điện thoại';
  static const phoneHint = 'Nhập số điện thoại';
  static const vietnamPhonePrefix = '+84 ';
  static const continueOrRegister = 'Tiếp tục / Đăng ký';
  static const phoneRequired = 'Vui lòng nhập số điện thoại';
  static const invalidPhone = 'Số điện thoại không hợp lệ';
  static const sendOtpFailed =
      'Không thể gửi OTP. Kiểm tra API hoặc số điện thoại.';
  static const or = 'HOẶC';
  static const google = 'Google';
  static const googleLoginFailed = 'Đăng nhập Google thất bại';
  static const continueAgreement = 'Bằng việc tiếp tục, bạn đồng ý với ';
  static const termsOfService = 'Điều khoản dịch vụ';
  static const and = ' và ';
  static const privacyPolicy = 'Chính sách bảo mật';
  static const agreementSuffix = ' của chúng tôi.';
  static const otpTitle = 'Xác thực mã OTP';
  static const resendAfter = 'Gửi lại sau ';
  static const resendTimer = '00:57';
  static const resendOtp = 'Gửi lại OTP';
  static const otpResent = 'Đã gửi lại OTP.';
  static const resendOtpFailed = 'Không thể gửi lại OTP.';
  static const otpRequired = 'Vui lòng nhập đủ 6 số OTP';
  static const invalidOtp = 'OTP không đúng hoặc đã hết hạn';
  static const otpLockedPrefix = 'Bạn nhập sai OTP quá nhiều lần. Thử lại sau ';
  static const otpAttemptsExceeded =
      'Bạn đã nhập sai OTP quá nhiều lần. Vui lòng yêu cầu mã mới.';

  static String otpDescription(String phoneNumber) =>
      'Vui lòng nhập mã gồm 6 chữ số đã được\ngửi đến $phoneNumber.';
}

abstract final class OnboardingStrings {
  static const welcome = 'Chào mừng bạn!';
  static const selectRoleQuestion = 'Bạn muốn bắt đầu với vai trò nào?';
  static const customerTitle = 'Tôi là Khách hàng';
  static const customerDescription =
      'Đặt xe nhanh chóng, an toàn và theo dõi hành trình trực tiếp.';
  static const driverTitle = 'Tôi là Tài xế';
  static const driverDescription =
      'Nhận việc linh hoạt, tăng thu nhập và quản lý chuyến đi dễ dàng.';
  static const rememberRole = 'Ghi nhớ lựa chọn cho lần sau';
  static const continueLabel = 'Tiếp tục';
}

abstract final class HomeStrings {
  static const historyPage = 'History Page';
  static const walletPage = 'Wallet Page';
  static const home = 'Trang chủ';
  static const activity = 'Hoạt động';
  static const wallet = 'Ví';
  static const account = 'Tài khoản';
  static const destinationQuestion = 'Bạn muốn đi đâu hôm nay?';
  static const bookNow = 'Đặt ngay';
  static const bookNowDescription = 'Tìm tài xế phù hợp cho chuyến đi';
  static const scheduleBooking = 'Đặt lịch trước';
  static const history = 'Lịch sử';
  static const myVehicles = 'Xe của tôi';
  static const promotions = 'Khuyến mãi';
  static const sos = 'SOS';
  static const recentTrips = 'Chuyến đi gần đây';
  static const defaultUser = 'Người dùng SafeRide';
  static const friendlyUser = 'bạn';
  static const defaultInitials = 'SR';
  static const recentPickup = '123 Nguyễn Văn Linh, Q.7';
  static const recentDestination = 'Sân bay Tân Sơn Nhất';
  static const recentTime = 'Hôm qua, 14:30';
  static const promotionTitle = 'Giảm 20% cho\nchuyến đi Tối';
  static const promotionCode = 'SAFENIGHT';

  static String greeting(String name) => 'Chào $name,';
}

abstract final class PromotionStrings {
  static const selectPromotion = 'Chọn mã khuyến mãi';
  static const enterPromoCode = 'Nhập mã khuyến mãi';
  static const apply = 'Áp dụng';
  static const useNow = 'Dùng\nngay';
  static const expiresToday = 'Hết hạn hôm nay';
  static const expiresAfter = 'Hết hạn sau';
  static const days = 'ngày';
}

abstract final class HistoryStrings {
  static const tripHistory = 'Lịch sử chuyến đi';
  static const all = 'Tất cả';
  static const completed = 'Hoàn thành';
  static const cancelled = 'Đã hủy';
  static const rebook = 'Đặt lại';
  static const report = 'Báo cáo';
  static const reported = 'Đã báo cáo';
  static const cancelledByCustomer = 'Đã hủy bởi khách hàng';
  static const driverRating = '★';
  static const booked = 'Đã đặt';
}

abstract final class BookingStrings {
  static const routeSearch = 'Tìm tuyến đường';
  static const locatingCurrentPosition = 'Đang xác định vị trí hiện tại...';
  static const destination = 'Điểm đến';
  static const searchDestination = 'Tìm điểm đến';
  static const locationHistory = 'LỊCH SỬ ĐỊA ĐIỂM';
  static const airport = 'Sân bay Tân Sơn Nhất';
  static const recentAddress = '123 Nguyễn Văn Linh';
  static const office = 'Văn phòng';
  static const selectPickupDate = 'Chọn ngày đón';
  static const selectPickupTimeHelp = 'Chọn giờ đón';
  static const invalidSchedule =
      'Thời gian đặt trước phải cách hiện tại ít nhất 30 phút.';
  static const sessionExpired =
      'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  static const selectServiceAndVehicle = 'Vui lòng chọn dịch vụ và xe.';
  static const noBookableVehicles =
      'Bạn chưa có xe hợp lệ để đặt chuyến. Vui lòng thêm xe trước khi đặt.';
  static const selectPickupTimeRequired = 'Vui lòng chọn thời gian đón.';
  static const bookingFailed =
      'Không thể đặt chuyến lúc này. Vui lòng thử lại.';
  static const bookingSuccess = 'Đặt chuyến thành công';
  static const backToHome = 'Về trang chủ';
  static const selectVehicle = 'Chọn xe của bạn';
  static const specialRequest = 'Yêu cầu đặc biệt (không bắt buộc)';
  static const fareCalculationNote =
      'Giá cước sẽ được backend tính từ quãng đường và thời gian thực tế.';
  static const confirmNow = 'Xác nhận đặt ngay';
  static const confirmScheduled = 'Xác nhận đặt trước';
  static const pickupLabel = 'ĐIỂM ĐÓN';
  static const destinationLabel = 'ĐIỂM ĐẾN';
  static const selectPickupTime = 'Chọn thời gian đón';
  static const tripService = 'Theo chuyến';
  static const tripServiceDescription = 'Tính giá theo quãng đường';
  static const hourlyService = 'Theo giờ';
  static const hourlyServiceDescription = 'Tính giá theo thời gian dự kiến';
  static const airportAddress = 'Sân bay Tân Sơn Nhất, Phường 2, Tân Bình';
  static const recentFullAddress =
      '123 Nguyễn Văn Linh, Quận 7, Thành phố Hồ Chí Minh';
  static const officeAddress = 'Tòa nhà Bitexco, Quận 1, Thành phố Hồ Chí Minh';
  static const demoVehicleName = 'Toyota Vios 2020';
  static const demoPlateNumber = '30F - 987.65';
  static const demoVehicleColor = 'Trắng';
  static const searchingDriver = 'Đang tìm tài xế cho bạn...';
  static const estimatedWaitTime = 'Thời gian chờ dự kiến: ~2 phút';
  static const cancelBooking = 'Huỷ chuyến';

  static String plateNumber(String value) => 'Biển số: $value';
  static String vehicleColor(String value) => 'Màu: $value';
}

abstract final class ProfileStrings {
  static const profileAndSettings = 'Hồ sơ & Cài đặt';
  static const switchToDriver = 'Chuyển sang chế độ Tài xế';
  static const startReceivingTrips = 'Bắt đầu nhận chuyến đi';
  static const accountSection = 'TÀI KHOẢN';
  static const editProfile = 'Chỉnh sửa hồ sơ';
  static const linkedAccounts = 'Tài khoản liên kết';
  static const phoneLogin = 'Số điện thoại';
  static const googleAccount = 'Google';
  static const linked = 'Đã liên kết';
  static const notLinked = 'Chưa liên kết';
  static const linkAccount = 'Liên kết';
  static const unlinkAccount = 'Hủy liên kết';
  static const unlinkGoogleQuestion = 'Hủy liên kết Google?';
  static const unlinkGoogleDescription =
      'Bạn vẫn có thể đăng nhập bằng số điện thoại đã xác thực.';
  static const linkGoogleFailed = 'Không thể liên kết Google.';
  static const unlinkGoogleFailed = 'Không thể hủy liên kết Google.';
  static const linkedAccountsLoadFailed =
      'Không thể tải trạng thái tài khoản liên kết.';
  static const appAndNotifications = 'ỨNG DỤNG & THÔNG BÁO';
  static const notificationSettings = 'Cài đặt thông báo';
  static const language = 'Ngôn ngữ';
  static const vietnamese = 'Tiếng Việt';
  static const darkMode = 'Chế độ tối';
  static const supportAndLegal = 'HỖ TRỢ & PHÁP LÝ';
  static const helpCenter = 'Trung tâm trợ giúp';
  static const appVersion = 'Phiên bản ứng dụng: 2.4.1';
  static const logout = 'Đăng xuất';
  static const logoutQuestion = 'Đăng xuất?';
  static const logoutDescription =
      'Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng?';
  static const logoutFailed = 'Không thể đăng xuất. Vui lòng thử lại.';
  static const completeProfile = 'Hoàn thiện thông tin';
  static const changeAvatar = 'Thay đổi ảnh đại diện';
  static const verifiedPhone = 'Số điện thoại đã xác minh';
  static const updateInformationHint =
      'Vui lòng cập nhật thông tin cá nhân để tiếp tục.';
  static const fullName = 'Họ và tên';
  static const email = 'Email';
  static const saving = 'Đang lưu...';
  static const saveAndContinue = 'Lưu và tiếp tục';
  static const invalidFullName = 'Vui lòng nhập họ và tên hợp lệ.';
  static const invalidEmail = 'Địa chỉ email không hợp lệ.';
  static const emailAlreadyUsed = 'Email đã được sử dụng bởi tài khoản khác.';
  static const phoneNumberAlreadyUsed =
      'Số điện thoại đã được sử dụng bởi tài khoản khác.';
  static const phoneNumberChangeRequiresVerification =
      'Không thể thay đổi số điện thoại đã liên kết tại màn hình này.';
  static const phoneVerificationRequired =
      'Vui lòng xác thực OTP trước khi thêm số điện thoại.';
  static const uploadAvatarFailed = 'Không thể tải ảnh đại diện lên.';
  static const updateProfileFailed = 'Không thể cập nhật thông tin.';
}

abstract final class LocationStrings {
  static const serviceDisabled = 'Vui lòng bật dịch vụ vị trí trên thiết bị.';
  static const permissionRequired =
      'SafeRide cần quyền vị trí để xác định điểm đón.';
  static const destinationRequired = 'Vui lòng nhập điểm đến.';
  static const locationNotFound = 'Không tìm thấy địa điểm phù hợp.';
  static const currentLocation = 'Vị trí hiện tại';
}

abstract final class AppConfig {
  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://192.168.1.19:5026/api/',
  );
  // https://safe-ride-ai.onrender.com
  // http://192.168.1.19:5026
  static const forceWebSockets = bool.fromEnvironment(
    'FORCE_WEBSOCKETS',
    defaultValue: true, // Dev only or config-based
  );
  static const fontFamily = 'SFProDisplay';
  static const logoUrl =
      'https://res.cloudinary.com/dj7y3ikck/image/upload/v1781487774/logo_poxclo.png';
  static const googleLogoUrl =
      'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQ2sSeQqjaUTuZ3gRgkKjidpaipF_l6s72lBw&s';
}

abstract final class ApiEndpoints {
  static const sendOtp = '/auth/send-otp';
  static const verifyOtp = '/auth/verify-otp';
  static const googleLogin = '/auth/google-login';
  static const refreshToken = '/auth/refresh-token';
  static const me = '/auth/me';
  static const profile = '/auth/profile';
  static const profilePhoneSendOtp = '/auth/profile/phone/send-otp';
  static const profilePhoneVerifyOtp = '/auth/profile/phone/verify-otp';
  static const linkedAccounts = '/auth/linked-accounts';
  static const linkedGoogleAccount = '/auth/linked-accounts/google';
  static const profileAvatar = '/auth/profile/avatar';
  static const logout = '/auth/logout';
  static const bookings = '/bookings';
  static const bookingHistory = '/bookings/history';
  static const activeBooking = '/bookings/active';
  static const notifications = '/notifications';
  static const availablePromotions = '/promotions/available';
  static const bookingCatalog = '/bookings/catalog';
  static const bookingEstimate = '/bookings/estimate';
  static const driverOnline = '/drivers/online';
  static const driverOffline = '/drivers/offline';
  static const driverLocation = '/drivers/location';
  static const driverActiveTrip = '/drivers/trips/active';
  static const driverWallet = '/drivers/wallet';
  static const driverWithdrawals = '/drivers/wallet/withdrawals';
  static const driverTripRequests = '/drivers/trip-requests';
  static const nearbyDrivers = '/drivers/nearby';
  static String acceptDriverOffer(int offerId) =>
      '/drivers/offers/$offerId/accept';
  static String rejectDriverOffer(int offerId) =>
      '/drivers/offers/$offerId/reject';
  static String confirmDriverOffer(int bookingId, int offerId) =>
      '/bookings/$bookingId/confirm-driver-offer/$offerId';
  static String tripStatus(int tripId) => '/trips/$tripId/status';
  static String completeTrip(int tripId) => '/trips/$tripId/complete';
  static String createDriverTripQrPayment(int tripId) =>
      '/payments/driver/trips/$tripId/qr';
  static String driverTripPaymentStatus(int tripId) =>
      '/payments/driver/trips/$tripId/status';
  static String customerTripPaymentStatus(int tripId) =>
      '/payments/trips/$tripId/status';
  static String confirmDriverTripCashPayment(int tripId) =>
      '/payments/driver/trips/$tripId/cash';
  static String submitTripRating(int tripId) =>
      '/feedbacks/trips/$tripId/rating';
  static String submitTripReport(int bookingId) =>
      '/feedbacks/bookings/$bookingId/reports';
  static String notificationRead(int notificationId) =>
      '/notifications/$notificationId/read';
  static const identityVerificationDocuments =
      '/identity-verification/documents';
  static String endTrip(int tripId) => '/trips/$tripId/end';
  static String customerReturnConfirmation(int tripId) =>
      '/trips/$tripId/return-confirmation/customer';
  static String driverReturnConfirmation(int tripId) =>
      '/trips/$tripId/return-confirmation/driver';
}

abstract final class ApiKeys {
  static const authorization = 'Authorization';
  static const bearer = 'Bearer';
  static const userId = 'userId';
  static const phoneNumber = 'phoneNumber';
  static const otpCode = 'otpCode';
  static const deviceId = 'deviceId';
  static const deviceName = 'deviceName';
  static const googleIdToken = 'googleIdToken';
  static const fullName = 'fullName';
  static const email = 'email';
  static const file = 'file';
  static const frontImage = 'frontImage';
  static const backImage = 'backImage';
  static const documentType = 'documentType';
  static const documentNumber = 'documentNumber';
  static const frontImageUrl = 'frontImageUrl';
  static const backImageUrl = 'backImageUrl';
  static const fileUrl = 'fileUrl';
  static const kycStatus = 'kycStatus';
  static const rejectionReason = 'rejectionReason';
  static const issueDate = 'issueDate';
  static const expiryDate = 'expiryDate';
  static const avatarUrl = 'avatarUrl';
  static const refreshToken = 'refreshToken';
  static const accessToken = 'accessToken';
  static const roles = 'roles';
  static const lastSelectedRole = 'lastSelectedRole';
  static const message = 'message';
  static const nextStep = 'nextStep';
  static const sessionMode = 'sessionMode';
  static const reloginRequiredAfterTrip = 'reloginRequiredAfterTrip';
  static const continuationTripId = 'continuationTripId';
  static const continuationAbsoluteExpiresAt = 'continuationAbsoluteExpiresAt';
  static const detail = 'detail';
  static const code = 'code';
  static const retryAfterSeconds = 'retryAfterSeconds';
  static const phoneNumberConfirmed = 'phoneNumberConfirmed';
  static const phoneLinked = 'phoneLinked';
  static const googleLinked = 'googleLinked';
  static const googleEmail = 'googleEmail';
  static const bookingId = 'bookingId';
  static const bookingType = 'bookingType';
  static const bookingStatus = 'bookingStatus';
  static const scheduledAt = 'scheduledAt';
  static const estimatedFare = 'estimatedFare';
  static const estimatedDistanceKm = 'estimatedDistanceKm';
  static const estimatedDurationMinutes = 'estimatedDurationMinutes';
  static const encodedPolyline = 'encodedPolyline';
  static const actualDistanceKm = 'actualDistanceKm';
  static const actualDurationMinutes = 'actualDurationMinutes';
  static const actualEncodedPolyline = 'actualEncodedPolyline';
  static const tripEndedAt = 'tripEndedAt';
  static const arrivalPolyline = 'arrivalPolyline';
  static const driverOffer = 'driverOffer';
  static const vehicle = 'vehicle';
  static const payment = 'payment';
  static const paymentId = 'paymentId';
  static const paymentMethod = 'paymentMethod';
  static const paymentStatus = 'paymentStatus';
  static const amount = 'amount';
  static const currency = 'currency';
  static const paidAt = 'paidAt';
  static const tripStatus = 'tripStatus';
  static const tripId = 'tripId';
  static const address = 'address';
  static const latitude = 'latitude';
  static const longitude = 'longitude';
  static const clientTimestampUtc = 'clientTimestampUtc';
  static const sequence = 'sequence';
  static const accuracyMeters = 'accuracyMeters';
  static const speedMetersPerSecond = 'speedMetersPerSecond';
  static const offerId = 'offerId';
  static const driverId = 'driverId';
  static const driverLatitude = 'driverLatitude';
  static const driverLongitude = 'driverLongitude';
  static const driverName = 'driverName';
  static const driverAvatarUrl = 'driverAvatarUrl';
  static const rating = 'rating';
  static const ratingScore = 'ratingScore';
  static const comment = 'comment';
  static const subject = 'subject';
  static const description = 'description';
  static const tripCount = 'tripCount';
  static const experienceYears = 'experienceYears';
  static const licenseClass = 'licenseClass';
  static const expiresAt = 'expiresAt';
  static const offerStatus = 'offerStatus';
  static const customerConfirmRemainingSeconds =
      'customerConfirmRemainingSeconds';
  static const estimatedHours = 'estimatedHours';
  static const vehicleId = 'vehicleId';
  static const serviceTypeId = 'serviceTypeId';
  static const pickupAddress = 'pickupAddress';
  static const pickupLatitude = 'pickupLatitude';
  static const pickupLongitude = 'pickupLongitude';
  static const destinationAddress = 'destinationAddress';
  static const destinationLatitude = 'destinationLatitude';
  static const destinationLongitude = 'destinationLongitude';
  static const specialRequest = 'specialRequest';
  static const promotionCode = 'promotionCode';
  static const originalFare = 'originalFare';
  static const discountAmount = 'discountAmount';
  static const finalFare = 'finalFare';
  static const vehicleReturnedConfirmed = 'vehicleReturnedConfirmed';
  static const currentSearchRadiusKm = 'currentSearchRadiusKm';
  static const estimatedRemainingSeconds = 'estimatedRemainingSeconds';
  static const matchingMessage = 'matchingMessage';
  static const userName = 'userName';
  static const recentTrips = 'recentTrips';
  static const pickup = 'pickup';
  static const destination = 'destination';
  static const time = 'time';
}

abstract final class AppValues {
  static const roleCustomer = 'customer';
  static const roleDriver = 'driver';
  static const bookingNow = 'Now';
  static const bookingScheduled = 'Scheduled';
  static const completeProfile = 'completeProfile';
  static const selectRole = 'selectRole';
  static const success = 'success';
  static const successVietnamese = 'thành công';
  static const multipartFormData = 'multipart/form-data';
  static const pngExtension = 'png';
  static const webpExtension = 'webp';
  static const pngMimeType = 'image/png';
  static const webpMimeType = 'image/webp';
  static const jpegMimeType = 'image/jpeg';
  static const vietnamCountryCode = '+84';
}

abstract final class StorageKeys {
  static const accessToken = 'auth.access_token';
  static const refreshToken = 'auth.refresh_token';
  static const userProfile = 'auth.user_profile';
  static const reloginRequired = 'auth.relogin_required';
  static const sessionMode = 'auth.session_mode';
  static const continuationTripId = 'auth.continuation_trip_id';
  static const continuationAbsoluteExpiresAt =
      'auth.continuation_absolute_expires_at';
  static const deviceId = 'device.id';
}

abstract final class DeviceStrings {
  static const android = 'SafeRide Android';
  static const ios = 'SafeRide iOS';
  static const macos = 'SafeRide macOS';
  static const windows = 'SafeRide Windows';
  static const linux = 'SafeRide Linux';
  static const fuchsia = 'SafeRide Fuchsia';
  static const idPrefix = 'saferide';
}

abstract final class DriverReturnEvidenceStrings {
  static const pageTitle = 'Xác nhận trả xe';
  static const instruction =
      'Chụp hoặc chọn ảnh bằng chứng bàn giao xe cho khách. Cần 1–3 ảnh.';
  static const addPhoto = 'Thêm ảnh bằng chứng';
  static const tapToAdd = 'Nhấn để thêm ảnh';
  static const noteLabel = 'Ghi chú (tùy chọn)';
  static const noteHint = 'Nhập ghi chú nếu cần...';
  static const submitButton = 'Xác nhận trả xe';
  static const submitting = 'Đang gửi...';
  static const successTitle = 'Xác nhận thành công';
  static const successMessage =
      'Đã ghi nhận trả xe. Chuyến đi đang được hoàn tất.';
  static const done = 'Hoàn tất';
  static const errorMinPhoto = 'Cần ít nhất 1 ảnh bằng chứng.';
  static const errorMaxPhoto = 'Không được tải lên quá 3 ảnh.';
  static const errorUploadFailed = 'Không thể gửi bằng chứng. Thử lại.';
  static const camera = 'Chụp ảnh';
  static const gallery = 'Chọn từ thư viện';
  static const removePhoto = 'Xóa ảnh';
  static const confirmRemove = 'Bạn có muốn xóa ảnh này không?';
  static const photoOf = 'Ảnh';
  static const waitingReturnLabel = 'Chờ xác nhận trả xe';
  static const returnConfirmedLabel = 'Đã xác nhận trả xe';
  static const endTripButton = 'Kết thúc chuyến';
}
