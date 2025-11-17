import 'package:app_dllh/services/auth_service.dart';
import 'package:flutter/material.dart';
import 'home_page.dart';
import 'qr_login_scanner_page.dart';
import 'register_page.dart';

// Định nghĩa màu xanh chính được sử dụng trong giao diện.
const Color primaryBlue = Color(0xFF007AFF);

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});
  
  @override
  _LoginPageState createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  // Controllers cho form
  final TextEditingController _usernameController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  final AuthService _authService = AuthService();

  // State quản lý trạng thái tải và thông báo lỗi
  bool _isLoading = false;
  String _message = "";
  bool _isPasswordVisible = false; // Biến để quản lý hiển thị mật khẩu

  // Mặc định role là DEFAULT
  final String _selectedRole = 'DEFAULT';

  // =========================================================
  // LOGIC CHỨC NĂNG
  // =========================================================
  
  Future<void> _login() async {
    setState(() {
      _isLoading = true;
      _message = "";
    });

    var username = _usernameController.text.trim();
    final password = _passwordController.text.trim();

    // Uppercase username để tương thích với database
    username = username.toUpperCase();

    if (username.isEmpty || password.isEmpty) {
      setState(() {
        _message = "Vui lòng nhập đầy đủ thông tin";
        _isLoading = false;
      });
      return;
    }

    try {
      final result = await _authService.login(username, password);
      
      setState(() => _isLoading = false);

      if (result['success'] == true) {
        // Đăng nhập thành công, điều hướng tới HomePage
        // Sử dụng userID numeric trả về từ server nếu có
        final returnedId = result['userID']?.toString() ?? username;
        final returnedRole = result['role']?.toString() ?? 'DEFAULT';
        Navigator.of(context).pushReplacement(
          MaterialPageRoute(
            builder: (_) => HomePage(
              userID: returnedId,
              role: returnedRole,
            ),
          ),
        );
      } else {
        setState(() {
          _message = result['message'] ?? 'Đăng nhập thất bại';
        });
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
        _message = "Lỗi kết nối hoặc xử lý: $e";
      });
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

  // Hàm điều hướng đến màn hình Đăng ký
  void _navigateToSignUp() {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (context) => RegisterPage(), // Sử dụng RegisterPage đã có
      ),
    );
  }

  // Hàm mô phỏng điều hướng đến màn hình Đăng nhập bằng QR
  void _navigateToQRLogin() {
    // Trong môi trường thực tế, bạn sẽ Navigator.push đến một trang Scanner QR
     // Navigate to QRLoginScannerPage
     Navigator.of(context).push(
       MaterialPageRoute(
         builder: (context) => const QRLoginScannerPage(),
       ),
     );
  }

  // =========================================================
  // WIDGETS GIAO DIỆN
  // =========================================================

  // Widget riêng để xây dựng ô nhập liệu
  Widget _buildInputField({
    required String hintText,
    required TextEditingController controller,
    bool isPassword = false,
    TextInputType keyboardType = TextInputType.text,
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
         obscureText: isPassword ? !_isPasswordVisible : false, // Ẩn văn bản nếu là mật khẩu và chưa được toggle
        decoration: InputDecoration(
          hintText: hintText,
          border: InputBorder.none, 
          // Icon ở bên phải (chỉ hiện thị cho trường mật khẩu)
          suffixIcon: isPassword 
              ? IconButton(
                  icon: Icon(
                    _isPasswordVisible ? Icons.visibility_off_outlined : Icons.visibility_outlined, 
                    color: Colors.grey[600],
                  ),
                  onPressed: () {
                    setState(() {
                      _isPasswordVisible = !_isPasswordVisible; // Toggle hiển thị mật khẩu
                    });
                  },
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

  // Widget riêng để xây dựng nút Đăng nhập
  Widget _buildSignInButton() {
    return SizedBox(
      width: double.infinity, 
      height: 56, 
      child: ElevatedButton(
        onPressed: _isLoading ? null : _login, // Gọi hàm _login
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
            : const Text('Sign In'),
      ),
    );
  }
  
  // Widget mới: Nút Đăng nhập bằng QR
  Widget _buildQRLoginButton() {
    return SizedBox(
      width: double.infinity, 
      height: 56, 
      child: OutlinedButton.icon(
        onPressed: _navigateToQRLogin,
        icon: const Icon(Icons.qr_code_scanner, color: primaryBlue),
        label: const Text(
          'Đăng nhập bằng QR', 
          style: TextStyle(
            fontSize: 18, 
            fontWeight: FontWeight.bold, 
            color: primaryBlue
          )
        ),
        style: OutlinedButton.styleFrom(
          side: const BorderSide(color: primaryBlue, width: 2),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final screenHeight = MediaQuery.of(context).size.height;
    
    return Scaffold(
      backgroundColor: Colors.white,
      // Bỏ FloatingActionButton
      // floatingActionButton: _buildQRButton(), 
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 32.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              // Vùng chứa hình ảnh minh họa
              SizedBox(
                height: screenHeight * 0.35,
                child: Center(
                  child: Container(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.flight_takeoff, size: 80, color: primaryBlue), // Icon minh họa
                        const SizedBox(height: 10),
                        const Text(
                          'Tour & Travel App',
                          textAlign: TextAlign.center,
                          style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: primaryBlue),
                        ),
                      ],
                    ),
                  ),
                ),
              ),

              const SizedBox(height: 16),

              // Tiêu đề "Sign In"
              const Text(
                'Sign In',
                style: TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.bold,
                  color: Colors.black87,
                ),
              ),

              const SizedBox(height: 32),

              // Ô nhập liệu Tên đăng nhập
              _buildInputField(
                hintText: 'Tên đăng nhập (Username)',
                controller: _usernameController,
                keyboardType: TextInputType.text,
              ),

              const SizedBox(height: 16),

              // Ô nhập liệu Mật khẩu
              _buildInputField(
                hintText: 'Mật khẩu',
                controller: _passwordController,
                isPassword: true,
              ),

              const SizedBox(height: 32),

              // 1. Nút Đăng nhập (Sign In Button)
              _buildSignInButton(),

              const SizedBox(height: 16), // Khoảng cách giữa Sign In và QR

              // 2. Nút Đăng nhập bằng QR (Vị trí mới)
              _buildQRLoginButton(),

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

              // 3. Quên mật khẩu (Vị trí mới: sau QR)
              TextButton(
                onPressed: () {
                  _showSnackBar("Tính năng Quên mật khẩu chưa khả dụng", Colors.blueGrey);
                },
                child: const Text(
                  'Forget Password?',
                  style: TextStyle(
                    color: Colors.black54,
                    fontSize: 16,
                  ),
                ),
              ),

              // Dãn cách để bố cục cân đối
              SizedBox(height: screenHeight * 0.05),

              // Đăng ký (Sign Up)
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Text(
                    "Don't have an Account?",
                    style: TextStyle(
                      color: Colors.black54,
                      fontSize: 16,
                    ),
                  ),
                  TextButton(
                    onPressed: _navigateToSignUp, // Gọi hàm chuyển màn hình đăng ký
                    child: const Text(
                      'Sign Up',
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

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    super.dispose();
  }
}