// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Japanese (`ja`).
class AppLocalizationsJa extends AppLocalizations {
  AppLocalizationsJa([String locale = 'ja']) : super(locale);

  @override
  String get appName => 'SafeRide';

  @override
  String get language => '言語';

  @override
  String get chooseLanguage => '言語を選択';

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
  String get profileAndSettings => 'プロフィールと設定';

  @override
  String get switchToDriver => 'ドライバーモードに切り替える';

  @override
  String get startReceivingTrips => '配車依頼の受付を開始';

  @override
  String get accountSection => 'アカウント';

  @override
  String get editProfile => 'プロフィールを編集';

  @override
  String get linkedAccounts => '連携アカウント';

  @override
  String get registerAsDriver => 'ドライバーとして登録';

  @override
  String get linked => '連携済み';

  @override
  String get notLinked => '未連携';

  @override
  String get appAndNotifications => 'アプリと通知';

  @override
  String get notificationSettings => '通知設定';

  @override
  String get darkMode => 'ダークモード';

  @override
  String get supportAndLegal => 'サポートと法的情報';

  @override
  String get helpCenter => 'ヘルプセンター';

  @override
  String get privacyPolicy => 'プライバシーポリシー';

  @override
  String get termsOfService => '利用規約';

  @override
  String get logout => 'ログアウト';

  @override
  String get logoutQuestion => 'ログアウトしますか？';

  @override
  String get logoutDescription => 'アプリからログアウトしてもよろしいですか？';

  @override
  String get cancel => 'キャンセル';

  @override
  String get cannotSwitchToDriver => '進行中の乗車がある間はドライバーモードに切り替えられません。';

  @override
  String get cannotSwitchToCustomer => '進行中の乗車がある間はカスタマーモードに切り替えられません。';

  @override
  String get tripNotFound => '乗車が見つかりません。';

  @override
  String get sessionExpired => 'セッションの有効期限が切れました。再度ログインしてください。';

  @override
  String get genericError => 'エラーが発生しました。もう一度お試しください。';

  @override
  String get statusPending => '待機中';

  @override
  String get statusDriverArriving => 'ドライバーが向かっています';

  @override
  String get statusInProgress => '進行中';

  @override
  String get statusCompleted => '完了';

  @override
  String get statusCancelled => 'キャンセル済み';

  @override
  String get notifications => '通知';

  @override
  String get notificationsLoadFailed => '通知を読み込めません';

  @override
  String get retry => '再試行';

  @override
  String get noNotifications => '通知はありません';

  @override
  String get noNotificationsDescription => '承認されたシステム通知がここに表示されます。';

  @override
  String get read => '既読';

  @override
  String get unread => '未読';

  @override
  String get notificationTypePromotion => 'プロモーション';

  @override
  String get notificationTypeWarning => '警告';

  @override
  String get notificationTypeSystemUpdate => 'システム更新';

  @override
  String get loadMoreNotifications => '通知をさらに表示';

  @override
  String get success => '成功';

  @override
  String get error => 'エラー';

  @override
  String get warning => '警告';

  @override
  String get information => 'お知らせ';

  @override
  String get serverConnectionError => 'サーバーに接続できません。しばらくしてからもう一度お試しください。';

  @override
  String get serverConnectionErrorTitle => 'サーバーは一時的に利用できません';

  @override
  String get serverConnectionRestored => 'サーバーへの接続が復旧しました。データを再読み込みしています。';

  @override
  String get serverConnectionRestoredTitle => 'サーバーに再接続しました';

  @override
  String get reload => '再読み込み';

  @override
  String get confirm => '確認';

  @override
  String get callStartFailed => '通話を開始できません。もう一度お試しください。';

  @override
  String get callRejected => '相手が通話を拒否しました。';

  @override
  String get callEnded => '通話が終了しました。';

  @override
  String get callConnecting => '接続中...';

  @override
  String get callRinging => '呼び出し中...';

  @override
  String get microphoneOn => 'ミュート解除';

  @override
  String get microphoneOff => 'ミュート';

  @override
  String get endCall => '通話終了';

  @override
  String get speaker => 'スピーカー';

  @override
  String get earpiece => '受話口';

  @override
  String get imageSelectionFailed => '画像を選択できません。';

  @override
  String get chatTitle => 'チャット';

  @override
  String get chatReadOnly => '乗車が終了したため、メッセージの確認のみ可能です。';

  @override
  String get noMessages => 'メッセージはありません。';

  @override
  String get messageHint => 'メッセージを入力...';

  @override
  String get tripEnded => '乗車終了';

  @override
  String get driverReviews => 'ドライバーの評価';

  @override
  String get driverHasNoReviews => 'このドライバーにはまだ評価がありません。';

  @override
  String get allReviews => 'すべてのレビュー';

  @override
  String get reviews => '件の評価';

  @override
  String get reportIncident => '問題を報告';

  @override
  String get reportHelpQuestion => 'どのような問題ですか？';

  @override
  String get tripIncident => '乗車中の問題';

  @override
  String get paymentIssue => '支払いの問題';

  @override
  String get partyFeedback => 'ドライバー／利用者への意見';

  @override
  String get appIssue => 'アプリの不具合';

  @override
  String get wrongRoute => 'ドライバーが経路を外れた';

  @override
  String get driverLate => 'ドライバーの到着が遅れた';

  @override
  String get inappropriateBehavior => '不適切な態度';

  @override
  String get other => 'その他';

  @override
  String get reportTrip => '乗車を報告';

  @override
  String get reportSent => '乗車レポートを送信しました。';

  @override
  String get reportSendFailed => 'レポートを送信できません。もう一度お試しください。';

  @override
  String get commonIssues => 'よくある問題';

  @override
  String get issueEncountered => '発生した問題';

  @override
  String get issueDescriptionHint => '発生した問題を詳しく入力してください...';

  @override
  String get reportContentRequired => 'レポート内容を入力してください。';

  @override
  String get safeRideDriver => 'SafeRideドライバー';

  @override
  String get sendReport => 'レポートを送信';

  @override
  String get edit => '編集';

  @override
  String get delete => '削除';

  @override
  String requiredLicense(String licenseClass) {
    return '免許 $licenseClass';
  }

  @override
  String get editVehicle => '車両を編集';

  @override
  String get addVehicle => '新しい車両を追加';

  @override
  String get vehicleType => '車両タイプ';

  @override
  String get motorbike => 'バイク';

  @override
  String get car => '自動車';

  @override
  String get vehicleName => '車両名';

  @override
  String get vehicleNameHint => '例：Honda Vision';

  @override
  String get engineCapacity => '排気量 (cc)';

  @override
  String get engineCapacityHint => '例：110、125、150';

  @override
  String get licensePlate => 'ナンバープレート';

  @override
  String get licensePlateHint => '例：29A1 - 123.45';

  @override
  String get color => '色';

  @override
  String get colorHint => '例：青';

  @override
  String get saveChanges => '変更を保存';

  @override
  String get saveVehicle => '車両を保存';

  @override
  String get vehicleNameValidation => '車両名は2～100文字で入力してください。';

  @override
  String get engineCapacityValidation => 'A1またはA免許要件の判定には有効なバイク排気量が必要です。';

  @override
  String get licensePlateLengthValidation => 'ナンバープレートは4～20文字で入力してください。';

  @override
  String get licensePlateFormatValidation =>
      'ナンバープレートには文字、数字、ピリオド、空白、ハイフンのみ使用できます。';

  @override
  String get colorValidation => '色は30文字以内で入力してください。';

