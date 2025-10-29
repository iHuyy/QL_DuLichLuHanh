import 'package:app_dllh/screens/login_page.dart';
import 'package:app_dllh/screens/register_page.dart';
import 'package:flutter/material.dart';

void main() {
  runApp(MyApp());
}

class MyApp extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: "Tour App",
      theme: ThemeData(primarySwatch: Colors.blue),
      // Mặc định mở trang Login
      home: LoginPage(),
      routes: {
        "/login": (context) => LoginPage(),
        "/register": (context) => RegisterPage(),
      },
    );
  }
}
