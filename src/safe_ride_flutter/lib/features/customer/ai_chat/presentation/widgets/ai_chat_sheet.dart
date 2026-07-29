import 'dart:io';

import 'package:audioplayers/audioplayers.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';
import 'package:record/record.dart';

import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../shared/profile/data/models/vehicle_model.dart';
import '../../../../shared/profile/presentation/providers/vehicle_provider.dart';
import '../../../../shared/profile/presentation/widgets/vehicle_form_sheet.dart';
import '../../../booking/data/models/booking_catalog.dart';
import '../../../booking/data/models/booking_fare_estimate.dart';
import '../../../booking/data/models/booking_location.dart';
import '../../../booking/data/models/create_booking_request.dart';
import '../../../booking/data/models/promo_model.dart';
import '../../../booking/presentation/pages/searching_driver_page.dart';
import '../../../booking/presentation/providers/booking_provider.dart';
import '../../data/models/ai_chat_models.dart';
import '../../data/services/ai_chat_service.dart';

enum _BookingStep { vehicle, promotion, confirmation }

class AiChatSheet extends StatefulWidget {
  const AiChatSheet({super.key});

  static Future<void> show(BuildContext context) => showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: Colors.transparent,
    builder: (_) => const AiChatSheet(),
  );

  @override
  State<AiChatSheet> createState() => _AiChatSheetState();
}

class _AiChatSheetState extends State<AiChatSheet> {
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  final _service = AiChatService();
  final _recorder = AudioRecorder();
  final List<AiChatMessage> _messages = [];
  String? _conversationId;
  AiBookingDraft? _draft;
  bool _sending = false;
  bool _voiceMode = false;
  bool _recording = false;
  bool _preparingBooking = false;
  bool _creatingBooking = false;
  String? _recordingPath;
  String? _error;
  BookingVehicleOption? _selectedVehicle;
  BookingServiceOption? _selectedService;
  PromoModel? _selectedPromo;
  BookingFareEstimate? _fareEstimate;
  String? _bookingNotice;
  _BookingStep _bookingStep = _BookingStep.vehicle;
  BookingLocation? _currentLocation;

