import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:app_dllh/services/auth_service.dart'; // Sử dụng AuthService để gọi API

// Màu xanh chính được định nghĩa lại để file chạy độc lập
const Color primaryBlue = Color(0xFF007AFF);

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  _RegisterPageState createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  // Controllers cho các trường nhập liệu MỚI (đầy đủ thông tin)
  final TextEditingController _usernameController = TextEditingController(); // Tên đăng nhập Oracle (gửi API)
  final TextEditingController _passwordController = TextEditingController();
  final TextEditingController _rePasswordController = TextEditingController();

  final TextEditingController _fullNameController = TextEditingController(); // Họ và tên
  final TextEditingController _emailController = TextEditingController(); // Email cá nhân
  final TextEditingController _phoneController = TextEditingController(); // Số điện thoại
  final TextEditingController _addressController = TextEditingController(); // Địa chỉ

  // State quản lý
  String _message = "";
  bool _isLoading = false;
  // Tách biến trạng thái ẩn/hiện cho từng trường mật khẩu
  bool _isPasswordVisible = false;
  bool _isRePasswordVisible = false;

  // Khởi tạo AuthService instance
  final AuthService _authService = AuthService();

  // =========================================================
  // LOGIC ĐĂNG KÝ (Đã sửa để gọi AuthService và gửi đủ 6 trường)
  // =========================================================
  Future<void> _register() async {
    setState(() {
      _isLoading = true;
      _message = "";
    });

    final oracleUser = _usernameController.text.trim();
    final password = _passwordController.text.trim();
    final rePassword = _rePasswordController.text.trim();
    final fullName = _fullNameController.text.trim();
    final email = _emailController.text.trim();
    final phone = _phoneController.text.trim();
    final address = _addressController.text.trim(); // Địa chỉ là tùy chọn

    // 1. Kiểm tra rỗng (Kiểm tra tất cả các trường bắt buộc)
    if (oracleUser.isEmpty || password.isEmpty || rePassword.isEmpty || 
        fullName.isEmpty || 
        email.isEmpty || 
        phone.isEmpty) {
      setState(() {
        _isLoading = false;
        _message = "Vui lòng nhập đầy đủ các trường bắt buộc: Tên đăng nhập, Mật khẩu, Họ tên, Email, SĐT.";
      });
      _showSnackBar(_message, Colors.orange);
      return;
    }

    // 2. Kiểm tra mật khẩu khớp
    if (password != rePassword) {
      setState(() {
        _isLoading = false;
        _message = "Mật khẩu nhập lại không khớp.";
        _passwordController.clear();
        _rePasswordController.clear();
      });
      _showSnackBar(_message, Colors.orange);
      return;
    }
    
    // 3. Thực hiện gọi API đăng ký qua AuthService (Gửi đầy đủ 6 trường)
    try {
      final Map<String, dynamic> result = await _authService.register(
        username: oracleUser,
        password: password,
        hoTen: fullName,
        email: email,
        soDienThoai: phone,
        diaChi: address,
      );

      debugPrint("API Result: $result");

      setState(() {
        _isLoading = false;
      });
      
      if (result['success'] == true) {
        _showSnackBar("Tạo user Oracle thành công! Vui lòng đăng nhập.", Colors.green);
        // Điều hướng về màn hình đăng nhập
        Navigator.of(context).pop(); 
      } else {
        setState(() {
          _message = result['message'] ?? "Đăng ký thất bại không rõ nguyên nhân.";
        });
        _showSnackBar(_message, Colors.red);
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
        _message = "Lỗi kết nối hoặc xử lý: $e";
      });
      _showSnackBar(_message, Colors.red);
    }
  }

  void _showSnackBar(String message, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: color,
        duration: const Duration(seconds: 3),
      ),
    );
  }

  // Hàm điều hướng trở lại màn hình Đăng nhập
  void _navigateToLogin() {
    Navigator.of(context).pop();
  }

  // =========================================================
  // WIDGETS GIAO DIỆN MỚI (Giữ nguyên)
  // =========================================================

  // Widget riêng để xây dựng ô nhập liệu
  Widget _buildInputField({
    required String hintText,
    required TextEditingController controller,
    bool isPassword = false,
    TextInputType keyboardType = TextInputType.text,
    bool? isVisible, // Biến trạng thái ẩn hiện (chỉ dùng cho mật khẩu)
    VoidCallback? toggleVisibility, // Hàm xử lý khi nhấn icon
  }) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFF2F2F7), // Màu nền nhẹ cho input field
        borderRadius: BorderRadius.circular(10), // Bo góc
      ),
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: TextField(
        controller: controller,
        keyboardType: keyboardType,
        // Dùng biến isVisible được truyền vào để kiểm soát obscureText
        obscureText: isPassword && !(isVisible ?? false), 
        decoration: InputDecoration(
          hintText: hintText,
          border: InputBorder.none, 
          suffixIcon: isPassword 
              ? IconButton(
                  icon: Icon(
                    // Sử dụng isVisible
                    (isVisible ?? false) ? Icons.visibility_off_outlined : Icons.visibility_outlined, 
                    color: Colors.grey[600],
                  ),
                  onPressed: toggleVisibility, // Gọi hàm toggle được truyền vào
                )
              : null,
          contentPadding: const EdgeInsets.symmetric(vertical: 18),
          hintStyle: TextStyle(
            color: Colors.grey[600],
            fontSize: 16,
          ),
        ),
        style: const TextStyle(fontSize: 16, color: Colors.black87),
      ),
    );
  }

  // Widget riêng để xây dựng nút Đăng ký
  Widget _buildRegistrationButton() {
    return SizedBox(
      width: double.infinity, 
      height: 56, 
      child: ElevatedButton(
        onPressed: _isLoading ? null : _register, // Gọi hàm _register
        style: ElevatedButton.styleFrom(
          backgroundColor: primaryBlue,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          elevation: 0,
          textStyle: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.bold,
          ),
        ),
        child: _isLoading 
            ? const SizedBox(
                width: 24,
                height: 24,
                child: CircularProgressIndicator(
                  color: Colors.white,
                  strokeWidth: 3,
                ),
              )
            : const Text('Registration'),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final screenHeight = MediaQuery.of(context).size.height;
    
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 32.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              // Vùng chứa hình ảnh minh họa
              SizedBox(
                height: screenHeight * 0.20, // Giảm chiều cao thêm để chứa nhiều trường hơn
                child: Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.person_add_alt_1, size: 50, color: primaryBlue),
                      const SizedBox(height: 10),
                      const Text(
                        'Create Your Account',
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: primaryBlue),
                      ),
                    ],
                  ),
                ),
              ),

              const SizedBox(height: 16),

              // Tiêu đề "Create Account"
              const Text(
                'Create Account',
                style: TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.bold,
                  color: Colors.black87,
                ),
              ),

              const SizedBox(height: 32),

              // --- BẮT ĐẦU CÁC Ô NHẬP LIỆU ĐẦY ĐỦ ---

              // 1. Tên đăng nhập Oracle
              _buildInputField(
                hintText: 'Tên đăng nhập (Oracle User)',
                controller: _usernameController,
                keyboardType: TextInputType.text,
              ),

              const SizedBox(height: 16),
              
              // 2. Họ và tên
              _buildInputField(
                hintText: 'Họ và tên',
                controller: _fullNameController,
                keyboardType: TextInputType.name,
              ),

              const SizedBox(height: 16),

              // 3. Email cá nhân
              _buildInputField(
                hintText: 'Email cá nhân',
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
              ),

              const SizedBox(height: 16),

              // 4. Số điện thoại
              _buildInputField(
                hintText: 'Số điện thoại',
                controller: _phoneController,
                keyboardType: TextInputType.phone,
              ),

              const SizedBox(height: 16),

              // 5. Địa chỉ
              _buildInputField(
                hintText: 'Địa chỉ (Tùy chọn)',
                controller: _addressController,
                keyboardType: TextInputType.streetAddress,
              ),

              const SizedBox(height: 16),

              // 6. Mật khẩu
              _buildInputField(
                hintText: 'Mật khẩu',
                controller: _passwordController,
                isPassword: true,
                isVisible: _isPasswordVisible,
                toggleVisibility: () {
                  setState(() {
                    _isPasswordVisible = !_isPasswordVisible;
                  });
                },
              ),
              
              const SizedBox(height: 16),

              // 7. Xác nhận Mật khẩu
              _buildInputField(
                hintText: 'Xác nhận Mật khẩu',
                controller: _rePasswordController,
                isPassword: true,
                isVisible: _isRePasswordVisible,
                toggleVisibility: () {
                  setState(() {
                    _isRePasswordVisible = !_isRePasswordVisible;
                  });
                },
              ),
              // --- KẾT THÚC CÁC Ô NHẬP LIỆU ĐẦY ĐỦ ---

              const SizedBox(height: 32),

              // Nút Đăng ký (Registration Button)
              _buildRegistrationButton(),

              const SizedBox(height: 20),
              
              // Hiển thị thông báo lỗi (nếu có)
              if (_message.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16.0),
                  child: Text(
                    _message,
                    style: const TextStyle(color: Colors.red, fontSize: 14),
                    textAlign: TextAlign.center,
                  ),
                ),


              // Đã có tài khoản (Sign In)
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Text(
                    "already have an account?",
                    style: TextStyle(
                      color: Colors.black54,
                      fontSize: 16,
                    ),
                  ),
                  TextButton(
                    onPressed: _navigateToLogin, // Gọi hàm chuyển màn hình đăng nhập
                    child: const Text(
                      'Sign In',
                      style: TextStyle(
                        color: primaryBlue,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ),
                ],
              ),
              
              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }
}