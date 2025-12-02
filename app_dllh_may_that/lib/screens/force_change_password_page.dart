import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';

const Color primaryBlue = Color(0xFF007AFF);

class ForceChangePasswordPage extends StatefulWidget {
  final String username;
  final String oldPassword;

  const ForceChangePasswordPage({
    Key? key, 
    required this.username, 
    required this.oldPassword
  }) : super(key: key);

  @override
  _ForceChangePasswordPageState createState() => _ForceChangePasswordPageState();
}

class _ForceChangePasswordPageState extends State<ForceChangePasswordPage> {
  final AuthService _authService = AuthService();
  final _newPassController = TextEditingController();
  final _confirmPassController = TextEditingController();
  
  bool _isLoading = false;
  bool _isObscureNew = true;
  bool _isObscureConfirm = true;
  String _message = "";

  @override
  void dispose() {
    _newPassController.dispose();
    _confirmPassController.dispose();
    super.dispose();
  }

  Future<void> _handleSubmit() async {
    final newPass = _newPassController.text.trim();
    final confirmPass = _confirmPassController.text.trim();

    if (newPass.isEmpty || confirmPass.isEmpty) {
      _showError("Vui lòng nhập đầy đủ thông tin.");
      return;
    }

    if (newPass == widget.oldPassword) {
      _showError("Mật khẩu mới không được trùng với mật khẩu cũ.");
      return;
    }

    if (newPass != confirmPass) {
      _showError("Mật khẩu xác nhận không khớp.");
      return;
    }

    if (newPass.length < 8 || 
        !newPass.contains(RegExp(r'[A-Z]')) || 
        !newPass.contains(RegExp(r'[0-9]')) || 
        !newPass.contains(RegExp(r'[!@#$%^&*(),.?":{}|<>]'))) {
      _showError("Mật khẩu mới phải có ít nhất 8 ký tự, 1 chữ hoa, 1 số và 1 ký tự đặc biệt.");
      return;
    }

    setState(() {
      _isLoading = true;
      _message = "";
    });

    try {
      final result = await _authService.forceChangePassword(
        widget.username, 
        widget.oldPassword, 
        newPass
      );

      setState(() => _isLoading = false);

      if (result['success'] == true) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text("Đổi mật khẩu thành công! Vui lòng đăng nhập lại."), backgroundColor: Colors.green),
        );
        Navigator.of(context).pop(); // Quay về màn hình Login
      } else {
        _showError(result['message'] ?? "Đổi mật khẩu thất bại.");
      }
    } catch (e) {
      setState(() => _isLoading = false);
      _showError("Lỗi kết nối: $e");
    }
  }

  void _showError(String msg) {
    setState(() => _message = msg);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg), backgroundColor: Colors.red),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white, // Đặt màu nền trắng cho sạch
      appBar: AppBar(
        title: const Text("Đổi Mật Khẩu Định Kỳ", style: TextStyle(color: Colors.white)),
        backgroundColor: primaryBlue,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Colors.white),
          onPressed: () => Navigator.of(context).pop(),
        ),
      ),
      // --- SỬA LỖI Ở ĐÂY: Thêm Center và SingleChildScrollView ---
      body: Center( 
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center, // Căn giữa nội dung
            children: [
              const Icon(Icons.lock_clock, size: 80, color: Colors.orange),
              const SizedBox(height: 20),
              const Text(
                "Mật khẩu của bạn đã hết hạn.\nVui lòng cập nhật mật khẩu mới để tiếp tục sử dụng dịch vụ.",
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 16, color: Colors.black87),
              ),
              const SizedBox(height: 30),
              
              _buildPassField("Mật khẩu mới", _newPassController, _isObscureNew, () {
                setState(() => _isObscureNew = !_isObscureNew);
              }),
              const SizedBox(height: 16),
              _buildPassField("Xác nhận mật khẩu mới", _confirmPassController, _isObscureConfirm, () {
                setState(() => _isObscureConfirm = !_isObscureConfirm);
              }),

              const SizedBox(height: 24),
              if (_message.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Text(_message, style: const TextStyle(color: Colors.red), textAlign: TextAlign.center),
                ),

              SizedBox(
                width: double.infinity,
                height: 50,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _handleSubmit,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryBlue,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  ),
                  child: _isLoading 
                    ? const SizedBox(
                        width: 24, 
                        height: 24, 
                        child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2)
                      )
                    : const Text("Đổi mật khẩu", style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPassField(String label, TextEditingController controller, bool obscure, VoidCallback toggle) {
    return TextField(
      controller: controller,
      obscureText: obscure,
      decoration: InputDecoration(
        labelText: label,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        suffixIcon: IconButton(
          icon: Icon(obscure ? Icons.visibility_off : Icons.visibility, color: Colors.grey),
          onPressed: toggle,
        ),
      ),
    );
  }
}