  @override
  void initState() {
    super.initState();
    _restoreLatestConversation();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadCurrentLocation());
  }

  @override
  void dispose() {
    _recorder.dispose();
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _startVoice() async {
    if (_sending || _voiceMode) return;
    final location = _currentLocation ?? await _loadCurrentLocation();
    if (location == null) {
      if (mounted) {
        setState(() {
          _error = context.read<BookingProvider>().locationErrorMessage ??
              'Hãy bật vị trí để SafeRide tự dùng GPS làm điểm đón.';
        });
      }
      return;
    }
    if (!await _recorder.hasPermission()) {
      if (mounted) setState(() => _error = 'Bạn cần cho phép SafeRide dùng micro.');
      return;
    }
    final path = '${Directory.systemTemp.path}${Platform.pathSeparator}'
        'saferide-voice-${DateTime.now().microsecondsSinceEpoch}.m4a';
    await _recorder.start(
      const RecordConfig(encoder: AudioEncoder.aacLc, numChannels: 1),
      path: path,
    );
    if (!mounted) return;
    setState(() {
      _error = null;
      _voiceMode = true;
      _recording = true;
      _recordingPath = path;
    });
  }

  Future<void> _cancelVoice() async {
    await _recorder.cancel();
    if (!mounted) return;
    setState(() {
      _voiceMode = false;
      _recording = false;
      _recordingPath = null;
      _error = null;
    });
  }

  Future<void> _sendVoice() async {
    final path = await _recorder.stop() ?? _recordingPath;
    if (path == null) return;
    if (!mounted) return;
    final pendingId = 'audio-${DateTime.now().microsecondsSinceEpoch}';
    setState(() {
      _voiceMode = false;
      _recording = false;
      _recordingPath = null;
      _sending = true;
      _messages.add(
        AiChatMessage(
          id: pendingId,
          role: 'user',
          content: 'Tin nhắn thoại',
          createdAt: DateTime.now(),
          localAudioPath: path,
          isAudio: true,
        ),
      );
    });
    _scrollToBottom();
    try {
      final location = _currentLocation ?? await _loadCurrentLocation();
      if (location == null) {
        if (mounted) {
          setState(() {
            _sending = false;
            _error = context.read<BookingProvider>().locationErrorMessage ??
                'Không lấy được GPS hiện tại. Vui lòng bật vị trí rồi thử lại.';
          });
        }
        return;
      }
      final reply = await _service.sendAudio(
        filePath: path,
        conversationId: _conversationId,
        currentLocation: location,
      );
      if (!mounted) return;
      setState(() {
        _conversationId = reply.conversationId;
        final pendingIndex = _messages.indexWhere((item) => item.id == pendingId);
        final persistedAudio = AiChatMessage(
          id: reply.userMessage.id,
          role: reply.userMessage.role,
          content: reply.userMessage.content,
          createdAt: reply.userMessage.createdAt,
          bookingDraft: reply.userMessage.bookingDraft,
          localAudioPath: path,
          isAudio: true,
          audioUrl: reply.userMessage.audioUrl,
        );
        if (pendingIndex == -1) {
          _messages.add(persistedAudio);
        } else {
          _messages[pendingIndex] = persistedAudio;
        }
        _messages.add(reply.assistantMessage);
        _draft = reply.bookingDraft;
      });
      if (reply.bookingDraft != null) await _prepareBooking(reply.bookingDraft!);
    } on DioException catch (exception) {
      if (!mounted) return;
      setState(() {
        _error = _apiErrorMessage(
          exception,
          'Không thể gửi file ghi âm. Vui lòng thử lại.',
        );
      });
    } finally {
      if (mounted) setState(() => _sending = false);
      _scrollToBottom();
    }
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _sending) return;
    _controller.clear();
    setState(() {
      _sending = true;
      _error = null;
      _messages.add(
        AiChatMessage(
          id: 'pending-${DateTime.now().microsecondsSinceEpoch}',
          role: 'user',
          content: text,
          createdAt: DateTime.now(),
        ),
      );
    });
    _scrollToBottom();

    try {
      final currentLocation = _currentLocation ?? await _loadCurrentLocation();
      final reply = await _service.sendMessage(
        message: text,
        conversationId: _conversationId,
        currentLocation: currentLocation,
      );
      if (!mounted) return;
      setState(() {
        _conversationId = reply.conversationId;
        _messages
          ..removeWhere((message) => message.id.startsWith('pending-'))
          ..add(reply.userMessage)
          ..add(reply.assistantMessage);
        _draft = reply.bookingDraft;
      });
      if (reply.bookingDraft != null) {
        await _prepareBooking(reply.bookingDraft!);
      }
    } on DioException catch (exception) {
      if (!mounted) return;
      final isServerError = (exception.response?.statusCode ?? 0) >= 500;
      setState(() {
        _error = isServerError
            ? 'Trợ lý AI đang gặp sự cố. Vui lòng thử lại sau.'
            : _apiErrorMessage(
                exception,
                'Không thể kết nối với trợ lý AI. Vui lòng thử lại.',
              );
      });
    } finally {
      if (mounted) setState(() => _sending = false);
      _scrollToBottom();
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 220),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _prepareBooking(AiBookingDraft draft) async {
    if (_preparingBooking) return;
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      setState(() => _error = 'Phiên đăng nhập đã hết hạn.');
      return;
    }

    setState(() {
      _preparingBooking = true;
      _error = null;
      _fareEstimate = null;
    });
    final bookingProvider = context.read<BookingProvider>();
    await Future.wait([
      bookingProvider.loadCatalog(token, forceRefresh: true),
      bookingProvider.loadAvailablePromotions(token),
    ]);
    if (!mounted) return;

    final catalog = bookingProvider.catalog;
    final vehicles = catalog?.vehicles ?? const <BookingVehicleOption>[];
    final promotions = bookingProvider.availablePromotions;
    final vehicleMatches = draft.vehicleQuery == null
        ? const <BookingVehicleOption>[]
        : vehicles
            .where((item) => _matchesVehicle(item, draft.vehicleQuery!))
            .toList();
    final promoMatches = draft.promotionCode == null
        ? const <PromoModel>[]
        : promotions
            .where(
              (item) =>
                  item.promotionCode.toLowerCase() ==
                  draft.promotionCode!.trim().toLowerCase(),
            )
            .toList();
    setState(() {
      _selectedVehicle = draft.vehicleQuery == null
          ? vehicles.firstOrNull
          : vehicleMatches.length == 1
          ? vehicleMatches.single
          : null;
      _selectedService = catalog?.services
          .where((item) => item.mode == BookingServiceMode.perTrip)
          .firstOrNull;
      _selectedPromo = promoMatches.length == 1 ? promoMatches.single : null;
      _bookingNotice = _buildSelectionNotice(
        draft,
        vehicleMatches.length,
        promoMatches.length,
      );
      _bookingStep = _BookingStep.vehicle;
      _preparingBooking = false;
    });
    await _estimateFare();
    _scrollToBottom();
  }

  Future<BookingLocation?> _loadCurrentLocation() async {
    final location = await context.read<BookingProvider>().getCurrentLocation();
    if (!mounted) return location;
    if (location != null) setState(() => _currentLocation = location);
    return location;
  }

  Future<void> _estimateFare() async {
    final draft = _draft;
    final vehicle = _selectedVehicle;
    final service = _selectedService;
    final token = context.read<AuthProvider>().token;
    if (draft == null || vehicle == null || service == null || token == null) {
      if (mounted) setState(() => _fareEstimate = null);
      return;
    }
    final estimate = await context.read<BookingProvider>().estimateFare(
      token,
      vehicleId: vehicle.id,
      serviceTypeId: service.id,
      pickup: draft.pickup,
      destination: draft.destination,
    );
    if (mounted) setState(() => _fareEstimate = estimate);
  }

  Future<void> _addVehicle() async {
    final vehicleProvider = context.read<VehicleProvider>();
    final knownIds = vehicleProvider.vehicles.map((item) => item.id).toSet();
    VehicleModel? savedVehicle;
    await VehicleFormSheet.show(
      context,
      onSave: (vehicle) async {
        final saved = await vehicleProvider.saveVehicle(vehicle);
        if (saved) {
          savedVehicle = vehicleProvider.vehicles
              .where((item) => !knownIds.contains(item.id))
              .lastOrNull;
        }
        return saved;
      },
    );
    if (!mounted || savedVehicle == null) return;
    final token = context.read<AuthProvider>().token;
    if (token == null) return;
    await context.read<BookingProvider>().loadCatalog(token, forceRefresh: true);
    if (!mounted) return;
    final vehicles = context.read<BookingProvider>().catalog?.vehicles ?? [];
    setState(() {
      _selectedVehicle = vehicles
          .where((item) => item.id == savedVehicle!.id)
          .firstOrNull;
    });
    await _estimateFare();
  }

  Future<void> _confirmBooking() async {
    final draft = _draft;
    final vehicle = _selectedVehicle;
    final service = _selectedService;
    final estimate = _fareEstimate;
    final token = context.read<AuthProvider>().token;
    if (draft == null || vehicle == null || service == null ||
        estimate == null || token == null || _creatingBooking) return;

    setState(() {
      _creatingBooking = true;
      _error = null;
    });
    final provider = context.read<BookingProvider>();
    final booking = await provider.createBooking(
      token,
      CreateBookingRequest(
        vehicleId: vehicle.id,
        serviceTypeId: service.id,
        bookingType: BookingType.now,
        pickup: draft.pickup,
        destination: draft.destination,
        promotionCode: _selectedPromo?.promotionCode,
      ),
    );
    if (!mounted) return;
    setState(() => _creatingBooking = false);
    if (booking == null) {
      setState(() => _error = provider.errorMessage ?? 'Không thể đặt chuyến.');
      return;
    }
    provider.setSearchingBooking(booking);
    final navigator = Navigator.of(context);
    navigator.pop();
    await navigator.push(
      MaterialPageRoute(
        builder: (_) => SearchingDriverPage(
          booking: booking,
          pickup: draft.pickup,
          destination: draft.destination,
          fareEstimate: estimate,
          vehicle: vehicle,
        ),
      ),
    );
  }

  Future<void> _restoreLatestConversation() async {
    try {
      final conversations = await _service.getConversations();
      if (!mounted || conversations.isEmpty) return;

      for (final conversation in conversations) {
        final messages = await _service.getMessages(conversation.id);
        if (!mounted) return;
        if (messages.isEmpty) continue;

        setState(() {
          _conversationId = conversation.id;
          _messages
            ..clear()
            ..addAll(messages);
          _draft = messages
              .where((message) => message.bookingDraft != null)
              .lastOrNull
              ?.bookingDraft;
        });
        if (_draft != null) await _prepareBooking(_draft!);
        _scrollToBottom();
        return;
      }
    } catch (_) {
      // The composer remains usable; send will surface a localized API error.
    }
  }

  void _newConversation() {
    setState(() {
      _conversationId = null;
      _messages.clear();
      _draft = null;
      _selectedVehicle = null;
      _selectedService = null;
      _selectedPromo = null;
      _fareEstimate = null;
      _bookingNotice = null;
      _bookingStep = _BookingStep.vehicle;
      _error = null;
    });
  }

  Future<void> _showHistory() async {
    final selectedId = await showModalBottomSheet<String>(
      context: context,
      useSafeArea: true,
      isScrollControlled: true,
      builder: (_) => _ConversationHistorySheet(
        service: _service,
        activeConversationId: _conversationId,
        onDeleted: (id) {
          if (id == _conversationId) _newConversation();
        },
      ),
    );
    if (!mounted || selectedId == null) return;
    await _openConversation(selectedId);
  }

  Future<void> _openConversation(String conversationId) async {
    setState(() {
      _sending = true;
      _error = null;
      _draft = null;
      _selectedVehicle = null;
      _selectedPromo = null;
      _fareEstimate = null;
      _bookingNotice = null;
      _bookingStep = _BookingStep.vehicle;
    });
    try {
      final messages = await _service.getMessages(conversationId);
      if (!mounted) return;
      final draft = messages
          .where((message) => message.bookingDraft != null)
          .lastOrNull
          ?.bookingDraft;
      setState(() {
        _conversationId = conversationId;
        _messages
          ..clear()
          ..addAll(messages);
        _draft = draft;
      });
      if (draft != null) await _prepareBooking(draft);
      _scrollToBottom();
    } on DioException catch (exception) {
      if (!mounted) return;
      setState(() {
        _error = _apiErrorMessage(exception, 'Không thể mở cuộc trò chuyện.');
      });
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.sizeOf(context).height * .82;
    return Container(
      height: height,
      decoration: const BoxDecoration(
        color: Color(0xFFFCF9F9),
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: Column(
        children: [
          _Header(
            onHistory: _showHistory,
            onNewConversation: _newConversation,
          ),
          Expanded(
            child: _messages.isEmpty
                ? const _Welcome()
                : ListView.builder(
                    controller: _scrollController,
                    padding: const EdgeInsets.all(16),
                    itemCount: _messages.length,
                    itemBuilder: (_, index) =>
                        _Bubble(message: _messages[index]),
                  ),
          ),
          if (_draft != null)
            _BookingComposer(
              loading: _preparingBooking,
              creating: _creatingBooking,
              vehicles: context.watch<BookingProvider>().catalog?.vehicles ?? const [],
              promotions: context.watch<BookingProvider>().availablePromotions,
              selectedVehicle: _selectedVehicle,
              selectedService: _selectedService,
              selectedPromo: _selectedPromo,
              fareEstimate: _fareEstimate,
              notice: _bookingNotice,
              draft: _draft!,
              step: _bookingStep,
              onVehicleSelected: (vehicle) async {
                setState(() => _selectedVehicle = vehicle);
                await _estimateFare();
              },
              onPromoSelected: (promo) => setState(() => _selectedPromo = promo),
              onContinueFromVehicle: () => setState(
                () => _bookingStep = _BookingStep.promotion,
              ),
              onContinueFromPromotion: () => setState(
                () => _bookingStep = _BookingStep.confirmation,
              ),
              onBack: () => setState(() {
                _bookingStep = switch (_bookingStep) {
                  _BookingStep.confirmation => _BookingStep.promotion,
                  _ => _BookingStep.vehicle,
                };
              }),
              onAddVehicle: _addVehicle,
              onConfirm: _confirmBooking,
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                _error!,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(color: Colors.red),
              ),
            ),
          Padding(
            padding: EdgeInsets.fromLTRB(
              12,
              8,
              12,
              12 + MediaQuery.viewInsetsOf(context).bottom,
            ),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _controller,
                    readOnly: _voiceMode,
                    minLines: 1,
                    maxLines: 4,
                    maxLength: 1000,
                    textInputAction: TextInputAction.send,
                    onSubmitted: (_) => _send(),
                    decoration: InputDecoration(
                      counterText: '',
                      hintText: _voiceMode
                          ? (_recording
                                ? 'Đang ghi âm...'
                                : 'Chọn gửi hoặc hủy bản ghi')
                          : 'Nhắn cho trợ lý SafeRide...',
                      filled: true,
                      fillColor: Colors.white,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(24),
                        borderSide: BorderSide.none,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                if (_voiceMode) ...[
                  IconButton.outlined(
                    tooltip: 'Hủy voice',
                    onPressed: _cancelVoice,
                    icon: const Icon(Icons.close_rounded),
                  ),
                  const SizedBox(width: 6),
                  IconButton.filled(
                    tooltip: 'Gửi voice',
                    onPressed: _sendVoice,
                    icon: const Icon(Icons.send_rounded),
                    style: IconButton.styleFrom(
                      backgroundColor: const Color(0xFF006B70),
                    ),
                  ),
                ] else ...[
                  IconButton.outlined(
                    tooltip: 'Nhập bằng giọng nói',
                    onPressed: _sending ? null : _startVoice,
                    icon: const Icon(Icons.mic_rounded),
                  ),
                  const SizedBox(width: 6),
                  IconButton.filled(
                    onPressed: _sending ? null : _send,
                    icon: _sending
                        ? const SizedBox.square(
                            dimension: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Icon(Icons.send_rounded),
                    style: IconButton.styleFrom(
                      backgroundColor: const Color(0xFF006B70),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

String _apiErrorMessage(DioException exception, String fallback) {
  final data = exception.response?.data;
  final detail = data is Map<String, dynamic>
      ? data['detail']?.toString().trim()
      : null;
  if (detail == null || detail.isEmpty || detail.length > 300) return fallback;
  if (detail.contains('Exception:') || detail.contains('\n   at ')) return fallback;
  return detail;
}

bool _matchesVehicle(BookingVehicleOption vehicle, String query) {
  String normalize(String value) =>
      value.toLowerCase().replaceAll(RegExp(r'[^a-z0-9]'), '');

  final needle = normalize(query);
  if (needle.isEmpty) return false;
  final name = normalize(vehicle.name);
  final plate = normalize(vehicle.plateNumber);
  return name.contains(needle) ||
      needle.contains(name) ||
      plate.contains(needle) ||
      needle.contains(plate);
}

String? _buildSelectionNotice(
  AiBookingDraft draft,
  int vehicleMatchCount,
  int promoMatchCount,
) {
  final notices = <String>[];
  if (draft.vehicleQuery != null) {
    notices.add(
      vehicleMatchCount == 1
          ? 'Đã chọn xe theo “${draft.vehicleQuery}”.'
          : 'Không tìm thấy chính xác xe “${draft.vehicleQuery}”. Vui lòng chọn lại.',
    );
  }
  if (draft.promotionCode != null) {
    notices.add(
      promoMatchCount == 1
          ? 'Đã áp dụng mã ${draft.promotionCode}.'
          : 'Mã ${draft.promotionCode} không khả dụng.',
    );
  }
  return notices.isEmpty ? null : notices.join(' ');
}

class _ConversationHistorySheet extends StatefulWidget {
  const _ConversationHistorySheet({
    required this.service,
    required this.activeConversationId,
    required this.onDeleted,
  });

  final AiChatService service;
  final String? activeConversationId;
  final ValueChanged<String> onDeleted;

  @override
  State<_ConversationHistorySheet> createState() =>
      _ConversationHistorySheetState();
}

class _ConversationHistorySheetState
    extends State<_ConversationHistorySheet> {
  List<AiConversation> _conversations = const [];
  bool _loading = true;
  String? _error;
  String? _deletingId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final conversations = await widget.service.getConversations();
      if (!mounted) return;
      setState(() {
        _conversations = conversations;
        _loading = false;
        _error = null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'Không thể tải lịch sử trò chuyện.';
      });
    }
  }

  Future<void> _delete(AiConversation conversation) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Xóa cuộc trò chuyện?'),
        content: Text(
          '“${conversation.title}” và các file ghi âm liên quan sẽ bị xóa vĩnh viễn.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _deletingId = conversation.id);
    try {
      await widget.service.deleteConversation(conversation.id);
      if (!mounted) return;
      setState(() {
        _conversations = _conversations
            .where((item) => item.id != conversation.id)
            .toList();
        _deletingId = null;
      });
      widget.onDeleted(conversation.id);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _deletingId = null;
        _error = 'Không thể xóa cuộc trò chuyện. Vui lòng thử lại.';
      });
    }
  }

  @override
  Widget build(BuildContext context) => SizedBox(
    height: MediaQuery.sizeOf(context).height * .72,
    child: Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 8, 12),
          child: Row(
            children: [
              const Expanded(
                child: Text(
                  'Lịch sử trò chuyện',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
              ),
              IconButton(
                tooltip: 'Đóng',
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.close_rounded),
              ),
            ],
          ),
        ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    _error!,
                    style: const TextStyle(color: Colors.red),
                  ),
                ),
                TextButton(onPressed: _load, child: const Text('Thử lại')),
              ],
            ),
          ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _conversations.isEmpty
              ? const Center(child: Text('Chưa có cuộc trò chuyện nào.'))
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
                  itemCount: _conversations.length,
                  separatorBuilder: (_, __) => const Divider(height: 1),
                  itemBuilder: (context, index) {
                    final conversation = _conversations[index];
                    final active =
                        conversation.id == widget.activeConversationId;
                    final deleting = conversation.id == _deletingId;
                    return ListTile(
                      selected: active,
                      leading: Icon(
                        active
                            ? Icons.chat_bubble_rounded
                            : Icons.chat_bubble_outline_rounded,
                      ),
                      title: Text(
                        conversation.title,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      subtitle: Text(
                        DateFormat('dd/MM/yyyy • HH:mm')
                            .format(conversation.updatedAt.toLocal()),
                      ),
                      onTap: deleting
                          ? null
                          : () => Navigator.pop(context, conversation.id),
                      trailing: deleting
                          ? const SizedBox.square(
                              dimension: 22,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : IconButton(
                              tooltip: 'Xóa cuộc trò chuyện',
                              onPressed: () => _delete(conversation),
                              icon: const Icon(Icons.delete_outline_rounded),
                            ),
                    );
                  },
                ),
        ),
      ],
    ),
  );
}

