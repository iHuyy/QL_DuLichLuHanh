import 'dart:convert';
import 'package:app_dllh/models/session.dart';
import 'package:app_dllh/services/api_client.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:device_info_plus/device_info_plus.dart';
import 'dart:io';

class AuthService {
  final ApiClient _apiClient = ApiClient();

  // (Hàm _getDeviceInfo không đổi)
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

  // (SỬA ĐỔI) Lưu cả 2 token: C# (JWT) và PHP (Session)
  Future<void> saveSessionData(
    String sessionId,
    String jwtToken,
    String userID,
    String role,
  ) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('session_id', sessionId); // Token PHP
    await prefs.setString('jwt_token', jwtToken); // Token C#
    await prefs.setString('user_id', userID);
    await prefs.setString('user_role', role);
  }

  // (Giữ nguyên) Lấy SESSION_ID (của PHP)
  Future<String?> getSessionId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('session_id');
  }

  // (SỬA ĐỔI) Xóa toàn bộ dữ liệu phiên
  Future<void> deleteSessionData() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('session_id');
    await prefs.remove('jwt_token'); // Xóa cả JWT
    await prefs.remove('user_id');
    await prefs.remove('user_role');
  }

  // (Giữ nguyên) Kiểm tra đã đăng nhập hay chưa
  Future<bool> isLoggedIn() async {
    final sessionId = await getSessionId();
    return sessionId != null;
  }

  // (HÀM MỚI) Lấy dữ liệu phiên đã lưu khi khởi động app
  Future<Map<String, dynamic>> getSavedSessionData() async {
    final prefs = await SharedPreferences.getInstance();
    final sessionId = prefs.getString('session_id');

    if (sessionId == null) {
      return {'isLoggedIn': false};
    }

    return {
      'isLoggedIn': true,
      'userID': prefs.getString('user_id') ?? 'UNKNOWN_ID',
      'role': prefs.getString('user_role') ?? 'DEFAULT',
      // Thêm userData nếu bạn đã lưu nó (ví dụ: fullname)
      'userData': {'username': prefs.getString('user_id') ?? 'UNKNOWN_ID'},
    };
  }

  /// Đăng nhập (*** ĐÃ CẬP NHẬT ĐỂ GỌI CẢ 2 BACKEND ***)
  Future<Map<String, dynamic>> login(String username, String password) async {
    final deviceInfo = await _getDeviceInfo();

    // ----- 1. Đăng nhập vào PHP (Lấy PHP Session ID) -----
    http.Response phpResponse;
    Map<String, dynamic> phpData;
    try {
      phpResponse = await _apiClient.postJson(
        "login.php", // Endpoint PHP
        body: {
          "username": username,
          "password": password,
          "device_type": "MOBILE",
          "device_info": deviceInfo,
        },
      );
      print("login.php status: ${phpResponse.statusCode}");
      print("login.php body: ${phpResponse.body}");
      phpData = jsonDecode(phpResponse.body);
      if (phpResponse.statusCode != 200 || phpData['success'] != true) {
        return phpData; // Trả về lỗi từ PHP
      }
    } catch (e) {
      return {
        "success": false,
        "message": "Lỗi đăng nhập PHP: ${e.toString()}",
      };
    }

    // ----- 2. Đăng nhập vào C# (Lấy JWT) -----
    http.Response csResponse;
    Map<String, dynamic> csData = {}; // Khởi tạo rỗng để tránh null
    
    try {
      csResponse = await _apiClient.postJson(
        "api/ApiAuthController/login", 
        body: {
          "username": username,
          "password": password,
        },
      );
      
      print("ApiAuthController/login status: ${csResponse.statusCode}");
      
      // *** SỬA LỖI: Kiểm tra kỹ trước khi decode để tránh FormatException ***
      if (csResponse.statusCode == 200 && csResponse.body.isNotEmpty) {
        try {
          csData = jsonDecode(csResponse.body);
        } catch (e) {
          print("Lỗi parse JSON C#: $e");
        }
      } else {
        print("C# Server Error Body: ${csResponse.body}");
      }
    } catch (e) {
      print("Lỗi kết nối C#: ${e.toString()}");
      // Không return ở đây, để code chạy tiếp xuống phần lưu token PHP
    }

    // ----- 3. Lưu token (Logic linh hoạt hơn) -----
    try {
      final phpSessionId = phpData['session_id'];
      
      // Token C# có thể bị null nếu bước 2 lỗi
      final jwtToken = csData['token']; 
      
      // Ưu tiên lấy ID từ PHP vì nó là hệ thống chính
      final userID = phpData['userID']?.toString() ?? csData['userId']?.toString() ?? username;
      final role = phpData['role']?.toString() ?? csData['role']?.toString() ?? 'DEFAULT';

      // LOGIC QUYẾT ĐỊNH:
      // Nếu có PHP session -> Cho phép đăng nhập (Web app hoạt động)
      // Nếu có thêm C# token -> Cho phép dùng tính năng QR
      
      if (phpSessionId != null) {
        // Nếu không có token C#, ta lưu chuỗi rỗng hoặc null để xử lý sau
        await saveSessionData(phpSessionId, jwtToken ?? "", userID, role);
        
        if (jwtToken == null) {
            // Trả về cảnh báo nhưng vẫn cho vào
            phpData['message'] = "Đăng nhập thành công (Lưu ý: Tính năng QR có thể không hoạt động do lỗi kết nối Server phụ).";
        }
        return phpData; 
      } else {
         throw Exception("Không thể tạo phiên đăng nhập (Thiếu PHP Session).");
      }

    } catch (e) {
      return {
        "success": false,
        "message": "Lỗi xử lý dữ liệu phiên: ${e.toString()}"
      };
    }
  }

  /// Đăng xuất
  Future<Map<String, dynamic>> logout() async {
    Map<String, dynamic> result = {"success": false};
    try {
      // Gọi API PHP (ApiClient sẽ tự gửi 'session_id')
      final response = await _apiClient.postJson("logout.php");
      try {
        final data = jsonDecode(response.body);
        if (data is Map<String, dynamic>) {
          result = data;
        } else {
          result = {"success": response.statusCode == 200};
        }
      } catch (e) {
        result = {
          "success": response.statusCode == 200,
          "message": response.body,
        };
      }
    } catch (e) {
      print("Error calling logout API, but proceeding with local logout: $e");
      result = {"success": false, "message": e.toString()};
    } finally {
      // (SỬA ĐỔI) Luôn xóa token ở client
      await deleteSessionData();
    }

    return result;
  }

  // (Các hàm còn lại giữ nguyên)

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
    final response = await _apiClient.postJson(
      "sessions/logout-remote",
      body: {"session_id_to_logout": sessionId},
    );
    if (response.statusCode == 200) return;

    if ((response.statusCode == 301 || response.statusCode == 302) &&
        response.headers['location'] != null) {
      final loc = response.headers['location']!;
      try {
        final sessionIdLocal = await getSessionId();
        final rawResp = await http.post(
          Uri.parse(loc),
          headers: {
            'Content-Type': 'application/json; charset=utf-8',
            if (sessionIdLocal != null)
              'Authorization': 'Bearer $sessionIdLocal',
          },
          body: jsonEncode({"session_id_to_logout": sessionId}),
        );
        if (rawResp.statusCode == 200) return;
      } catch (_) {
        // ignore
      }
    }

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
    final response = await _apiClient.postJson(
      'register.php',
      body: {
        "username": username,
        "password": password,
        "hoTen": hoTen,
        "email": email,
        "soDienThoai": soDienThoai ?? '',
        "diaChi": diaChi ?? '',
      },
    );
    return jsonDecode(response.body);
  }

  Future<Map<String, dynamic>> getUser(String userID) async {
    final response = await _apiClient.postJson(
      "get_user.php",
      body: {"userID": userID},
    );
    return jsonDecode(response.body);
  }

  Future<List<dynamic>> getInvoices() async {
    final response = await _apiClient.getJson('get_user_invoices.php');
    final body = response.body;
    String candidate = body.trim();
    if (candidate.isEmpty) throw Exception('Empty response from server');

    if (candidate.startsWith('<')) {
      final idxObj = candidate.indexOf('{');
      final idxArr = candidate.indexOf('[');
      int idx = -1;
      if (idxObj >= 0 && idxArr >= 0)
        idx = idxObj < idxArr ? idxObj : idxArr;
      else if (idxObj >= 0)
        idx = idxObj;
      else if (idxArr >= 0)
        idx = idxArr;
      if (idx >= 0) candidate = candidate.substring(idx);
    }

    try {
      final decoded = jsonDecode(candidate);
      if (decoded is Map) {
        if (decoded.containsKey('invoices') && decoded['invoices'] is List) {
          return decoded['invoices'];
        }
        if (decoded.containsKey('data') && decoded['data'] is List)
          return decoded['data'];
        return [decoded];
      } else if (decoded is List) {
        return decoded;
      }
    } catch (e) {
      throw Exception(
        'Invalid JSON from invoices endpoint: $e\nBody: ${body.length > 500 ? body.substring(0, 500) : body}',
      );
    }

    throw Exception('Failed to fetch invoices (status ${response.statusCode})');
  }

  // (Thêm hàm này, ApiClient sẽ tự gửi 'jwt_token')
  Future<bool> approveQrLogin(String qrToken) async {
    try {
      // ApiClient sẽ thấy '/api/' và gửi 'jwt_token'
      final response = await _apiClient.postJson(
        'api/QrLogin/approve-qr-login',
        body: {'qrToken': qrToken},
      );

      return response.statusCode == 200;
    } catch (e) {
      print('Error approving QR login: $e');
      return false;
    }
  }
}
