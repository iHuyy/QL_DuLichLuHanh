import 'dart:convert';
import 'dart:typed_data';
import 'package:flutter/material.dart';

class ImageHelper {
  /// Build an image widget from either:
  /// - a base64 string (raw base64 or data:<mime>;base64,...)
  /// - an absolute/https url
  /// If data is null or cannot be decoded, returns a placeholder Container.
  static Widget imageFromData(
    String? data, {
    String? mime,
    double? width,
    double? height,
    BoxFit fit = BoxFit.cover,
    BorderRadius? borderRadius,
  }) {
    if (data == null || data.trim().isEmpty) {
      return _placeholder(width, height);
    }

    // If string is a data URL like: data:image/jpeg;base64,/9j/4AAQ...
    String payload = data.trim();
    if (payload.startsWith('data:')) {
      final comma = payload.indexOf(',');
      if (comma > 0) {
        final body = payload.substring(comma + 1);
        try {
          final bytes = base64Decode(body);
          return _imageMemory(bytes, width, height, fit, borderRadius);
        } catch (_) {
          return _placeholder(width, height);
        }
      }
    }

    // Try decode as base64 directly
    try {
      // base64 strings are often long; attempt decode and if it fails, treat as URL
      final bytes = base64Decode(payload);
      return _imageMemory(bytes, width, height, fit, borderRadius);
    } catch (_) {
      // Not base64: treat as URL
      return _imageNetwork(payload, width, height, fit, borderRadius);
    }
  }

  static Widget _imageMemory(Uint8List bytes, double? width, double? height, BoxFit fit, BorderRadius? br) {
    Widget img = Image.memory(bytes, width: width, height: height, fit: fit);
    if (br != null) return ClipRRect(borderRadius: br, child: img);
    return img;
  }

  static Widget _imageNetwork(String url, double? width, double? height, BoxFit fit, BorderRadius? br) {
    Widget img = Image.network(
      url,
      width: width,
      height: height,
      fit: fit,
      errorBuilder: (c, e, st) => _placeholder(width, height),
    );
    if (br != null) return ClipRRect(borderRadius: br, child: img);
    return img;
  }

  static Widget _placeholder(double? width, double? height) {
    return Container(
      width: width ?? 100,
      height: height ?? 100,
      color: Colors.grey[200],
      child: const Icon(Icons.image_not_supported, color: Colors.grey),
    );
  }
}