class _Header extends StatelessWidget {
  const _Header({
    required this.onHistory,
    required this.onNewConversation,
  });

  final VoidCallback onHistory;
  final VoidCallback onNewConversation;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 12, 8, 8),
    child: Row(
      children: [
        const CircleAvatar(
          backgroundColor: Color(0xFFE0F2F1),
          child: Icon(Icons.auto_awesome, color: Color(0xFF006B70)),
        ),
        const SizedBox(width: 12),
        const Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Trợ lý SafeRide',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
              Text(
                'AI có thể mắc lỗi • Kiểm tra trước khi đặt',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
            ],
          ),
        ),
        IconButton(
          tooltip: 'Lịch sử trò chuyện',
          onPressed: onHistory,
          icon: const Icon(Icons.history_rounded),
        ),
        IconButton(
          tooltip: 'Đoạn chat mới',
          onPressed: onNewConversation,
          icon: const Icon(Icons.add_comment_outlined),
        ),
        IconButton(
          onPressed: () => Navigator.pop(context),
          icon: const Icon(Icons.close),
        ),
      ],
    ),
  );
}

class _BookingComposer extends StatelessWidget {
  const _BookingComposer({
    required this.loading,
    required this.creating,
    required this.vehicles,
    required this.promotions,
    required this.selectedVehicle,
    required this.selectedService,
    required this.selectedPromo,
    required this.fareEstimate,
    required this.notice,
    required this.draft,
    required this.step,
    required this.onVehicleSelected,
    required this.onPromoSelected,
    required this.onContinueFromVehicle,
    required this.onContinueFromPromotion,
    required this.onBack,
    required this.onAddVehicle,
    required this.onConfirm,
  });

