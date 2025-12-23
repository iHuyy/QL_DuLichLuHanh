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

  // --- BỔ SUNG HÀM HỦY TOUR ---
  /// Hủy đặt tour
  Future<Map<String, dynamic>> cancelBooking(int bookingId, String reason) async {
    try {
      final response = await _apiClient.postJson(
        "cancel_booking.php",
        body: {
          "bookingId": bookingId,
          "lyDo": reason
        },
      );

      final body = response.body.trim();
      
      // Xử lý lỗi nếu server trả về HTML thay vì JSON
      if (body.startsWith('<')) {
         return {
          "success": false,
          "message": "Lỗi máy chủ (HTML response)"
        };
      }
      
      return jsonDecode(body);
    } catch (e) {
      return {
        "success": false,
        "message": "Lỗi kết nối: $e"
      };
    }
  }
}