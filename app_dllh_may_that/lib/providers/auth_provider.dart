import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class AuthProvider extends ChangeNotifier {
  bool _isLoading = false;
  
  bool get isLoading => _isLoading;

  Future<void> signInWithEmailPassword({
    required String username,
    required String password,
  }) async {
    _isLoading = true;
    notifyListeners();

    const String apiUrl = 'http://192.168.0.178:3000/api/login';

    try {
      final response = await http.post(
        Uri.parse(apiUrl),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'tenDN': username,
          'mk': password,
        }),
      );

      final responseData = jsonDecode(response.body);

      if (response.statusCode == 200 && responseData['success'] == true) {
        // Handle successful login
        return;
      } else {
        throw Exception(responseData['message'] ?? 'Lỗi đăng nhập không xác định.');
      }
    } catch (e) {
      throw Exception('Lỗi kết nối hoặc server: $e');
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
  
  Future<void> signUpWithEmailPassword({
    required String email,
    required String password,
  }) async {
    _isLoading = true;
    notifyListeners();

    const String apiUrl = 'http://192.168.0.178:3000/api/signup';

    try {
      final response = await http.post(
        Uri.parse(apiUrl),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'email': email,
          'password': password,
        }),
      );

      final responseData = jsonDecode(response.body);

      if (response.statusCode == 201 && responseData['success'] == true) {
        // Handle successful signup
        return;
      } else {
        throw Exception(responseData['message'] ?? 'Đăng ký không thành công.');
      }
    } catch (e) {
      throw Exception('Lỗi kết nối hoặc server: $e');
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> signInWithGoogle() async {}
  Future<void> signInWithFacebook() async {}
}