import 'dart:convert';
import 'package:http/http.dart' as http;
import 'dart:io';
import 'dart:async';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:app_dllh/config/app_config.dart';
import 'navigation_service.dart';
import 'package:flutter/material.dart';

class ApiClient extends http.BaseClient {
  final http.Client _inner = http.Client();
  
  final String phpUrl = AppConfig.baseUrl;
  final String dotnetUrl = AppConfig.dotnetBaseUrl; 
  
  Uri _buildUri(String endpoint) {
    if (endpoint.startsWith("http")) return Uri.parse(endpoint);
    if (endpoint.startsWith("api/") || endpoint.startsWith("/api/")) {
      String base = dotnetUrl.endsWith("/") ? dotnetUrl : "$dotnetUrl/";
      String path = endpoint.startsWith("/") ? endpoint.substring(1) : endpoint;
      return Uri.parse("$base$path");
    } 
    String base = phpUrl.endsWith("/") ? phpUrl : "$phpUrl/";
    String path = endpoint.startsWith("/") ? endpoint.substring(1) : endpoint;
    return Uri.parse("$base$path");
  }

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final prefs = await SharedPreferences.getInstance();
    
    // Logic Token "Lai"
    String? token;
    bool isDotNetRequest = request.url.path.contains('/api/'); 

    if (isDotNetRequest) {
      token = prefs.getString('jwt_token');
    } else {
      token = prefs.getString('session_id');
    }

    if (token != null) {
      request.headers['Authorization'] = 'Bearer $token';
    }

    // --- SỬA LỖI QUAN TRỌNG ---
    // Chỉ set Content-Type là JSON nếu request KHÔNG PHẢI là Multipart (gửi file)
    // Nếu là MultipartRequest, http package sẽ tự động set boundary. Việc ghi đè sẽ làm hỏng request.
    if (request is! http.MultipartRequest) {
      // Chỉ set nếu chưa có hoặc cần đảm bảo charset
      request.headers['Content-Type'] = 'application/json; charset=utf-8';
    }
    
    request.headers['Accept'] = 'application/json';

    http.StreamedResponse response;
    try {
      response = await _inner.send(request).timeout(const Duration(seconds: AppConfig.requestTimeoutSeconds));
      // Log debug để kiểm tra status
      // print(">>> [Flutter Debug] ${request.method} ${request.url} -> Mã: ${response.statusCode}");
    } on SocketException catch (e) {
      await _clearTokens(prefs);
      _showErrorDialog('Không thể kết nối', 'Lỗi mạng: ${e.message}');
      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Network error"}))),
        503,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    } on TimeoutException {
      await _clearTokens(prefs);
      _showErrorDialog('Hết thời gian chờ', 'Yêu cầu tới máy chủ mất quá nhiều thời gian.');
      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Timeout"}))),
        504,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    if (response.statusCode == 401) {
      if (!request.url.path.contains('login')) {
          await _clearTokens(prefs);
          _showSessionExpiredDialog('Phiên đăng nhập đã hết hạn.');
      }
      return http.StreamedResponse(
        Stream.value(utf8.encode('{}')),
        401,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    if (!isDotNetRequest && token != null && !token.contains('.')) {
       await _checkPhpSessionStatus(token, prefs);
    }

    return response;
  }

  @override
  Future<http.Response> postJson(String endpoint, {Object? body}) async {
    final uri = _buildUri(endpoint);
    
    final request = http.Request('POST', uri);
    
    // --- SỬA LỖI: Sử dụng bodyBytes để đảm bảo encoding UTF-8 chính xác ---
    if (body != null) {
      final jsonString = jsonEncode(body);
      request.bodyBytes = utf8.encode(jsonString);
    }

    // Gửi request thông qua pipeline send()
    final streamedResponse = await send(request);
    
    return http.Response.fromStream(streamedResponse);
  }

  @override
  Future<http.Response> getJson(String endpoint) async {
    final uri = _buildUri(endpoint);
    final request = http.Request('GET', uri);
    final streamedResponse = await send(request);
    return http.Response.fromStream(streamedResponse);
  }

  // --- Helpers ---
  Future<void> _clearTokens(SharedPreferences prefs) async {
    await prefs.remove('session_id');
    await prefs.remove('jwt_token');
  }

  void _showErrorDialog(String title, String content) {
    final navigatorState = NavigationService.navigatorKey.currentState;
    if (navigatorState != null) {
      showDialog(
        context: navigatorState.context,
        barrierDismissible: true,
        builder: (ctx) => AlertDialog(
          title: Text(title),
          content: Text(content),
          actions: [TextButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('OK'))],
        ),
      );
    }
  }

  void _showSessionExpiredDialog(String message) {
    final navigatorState = NavigationService.navigatorKey.currentState;
    if (navigatorState != null) {
      showDialog(
        context: navigatorState.context,
        barrierDismissible: false,
        builder: (dContext) => AlertDialog(
          title: const Text('Phiên Đã Hết Hạn'),
          content: Text(message),
          actions: <Widget>[
            TextButton(
              child: const Text('OK'),
              onPressed: () {
                Navigator.of(dContext).pop(); 
                NavigationService.navigateToAndRemoveUntil('/login');
              },
            ),
          ],
        ),
      );
    } else {
      NavigationService.navigateToAndRemoveUntil('/login');
    }
  }

  Future<void> _checkPhpSessionStatus(String token, SharedPreferences prefs) async {
     try {
        final checkUri = Uri.parse('$phpUrl/Customer/CheckSession');
        final checkResp = await _inner.get(checkUri, headers: {'Authorization': 'Bearer $token'}).timeout(const Duration(seconds: 6));
        if (checkResp.statusCode == 200) {
          try {
            final map = jsonDecode(checkResp.body);
            if (map is Map && map['valid'] != true) {
              await _clearTokens(prefs);
              _showSessionExpiredDialog('Tài khoản đã đăng xuất từ xa.');
            }
          } catch (_) {}
        }
     } catch (_) {}
  }
}