  @override
  String get deleteVehicleQuestion => '車両を削除しますか？';

  @override
  String deleteVehicleDescription(String name) {
    return '\"$name\"を削除してもよろしいですか？この操作は取り消せません。';
  }

  @override
  String get deleteNow => '削除する';

  @override
  String get dismiss => 'キャンセル';

  @override
  String get requestFailed => 'リクエストを処理できません。';

  @override
  String get myVehicles => 'マイ車両';

  @override
  String get vehicleManagementDescription => '駐車および運転支援サービスで使用する車両を管理します。';

  @override
  String get noVehicles => '車両が登録されていません。';

  @override
  String get historyLoadFailed => '乗車履歴を読み込めません。';

  @override
  String get noTripHistory => '乗車履歴がありません。';

  @override
  String get tripNotRebookable => '再予約に必要な乗車情報が不足しています。';

  @override
  String get loadingTrip => '乗車情報を読み込み中...';

  @override
  String get chatOpenFailed => '現在チャットを開けません。';

  @override
  String get chat => 'チャット';

  @override
  String get viewReviews => 'レビューを見る';

  @override
  String get tripDetailsLoadFailed => '乗車情報を読み込めません。';

  @override
  String get tripDetails => '乗車詳細';

  @override
  String get rebookThisTrip => 'この乗車を再予約';

  @override
  String get tripCode => '乗車コード';

  @override
  String bookingOrder(int id) {
    return '予約 #$id';
  }

  @override
  String get routeMapUnavailable => 'この乗車の経路地図はありません。';

  @override
  String get route => '経路';

  @override
  String get tripRoute => '乗車経路';

  @override
  String get pickupPoint => '乗車地点';

  @override
  String get destinationPoint => '目的地';

  @override
  String get distance => '距離';

  @override
  String get duration => '所要時間';

  @override
  String minutesValue(num minutes) {
    return '$minutes分';
  }

  @override
  String get unknown => '不明';

  @override
  String get driverAndVehicle => 'ドライバーと車両';

  @override
  String get driverInfoUnavailable => 'この乗車のドライバー情報はありません。';

  @override
  String plateValue(String plate) {
    return 'ナンバー: $plate';
  }

  @override
  String vehicleColorValue(String color) {
    return '車両色: $color';
  }

  @override
  String tripCountValue(int count) {
    return '$count回の乗車';
  }

  @override
  String experienceYearsValue(int years) {
    return '経験$years年';
  }

  @override
  String get tripCost => '乗車料金';

  @override
  String get unknownPaymentMethod => '支払い方法不明';

  @override
  String get fare => '運賃';

  @override
  String get discount => '割引';

  @override
  String get total => '合計';

  @override
  String paidAtValue(String time) {
    return '$timeに支払い';
  }

  @override
  String get customerReview => '利用者の評価';

  @override
  String get reviewAndFeedback => '評価とフィードバック';

  @override
  String get customerHasNotReviewed => '利用者はまだこの乗車を評価していません。';

  @override
  String get noReviewData => 'この乗車の評価データはありません。';

  @override
  String get tripHistory => '乗車履歴';

  @override
  String get tripCompletedThanks => '乗車が完了しました。ありがとうございます！';

  @override
  String get tripInfoUnavailable => '乗車情報を確認できません。もう一度お試しください。';

  @override
  String get returnConfirmationFailed => '車両返却を確認できません。もう一度お試しください。';

  @override
  String get ratingSubmitFailed => '評価を送信できません。もう一度お試しください。';

  @override
  String get waitForPayment => '支払いが完了するまでお待ちください。';

  @override
  String get completeRequirementsBeforeLeaving =>
      'この画面を離れる前に車両返却を確認し、評価を送信してください。';

  @override
  String get tripComplete => '乗車完了';

  @override
  String get thanksForUsingService => 'ご利用ありがとうございました';

  @override
  String get distanceUpper => '距離';

  @override
  String get durationUpper => '所要時間';

  @override
  String get confirmVehicleReturned => 'ドライバーが車両を返却したことを確認';

  @override
  String get sendRatingAndWaitPayment => '評価を送信して支払いを待つ';

  @override
  String get confirmTripRateLater => '乗車を確認して後で評価';

  @override
  String get paymentDetails => '支払い詳細';

  @override
  String get baseFare => '基本料金';

  @override
  String get promotion => 'プロモーション';

  @override
  String get driverRatingQuestion => 'ドライバーはいかがでしたか？';

  @override
  String get driverCommentHint => 'ドライバーへのコメント（任意）';

  @override
  String get waitingForPayment => '支払い待ち';

  @override
  String get paymentWaitingInstructions =>
      'ドライバーの端末に表示されたQRコードを読み取るか、現金支払いの確認をお待ちください。';

  @override
  String get cancelReasonPlanChanged => '予定が変わった';

  @override
  String get cancelReasonWaitTooLong => '待ち時間が長すぎる';

  @override
  String get cancelReasonWrongLocation => '場所を間違えた';

  @override
  String get cancelReasonNoLongerNeeded => 'ドライバーが不要になった';

  @override
  String get cancelReasonOther => 'その他の理由';

  @override
  String get cancelTripQuestion => '乗車をキャンセルしますか？';

  @override
  String get cancelSearchConfirmation => 'ドライバー検索をキャンセルしてもよろしいですか？';

  @override
  String cancelBookingConfirmation(int id) {
    return '乗車 #$id をキャンセルしてもよろしいですか？';
  }

  @override
  String get cancelReason => 'キャンセル理由';

  @override
  String get confirmCancellation => 'キャンセルを確定';

  @override
  String get goBack => 'いいえ、戻る';

  @override
  String get cancelTripFailed => '乗車をキャンセルできません。もう一度お試しください。';

  @override
  String get tripCannotBeCancelled => 'この乗車は現在の状態ではキャンセルできません。';

  @override
  String get tripWaitExpired => '待機時間が終了したため、乗車は終了しました。';

  @override
  String get tripCancelledSuccessfully => '乗車をキャンセルしました。';

  @override
  String get scheduledTripCancelledSuccessfully => '予約乗車をキャンセルしました。';

  @override
  String get rebook => '再予約';

  @override
  String get noPromotions => '現在プロモーションはありません';

  @override
  String remainingUses(int count) {
    return '残り回数: $count';
  }

  @override
  String get promoValidatedOnBooking => '予約時にコードが確認されます';

  @override
  String get noAvailablePromoCodes => '現在利用可能なプロモーションコードはありません。';

  @override
  String get deselectPromo => 'プロモーションコードを解除';

  @override
  String minimumOrder(String amount) {
    return '最低注文額: $amount';
  }

  @override
  String remainingUseCount(int count) {
    return '残り$count回';
  }

  @override
  String get usageExhausted => '利用回数終了';

  @override
  String get inUse => '使用\n中';

  @override
  String get useNow => '今すぐ\n使用';

  @override
  String percentDiscount(num percent) {
    return '$percent%割引';
  }

  @override
  String maximumDiscount(String amount) {
    return '（最大$amount）';
  }

  @override
  String fixedDiscount(String amount) {
    return '$amount割引';
  }

  @override
  String expiresOn(String date) {
    return '有効期限: $date';
  }

  @override
  String minimumOrderShort(String amount) {
    return '最低注文額 $amount';
  }

  @override
  String get exitAppQuestion => 'アプリを終了しますか？';

  @override
  String get exitAppDescription => 'SafeRideを終了してもよろしいですか？';

  @override
  String get exit => '終了';