  final bool loading;
  final bool creating;
  final List<BookingVehicleOption> vehicles;
  final List<PromoModel> promotions;
  final BookingVehicleOption? selectedVehicle;
  final BookingServiceOption? selectedService;
  final PromoModel? selectedPromo;
  final BookingFareEstimate? fareEstimate;
  final String? notice;
  final AiBookingDraft draft;
  final _BookingStep step;
  final ValueChanged<BookingVehicleOption> onVehicleSelected;
  final ValueChanged<PromoModel?> onPromoSelected;
  final VoidCallback onContinueFromVehicle;
  final VoidCallback onContinueFromPromotion;
  final VoidCallback onBack;
  final VoidCallback onAddVehicle;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Padding(
        padding: EdgeInsets.all(16),
        child: LinearProgressIndicator(),
      );
    }

    return Container(
      constraints: const BoxConstraints(maxHeight: 500),
      margin: const EdgeInsets.fromLTRB(12, 4, 12, 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFDDE5E5)),
        borderRadius: BorderRadius.circular(8),
      ),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                if (step != _BookingStep.vehicle)
                  IconButton(
                    tooltip: 'Quay lại',
                    onPressed: onBack,
                    icon: const Icon(Icons.arrow_back_rounded),
                  ),
                Expanded(
                  child: Text(
                    switch (step) {
                      _BookingStep.vehicle => 'Bạn muốn đi bằng xe nào?',
                      _BookingStep.promotion => 'Chọn mã giảm giá',
                      _BookingStep.confirmation => 'Xác nhận chuyến đi',
                    },
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            if (notice != null && step != _BookingStep.confirmation) ...[
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFFEAF5F4),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(notice!, style: const TextStyle(fontSize: 13)),
              ),
              const SizedBox(height: 12),
            ],
            if (step == _BookingStep.vehicle) ...[
              Row(
              children: [
                const Expanded(
                  child: Text('Xe của bạn', style: TextStyle(fontWeight: FontWeight.w600)),
                ),
                TextButton.icon(
                  onPressed: onAddVehicle,
                  icon: const Icon(Icons.add_rounded, size: 18),
                  label: Text(vehicles.isEmpty ? 'Thêm xe' : 'Xe mới'),
                ),
              ],
            ),
              if (vehicles.isEmpty)
              const Text(
                'Bạn chưa có xe. Hãy thêm xe để tiếp tục đặt chuyến.',
                style: TextStyle(color: Color(0xFF666666)),
              )
              else
              ...vehicles.map(
                (vehicle) => RadioListTile<int>(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  value: vehicle.id,
                  groupValue: selectedVehicle?.id,
                  onChanged: (_) => onVehicleSelected(vehicle),
                  secondary: Icon(
                    vehicle.isMotorbike ? Icons.two_wheeler : Icons.directions_car,
                  ),
                  title: Text(vehicle.name),
                  subtitle: Text('${vehicle.plateNumber} • ${vehicle.color}'),
                ),
              ),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: selectedVehicle == null || fareEstimate == null
                    ? null
                    : onContinueFromVehicle,
                child: const Text('Tiếp tục chọn mã giảm giá'),
              ),
            ],
            if (step == _BookingStep.promotion) ...[
              if (promotions.isEmpty)
              const Padding(
                padding: EdgeInsets.only(top: 6),
                child: Text(
                  'Hiện không có mã giảm giá khả dụng.',
                  style: TextStyle(color: Color(0xFF666666)),
                ),
              )
              else ...[
              RadioListTile<int>(
                dense: true,
                contentPadding: EdgeInsets.zero,
                value: -1,
                groupValue: selectedPromo?.promotionId ?? -1,
                onChanged: (_) => onPromoSelected(null),
                title: const Text('Không dùng mã giảm giá'),
              ),
              ...promotions.map(
                (promo) => RadioListTile<int>(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  value: promo.promotionId,
                  groupValue: selectedPromo?.promotionId ?? -1,
                  onChanged: (_) => onPromoSelected(promo),
                  title: Text(promo.promotionCode),
                  subtitle: Text(promo.shortDescription),
                ),
              ),
              ],
              const SizedBox(height: 10),
              FilledButton(
                onPressed: onContinueFromPromotion,
                child: Text(
                  promotions.isEmpty || selectedPromo == null
                      ? 'Tiếp tục không dùng mã'
                      : 'Dùng mã ${selectedPromo!.promotionCode}',
                ),
              ),
            ],
            if (step == _BookingStep.confirmation) ...[
              SizedBox(
                height: 190,
                child: _ChatRouteMap(draft: draft, estimate: fareEstimate),
              ),
              const SizedBox(height: 12),
              _SummaryRow(label: 'Điểm đón', value: draft.pickup.address),
              _SummaryRow(label: 'Điểm đến', value: draft.destination.address),
              if (selectedVehicle != null)
                _SummaryRow(
                  label: 'Xe',
                  value:
                      '${selectedVehicle!.name} • ${selectedVehicle!.plateNumber}',
                ),
              _SummaryRow(
                label: 'Khuyến mãi',
                value: selectedPromo?.promotionCode ?? 'Không sử dụng',
              ),
              if (fareEstimate != null) ...[
              const Divider(height: 20),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    '${fareEstimate!.estimatedDistanceKm.toStringAsFixed(1)} km • '
                    '${fareEstimate!.estimatedDurationMinutes} phút',
                  ),
                  Text(
                    NumberFormat.currency(locale: 'vi_VN', symbol: 'đ')
                        .format(fareEstimate!.estimatedFare),
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                ],
              ),
              ],
              const SizedBox(height: 10),
              FilledButton.icon(
              onPressed: creating || selectedVehicle == null ||
                      selectedService == null || fareEstimate == null
                  ? null
                  : onConfirm,
              icon: creating
                  ? const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.search_rounded),
              label: const Text('Xác nhận và tìm tài xế'),
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(46),
                backgroundColor: const Color(0xFF006B70),
              ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 82,
          child: Text(label, style: const TextStyle(color: Color(0xFF666666))),
        ),
        Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600))),
      ],
    ),
  );
}

