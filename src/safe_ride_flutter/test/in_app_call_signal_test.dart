import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/services/socket_service.dart';

void main() {
  test('in-app call signal serializes SignalR payload', () {
    const signal = InAppCallSignal(
      tripId: 42,
      bookingId: 24,
      callId: 'call-1',
      sdp: 'offer-sdp',
      sdpType: 'offer',
      candidate: 'candidate:1',
      sdpMid: '0',
      sdpMLineIndex: 0,
    );

    final json = signal.toJson();

    expect(json[ApiKeys.tripId], 42);
    expect(json[ApiKeys.bookingId], 24);
    expect(json['callId'], 'call-1');
    expect(json['sdp'], 'offer-sdp');
    expect(json['sdpType'], 'offer');
    expect(json['candidate'], 'candidate:1');
    expect(json['sdpMid'], '0');
    expect(json['sdpMLineIndex'], 0);
  });

  test('in-app call signal parses camelCase SignalR arguments', () {
    final signal = InAppCallSignal.fromSignalRArguments([
      {
        'tripId': 42,
        'bookingId': 24,
        'callId': 'call-1',
        'sdp': 'answer-sdp',
        'sdpType': 'answer',
        'candidate': 'candidate:1',
        'sdpMid': 'audio',
        'sdpMLineIndex': 0,
      },
    ]);

    expect(signal, isNotNull);
    expect(signal!.tripId, 42);
    expect(signal.bookingId, 24);
    expect(signal.callId, 'call-1');
    expect(signal.sdp, 'answer-sdp');
    expect(signal.sdpType, 'answer');
    expect(signal.candidate, 'candidate:1');
    expect(signal.sdpMid, 'audio');
    expect(signal.sdpMLineIndex, 0);
  });

  test('in-app call signal parses PascalCase SignalR arguments', () {
    final signal = InAppCallSignal.fromSignalRArguments([
      {
        'TripId': 42,
        'BookingId': 24,
        'CallId': 'call-1',
        'Sdp': 'offer-sdp',
        'SdpType': 'offer',
        'Candidate': 'candidate:1',
        'SdpMid': 'audio',
        'SdpMLineIndex': 0,
      },
    ]);

    expect(signal, isNotNull);
    expect(signal!.tripId, 42);
    expect(signal.bookingId, 24);
    expect(signal.callId, 'call-1');
    expect(signal.sdp, 'offer-sdp');
    expect(signal.sdpType, 'offer');
    expect(signal.candidate, 'candidate:1');
    expect(signal.sdpMid, 'audio');
    expect(signal.sdpMLineIndex, 0);
  });

  test('in-app call signal ignores invalid arguments', () {
    expect(InAppCallSignal.fromSignalRArguments(null), isNull);
    expect(InAppCallSignal.fromSignalRArguments([]), isNull);
    expect(InAppCallSignal.fromSignalRArguments(['not-a-map']), isNull);
    expect(
      InAppCallSignal.fromSignalRArguments([
        {'bookingId': 24, 'callId': 'call-1'},
      ]),
      isNull,
    );
    expect(
      InAppCallSignal.fromSignalRArguments([
        {'tripId': 42},
      ]),
      isNull,
    );
  });
}
