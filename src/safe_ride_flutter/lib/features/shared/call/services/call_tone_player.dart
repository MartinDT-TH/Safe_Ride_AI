import 'dart:math' as math;
import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';

/// Plays synthetic call tones so the app does not depend on device ringtones
/// or bundled copyrighted audio files.
class CallTonePlayer {
  CallTonePlayer() : _player = AudioPlayer();

  final AudioPlayer _player;
  bool _disposed = false;

  Future<void> playOutgoing() => _play(
    _buildPattern(
      frequencies: const [425],
      toneMilliseconds: 1000,
      silenceMilliseconds: 3000,
    ),
  );

  Future<void> playIncoming() => _play(
    _buildPattern(
      frequencies: const [440, 480],
      toneMilliseconds: 900,
      silenceMilliseconds: 1100,
    ),
  );

  Future<void> _play(Uint8List bytes) async {
    if (_disposed) return;
    try {
      await _player.stop();
      await _player.setReleaseMode(ReleaseMode.loop);
      await _player.setVolume(0.8);
      await _player.play(BytesSource(bytes));
    } catch (_) {
      // A tone must never prevent the actual call flow from continuing.
    }
  }

  Future<void> stop() async {
    if (_disposed) return;
    try {
      await _player.stop();
    } catch (_) {
      // The platform player may already have been released.
    }
  }

  Future<void> dispose() async {
    if (_disposed) return;
    _disposed = true;
    try {
      await _player.stop();
      await _player.dispose();
    } catch (_) {
      // Cleanup is best-effort during route disposal.
    }
  }

  static Uint8List _buildPattern({
    required List<int> frequencies,
    required int toneMilliseconds,
    required int silenceMilliseconds,
  }) {
    const sampleRate = 16000;
    const amplitude = 7000;
    final toneSamples = sampleRate * toneMilliseconds ~/ 1000;
    final silenceSamples = sampleRate * silenceMilliseconds ~/ 1000;
    final sampleCount = toneSamples + silenceSamples;
    final pcmLength = sampleCount * 2;
    final data = ByteData(44 + pcmLength);

    void ascii(int offset, String value) {
      for (var i = 0; i < value.length; i++) {
        data.setUint8(offset + i, value.codeUnitAt(i));
      }
    }

    ascii(0, 'RIFF');
    data.setUint32(4, 36 + pcmLength, Endian.little);
    ascii(8, 'WAVE');
    ascii(12, 'fmt ');
    data.setUint32(16, 16, Endian.little);
    data.setUint16(20, 1, Endian.little);
    data.setUint16(22, 1, Endian.little);
    data.setUint32(24, sampleRate, Endian.little);
    data.setUint32(28, sampleRate * 2, Endian.little);
    data.setUint16(32, 2, Endian.little);
    data.setUint16(34, 16, Endian.little);
    ascii(36, 'data');
    data.setUint32(40, pcmLength, Endian.little);

    for (var i = 0; i < toneSamples; i++) {
      var wave = 0.0;
      for (final frequency in frequencies) {
        wave += math.sin(2 * math.pi * frequency * i / sampleRate);
      }
      final fade = i < 160
          ? i / 160
          : (toneSamples - i < 160 ? (toneSamples - i) / 160 : 1.0);
      final sample = (amplitude * fade * wave / frequencies.length).round();
      data.setInt16(44 + i * 2, sample, Endian.little);
    }
    return data.buffer.asUint8List();
  }
}
