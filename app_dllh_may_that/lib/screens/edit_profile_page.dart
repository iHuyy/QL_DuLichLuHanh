import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'dart:convert';

const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);

class EditProfilePage extends StatefulWidget {
  const EditProfilePage({Key? key}) : super(key: key);

  @override
  _EditProfilePageState createState() => _EditProfilePageState();
}

class _EditProfilePageState extends State<EditProfilePage> {
  final ApiClient _api = ApiClient();
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;

  final TextEditingController _nameController = TextEditingController();
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _phoneController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadCurrentProfile();
  }

  Future<void> _loadCurrentProfile() async {
    setState(() => _isLoading = true);
    try {
      final resp = await _api.getJson('get_user.php');
      if (resp.statusCode == 200) {
        final json = jsonDecode(resp.body);
        if (json['success'] == true && json['data'] != null) {
          final data = json['data'];
          _nameController.text = data['fullName'] ?? '';
          _emailController.text = data['email'] ?? '';
          _phoneController.text = data['phone'] ?? '';
          _addressController.text = data['address'] ?? '';
        }
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Lỗi tải thông tin: $e')));
    } finally {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _updateProfile() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isLoading = true);
    try {
      final resp = await _api.postJson('update_profile.php', body: {
        'hoTen': _nameController.text.trim(),
        'email': _emailController.text.trim(),
        'soDienThoai': _phoneController.text.trim(),
        'diaChi': _addressController.text.trim(),
      });

      final json = jsonDecode(resp.body);
      if (json['success'] == true) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Cập nhật thành công!'), backgroundColor: primaryGreen),
        );
        Navigator.pop(context, true); // Trả về true để màn hình trước refresh
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(json['message'] ?? 'Thất bại'), backgroundColor: Colors.red),
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
        title: const Text('CHỈNH SỬA THÔNG TIN', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        backgroundColor: Colors.white,
        foregroundColor: primaryDark,
        elevation: 0,
        centerTitle: true,
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: primaryGreen))
          : SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Form(
                key: _formKey,
                child: Column(
                  children: [
                    _buildTextField('Họ và tên', _nameController, Icons.person, required: true),
                    const SizedBox(height: 16),
                    _buildTextField('Email', _emailController, Icons.email, required: true, isEmail: true),
                    const SizedBox(height: 16),
                    _buildTextField('Số điện thoại', _phoneController, Icons.phone, isPhone: true),
                    const SizedBox(height: 16),
                    _buildTextField('Địa chỉ', _addressController, Icons.location_on, maxLines: 2),
                    const SizedBox(height: 32),
                    SizedBox(
                      width: double.infinity,
                      height: 50,
                      child: ElevatedButton(
                        onPressed: _updateProfile,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: primaryGreen,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                        ),
                        child: const Text('LƯU THAY ĐỔI', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildTextField(String label, TextEditingController controller, IconData icon, 
      {bool required = false, bool isEmail = false, bool isPhone = false, int maxLines = 1}) {
    return TextFormField(
      controller: controller,
      maxLines: maxLines,
      validator: (value) {
        if (required && (value == null || value.isEmpty)) return 'Vui lòng nhập $label';
        if (isEmail && value != null && value.isNotEmpty && !value.contains('@')) return 'Email không hợp lệ';
        return null;
      },
      keyboardType: isPhone ? TextInputType.phone : (isEmail ? TextInputType.emailAddress : TextInputType.text),
      decoration: InputDecoration(
        labelText: label,
        prefixIcon: Icon(icon, color: primaryDark.withOpacity(0.6)),
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: Colors.grey.shade200)),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: primaryGreen)),
      ),
    );
  }
}