  @override
  String get activity => '利用中';

  @override
  String get safeRideAssistant => 'SafeRideアシスタント';

  @override
  String get tryAgain => '再試行';

  @override
  String get activeTripNotice => '進行中の乗車があります。「利用中」で確認してください。';

  @override
  String get trackingTrip => '乗車を追跡中';

  @override
  String get noActiveTripForSos => 'SOSを使用できる進行中の乗車がありません。';

  @override
  String get viewAll => 'すべて表示';

  @override
  String get locatingAddress => '住所を確認中...';

  @override
  String get searchPickup => '乗車地を検索';

  @override
  String get searchDestination => '目的地を検索';

  @override
  String get selectedPickup => '選択した乗車地';

  @override
  String get selectedDestination => '選択した目的地';

  @override
  String get searchOrTapMap => '検索するか地図をタップして選択してください。';

  @override
  String get confirmPickup => '乗車地を確定';

  @override
  String get confirmDestination => '目的地を確定';

  @override
  String get prepayment => '事前決済';

  @override
  String get payosPaymentAmount => 'PayOSでの支払額';

  @override
  String get checkPayment => '支払いを確認';

  @override
  String get payAfterTrip => '乗車後に支払う';

  @override
  String get prepaid => '事前決済済み';

  @override
  String get backToTrip => '乗車に戻る';

  @override
  String get payosQrCreateFailed => 'PayOS QRコードを作成できませんでした。';

  @override
  String get scanQrToPay => '銀行アプリでスキャンしてお支払いください';

  @override
  String get cameraOpenFailed => 'カメラを開けませんでした。権限を確認してください。';

  @override
  String get photoCaptureFailed => '撮影できませんでした。もう一度お試しください。';

  @override
  String get alignDocumentCorners => '書類の四隅を枠内に合わせてください';

  @override
  String get submittedInformation => '送信済み情報';

  @override
  String get documentNumber => '書類番号';

  @override
  String get licenseClass => '免許区分';

  @override
  String get issueDate => '発行日';

  @override
  String get expiryDate => '満了日';

  @override
  String get documents => '書類';

  @override
  String get frontSide => '表面';

  @override
  String get backSide => '裏面';

  @override
  String get submittedFile => '提出ファイル';

  @override
  String get documentApproved => '承認済み';

  @override
  String get documentPendingReview => '提出済み・審査待ち';

  @override
  String get documentRejected => '却下';

  @override
  String get documentNotSubmitted => '未提出';

  @override
  String get identityVerification => '本人確認';

  @override
  String get completeYourProfile => 'プロフィールを完成させる';

  @override
  String get identityVerificationIntro =>
      '乗車依頼の受付を開始し乗客の安全を守るため、本人確認と必要書類の提出を行ってください。';

  @override
  String get requiredDocuments => '必要書類';

  @override
  String get submitApplicationNow => '今すぐ申請';

  @override
  String get verificationTime => '確認には通常1～3営業日かかります。';

  @override
  String get previousApplicationRejected => '以前の申請は却下されました';

  @override
  String get profileStatusLoadFailed => '申請状況を読み込めませんでした。もう一度お試しください。';

  @override
  String get idCardOrPassport => '身分証明書 / パスポート';

  @override
  String get frontAndBack => '表面と裏面';

  @override
  String get drivingLicense => '運転免許証';

  @override
  String get licensePhotoAndInfo => '免許証の写真と情報';

  @override
  String get criminalRecord => '犯罪経歴証明書';

  @override
  String get originalIssuedWithinSixMonths => '6か月以内に発行された原本';

  @override
  String get resubmissionRequired => '再提出が必要';

  @override
  String get submitted => '提出済み';

  @override
  String get confirmHireDriver => 'ドライバーを確定';

  @override
  String get hourlyHire => '時間貸し';

  @override
  String get tripDetailsHeading => '乗車詳細';

  @override
  String get notCreated => '未作成';

  @override
  String get awaitingConfirmation => '確認待ち';

  @override
  String get estimatedDuration => '予想時間';

  @override
  String get updating => '更新中';

  @override
  String get estimatedTotalPayment => '予想支払総額';

  @override
  String get missingTripToConfirmDriver => 'ドライバーを確定する乗車がありません。';

  @override
  String get driverOfferNotFound => 'ドライバーの提案情報が見つかりません。';

  @override
  String get confirmDriverFailed => 'ドライバーを確定できませんでした。もう一度お試しください。';

  @override
  String get driverConfirmed => 'ドライバーを確定しました';

  @override
  String driverConfirmedMessage(String driverName, int bookingId) {
    return '$driverNameが乗車#$bookingIdを担当します。配車を待っています...';
  }

  @override
  String get agree => 'OK';

  @override
  String driverRatingSummary(String rating, int tripCount, int years) {
    return '評価$rating • $tripCount回 • 経験$years年';
  }

  @override
  String get confirmDriverNotice => '確定する前にドライバー情報をよく確認してください。';

  @override
  String get oldTripDataInvalid => '以前の乗車データが無効です。';

  @override
  String get calculatingFarePleaseWait => '料金を計算しています。お待ちください。';

  @override
  String get bookingSuccessful => '予約が完了しました。ドライバーが時間どおりにお迎えします。';

  @override
  String get rebookTrip => 'この乗車を再予約';

  @override
  String get confirmPreviousInformation => '以前の情報を確認';

  @override
  String get reviewRouteAndVehicle => '次の乗車のルートと車両を確認してください。';

  @override
  String get departureTime => '出発時刻';

  @override
  String get leaveNow => '今すぐ出発';

  @override
  String get scheduleAhead => '予約';

  @override
  String get promotionCode => 'プロモーションコード';

  @override
  String get oldPromoCannotBeReused =>
      '以前のプロモーションは再利用できません。新しいコードを選択または入力してください。';

  @override
  String get grandTotal => '合計';

  @override
  String discountApplied(String amount) {
    return '↓ $amount割引';
  }

  @override
  String get taxesIncluded => '税・手数料込み';

  @override
  String get confirmAndFindDriver => '確定してドライバーを検索';

  @override
  String get addNewPromoCode => '新しいプロモーションコードを追加';

  @override
  String get completePaymentBeforeExit => '終了する前に支払いを完了してください';

  @override
  String get completePayment => '支払いを完了してください。';

  @override
  String get tripPayment => '乗車料金の支払い';

  @override
  String get customerPaymentAmount => 'お客様のお支払額';

  @override
  String get paid => '支払済み';

  @override
  String get checkAgain => '再確認';

  @override
  String get cashConfirmed => '現金確認済み';

  @override
  String get customerPaid => 'お客様の支払い完了';

  @override
  String get backToHome => 'ホームに戻る';

  @override
  String get paymentQrCreateFailed => '支払い用QRコードを作成できませんでした。';

  @override
  String get reconfirmCash => '現金を再確認';

  @override
  String get recreateQr => 'QRを再作成';

  @override
  String get switchPaymentMethod => '別の支払方法';

  @override
  String get customerScanQr => 'お客様にこのコードを読み取ってもらってください';

  @override
  String get cashPaymentConfirmFailed => '現金支払いを確認できませんでした。';

  @override
  String get chooseCustomerPaymentMethod => 'お客様の支払方法を選択';

  @override
  String get qrPayment => 'QR決済';

  @override
  String get cashPayment => '現金';

  @override
  String get returnVehicleConfirmation => '車両返却の確認';

  @override
  String get returnEvidenceInstruction => '車両をお客様に返却した証拠写真を1～3枚撮影または選択してください。';

