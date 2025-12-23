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
      navigatorKey: NavigationService.navigatorKey,
      debugShowCheckedModeBanner: false,
      title: "Tour App",
      theme: ThemeData(primarySwatch: Colors.blue),
      home: const UserActivityDetector(child: AuthWrapper()),
      //Định nghĩa routes
      routes: {
        '/login': (context) => const LoginPage(),
        '/register': (context) => RegisterPage(),
      },
    );
  }
}

class UserActivityDetector extends StatefulWidget {
  final Widget child;
  const UserActivityDetector({super.key, required this.child});

  @override
  State<UserActivityDetector> createState() => _UserActivityDetectorState();
}

class _UserActivityDetectorState extends State<UserActivityDetector> {
  Timer? _timer;
  final AuthService _auth = AuthService();
  
  static const int idleDurationMinutes = 2; 

  @override
  void initState() {
    super.initState();
    _resetTimer();
  }

  void _resetTimer() {
    _timer?.cancel();
    _timer = Timer(const Duration(minutes: idleDurationMinutes), _handleTimeout);
  }

  void _handleTimeout() async {
    // Chỉ logout nếu đang đăng nhập
    if (await _auth.isLoggedIn()) {
      print("User idle for $idleDurationMinutes minutes. Logging out...");
      await _auth.logout();
      
      NavigationService.navigateToAndRemoveUntil('/login');
      
      // Hiển thị thông báo
      final ctx = NavigationService.navigatorKey.currentContext;
      if (ctx != null) {
        showDialog(
          context: ctx,
          barrierDismissible: false,
          builder: (_) => AlertDialog(
            title: const Text("Hết phiên làm việc"),
            content: const Text("Bạn đã không hoạt động trong 2 phút. Vui lòng đăng nhập lại."),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx), 
                child: const Text("OK")
              )
            ],
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Listener(
      behavior: HitTestBehavior.translucent,
      onPointerDown: (_) => _resetTimer(), 
      onPointerMove: (_) => _resetTimer(),
      child: widget.child,
    );
  }
}

class AuthWrapper extends StatefulWidget {
  const AuthWrapper({super.key});

  @override
  _AuthWrapperState createState() => _AuthWrapperState();
}

class _AuthWrapperState extends State<AuthWrapper> with WidgetsBindingObserver {
  final AuthService _authService = AuthService();
  Timer? _sessionCheckTimer;

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
    _sessionCheckTimer?.cancel();
    _sessionCheckTimer = Timer.periodic(const Duration(seconds: 30), (timer) {
      _checkSession();
    });
  }

  Future<void> _checkSession() async {
    final isLoggedIn = await _authService.isLoggedIn();
    if (isLoggedIn) {
      final isValid = await _authService.checkSession();
      if (!isValid) {
        final navState = NavigationService.navigatorKey.currentState;
        final dialogContext = navState?.context;
        
        if (dialogContext != null && navState?.canPop() == true) {
        }

        await _authService.logout();
        NavigationService.navigateToAndRemoveUntil('/login');
        
        if (NavigationService.currentContext != null) {
           showDialog(
            context: NavigationService.currentContext!,
            barrierDismissible: false,
            builder: (dCtx) => AlertDialog(
              title: const Text('Phiên Đã Hết Hạn'),
              content: const Text('Tài khoản của bạn đã hết hạn phiên hoặc đăng nhập nơi khác.'),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dCtx).pop(),
                  child: const Text('OK'),
                ),
              ],
            ),
          );
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Map<String, dynamic>>(
      future: _authService.getSavedSessionData(),
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        final sessionData = snapshot.data ?? {'isLoggedIn': false};

        if (sessionData['isLoggedIn'] == true) {
          final String userID = sessionData['userID'];
          final String role = sessionData['role'];
          final Map<String, dynamic>? userData = sessionData['userData'];
          
          return HomePage(
            userID: userID, 
            role: role,
            userData: userData,
          );
        } else {
          return const LoginPage();
        }
      },
    );
  }
}