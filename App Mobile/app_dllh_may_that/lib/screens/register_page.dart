import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';

const Color primaryBlue = Color(0xFF007AFF);

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  _RegisterPageState createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final TextEditingController _usernameController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  final TextEditingController _rePasswordController = TextEditingController();
  final TextEditingController _fullNameController = TextEditingController();
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _phoneController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();

  String _message = "";
  bool _isLoading = false;
  bool _isPasswordVisible = false;
  bool _isRePasswordVisible = false;

  final AuthService _authService = AuthService();
  String? _serverOtpHash;
  int? _serverOtpExpiry;

  Future<void> _handleRegisterButton() async {
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

    if (oracleUser.isEmpty || password.isEmpty || rePassword.isEmpty ||
        fullName.isEmpty || email.isEmpty || phone.isEmpty) {
      _showError("Vui lòng nhập đầy đủ các trường bắt buộc.");
      return;
    }

    // *** Validate Số điện thoại ***
    if (!RegExp(r'^0\d{9}$').hasMatch(phone)) {
      _showError("Số điện thoại không hợp lệ (Phải là 10 số và bắt đầu bằng 0).");
      return;
    }

    if (password.length < 8 ||
        !password.contains(RegExp(r'[A-Z]')) ||
        !password.contains(RegExp(r'[0-9]')) ||
        !password.contains(RegExp(r'[!@#$%^&*(),.?":{}|<>]'))) {
      _showError("Mật khẩu không đủ mạnh (Cần 8 ký tự, Hoa, Số, Ký tự đặc biệt).");
      return;
    }

    if (password != rePassword) {
      _showError("Mật khẩu nhập lại không khớp.");
      return;
    }

    try {
      final result = await _authService.sendRegisterOtp(email, oracleUser);
      setState(() => _isLoading = false);

      if (result['success'] == true) {
        _serverOtpHash = result['otp_hash'];
        _serverOtpExpiry = result['otp_expiry'];
        if (result['debug_otp'] != null) print("OTP DEBUG: ${result['debug_otp']}");
        if (mounted) _showOtpInputDialog();
      } else {
        _showError(result['message'] ?? "Không thể gửi mã xác thực.");
      }
    } catch (e) {
      _showError("Lỗi kết nối: $e");
    }
  }

  void _showOtpInputDialog() {
    final otpController = TextEditingController();
    bool isVerifying = false;

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) {
        return StatefulBuilder(
          builder: (context, setStateDialog) {
            return AlertDialog(
              title: const Text("Xác thực Email"),
              content: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text("Mã OTP đã được gửi đến:\n${_emailController.text}"),
                  const SizedBox(height: 20),
                  TextField(
                    controller: otpController,
                    keyboardType: TextInputType.number,
                    textAlign: TextAlign.center,
                    maxLength: 6,
                    style: const TextStyle(fontSize: 24, letterSpacing: 8, fontWeight: FontWeight.bold),
                    decoration: const InputDecoration(
                      hintText: "######",
                      border: OutlineInputBorder(),
                      counterText: "",
                    ),
                  ),
                  if (isVerifying)
                    const Padding(padding: EdgeInsets.only(top: 16), child: CircularProgressIndicator()),
                ],
              ),
              actions: [
                TextButton(
                  onPressed: isVerifying ? null : () => Navigator.pop(ctx),
                  child: const Text("Hủy"),
                ),
                ElevatedButton(
                  onPressed: isVerifying ? null : () async {
                    final otp = otpController.text.trim();
                    if (otp.length < 6) return;
                    setStateDialog(() => isVerifying = true);
                    await _submitFinalRegistration(otp, ctx);
                    if (mounted) setStateDialog(() => isVerifying = false);
                  },
                  child: const Text("Xác nhận"),
                ),
              ],
            );
          },
        );
      },
    );
  }

  Future<void> _submitFinalRegistration(String otp, BuildContext dialogContext) async {
    try {
      final result = await _authService.register(
        username: _usernameController.text.trim(),
        password: _passwordController.text.trim(),
        hoTen: _fullNameController.text.trim(),
        email: _emailController.text.trim(),
        soDienThoai: _phoneController.text.trim(),
        diaChi: _addressController.text.trim(),
        otp: otp,
        otpHash: _serverOtpHash!,
        otpExpiry: _serverOtpExpiry!,
      );

      if (mounted) Navigator.pop(dialogContext);

      if (result['success'] == true) {
        _showSnackBar("Đăng ký thành công! Vui lòng đăng nhập.", Colors.green);
        if (mounted) Navigator.of(context).pop();
      } else {
        _showSnackBar(result['message'] ?? "Đăng ký thất bại.", Colors.red);
      }
    } catch (e) {
      if (mounted) Navigator.pop(dialogContext);
      _showSnackBar("Lỗi xử lý đăng ký: $e", Colors.red);
    }
  }

  void _showError(String msg) {
    setState(() {
      _isLoading = false;
      _message = msg;
    });
    _showSnackBar(msg, Colors.red);
  }

  void _showSnackBar(String message, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: color, duration: const Duration(seconds: 3)),
    );
  }

  Widget _buildInputField({
    required String hintText,
    required TextEditingController controller,
    bool isPassword = false,
    TextInputType keyboardType = TextInputType.text,
    bool? isVisible,
    VoidCallback? toggleVisibility,
  }) {
    return Container(
      decoration: BoxDecoration(color: const Color(0xFFF2F2F7), borderRadius: BorderRadius.circular(10)),
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: TextField(
        controller: controller,
        keyboardType: keyboardType,
        obscureText: isPassword && !(isVisible ?? false),
        decoration: InputDecoration(
          hintText: hintText,
          border: InputBorder.none,
          suffixIcon: isPassword
              ? IconButton(
                  icon: Icon((isVisible ?? false) ? Icons.visibility_off_outlined : Icons.visibility_outlined, color: Colors.grey[600]),
                  onPressed: toggleVisibility,
                )
              : null,
          contentPadding: const EdgeInsets.symmetric(vertical: 18),
          hintStyle: TextStyle(color: Colors.grey[600], fontSize: 16),
        ),
        style: const TextStyle(fontSize: 16, color: Colors.black87),
      ),
    );
  }

  Widget _buildRegistrationButton() {
    return SizedBox(
      width: double.infinity,
      height: 56,
      child: ElevatedButton(
        onPressed: _isLoading ? null : _handleRegisterButton,
        style: ElevatedButton.styleFrom(
          backgroundColor: primaryBlue,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          textStyle: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        child: _isLoading
            ? const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 3))
            : const Text('Đăng ký'),
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
              SizedBox(
                height: screenHeight * 0.20,
                child: const Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.person_add_alt_1, size: 50, color: primaryBlue),
                      SizedBox(height: 10),
                      Text('Tạo tài khoản của bạn', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: primaryBlue)),
                    ],
                  ),
                ),
              ),
              const Text('Đăng Ký', style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: Colors.black87)),
              const SizedBox(height: 32),
              _buildInputField(hintText: 'Tên đăng nhập', controller: _usernameController),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Họ và tên', controller: _fullNameController, keyboardType: TextInputType.name),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Email cá nhân', controller: _emailController, keyboardType: TextInputType.emailAddress),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Số điện thoại', controller: _phoneController, keyboardType: TextInputType.phone),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Địa chỉ (Tùy chọn)', controller: _addressController, keyboardType: TextInputType.streetAddress),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Mật khẩu', controller: _passwordController, isPassword: true, isVisible: _isPasswordVisible, toggleVisibility: () => setState(() => _isPasswordVisible = !_isPasswordVisible)),
              const SizedBox(height: 16),
              _buildInputField(hintText: 'Xác nhận Mật khẩu', controller: _rePasswordController, isPassword: true, isVisible: _isRePasswordVisible, toggleVisibility: () => setState(() => _isRePasswordVisible = !_isRePasswordVisible)),
              const SizedBox(height: 32),
              _buildRegistrationButton(),
              const SizedBox(height: 20),
              if (_message.isNotEmpty) Padding(padding: const EdgeInsets.only(bottom: 16.0), child: Text(_message, style: const TextStyle(color: Colors.red, fontSize: 14), textAlign: TextAlign.center)),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Text("Đã có tài khoản?", style: TextStyle(color: Colors.black54, fontSize: 16)),
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(),
                    child: const Text('Đăng nhập', style: TextStyle(color: primaryBlue, fontWeight: FontWeight.bold, fontSize: 16)),
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