import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';

import '../../../../../core/services/socket_service.dart';
import '../../../../../dependency_injection/injection.dart';

class InAppVoiceCallPage extends StatefulWidget {
  const InAppVoiceCallPage({
    super.key,
    required this.tripId,
    required this.peerName,
    required this.accessToken,
    this.bookingId,
    this.initialOffer,
  });

  final int tripId;
  final int? bookingId;
  final String peerName;
  final String accessToken;
  final InAppCallSignal? initialOffer;

  @override
  State<InAppVoiceCallPage> createState() => _InAppVoiceCallPageState();
}

class _InAppVoiceCallPageState extends State<InAppVoiceCallPage> {
  final SocketService _socketService = getIt<SocketService>();
  final RTCVideoRenderer _remoteRenderer = RTCVideoRenderer();
  final String _callId =
      '${DateTime.now().millisecondsSinceEpoch}-${Random().nextInt(999999)}';

  RTCPeerConnection? _peerConnection;
  MediaStream? _localStream;
  MediaStream? _remoteStream;
  Timer? _durationTimer;
  Duration _duration = Duration.zero;
  bool _initializing = true;
  bool _connected = false;
  bool _muted = false;
  bool _speakerOn = true;
  bool _closed = false;
  String? _errorMessage;

  bool get _isCallee => widget.initialOffer != null;
  String get _activeCallId => widget.initialOffer?.callId ?? _callId;

  @override
  void initState() {
    super.initState();
    unawaited(_initializeCall());
  }

  @override
  void dispose() {
    _durationTimer?.cancel();
    _removeSignalHandlers();
    unawaited(_hangUp(notifyPeer: true));
    _remoteRenderer.dispose();
    super.dispose();
  }