class _ChatRouteMap extends StatefulWidget {
  const _ChatRouteMap({required this.draft, required this.estimate});

  final AiBookingDraft draft;
  final BookingFareEstimate? estimate;

  @override
  State<_ChatRouteMap> createState() => _ChatRouteMapState();
}

class _ChatRouteMapState extends State<_ChatRouteMap> {
  AppMapController? _controller;

  AppLatLng get _pickup => AppLatLng(
    widget.draft.pickup.latitude,
    widget.draft.pickup.longitude,
  );
  AppLatLng get _destination => AppLatLng(
    widget.draft.destination.latitude,
    widget.draft.destination.longitude,
  );

  List<AppLatLng> get _route {
    final encoded = widget.estimate?.encodedPolyline ?? '';
    if (encoded.isEmpty) return [_pickup, _destination];
    try {
      return decodePolyline(encoded);
    } on FormatException {
      return [_pickup, _destination];
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _fitRoute() => _controller!.animateCameraToBounds(
    AppLatLng(
      _pickup.latitude < _destination.latitude
          ? _pickup.latitude
          : _destination.latitude,
      _pickup.longitude < _destination.longitude
          ? _pickup.longitude
          : _destination.longitude,
    ),
    AppLatLng(
      _pickup.latitude > _destination.latitude
          ? _pickup.latitude
          : _destination.latitude,
      _pickup.longitude > _destination.longitude
          ? _pickup.longitude
          : _destination.longitude,
    ),
    48,
  );

  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(8),
    child: MapRendererWidget(
      initialCameraPosition: AppCameraPosition(target: _pickup, zoom: 13),
      markers: {
        AppMarker(
          id: 'pickup',
          position: _pickup,
          markerType: AppMarkerType.pickup,
        ),
        AppMarker(
          id: 'destination',
          position: _destination,
          markerType: AppMarkerType.destination,
        ),
      },
      polylines: {
        AppPolyline(id: 'route', points: _route, color: const Color(0xFF006B70)),
      },
      onMapCreated: (controller) {
        _controller = controller;
        WidgetsBinding.instance.addPostFrameCallback((_) => _fitRoute());
      },
      myLocationButtonEnabled: false,
    ),
  );
}

class _Welcome extends StatelessWidget {
  const _Welcome();

