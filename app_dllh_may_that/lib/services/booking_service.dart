import 'dart:convert';
import 'package:app_dllh/models/booking_request.dart';
import 'package:app_dllh/services/api_client.dart';

class BookingService {
  final ApiClient _apiClient = ApiClient();

  /// Tạo đơn đặt tour
  Future<Map<String, dynamic>> createBooking(BookingRequest booking) async {
    final response = await _apiClient.postJson(
      "create_booking.php",
      body: booking.toJson(),
    );

    print("create_booking.php status: ${response.statusCode}");
    print("create_booking.php body: ${response.body}");

    try {
      final decoded = jsonDecode(response.body);
      return decoded as Map<String, dynamic>;
    } catch (e) {
      return {
        "success": false,
        "message": "Invalid JSON from server (HTTP ${response.statusCode}): ${response.body}"
      };
    }
  }

  /// Lấy danh sách booking của khách hàng
  Future<Map<String, dynamic>> getCustomerBookings(String maKhachHang) async {
    final response = await _apiClient.postJson(
      "get_bookings.php",
      body: {"maKhachHang": maKhachHang},
    );

    print("get_bookings.php status: ${response.statusCode}");
    print("get_bookings.php body: ${response.body}");

    try {
      final decoded = jsonDecode(response.body);
      return decoded as Map<String, dynamic>;
    } catch (e) {
      return {
        "success": false,
        "message": "Invalid JSON from server (HTTP ${response.statusCode}): ${response.body}"
      };
    }
  }
}