  @override
  String get tapToAddPhoto => 'タップして写真を追加';

  @override
  String get optionalNote => 'メモ（任意）';

  @override
  String get noteHint => '必要に応じてメモを入力...';

  @override
  String get submitting => '送信中...';

  @override
  String get returnConfirmedSuccess => '確認が完了しました';

  @override
  String get returnConfirmedMessage => '車両返却を記録しました。乗車を完了しています。';

  @override
  String get done => '完了';

  @override
  String get minimumEvidencePhoto => '証拠写真が1枚以上必要です。';

  @override
  String get maximumEvidencePhotos => '写真は3枚までアップロードできます。';

  @override
  String get evidenceUploadFailed => '証拠を送信できませんでした。再試行してください。';

  @override
  String get takePhoto => '撮影';

  @override
  String get chooseFromGallery => 'ギャラリーから選択';

  @override
  String get removePhoto => '写真を削除';

  @override
  String get removePhotoQuestion => 'この写真を削除しますか？';

  @override
  String photoNumber(int number) {
    return '写真 $number';
  }

  @override
  String photoCount(int count, int max) {
    return '$count / $max枚';
  }

  @override
  String remainingPhotos(int count) {
    return '残り$count枚';
  }

  @override
  String submitEvidenceWithCount(int count) {
    return '返却を確認（$count枚）';
  }

  @override
  String mediaAccessFailed(String source) {
    return '$sourceにアクセスできません。';
  }

  @override
  String get camera => 'カメラ';

  @override
  String get gallery => 'ギャラリー';

  @override
  String get myWallet => 'マイウォレット';

  @override
  String get availableBalance => '利用可能残高';

  @override
  String get withdraw => '出金';

  @override
  String get topUp => 'チャージ';

  @override
  String get income => '収入';

  @override
  String get day => '日';

  @override
  String get week => '週';

  @override
  String get month => '月';

  @override
  String totalIncomeForPeriod(String period) {
    return '総収入\n$period';
  }

  @override
  String get recentTransactions => '最近の取引';

  @override
  String get bankListLoadFailed => '銀行一覧を読み込めませんでした。';

  @override
  String get withdrawalRequestSent => '出金申請を送信しました。';

  @override
  String get withdrawalRequestFailed => '出金申請を送信できませんでした。';

  @override
  String get withdrawToBank => '銀行口座へ出金';

  @override
  String get bankInfoWillBeSaved => '次回の出金用にこの情報を保存します。';

  @override
  String get lastBankPreFilled => '直近の口座情報を入力済みです。';

  @override
  String get selectBankRequired => '銀行を選択してください';

  @override
  String get bank => '銀行';

  @override
  String get searchAndSelectBank => '銀行を検索して選択';

  @override
  String get accountNumber => '口座番号';

  @override
  String get invalidAccountNumber => '口座番号が無効です';

  @override
  String get accountHolderName => '口座名義';

  @override
  String get accountHolderRequired => '口座名義を入力してください';

  @override
  String get withdrawalAmount => '出金額';

  @override
  String minimumWithdrawal(String amount) {
    return '最低出金額は$amountです';
  }

  @override
  String get confirmWithdrawal => '出金を確認';

  @override
  String get selectBank => '銀行を選択';

  @override
  String get searchBankHint => '名称、コード、BINで検索';

  @override
  String get bankNotFound => '銀行が見つかりません。';

  @override
  String get noTransactions => '取引履歴はありません。';

  @override
  String get today => '今日';

  @override
  String get thisMonth => '今月';

  @override
  String get thisWeek => '今週';

  @override
  String get noPreviousPeriodData => '前期間の\nデータなし';

  @override
  String periodComparison(String value) {
    return '前期間比\n$value%';
  }

  @override
  String get completed => '完了';

  @override
  String get home => 'ホーム';

  @override
  String get account => 'アカウント';

  @override
  String get wallet => 'ウォレット';

  @override
  String get destinationQuestion => '今日はどちらへ行きますか？';

  @override
  String get bookNow => '今すぐ予約';

  @override
  String get bookNowDescription => '乗車に適したドライバーを探します';

  @override
  String get scheduleBooking => '日時を指定';

  @override
  String get history => '履歴';

  @override
  String get myVehiclesShort => 'マイ車両';

  @override
  String get promotions => 'プロモーション';

  @override
  String get sos => '緊急SOS';

  @override
  String get recentTrips => '最近の乗車';

  @override
  String get friendlyUser => 'お客様';

  @override
  String greeting(String name) {
    return 'こんにちは、$name';
  }

  @override
  String get sampleRecentPickup => 'グエン・ヴァン・リン123番地、7区';

  @override
  String get sampleRecentDestination => 'タンソンニャット空港';

  @override
  String get sampleRecentTime => '昨日 14:30';

  @override
  String get driverProfile => 'ドライバープロフィール';

  @override
  String tripCountPlus(String count) {
    return '$count+回の乗車';
  }

  @override
  String get kycStatus => 'KYCステータス';

  @override
  String get kycApprovedDescription => 'プロフィールはシステムにより承認済みです';

  @override
  String get cleanCriminalRecord => '問題のない透明な記録';

  @override
  String get confirmHire => '依頼を確定';

  @override
  String get rejectAndFindAnotherDriver => '拒否して別のドライバーを探す';

  @override
  String get rejectDriverQuestion => 'このドライバーを拒否しますか？';

  @override
  String get rejectDriverDescription => 'このドライバーをスキップし、別のドライバーを探し続けます。';

  @override
  String get findingAnotherDriver => '別のドライバーを探しています...';

  @override
  String get rejectDriverFailed => 'ドライバーを拒否できませんでした。';

  @override
  String get experienceUpper => '経験';

  @override
  String yearsValueCapitalized(int years) {
    return '$years年';
  }

  @override
  String get safeDriving => '安全運転';

  @override
  String get friendly => '親切';

  @override
  String get verified => '認証済み';

  @override
  String get idCardFront => '身分証明書の表面';

  @override
  String get idCardBack => '身分証明書の裏面';

  @override
  String get idCardCameraInstruction => '身分証明書全体を枠内に収め、明るく鮮明に撮影してください。';

  @override
  String get idCardScanned => '身分証明書の情報を読み取りました。';

  @override
  String get ocrScanFailed => 'この画像をOCRで読み取れませんでした。';

  @override
  String get stepOneOfThree => 'ステップ1/3';

  @override
  String get uploadIdCard => '身分証明書をアップロード';

  @override
  String get captureIdCard => '身分証明書を撮影';

  @override
  String get idCardUploadInstruction => '反射や欠けのない鮮明な身分証明書の表裏画像を提出してください。';

  @override
  String get fullName => '氏名';

  @override
  String get idCardNameHint => '身分証明書に記載された氏名を入力';

  @override
  String get idCardNumber => '身分証明書番号';

  @override
  String get idCardNumberHint => '身分証明書番号を入力';

  @override
  String get continueAction => '続ける';

  @override
  String get idCardFieldsRequired => '表裏を撮影し、氏名と身分証明書番号を確認してください。';

  @override
  String get idCardPhotoTip => 'ヒント：暗い平面に置き、十分な自然光で撮影すると最良の結果になります。';

  @override
  String get ocrScanningOnDevice => '端末上でOCRスキャン中...';

  @override
  String get idCardOcrFilled => 'OCRが身分証明書情報を自動入力しました';

  @override
  String get tapToCaptureOrUpload => 'タップして撮影またはアップロード';