  @override
  Widget build(BuildContext context) => const Center(
    child: Padding(
      padding: EdgeInsets.all(32),
      child: Text(
        'Xin chào! Mình có thể hỗ trợ bạn sử dụng SafeRide hoặc chuẩn bị một chuyến đi.\n\nVí dụ: “Đặt xe từ Đại học FPT đến sân bay Tân Sơn Nhất”.',
        textAlign: TextAlign.center,
        style: TextStyle(height: 1.5, color: Color(0xFF555555)),
      ),
    ),
  );
}

class _Bubble extends StatefulWidget {
  const _Bubble({required this.message});

  final AiChatMessage message;

  @override
  State<_Bubble> createState() => _BubbleState();
}

class _BubbleState extends State<_Bubble> {
  AudioPlayer? _player;
  bool _playing = false;

  @override
  void dispose() {
    _player?.dispose();
    super.dispose();
  }

  Future<void> _toggleAudio() async {
    final path = widget.message.localAudioPath;
    final url = widget.message.audioUrl;
    if (path == null && (url == null || url.isEmpty)) return;
    final player = _player ??= AudioPlayer();
    if (_playing) {
      await player.pause();
      if (mounted) setState(() => _playing = false);
      return;
    }
    player.onPlayerComplete.first.then((_) {
      if (mounted) setState(() => _playing = false);
    });
    await player.play(path != null ? DeviceFileSource(path) : UrlSource(url!));
    if (mounted) setState(() => _playing = true);
  }

