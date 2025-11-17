import 'dart:async';

import 'package:app_dllh/screens/home_page.dart';
import 'package:app_dllh/screens/login_page.dart';
import 'package:app_dllh/screens/register_page.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/services/navigation_service.dart';
import 'package:flutter/material.dart';
 
void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      // 1. Gán navigatorKey
      navigatorKey: NavigationService.navigatorKey,
      debugShowCheckedModeBanner: false,
      title: "Tour App",
      theme: ThemeData(primarySwatch: Colors.blue),
      // 2. Sử dụng AuthWrapper làm home
      home: const AuthWrapper(),
      // 3. Định nghĩa routes
      routes: {
        '/login': (context) => const LoginPage(),
        '/register': (context) => RegisterPage(),
        // '/home' có thể được định nghĩa ở đây nếu cần
      },
    );
  }
}

// Widget này kiểm tra trạng thái đăng nhập và quyết định trang nào sẽ hiển thị
class AuthWrapper extends StatefulWidget {
  const AuthWrapper({super.key});

  @override
  _AuthWrapperState createState() => _AuthWrapperState();
}

class _AuthWrapperState extends State<AuthWrapper> with WidgetsBindingObserver {
  final AuthService _authService = AuthService();
  Timer? _sessionCheckTimer;
  DateTime? _lastInteraction;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _startSessionCheck();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _sessionCheckTimer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _checkSession();
      _startSessionCheck();
    } else if (state == AppLifecycleState.paused) {
      _sessionCheckTimer?.cancel();
    }
  }

  void _startSessionCheck() {
    _sessionCheckTimer?.cancel(); // Hủy timer cũ nếu có
    // (SỬA ĐỔI) Tăng thời gian check lên 60s để giảm tải
    _sessionCheckTimer = Timer.periodic(const Duration(seconds: 60), (timer) {
      _checkSession();
    });
  }

  // Throttle interactive checks (Giữ nguyên logic này)
  Future<void> _maybeCheckSessionInteractive() async {
    try {
      final now = DateTime.now();
      // (SỬA ĐỔI) Tăng thời gian throttle lên 10s
      if (_lastInteraction != null && now.difference(_lastInteraction!) < const Duration(seconds: 10)) {
        return; // recently checked
      }
      _lastInteraction = now;

      final isLoggedIn = await _authService.isLoggedIn();
      if (!isLoggedIn) return;
      final isValid = await _authService.checkSession();
      if (!isValid) {
        // Show an explicit dialog and then force client-side logout so the
        // user sees a clear message that their session was expired remotely.
        final navState = NavigationService.navigatorKey.currentState;
        final dialogContext = navState?.context;
        if (dialogContext != null) {
          // Blocking dialog
          await showDialog(
            context: dialogContext,
            barrierDismissible: false,
            builder: (dCtx) => AlertDialog(
              title: const Text('Phiên Đã Hết Hạn'),
              content: const Text('Tài khoản của bạn đã được đăng nhập trên một thiết bị khác hoặc đã bị đăng xuất từ xa.'),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dCtx).pop(),
                  child: const Text('OK'),
                ),
              ],
            ),
          );
        }

        await _authService.logout();
        NavigationService.navigateToAndRemoveUntil('/login');
      }
    } catch (e) {
      // ignore errors — don't block UI
    }
  }

  // (Hàm _checkSession giữ nguyên)
  Future<void> _checkSession() async {
    final isLoggedIn = await _authService.isLoggedIn();
    if (isLoggedIn) {
      final isValid = await _authService.checkSession();
      if (!isValid) {
        final navState = NavigationService.navigatorKey.currentState;
        final dialogContext = navState?.context;
        if (dialogContext != null) {
          await showDialog(
            context: dialogContext,
            barrierDismissible: false,
            builder: (dCtx) => AlertDialog(
              title: const Text('Phiên Đã Hết Hạn'),
              content: const Text('Tài khoản của bạn đã được đăng nhập trên một thiết bị khác hoặc đã bị đăng xuất từ xa.'),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dCtx).pop(),
                  child: const Text('OK'),
                ),
              ],
            ),
          );
        }

        await _authService.logout();
        // Sử dụng navigator key để điều hướng mà không cần context
        NavigationService.navigateToAndRemoveUntil('/login');
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    // (SỬA ĐỔI) Thay vì Future<bool>, gọi hàm mới getSavedSessionData
    return FutureBuilder<Map<String, dynamic>>(
      future: _authService.getSavedSessionData(),
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          // Hiển thị màn hình chờ trong khi kiểm tra token
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        final sessionData = snapshot.data ?? {'isLoggedIn': false};

        if (sessionData['isLoggedIn'] == true) {
          // Đã đăng nhập, đi tới HomePage
          
          // (SỬA ĐỔI) Lấy dữ liệu thật từ sessionData
          final String userID = sessionData['userID'];
          final String role = sessionData['role'];
          final Map<String, dynamic>? userData = sessionData['userData'];

          // Wrap the authenticated UI in a Listener... (Giữ nguyên logic này)
          return Listener(
            behavior: HitTestBehavior.translucent,
            onPointerDown: (_) => _maybeCheckSessionInteractive(),
            child: HomePage(
              userID: userID, 
              role: role,
              userData: userData,
            ),
          );
        } else {
          // Chưa đăng nhập, hiển thị LoginPage
          return const LoginPage();
        }
      },
    );
  }
}