  @override
  String get licenseFront => '免許証の表面';

  @override
  String get licenseBack => '免許証の裏面';

  @override
  String get licenseCameraInstruction => '免許証全体を枠内に収め、明るく鮮明に撮影してください。';

  @override
  String get ocrMlKitScanned => 'Google ML Kit OCRで読み取りました。';

  @override
  String get licenseOcrFailed => 'この免許証画像をOCRで読み取れませんでした。';

  @override
  String get licenseType => '免許の種類';

  @override
  String get licensePhotos => '運転免許証の写真';

  @override
  String get licenseNameHint => '免許証に記載された氏名を入力';

  @override
  String get licenseNumber => '免許証番号';

  @override
  String get licenseNumberHint => '免許証番号を入力';

  @override
  String get selectLicenseClass => '免許区分を選択';

  @override
  String get unlimited => '無期限';

  @override
  String get licenseNoExpiry => 'この免許証に有効期限はありません';

  @override
  String get idAndLicenseNameMismatch => '身分証明書と運転免許証の氏名が一致しません。';

  @override
  String get stepTwoOfThree => 'ステップ2/3';

  @override
  String get uploadLicense => '運転免許証をアップロード';

  @override
  String get licenseOcrFilled => 'OCRが免許証情報を自動入力しました';

  @override
  String get criminalRecordInstruction =>
      '乗客の安全のため、6か月以内に発行された犯罪経歴証明書を提出してください。';

  @override
  String get reviewWithinHours => '申請は24～48営業時間以内に審査されます。';

  @override
  String get submittingApplication => '申請を送信中...';

  @override
  String get completeAndSubmit => '完了して送信';

  @override
  String get stepThreeOfThree => 'ステップ3/3';

  @override
  String get uploadCriminalRecord => '犯罪経歴証明書をアップロード';

  @override
  String get uploadRequirements => 'アップロード要件';

  @override
  String get clearNoGlare => '反射のない鮮明な写真。';

  @override
  String get allFourCorners => '書類の四隅がすべて見えること。';

  @override
  String get supportedDocumentFormats => '対応形式：JPG、PNG、PDF（最大10MB）。';

  @override
  String get tapToUploadDocument => 'タップしてアップロードするか、ファイルをここにドロップ';

  @override
  String get photoOrPdfSupported => '写真またはスキャンPDFに対応';

  @override
  String get chooseDocument => '書類を選択';

  @override
  String get documentSelected => '書類を選択済み';

  @override
  String get change => '変更';

  @override
  String get criminalRecordOcrRead => 'OCRが犯罪経歴証明書の内容を読み取りました';

  @override
  String get criminalRecordScanned => '犯罪経歴証明書をOCRで読み取りました。';

  @override
  String get documentOcrFailed => 'この書類をOCRで読み取れませんでした。';

  @override
  String get applicationSubmitted => '申請を送信しました！';

  @override
  String get applicationProcessing => '申請を処理中です。結果はまもなくお知らせします。';

  @override
  String get applicationSubmitFailed => '申請を送信できませんでした。もう一度お試しください。';

  @override
  String tripEndedWithId(int id) {
    return '乗車#$idは終了しました。';
  }

  @override
  String get searchingDriver => 'ドライバーを探しています...';

  @override
  String get cancelling => 'キャンセル中...';

  @override
  String get cancelBooking => '乗車をキャンセル';

  @override
  String remainingCountdown(String message, String countdown) {
    return '$message - 残り$countdown';
  }

  @override
  String get estimatedWaitTime => '予想待ち時間：約2分';

  @override
  String tripCodeWithStatus(int id, String status) {
    return '乗車#$id • $status';
  }

  @override
  String secondsRemaining(int seconds) {
    return '残り$seconds秒';
  }

  @override
  String get suitableDriverReady => '適切なドライバーが見つかりました';

  @override
  String reviewProfileAndConfirm(String countdown) {
    return 'プロフィールを確認して確定してください$countdown。';
  }

  @override
  String get viewProfile => 'プロフィールを見る';

  @override
  String get waitingDriverAccept => 'ドライバーの承認待ち';

  @override
  String get appliedCode => '適用済みコード';

  @override
  String promotionWithCode(String code) {
    return 'プロモーション（$code）：';
  }

  @override
  String currentLocationFailed(String error) {
    return '現在地を取得できませんでした：$error';
  }

  @override
  String get callUnavailableSessionExpired => 'セッションが期限切れのため通話できません。';

  @override
  String get customer => 'お客様';

  @override
  String get incomingCall => '着信';

  @override
  String get customerCalling => 'お客様から着信です。';

  @override
  String get decline => '拒否';

  @override
  String get answer => '応答';

  @override
  String onlineLocationFailed(String error) {
    return '位置情報を取得できないか、オンラインにできません：$error';
  }

  @override
  String get chatUnavailable => '現在チャットを開けません。';

  @override
  String get gpsSimulationEnabled => 'バックエンドGPSシミュレーションを有効にしました';

  @override
  String get gpsSimulationDisabled => 'GPSシミュレーションを無効にし、実際のGPSを使用します';

  @override
  String get activeTrip => '進行中の乗車';

  @override
  String get message => 'メッセージ';

  @override
  String get callCustomer => 'お客様に電話';

  @override
  String get processing => '処理中...';

  @override
  String get startPickup => 'お迎えに向かう';

  @override
  String get driverArrived => '乗車地に到着';

  @override
  String get startTrip => '乗車を開始';

  @override
  String get endTrip => '乗車を終了';

  @override
  String get waitingCustomerReturnConfirmation =>
      'お客様の車両返却確認を待っています。\n応答がない場合は代理で確認できます。';

  @override
  String get confirmReturnWithEvidence => '証拠写真で代理確認';

  @override
  String get returnConfirmedCompleting => '車両返却を確認しました。乗車を完了しています...';

  @override
  String get returnConfirmedPaymentRequired =>
      '車両返却を確認しました。乗車完了のため支払いを確認してください。';

  @override
  String get confirmPayment => '支払いを確認';

  @override
  String get statusAccepted => '乗車を承認';

  @override
  String get statusArrived => '乗車地に到着';

  @override
  String get waitingReturnConfirmation => '返却確認待ち';

  @override
  String get returnConfirmedStatus => '返却確認済み';

  @override
  String get tripStatusUpdateFailed => '乗車ステータスを更新できませんでした。';

  @override
  String get todayIncomeUpper => '本日の収入';

  @override
  String tripCountShort(int count) {
    return '$count回';
  }

  @override
  String get waitingConfirmation => '確認待ち';

  @override
  String get waitingCustomerDriverConfirmation =>
      'お客様によるドライバー確認を待っています。アプリを閉じないでください。';

  @override
  String get newTripAvailable => '新しい乗車依頼があります！';

  @override
  String get expectedIncomeUpper => '予想収入';

  @override
  String get pickupCustomerUpper => 'お迎え';

  @override
  String get pickupPointA => '乗車地 (A)';

  @override
  String get destinationPointB => '目的地 (B)';

  @override
  String get accept => '承認';

  @override
  String get selectPickupDate => '乗車日を選択';

  @override
  String get selectPickupTimeHelp => '乗車時刻を選択';

  @override
  String get invalidSchedule => '予約時刻は現在から30分以上後に設定してください。';

  @override
  String get selectPickupRequired => '乗車地を選択してください。';

  @override
  String get selectServiceAndVehicle => 'サービスと車両を選択してください。';

  @override
  String get selectDestinationRequired => '目的地を選択してください。';

