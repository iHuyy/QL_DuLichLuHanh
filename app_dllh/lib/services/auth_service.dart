import 'dart:convert';
import 'package:http/http.dart' as http;

class AuthService {
  // 🔧 Địa chỉ API PHP trên máy ảo / server Oracle Linux
  final String baseUrl = "http://10.0.2.2/KLTN";

  /// Đăng nhập bằng tài khoản Oracle có quyền SYSDBA
  Future<Map<String, dynamic>> login(String username, String password) async { 
    final response = await http.post(
      Uri.parse("$baseUrl/login.php"),
      headers: {"Content-Type": "application/json"},
      body: jsonEncode({
        "username": username,
        "password": password,
        // Role không còn được gửi từ client, server sẽ tự quyết định
      }),
    );

    // Log thô để debug (console của Flutter)
    print("login.php status: ${response.statusCode}");
    print("login.php body: ${response.body}");

    // Nếu server trả không phải JSON, bắt và trả lỗi rõ ràng
    try {
      final decoded = jsonDecode(response.body);
      return decoded as Map<String, dynamic>;
    } catch (e) {
      // Trả thông tin body giúp biết nguyên nhân (HTML error page / PHP warning...)
      return {
        "success": false,
        "message": "Invalid JSON from server (HTTP ${response.statusCode}): ${response.body}"
      };
    }
  }

  // New: Lấy thông tin user theo userID
  Future<Map<String, dynamic>> getUser(String userID, {String? dbUser, String? dbPass, String role = 'DEFAULT'}) async {
    final body = {
      "userID": userID,
      if (dbUser != null) "dbUser": dbUser,
      if (dbPass != null) "dbPass": dbPass,
      "role": role,
    };

    final response = await http.post(
      Uri.parse("$baseUrl/get_user.php"),
      headers: {"Content-Type": "application/json"},
      body: jsonEncode(body),
    );

    print("get_user.php status: ${response.statusCode}");
    print("get_user.php body: ${response.body}");

    try {
      final decoded = jsonDecode(response.body);
      return decoded as Map<String, dynamic>;
    } catch (e) {
      return {
        "success": false,
        "message": "Invalid JSON from server (HTTP ${response.statusCode}): ${response.body}"
      };
    }
  }

  /// Cập nhật: Tạo user Oracle mới (tự động gán ROLE_CUSTOMER & PROFILE_CUS)
  Future<Map<String, dynamic>> register({
    required String username,
    required String password,
    required String hoTen,
    required String email,
    String? soDienThoai,
    String? diaChi,
  }) async {
    final url = Uri.parse('$baseUrl/register.php');

    final payload = {
      "username": username,
      "password": password,
      "hoTen": hoTen,
      "email": email,
      "soDienThoai": soDienThoai ?? '',
      "diaChi": diaChi ?? ''
    };

    try {
      final res = await http.post(url,
          headers: {'Content-Type': 'application/json; charset=utf-8'},
          body: jsonEncode(payload));

      if (res.statusCode == 200) {
        final Map<String, dynamic> body = jsonDecode(res.body);
        return body;
      } else {
        return {
          "success": false,
          "message": "Lỗi kết nối (${res.statusCode}). Chi tiết: ${res.body}"
        };
      }
    } catch (e) {
      return {"success": false, "message": "Lỗi mạng hoặc ngoại lệ: $e"};
    }
  }

  /// Đăng xuất khỏi session SYSDBA
  Future<Map<String, dynamic>> logout() async {
    final response = await http.get(Uri.parse("$baseUrl/logout.php"));
    if (response.statusCode == 200) {
      return jsonDecode(response.body);
    } else {
      return {
        "success": false,
        "message": "HTTP ${response.statusCode}: ${response.reasonPhrase}"
      };
    }
  }
}