  Future<void> _initializeCall() async {
    try {
      await _remoteRenderer.initialize();
      await _socketService.connect(widget.accessToken);
      await _socketService.joinTrip(widget.tripId);
      _registerSignalHandlers();
      await _createPeerConnection();

      if (_isCallee) {
        await _acceptInitialOffer(widget.initialOffer!);
      } else {
        await _startOutgoingCall();
      }

      if (mounted) {
        setState(() => _initializing = false);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _initializing = false;
        _errorMessage = 'Không thể bắt đầu cuộc gọi. Vui lòng thử lại.';
      });
    }
  }

  Future<void> _createPeerConnection() async {
    _localStream = await navigator.mediaDevices.getUserMedia({
      'audio': true,
      'video': false,
    });

    final peerConnection = await createPeerConnection({
      'iceServers': [
        {'urls': 'stun:stun.l.google.com:19302'},
      ],
      'sdpSemantics': 'unified-plan',
    });
    _peerConnection = peerConnection;

    for (final track in _localStream!.getAudioTracks()) {
      await peerConnection.addTrack(track, _localStream!);
    }

    peerConnection.onIceCandidate = (candidate) {
      if (candidate.candidate == null) return;
      unawaited(
        _socketService.sendInAppCallIceCandidate(
          InAppCallSignal(
            tripId: widget.tripId,
            bookingId: widget.bookingId,
            callId: _activeCallId,
            candidate: candidate.candidate,
            sdpMid: candidate.sdpMid,
            sdpMLineIndex: candidate.sdpMLineIndex,
          ),
        ),
      );
    };

    peerConnection.onTrack = (event) {
      if (event.streams.isEmpty) return;
      _remoteStream = event.streams.first;
      _remoteRenderer.srcObject = _remoteStream;
      if (!mounted) return;
      setState(() => _connected = true);
      _startDurationTimer();
    };
  }

  Future<void> _startOutgoingCall() async {
    final offer = await _peerConnection!.createOffer({
      'offerToReceiveAudio': true,
      'offerToReceiveVideo': false,
    });
    await _peerConnection!.setLocalDescription(offer);
    await _socketService.sendInAppCallOffer(
      InAppCallSignal(
        tripId: widget.tripId,
        bookingId: widget.bookingId,
        callId: _activeCallId,
        sdp: offer.sdp,
        sdpType: offer.type,
      ),
    );
  }

  Future<void> _acceptInitialOffer(InAppCallSignal offerSignal) async {
    final sdp = offerSignal.sdp;
    final sdpType = offerSignal.sdpType;
    if (sdp == null || sdpType == null) {
      throw StateError('Incoming call offer is missing SDP.');
    }

    await _peerConnection!.setRemoteDescription(
      RTCSessionDescription(sdp, sdpType),
    );
    final answer = await _peerConnection!.createAnswer({
      'offerToReceiveAudio': true,
      'offerToReceiveVideo': false,
    });
    await _peerConnection!.setLocalDescription(answer);
    await _socketService.sendInAppCallAnswer(
      InAppCallSignal(
        tripId: widget.tripId,
        bookingId: widget.bookingId,
        callId: _activeCallId,
        sdp: answer.sdp,
        sdpType: answer.type,
      ),
    );
  }

  void _registerSignalHandlers() {
    final key = 'voiceCall:${widget.tripId}:$_activeCallId';
    _socketService.onInAppCallAnswer((signal) async {
      if (!_matchesCall(signal) ||
          signal.sdp == null ||
          signal.sdpType == null) {
        return;
      }
      await _peerConnection?.setRemoteDescription(
        RTCSessionDescription(signal.sdp, signal.sdpType),
      );
      if (mounted) setState(() => _connected = true);
      _startDurationTimer();
    }, key: '$key:answer');
    _socketService.onInAppCallIceCandidate((signal) async {
      if (!_matchesCall(signal) || signal.candidate == null) return;
      await _peerConnection?.addCandidate(
        RTCIceCandidate(signal.candidate, signal.sdpMid, signal.sdpMLineIndex),
      );
    }, key: '$key:ice');
    _socketService.onInAppCallRejected((signal) {
      if (!_matchesCall(signal)) return;
      _closeWithMessage('Đối phương đã từ chối cuộc gọi.');
    }, key: '$key:rejected');
    _socketService.onInAppCallEnded((signal) {
      if (!_matchesCall(signal)) return;
      _closeWithMessage('Cuộc gọi đã kết thúc.');
    }, key: '$key:ended');
  }

  void _removeSignalHandlers() {
    final key = 'voiceCall:${widget.tripId}:$_activeCallId';
    _socketService.removeInAppCallAnswerHandler('$key:answer');
    _socketService.removeInAppCallIceCandidateHandler('$key:ice');
    _socketService.removeInAppCallRejectedHandler('$key:rejected');
    _socketService.removeInAppCallEndedHandler('$key:ended');
  }

  bool _matchesCall(InAppCallSignal signal) {
    return signal.tripId == widget.tripId && signal.callId == _activeCallId;
  }

  void _startDurationTimer() {
    _durationTimer ??= Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) {
        setState(() => _duration += const Duration(seconds: 1));
      }
    });
  }

  Future<void> _toggleMute() async {
    final nextMuted = !_muted;
    for (final track
        in _localStream?.getAudioTracks() ?? <MediaStreamTrack>[]) {
      track.enabled = !nextMuted;
    }
    setState(() => _muted = nextMuted);
  }

  Future<void> _toggleSpeaker() async {
    final nextSpeaker = !_speakerOn;
    await Helper.setSpeakerphoneOn(nextSpeaker);
    setState(() => _speakerOn = nextSpeaker);
  }

  Future<void> _hangUp({required bool notifyPeer}) async {
    if (_closed) return;
    _closed = true;
    if (notifyPeer) {
      await _socketService.endInAppCall(
        InAppCallSignal(
          tripId: widget.tripId,
          bookingId: widget.bookingId,
          callId: _activeCallId,
        ),
      );
    }
    for (final track in _localStream?.getTracks() ?? <MediaStreamTrack>[]) {
      await track.stop();
    }
    await _localStream?.dispose();
    await _remoteStream?.dispose();
    await _peerConnection?.close();
    await _peerConnection?.dispose();
  }

  void _closeWithMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
    Navigator.of(context).maybePop();
  }

  String _formatDuration() {
    final minutes = _duration.inMinutes
        .remainder(60)
        .toString()
        .padLeft(2, '0');
    final seconds = _duration.inSeconds
        .remainder(60)
        .toString()
        .padLeft(2, '0');
    return '$minutes:$seconds';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF052C2F),
      body: SafeArea(
        child: Stack(
          children: [
            Positioned.fill(child: RTCVideoView(_remoteRenderer)),
            Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  const Spacer(),
                  CircleAvatar(
                    radius: 48,
                    backgroundColor: const Color(0xFF0E6B70),
                    child: Text(
                      widget.peerName.trim().isEmpty
                          ? 'SR'
                          : widget.peerName.trim()[0].toUpperCase(),
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 36,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text(
                    widget.peerName,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 26,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    _errorMessage ??
                        (_initializing
                            ? 'Đang kết nối...'
                            : _connected
                            ? _formatDuration()
                            : 'Đang đổ chuông...'),
                    style: const TextStyle(
                      color: Color(0xFFCFE8E8),
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const Spacer(),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      _CallControlButton(
                        icon: _muted
                            ? Icons.mic_off_rounded
                            : Icons.mic_rounded,
                        label: _muted ? 'Bật mic' : 'Tắt mic',
                        onPressed: _toggleMute,
                      ),
                      _CallControlButton(
                        icon: Icons.call_end_rounded,
                        label: 'Kết thúc',
                        backgroundColor: const Color(0xFFE53935),
                        onPressed: () async {
                          await _hangUp(notifyPeer: true);
                          if (context.mounted) Navigator.of(context).pop();
                        },
                      ),
                      _CallControlButton(
                        icon: _speakerOn
                            ? Icons.volume_up_rounded
                            : Icons.hearing_rounded,
                        label: _speakerOn ? 'Loa ngoài' : 'Tai nghe',
                        onPressed: _toggleSpeaker,
                      ),
                    ],
                  ),
                  const SizedBox(height: 32),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CallControlButton extends StatelessWidget {
  const _CallControlButton({
    required this.icon,
    required this.label,
    required this.onPressed,
    this.backgroundColor = const Color(0xFF174D51),
  });

  final IconData icon;
  final String label;
  final Future<void> Function() onPressed;
  final Color backgroundColor;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        InkWell(
          onTap: () => unawaited(onPressed()),
          borderRadius: BorderRadius.circular(32),
          child: Container(
            width: 64,
            height: 64,
            decoration: BoxDecoration(
              color: backgroundColor,
              shape: BoxShape.circle,
            ),
            child: Icon(icon, color: Colors.white, size: 28),
          ),
        ),
        const SizedBox(height: 10),
        Text(
          label,
          style: const TextStyle(
            color: Color(0xFFCFE8E8),
            fontSize: 12,
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}