  @override
  String get selectPickupTimeRequired => '乗車時刻を選択してください。';

  @override
  String get fareEstimateUnavailable => '予想料金を取得できません。ルートを確認して再試行してください。';

  @override
  String get bookingFailed => '乗車を予約できませんでした。もう一度お試しください。';

  @override
  String get bookingSuccess => '予約が完了しました';

  @override
  String get addVehicleFailed => '車両を追加できませんでした。もう一度お試しください。';

  @override
  String get vehicleAdded => '新しい車両を追加しました。';

  @override
  String get selectYourVehicle => '車両を選択';

  @override
  String get loadingServices => 'サービス情報を読み込み中...';

  @override
  String get specialRequest => '特別なリクエスト（任意）';

  @override
  String get fareCalculationNote => '実際の距離と時間に基づきバックエンドで料金を計算します。';

  @override
  String get confirmScheduled => '予約乗車を確定';

  @override
  String get confirmHourlyHire => '時間貸しを確定';

  @override
  String get confirmNow => '今すぐ予約を確定';

  @override
  String get selectPickup => '乗車地を選択';

  @override
  String get selectDestination => '目的地を選択';

  @override
  String get calculatingFare => '予想料金を計算中...';

  @override
  String hoursValue(int hours) {
    return '$hours時間';
  }

  @override
  String surgePricing(num multiplier) {
    return '需要増加による割増料金 (x$multiplier)';
  }

  @override
  String estimatedRentalHours(int hours) {
    return '予想レンタル時間：$hours時間';
  }

  @override
  String get addPromoCode => 'プロモーションコードを追加';

  @override
  String get tripService => '乗車ごと';

  @override
  String get hourlyService => '時間制';

  @override
  String get addNewVehicle => '新しい車両を追加';

  @override
  String get saveVehicleAndContinue => '車両をアカウントに保存して予約を続けます。';

  @override
  String get add => '追加';

  @override
  String plateNumberLabel(String value) {
    return 'ナンバー：$value';
  }

  @override
  String vehicleColorLabel(String value) {
    return '色：$value';
  }

  @override
  String get noBookableVehicles => '予約可能な車両がありません。予約前に追加してください。';

  @override
  String get mapsConfigMissing => '地図が設定されていません。後でもう一度お試しください。';

  @override
  String get serverDisconnectedRetrying => 'サーバーとの接続が切れました。再接続しています...';

  @override
  String get tripCancelled => '乗車はキャンセルされました。';

  @override
  String get driverLocationTrackingRetrying => 'ドライバー位置追跡に接続できません。再試行中...';

  @override
  String get safetyCheck => '安全確認';

  @override
  String get safetyConfirmed => 'SafeRideが安全を確認しました。';

  @override
  String get iAmSafe => '安全です';

  @override
  String get callDriver => 'ドライバーに電話';

  @override
  String get activateSosQuestion => '緊急SOSを有効にしますか？';

  @override
  String get activateSosDescription => 'この乗車の緊急信号を送信しますか？';

  @override
  String get activateSos => '緊急SOSを有効化';

  @override
  String get sosActivationFailed => 'SOSを有効にできませんでした。もう一度お試しください。';

  @override
  String get sosLocationFailed => 'SOS用の現在地を取得できませんでした。';

  @override
  String get emergencyHelpMessage => '緊急支援が必要です';

  @override
  String get sosActivatedForTrip => 'この乗車でSOSが有効になりました。';

  @override
  String get sosActivatedHelpComing => 'SOSを有効にしました。できるだけ早く支援します。';

  @override
  String get driverAtPickup => 'ドライバーが乗車地に到着しました';

  @override
  String get waitingDriverPayment => 'ドライバーへの支払い待ち';

  @override
  String driverArrivingMinutes(int minutes) {
    return 'ドライバーが到着中 • $minutes分';
  }

  @override
  String movingMinutes(int minutes) {
    return '移動中 • $minutes分';
  }

  @override
  String get onCorrectRoute => '正しいルートを走行中です';

  @override
  String get safeRideDriverName => 'SafeRideドライバー';

  @override
  String get updatingVehicle => '車両情報を更新中';

  @override
  String get prepayWithPayos => 'PayOSで事前決済';

  @override
  String get call => '電話';

  @override
  String get share => '共有';

  @override
  String get payDriverToComplete => '乗車を完了するにはドライバーへお支払いください。';

  @override
  String get endingTrip => '乗車を終了中...';

  @override
  String get tripNotReadyForPayment => '乗車はまだ支払い準備ができていません。';

  @override
  String get tripNotReadyForChat => '乗車はまだチャット準備ができていません。';

  @override
  String get chatAccountUnknown => 'チャット用アカウントを特定できません。';

  @override
  String get tripNotReadyForCall => '乗車準備が整うまで通話できません。';

  @override
  String driverCalling(String driverName) {
    return '$driverNameから着信です。';
  }

  @override
  String get tripCannotEndNow => '現在この乗車を終了できません。';

  @override
  String get tripEndFailed => '乗車を終了できませんでした。もう一度お試しください。';

  @override
  String get sosActivated => '緊急SOS有効';

  @override
  String get sendingSos => '緊急SOSを送信中...';

  @override
  String get shareRoute => 'ルートを共有';

  @override
  String get shareRouteDescription =>
      '家族や友人が乗車をリアルタイムで追跡できるよう、以下のリンクを送信してください。';

  @override
  String get linkCopied => 'リンクをコピーしました';

  @override
  String get close => '閉じる';

  @override
  String get enableLocationForPickup =>
      'SafeRideがGPSを乗車地として使用できるよう位置情報を有効にしてください。';

  @override
  String get microphonePermissionRequired => 'SafeRideのマイク使用を許可してください。';

  @override
  String get voiceMessage => '音声メッセージ';

  @override
  String get currentGpsUnavailable =>
      '現在のGPS位置を取得できませんでした。位置情報を有効にして再試行してください。';

  @override
  String get audioUploadFailed => '録音ファイルをアップロードできませんでした。もう一度お試しください。';

  @override
  String get aiAssistantUnavailable => 'AIアシスタントを利用できません。後でもう一度お試しください。';

  @override
  String get aiAssistantConnectionFailed => 'AIアシスタントに接続できませんでした。もう一度お試しください。';

  @override
  String get aiBookingFailed => '乗車を予約できませんでした。';

  @override
  String get conversationOpenFailed => '会話を開けませんでした。';

  @override
  String get recording => '録音中...';

  @override
  String get sendOrCancelRecording => '録音を送信またはキャンセル';

  @override
  String get aiMessageHint => 'SafeRideアシスタントにメッセージ...';

  @override
  String get cancelVoice => '音声をキャンセル';

  @override
  String get sendVoice => '音声を送信';

  @override
  String get voiceInput => '音声入力';

  @override
  String vehicleSelectedByQuery(String query) {
    return '“$query”に一致する車両を選択しました。';
  }

  @override
  String vehicleQueryNotFound(String query) {
    return '“$query”に完全一致する車両が見つかりません。選択し直してください。';
  }

  @override
  String promoApplied(String code) {
    return 'コード$codeを適用しました。';
  }

  @override
  String promoUnavailable(String code) {
    return 'コード$codeは利用できません。';
  }

  @override
  String get conversationHistoryLoadFailed => '会話履歴を読み込めませんでした。';

  @override
  String get deleteConversationQuestion => '会話を削除しますか？';

  @override
  String deleteConversationDescription(String title) {
    return '“$title”と関連する音声ファイルは完全に削除されます。';
  }

