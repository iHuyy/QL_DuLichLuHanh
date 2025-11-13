import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:app_dllh/models/booking_request.dart';

class BookingService {
  final String baseUrl = "http://10.0.2.2/KLTN";

  /// Tạo đơn đặt tour
  Future<Map<String, dynamic>> createBooking(BookingRequest booking) async {
    final response = await http.post(
      Uri.parse("$baseUrl/create_booking.php"),
      headers: {"Content-Type": "application/json"},
      body: jsonEncode(booking.toJson()),
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
    final response = await http.post(
      Uri.parse("$baseUrl/get_bookings.php"),
      headers: {"Content-Type": "application/json"},
      body: jsonEncode({"maKhachHang": maKhachHang}),
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