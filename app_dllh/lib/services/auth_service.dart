import 'dart:convert';
import 'package:app_dllh/models/session.dart';
import 'package:app_dllh/services/api_client.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:device_info_plus/device_info_plus.dart';
import 'dart:io';

class AuthService {
  final ApiClient _apiClient = ApiClient();

  // Lấy thông tin thiết bị
  Future<String> _getDeviceInfo() async {
    DeviceInfoPlugin deviceInfo = DeviceInfoPlugin();
    try {
      if (Platform.isAndroid) {
        AndroidDeviceInfo androidInfo = await deviceInfo.androidInfo;
        return 'Android ${androidInfo.version.release} (${androidInfo.model})';
      } else if (Platform.isIOS) {
        IosDeviceInfo iosInfo = await deviceInfo.iosInfo;
        return 'iOS ${iosInfo.systemVersion} (${iosInfo.utsname.machine})';
      }
    } catch (e) {
      return 'Unknown Device';
    }
    return 'Unknown Platform';
  }

  // Lưu SESSION_ID vào SharedPreferences
  Future<void> saveSessionId(String sessionId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('session_id', sessionId);
  }

  // Lấy SESSION_ID từ SharedPreferences
  Future<String?> getSessionId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('session_id');
  }

  // Xóa SESSION_ID
  Future<void> deleteSessionId() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('session_id');
  }

  // Kiểm tra đã đăng nhập hay chưa
  Future<bool> isLoggedIn() async {
    final sessionId = await getSessionId();
    return sessionId != null;
  }

  /// Đăng nhập
  Future<Map<String, dynamic>> login(String username, String password) async {
    final deviceInfo = await _getDeviceInfo();
    
    final response = await _apiClient.postJson(
      "login.php", // Endpoint
      body: {
        "username": username,
        "password": password,
        "device_type": "MOBILE",
        "device_info": deviceInfo, // Gửi thông tin thiết bị
      },
    );

    print("login.php status: ${response.statusCode}");
    print("login.php body: ${response.body}");

    try {
      final data = jsonDecode(response.body);
      if (response.statusCode == 200 && data['success'] == true) {
        final sessionId = data['session_id'];
        if (sessionId != null) {
          await saveSessionId(sessionId);
        }
      }
      return data;
    } catch (e) {
      return {
        "success": false,
        "message": "Invalid JSON from server (HTTP ${response.statusCode}): ${response.body}"
      };
    }
  }

  /// Đăng xuất
  /// Returns a map with keys: success (bool) and optional message
  Future<Map<String, dynamic>> logout() async {
    Map<String, dynamic> result = {"success": false};
    try {
      // Gọi API để thông báo cho server về việc đăng xuất
      final response = await _apiClient.postJson("logout.php");
      try {
        final data = jsonDecode(response.body);
        if (data is Map<String, dynamic>) {
          result = data;
        } else {
          result = {"success": response.statusCode == 200};
        }
      } catch (e) {
        // Nếu server trả về body không phải JSON
        result = {"success": response.statusCode == 200, "message": response.body};
      }
    } catch (e) {
      // Ghi log và trả về lỗi, nhưng vẫn xóa token cục bộ
      print("Error calling logout API, but proceeding with local logout: $e");
      result = {"success": false, "message": e.toString()};
    } finally {
      // Luôn xóa token ở client
      await deleteSessionId();
    }

    return result;
  }

  Future<List<Session>> getActiveSessions() async {
    final response = await _apiClient.getJson("sessions/active");
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body) as List;
      return data.map((session) => Session.fromJson(session)).toList();
    } else {
      throw Exception('Failed to load active sessions');
    }
  }

  Future<void> logoutRemote(String sessionId) async {
    // Try the standard remote logout endpoint first. If it fails (non-200),
    // try to follow any redirect location and finally call the force endpoint.
    final response = await _apiClient.postJson(
      "sessions/logout-remote",
      body: {"session_id_to_logout": sessionId},
    );
    if (response.statusCode == 200) return;

    // If server returned a redirect (301/302) with a Location header, follow it
    // using a raw http POST so we preserve method and body.
    if ((response.statusCode == 301 || response.statusCode == 302) && response.headers['location'] != null) {
      final loc = response.headers['location']!;
      try {
        final sessionIdLocal = await getSessionId();
        // Use package:http directly to post to absolute location
        final rawResp = await http.post(
          Uri.parse(loc),
          headers: {
            'Content-Type': 'application/json; charset=utf-8',
            if (sessionIdLocal != null) 'Authorization': 'Bearer $sessionIdLocal',
          },
          body: jsonEncode({"session_id_to_logout": sessionId}),
        );
        if (rawResp.statusCode == 200) return;
        // Otherwise, continue to fallback behavior below
      } catch (_) {
        // ignore and continue to fallback
      }
    }

    // fallback: try the force endpoint under the KLTN path
    final fallback = await _apiClient.postJson(
      "sessions/logout-remote-force",
      body: {"session_id_to_logout": sessionId},
    );
    if (fallback.statusCode != 200) {
      String body = fallback.body;
      String msg = 'Failed to logout session (fallback)';
      try {
        final parsed = jsonDecode(body);
        if (parsed is Map && parsed['message'] != null) msg = parsed['message'];
      } catch (_) {
        // not JSON
        if (body.trim().isNotEmpty) msg = body.trim();
      }
      throw Exception('$msg (status ${fallback.statusCode})');
    }
  }

  Future<bool> checkSession() async {
    final response = await _apiClient.getJson("Customer/CheckSession");
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      return data['valid'] == true;
    }
    return false;
  }

  // Các hàm khác giữ nguyên nhưng dùng _apiClient
  Future<Map<String, dynamic>> getCustomerId(String username) async {
    final response = await _apiClient.postJson(
      "get_customer_id.php",
      body: {"username": username},
    );
    print("get_customer_id.php status: ${response.statusCode}");
    print("get_customer_id.php body: ${response.body}");
    return jsonDecode(response.body);
  }

  Future<Map<String, dynamic>> register({
    required String username,
    required String password,
    required String hoTen,
    required String email,
    String? soDienThoai,
    String? diaChi,
  }) async {
     final response = await _apiClient.postJson('register.php',
      body: {
        "username": username,
        "password": password,
        "hoTen": hoTen,
        "email": email,
        "soDienThoai": soDienThoai ?? '',
        "diaChi": diaChi ?? ''
      });
      return jsonDecode(response.body);
  }
  
  Future<Map<String, dynamic>> getUser(String userID) async {
    final response = await _apiClient.postJson(
      "get_user.php",
      body: { "userID": userID },
    );
    return jsonDecode(response.body);
  }
}
