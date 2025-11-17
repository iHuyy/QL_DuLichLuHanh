import 'package:app_dllh/screens/remote_logout_page.dart';
import 'package:flutter/material.dart';
import 'login_page.dart';
import 'my_booking_page.dart';
import 'package:app_dllh/services/auth_service.dart';

const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

class ProfileScreen extends StatelessWidget {
  final String userID;
  final String userName;

  const ProfileScreen({
    Key? key,
    required this.userID,
    this.userName = 'User',
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header với thông tin người dùng
              Padding(
                padding: const EdgeInsets.all(16.0),
                child: Row(
                  children: [
                    // Avatar
                    CircleAvatar(
                      radius: 40,
                      backgroundColor: primaryBlue,
                      child: const Icon(
                        Icons.person,
                        size: 50,
                        color: Colors.white,
                      ),
                    ),
                    const SizedBox(width: 16),
                    // Thông tin người dùng
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            userName,
                            style: const TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.bold,
                              color: darkTextColor,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            'jit.bonik@mail.com',
                            style: const TextStyle(
                              fontSize: 14,
                              color: Colors.grey,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const Divider(thickness: 1),
              const SizedBox(height: 16),

              // ACCOUNT SECTION
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16.0),
                child: Text(
                  'Account',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    color: Colors.grey,
                  ),
                ),
              ),
              const SizedBox(height: 8),
              _buildMenuItem(
                icon: Icons.person_outline,
                title: 'Your Profile',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.payment,
                title: 'Payment History',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.bookmark_outline,
                title: 'My Booking',
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (context) => MyBookingPage(userID: userID),
                    ),
                  );
                },
              ),
              _buildMenuItem(
                icon: Icons.local_offer_outlined,
                title: 'Your Offers',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.public,
                title: 'All Country',
                onTap: () {},
              ),
              const Divider(thickness: 1, height: 24),

              // SETTING SECTION
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16.0),
                child: Text(
                  'Setting',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    color: Colors.grey,
                  ),
                ),
              ),
              const SizedBox(height: 8),
              _buildMenuItem(
                icon: Icons.language,
                title: 'Language',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.dark_mode_outlined,
                title: 'Dark Mood',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.logout,
                title: 'Remote Logout',
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (context) => RemoteLogoutPage(),
                    ),
                  );
                },
              ),
              const Divider(thickness: 1, height: 24),

              // HELP & LEGAL SECTION
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16.0),
                child: Text(
                  'Help & Legal',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    color: Colors.grey,
                  ),
                ),
              ),
              const SizedBox(height: 8),
              _buildMenuItem(
                icon: Icons.support_agent_outlined,
                title: 'Emegency Support',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.help_outline,
                title: 'Help',
                onTap: () {},
              ),
              _buildMenuItem(
                icon: Icons.description_outlined,
                title: 'Terms & Conditions',
                onTap: () {},
              ),

              // Logout button
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 24),
                child: SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: () => _showLogoutDialog(context),
                    icon: const Icon(Icons.logout),
                    label: const Text('Logout'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.red.shade400,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildMenuItem({
    required IconData icon,
    required String title,
    required VoidCallback onTap,
  }) {
    return ListTile(
      leading: Icon(icon, color: primaryBlue),
      title: Text(
        title,
        style: const TextStyle(
          fontSize: 16,
          color: darkTextColor,
        ),
      ),
      trailing: const Icon(Icons.arrow_forward_ios, size: 16, color: Colors.grey),
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      onTap: onTap,
    );
  }

  void _showLogoutDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (BuildContext dialogContext) {
        return AlertDialog(
          title: const Text('Logout'),
          content: const Text('Are you sure you want to logout?'),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Cancel'),
            ),
            TextButton(
              onPressed: () async {
                Navigator.pop(dialogContext);
                // Call server-side logout which sets IS_ACTIVE='N', then
                // remove local session and navigate to login. This matches
                // the logout behavior used elsewhere in the app.
                final auth = AuthService();
                try {
                  await auth.logout();
                } catch (e) {
                  // ignore — still navigate to login to clear local state
                }

                Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute(builder: (_) => const LoginPage()),
                  (route) => false,
                );
              },
              child: const Text('Logout', style: TextStyle(color: Colors.red)),
            ),
          ],
        );
      },
    );
  }
}