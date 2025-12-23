import 'package:flutter/material.dart';

class NavigationService {
  // Khóa navigator toàn cục
  static final GlobalKey<NavigatorState> navigatorKey = GlobalKey<NavigatorState>();

  // Điều hướng đến một route và xóa tất cả các route trước đó
  static Future<dynamic> navigateToAndRemoveUntil(String routeName, {Object? arguments}) {
    return navigatorKey.currentState!.pushNamedAndRemoveUntil(
      routeName, 
      (Route<dynamic> route) => false, 
      arguments: arguments,
    );
  }

  // Lấy context hiện tại một cách an toàn
  static BuildContext? get currentContext => navigatorKey.currentContext;
}
