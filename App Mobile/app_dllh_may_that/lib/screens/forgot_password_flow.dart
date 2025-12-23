import 'dart:async';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';

const Color primaryBlue = Color(0xFF007AFF);

class ForgotPasswordFlow extends StatefulWidget {
  const ForgotPasswordFlow({Key? key}) : super(key: key);

  @override
  _ForgotPasswordFlowState createState() => _ForgotPasswordFlowState();
}

class _ForgotPasswordFlowState extends State<ForgotPasswordFlow> {
  final AuthService _authService = AuthService();
  final PageController _pageController = PageController();
  
  // Data
  final TextEditingController _usernameController = TextEditingController();
  final TextEditingController _otpController = TextEditingController();
  final TextEditingController _pass1Controller = TextEditingController();
  final TextEditingController _pass2Controller = TextEditingController();
  
  bool _isLoading = false;
  int _currentPage = 0;
  int _countdown = 180;
  Timer? _timer;

  @override
  void dispose() {
    _usernameController.dispose();
    _otpController.dispose();
    _pass1Controller.dispose();
    _pass2Controller.dispose();
    _pageController.dispose();
    _timer?.cancel();
    super.dispose();
  }

  void _showSnackBar(String message, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message), backgroundColor: color));
  }

  // --- STEP 1: Gửi Username ---
  Future<void> _submitUsername() async {
    final username = _usernameController.text.trim();
    if (username.isEmpty) {
      _showSnackBar("Vui lòng nhập tên đăng nhập", Colors.orange);
      return;
    }

    setState(() => _isLoading = true);
    final res = await _authService.forgotPassword(username);
    setState(() => _isLoading = false);

    if (res['success'] == true) {
      _showSnackBar(res['message'] ?? "Đã gửi mã xác thực", Colors.green);
      _startTimer();
      _nextPage();
    } else {
      _showSnackBar(res['message'] ?? "Lỗi không xác định", Colors.red);
    }
  }

  // --- STEP 2: Xác thực OTP ---
  void _startTimer() {
    setState(() => _countdown = 180);
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_countdown > 0) {
        setState(() => _countdown--);
      } else {
        timer.cancel();
      }
    });
  }

  Future<void> _submitOtp() async {
    final otp = _otpController.text.trim();
    if (otp.length != 6) {
      _showSnackBar("Mã xác thực phải có 6 chữ số", Colors.orange);
      return;
    }

    setState(() => _isLoading = true);
    final res = await _authService.verifyOtp(_usernameController.text.trim(), otp);
    setState(() => _isLoading = false);

    if (res['success'] == true) {
      _nextPage();
    } else {
      _showSnackBar(res['message'] ?? "Mã xác thực không đúng", Colors.red);
    }
  }

  // --- STEP 3: Đổi mật khẩu ---
  Future<void> _submitNewPassword() async {
    final p1 = _pass1Controller.text;
    final p2 = _pass2Controller.text;

    if (p1.length < 6) {
      _showSnackBar("Mật khẩu phải từ 6 ký tự trở lên", Colors.orange);
      return;
    }
    if (p1 != p2) {
      _showSnackBar("Mật khẩu nhập lại không khớp", Colors.orange);
      return;
    }

    setState(() => _isLoading = true);
    final res = await _authService.resetPassword(
      _usernameController.text.trim(),
      _otpController.text.trim(),
      p1
    );
    setState(() => _isLoading = false);

    if (res['success'] == true) {
      _showSnackBar("Đổi mật khẩu thành công! Vui lòng đăng nhập.", Colors.green);
      Navigator.pop(context); // Quay về Login
    } else {
      _showSnackBar(res['message'] ?? "Lỗi đổi mật khẩu", Colors.red);
    }
  }

  void _nextPage() {
    _pageController.nextPage(duration: const Duration(milliseconds: 300), curve: Curves.easeInOut);
    setState(() => _currentPage++);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text("Quên mật khẩu", style: TextStyle(color: Colors.white)),
        backgroundColor: primaryBlue,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Colors.white),
          onPressed: () {
            if (_currentPage > 0) {
              _pageController.previousPage(duration: const Duration(milliseconds: 300), curve: Curves.easeInOut);
              setState(() => _currentPage--);
            } else {
              Navigator.pop(context);
            }
          },
        ),
      ),
      body: PageView(
        controller: _pageController,
        physics: const NeverScrollableScrollPhysics(), // Chặn người dùng vuốt tay
        children: [
          _buildStep1(),
          _buildStep2(),
          _buildStep3(),
        ],
      ),
    );
  }

  // --- SỬA LỖI Ở ĐÂY: Bọc nội dung vào SingleChildScrollView ---

  // Giao diện Nhập Username
  Widget _buildStep1() {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.lock_reset, size: 80, color: primaryBlue),
            const SizedBox(height: 20),
            const Text("Nhập tên đăng nhập của bạn", style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 10),
            const Text("Chúng tôi sẽ gửi mã xác thực đến email đã đăng ký.", textAlign: TextAlign.center, style: TextStyle(color: Colors.grey)),
            const SizedBox(height: 30),
            TextField(
              controller: _usernameController,
              decoration: const InputDecoration(
                labelText: "Tên đăng nhập",
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.person),
              ),
            ),
            const SizedBox(height: 30),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _submitUsername,
                style: ElevatedButton.styleFrom(backgroundColor: primaryBlue),
                child: _isLoading ? const CircularProgressIndicator(color: Colors.white) : const Text("Tiếp tục", style: TextStyle(color: Colors.white, fontSize: 16)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Giao diện Nhập OTP
  Widget _buildStep2() {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.email_outlined, size: 80, color: primaryBlue),
            const SizedBox(height: 20),
            const Text("Nhập mã xác thực", style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 10),
            Text("Mã đã được gửi. Vui lòng kiểm tra email.\nMã hết hạn sau: ${_countdown}s", textAlign: TextAlign.center, style: const TextStyle(color: Colors.grey)),
            const SizedBox(height: 30),
            TextField(
              controller: _otpController,
              keyboardType: TextInputType.number,
              textAlign: TextAlign.center,
              maxLength: 6,
              style: const TextStyle(fontSize: 24, letterSpacing: 8, fontWeight: FontWeight.bold),
              decoration: const InputDecoration(
                hintText: "------",
                border: OutlineInputBorder(),
                counterText: "", // Ẩn bộ đếm ký tự
              ),
            ),
            const SizedBox(height: 30),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _submitOtp,
                style: ElevatedButton.styleFrom(backgroundColor: primaryBlue),
                child: _isLoading ? const CircularProgressIndicator(color: Colors.white) : const Text("Xác nhận", style: TextStyle(color: Colors.white, fontSize: 16)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // Giao diện Đổi mật khẩu
  Widget _buildStep3() {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.vpn_key, size: 80, color: primaryBlue),
            const SizedBox(height: 20),
            const Text("Đặt lại mật khẩu", style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 30),
            TextField(
              controller: _pass1Controller,
              obscureText: true,
              decoration: const InputDecoration(labelText: "Mật khẩu mới", border: OutlineInputBorder(), prefixIcon: Icon(Icons.lock)),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _pass2Controller,
              obscureText: true,
              decoration: const InputDecoration(labelText: "Nhập lại mật khẩu mới", border: OutlineInputBorder(), prefixIcon: Icon(Icons.lock_outline)),
            ),
            const SizedBox(height: 30),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _submitNewPassword,
                style: ElevatedButton.styleFrom(backgroundColor: primaryBlue),
                child: _isLoading ? const CircularProgressIndicator(color: Colors.white) : const Text("Hoàn tất", style: TextStyle(color: Colors.white, fontSize: 16)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}