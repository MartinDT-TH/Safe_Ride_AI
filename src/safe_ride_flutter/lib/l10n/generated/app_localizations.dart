import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_en.dart';
import 'app_localizations_ja.dart';
import 'app_localizations_ko.dart';
import 'app_localizations_vi.dart';
import 'app_localizations_zh.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'generated/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('en'),
    Locale('ja'),
    Locale('ko'),
    Locale('vi'),
    Locale('zh'),
  ];

  /// No description provided for @appName.
  ///
  /// In vi, this message translates to:
  /// **'SafeRide'**
  String get appName;

  /// No description provided for @language.
  ///
  /// In vi, this message translates to:
  /// **'Ngôn ngữ'**
  String get language;

  /// No description provided for @chooseLanguage.
  ///
  /// In vi, this message translates to:
  /// **'Chọn ngôn ngữ'**
  String get chooseLanguage;

  /// No description provided for @vietnamese.
  ///
  /// In vi, this message translates to:
  /// **'Tiếng Việt'**
  String get vietnamese;

  /// No description provided for @english.
  ///
  /// In vi, this message translates to:
  /// **'English'**
  String get english;

  /// No description provided for @korean.
  ///
  /// In vi, this message translates to:
  /// **'한국어'**
  String get korean;

  /// No description provided for @japanese.
  ///
  /// In vi, this message translates to:
  /// **'日本語'**
  String get japanese;

  /// No description provided for @simplifiedChinese.
  ///
  /// In vi, this message translates to:
  /// **'简体中文'**
  String get simplifiedChinese;

  /// No description provided for @profileAndSettings.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ & Cài đặt'**
  String get profileAndSettings;

  /// No description provided for @switchToDriver.
  ///
  /// In vi, this message translates to:
  /// **'Chuyển sang chế độ Tài xế'**
  String get switchToDriver;

  /// No description provided for @startReceivingTrips.
  ///
  /// In vi, this message translates to:
  /// **'Bắt đầu nhận chuyến đi'**
  String get startReceivingTrips;

  /// No description provided for @accountSection.
  ///
  /// In vi, this message translates to:
  /// **'TÀI KHOẢN'**
  String get accountSection;

  /// No description provided for @editProfile.
  ///
  /// In vi, this message translates to:
  /// **'Chỉnh sửa hồ sơ'**
  String get editProfile;

  /// No description provided for @linkedAccounts.
  ///
  /// In vi, this message translates to:
  /// **'Tài khoản liên kết'**
  String get linkedAccounts;

  /// No description provided for @registerAsDriver.
  ///
  /// In vi, this message translates to:
  /// **'Đăng ký tài xế'**
  String get registerAsDriver;

  /// No description provided for @linked.
  ///
  /// In vi, this message translates to:
  /// **'Đã liên kết'**
  String get linked;

  /// No description provided for @notLinked.
  ///
  /// In vi, this message translates to:
  /// **'Chưa liên kết'**
  String get notLinked;

  /// No description provided for @appAndNotifications.
  ///
  /// In vi, this message translates to:
  /// **'ỨNG DỤNG & THÔNG BÁO'**
  String get appAndNotifications;

  /// No description provided for @notificationSettings.
  ///
  /// In vi, this message translates to:
  /// **'Cài đặt thông báo'**
  String get notificationSettings;

  /// No description provided for @darkMode.
  ///
  /// In vi, this message translates to:
  /// **'Chế độ tối'**
  String get darkMode;

  /// No description provided for @supportAndLegal.
  ///
  /// In vi, this message translates to:
  /// **'HỖ TRỢ & PHÁP LÝ'**
  String get supportAndLegal;

  /// No description provided for @helpCenter.
  ///
  /// In vi, this message translates to:
  /// **'Trung tâm trợ giúp'**
  String get helpCenter;

  /// No description provided for @privacyPolicy.
  ///
  /// In vi, this message translates to:
  /// **'Chính sách bảo mật'**
  String get privacyPolicy;

  /// No description provided for @termsOfService.
  ///
  /// In vi, this message translates to:
  /// **'Điều khoản dịch vụ'**
  String get termsOfService;

  /// No description provided for @logout.
  ///
  /// In vi, this message translates to:
  /// **'Đăng xuất'**
  String get logout;

  /// No description provided for @logoutQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Đăng xuất?'**
  String get logoutQuestion;

  /// No description provided for @logoutDescription.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng?'**
  String get logoutDescription;

  /// No description provided for @cancel.
  ///
  /// In vi, this message translates to:
  /// **'Hủy'**
  String get cancel;

  /// No description provided for @cannotSwitchToDriver.
  ///
  /// In vi, this message translates to:
  /// **'Bạn không thể chuyển sang chế độ Tài xế khi đang có chuyến đi hoạt động.'**
  String get cannotSwitchToDriver;

  /// No description provided for @cannotSwitchToCustomer.
  ///
  /// In vi, this message translates to:
  /// **'Bạn không thể chuyển sang chế độ Khách hàng khi đang có chuyến đi hoạt động.'**
  String get cannotSwitchToCustomer;

  /// No description provided for @tripNotFound.
  ///
  /// In vi, this message translates to:
  /// **'Không tìm thấy chuyến đi.'**
  String get tripNotFound;

  /// No description provided for @sessionExpired.
  ///
  /// In vi, this message translates to:
  /// **'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'**
  String get sessionExpired;

  /// No description provided for @genericError.
  ///
  /// In vi, this message translates to:
  /// **'Đã xảy ra lỗi. Vui lòng thử lại.'**
  String get genericError;

  /// No description provided for @statusPending.
  ///
  /// In vi, this message translates to:
  /// **'Đang chờ'**
  String get statusPending;

  /// No description provided for @statusDriverArriving.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế đang đến'**
  String get statusDriverArriving;

  /// No description provided for @statusInProgress.
  ///
  /// In vi, this message translates to:
  /// **'Đang thực hiện'**
  String get statusInProgress;

  /// No description provided for @statusCompleted.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn thành'**
  String get statusCompleted;

  /// No description provided for @statusCancelled.
  ///
  /// In vi, this message translates to:
  /// **'Đã hủy'**
  String get statusCancelled;

  /// No description provided for @notifications.
  ///
  /// In vi, this message translates to:
  /// **'Thông báo'**
  String get notifications;

  /// No description provided for @notificationsLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải thông báo'**
  String get notificationsLoadFailed;

  /// No description provided for @retry.
  ///
  /// In vi, this message translates to:
  /// **'Thử lại'**
  String get retry;

  /// No description provided for @noNotifications.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có thông báo'**
  String get noNotifications;

  /// No description provided for @noNotificationsDescription.
  ///
  /// In vi, this message translates to:
  /// **'Các thông báo hệ thống được duyệt sẽ xuất hiện tại đây để bạn theo dõi.'**
  String get noNotificationsDescription;

  /// No description provided for @read.
  ///
  /// In vi, this message translates to:
  /// **'Đã đọc'**
  String get read;

  /// No description provided for @unread.
  ///
  /// In vi, this message translates to:
  /// **'Chưa đọc'**
  String get unread;

  /// No description provided for @notificationTypePromotion.
  ///
  /// In vi, this message translates to:
  /// **'Khuyến mãi'**
  String get notificationTypePromotion;

  /// No description provided for @notificationTypeWarning.
  ///
  /// In vi, this message translates to:
  /// **'Cảnh báo'**
  String get notificationTypeWarning;

  /// No description provided for @notificationTypeSystemUpdate.
  ///
  /// In vi, this message translates to:
  /// **'Cập nhật hệ thống'**
  String get notificationTypeSystemUpdate;

  /// No description provided for @loadMoreNotifications.
  ///
  /// In vi, this message translates to:
  /// **'Xem thêm thông báo'**
  String get loadMoreNotifications;

  /// No description provided for @success.
  ///
  /// In vi, this message translates to:
  /// **'Thành công'**
  String get success;

  /// No description provided for @error.
  ///
  /// In vi, this message translates to:
  /// **'Lỗi'**
  String get error;

  /// No description provided for @warning.
  ///
  /// In vi, this message translates to:
  /// **'Cảnh báo'**
  String get warning;

  /// No description provided for @information.
  ///
  /// In vi, this message translates to:
  /// **'Thông báo'**
  String get information;

  /// No description provided for @serverConnectionError.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết nối đến máy chủ. Vui lòng thử lại sau.'**
  String get serverConnectionError;

  /// No description provided for @serverConnectionErrorTitle.
  ///
  /// In vi, this message translates to:
  /// **'Máy chủ tạm thời không khả dụng'**
  String get serverConnectionErrorTitle;

  /// No description provided for @serverConnectionRestored.
  ///
  /// In vi, this message translates to:
  /// **'Máy chủ đã hoạt động trở lại. Dữ liệu đang được tải lại.'**
  String get serverConnectionRestored;

  /// No description provided for @serverConnectionRestoredTitle.
  ///
  /// In vi, this message translates to:
  /// **'Đã kết nối lại máy chủ'**
  String get serverConnectionRestoredTitle;

  /// No description provided for @reload.
  ///
  /// In vi, this message translates to:
  /// **'Tải lại'**
  String get reload;

  /// No description provided for @confirm.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận'**
  String get confirm;

  /// No description provided for @callStartFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể bắt đầu cuộc gọi. Vui lòng thử lại.'**
  String get callStartFailed;

  /// No description provided for @callRejected.
  ///
  /// In vi, this message translates to:
  /// **'Đối phương đã từ chối cuộc gọi.'**
  String get callRejected;

  /// No description provided for @callEnded.
  ///
  /// In vi, this message translates to:
  /// **'Cuộc gọi đã kết thúc.'**
  String get callEnded;

  /// No description provided for @callConnecting.
  ///
  /// In vi, this message translates to:
  /// **'Đang kết nối...'**
  String get callConnecting;

  /// No description provided for @callRinging.
  ///
  /// In vi, this message translates to:
  /// **'Đang đổ chuông...'**
  String get callRinging;

  /// No description provided for @microphoneOn.
  ///
  /// In vi, this message translates to:
  /// **'Bật mic'**
  String get microphoneOn;

  /// No description provided for @microphoneOff.
  ///
  /// In vi, this message translates to:
  /// **'Tắt mic'**
  String get microphoneOff;

  /// No description provided for @endCall.
  ///
  /// In vi, this message translates to:
  /// **'Kết thúc'**
  String get endCall;

  /// No description provided for @speaker.
  ///
  /// In vi, this message translates to:
  /// **'Loa ngoài'**
  String get speaker;

  /// No description provided for @earpiece.
  ///
  /// In vi, this message translates to:
  /// **'Tai nghe'**
  String get earpiece;

  /// No description provided for @imageSelectionFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể chọn ảnh.'**
  String get imageSelectionFailed;

  /// No description provided for @chatTitle.
  ///
  /// In vi, this message translates to:
  /// **'Nhắn tin'**
  String get chatTitle;

  /// No description provided for @chatReadOnly.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi đã kết thúc, bạn chỉ có thể xem lại tin nhắn.'**
  String get chatReadOnly;

  /// No description provided for @noMessages.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có tin nhắn nào.'**
  String get noMessages;

  /// No description provided for @messageHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập tin nhắn...'**
  String get messageHint;

  /// No description provided for @tripEnded.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi đã kết thúc'**
  String get tripEnded;

  /// No description provided for @driverReviews.
  ///
  /// In vi, this message translates to:
  /// **'Đánh giá tài xế'**
  String get driverReviews;

  /// No description provided for @driverHasNoReviews.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế chưa có đánh giá nào.'**
  String get driverHasNoReviews;

  /// No description provided for @allReviews.
  ///
  /// In vi, this message translates to:
  /// **'Tất cả nhận xét'**
  String get allReviews;

  /// No description provided for @reviews.
  ///
  /// In vi, this message translates to:
  /// **'đánh giá'**
  String get reviews;

  /// No description provided for @reportIncident.
  ///
  /// In vi, this message translates to:
  /// **'Báo cáo sự cố'**
  String get reportIncident;

  /// No description provided for @reportHelpQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Chúng tôi có thể giúp gì cho bạn?'**
  String get reportHelpQuestion;

  /// No description provided for @tripIncident.
  ///
  /// In vi, this message translates to:
  /// **'Sự cố chuyến đi'**
  String get tripIncident;

  /// No description provided for @paymentIssue.
  ///
  /// In vi, this message translates to:
  /// **'Vấn đề thanh toán'**
  String get paymentIssue;

  /// No description provided for @partyFeedback.
  ///
  /// In vi, this message translates to:
  /// **'Phản hồi về tài xế/khách hàng'**
  String get partyFeedback;

  /// No description provided for @appIssue.
  ///
  /// In vi, this message translates to:
  /// **'Lỗi ứng dụng'**
  String get appIssue;

  /// No description provided for @wrongRoute.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế đi sai tuyến'**
  String get wrongRoute;

  /// No description provided for @driverLate.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế đến muộn'**
  String get driverLate;

  /// No description provided for @inappropriateBehavior.
  ///
  /// In vi, this message translates to:
  /// **'Thái độ không phù hợp'**
  String get inappropriateBehavior;

  /// No description provided for @other.
  ///
  /// In vi, this message translates to:
  /// **'Khác'**
  String get other;

  /// No description provided for @reportTrip.
  ///
  /// In vi, this message translates to:
  /// **'Báo cáo chuyến đi'**
  String get reportTrip;

  /// No description provided for @reportSent.
  ///
  /// In vi, this message translates to:
  /// **'Gửi báo cáo chuyến đi thành công.'**
  String get reportSent;

  /// No description provided for @reportSendFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi báo cáo. Vui lòng thử lại.'**
  String get reportSendFailed;

  /// No description provided for @commonIssues.
  ///
  /// In vi, this message translates to:
  /// **'Vấn đề phổ biến'**
  String get commonIssues;

  /// No description provided for @issueEncountered.
  ///
  /// In vi, this message translates to:
  /// **'Vấn đề gặp phải'**
  String get issueEncountered;

  /// No description provided for @issueDescriptionHint.
  ///
  /// In vi, this message translates to:
  /// **'Mô tả chi tiết vấn đề bạn gặp phải...'**
  String get issueDescriptionHint;

  /// No description provided for @reportContentRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập nội dung báo cáo.'**
  String get reportContentRequired;

  /// No description provided for @safeRideDriver.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế SafeRide'**
  String get safeRideDriver;

  /// No description provided for @sendReport.
  ///
  /// In vi, this message translates to:
  /// **'Gửi báo cáo'**
  String get sendReport;

  /// No description provided for @edit.
  ///
  /// In vi, this message translates to:
  /// **'Sửa'**
  String get edit;

  /// No description provided for @delete.
  ///
  /// In vi, this message translates to:
  /// **'Xóa'**
  String get delete;

  /// No description provided for @requiredLicense.
  ///
  /// In vi, this message translates to:
  /// **'Bằng {licenseClass}'**
  String requiredLicense(String licenseClass);

  /// No description provided for @editVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Chỉnh sửa phương tiện'**
  String get editVehicle;

  /// No description provided for @addVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Thêm phương tiện mới'**
  String get addVehicle;

  /// No description provided for @vehicleType.
  ///
  /// In vi, this message translates to:
  /// **'Loại phương tiện'**
  String get vehicleType;

  /// No description provided for @motorbike.
  ///
  /// In vi, this message translates to:
  /// **'Xe máy'**
  String get motorbike;

  /// No description provided for @car.
  ///
  /// In vi, this message translates to:
  /// **'Ô tô'**
  String get car;

  /// No description provided for @vehicleName.
  ///
  /// In vi, this message translates to:
  /// **'Tên phương tiện'**
  String get vehicleName;

  /// No description provided for @vehicleNameHint.
  ///
  /// In vi, this message translates to:
  /// **'Ví dụ: Honda Vision'**
  String get vehicleNameHint;

  /// No description provided for @engineCapacity.
  ///
  /// In vi, this message translates to:
  /// **'Dung tích xi-lanh (cc)'**
  String get engineCapacity;

  /// No description provided for @engineCapacityHint.
  ///
  /// In vi, this message translates to:
  /// **'Ví dụ: 110, 125, 150'**
  String get engineCapacityHint;

  /// No description provided for @licensePlate.
  ///
  /// In vi, this message translates to:
  /// **'Biển số xe'**
  String get licensePlate;

  /// No description provided for @licensePlateHint.
  ///
  /// In vi, this message translates to:
  /// **'Ví dụ: 29A1 - 123.45'**
  String get licensePlateHint;

  /// No description provided for @color.
  ///
  /// In vi, this message translates to:
  /// **'Màu sắc'**
  String get color;

  /// No description provided for @colorHint.
  ///
  /// In vi, this message translates to:
  /// **'Ví dụ: Xanh dương'**
  String get colorHint;

  /// No description provided for @saveChanges.
  ///
  /// In vi, this message translates to:
  /// **'Lưu thay đổi'**
  String get saveChanges;

  /// No description provided for @saveVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Lưu phương tiện'**
  String get saveVehicle;

  /// No description provided for @vehicleNameValidation.
  ///
  /// In vi, this message translates to:
  /// **'Tên phương tiện phải từ 2 đến 100 ký tự.'**
  String get vehicleNameValidation;

  /// No description provided for @engineCapacityValidation.
  ///
  /// In vi, this message translates to:
  /// **'Xe máy cần dung tích xi-lanh hợp lệ để xác định bằng A1 hoặc A.'**
  String get engineCapacityValidation;

  /// No description provided for @licensePlateLengthValidation.
  ///
  /// In vi, this message translates to:
  /// **'Biển số xe phải từ 4 đến 20 ký tự.'**
  String get licensePlateLengthValidation;

  /// No description provided for @licensePlateFormatValidation.
  ///
  /// In vi, this message translates to:
  /// **'Biển số chỉ được chứa chữ cái, chữ số, dấu chấm, khoảng trắng và gạch ngang.'**
  String get licensePlateFormatValidation;

  /// No description provided for @colorValidation.
  ///
  /// In vi, this message translates to:
  /// **'Màu sắc không được vượt quá 30 ký tự.'**
  String get colorValidation;

  /// No description provided for @deleteVehicleQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Xóa phương tiện?'**
  String get deleteVehicleQuestion;

  /// No description provided for @deleteVehicleDescription.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc chắn muốn xóa phương tiện \"{name}\"? Hành động này không thể hoàn tác.'**
  String deleteVehicleDescription(String name);

  /// No description provided for @deleteNow.
  ///
  /// In vi, this message translates to:
  /// **'Xóa ngay'**
  String get deleteNow;

  /// No description provided for @dismiss.
  ///
  /// In vi, this message translates to:
  /// **'Hủy bỏ'**
  String get dismiss;

  /// No description provided for @requestFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xử lý yêu cầu.'**
  String get requestFailed;

  /// No description provided for @myVehicles.
  ///
  /// In vi, this message translates to:
  /// **'Xe của tôi'**
  String get myVehicles;

  /// No description provided for @vehicleManagementDescription.
  ///
  /// In vi, this message translates to:
  /// **'Quản lý phương tiện cá nhân của bạn để sử dụng cho các dịch vụ gửi xe hoặc hỗ trợ lái xe.'**
  String get vehicleManagementDescription;

  /// No description provided for @noVehicles.
  ///
  /// In vi, this message translates to:
  /// **'Bạn chưa có phương tiện nào.'**
  String get noVehicles;

  /// No description provided for @historyLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải lịch sử chuyến đi.'**
  String get historyLoadFailed;

  /// No description provided for @noTripHistory.
  ///
  /// In vi, this message translates to:
  /// **'Không có dữ liệu chuyến đi.'**
  String get noTripHistory;

  /// No description provided for @tripNotRebookable.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi này chưa có đủ dữ liệu để đặt lại.'**
  String get tripNotRebookable;

  /// No description provided for @loadingTrip.
  ///
  /// In vi, this message translates to:
  /// **'Đang tải thông tin chuyến đi...'**
  String get loadingTrip;

  /// No description provided for @chatOpenFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể mở trò chuyện lúc này.'**
  String get chatOpenFailed;

  /// No description provided for @chat.
  ///
  /// In vi, this message translates to:
  /// **'Nhắn tin'**
  String get chat;

  /// No description provided for @viewReviews.
  ///
  /// In vi, this message translates to:
  /// **'Xem đánh giá'**
  String get viewReviews;

  /// No description provided for @tripDetailsLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải thông tin chuyến đi.'**
  String get tripDetailsLoadFailed;

  /// No description provided for @tripDetails.
  ///
  /// In vi, this message translates to:
  /// **'Chi tiết chuyến đi'**
  String get tripDetails;

  /// No description provided for @rebookThisTrip.
  ///
  /// In vi, this message translates to:
  /// **'Đặt lại chuyến này'**
  String get rebookThisTrip;

  /// No description provided for @tripCode.
  ///
  /// In vi, this message translates to:
  /// **'Mã chuyến'**
  String get tripCode;

  /// No description provided for @bookingOrder.
  ///
  /// In vi, this message translates to:
  /// **'Đơn đặt xe #{id}'**
  String bookingOrder(int id);

  /// No description provided for @routeMapUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có bản đồ tuyến đường cho chuyến này.'**
  String get routeMapUnavailable;

  /// No description provided for @route.
  ///
  /// In vi, this message translates to:
  /// **'Tuyến đường'**
  String get route;

  /// No description provided for @tripRoute.
  ///
  /// In vi, this message translates to:
  /// **'Lộ trình chuyến đi'**
  String get tripRoute;

  /// No description provided for @pickupPoint.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đón'**
  String get pickupPoint;

  /// No description provided for @destinationPoint.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đến'**
  String get destinationPoint;

  /// No description provided for @distance.
  ///
  /// In vi, this message translates to:
  /// **'Quãng đường'**
  String get distance;

  /// No description provided for @duration.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian'**
  String get duration;

  /// No description provided for @minutesValue.
  ///
  /// In vi, this message translates to:
  /// **'{minutes} phút'**
  String minutesValue(num minutes);

  /// No description provided for @unknown.
  ///
  /// In vi, this message translates to:
  /// **'Chưa rõ'**
  String get unknown;

  /// No description provided for @driverAndVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế và phương tiện'**
  String get driverAndVehicle;

  /// No description provided for @driverInfoUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có thông tin tài xế cho chuyến đi này.'**
  String get driverInfoUnavailable;

  /// No description provided for @plateValue.
  ///
  /// In vi, this message translates to:
  /// **'Biển số: {plate}'**
  String plateValue(String plate);

  /// No description provided for @vehicleColorValue.
  ///
  /// In vi, this message translates to:
  /// **'Màu xe: {color}'**
  String vehicleColorValue(String color);

  /// No description provided for @tripCountValue.
  ///
  /// In vi, this message translates to:
  /// **'{count} chuyến'**
  String tripCountValue(int count);

  /// No description provided for @experienceYearsValue.
  ///
  /// In vi, this message translates to:
  /// **'{years} năm kinh nghiệm'**
  String experienceYearsValue(int years);

  /// No description provided for @tripCost.
  ///
  /// In vi, this message translates to:
  /// **'Chi phí chuyến đi'**
  String get tripCost;

  /// No description provided for @unknownPaymentMethod.
  ///
  /// In vi, this message translates to:
  /// **'Chưa rõ phương thức'**
  String get unknownPaymentMethod;

  /// No description provided for @fare.
  ///
  /// In vi, this message translates to:
  /// **'Cước phí'**
  String get fare;

  /// No description provided for @discount.
  ///
  /// In vi, this message translates to:
  /// **'Giảm giá'**
  String get discount;

  /// No description provided for @total.
  ///
  /// In vi, this message translates to:
  /// **'Tổng cộng'**
  String get total;

  /// No description provided for @paidAtValue.
  ///
  /// In vi, this message translates to:
  /// **'Thanh toán lúc {time}'**
  String paidAtValue(String time);

  /// No description provided for @customerReview.
  ///
  /// In vi, this message translates to:
  /// **'Đánh giá của khách hàng'**
  String get customerReview;

  /// No description provided for @reviewAndFeedback.
  ///
  /// In vi, this message translates to:
  /// **'Đánh giá và phản hồi'**
  String get reviewAndFeedback;

  /// No description provided for @customerHasNotReviewed.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng chưa đánh giá chuyến đi này.'**
  String get customerHasNotReviewed;

  /// No description provided for @noReviewData.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có dữ liệu đánh giá cho chuyến đi này.'**
  String get noReviewData;

  /// No description provided for @tripHistory.
  ///
  /// In vi, this message translates to:
  /// **'Lịch sử chuyến đi'**
  String get tripHistory;

  /// No description provided for @tripCompletedThanks.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi đã hoàn thành. Cảm ơn bạn!'**
  String get tripCompletedThanks;

  /// No description provided for @tripInfoUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xác định thông tin chuyến đi. Vui lòng thử lại.'**
  String get tripInfoUnavailable;

  /// No description provided for @returnConfirmationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xác nhận trả xe. Vui lòng thử lại.'**
  String get returnConfirmationFailed;

  /// No description provided for @ratingSubmitFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi đánh giá. Vui lòng thử lại.'**
  String get ratingSubmitFailed;

  /// No description provided for @waitForPayment.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng đợi thanh toán hoàn tất.'**
  String get waitForPayment;

  /// No description provided for @completeRequirementsBeforeLeaving.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng xác nhận trả xe và gửi đánh giá trước khi rời màn hình.'**
  String get completeRequirementsBeforeLeaving;

  /// No description provided for @tripComplete.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi hoàn tất'**
  String get tripComplete;

  /// No description provided for @thanksForUsingService.
  ///
  /// In vi, this message translates to:
  /// **'Cảm ơn bạn đã sử dụng dịch vụ'**
  String get thanksForUsingService;

  /// No description provided for @distanceUpper.
  ///
  /// In vi, this message translates to:
  /// **'QUÃNG ĐƯỜNG'**
  String get distanceUpper;

  /// No description provided for @durationUpper.
  ///
  /// In vi, this message translates to:
  /// **'THỜI GIAN'**
  String get durationUpper;

  /// No description provided for @confirmVehicleReturned.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận tài xế đã trả lại phương tiện'**
  String get confirmVehicleReturned;

  /// No description provided for @sendRatingAndWaitPayment.
  ///
  /// In vi, this message translates to:
  /// **'Gửi đánh giá & chờ thanh toán'**
  String get sendRatingAndWaitPayment;

  /// No description provided for @confirmTripRateLater.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận chuyến & đánh giá sau'**
  String get confirmTripRateLater;

  /// No description provided for @paymentDetails.
  ///
  /// In vi, this message translates to:
  /// **'Chi tiết thanh toán'**
  String get paymentDetails;

  /// No description provided for @baseFare.
  ///
  /// In vi, this message translates to:
  /// **'Cước phí cơ bản'**
  String get baseFare;

  /// No description provided for @promotion.
  ///
  /// In vi, this message translates to:
  /// **'Khuyến mãi'**
  String get promotion;

  /// No description provided for @driverRatingQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Bạn thấy tài xế thế nào?'**
  String get driverRatingQuestion;

  /// No description provided for @driverCommentHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhận xét về tài xế (không bắt buộc)'**
  String get driverCommentHint;

  /// No description provided for @waitingForPayment.
  ///
  /// In vi, this message translates to:
  /// **'Đang chờ thanh toán'**
  String get waitingForPayment;

  /// No description provided for @paymentWaitingInstructions.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng quét mã QR từ điện thoại của tài xế hoặc chờ tài xế xác nhận nếu trả tiền mặt.'**
  String get paymentWaitingInstructions;

  /// No description provided for @cancelReasonPlanChanged.
  ///
  /// In vi, this message translates to:
  /// **'Thay đổi kế hoạch'**
  String get cancelReasonPlanChanged;

  /// No description provided for @cancelReasonWaitTooLong.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian chờ quá lâu'**
  String get cancelReasonWaitTooLong;

  /// No description provided for @cancelReasonWrongLocation.
  ///
  /// In vi, this message translates to:
  /// **'Đặt nhầm địa điểm'**
  String get cancelReasonWrongLocation;

  /// No description provided for @cancelReasonNoLongerNeeded.
  ///
  /// In vi, this message translates to:
  /// **'Không còn cần tài xế'**
  String get cancelReasonNoLongerNeeded;

  /// No description provided for @cancelReasonOther.
  ///
  /// In vi, this message translates to:
  /// **'Lý do khác'**
  String get cancelReasonOther;

  /// No description provided for @cancelTripQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Hủy chuyến đi?'**
  String get cancelTripQuestion;

  /// No description provided for @cancelSearchConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc chắn muốn hủy yêu cầu tìm tài xế này không?'**
  String get cancelSearchConfirmation;

  /// No description provided for @cancelBookingConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc chắn muốn hủy chuyến #{id} không?'**
  String cancelBookingConfirmation(int id);

  /// No description provided for @cancelReason.
  ///
  /// In vi, this message translates to:
  /// **'Lý do hủy chuyến'**
  String get cancelReason;

  /// No description provided for @confirmCancellation.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận hủy'**
  String get confirmCancellation;

  /// No description provided for @goBack.
  ///
  /// In vi, this message translates to:
  /// **'Không, quay lại'**
  String get goBack;

  /// No description provided for @cancelTripFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể hủy chuyến. Vui lòng thử lại.'**
  String get cancelTripFailed;

  /// No description provided for @tripCannotBeCancelled.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi này không thể hủy ở trạng thái hiện tại.'**
  String get tripCannotBeCancelled;

  /// No description provided for @tripWaitExpired.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đã hết thời gian chờ và được kết thúc.'**
  String get tripWaitExpired;

  /// No description provided for @tripCancelledSuccessfully.
  ///
  /// In vi, this message translates to:
  /// **'Đã hủy chuyến thành công.'**
  String get tripCancelledSuccessfully;

  /// No description provided for @scheduledTripCancelledSuccessfully.
  ///
  /// In vi, this message translates to:
  /// **'Đã hủy chuyến đặt trước thành công.'**
  String get scheduledTripCancelledSuccessfully;

  /// No description provided for @rebook.
  ///
  /// In vi, this message translates to:
  /// **'Đặt lại'**
  String get rebook;

  /// No description provided for @noPromotions.
  ///
  /// In vi, this message translates to:
  /// **'Hiện chưa có khuyến mãi'**
  String get noPromotions;

  /// No description provided for @remainingUses.
  ///
  /// In vi, this message translates to:
  /// **'Lượt còn lại: {count}'**
  String remainingUses(int count);

  /// No description provided for @promoValidatedOnBooking.
  ///
  /// In vi, this message translates to:
  /// **'Mã sẽ được kiểm tra khi đặt chuyến'**
  String get promoValidatedOnBooking;

  /// No description provided for @noAvailablePromoCodes.
  ///
  /// In vi, this message translates to:
  /// **'Hiện chưa có mã khuyến mãi khả dụng.'**
  String get noAvailablePromoCodes;

  /// No description provided for @deselectPromo.
  ///
  /// In vi, this message translates to:
  /// **'Bỏ chọn mã khuyến mãi'**
  String get deselectPromo;

  /// No description provided for @minimumOrder.
  ///
  /// In vi, this message translates to:
  /// **'Đơn tối thiểu: {amount}'**
  String minimumOrder(String amount);

  /// No description provided for @remainingUseCount.
  ///
  /// In vi, this message translates to:
  /// **'Còn lại: {count} lượt'**
  String remainingUseCount(int count);

  /// No description provided for @usageExhausted.
  ///
  /// In vi, this message translates to:
  /// **'Hết lượt sử dụng'**
  String get usageExhausted;

  /// No description provided for @inUse.
  ///
  /// In vi, this message translates to:
  /// **'Đang\ndùng'**
  String get inUse;

  /// No description provided for @useNow.
  ///
  /// In vi, this message translates to:
  /// **'Dùng\nngay'**
  String get useNow;

  /// No description provided for @percentDiscount.
  ///
  /// In vi, this message translates to:
  /// **'Giảm {percent}%'**
  String percentDiscount(num percent);

  /// No description provided for @maximumDiscount.
  ///
  /// In vi, this message translates to:
  /// **' (Tối đa {amount})'**
  String maximumDiscount(String amount);

  /// No description provided for @fixedDiscount.
  ///
  /// In vi, this message translates to:
  /// **'Giảm {amount}'**
  String fixedDiscount(String amount);

  /// No description provided for @expiresOn.
  ///
  /// In vi, this message translates to:
  /// **'Hết hạn: {date}'**
  String expiresOn(String date);

  /// No description provided for @minimumOrderShort.
  ///
  /// In vi, this message translates to:
  /// **'Đơn tối thiểu {amount}'**
  String minimumOrderShort(String amount);

  /// No description provided for @exitAppQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Thoát ứng dụng?'**
  String get exitAppQuestion;

  /// No description provided for @exitAppDescription.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc chắn muốn thoát khỏi SafeRide không?'**
  String get exitAppDescription;

  /// No description provided for @exit.
  ///
  /// In vi, this message translates to:
  /// **'Thoát'**
  String get exit;

  /// No description provided for @activity.
  ///
  /// In vi, this message translates to:
  /// **'Hoạt động'**
  String get activity;

  /// No description provided for @safeRideAssistant.
  ///
  /// In vi, this message translates to:
  /// **'Trợ lý SafeRide'**
  String get safeRideAssistant;

  /// No description provided for @tryAgain.
  ///
  /// In vi, this message translates to:
  /// **'Thử lại'**
  String get tryAgain;

  /// No description provided for @activeTripNotice.
  ///
  /// In vi, this message translates to:
  /// **'Bạn đang có chuyến đang hoạt động. Vui lòng theo dõi ở mục Hoạt động.'**
  String get activeTripNotice;

  /// No description provided for @trackingTrip.
  ///
  /// In vi, this message translates to:
  /// **'Đang theo dõi chuyến đi'**
  String get trackingTrip;

  /// No description provided for @noActiveTripForSos.
  ///
  /// In vi, this message translates to:
  /// **'Bạn chưa có chuyến đi đang diễn ra để kích hoạt SOS.'**
  String get noActiveTripForSos;

  /// No description provided for @viewAll.
  ///
  /// In vi, this message translates to:
  /// **'Xem tất cả'**
  String get viewAll;

  /// No description provided for @locatingAddress.
  ///
  /// In vi, this message translates to:
  /// **'Đang xác định địa chỉ...'**
  String get locatingAddress;

  /// No description provided for @searchPickup.
  ///
  /// In vi, this message translates to:
  /// **'Tìm điểm đón'**
  String get searchPickup;

  /// No description provided for @searchDestination.
  ///
  /// In vi, this message translates to:
  /// **'Tìm điểm đến'**
  String get searchDestination;

  /// No description provided for @selectedPickup.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đón đã chọn'**
  String get selectedPickup;

  /// No description provided for @selectedDestination.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đến đã chọn'**
  String get selectedDestination;

  /// No description provided for @searchOrTapMap.
  ///
  /// In vi, this message translates to:
  /// **'Tìm kiếm hoặc chạm vào bản đồ để chọn.'**
  String get searchOrTapMap;

  /// No description provided for @confirmPickup.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận điểm đón'**
  String get confirmPickup;

  /// No description provided for @confirmDestination.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận điểm đến'**
  String get confirmDestination;

  /// No description provided for @prepayment.
  ///
  /// In vi, this message translates to:
  /// **'Thanh toán trước'**
  String get prepayment;

  /// No description provided for @payosPaymentAmount.
  ///
  /// In vi, this message translates to:
  /// **'Số tiền thanh toán qua PayOS'**
  String get payosPaymentAmount;

  /// No description provided for @checkPayment.
  ///
  /// In vi, this message translates to:
  /// **'Kiểm tra thanh toán'**
  String get checkPayment;

  /// No description provided for @payAfterTrip.
  ///
  /// In vi, this message translates to:
  /// **'Để sau chuyến thanh toán'**
  String get payAfterTrip;

  /// No description provided for @prepaid.
  ///
  /// In vi, this message translates to:
  /// **'Đã thanh toán trước'**
  String get prepaid;

  /// No description provided for @backToTrip.
  ///
  /// In vi, this message translates to:
  /// **'Quay lại chuyến đi'**
  String get backToTrip;

  /// No description provided for @payosQrCreateFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tạo mã QR PayOS.'**
  String get payosQrCreateFailed;

  /// No description provided for @scanQrToPay.
  ///
  /// In vi, this message translates to:
  /// **'Quét mã bằng ứng dụng ngân hàng để thanh toán'**
  String get scanQrToPay;

  /// No description provided for @cameraOpenFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể mở camera. Vui lòng kiểm tra quyền truy cập.'**
  String get cameraOpenFailed;

  /// No description provided for @photoCaptureFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể chụp ảnh. Vui lòng thử lại.'**
  String get photoCaptureFailed;

  /// No description provided for @alignDocumentCorners.
  ///
  /// In vi, this message translates to:
  /// **'Canh 4 góc giấy tờ sát trong khung'**
  String get alignDocumentCorners;

  /// No description provided for @submittedInformation.
  ///
  /// In vi, this message translates to:
  /// **'Thông tin đã gửi'**
  String get submittedInformation;

  /// No description provided for @documentNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số giấy tờ'**
  String get documentNumber;

  /// No description provided for @licenseClass.
  ///
  /// In vi, this message translates to:
  /// **'Hạng bằng'**
  String get licenseClass;

  /// No description provided for @issueDate.
  ///
  /// In vi, this message translates to:
  /// **'Ngày cấp'**
  String get issueDate;

  /// No description provided for @expiryDate.
  ///
  /// In vi, this message translates to:
  /// **'Hết hạn'**
  String get expiryDate;

  /// No description provided for @documents.
  ///
  /// In vi, this message translates to:
  /// **'Tài liệu'**
  String get documents;

  /// No description provided for @frontSide.
  ///
  /// In vi, this message translates to:
  /// **'Mặt trước'**
  String get frontSide;

  /// No description provided for @backSide.
  ///
  /// In vi, this message translates to:
  /// **'Mặt sau'**
  String get backSide;

  /// No description provided for @submittedFile.
  ///
  /// In vi, this message translates to:
  /// **'Tệp đã nộp'**
  String get submittedFile;

  /// No description provided for @documentApproved.
  ///
  /// In vi, this message translates to:
  /// **'Đã duyệt'**
  String get documentApproved;

  /// No description provided for @documentPendingReview.
  ///
  /// In vi, this message translates to:
  /// **'Đã nộp, đang chờ duyệt'**
  String get documentPendingReview;

  /// No description provided for @documentRejected.
  ///
  /// In vi, this message translates to:
  /// **'Bị từ chối'**
  String get documentRejected;

  /// No description provided for @documentNotSubmitted.
  ///
  /// In vi, this message translates to:
  /// **'Chưa nộp'**
  String get documentNotSubmitted;

  /// No description provided for @identityVerification.
  ///
  /// In vi, this message translates to:
  /// **'Xác minh danh tính'**
  String get identityVerification;

  /// No description provided for @completeYourProfile.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn tất hồ sơ của bạn'**
  String get completeYourProfile;

  /// No description provided for @identityVerificationIntro.
  ///
  /// In vi, this message translates to:
  /// **'Để bắt đầu nhận chuyến và đảm bảo an toàn cho hành khách, vui lòng xác minh danh tính và cung cấp các giấy tờ cần thiết.'**
  String get identityVerificationIntro;

  /// No description provided for @requiredDocuments.
  ///
  /// In vi, this message translates to:
  /// **'Danh sách tài liệu cần nộp'**
  String get requiredDocuments;

  /// No description provided for @submitApplicationNow.
  ///
  /// In vi, this message translates to:
  /// **'Nộp hồ sơ ngay'**
  String get submitApplicationNow;

  /// No description provided for @verificationTime.
  ///
  /// In vi, this message translates to:
  /// **'Quá trình xác minh thường mất từ 1-3 ngày làm việc.'**
  String get verificationTime;

  /// No description provided for @previousApplicationRejected.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ trước đó bị từ chối'**
  String get previousApplicationRejected;

  /// No description provided for @profileStatusLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải trạng thái hồ sơ. Vui lòng thử lại.'**
  String get profileStatusLoadFailed;

  /// No description provided for @idCardOrPassport.
  ///
  /// In vi, this message translates to:
  /// **'CCCD / Hộ chiếu'**
  String get idCardOrPassport;

  /// No description provided for @frontAndBack.
  ///
  /// In vi, this message translates to:
  /// **'Mặt trước và mặt sau'**
  String get frontAndBack;

  /// No description provided for @drivingLicense.
  ///
  /// In vi, this message translates to:
  /// **'Bằng lái xe (GPLX)'**
  String get drivingLicense;

  /// No description provided for @licensePhotoAndInfo.
  ///
  /// In vi, this message translates to:
  /// **'Ảnh bằng lái và thông tin GPLX'**
  String get licensePhotoAndInfo;

  /// No description provided for @criminalRecord.
  ///
  /// In vi, this message translates to:
  /// **'Lý lịch tư pháp'**
  String get criminalRecord;

  /// No description provided for @originalIssuedWithinSixMonths.
  ///
  /// In vi, this message translates to:
  /// **'Bản gốc, cấp trong 6 tháng'**
  String get originalIssuedWithinSixMonths;

  /// No description provided for @resubmissionRequired.
  ///
  /// In vi, this message translates to:
  /// **'Cần nộp lại'**
  String get resubmissionRequired;

  /// No description provided for @submitted.
  ///
  /// In vi, this message translates to:
  /// **'Đã nộp'**
  String get submitted;

  /// No description provided for @confirmHireDriver.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thuê tài xế'**
  String get confirmHireDriver;

  /// No description provided for @hourlyHire.
  ///
  /// In vi, this message translates to:
  /// **'Thuê theo giờ'**
  String get hourlyHire;

  /// No description provided for @tripDetailsHeading.
  ///
  /// In vi, this message translates to:
  /// **'Chi tiết chuyến đi'**
  String get tripDetailsHeading;

  /// No description provided for @notCreated.
  ///
  /// In vi, this message translates to:
  /// **'Chưa tạo'**
  String get notCreated;

  /// No description provided for @awaitingConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Chờ xác nhận'**
  String get awaitingConfirmation;

  /// No description provided for @estimatedDuration.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian dự kiến'**
  String get estimatedDuration;

  /// No description provided for @updating.
  ///
  /// In vi, this message translates to:
  /// **'Đang cập nhật'**
  String get updating;

  /// No description provided for @estimatedTotalPayment.
  ///
  /// In vi, this message translates to:
  /// **'Tổng thanh toán dự kiến'**
  String get estimatedTotalPayment;

  /// No description provided for @missingTripToConfirmDriver.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có mã chuyến để xác nhận tài xế.'**
  String get missingTripToConfirmDriver;

  /// No description provided for @driverOfferNotFound.
  ///
  /// In vi, this message translates to:
  /// **'Không tìm thấy thông tin đề nghị của tài xế.'**
  String get driverOfferNotFound;

  /// No description provided for @confirmDriverFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xác nhận tài xế. Vui lòng thử lại.'**
  String get confirmDriverFailed;

  /// No description provided for @driverConfirmed.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác nhận thuê tài xế'**
  String get driverConfirmed;

  /// No description provided for @driverConfirmedMessage.
  ///
  /// In vi, this message translates to:
  /// **'{driverName} sẽ nhận chuyến #{bookingId}. Đang chờ hệ thống điều phối...'**
  String driverConfirmedMessage(String driverName, int bookingId);

  /// No description provided for @agree.
  ///
  /// In vi, this message translates to:
  /// **'Đồng ý'**
  String get agree;

  /// No description provided for @driverRatingSummary.
  ///
  /// In vi, this message translates to:
  /// **'{rating} sao • {tripCount} chuyến • {years} năm'**
  String driverRatingSummary(String rating, int tripCount, int years);

  /// No description provided for @confirmDriverNotice.
  ///
  /// In vi, this message translates to:
  /// **'Hãy kiểm tra kỹ thông tin tài xế trước khi xác nhận.'**
  String get confirmDriverNotice;

  /// No description provided for @oldTripDataInvalid.
  ///
  /// In vi, this message translates to:
  /// **'Dữ liệu chuyến đi cũ không hợp lệ.'**
  String get oldTripDataInvalid;

  /// No description provided for @calculatingFarePleaseWait.
  ///
  /// In vi, this message translates to:
  /// **'Đang tính toán giá, vui lòng đợi.'**
  String get calculatingFarePleaseWait;

  /// No description provided for @bookingSuccessful.
  ///
  /// In vi, this message translates to:
  /// **'Đặt xe thành công. Tài xế sẽ đến đón bạn đúng giờ.'**
  String get bookingSuccessful;

  /// No description provided for @rebookTrip.
  ///
  /// In vi, this message translates to:
  /// **'Đặt lại chuyến đi'**
  String get rebookTrip;

  /// No description provided for @confirmPreviousInformation.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thông tin cũ'**
  String get confirmPreviousInformation;

  /// No description provided for @reviewRouteAndVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng kiểm tra lại lộ trình và phương tiện cho chuyến đi sắp tới của bạn.'**
  String get reviewRouteAndVehicle;

  /// No description provided for @departureTime.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian khởi hành'**
  String get departureTime;

  /// No description provided for @leaveNow.
  ///
  /// In vi, this message translates to:
  /// **'Đi ngay'**
  String get leaveNow;

  /// No description provided for @scheduleAhead.
  ///
  /// In vi, this message translates to:
  /// **'Đặt trước'**
  String get scheduleAhead;

  /// No description provided for @promotionCode.
  ///
  /// In vi, this message translates to:
  /// **'Mã khuyến mãi'**
  String get promotionCode;

  /// No description provided for @oldPromoCannotBeReused.
  ///
  /// In vi, this message translates to:
  /// **'Mã của chuyến cũ không được dùng lại. Bạn chỉ có thể chọn hoặc nhập mã mới cho chuyến này.'**
  String get oldPromoCannotBeReused;

  /// No description provided for @grandTotal.
  ///
  /// In vi, this message translates to:
  /// **'Tổng cộng'**
  String get grandTotal;

  /// No description provided for @discountApplied.
  ///
  /// In vi, this message translates to:
  /// **'↓ Đã giảm {amount}'**
  String discountApplied(String amount);

  /// No description provided for @taxesIncluded.
  ///
  /// In vi, this message translates to:
  /// **'Bao gồm thuế phí'**
  String get taxesIncluded;

  /// No description provided for @confirmAndFindDriver.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận & Tìm tài xế'**
  String get confirmAndFindDriver;

  /// No description provided for @addNewPromoCode.
  ///
  /// In vi, this message translates to:
  /// **'Thêm mã khuyến mãi mới'**
  String get addNewPromoCode;

  /// No description provided for @completePaymentBeforeExit.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng hoàn thành thanh toán trước khi thoát'**
  String get completePaymentBeforeExit;

  /// No description provided for @completePayment.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng hoàn thành thanh toán.'**
  String get completePayment;

  /// No description provided for @tripPayment.
  ///
  /// In vi, this message translates to:
  /// **'Thanh toán chuyến đi'**
  String get tripPayment;

  /// No description provided for @customerPaymentAmount.
  ///
  /// In vi, this message translates to:
  /// **'Số tiền khách cần thanh toán'**
  String get customerPaymentAmount;

  /// No description provided for @paid.
  ///
  /// In vi, this message translates to:
  /// **'Đã thanh toán'**
  String get paid;

  /// No description provided for @checkAgain.
  ///
  /// In vi, this message translates to:
  /// **'Kiểm tra lại'**
  String get checkAgain;

  /// No description provided for @cashConfirmed.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác nhận tiền mặt'**
  String get cashConfirmed;

  /// No description provided for @customerPaid.
  ///
  /// In vi, this message translates to:
  /// **'Khách đã thanh toán'**
  String get customerPaid;

  /// No description provided for @backToHome.
  ///
  /// In vi, this message translates to:
  /// **'Về màn hình chính'**
  String get backToHome;

  /// No description provided for @paymentQrCreateFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tạo mã QR thanh toán.'**
  String get paymentQrCreateFailed;

  /// No description provided for @reconfirmCash.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận lại tiền mặt'**
  String get reconfirmCash;

  /// No description provided for @recreateQr.
  ///
  /// In vi, this message translates to:
  /// **'Tạo lại mã QR'**
  String get recreateQr;

  /// No description provided for @switchPaymentMethod.
  ///
  /// In vi, this message translates to:
  /// **'Chuyển phương thức khác'**
  String get switchPaymentMethod;

  /// No description provided for @customerScanQr.
  ///
  /// In vi, this message translates to:
  /// **'Đưa khách quét mã này'**
  String get customerScanQr;

  /// No description provided for @cashPaymentConfirmFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xác nhận thanh toán tiền mặt.'**
  String get cashPaymentConfirmFailed;

  /// No description provided for @chooseCustomerPaymentMethod.
  ///
  /// In vi, this message translates to:
  /// **'Chọn phương thức khách thanh toán'**
  String get chooseCustomerPaymentMethod;

  /// No description provided for @qrPayment.
  ///
  /// In vi, this message translates to:
  /// **'Thanh toán QR'**
  String get qrPayment;

  /// No description provided for @cashPayment.
  ///
  /// In vi, this message translates to:
  /// **'Trả tiền mặt'**
  String get cashPayment;

  /// No description provided for @returnVehicleConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận trả xe'**
  String get returnVehicleConfirmation;

  /// No description provided for @returnEvidenceInstruction.
  ///
  /// In vi, this message translates to:
  /// **'Chụp hoặc chọn ảnh bằng chứng bàn giao xe cho khách. Cần 1–3 ảnh.'**
  String get returnEvidenceInstruction;

  /// No description provided for @tapToAddPhoto.
  ///
  /// In vi, this message translates to:
  /// **'Nhấn để thêm ảnh'**
  String get tapToAddPhoto;

  /// No description provided for @optionalNote.
  ///
  /// In vi, this message translates to:
  /// **'Ghi chú (tùy chọn)'**
  String get optionalNote;

  /// No description provided for @noteHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập ghi chú nếu cần...'**
  String get noteHint;

  /// No description provided for @submitting.
  ///
  /// In vi, this message translates to:
  /// **'Đang gửi...'**
  String get submitting;

  /// No description provided for @returnConfirmedSuccess.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thành công'**
  String get returnConfirmedSuccess;

  /// No description provided for @returnConfirmedMessage.
  ///
  /// In vi, this message translates to:
  /// **'Đã ghi nhận trả xe. Chuyến đi đang được hoàn tất.'**
  String get returnConfirmedMessage;

  /// No description provided for @done.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn tất'**
  String get done;

  /// No description provided for @minimumEvidencePhoto.
  ///
  /// In vi, this message translates to:
  /// **'Cần ít nhất 1 ảnh bằng chứng.'**
  String get minimumEvidencePhoto;

  /// No description provided for @maximumEvidencePhotos.
  ///
  /// In vi, this message translates to:
  /// **'Không được tải lên quá 3 ảnh.'**
  String get maximumEvidencePhotos;

  /// No description provided for @evidenceUploadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi bằng chứng. Thử lại.'**
  String get evidenceUploadFailed;

  /// No description provided for @takePhoto.
  ///
  /// In vi, this message translates to:
  /// **'Chụp ảnh'**
  String get takePhoto;

  /// No description provided for @chooseFromGallery.
  ///
  /// In vi, this message translates to:
  /// **'Chọn từ thư viện'**
  String get chooseFromGallery;

  /// No description provided for @removePhoto.
  ///
  /// In vi, this message translates to:
  /// **'Xóa ảnh'**
  String get removePhoto;

  /// No description provided for @removePhotoQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có muốn xóa ảnh này không?'**
  String get removePhotoQuestion;

  /// No description provided for @photoNumber.
  ///
  /// In vi, this message translates to:
  /// **'Ảnh {number}'**
  String photoNumber(int number);

  /// No description provided for @photoCount.
  ///
  /// In vi, this message translates to:
  /// **'{count} / {max} ảnh'**
  String photoCount(int count, int max);

  /// No description provided for @remainingPhotos.
  ///
  /// In vi, this message translates to:
  /// **'Còn {count} ảnh'**
  String remainingPhotos(int count);

  /// No description provided for @submitEvidenceWithCount.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận trả xe ({count} ảnh)'**
  String submitEvidenceWithCount(int count);

  /// No description provided for @mediaAccessFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể truy cập {source}.'**
  String mediaAccessFailed(String source);

  /// No description provided for @camera.
  ///
  /// In vi, this message translates to:
  /// **'camera'**
  String get camera;

  /// No description provided for @gallery.
  ///
  /// In vi, this message translates to:
  /// **'thư viện'**
  String get gallery;

  /// No description provided for @myWallet.
  ///
  /// In vi, this message translates to:
  /// **'Ví của tôi'**
  String get myWallet;

  /// No description provided for @availableBalance.
  ///
  /// In vi, this message translates to:
  /// **'SỐ DƯ KHẢ DỤNG'**
  String get availableBalance;

  /// No description provided for @withdraw.
  ///
  /// In vi, this message translates to:
  /// **'Rút tiền'**
  String get withdraw;

  /// No description provided for @topUp.
  ///
  /// In vi, this message translates to:
  /// **'Nạp thẻ'**
  String get topUp;

  /// No description provided for @income.
  ///
  /// In vi, this message translates to:
  /// **'Thu nhập'**
  String get income;

  /// No description provided for @day.
  ///
  /// In vi, this message translates to:
  /// **'Ngày'**
  String get day;

  /// No description provided for @week.
  ///
  /// In vi, this message translates to:
  /// **'Tuần'**
  String get week;

  /// No description provided for @month.
  ///
  /// In vi, this message translates to:
  /// **'Tháng'**
  String get month;

  /// No description provided for @totalIncomeForPeriod.
  ///
  /// In vi, this message translates to:
  /// **'Tổng thu nhập\n{period}'**
  String totalIncomeForPeriod(String period);

  /// No description provided for @recentTransactions.
  ///
  /// In vi, this message translates to:
  /// **'Giao dịch gần đây'**
  String get recentTransactions;

  /// No description provided for @bankListLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải danh sách ngân hàng.'**
  String get bankListLoadFailed;

  /// No description provided for @withdrawalRequestSent.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi yêu cầu rút tiền.'**
  String get withdrawalRequestSent;

  /// No description provided for @withdrawalRequestFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi yêu cầu rút tiền.'**
  String get withdrawalRequestFailed;

  /// No description provided for @withdrawToBank.
  ///
  /// In vi, this message translates to:
  /// **'Rút tiền về ngân hàng'**
  String get withdrawToBank;

  /// No description provided for @bankInfoWillBeSaved.
  ///
  /// In vi, this message translates to:
  /// **'Thông tin này sẽ được lưu cho lần rút tiếp theo.'**
  String get bankInfoWillBeSaved;

  /// No description provided for @lastBankPreFilled.
  ///
  /// In vi, this message translates to:
  /// **'Tài khoản gần nhất đã được điền sẵn.'**
  String get lastBankPreFilled;

  /// No description provided for @selectBankRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chọn ngân hàng'**
  String get selectBankRequired;

  /// No description provided for @bank.
  ///
  /// In vi, this message translates to:
  /// **'Ngân hàng'**
  String get bank;

  /// No description provided for @searchAndSelectBank.
  ///
  /// In vi, this message translates to:
  /// **'Tìm và chọn ngân hàng'**
  String get searchAndSelectBank;

  /// No description provided for @accountNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số tài khoản'**
  String get accountNumber;

  /// No description provided for @invalidAccountNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số tài khoản không hợp lệ'**
  String get invalidAccountNumber;

  /// No description provided for @accountHolderName.
  ///
  /// In vi, this message translates to:
  /// **'Tên chủ tài khoản'**
  String get accountHolderName;

  /// No description provided for @accountHolderRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập tên chủ tài khoản'**
  String get accountHolderRequired;

  /// No description provided for @withdrawalAmount.
  ///
  /// In vi, this message translates to:
  /// **'Số tiền muốn rút'**
  String get withdrawalAmount;

  /// No description provided for @minimumWithdrawal.
  ///
  /// In vi, this message translates to:
  /// **'Số tiền tối thiểu là {amount}'**
  String minimumWithdrawal(String amount);

  /// No description provided for @confirmWithdrawal.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận rút tiền'**
  String get confirmWithdrawal;

  /// No description provided for @selectBank.
  ///
  /// In vi, this message translates to:
  /// **'Chọn ngân hàng'**
  String get selectBank;

  /// No description provided for @searchBankHint.
  ///
  /// In vi, this message translates to:
  /// **'Tìm theo tên, mã hoặc BIN'**
  String get searchBankHint;

  /// No description provided for @bankNotFound.
  ///
  /// In vi, this message translates to:
  /// **'Không tìm thấy ngân hàng.'**
  String get bankNotFound;

  /// No description provided for @noTransactions.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có giao dịch nào.'**
  String get noTransactions;

  /// No description provided for @today.
  ///
  /// In vi, this message translates to:
  /// **'hôm nay'**
  String get today;

  /// No description provided for @thisMonth.
  ///
  /// In vi, this message translates to:
  /// **'tháng này'**
  String get thisMonth;

  /// No description provided for @thisWeek.
  ///
  /// In vi, this message translates to:
  /// **'tuần này'**
  String get thisWeek;

  /// No description provided for @noPreviousPeriodData.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có dữ liệu\nkỳ trước'**
  String get noPreviousPeriodData;

  /// No description provided for @periodComparison.
  ///
  /// In vi, this message translates to:
  /// **'{value}% so với\nkỳ trước'**
  String periodComparison(String value);

  /// No description provided for @completed.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn thành'**
  String get completed;

  /// No description provided for @home.
  ///
  /// In vi, this message translates to:
  /// **'Trang chủ'**
  String get home;

  /// No description provided for @account.
  ///
  /// In vi, this message translates to:
  /// **'Tài khoản'**
  String get account;

  /// No description provided for @wallet.
  ///
  /// In vi, this message translates to:
  /// **'Ví'**
  String get wallet;

  /// No description provided for @destinationQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Bạn muốn đi đâu hôm nay?'**
  String get destinationQuestion;

  /// No description provided for @bookNow.
  ///
  /// In vi, this message translates to:
  /// **'Đặt ngay'**
  String get bookNow;

  /// No description provided for @bookNowDescription.
  ///
  /// In vi, this message translates to:
  /// **'Tìm tài xế phù hợp cho chuyến đi'**
  String get bookNowDescription;

  /// No description provided for @scheduleBooking.
  ///
  /// In vi, this message translates to:
  /// **'Đặt lịch trước'**
  String get scheduleBooking;

  /// No description provided for @history.
  ///
  /// In vi, this message translates to:
  /// **'Lịch sử'**
  String get history;

  /// No description provided for @myVehiclesShort.
  ///
  /// In vi, this message translates to:
  /// **'Xe của tôi'**
  String get myVehiclesShort;

  /// No description provided for @promotions.
  ///
  /// In vi, this message translates to:
  /// **'Khuyến mãi'**
  String get promotions;

  /// No description provided for @sos.
  ///
  /// In vi, this message translates to:
  /// **'SOS khẩn cấp'**
  String get sos;

  /// No description provided for @recentTrips.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi gần đây'**
  String get recentTrips;

  /// No description provided for @friendlyUser.
  ///
  /// In vi, this message translates to:
  /// **'bạn'**
  String get friendlyUser;

  /// No description provided for @greeting.
  ///
  /// In vi, this message translates to:
  /// **'Chào {name},'**
  String greeting(String name);

  /// No description provided for @sampleRecentPickup.
  ///
  /// In vi, this message translates to:
  /// **'123 Nguyễn Văn Linh, Q.7'**
  String get sampleRecentPickup;

  /// No description provided for @sampleRecentDestination.
  ///
  /// In vi, this message translates to:
  /// **'Sân bay Tân Sơn Nhất'**
  String get sampleRecentDestination;

  /// No description provided for @sampleRecentTime.
  ///
  /// In vi, this message translates to:
  /// **'Hôm qua, 14:30'**
  String get sampleRecentTime;

  /// No description provided for @driverProfile.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ tài xế'**
  String get driverProfile;

  /// No description provided for @tripCountPlus.
  ///
  /// In vi, this message translates to:
  /// **'{count}+ chuyến đi'**
  String tripCountPlus(String count);

  /// No description provided for @kycStatus.
  ///
  /// In vi, this message translates to:
  /// **'Trạng thái KYC'**
  String get kycStatus;

  /// No description provided for @kycApprovedDescription.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ đã được duyệt bởi hệ thống'**
  String get kycApprovedDescription;

  /// No description provided for @cleanCriminalRecord.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn toàn trong sạch & minh bạch'**
  String get cleanCriminalRecord;

  /// No description provided for @confirmHire.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thuê'**
  String get confirmHire;

  /// No description provided for @rejectAndFindAnotherDriver.
  ///
  /// In vi, this message translates to:
  /// **'Từ chối và tìm tài xế khác'**
  String get rejectAndFindAnotherDriver;

  /// No description provided for @rejectDriverQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Từ chối tài xế?'**
  String get rejectDriverQuestion;

  /// No description provided for @rejectDriverDescription.
  ///
  /// In vi, this message translates to:
  /// **'Hệ thống sẽ bỏ qua tài xế này và tiếp tục tìm kiếm người khác cho bạn.'**
  String get rejectDriverDescription;

  /// No description provided for @findingAnotherDriver.
  ///
  /// In vi, this message translates to:
  /// **'Đang tìm tài xế khác cho bạn...'**
  String get findingAnotherDriver;

  /// No description provided for @rejectDriverFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể từ chối tài xế.'**
  String get rejectDriverFailed;

  /// No description provided for @experienceUpper.
  ///
  /// In vi, this message translates to:
  /// **'KINH NGHIỆM'**
  String get experienceUpper;

  /// No description provided for @yearsValueCapitalized.
  ///
  /// In vi, this message translates to:
  /// **'{years} Năm'**
  String yearsValueCapitalized(int years);

  /// No description provided for @safeDriving.
  ///
  /// In vi, this message translates to:
  /// **'Lái xe an toàn'**
  String get safeDriving;

  /// No description provided for @friendly.
  ///
  /// In vi, this message translates to:
  /// **'Thân thiện'**
  String get friendly;

  /// No description provided for @verified.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác minh'**
  String get verified;

  /// No description provided for @idCardFront.
  ///
  /// In vi, this message translates to:
  /// **'Mặt trước CCCD'**
  String get idCardFront;

  /// No description provided for @idCardBack.
  ///
  /// In vi, this message translates to:
  /// **'Mặt sau CCCD'**
  String get idCardBack;

  /// No description provided for @idCardCameraInstruction.
  ///
  /// In vi, this message translates to:
  /// **'Đặt CCCD nằm gọn trong khung, đủ sáng và rõ nét.'**
  String get idCardCameraInstruction;

  /// No description provided for @idCardScanned.
  ///
  /// In vi, this message translates to:
  /// **'Đã quét thông tin CCCD.'**
  String get idCardScanned;

  /// No description provided for @ocrScanFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể quét OCR từ ảnh này.'**
  String get ocrScanFailed;

  /// No description provided for @stepOneOfThree.
  ///
  /// In vi, this message translates to:
  /// **'Bước 1/3'**
  String get stepOneOfThree;

  /// No description provided for @uploadIdCard.
  ///
  /// In vi, this message translates to:
  /// **'Tải lên CCCD'**
  String get uploadIdCard;

  /// No description provided for @captureIdCard.
  ///
  /// In vi, this message translates to:
  /// **'Chụp ảnh CCCD'**
  String get captureIdCard;

  /// No description provided for @idCardUploadInstruction.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng cung cấp hình ảnh mặt trước và mặt sau của Căn cước công dân. Đảm bảo ảnh rõ nét, không bị lóa sáng hay mất góc.'**
  String get idCardUploadInstruction;

  /// No description provided for @fullName.
  ///
  /// In vi, this message translates to:
  /// **'Họ và Tên'**
  String get fullName;

  /// No description provided for @idCardNameHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập họ và tên trên CCCD'**
  String get idCardNameHint;

  /// No description provided for @idCardNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số CCCD'**
  String get idCardNumber;

  /// No description provided for @idCardNumberHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập số CCCD'**
  String get idCardNumberHint;

  /// No description provided for @continueAction.
  ///
  /// In vi, this message translates to:
  /// **'Tiếp tục'**
  String get continueAction;

  /// No description provided for @idCardFieldsRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chụp đủ ảnh và kiểm tra Họ và Tên, Số CCCD.'**
  String get idCardFieldsRequired;

  /// No description provided for @idCardPhotoTip.
  ///
  /// In vi, this message translates to:
  /// **'Mẹo: Đặt CCCD trên mặt phẳng tối màu, đủ ánh sáng tự nhiên để đạt kết quả tốt nhất.'**
  String get idCardPhotoTip;

  /// No description provided for @ocrScanningOnDevice.
  ///
  /// In vi, this message translates to:
  /// **'Đang quét OCR trên thiết bị...'**
  String get ocrScanningOnDevice;

  /// No description provided for @idCardOcrFilled.
  ///
  /// In vi, this message translates to:
  /// **'OCR đã tự điền thông tin CCCD'**
  String get idCardOcrFilled;

  /// No description provided for @tapToCaptureOrUpload.
  ///
  /// In vi, this message translates to:
  /// **'Chạm để chụp hoặc tải lên'**
  String get tapToCaptureOrUpload;

  /// No description provided for @licenseFront.
  ///
  /// In vi, this message translates to:
  /// **'Mặt trước GPLX'**
  String get licenseFront;

  /// No description provided for @licenseBack.
  ///
  /// In vi, this message translates to:
  /// **'Mặt sau GPLX'**
  String get licenseBack;

  /// No description provided for @licenseCameraInstruction.
  ///
  /// In vi, this message translates to:
  /// **'Đặt bằng lái nằm gọn trong khung, đủ sáng và rõ nét.'**
  String get licenseCameraInstruction;

  /// No description provided for @ocrMlKitScanned.
  ///
  /// In vi, this message translates to:
  /// **'Đã quét OCR bằng Google ML Kit.'**
  String get ocrMlKitScanned;

  /// No description provided for @licenseOcrFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể quét OCR từ ảnh GPLX này.'**
  String get licenseOcrFailed;

  /// No description provided for @licenseType.
  ///
  /// In vi, this message translates to:
  /// **'Loại bằng lái'**
  String get licenseType;

  /// No description provided for @licensePhotos.
  ///
  /// In vi, this message translates to:
  /// **'Ảnh chụp bằng lái xe'**
  String get licensePhotos;

  /// No description provided for @licenseNameHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập họ và tên trên GPLX'**
  String get licenseNameHint;

  /// No description provided for @licenseNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số bằng lái (GPLX)'**
  String get licenseNumber;

  /// No description provided for @licenseNumberHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập số trên bằng lái'**
  String get licenseNumberHint;

  /// No description provided for @selectLicenseClass.
  ///
  /// In vi, this message translates to:
  /// **'Chọn hạng bằng'**
  String get selectLicenseClass;

  /// No description provided for @unlimited.
  ///
  /// In vi, this message translates to:
  /// **'Không giới hạn'**
  String get unlimited;

  /// No description provided for @licenseNoExpiry.
  ///
  /// In vi, this message translates to:
  /// **'Bằng lái không có ngày hết hạn'**
  String get licenseNoExpiry;

  /// No description provided for @idAndLicenseNameMismatch.
  ///
  /// In vi, this message translates to:
  /// **'Họ và Tên trên CCCD và GPLX không trùng khớp.'**
  String get idAndLicenseNameMismatch;

  /// No description provided for @stepTwoOfThree.
  ///
  /// In vi, this message translates to:
  /// **'Bước 2/3'**
  String get stepTwoOfThree;

  /// No description provided for @uploadLicense.
  ///
  /// In vi, this message translates to:
  /// **'Tải lên GPLX'**
  String get uploadLicense;

  /// No description provided for @licenseOcrFilled.
  ///
  /// In vi, this message translates to:
  /// **'OCR đã tự điền thông tin GPLX'**
  String get licenseOcrFilled;

  /// No description provided for @criminalRecordInstruction.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng cung cấp Lý lịch tư pháp (Bản số 1 hoặc số 2) được cấp không quá 6 tháng để đảm bảo an toàn cho hành khách.'**
  String get criminalRecordInstruction;

  /// No description provided for @reviewWithinHours.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ của bạn sẽ được xét duyệt trong vòng 24-48 giờ làm việc.'**
  String get reviewWithinHours;

  /// No description provided for @submittingApplication.
  ///
  /// In vi, this message translates to:
  /// **'Đang gửi hồ sơ...'**
  String get submittingApplication;

  /// No description provided for @completeAndSubmit.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn tất & Gửi hồ sơ'**
  String get completeAndSubmit;

  /// No description provided for @stepThreeOfThree.
  ///
  /// In vi, this message translates to:
  /// **'Bước 3/3'**
  String get stepThreeOfThree;

  /// No description provided for @uploadCriminalRecord.
  ///
  /// In vi, this message translates to:
  /// **'Tải lên Lý lịch tư pháp'**
  String get uploadCriminalRecord;

  /// No description provided for @uploadRequirements.
  ///
  /// In vi, this message translates to:
  /// **'Yêu cầu tải lên'**
  String get uploadRequirements;

  /// No description provided for @clearNoGlare.
  ///
  /// In vi, this message translates to:
  /// **'Ảnh chụp rõ nét, không bị lóa sáng.'**
  String get clearNoGlare;

  /// No description provided for @allFourCorners.
  ///
  /// In vi, this message translates to:
  /// **'Hiển thị đầy đủ 4 góc của tài liệu.'**
  String get allFourCorners;

  /// No description provided for @supportedDocumentFormats.
  ///
  /// In vi, this message translates to:
  /// **'Định dạng hỗ trợ: JPG, PNG, PDF (Tối đa 10MB).'**
  String get supportedDocumentFormats;

  /// No description provided for @tapToUploadDocument.
  ///
  /// In vi, this message translates to:
  /// **'Nhấn để tải lên hoặc kéo thả file vào đây'**
  String get tapToUploadDocument;

  /// No description provided for @photoOrPdfSupported.
  ///
  /// In vi, this message translates to:
  /// **'Hỗ trợ ảnh chụp hoặc file scan (.pdf)'**
  String get photoOrPdfSupported;

  /// No description provided for @chooseDocument.
  ///
  /// In vi, this message translates to:
  /// **'Chọn tài liệu'**
  String get chooseDocument;

  /// No description provided for @documentSelected.
  ///
  /// In vi, this message translates to:
  /// **'Đã chọn tài liệu'**
  String get documentSelected;

  /// No description provided for @change.
  ///
  /// In vi, this message translates to:
  /// **'Thay đổi'**
  String get change;

  /// No description provided for @criminalRecordOcrRead.
  ///
  /// In vi, this message translates to:
  /// **'OCR đã đọc nội dung lý lịch tư pháp'**
  String get criminalRecordOcrRead;

  /// No description provided for @criminalRecordScanned.
  ///
  /// In vi, this message translates to:
  /// **'Đã quét OCR lý lịch tư pháp.'**
  String get criminalRecordScanned;

  /// No description provided for @documentOcrFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể quét OCR từ tài liệu này.'**
  String get documentOcrFailed;

  /// No description provided for @applicationSubmitted.
  ///
  /// In vi, this message translates to:
  /// **'Gửi hồ sơ thành công!'**
  String get applicationSubmitted;

  /// No description provided for @applicationProcessing.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ của bạn đang được xử lý. Chúng tôi sẽ thông báo kết quả cho bạn sớm nhất.'**
  String get applicationProcessing;

  /// No description provided for @applicationSubmitFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi hồ sơ. Vui lòng thử lại.'**
  String get applicationSubmitFailed;

  /// No description provided for @tripEndedWithId.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến #{id} đã kết thúc.'**
  String tripEndedWithId(int id);

  /// No description provided for @searchingDriver.
  ///
  /// In vi, this message translates to:
  /// **'Đang tìm tài xế cho bạn...'**
  String get searchingDriver;

  /// No description provided for @cancelling.
  ///
  /// In vi, this message translates to:
  /// **'Đang hủy...'**
  String get cancelling;

  /// No description provided for @cancelBooking.
  ///
  /// In vi, this message translates to:
  /// **'Hủy chuyến'**
  String get cancelBooking;

  /// No description provided for @remainingCountdown.
  ///
  /// In vi, this message translates to:
  /// **'{message} - Còn {countdown}'**
  String remainingCountdown(String message, String countdown);

  /// No description provided for @estimatedWaitTime.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian chờ dự kiến: ~2 phút'**
  String get estimatedWaitTime;

  /// No description provided for @tripCodeWithStatus.
  ///
  /// In vi, this message translates to:
  /// **'Mã chuyến #{id} • {status}'**
  String tripCodeWithStatus(int id, String status);

  /// No description provided for @secondsRemaining.
  ///
  /// In vi, this message translates to:
  /// **'Còn {seconds} giây'**
  String secondsRemaining(int seconds);

  /// No description provided for @suitableDriverReady.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế phù hợp đã sẵn sàng'**
  String get suitableDriverReady;

  /// No description provided for @reviewProfileAndConfirm.
  ///
  /// In vi, this message translates to:
  /// **'Xem hồ sơ và xác nhận thuê{countdown}.'**
  String reviewProfileAndConfirm(String countdown);

  /// No description provided for @viewProfile.
  ///
  /// In vi, this message translates to:
  /// **'Xem hồ sơ'**
  String get viewProfile;

  /// No description provided for @waitingDriverAccept.
  ///
  /// In vi, this message translates to:
  /// **'Đang chờ tài xế nhận chuyến'**
  String get waitingDriverAccept;

  /// No description provided for @appliedCode.
  ///
  /// In vi, this message translates to:
  /// **'Mã đã áp dụng'**
  String get appliedCode;

  /// No description provided for @promotionWithCode.
  ///
  /// In vi, this message translates to:
  /// **'Khuyến mãi ({code}):'**
  String promotionWithCode(String code);

  /// No description provided for @currentLocationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể lấy vị trí hiện tại: {error}'**
  String currentLocationFailed(String error);

  /// No description provided for @callUnavailableSessionExpired.
  ///
  /// In vi, this message translates to:
  /// **'Chưa thể gọi khi phiên đã hết hạn.'**
  String get callUnavailableSessionExpired;

  /// No description provided for @customer.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng'**
  String get customer;

  /// No description provided for @incomingCall.
  ///
  /// In vi, this message translates to:
  /// **'Cuộc gọi đến'**
  String get incomingCall;

  /// No description provided for @customerCalling.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng đang gọi cho bạn.'**
  String get customerCalling;

  /// No description provided for @decline.
  ///
  /// In vi, this message translates to:
  /// **'Từ chối'**
  String get decline;

  /// No description provided for @answer.
  ///
  /// In vi, this message translates to:
  /// **'Nghe máy'**
  String get answer;

  /// No description provided for @onlineLocationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể lấy vị trí hiện tại hoặc không thể online: {error}'**
  String onlineLocationFailed(String error);

  /// No description provided for @chatUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Không thể mở trò chuyện lúc này.'**
  String get chatUnavailable;

  /// No description provided for @gpsSimulationEnabled.
  ///
  /// In vi, this message translates to:
  /// **'Đã bật mô phỏng GPS (Backend)'**
  String get gpsSimulationEnabled;

  /// No description provided for @gpsSimulationDisabled.
  ///
  /// In vi, this message translates to:
  /// **'Đã tắt mô phỏng GPS, dùng GPS thật'**
  String get gpsSimulationDisabled;

  /// No description provided for @activeTrip.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đang thực hiện'**
  String get activeTrip;

  /// No description provided for @message.
  ///
  /// In vi, this message translates to:
  /// **'Nhắn tin'**
  String get message;

  /// No description provided for @callCustomer.
  ///
  /// In vi, this message translates to:
  /// **'Gọi khách'**
  String get callCustomer;

  /// No description provided for @processing.
  ///
  /// In vi, this message translates to:
  /// **'Đang xử lý...'**
  String get processing;

  /// No description provided for @startPickup.
  ///
  /// In vi, this message translates to:
  /// **'Bắt đầu đến đón'**
  String get startPickup;

  /// No description provided for @driverArrived.
  ///
  /// In vi, this message translates to:
  /// **'Đã tới đón'**
  String get driverArrived;

  /// No description provided for @startTrip.
  ///
  /// In vi, this message translates to:
  /// **'Bắt đầu chuyến'**
  String get startTrip;

  /// No description provided for @endTrip.
  ///
  /// In vi, this message translates to:
  /// **'Kết thúc chuyến'**
  String get endTrip;

  /// No description provided for @waitingCustomerReturnConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Đang chờ khách xác nhận trả xe.\nNếu khách không phản hồi, bạn có thể xác nhận thay.'**
  String get waitingCustomerReturnConfirmation;

  /// No description provided for @confirmReturnWithEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thay bằng ảnh bằng chứng'**
  String get confirmReturnWithEvidence;

  /// No description provided for @returnConfirmedCompleting.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác nhận trả xe. Đang hoàn tất chuyến đi...'**
  String get returnConfirmedCompleting;

  /// No description provided for @returnConfirmedPaymentRequired.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác nhận trả xe. Vui lòng xác nhận thanh toán để hoàn tất chuyến đi.'**
  String get returnConfirmedPaymentRequired;

  /// No description provided for @confirmPayment.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thanh toán'**
  String get confirmPayment;

  /// No description provided for @statusAccepted.
  ///
  /// In vi, this message translates to:
  /// **'Đã nhận chuyến'**
  String get statusAccepted;

  /// No description provided for @statusArrived.
  ///
  /// In vi, this message translates to:
  /// **'Đã tới điểm đón'**
  String get statusArrived;

  /// No description provided for @waitingReturnConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Chờ xác nhận trả xe'**
  String get waitingReturnConfirmation;

  /// No description provided for @returnConfirmedStatus.
  ///
  /// In vi, this message translates to:
  /// **'Đã xác nhận trả xe'**
  String get returnConfirmedStatus;

  /// No description provided for @tripStatusUpdateFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể cập nhật trạng thái chuyến.'**
  String get tripStatusUpdateFailed;

  /// No description provided for @todayIncomeUpper.
  ///
  /// In vi, this message translates to:
  /// **'THU NHẬP HÔM NAY'**
  String get todayIncomeUpper;

  /// No description provided for @tripCountShort.
  ///
  /// In vi, this message translates to:
  /// **'{count} chuyến'**
  String tripCountShort(int count);

  /// No description provided for @waitingConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Đang đợi xác nhận'**
  String get waitingConfirmation;

  /// No description provided for @waitingCustomerDriverConfirmation.
  ///
  /// In vi, this message translates to:
  /// **'Đang đợi khách hàng xác nhận tài xế. Vui lòng không tắt ứng dụng.'**
  String get waitingCustomerDriverConfirmation;

  /// No description provided for @newTripAvailable.
  ///
  /// In vi, this message translates to:
  /// **'Bạn đã có chuyến mới!'**
  String get newTripAvailable;

  /// No description provided for @expectedIncomeUpper.
  ///
  /// In vi, this message translates to:
  /// **'THU NHẬP DỰ KIẾN'**
  String get expectedIncomeUpper;

  /// No description provided for @pickupCustomerUpper.
  ///
  /// In vi, this message translates to:
  /// **'ĐÓN KHÁCH'**
  String get pickupCustomerUpper;

  /// No description provided for @pickupPointA.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đón (A)'**
  String get pickupPointA;

  /// No description provided for @destinationPointB.
  ///
  /// In vi, this message translates to:
  /// **'Điểm đến (B)'**
  String get destinationPointB;

  /// No description provided for @accept.
  ///
  /// In vi, this message translates to:
  /// **'Chấp nhận'**
  String get accept;

  /// No description provided for @selectPickupDate.
  ///
  /// In vi, this message translates to:
  /// **'Chọn ngày đón'**
  String get selectPickupDate;

  /// No description provided for @selectPickupTimeHelp.
  ///
  /// In vi, this message translates to:
  /// **'Chọn giờ đón'**
  String get selectPickupTimeHelp;

  /// No description provided for @invalidSchedule.
  ///
  /// In vi, this message translates to:
  /// **'Thời gian đặt trước phải cách hiện tại ít nhất 30 phút.'**
  String get invalidSchedule;

  /// No description provided for @selectPickupRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chọn điểm đón.'**
  String get selectPickupRequired;

  /// No description provided for @selectServiceAndVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chọn dịch vụ và xe.'**
  String get selectServiceAndVehicle;

  /// No description provided for @selectDestinationRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chọn điểm đến.'**
  String get selectDestinationRequired;

  /// No description provided for @selectPickupTimeRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng chọn thời gian đón.'**
  String get selectPickupTimeRequired;

  /// No description provided for @fareEstimateUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có giá dự kiến. Vui lòng kiểm tra lại tuyến đường.'**
  String get fareEstimateUnavailable;

  /// No description provided for @bookingFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể đặt chuyến lúc này. Vui lòng thử lại.'**
  String get bookingFailed;

  /// No description provided for @bookingSuccess.
  ///
  /// In vi, this message translates to:
  /// **'Đặt chuyến thành công'**
  String get bookingSuccess;

  /// No description provided for @addVehicleFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể thêm xe. Vui lòng thử lại.'**
  String get addVehicleFailed;

  /// No description provided for @vehicleAdded.
  ///
  /// In vi, this message translates to:
  /// **'Đã thêm xe mới.'**
  String get vehicleAdded;

  /// No description provided for @selectYourVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Chọn xe của bạn'**
  String get selectYourVehicle;

  /// No description provided for @loadingServices.
  ///
  /// In vi, this message translates to:
  /// **'Đang tải thông tin dịch vụ...'**
  String get loadingServices;

  /// No description provided for @specialRequest.
  ///
  /// In vi, this message translates to:
  /// **'Yêu cầu đặc biệt (không bắt buộc)'**
  String get specialRequest;

  /// No description provided for @fareCalculationNote.
  ///
  /// In vi, this message translates to:
  /// **'Cước được chấp nhận sẽ được khóa khi bạn đặt chuyến.'**
  String get fareCalculationNote;

  /// No description provided for @confirmScheduled.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận đặt trước'**
  String get confirmScheduled;

  /// No description provided for @confirmHourlyHire.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận thuê theo giờ'**
  String get confirmHourlyHire;

  /// No description provided for @confirmNow.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận đặt ngay'**
  String get confirmNow;

  /// No description provided for @selectPickup.
  ///
  /// In vi, this message translates to:
  /// **'Chọn điểm đón'**
  String get selectPickup;

  /// No description provided for @selectDestination.
  ///
  /// In vi, this message translates to:
  /// **'Chọn điểm đến'**
  String get selectDestination;

  /// No description provided for @calculatingFare.
  ///
  /// In vi, this message translates to:
  /// **'Đang tính giá dự kiến...'**
  String get calculatingFare;

  /// No description provided for @hoursValue.
  ///
  /// In vi, this message translates to:
  /// **'{hours} giờ'**
  String hoursValue(int hours);

  /// No description provided for @surgePricing.
  ///
  /// In vi, this message translates to:
  /// **'Giá đang tăng do nhu cầu cao (x{multiplier})'**
  String surgePricing(num multiplier);

  /// No description provided for @estimatedRentalHours.
  ///
  /// In vi, this message translates to:
  /// **'{hours} giờ thuê dự kiến'**
  String estimatedRentalHours(int hours);

  /// No description provided for @addPromoCode.
  ///
  /// In vi, this message translates to:
  /// **'Thêm mã khuyến mãi'**
  String get addPromoCode;

  /// No description provided for @tripService.
  ///
  /// In vi, this message translates to:
  /// **'Theo chuyến'**
  String get tripService;

  /// No description provided for @hourlyService.
  ///
  /// In vi, this message translates to:
  /// **'Theo giờ'**
  String get hourlyService;

  /// No description provided for @addNewVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Thêm xe mới'**
  String get addNewVehicle;

  /// No description provided for @saveVehicleAndContinue.
  ///
  /// In vi, this message translates to:
  /// **'Lưu xe vào tài khoản rồi tiếp tục đặt chuyến.'**
  String get saveVehicleAndContinue;

  /// No description provided for @add.
  ///
  /// In vi, this message translates to:
  /// **'Thêm'**
  String get add;

  /// No description provided for @plateNumberLabel.
  ///
  /// In vi, this message translates to:
  /// **'Biển số: {value}'**
  String plateNumberLabel(String value);

  /// No description provided for @vehicleColorLabel.
  ///
  /// In vi, this message translates to:
  /// **'Màu: {value}'**
  String vehicleColorLabel(String value);

  /// No description provided for @noBookableVehicles.
  ///
  /// In vi, this message translates to:
  /// **'Bạn chưa có xe hợp lệ để đặt chuyến. Vui lòng thêm xe trước khi đặt.'**
  String get noBookableVehicles;

  /// No description provided for @mapsConfigMissing.
  ///
  /// In vi, this message translates to:
  /// **'Bản đồ chưa được cấu hình. Vui lòng thử lại sau.'**
  String get mapsConfigMissing;

  /// No description provided for @serverDisconnectedRetrying.
  ///
  /// In vi, this message translates to:
  /// **'Mất kết nối tới máy chủ. Đang thử kết nối lại...'**
  String get serverDisconnectedRetrying;

  /// No description provided for @tripCancelled.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi đã được hủy.'**
  String get tripCancelled;

  /// No description provided for @driverLocationTrackingRetrying.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết nối theo dõi vị trí tài xế. Đang thử lại...'**
  String get driverLocationTrackingRetrying;

  /// No description provided for @safetyCheck.
  ///
  /// In vi, this message translates to:
  /// **'Kiểm tra an toàn'**
  String get safetyCheck;

  /// No description provided for @safetyConfirmed.
  ///
  /// In vi, this message translates to:
  /// **'SafeRide đã ghi nhận bạn vẫn an toàn.'**
  String get safetyConfirmed;

  /// No description provided for @iAmSafe.
  ///
  /// In vi, this message translates to:
  /// **'Tôi vẫn an toàn'**
  String get iAmSafe;

  /// No description provided for @callDriver.
  ///
  /// In vi, this message translates to:
  /// **'Gọi tài xế'**
  String get callDriver;

  /// No description provided for @activateSosQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Kích hoạt SOS khẩn cấp?'**
  String get activateSosQuestion;

  /// No description provided for @activateSosDescription.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chắc muốn gửi tín hiệu khẩn cấp cho chuyến đi này không?'**
  String get activateSosDescription;

  /// No description provided for @activateSos.
  ///
  /// In vi, this message translates to:
  /// **'Kích hoạt SOS khẩn cấp'**
  String get activateSos;

  /// No description provided for @sosActivationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kích hoạt SOS. Vui lòng thử lại.'**
  String get sosActivationFailed;

  /// No description provided for @sosLocationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể lấy vị trí hiện tại để kích hoạt SOS.'**
  String get sosLocationFailed;

  /// No description provided for @emergencyHelpMessage.
  ///
  /// In vi, this message translates to:
  /// **'Tôi cần hỗ trợ khẩn cấp'**
  String get emergencyHelpMessage;

  /// No description provided for @sosActivatedForTrip.
  ///
  /// In vi, this message translates to:
  /// **'SOS đã được kích hoạt cho chuyến đi này.'**
  String get sosActivatedForTrip;

  /// No description provided for @sosActivatedHelpComing.
  ///
  /// In vi, this message translates to:
  /// **'SOS đã được kích hoạt. Hệ thống sẽ hỗ trợ bạn sớm nhất.'**
  String get sosActivatedHelpComing;

  /// No description provided for @driverAtPickup.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế đã đến điểm đón'**
  String get driverAtPickup;

  /// No description provided for @waitingDriverPayment.
  ///
  /// In vi, this message translates to:
  /// **'Chờ thanh toán cho tài xế'**
  String get waitingDriverPayment;

  /// No description provided for @driverArrivingMinutes.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế đang đến • {minutes} phút'**
  String driverArrivingMinutes(int minutes);

  /// No description provided for @movingMinutes.
  ///
  /// In vi, this message translates to:
  /// **'Đang di chuyển • {minutes} phút'**
  String movingMinutes(int minutes);

  /// No description provided for @onCorrectRoute.
  ///
  /// In vi, this message translates to:
  /// **'Bạn đang đi đúng lộ trình'**
  String get onCorrectRoute;

  /// No description provided for @safeRideDriverName.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế SafeRide'**
  String get safeRideDriverName;

  /// No description provided for @updatingVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Đang cập nhật xe'**
  String get updatingVehicle;

  /// No description provided for @prepayWithPayos.
  ///
  /// In vi, this message translates to:
  /// **'Thanh toán trước bằng PayOS'**
  String get prepayWithPayos;

  /// No description provided for @call.
  ///
  /// In vi, this message translates to:
  /// **'Gọi điện'**
  String get call;

  /// No description provided for @share.
  ///
  /// In vi, this message translates to:
  /// **'Chia sẻ'**
  String get share;

  /// No description provided for @payDriverToComplete.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.'**
  String get payDriverToComplete;

  /// No description provided for @endingTrip.
  ///
  /// In vi, this message translates to:
  /// **'Đang kết thúc...'**
  String get endingTrip;

  /// No description provided for @tripNotReadyForPayment.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi chưa sẵn sàng để thanh toán.'**
  String get tripNotReadyForPayment;

  /// No description provided for @tripNotReadyForChat.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi chưa sẵn sàng để trò chuyện.'**
  String get tripNotReadyForChat;

  /// No description provided for @chatAccountUnknown.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xác định tài khoản để mở trò chuyện.'**
  String get chatAccountUnknown;

  /// No description provided for @tripNotReadyForCall.
  ///
  /// In vi, this message translates to:
  /// **'Chưa thể gọi khi chuyến đi chưa sẵn sàng.'**
  String get tripNotReadyForCall;

  /// No description provided for @driverCalling.
  ///
  /// In vi, this message translates to:
  /// **'{driverName} đang gọi cho bạn.'**
  String driverCalling(String driverName);

  /// No description provided for @tripCannotEndNow.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết thúc chuyến lúc này.'**
  String get tripCannotEndNow;

  /// No description provided for @tripEndFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết thúc chuyến. Vui lòng thử lại.'**
  String get tripEndFailed;

  /// No description provided for @sosActivated.
  ///
  /// In vi, this message translates to:
  /// **'SOS khẩn cấp đã kích hoạt'**
  String get sosActivated;

  /// No description provided for @sendingSos.
  ///
  /// In vi, this message translates to:
  /// **'Đang gửi tín hiệu SOS khẩn cấp...'**
  String get sendingSos;

  /// No description provided for @shareRoute.
  ///
  /// In vi, this message translates to:
  /// **'Chia sẻ lộ trình'**
  String get shareRoute;

  /// No description provided for @shareRouteDescription.
  ///
  /// In vi, this message translates to:
  /// **'Gửi link bên dưới cho người thân để theo dõi chuyến đi của bạn theo thời gian thực.'**
  String get shareRouteDescription;

  /// No description provided for @linkCopied.
  ///
  /// In vi, this message translates to:
  /// **'Đã sao chép liên kết'**
  String get linkCopied;

  /// No description provided for @close.
  ///
  /// In vi, this message translates to:
  /// **'Đóng'**
  String get close;

  /// No description provided for @enableLocationForPickup.
  ///
  /// In vi, this message translates to:
  /// **'Hãy bật vị trí để SafeRide tự dùng GPS làm điểm đón.'**
  String get enableLocationForPickup;

  /// No description provided for @microphonePermissionRequired.
  ///
  /// In vi, this message translates to:
  /// **'Bạn cần cho phép SafeRide dùng micro.'**
  String get microphonePermissionRequired;

  /// No description provided for @voiceMessage.
  ///
  /// In vi, this message translates to:
  /// **'Tin nhắn thoại'**
  String get voiceMessage;

  /// No description provided for @currentGpsUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Không lấy được GPS hiện tại. Vui lòng bật vị trí rồi thử lại.'**
  String get currentGpsUnavailable;

  /// No description provided for @audioUploadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi file ghi âm. Vui lòng thử lại.'**
  String get audioUploadFailed;

  /// No description provided for @aiAssistantUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Trợ lý AI đang gặp sự cố. Vui lòng thử lại sau.'**
  String get aiAssistantUnavailable;

  /// No description provided for @aiAssistantConnectionFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết nối với trợ lý AI. Vui lòng thử lại.'**
  String get aiAssistantConnectionFailed;

  /// No description provided for @aiBookingFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể đặt chuyến.'**
  String get aiBookingFailed;

  /// No description provided for @conversationOpenFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể mở cuộc trò chuyện.'**
  String get conversationOpenFailed;

  /// No description provided for @recording.
  ///
  /// In vi, this message translates to:
  /// **'Đang ghi âm...'**
  String get recording;

  /// No description provided for @sendOrCancelRecording.
  ///
  /// In vi, this message translates to:
  /// **'Chọn gửi hoặc hủy bản ghi'**
  String get sendOrCancelRecording;

  /// No description provided for @aiMessageHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhắn cho trợ lý SafeRide...'**
  String get aiMessageHint;

  /// No description provided for @cancelVoice.
  ///
  /// In vi, this message translates to:
  /// **'Hủy voice'**
  String get cancelVoice;

  /// No description provided for @sendVoice.
  ///
  /// In vi, this message translates to:
  /// **'Gửi voice'**
  String get sendVoice;

  /// No description provided for @voiceInput.
  ///
  /// In vi, this message translates to:
  /// **'Nhập bằng giọng nói'**
  String get voiceInput;

  /// No description provided for @vehicleSelectedByQuery.
  ///
  /// In vi, this message translates to:
  /// **'Đã chọn xe theo “{query}”.'**
  String vehicleSelectedByQuery(String query);

  /// No description provided for @vehicleQueryNotFound.
  ///
  /// In vi, this message translates to:
  /// **'Không tìm thấy chính xác xe “{query}”. Vui lòng chọn lại.'**
  String vehicleQueryNotFound(String query);

  /// No description provided for @promoApplied.
  ///
  /// In vi, this message translates to:
  /// **'Đã áp dụng mã {code}.'**
  String promoApplied(String code);

  /// No description provided for @promoUnavailable.
  ///
  /// In vi, this message translates to:
  /// **'Mã {code} không khả dụng.'**
  String promoUnavailable(String code);

  /// No description provided for @conversationHistoryLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải lịch sử trò chuyện.'**
  String get conversationHistoryLoadFailed;

  /// No description provided for @deleteConversationQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Xóa cuộc trò chuyện?'**
  String get deleteConversationQuestion;

  /// No description provided for @deleteConversationDescription.
  ///
  /// In vi, this message translates to:
  /// **'“{title}” và các file ghi âm liên quan sẽ bị xóa vĩnh viễn.'**
  String deleteConversationDescription(String title);

  /// No description provided for @conversationDeleteFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể xóa cuộc trò chuyện. Vui lòng thử lại.'**
  String get conversationDeleteFailed;

  /// No description provided for @conversationHistory.
  ///
  /// In vi, this message translates to:
  /// **'Lịch sử trò chuyện'**
  String get conversationHistory;

  /// No description provided for @noConversations.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có cuộc trò chuyện nào.'**
  String get noConversations;

  /// No description provided for @deleteConversation.
  ///
  /// In vi, this message translates to:
  /// **'Xóa cuộc trò chuyện'**
  String get deleteConversation;

  /// No description provided for @safeRideAssistantTitle.
  ///
  /// In vi, this message translates to:
  /// **'Trợ lý SafeRide'**
  String get safeRideAssistantTitle;

  /// No description provided for @aiDisclaimer.
  ///
  /// In vi, this message translates to:
  /// **'AI có thể mắc lỗi • Kiểm tra trước khi đặt'**
  String get aiDisclaimer;

  /// No description provided for @newChat.
  ///
  /// In vi, this message translates to:
  /// **'Đoạn chat mới'**
  String get newChat;

  /// No description provided for @back.
  ///
  /// In vi, this message translates to:
  /// **'Quay lại'**
  String get back;

  /// No description provided for @chooseVehicleQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Bạn muốn đi bằng xe nào?'**
  String get chooseVehicleQuestion;

  /// No description provided for @chooseDiscountCode.
  ///
  /// In vi, this message translates to:
  /// **'Chọn mã giảm giá'**
  String get chooseDiscountCode;

  /// No description provided for @confirmTrip.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận chuyến đi'**
  String get confirmTrip;

  /// No description provided for @yourVehicles.
  ///
  /// In vi, this message translates to:
  /// **'Xe của bạn'**
  String get yourVehicles;

  /// No description provided for @newVehicle.
  ///
  /// In vi, this message translates to:
  /// **'Xe mới'**
  String get newVehicle;

  /// No description provided for @noVehicleForAiBooking.
  ///
  /// In vi, this message translates to:
  /// **'Bạn chưa có xe. Hãy thêm xe để tiếp tục đặt chuyến.'**
  String get noVehicleForAiBooking;

  /// No description provided for @continueChooseDiscount.
  ///
  /// In vi, this message translates to:
  /// **'Tiếp tục chọn mã giảm giá'**
  String get continueChooseDiscount;

  /// No description provided for @noDiscountAvailable.
  ///
  /// In vi, this message translates to:
  /// **'Hiện không có mã giảm giá khả dụng.'**
  String get noDiscountAvailable;

  /// No description provided for @noDiscount.
  ///
  /// In vi, this message translates to:
  /// **'Không dùng mã giảm giá'**
  String get noDiscount;

  /// No description provided for @continueWithoutDiscount.
  ///
  /// In vi, this message translates to:
  /// **'Tiếp tục không dùng mã'**
  String get continueWithoutDiscount;

  /// No description provided for @usePromoCode.
  ///
  /// In vi, this message translates to:
  /// **'Dùng mã {code}'**
  String usePromoCode(String code);

  /// No description provided for @notUsed.
  ///
  /// In vi, this message translates to:
  /// **'Không sử dụng'**
  String get notUsed;

  /// No description provided for @confirmAndFindDriverAi.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận và tìm tài xế'**
  String get confirmAndFindDriverAi;

  /// No description provided for @aiWelcome.
  ///
  /// In vi, this message translates to:
  /// **'Xin chào! Mình có thể hỗ trợ bạn sử dụng SafeRide hoặc chuẩn bị một chuyến đi.\n\nVí dụ: “Đặt xe từ Đại học FPT đến sân bay Tân Sơn Nhất”.'**
  String get aiWelcome;

  /// No description provided for @slogan.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến đi an toàn, tin cậy tuyệt đối'**
  String get slogan;

  /// No description provided for @phoneNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số điện thoại'**
  String get phoneNumber;

  /// No description provided for @phoneHint.
  ///
  /// In vi, this message translates to:
  /// **'Nhập số điện thoại'**
  String get phoneHint;

  /// No description provided for @continueOrRegister.
  ///
  /// In vi, this message translates to:
  /// **'Tiếp tục / Đăng ký'**
  String get continueOrRegister;

  /// No description provided for @phoneRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập số điện thoại'**
  String get phoneRequired;

  /// No description provided for @invalidPhone.
  ///
  /// In vi, this message translates to:
  /// **'Số điện thoại không hợp lệ'**
  String get invalidPhone;

  /// No description provided for @sendOtpFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi OTP. Kiểm tra API hoặc số điện thoại.'**
  String get sendOtpFailed;

  /// No description provided for @or.
  ///
  /// In vi, this message translates to:
  /// **'HOẶC'**
  String get or;

  /// No description provided for @googleLoginFailed.
  ///
  /// In vi, this message translates to:
  /// **'Đăng nhập Google thất bại'**
  String get googleLoginFailed;

  /// No description provided for @continueAgreement.
  ///
  /// In vi, this message translates to:
  /// **'Bằng việc tiếp tục, bạn đồng ý với '**
  String get continueAgreement;

  /// No description provided for @and.
  ///
  /// In vi, this message translates to:
  /// **' và '**
  String get and;

  /// No description provided for @agreementSuffix.
  ///
  /// In vi, this message translates to:
  /// **' của chúng tôi.'**
  String get agreementSuffix;

  /// No description provided for @otpTitle.
  ///
  /// In vi, this message translates to:
  /// **'Xác thực mã OTP'**
  String get otpTitle;

  /// No description provided for @resendAfter.
  ///
  /// In vi, this message translates to:
  /// **'Gửi lại sau '**
  String get resendAfter;

  /// No description provided for @resendOtp.
  ///
  /// In vi, this message translates to:
  /// **'Gửi lại OTP'**
  String get resendOtp;

  /// No description provided for @otpResent.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi lại OTP.'**
  String get otpResent;

  /// No description provided for @resendOtpFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi lại OTP.'**
  String get resendOtpFailed;

  /// No description provided for @otpRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập đủ 6 số OTP'**
  String get otpRequired;

  /// No description provided for @invalidOtp.
  ///
  /// In vi, this message translates to:
  /// **'OTP không đúng hoặc đã hết hạn'**
  String get invalidOtp;

  /// No description provided for @otpLockedPrefix.
  ///
  /// In vi, this message translates to:
  /// **'Bạn nhập sai OTP quá nhiều lần. Thử lại sau '**
  String get otpLockedPrefix;

  /// No description provided for @otpAttemptsExceeded.
  ///
  /// In vi, this message translates to:
  /// **'Bạn đã nhập sai OTP quá nhiều lần. Vui lòng yêu cầu mã mới.'**
  String get otpAttemptsExceeded;

  /// No description provided for @otpDescription.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập mã gồm 6 chữ số đã được\ngửi đến {phoneNumber}.'**
  String otpDescription(String phoneNumber);

  /// No description provided for @welcome.
  ///
  /// In vi, this message translates to:
  /// **'Chào mừng bạn!'**
  String get welcome;

  /// No description provided for @selectRoleQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Bạn muốn bắt đầu với vai trò nào?'**
  String get selectRoleQuestion;

  /// No description provided for @customerRoleTitle.
  ///
  /// In vi, this message translates to:
  /// **'Tôi là Khách hàng'**
  String get customerRoleTitle;

  /// No description provided for @customerRoleDescription.
  ///
  /// In vi, this message translates to:
  /// **'Đặt xe nhanh chóng, an toàn và theo dõi hành trình trực tiếp.'**
  String get customerRoleDescription;

  /// No description provided for @driverRoleTitle.
  ///
  /// In vi, this message translates to:
  /// **'Tôi là Tài xế'**
  String get driverRoleTitle;

  /// No description provided for @driverRoleDescription.
  ///
  /// In vi, this message translates to:
  /// **'Nhận việc linh hoạt, tăng thu nhập và quản lý chuyến đi dễ dàng.'**
  String get driverRoleDescription;

  /// No description provided for @rememberRole.
  ///
  /// In vi, this message translates to:
  /// **'Ghi nhớ lựa chọn cho lần sau'**
  String get rememberRole;

  /// No description provided for @completeProfile.
  ///
  /// In vi, this message translates to:
  /// **'Hoàn thiện thông tin'**
  String get completeProfile;

  /// No description provided for @changeAvatar.
  ///
  /// In vi, this message translates to:
  /// **'Thay đổi ảnh đại diện'**
  String get changeAvatar;

  /// No description provided for @verifiedPhone.
  ///
  /// In vi, this message translates to:
  /// **'Số điện thoại đã xác minh'**
  String get verifiedPhone;

  /// No description provided for @updateInformationHint.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng cập nhật thông tin cá nhân để tiếp tục.'**
  String get updateInformationHint;

  /// No description provided for @email.
  ///
  /// In vi, this message translates to:
  /// **'Email'**
  String get email;

  /// No description provided for @saving.
  ///
  /// In vi, this message translates to:
  /// **'Đang lưu...'**
  String get saving;

  /// No description provided for @saveAndContinue.
  ///
  /// In vi, this message translates to:
  /// **'Lưu và tiếp tục'**
  String get saveAndContinue;

  /// No description provided for @uploadAvatarFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải ảnh đại diện lên.'**
  String get uploadAvatarFailed;

  /// No description provided for @updateProfileFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể cập nhật thông tin.'**
  String get updateProfileFailed;

  /// No description provided for @invalidFullName.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập họ và tên hợp lệ.'**
  String get invalidFullName;

  /// No description provided for @invalidEmail.
  ///
  /// In vi, this message translates to:
  /// **'Địa chỉ email không hợp lệ.'**
  String get invalidEmail;

  /// No description provided for @emailAlreadyUsed.
  ///
  /// In vi, this message translates to:
  /// **'Email đã được sử dụng bởi tài khoản khác.'**
  String get emailAlreadyUsed;

  /// No description provided for @phoneNumberAlreadyUsed.
  ///
  /// In vi, this message translates to:
  /// **'Số điện thoại đã được sử dụng bởi tài khoản khác.'**
  String get phoneNumberAlreadyUsed;

  /// No description provided for @phoneNumberChangeRequiresVerification.
  ///
  /// In vi, this message translates to:
  /// **'Không thể thay đổi số điện thoại đã liên kết tại màn hình này.'**
  String get phoneNumberChangeRequiresVerification;

  /// No description provided for @phoneVerificationRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng xác thực OTP trước khi thêm số điện thoại.'**
  String get phoneVerificationRequired;

  /// No description provided for @appVersion.
  ///
  /// In vi, this message translates to:
  /// **'Phiên bản ứng dụng: 2.4.1'**
  String get appVersion;

  /// No description provided for @linkGoogleFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể liên kết Google.'**
  String get linkGoogleFailed;

  /// No description provided for @unlinkGoogleQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Hủy liên kết Google?'**
  String get unlinkGoogleQuestion;

  /// No description provided for @unlinkGoogleDescription.
  ///
  /// In vi, this message translates to:
  /// **'Bạn vẫn có thể đăng nhập bằng số điện thoại đã xác thực.'**
  String get unlinkGoogleDescription;

  /// No description provided for @unlinkAccount.
  ///
  /// In vi, this message translates to:
  /// **'Hủy liên kết'**
  String get unlinkAccount;

  /// No description provided for @unlinkGoogleFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể hủy liên kết Google.'**
  String get unlinkGoogleFailed;

  /// No description provided for @logoutFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể đăng xuất. Vui lòng thử lại.'**
  String get logoutFailed;

  /// No description provided for @historyFilterAll.
  ///
  /// In vi, this message translates to:
  /// **'Tất cả'**
  String get historyFilterAll;

  /// No description provided for @historyFilterCancelled.
  ///
  /// In vi, this message translates to:
  /// **'Đã hủy'**
  String get historyFilterCancelled;

  /// No description provided for @historyFilterBooked.
  ///
  /// In vi, this message translates to:
  /// **'Đã đặt'**
  String get historyFilterBooked;

  /// No description provided for @cancelledByCustomer.
  ///
  /// In vi, this message translates to:
  /// **'Đã hủy bởi khách hàng'**
  String get cancelledByCustomer;

  /// No description provided for @reported.
  ///
  /// In vi, this message translates to:
  /// **'Đã báo cáo'**
  String get reported;

  /// No description provided for @report.
  ///
  /// In vi, this message translates to:
  /// **'Báo cáo'**
  String get report;

  /// No description provided for @aiConversationFallback.
  ///
  /// In vi, this message translates to:
  /// **'Cuộc trò chuyện'**
  String get aiConversationFallback;

  /// No description provided for @chatConnectionFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết nối trò chuyện.'**
  String get chatConnectionFailed;

  /// No description provided for @chatMessageSendFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi tin nhắn.'**
  String get chatMessageSendFailed;

  /// No description provided for @chatImageSendFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi ảnh.'**
  String get chatImageSendFailed;

  /// No description provided for @routeUpdated.
  ///
  /// In vi, this message translates to:
  /// **'SafeRide đã cập nhật tuyến đường.'**
  String get routeUpdated;

  /// No description provided for @newTripMessage.
  ///
  /// In vi, this message translates to:
  /// **'Bạn có chuyến mới.'**
  String get newTripMessage;

  /// No description provided for @noInternetConnection.
  ///
  /// In vi, this message translates to:
  /// **'Không có kết nối internet'**
  String get noInternetConnection;

  /// No description provided for @connectionLost.
  ///
  /// In vi, this message translates to:
  /// **'Mất kết nối'**
  String get connectionLost;

  /// No description provided for @internetRestored.
  ///
  /// In vi, this message translates to:
  /// **'Đã khôi phục kết nối internet'**
  String get internetRestored;

  /// No description provided for @backOnline.
  ///
  /// In vi, this message translates to:
  /// **'Đã trực tuyến'**
  String get backOnline;

  /// No description provided for @calculating.
  ///
  /// In vi, this message translates to:
  /// **'Đang tính'**
  String get calculating;

  /// No description provided for @viewTripAfterAccept.
  ///
  /// In vi, this message translates to:
  /// **'Mở chi tiết chuyến sau khi nhận'**
  String get viewTripAfterAccept;

  /// No description provided for @customerCancelledDriverRequest.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng đã hủy yêu cầu đặt tài xế.'**
  String get customerCancelledDriverRequest;

  /// No description provided for @onlineFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể online. Vui lòng thử lại.'**
  String get onlineFailed;

  /// No description provided for @acceptTripFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể nhận chuyến. Vui lòng thử lại.'**
  String get acceptTripFailed;

  /// No description provided for @declineTripFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể từ chối chuyến. Vui lòng thử lại.'**
  String get declineTripFailed;

  /// No description provided for @tripRequestsLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải yêu cầu chuyến. Vui lòng thử lại.'**
  String get tripRequestsLoadFailed;

  /// No description provided for @noDestination.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có điểm đến'**
  String get noDestination;

  /// No description provided for @expiresSoon.
  ///
  /// In vi, this message translates to:
  /// **'Sắp hết hạn'**
  String get expiresSoon;

  /// No description provided for @evidencePhotoCountError.
  ///
  /// In vi, this message translates to:
  /// **'Cần từ 1 đến 3 ảnh bằng chứng.'**
  String get evidencePhotoCountError;

  /// No description provided for @activeTripLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải dữ liệu chuyến đi hiện tại. Vui lòng thử lại.'**
  String get activeTripLoadFailed;

  /// No description provided for @ratingStars.
  ///
  /// In vi, this message translates to:
  /// **'{count} sao'**
  String ratingStars(int count);

  /// No description provided for @demoGpsMode.
  ///
  /// In vi, this message translates to:
  /// **'Chế độ GPS mô phỏng'**
  String get demoGpsMode;

  /// No description provided for @serviceDisabled.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng bật dịch vụ vị trí trên thiết bị.'**
  String get serviceDisabled;

  /// No description provided for @permissionRequired.
  ///
  /// In vi, this message translates to:
  /// **'SafeRide cần quyền vị trí để xác định điểm đón.'**
  String get permissionRequired;

  /// No description provided for @locationNotFound.
  ///
  /// In vi, this message translates to:
  /// **'Không tìm thấy địa điểm phù hợp.'**
  String get locationNotFound;

  /// No description provided for @destinationRequired.
  ///
  /// In vi, this message translates to:
  /// **'Vui lòng nhập điểm đến.'**
  String get destinationRequired;

  /// No description provided for @statusLabel.
  ///
  /// In vi, this message translates to:
  /// **'Trạng thái'**
  String get statusLabel;

  /// No description provided for @selectPromotion.
  ///
  /// In vi, this message translates to:
  /// **'Chọn mã khuyến mãi'**
  String get selectPromotion;

  /// No description provided for @enterPromoCode.
  ///
  /// In vi, this message translates to:
  /// **'Nhập mã khuyến mãi'**
  String get enterPromoCode;

  /// No description provided for @apply.
  ///
  /// In vi, this message translates to:
  /// **'Áp dụng'**
  String get apply;

  /// No description provided for @expired.
  ///
  /// In vi, this message translates to:
  /// **'Hết hạn'**
  String get expired;

  /// No description provided for @statusOnline.
  ///
  /// In vi, this message translates to:
  /// **'Đang hoạt động'**
  String get statusOnline;

  /// No description provided for @statusOffline.
  ///
  /// In vi, this message translates to:
  /// **'Ngoại tuyến'**
  String get statusOffline;

  /// No description provided for @statusBusy.
  ///
  /// In vi, this message translates to:
  /// **'Đang có chuyến'**
  String get statusBusy;

  /// No description provided for @offerSent.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi tài xế'**
  String get offerSent;

  /// No description provided for @offerRejected.
  ///
  /// In vi, this message translates to:
  /// **'Đã từ chối'**
  String get offerRejected;

  /// No description provided for @offerCustomerConfirmed.
  ///
  /// In vi, this message translates to:
  /// **'Khách đã xác nhận'**
  String get offerCustomerConfirmed;

  /// No description provided for @driverEndTripRequestTitle.
  ///
  /// In vi, this message translates to:
  /// **'Yêu cầu kết thúc chuyến'**
  String get driverEndTripRequestTitle;

  /// No description provided for @driverEndTripRequestMessage.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế muốn kết thúc chuyến sớm. Cước sẽ dựa trên tiến độ tuyến đã đặt và mức cước dịch vụ tối thiểu.'**
  String get driverEndTripRequestMessage;

  /// No description provided for @continueTrip.
  ///
  /// In vi, this message translates to:
  /// **'Tiếp tục chuyến'**
  String get continueTrip;

  /// No description provided for @endTripRequestSent.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi yêu cầu kết thúc chuyến. Đang chờ khách hàng xác nhận.'**
  String get endTripRequestSent;

  /// No description provided for @endTripRequestAccepted.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng đã đồng ý kết thúc chuyến.'**
  String get endTripRequestAccepted;

  /// No description provided for @endTripRequestRejected.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng đã từ chối. Chuyến đi tiếp tục.'**
  String get endTripRequestRejected;

  /// No description provided for @endTripResponseFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể phản hồi yêu cầu kết thúc chuyến. Vui lòng thử lại.'**
  String get endTripResponseFailed;

  /// No description provided for @preTripSafetyTitle.
  ///
  /// In vi, this message translates to:
  /// **'Kiểm tra an toàn trước chuyến'**
  String get preTripSafetyTitle;

  /// No description provided for @preTripSafetyDescription.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận từng hạng mục trước khi bắt đầu. Các lần kiểm tra không đạt vẫn được lưu để kiểm toán.'**
  String get preTripSafetyDescription;

  /// No description provided for @brakeResponse.
  ///
  /// In vi, this message translates to:
  /// **'Phản hồi phanh'**
  String get brakeResponse;

  /// No description provided for @frontRearLights.
  ///
  /// In vi, this message translates to:
  /// **'Đèn trước và sau'**
  String get frontRearLights;

  /// No description provided for @turnSignals.
  ///
  /// In vi, this message translates to:
  /// **'Đèn xi-nhan'**
  String get turnSignals;

  /// No description provided for @visibleTires.
  ///
  /// In vi, this message translates to:
  /// **'Tình trạng lốp quan sát được'**
  String get visibleTires;

  /// No description provided for @dashboardWarning.
  ///
  /// In vi, this message translates to:
  /// **'Không có cảnh báo bảng điều khiển'**
  String get dashboardWarning;

  /// No description provided for @windshieldVisibility.
  ///
  /// In vi, this message translates to:
  /// **'Kính và gương quan sát rõ'**
  String get windshieldVisibility;

  /// No description provided for @noMajorVisibleIssue.
  ///
  /// In vi, this message translates to:
  /// **'Không có lỗi nghiêm trọng dễ thấy'**
  String get noMajorVisibleIssue;

  /// No description provided for @confirmSafetyCheck.
  ///
  /// In vi, this message translates to:
  /// **'Xác nhận kiểm tra'**
  String get confirmSafetyCheck;

  /// No description provided for @allChecksRequired.
  ///
  /// In vi, this message translates to:
  /// **'Tất cả hạng mục phải đạt trước khi bắt đầu chuyến.'**
  String get allChecksRequired;

  /// No description provided for @safetyTermination.
  ///
  /// In vi, this message translates to:
  /// **'Kết thúc vì an toàn'**
  String get safetyTermination;

  /// No description provided for @safetyTerminationDescription.
  ///
  /// In vi, this message translates to:
  /// **'Chuyến vẫn ở trạng thái đã hủy. Khuyến mãi không được dùng và có thể tính cước một phần nếu chuyến đã bắt đầu.'**
  String get safetyTerminationDescription;

  /// No description provided for @safetyTerminationReasonHint.
  ///
  /// In vi, this message translates to:
  /// **'Mô tả rủi ro an toàn'**
  String get safetyTerminationReasonHint;

  /// No description provided for @captureSafetyEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Chụp ảnh bằng chứng (tùy chọn)'**
  String get captureSafetyEvidence;

  /// No description provided for @retakePhoto.
  ///
  /// In vi, this message translates to:
  /// **'Chụp lại'**
  String get retakePhoto;

  /// No description provided for @reportAccident.
  ///
  /// In vi, this message translates to:
  /// **'Báo cáo tai nạn'**
  String get reportAccident;

  /// No description provided for @accidentDescriptionHint.
  ///
  /// In vi, this message translates to:
  /// **'Mô tả diễn biến và thiệt hại ban đầu'**
  String get accidentDescriptionHint;

  /// No description provided for @createAccidentReport.
  ///
  /// In vi, this message translates to:
  /// **'Tạo báo cáo'**
  String get createAccidentReport;

  /// No description provided for @accidentReported.
  ///
  /// In vi, this message translates to:
  /// **'Đã tạo báo cáo tai nạn.'**
  String get accidentReported;

  /// No description provided for @safetyTerminationFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể kết thúc chuyến vì an toàn.'**
  String get safetyTerminationFailed;

  /// No description provided for @preTripCheckFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi kiểm tra an toàn.'**
  String get preTripCheckFailed;

  /// No description provided for @riskProtectionCaseTitle.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ bảo vệ tai nạn'**
  String get riskProtectionCaseTitle;

  /// No description provided for @riskProtectionClaim.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ yêu cầu bảo vệ'**
  String get riskProtectionClaim;

  /// No description provided for @riskProtectionEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Bằng chứng'**
  String get riskProtectionEvidence;

  /// No description provided for @riskProtectionAssessment.
  ///
  /// In vi, this message translates to:
  /// **'Đánh giá trách nhiệm'**
  String get riskProtectionAssessment;

  /// No description provided for @uploadAccidentEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Thêm ảnh bằng chứng'**
  String get uploadAccidentEvidence;

  /// No description provided for @sendEvidencePhoto.
  ///
  /// In vi, this message translates to:
  /// **'Gửi ảnh'**
  String get sendEvidencePhoto;

  /// No description provided for @evidencePreviewFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể đọc ảnh đã chọn. Vui lòng chọn lại.'**
  String get evidencePreviewFailed;

  /// No description provided for @disputeLiability.
  ///
  /// In vi, this message translates to:
  /// **'Yêu cầu xem xét trách nhiệm'**
  String get disputeLiability;

  /// No description provided for @disputeReasonHint.
  ///
  /// In vi, this message translates to:
  /// **'Nêu rõ lý do cần xem xét lại kết quả đánh giá'**
  String get disputeReasonHint;

  /// No description provided for @liabilityDisputed.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi yêu cầu xem xét lại.'**
  String get liabilityDisputed;

  /// No description provided for @accidentEvidenceUploaded.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi ảnh bằng chứng.'**
  String get accidentEvidenceUploaded;

  /// No description provided for @noAccidentEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Chưa có bằng chứng nào.'**
  String get noAccidentEvidence;

  /// No description provided for @noProtectionClaim.
  ///
  /// In vi, this message translates to:
  /// **'Hồ sơ yêu cầu bảo vệ chưa được tạo.'**
  String get noProtectionClaim;

  /// No description provided for @driverLiabilities.
  ///
  /// In vi, this message translates to:
  /// **'Trách nhiệm của tôi'**
  String get driverLiabilities;

  /// No description provided for @noDriverLiabilities.
  ///
  /// In vi, this message translates to:
  /// **'Bạn chưa có trách nhiệm tài xế nào được xác nhận.'**
  String get noDriverLiabilities;

  /// No description provided for @confirmedAmount.
  ///
  /// In vi, this message translates to:
  /// **'Số tiền xác nhận'**
  String get confirmedAmount;

  /// No description provided for @paidAmount.
  ///
  /// In vi, this message translates to:
  /// **'Đã thanh toán'**
  String get paidAmount;

  /// No description provided for @outstandingAmount.
  ///
  /// In vi, this message translates to:
  /// **'Còn phải thanh toán'**
  String get outstandingAmount;

  /// No description provided for @attributableDamage.
  ///
  /// In vi, this message translates to:
  /// **'Thiệt hại đủ điều kiện do tài xế chịu trách nhiệm'**
  String get attributableDamage;

  /// No description provided for @recoveryHistory.
  ///
  /// In vi, this message translates to:
  /// **'Lịch sử thu hồi'**
  String get recoveryHistory;

  /// No description provided for @claimStatus.
  ///
  /// In vi, this message translates to:
  /// **'Trạng thái claim'**
  String get claimStatus;

  /// No description provided for @insuranceCoverage.
  ///
  /// In vi, this message translates to:
  /// **'Bảo hiểm chi trả'**
  String get insuranceCoverage;

  /// No description provided for @riskFundCoverage.
  ///
  /// In vi, this message translates to:
  /// **'Risk Fund chi trả'**
  String get riskFundCoverage;

  /// No description provided for @participantLiabilities.
  ///
  /// In vi, this message translates to:
  /// **'Trách nhiệm các bên'**
  String get participantLiabilities;

  /// No description provided for @accidentStatus.
  ///
  /// In vi, this message translates to:
  /// **'Trạng thái tai nạn'**
  String get accidentStatus;

  /// No description provided for @accidentCategory.
  ///
  /// In vi, this message translates to:
  /// **'Loại tai nạn'**
  String get accidentCategory;

  /// No description provided for @accidentOccurredAt.
  ///
  /// In vi, this message translates to:
  /// **'Thời điểm xảy ra'**
  String get accidentOccurredAt;

  /// No description provided for @safetyReportTitle.
  ///
  /// In vi, this message translates to:
  /// **'Báo cáo sự cố an toàn'**
  String get safetyReportTitle;

  /// No description provided for @unsafeCustomer.
  ///
  /// In vi, this message translates to:
  /// **'Khách hàng không an toàn'**
  String get unsafeCustomer;

  /// No description provided for @vehicleIssue.
  ///
  /// In vi, this message translates to:
  /// **'Sự cố phương tiện'**
  String get vehicleIssue;

  /// No description provided for @safetyReasonCode.
  ///
  /// In vi, this message translates to:
  /// **'Lý do'**
  String get safetyReasonCode;

  /// No description provided for @safetyReportDescription.
  ///
  /// In vi, this message translates to:
  /// **'Mô tả sự việc'**
  String get safetyReportDescription;

  /// No description provided for @requestSosEscalation.
  ///
  /// In vi, this message translates to:
  /// **'Yêu cầu SOS / chuyển cấp'**
  String get requestSosEscalation;

  /// No description provided for @requestSosEscalationHint.
  ///
  /// In vi, this message translates to:
  /// **'Gửi vị trí hiện tại và tạo cảnh báo SOS bền vững'**
  String get requestSosEscalationHint;

  /// No description provided for @safetyReportSubmitted.
  ///
  /// In vi, this message translates to:
  /// **'Đã gửi báo cáo sự cố an toàn.'**
  String get safetyReportSubmitted;

  /// No description provided for @safetyReportFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể gửi báo cáo sự cố an toàn. Vui lòng thử lại.'**
  String get safetyReportFailed;

  /// No description provided for @vehicleFaultType.
  ///
  /// In vi, this message translates to:
  /// **'Loại lỗi phương tiện'**
  String get vehicleFaultType;

  /// No description provided for @otherVehicleFault.
  ///
  /// In vi, this message translates to:
  /// **'Lỗi phương tiện khác'**
  String get otherVehicleFault;

  /// No description provided for @optionalEvidence.
  ///
  /// In vi, this message translates to:
  /// **'Bằng chứng (tùy chọn)'**
  String get optionalEvidence;

  /// No description provided for @vehicleInsurance.
  ///
  /// In vi, this message translates to:
  /// **'Bảo hiểm'**
  String get vehicleInsurance;

  /// No description provided for @addInsurance.
  ///
  /// In vi, this message translates to:
  /// **'Thêm bảo hiểm'**
  String get addInsurance;

  /// No description provided for @insuranceLoadFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể tải thông tin bảo hiểm. Vui lòng thử lại.'**
  String get insuranceLoadFailed;

  /// No description provided for @insuranceUpdateFailed.
  ///
  /// In vi, this message translates to:
  /// **'Không thể cập nhật bảo hiểm.'**
  String get insuranceUpdateFailed;

  /// No description provided for @deleteInsuranceQuestion.
  ///
  /// In vi, this message translates to:
  /// **'Xóa hợp đồng bảo hiểm?'**
  String get deleteInsuranceQuestion;

  /// No description provided for @policyNumber.
  ///
  /// In vi, this message translates to:
  /// **'Số hợp đồng'**
  String get policyNumber;

  /// No description provided for @optionalInsuranceEmpty.
  ///
  /// In vi, this message translates to:
  /// **'Bảo hiểm là tùy chọn. Phương tiện chưa có hợp đồng nào.'**
  String get optionalInsuranceEmpty;

  /// No description provided for @addInsurancePolicy.
  ///
  /// In vi, this message translates to:
  /// **'Thêm hợp đồng bảo hiểm'**
  String get addInsurancePolicy;

  /// No description provided for @editInsurancePolicy.
  ///
  /// In vi, this message translates to:
  /// **'Sửa hợp đồng bảo hiểm'**
  String get editInsurancePolicy;

  /// No description provided for @insuranceType.
  ///
  /// In vi, this message translates to:
  /// **'Loại bảo hiểm'**
  String get insuranceType;

  /// No description provided for @mandatoryTplInsurance.
  ///
  /// In vi, this message translates to:
  /// **'Trách nhiệm dân sự bắt buộc'**
  String get mandatoryTplInsurance;

  /// No description provided for @physicalDamageInsurance.
  ///
  /// In vi, this message translates to:
  /// **'Thiệt hại vật chất'**
  String get physicalDamageInsurance;

  /// No description provided for @insuranceProvider.
  ///
  /// In vi, this message translates to:
  /// **'Nhà cung cấp'**
  String get insuranceProvider;

  /// No description provided for @effectiveDate.
  ///
  /// In vi, this message translates to:
  /// **'Hiệu lực'**
  String get effectiveDate;

  /// No description provided for @insuranceCoverageLimit.
  ///
  /// In vi, this message translates to:
  /// **'Hạn mức bảo hiểm'**
  String get insuranceCoverageLimit;

  /// No description provided for @insuranceDeductible.
  ///
  /// In vi, this message translates to:
  /// **'Mức khấu trừ'**
  String get insuranceDeductible;

  /// No description provided for @optionalDocumentUrl.
  ///
  /// In vi, this message translates to:
  /// **'URL tài liệu (tùy chọn)'**
  String get optionalDocumentUrl;

  /// No description provided for @optionalInsuranceHint.
  ///
  /// In vi, this message translates to:
  /// **'Bảo hiểm không bắt buộc. Tạo hoặc sửa hợp đồng sẽ chuyển trạng thái về PENDING để Staff xác minh.'**
  String get optionalInsuranceHint;

  /// No description provided for @endTripReasonTitle.
  ///
  /// In vi, this message translates to:
  /// **'Lý do kết thúc chuyến'**
  String get endTripReasonTitle;

  /// No description provided for @endTripReasonDescription.
  ///
  /// In vi, this message translates to:
  /// **'Chọn đúng lý do. Kết thúc vì an toàn phải dùng quy trình Risk Protection riêng.'**
  String get endTripReasonDescription;

  /// No description provided for @normalCompletionReason.
  ///
  /// In vi, this message translates to:
  /// **'Đã đến điểm đến'**
  String get normalCompletionReason;

  /// No description provided for @normalCompletionReasonDescription.
  ///
  /// In vi, this message translates to:
  /// **'Áp dụng cước đã đặt.'**
  String get normalCompletionReasonDescription;

  /// No description provided for @customerRequestedStopReason.
  ///
  /// In vi, this message translates to:
  /// **'Khách yêu cầu dừng sớm'**
  String get customerRequestedStopReason;

  /// No description provided for @customerRequestedStopReasonDescription.
  ///
  /// In vi, this message translates to:
  /// **'Cước dựa trên tiến độ tuyến đã đặt và mức cước dịch vụ tối thiểu.'**
  String get customerRequestedStopReasonDescription;

  /// No description provided for @driverUnableToContinueReason.
  ///
  /// In vi, this message translates to:
  /// **'Tài xế không thể tiếp tục'**
  String get driverUnableToContinueReason;

  /// No description provided for @startedByMistakeReason.
  ///
  /// In vi, this message translates to:
  /// **'Bắt đầu chuyến do nhầm lẫn'**
  String get startedByMistakeReason;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['en', 'ja', 'ko', 'vi', 'zh'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'en':
      return AppLocalizationsEn();
    case 'ja':
      return AppLocalizationsJa();
    case 'ko':
      return AppLocalizationsKo();
    case 'vi':
      return AppLocalizationsVi();
    case 'zh':
      return AppLocalizationsZh();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