  @override
  String get conversationDeleteFailed => '会話を削除できませんでした。もう一度お試しください。';

  @override
  String get conversationHistory => '会話履歴';

  @override
  String get noConversations => '会話はまだありません。';

  @override
  String get deleteConversation => '会話を削除';

  @override
  String get safeRideAssistantTitle => 'SafeRideアシスタント';

  @override
  String get aiDisclaimer => 'AIは間違えることがあります • 予約前に確認してください';

  @override
  String get newChat => '新しいチャット';

  @override
  String get back => '戻る';

  @override
  String get chooseVehicleQuestion => 'どの車両を使用しますか？';

  @override
  String get chooseDiscountCode => '割引コードを選択';

  @override
  String get confirmTrip => '乗車を確認';

  @override
  String get yourVehicles => 'あなたの車両';

  @override
  String get newVehicle => '新しい車両';

  @override
  String get noVehicleForAiBooking => '車両がありません。予約を続けるには追加してください。';

  @override
  String get continueChooseDiscount => '割引コードの選択へ';

  @override
  String get noDiscountAvailable => '現在利用可能な割引コードはありません。';

  @override
  String get noDiscount => '割引コードを使用しない';

  @override
  String get continueWithoutDiscount => 'コードなしで続ける';

  @override
  String usePromoCode(String code) {
    return 'コード$codeを使用';
  }

  @override
  String get notUsed => '使用しない';

  @override
  String get confirmAndFindDriverAi => '確定してドライバーを検索';

  @override
  String get aiWelcome =>
      'こんにちは！SafeRideの利用や乗車準備をお手伝いします。\n\n例：「FPT大学からタンソンニャット空港まで予約して」。';

  @override
  String get slogan => '安全で信頼できる乗車';

  @override
  String get phoneNumber => '電話番号';

  @override
  String get phoneHint => '電話番号を入力';

  @override
  String get continueOrRegister => '続ける / 登録';

  @override
  String get phoneRequired => '電話番号を入力してください';

  @override
  String get invalidPhone => '電話番号が無効です';

  @override
  String get sendOtpFailed => 'OTPを送信できませんでした。電話番号を確認して再試行してください。';

  @override
  String get or => 'または';

  @override
  String get googleLoginFailed => 'Googleログインに失敗しました';

  @override
  String get continueAgreement => '続行すると、当社の ';

  @override
  String get and => ' および ';

  @override
  String get agreementSuffix => ' に同意したものとみなされます。';

  @override
  String get otpTitle => 'OTP認証';

  @override
  String get resendAfter => '再送信まで ';

  @override
  String get resendOtp => 'OTPを再送信';

  @override
  String get otpResent => 'OTPを再送信しました。';

  @override
  String get resendOtpFailed => 'OTPを再送信できませんでした。';

  @override
  String get otpRequired => '6桁のOTPをすべて入力してください';

  @override
  String get invalidOtp => 'OTPが正しくないか期限切れです';

  @override
  String get otpLockedPrefix => '誤入力が多すぎます。次の時間後に再試行：';

  @override
  String get otpAttemptsExceeded => 'OTPの誤入力が多すぎます。新しいコードをリクエストしてください。';

  @override
  String otpDescription(String phoneNumber) {
    return '$phoneNumberに送信された\n6桁のコードを入力してください。';
  }

  @override
  String get welcome => 'ようこそ！';

  @override
  String get selectRoleQuestion => 'どの役割で始めますか？';

  @override
  String get customerRoleTitle => 'お客様として利用';

  @override
  String get customerRoleDescription => '安全な乗車をすばやく予約し、リアルタイムで追跡できます。';

  @override
  String get driverRoleTitle => 'ドライバーとして利用';

  @override
  String get driverRoleDescription => '柔軟に働き、収入を増やし、乗車を簡単に管理できます。';

  @override
  String get rememberRole => '選択を記憶';

  @override
  String get completeProfile => 'プロフィールを完成';

  @override
  String get changeAvatar => 'プロフィール写真を変更';

  @override
  String get verifiedPhone => '確認済み電話番号';

  @override
  String get updateInformationHint => '続行するには個人情報を更新してください。';

  @override
  String get email => 'メール';

  @override
  String get saving => '保存中...';

  @override
  String get saveAndContinue => '保存して続ける';

  @override
  String get uploadAvatarFailed => 'プロフィール写真をアップロードできませんでした。';

  @override
  String get updateProfileFailed => '情報を更新できませんでした。';

  @override
  String get invalidFullName => '有効な氏名を入力してください。';

  @override
  String get invalidEmail => 'メールアドレスが無効です。';

  @override
  String get emailAlreadyUsed => 'このメールは別のアカウントで使用されています。';

  @override
  String get phoneNumberAlreadyUsed => 'この電話番号は別のアカウントで使用されています。';

  @override
  String get phoneNumberChangeRequiresVerification => 'この画面では連携済み電話番号を変更できません。';

  @override
  String get phoneVerificationRequired => '電話番号を追加する前にOTPを確認してください。';

  @override
  String get appVersion => 'アプリバージョン：2.4.1';

  @override
  String get linkGoogleFailed => 'Googleを連携できませんでした。';

  @override
  String get unlinkGoogleQuestion => 'Google連携を解除しますか？';

  @override
  String get unlinkGoogleDescription => '確認済み電話番号で引き続きログインできます。';

  @override
  String get unlinkAccount => '連携解除';

  @override
  String get unlinkGoogleFailed => 'Google連携を解除できませんでした。';

  @override
  String get logoutFailed => 'ログアウトできませんでした。もう一度お試しください。';

  @override
  String get historyFilterAll => 'すべて';

  @override
  String get historyFilterCancelled => 'キャンセル済み';

  @override
  String get historyFilterBooked => '予約済み';

  @override
  String get cancelledByCustomer => 'お客様がキャンセルしました';

  @override
  String get reported => '報告済み';

  @override
  String get report => '報告';

  @override
  String get aiConversationFallback => '会話';

  @override
  String get chatConnectionFailed => 'チャットに接続できません。';

  @override
  String get chatMessageSendFailed => 'メッセージを送信できません。';

  @override
  String get chatImageSendFailed => '画像を送信できません。';

  @override
  String get routeUpdated => 'SafeRideがルートを更新しました。';

  @override
  String get newTripMessage => '新しい配車があります。';

  @override
  String get noInternetConnection => 'インターネット接続がありません';

  @override
  String get connectionLost => '接続が切れました';

  @override
  String get internetRestored => 'インターネット接続が復旧しました';

  @override
  String get backOnline => 'オンラインに復帰';

  @override
  String get calculating => '計算中';

  @override
  String get viewTripAfterAccept => '承諾後に配車詳細を表示';

  @override
  String get customerCancelledDriverRequest => 'お客様がドライバー依頼をキャンセルしました。';

  @override
  String get onlineFailed => 'オンラインにできません。もう一度お試しください。';

  @override
  String get acceptTripFailed => '配車を承諾できません。もう一度お試しください。';

  @override
  String get declineTripFailed => '配車を辞退できません。もう一度お試しください。';

  @override
  String get tripRequestsLoadFailed => '配車リクエストを読み込めません。もう一度お試しください。';

  @override
  String get noDestination => '目的地は未設定です';

  @override
  String get expiresSoon => 'まもなく期限切れ';

  @override
  String get evidencePhotoCountError => '証拠写真を1～3枚添付してください。';

  @override
  String get activeTripLoadFailed => '現在の配車を読み込めません。もう一度お試しください。';

