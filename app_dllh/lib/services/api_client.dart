import 'dart:convert';
import 'package:http/http.dart' as http;
import 'dart:io';
import 'dart:async';
import 'package:shared_preferences/shared_preferences.dart';
import 'navigation_service.dart';
import 'package:flutter/material.dart';

class ApiClient extends http.BaseClient {
  final http.Client _inner = http.Client();
  final String baseUrl = "http://10.0.2.2/KLTN";

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final prefs = await SharedPreferences.getInstance();
    final sessionId = prefs.getString('session_id');

    // Thêm session_id vào header nếu có
    if (sessionId != null) {
      request.headers['Authorization'] = 'Bearer $sessionId';
    }
    request.headers['Content-Type'] = 'application/json; charset=utf-8';

    // Gửi yêu cầu (catch network errors like SocketException / timeout)
    http.StreamedResponse response;
    try {
      // set a reasonable timeout for network operations
      response = await _inner.send(request).timeout(const Duration(seconds: 12));
    } on SocketException catch (e) {
      // network unreachable
      await prefs.remove('session_id');
      final navigatorState = NavigationService.navigatorKey.currentState;
      if (navigatorState != null) {
        showDialog(
          context: navigatorState.context,
          barrierDismissible: true,
          builder: (ctx) => AlertDialog(
            title: const Text('Không thể kết nối'),
            content: Text('Không thể kết nối tới máy chủ: ${e.message}. Vui lòng kiểm tra kết nối hoặc cấu hình server.'),
            actions: [
              TextButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('OK')),
            ],
          ),
        );
      }

      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Network error: ${e.message}"}))),
        503,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
  } on TimeoutException {
      await prefs.remove('session_id');
      final navigatorState = NavigationService.navigatorKey.currentState;
      if (navigatorState != null) {
        showDialog(
          context: navigatorState.context,
          barrierDismissible: true,
          builder: (ctx) => AlertDialog(
            title: const Text('Hết thời gian chờ'),
            content: const Text('Yêu cầu tới máy chủ mất quá nhiều thời gian. Vui lòng thử lại.'),
            actions: [
              TextButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('OK')),
            ],
          ),
        );
      }

      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Timeout"}))),
        504,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    // === XỬ LÝ 401 UNAUTHORIZED ===
    if (response.statusCode == 401) {
      // Xóa session_id đã lưu
      await prefs.remove('session_id');

      // Try to show a dialog using the navigator key's state/context. If not available,
      // fallback to direct navigation to the login route.
      final navigatorState = NavigationService.navigatorKey.currentState;
      final dialogContext = NavigationService.currentContext;

      if (navigatorState != null && dialogContext != null) {
        // Show a blocking dialog to inform the user their session is invalid.
        // Use navigatorState.context to ensure we have a valid mounted context.
        showDialog(
          context: navigatorState.context,
          barrierDismissible: false,
          builder: (BuildContext dContext) {
            return AlertDialog(
              title: const Text('Phiên Đã Hết Hạn'),
              content: const Text('Tài khoản của bạn đã được đăng nhập trên một thiết bị khác. Vui lòng đăng nhập lại.'),
              actions: <Widget>[
                TextButton(
                  child: const Text('OK'),
                  onPressed: () {
                    Navigator.of(dContext).pop(); // Đóng dialog
                    NavigationService.navigateToAndRemoveUntil('/login');
                  },
                ),
              ],
            );
          },
        );
      } else {
        // Fallback: navigate without showing dialog
        NavigationService.navigateToAndRemoveUntil('/login');
      }

      // Return an empty JSON StreamedResponse with proper content-type header so callers
      // attempting to decode JSON don't crash.
      return http.StreamedResponse(
        Stream.value(utf8.encode('{}')),
        401,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    // Fire-and-forget: verify session still active on server-side.
    // We use the inner client to avoid re-entering this send() method.
    // This helps the app detect when IS_ACTIVE has been flipped to 'N' externally
    // (for example, when web triggers a remote logout) even if the current
    // response was 200 and not protected by require_auth().
    try {
      if (sessionId != null) {
        final checkUri = Uri.parse('$baseUrl/Customer/CheckSession');
        // Use the inner client and pass the same Authorization header.
        final checkResp = await _inner
            .get(checkUri, headers: {'Authorization': 'Bearer $sessionId'})
            .timeout(const Duration(seconds: 6));
        if (checkResp.statusCode == 200) {
          try {
            final map = jsonDecode(checkResp.body);
            final valid = map is Map && map['valid'] == true;
            if (!valid) {
              // Session invalidated on server side -> force client logout flow.
              await prefs.remove('session_id');
              final navigatorState = NavigationService.navigatorKey.currentState;
              if (navigatorState != null) {
                showDialog(
                  context: navigatorState.context,
                  barrierDismissible: false,
                  builder: (BuildContext dContext) {
                    return AlertDialog(
                      title: const Text('Phiên Đã Hết Hạn'),
                      content: const Text('Tài khoản của bạn đã được đăng xuất từ xa. Vui lòng đăng nhập lại.'),
                      actions: <Widget>[
                        TextButton(
                          child: const Text('OK'),
                          onPressed: () {
                            Navigator.of(dContext).pop();
                            NavigationService.navigateToAndRemoveUntil('/login');
                          },
                        ),
                      ],
                    );
                  },
                );
              } else {
                NavigationService.navigateToAndRemoveUntil('/login');
              }
            }
          } catch (e) {
            // ignore JSON parse errors from check
          }
        }
      }
    } catch (e) {
      // Ignore check errors - we don't want to block normal response flow
    }

    return response;
  }

  // Các phương thức tiện ích (GET, POST) that accept endpoint paths and JSON bodies.
  Future<http.Response> postJson(String endpoint, {Object? body}) {
    final uri = Uri.parse('$baseUrl/$endpoint');
    return post(uri, body: jsonEncode(body));
  }

  Future<http.Response> getJson(String endpoint) {
    final uri = Uri.parse('$baseUrl/$endpoint');
    return super.get(uri);
  }
}