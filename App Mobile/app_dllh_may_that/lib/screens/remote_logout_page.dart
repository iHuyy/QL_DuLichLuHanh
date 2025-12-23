import 'package:flutter/material.dart';
import 'package:app_dllh/models/session.dart';
import 'package:app_dllh/services/auth_service.dart';

// --- BỘ MÀU WEB STYLE ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);

class RemoteLogoutPage extends StatefulWidget {
  const RemoteLogoutPage({Key? key}) : super(key: key);

  @override
  _RemoteLogoutPageState createState() => _RemoteLogoutPageState();
}

class _RemoteLogoutPageState extends State<RemoteLogoutPage> {
  late Future<List<Session>> _sessionsFuture;
  final AuthService _authService = AuthService();

  @override
  void initState() {
    super.initState();
    _sessionsFuture = _authService.getActiveSessions();
  }

  void _logoutSession(String sessionId) async {
    try {
      await _authService.logoutRemote(sessionId);
      setState(() {
        _sessionsFuture = _authService.getActiveSessions();
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã đăng xuất thiết bị thành công'), backgroundColor: primaryGreen),
      );
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Lỗi: $e'), backgroundColor: Colors.red),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        title: const Text('QUẢN LÝ PHIÊN ĐĂNG NHẬP', 
            style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        backgroundColor: Colors.white,
        foregroundColor: primaryDark,
        elevation: 0,
        centerTitle: true,
      ),
      body: FutureBuilder<List<Session>>(
        future: _sessionsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator(color: primaryGreen));
          } else if (snapshot.hasError) {
            return Center(child: Text('Lỗi tải dữ liệu: ${snapshot.error}'));
          } else if (!snapshot.hasData || snapshot.data!.isEmpty) {
            return const Center(child: Text('Không có phiên đăng nhập nào khác.'));
          } else {
            // Chỉ hiện các session KHÁC (Không phải Mobile đang dùng)
            // Ví dụ: Web, Tablet...
            final sessions = snapshot.data!
                .where((s) => s.deviceType.toLowerCase() != 'mobile') 
                .toList();

            if (sessions.isEmpty) {
              return Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: const [
                    Icon(Icons.devices, size: 60, color: Colors.grey),
                    SizedBox(height: 16),
                    Text('Chưa có thiết bị Web nào đang đăng nhập.', style: TextStyle(color: Colors.grey)),
                  ],
                ),
              );
            }

            return ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: sessions.length,
              separatorBuilder: (ctx, i) => const SizedBox(height: 12),
              itemBuilder: (context, index) {
                final session = sessions[index];
                return Container(
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(12),
                    boxShadow: [
                      BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 5, offset: const Offset(0, 2))
                    ],
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    leading: Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.blue.shade50,
                        shape: BoxShape.circle,
                      ),
                      child: Icon(
                        session.deviceType == 'WEB' ? Icons.language : Icons.device_unknown, 
                        color: primaryDark
                      ),
                    ),
                    title: Text(
                      session.deviceInfo.isNotEmpty ? session.deviceInfo : 'Thiết bị Web',
                      style: const TextStyle(fontWeight: FontWeight.bold, color: primaryDark),
                    ),
                    subtitle: Text(
                      'Đăng nhập: ${session.loginTime}',
                      style: TextStyle(fontSize: 12, color: Colors.grey[600]),
                    ),
                    trailing: IconButton(
                      icon: const Icon(Icons.logout, color: Colors.red),
                      tooltip: "Đăng xuất thiết bị này",
                      onPressed: () => _logoutSession(session.sessionId),
                    ),
                  ),
                );
              },
            );
          }
        },
      ),
    );
  }
}