  @override
  String ratingStars(int count) {
    return '星$count個';
  }

  @override
  String get demoGpsMode => 'GPSシミュレーションモード';

  @override
  String get serviceDisabled => '端末の位置情報サービスを有効にしてください。';

  @override
  String get permissionRequired => '乗車地点を特定するため、SafeRideに位置情報の許可が必要です。';

  @override
  String get locationNotFound => '一致する場所が見つかりません。';

  @override
  String get destinationRequired => '目的地を入力してください。';

  @override
  String get statusLabel => 'ステータス';

  @override
  String get selectPromotion => 'プロモーションを選択';

  @override
  String get enterPromoCode => 'プロモーションコードを入力';

  @override
  String get apply => '適用';

  @override
  String get expired => '期限切れ';

  @override
  String get statusOnline => 'オンライン';

  @override
  String get statusOffline => 'オフライン';

  @override
  String get statusBusy => '配車中';

  @override
  String get offerSent => 'ドライバーに送信済み';

  @override
  String get offerRejected => '拒否済み';

  @override
  String get offerCustomerConfirmed => 'お客様が確認済み';

  @override
  String get driverEndTripRequestTitle => '乗車終了リクエスト';

  @override
  String get driverEndTripRequestMessage =>
      'ドライバーが今すぐ乗車を終了したいと申し出ています。同意すると、実際の走行距離に基づいて最低2,000 VNDの料金が計算されます。';

  @override
  String get continueTrip => '乗車を続ける';

  @override
  String get endTripRequestSent => '終了リクエストを送信しました。お客様の確認を待っています。';

  @override
  String get endTripRequestAccepted => 'お客様が乗車終了に同意しました。';

  @override
  String get endTripRequestRejected => 'お客様が拒否しました。乗車を続けます。';

  @override
  String get endTripResponseFailed => '終了リクエストに応答できませんでした。もう一度お試しください。';

  @override
  String get preTripSafetyTitle => '運行前車両安全確認';

  @override
  String get preTripSafetyDescription => '開始前に全項目を確認してください。不合格の履歴も監査用に保存されます。';

  @override
  String get brakeResponse => 'ブレーキの反応';

  @override
  String get frontRearLights => '前後ライト';

  @override
  String get turnSignals => '方向指示器';

  @override
  String get visibleTires => 'タイヤの外観';

  @override
  String get dashboardWarning => '警告灯なし';

  @override
  String get windshieldVisibility => '窓とミラーの視界';

  @override
  String get noMajorVisibleIssue => '重大な外観異常なし';

  @override
  String get confirmSafetyCheck => '安全確認を送信';

  @override
  String get allChecksRequired => '運行開始前に全項目が合格する必要があります。';

  @override
  String get safetyTermination => '安全上の理由で終了';

  @override
  String get safetyTerminationDescription =>
      '運行はキャンセルのままです。プロモーションは使用されず、開始後は部分運賃が発生する場合があります。';

  @override
  String get safetyTerminationReasonHint => '安全上のリスクを説明';

  @override
  String get captureSafetyEvidence => '証拠写真を撮影（任意）';

  @override
  String get retakePhoto => '撮り直す';

  @override
  String get reportAccident => '事故を報告';

  @override
  String get accidentDescriptionHint => '状況と初期損害を説明';

  @override
  String get createAccidentReport => '報告を作成';

  @override
  String get accidentReported => '事故報告を作成しました。';

  @override
  String get safetyTerminationFailed => '安全上の終了処理に失敗しました。';

  @override
  String get preTripCheckFailed => '安全確認を送信できませんでした。';

  @override
  String get riskProtectionCaseTitle => '事故保護ケース';

  @override
  String get riskProtectionClaim => '保護請求';

  @override
  String get riskProtectionEvidence => '証拠';

  @override
  String get riskProtectionAssessment => '責任評価';

  @override
  String get uploadAccidentEvidence => '証拠写真を追加';

  @override
  String get sendEvidencePhoto => '写真を送信';

  @override
  String get evidencePreviewFailed => '選択した画像を読み込めません。もう一度選択してください。';

  @override
  String get disputeLiability => '責任評価の再審査を依頼';

  @override
  String get disputeReasonHint => '再審査が必要な理由を入力してください';

  @override
  String get liabilityDisputed => '再審査依頼を送信しました。';

  @override
  String get accidentEvidenceUploaded => '証拠写真を送信しました。';

  @override
  String get noAccidentEvidence => '証拠はまだありません。';

  @override
  String get noProtectionClaim => '保護請求はまだ作成されていません。';

  @override
  String get driverLiabilities => '自分の責任';

  @override
  String get noDriverLiabilities => '確定した運転者責任はありません。';

  @override
  String get confirmedAmount => '確定額';

  @override
  String get paidAmount => '支払済み';

  @override
  String get outstandingAmount => '未払い額';

  @override
  String get attributableDamage => '運転者責任の対象損害';

  @override
  String get recoveryHistory => '回収履歴';

  @override
  String get claimStatus => '請求状況';

  @override
  String get insuranceCoverage => '保険補償';

  @override
  String get riskFundCoverage => 'リスク基金補償';

  @override
  String get participantLiabilities => '当事者の責任';

  @override
  String get accidentStatus => '事故状況';

  @override
  String get accidentCategory => '事故区分';

  @override
  String get accidentOccurredAt => '発生日時';

  @override
  String get safetyReportTitle => '安全上の問題を報告';

  @override
  String get unsafeCustomer => '危険な顧客';

  @override
  String get vehicleIssue => '車両の問題';

  @override
  String get safetyReasonCode => '理由';

  @override
  String get safetyReportDescription => '状況を説明してください';

  @override
  String get requestSosEscalation => 'SOSエスカレーションを依頼';

  @override
  String get requestSosEscalationHint => '現在地を送信し、永続的なSOS警告を作成します';

  @override
  String get safetyReportSubmitted => '安全上の問題を報告しました。';

  @override
  String get safetyReportFailed => '安全上の問題を報告できませんでした。もう一度お試しください。';

  @override
  String get vehicleFaultType => '車両の不具合種別';

  @override
  String get otherVehicleFault => 'その他の車両不具合';

  @override
  String get optionalEvidence => '証拠（任意）';

  @override
  String get vehicleInsurance => '保険';

  @override
  String get addInsurance => '保険を追加';

  @override
  String get insuranceLoadFailed => '保険情報を読み込めませんでした。再試行してください。';

  @override
  String get insuranceUpdateFailed => '保険を更新できませんでした。';

  @override
  String get deleteInsuranceQuestion => '保険契約を削除しますか？';

  @override
  String get policyNumber => '証券番号';

  @override
  String get optionalInsuranceEmpty => '保険は任意です。この車両には契約がありません。';

  @override
  String get addInsurancePolicy => '保険契約を追加';

  @override
  String get editInsurancePolicy => '保険契約を編集';

  @override
  String get insuranceType => '保険種別';

  @override
  String get mandatoryTplInsurance => '自賠責保険';

  @override
  String get physicalDamageInsurance => '車両損害';

  @override
  String get insuranceProvider => '保険会社';

  @override
  String get effectiveDate => '開始日';

  @override
  String get insuranceCoverageLimit => '補償限度額';

  @override
  String get insuranceDeductible => '免責額';

  @override
  String get optionalDocumentUrl => '書類URL（任意）';

  @override
  String get optionalInsuranceHint =>
      '保険は任意です。作成または編集すると、スタッフ確認のためPENDINGに戻ります。';
}
