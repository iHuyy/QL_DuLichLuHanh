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
  
  // Lấy cấu hình URL từ AppConfig
  final String phpUrl = AppConfig.baseUrl;
  
  // Đảm bảo bạn đã thêm biến này vào AppConfig (ví dụ: "http://192.168.1.14:5000")
  final String dotnetUrl = AppConfig.dotnetBaseUrl; 
  
  /// Hàm helper để xác định và xây dựng URL đúng server
  Uri _buildUri(String endpoint) {
    // Nếu đã là full URL thì dùng luôn
    if (endpoint.startsWith("http")) return Uri.parse(endpoint);

    // LOGIC ROUTING:
    // Nếu endpoint bắt đầu bằng "api/" hoặc "/api/", đây là request tới server .NET Core
    if (endpoint.startsWith("api/") || endpoint.startsWith("/api/")) {
      String base = dotnetUrl.endsWith("/") ? dotnetUrl : "$dotnetUrl/";
      String path = endpoint.startsWith("/") ? endpoint.substring(1) : endpoint;
      return Uri.parse("$base$path");
    } 
    
    // Mặc định: Các endpoint còn lại (login.php, get_tours.php...) gửi về PHP
    String base = phpUrl.endsWith("/") ? phpUrl : "$phpUrl/";
    String path = endpoint.startsWith("/") ? endpoint.substring(1) : endpoint;
    return Uri.parse("$base$path");
  }

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final prefs = await SharedPreferences.getInstance();
    
    // *** BẮT ĐẦU SỬA LỖI (Logic Token "Lai") ***
    String? token;
    
    // Kiểm tra xem request này đang gửi tới đâu dựa trên đường dẫn
    // Nếu path chứa "/api/", ta coi đó là request tới .NET
    bool isDotNetRequest = request.url.path.contains('/api/'); 

    // 1. Chọn token phù hợp
    if (isDotNetRequest) {
      token = prefs.getString('jwt_token'); // Gửi JWT cho C#
    } else {
      token = prefs.getString('session_id'); // Gửi Session ID cho PHP
    }

    // 2. Thêm token vào header nếu có
    if (token != null) {
      request.headers['Authorization'] = 'Bearer $token';
    }
    // *** KẾT THÚC SỬA LỖI ***

    request.headers['Content-Type'] = 'application/json; charset=utf-8';

    http.StreamedResponse response;
    try {
      response = await _inner.send(request).timeout(Duration(seconds: AppConfig.requestTimeoutSeconds));
      print(">>> [Flutter Debug] Server trả về Mã: ${response.statusCode}");
      if (response.statusCode == 307 || response.statusCode == 302 || response.statusCode == 301) {
         print(">>> [Flutter Debug] Server đòi chuyển hướng tới: ${response.headers['location']}");
      }
      if (response.statusCode == 405) {
         print(">>> [Flutter Debug] Server từ chối Method POST. Headers: ${response.headers}");
      }
    } on SocketException catch (e) {
      // Xử lý mất mạng
      await _clearTokens(prefs);
      _showErrorDialog('Không thể kết nối', 'Không thể kết nối tới máy chủ: ${e.message}. Vui lòng kiểm tra kết nối hoặc cấu hình server.');

      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Network error: ${e.message}"}))),
        503,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    } on TimeoutException {
      // Xử lý timeout
      await _clearTokens(prefs);
      _showErrorDialog('Hết thời gian chờ', 'Yêu cầu tới máy chủ mất quá nhiều thời gian. Vui lòng thử lại.');

      return http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode({"success": false, "message": "Timeout"}))),
        504,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    // === XỬ LÝ 401 UNAUTHORIZED ===
    if (response.statusCode == 401) {
      await _clearTokens(prefs);
      _showSessionExpiredDialog('Tài khoản của bạn đã được đăng nhập trên một thiết bị khác hoặc phiên đã hết hạn. Vui lòng đăng nhập lại.');
      
      return http.StreamedResponse(
        Stream.value(utf8.encode('{}')),
        401,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    // Logic kiểm tra session PHP (Chỉ chạy nếu là request PHP và có token)
    if (!isDotNetRequest && token != null && !token.contains('.')) {
       await _checkPhpSessionStatus(token, prefs);
    }

    return response;
  }

  // --- Các hàm Helper riêng ---

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
          actions: [
            TextButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('OK')),
          ],
        ),
      );
    }
  }

  void _showSessionExpiredDialog(String message) {
    final navigatorState = NavigationService.navigatorKey.currentState;
    final dialogContext = NavigationService.currentContext;

    if (navigatorState != null && dialogContext != null) {
      showDialog(
        context: navigatorState.context,
        barrierDismissible: false,
        builder: (BuildContext dContext) {
          return AlertDialog(
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
          );
        },
      );
    } else {
      NavigationService.navigateToAndRemoveUntil('/login');
    }
  }

  // Kiểm tra session PHP còn sống không (Logic cũ giữ nguyên)
  Future<void> _checkPhpSessionStatus(String token, SharedPreferences prefs) async {
     try {
        final checkUri = Uri.parse('$phpUrl/Customer/CheckSession');
        final checkResp = await _inner
            .get(checkUri, headers: {'Authorization': 'Bearer $token'})
            .timeout(const Duration(seconds: 6));
            
        if (checkResp.statusCode == 200) {
          try {
            final map = jsonDecode(checkResp.body);
            final valid = map is Map && map['valid'] == true;
            if (!valid) {
              await _clearTokens(prefs);
              _showSessionExpiredDialog('Tài khoản của bạn đã được đăng xuất từ xa. Vui lòng đăng nhập lại.');
            }
          } catch (e) {
            // JSON parse error, ignore
          }
        }
     } catch (e) {
        // Network error during background check, ignore
     }
  }

  // --- Override post/get để dùng _buildUri ---

  @override
  Future<http.Response> postJson(String endpoint, {Object? body}) {
    final uri = _buildUri(endpoint); // Tự động chọn server
    return post(uri, body: jsonEncode(body));
  }

  @override
  Future<http.Response> getJson(String endpoint) {
    final uri = _buildUri(endpoint); // Tự động chọn server
    return get(uri);
  }
}