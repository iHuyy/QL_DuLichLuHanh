import 'package:app_dllh/screens/change_password_page.dart';
import 'package:app_dllh/screens/edit_profile_page.dart';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'remote_logout_page.dart';
import 'login_page.dart';
import 'invoice_verification_page.dart'; // Import trang mới sắp tạo

// --- BỘ MÀU WEB STYLE ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);

class ProfileScreen extends StatefulWidget {
  final String userID;
  final String userName;

  const ProfileScreen({Key? key, required this.userID, required this.userName})
      : super(key: key);

  @override
  _ProfileScreenState createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final AuthService _authService = AuthService();

  void _logout() async {
    await _authService.logout();
    if (mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (context) => const LoginPage()),
        (Route<dynamic> route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      body: SingleChildScrollView(
        child: Column(
          children: [
            // 1. Header Profile (Giống Banner Web)
            Container(
              width: double.infinity,
              decoration: const BoxDecoration(
                color: primaryDark,
                borderRadius: BorderRadius.vertical(bottom: Radius.circular(30)),
              ),
              padding: const EdgeInsets.fromLTRB(24, 60, 24, 40),
              child: Column(
                children: [
                  Container(
                    padding: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      border: Border.all(color: primaryGreen, width: 3),
                    ),
                    child: const CircleAvatar(
                      radius: 40,
                      backgroundColor: Colors.white,
                      child: Icon(Icons.person, size: 50, color: primaryDark),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    widget.userName,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'ID: ${widget.userID}',
                    style: TextStyle(color: Colors.white.withOpacity(0.7)),
                  ),
                ],
              ),
            ),

            const SizedBox(height: 24),

            // 2. Menu Options
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                children: [
                  _buildMenuCard(
                    title: 'Xác thực Hóa đơn PDF',
                    subtitle: 'Kiểm tra chữ ký số và tính toàn vẹn',
                    icon: Icons.verified_user_outlined,
                    iconColor: primaryGreen,
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                            builder: (context) => const InvoiceVerificationPage()),
                      );
                    },
                  ),
                  const SizedBox(height: 16),
                  _buildMenuCard(
                    title: 'Chỉnh sửa thông tin',
                    subtitle: 'Cập nhật email, số điện thoại',
                    icon: Icons.edit_outlined,
                    iconColor: Colors.orange,
                    onTap: () async {
                      // Chuyển trang và đợi kết quả
                      final result = await Navigator.push(
                        context, 
                        MaterialPageRoute(builder: (_) => const EditProfilePage())
                      );
                      // Nếu update thành công (result = true), có thể cần reload lại profile cha nếu muốn cập nhật tên ngay lập tức
                    },
                  ),
                  const SizedBox(height: 16),
                  _buildMenuCard(
                    title: 'Quản lý Thiết bị',
                    subtitle: 'Đăng xuất tài khoản khỏi Web/Desktop',
                    icon: Icons.devices_other_rounded,
                    iconColor: Colors.purple,
                    onTap: () {
                      Navigator.push(context, MaterialPageRoute(builder: (_) => const RemoteLogoutPage()));
                    },
                  ),
                  const SizedBox(height: 16),
                  _buildMenuCard(
                    title: 'Đổi mật khẩu',
                    subtitle: 'Bảo mật tài khoản của bạn',
                    icon: Icons.lock_outline,
                    iconColor: Colors.blue,
                    onTap: () {
                      Navigator.push(context, MaterialPageRoute(builder: (_) => const ChangePasswordPage()));
                    },
                  ),
                  const SizedBox(height: 16),
                  _buildMenuCard(
                    title: 'Đăng xuất',
                    subtitle: 'Thoát khỏi tài khoản',
                    icon: Icons.logout,
                    iconColor: Colors.red,
                    onTap: _logout,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 40),
          ],
        ),
      ),
    );
  }

  Widget _buildMenuCard({
    required String title,
    required String subtitle,
    required IconData icon,
    required Color iconColor,
    required VoidCallback onTap,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.05),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
        leading: Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: iconColor.withOpacity(0.1),
            borderRadius: BorderRadius.circular(8),
          ),
          child: Icon(icon, color: iconColor),
        ),
        title: Text(
          title,
          style: const TextStyle(
            fontWeight: FontWeight.bold,
            color: primaryDark,
            fontSize: 16,
          ),
        ),
        subtitle: Text(
          subtitle,
          style: TextStyle(color: Colors.grey[600], fontSize: 13),
        ),
        trailing: const Icon(Icons.arrow_forward_ios, size: 16, color: Colors.grey),
        onTap: onTap,
      ),
    );
  }
}