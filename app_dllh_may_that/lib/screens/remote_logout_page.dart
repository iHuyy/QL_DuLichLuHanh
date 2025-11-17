import 'package:flutter/material.dart';
import '../models/session.dart';
import '../services/auth_service.dart';

class RemoteLogoutPage extends StatefulWidget {
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
        SnackBar(content: Text('Session logged out successfully')),
      );
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Failed to log out session: $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Active Sessions'),
      ),
      body: FutureBuilder<List<Session>>(
        future: _sessionsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return Center(child: CircularProgressIndicator());
          } else if (snapshot.hasError) {
            return Center(child: Text('Error: ${snapshot.error}'));
          } else if (!snapshot.hasData || snapshot.data!.isEmpty) {
            return Center(child: Text('No active sessions found.'));
          } else {
            // Show only non-mobile sessions (e.g. web) — we don't want to list
            // the current mobile session here. This makes the UI show web sessions.
      final sessions = snapshot.data!
        .where((s) => s.deviceType.toLowerCase() != 'mobile')
        .toList();

            if (sessions.isEmpty) {
              return Center(child: Text('No active web sessions found.'));
            }

            return ListView.builder(
              itemCount: sessions.length,
              itemBuilder: (context, index) {
                final session = sessions[index];
                return ListTile(
                  title: Text(session.deviceInfo),
                  subtitle: Text(
                      '${session.deviceType} - Logged in at ${session.loginTime}'),
                  trailing: ElevatedButton(
                    onPressed: () => _logoutSession(session.sessionId),
                    child: Text('Logout'),
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
