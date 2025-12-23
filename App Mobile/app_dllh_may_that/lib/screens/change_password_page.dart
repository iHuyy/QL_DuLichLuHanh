import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'dart:convert';

const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);

class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({Key? key}) : super(key: key);

  @override
  _ChangePasswordPageState createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final ApiClient _api = ApiClient();
  final _formKey = GlobalKey<FormState>();
  
  final _oldPassController = TextEditingController();
  final _newPassController = TextEditingController();
  final _confirmPassController = TextEditingController();
  
  bool _isLoading = false;
  bool _obscureOld = true;
  bool _obscureNew = true;
  bool _obscureConfirm = true;

  // Regex kiểm tra mật khẩu mạnh: 8 ký tự, 1 hoa, 1 thường, 1 số, 1 đặc biệt
  // Tham khảo các pattern phổ biến
  final _strongPasswordRegex = RegExp(r'^(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9])(?=.*[!@#\$%^&*(),.?":{}|<>]).{8,}$');

  Future<void> _changePassword() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isLoading = true);
    try {
      final resp = await _api.postJson('change_password.php', body: {
        'oldPassword': _oldPassController.text,
        'newPassword': _newPassController.text,
      });

      final json = jsonDecode(resp.body);
      if (json['success'] == true) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Đổi mật khẩu thành công!'), backgroundColor: primaryGreen),
        );
        Navigator.pop(context);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(json['message'] ?? 'Lỗi không xác định'), backgroundColor: Colors.red),
        );
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Lỗi kết nối: $e')));
    } finally {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        title: const Text('ĐỔI MẬT KHẨU', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        backgroundColor: Colors.white,
        foregroundColor: primaryDark,
        elevation: 0,
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Form(
          key: _formKey,
          child: Column(
            children: [
              // 1. Mật khẩu cũ
              _buildPasswordField(
                label: 'Mật khẩu hiện tại', 
                controller: _oldPassController, 
                obscure: _obscureOld, 
                onToggle: (val) => setState(() => _obscureOld = val),
                validator: (val) {
                  if (val == null || val.isEmpty) return 'Vui lòng nhập mật khẩu hiện tại';
                  return null;
                }
              ),
              const SizedBox(height: 16),

              // 2. Mật khẩu mới
              _buildPasswordField(
                label: 'Mật khẩu mới', 
                controller: _newPassController, 
                obscure: _obscureNew, 
                onToggle: (val) => setState(() => _obscureNew = val),
                validator: (val) {
                  if (val == null || val.isEmpty) return 'Vui lòng nhập mật khẩu mới';
                  if (val.length < 8) return 'Mật khẩu phải từ 8 ký tự trở lên';
                  if (!_strongPasswordRegex.hasMatch(val)) {
                    return 'Cần có chữ Hoa, thường, số và ký tự đặc biệt';
                  }
                  if (val == _oldPassController.text) {
                    return 'Mật khẩu mới không được trùng mật khẩu cũ';
                  }
                  return null;
                }
              ),
              const SizedBox(height: 16),

              // 3. Xác nhận mật khẩu mới
              _buildPasswordField(
                label: 'Xác nhận mật khẩu mới', 
                controller: _confirmPassController, 
                obscure: _obscureConfirm, 
                onToggle: (val) => setState(() => _obscureConfirm = val),
                validator: (val) {
                  if (val == null || val.isEmpty) return 'Vui lòng xác nhận mật khẩu';
                  if (val != _newPassController.text) return 'Mật khẩu xác nhận không khớp';
                  return null;
                }
              ),
              
              const SizedBox(height: 32),
              
              SizedBox(
                width: double.infinity,
                height: 50,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _changePassword,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryGreen,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  ),
                  child: _isLoading 
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                    : const Text('XÁC NHẬN', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPasswordField({
    required String label, 
    required TextEditingController controller, 
    required bool obscure, 
    required Function(bool) onToggle, 
    required String? Function(String?) validator
  }) {
    return TextFormField(
      controller: controller,
      obscureText: obscure,
      validator: validator,
      autovalidateMode: AutovalidateMode.onUserInteraction, // Kiểm tra ngay khi nhập
      decoration: InputDecoration(
        labelText: label,
        prefixIcon: Icon(Icons.lock_outline, color: primaryDark.withOpacity(0.6)),
        suffixIcon: IconButton(
          icon: Icon(obscure ? Icons.visibility_off : Icons.visibility, color: Colors.grey),
          onPressed: () => onToggle(!obscure),
        ),
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: Colors.grey.shade200)),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: primaryGreen)),
        errorMaxLines: 2,
      ),
    );
  }
}