  @override
  Widget build(BuildContext context) => Align(
    alignment: widget.message.isUser
        ? Alignment.centerRight
        : Alignment.centerLeft,
    child: Container(
      constraints: const BoxConstraints(maxWidth: 300),
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: widget.message.isUser ? const Color(0xFF006B70) : Colors.white,
        borderRadius: BorderRadius.circular(16),
      ),
      child: !widget.message.isAudio
          ? Text(
              widget.message.content,
              style: TextStyle(
                color: widget.message.isUser
                    ? Colors.white
                    : const Color(0xFF222222),
                height: 1.4,
              ),
            )
          : InkWell(
              onTap: widget.message.localAudioPath == null &&
                      widget.message.audioUrl == null
                  ? null
                  : _toggleAudio,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    widget.message.localAudioPath == null &&
                            widget.message.audioUrl == null
                        ? Icons.mic_rounded
                        : _playing
                        ? Icons.pause_rounded
                        : Icons.play_arrow_rounded,
                    color: Colors.white,
                  ),
                  const SizedBox(width: 8),
                  const Icon(Icons.graphic_eq_rounded, color: Colors.white),
                  const SizedBox(width: 8),
                  const Text(
                    'Tin nhắn thoại',
                    style: TextStyle(color: Colors.white),
                  ),
                ],
              ),
            ),
    ),
  );
}
