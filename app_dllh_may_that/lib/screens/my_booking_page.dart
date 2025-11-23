import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'dart:convert';
import 'booking_detail_page.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'package:app_dllh/models/booking_item.dart';
import 'package:app_dllh/config/app_config.dart';
import 'package:intl/intl.dart';

// --- BỘ MÀU WEB STYLE ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);
const Color darkTextColor = Color(0xFF2C3E50);

class MyBookingPage extends StatefulWidget {
  final String userID;

  const MyBookingPage({Key? key, required this.userID}) : super(key: key);

  @override
  MyBookingPageState createState() => MyBookingPageState();
}

class MyBookingPageState extends State<MyBookingPage> {
  late Future<List<BookingItem>> _bookingsFuture;
  final ApiClient _apiClient = ApiClient();
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

  @override
  void initState() {
    super.initState();
    _bookingsFuture = _fetchUserBookings();
  }

  void refreshData() {
    if (mounted) {
      setState(() {
        _bookingsFuture = _fetchUserBookings();
      });
    }
  }

  Future<List<BookingItem>> _fetchUserBookings() async {
    try {
      final endpoint = 'get_user_bookings.php?makhachhang=${widget.userID}';
      final response = await _apiClient.getJson(endpoint);

      if (response.statusCode != 200) throw Exception('HTTP ${response.statusCode}');

      final body = response.body.trim();
      if (body.isEmpty || body.startsWith('<')) throw Exception('Lỗi dữ liệu từ máy chủ');

      final decoded = json.decode(body);
      if (decoded['success'] != true) throw Exception(decoded['error'] ?? 'Lỗi không xác định');

      final bookingsList = decoded['data'] as List;
      return bookingsList.map<BookingItem>((e) => BookingItem.fromJson(e)).toList();
    } catch (e) {
      throw Exception('Không thể tải danh sách: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        iconTheme: const IconThemeData(color: primaryDark),
        title: const Text(
          'VÉ CỦA TÔI',
          style: TextStyle(
            color: primaryDark,
            fontSize: 16,
            fontWeight: FontWeight.w800,
            letterSpacing: 1,
          ),
        ),
      ),
      body: FutureBuilder<List<BookingItem>>(
        future: _bookingsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator(color: primaryGreen));
          }

          if (snapshot.hasError) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.error_outline, size: 50, color: Colors.redAccent),
                  const SizedBox(height: 16),
                  Text('Lỗi: ${snapshot.error}', textAlign: TextAlign.center),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: refreshData,
                    style: ElevatedButton.styleFrom(backgroundColor: primaryDark),
                    child: const Text('Thử lại', style: TextStyle(color: Colors.white)),
                  ),
                ],
              ),
            );
          }

          final bookings = snapshot.data ?? [];

          if (bookings.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.confirmation_number_outlined, size: 80, color: Colors.grey[300]),
                  const SizedBox(height: 16),
                  Text(
                    'Bạn chưa đặt tour nào',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.grey[600]),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Hãy khám phá và đặt chuyến đi ngay!',
                    style: TextStyle(color: Colors.grey[500]),
                  ),
                ],
              ),
            );
          }

          // Phân loại
          final closedBookings = bookings.where((b) {
            final status = b.trangThaiDat.toLowerCase();
            return status.contains('hủy') || status.contains('hoàn thành') || status.contains('closed');
          }).toList();

          final onGoingBookings = bookings.where((b) => !closedBookings.contains(b)).toList();

          return ListView(
            padding: const EdgeInsets.symmetric(vertical: 16),
            children: [
              if (onGoingBookings.isNotEmpty) ...[
                _buildSectionHeader('SẮP DIỄN RA (${onGoingBookings.length})'),
                ...onGoingBookings.map((b) => _buildBookingCard(context, b, isActive: true)),
              ],
              
              if (closedBookings.isNotEmpty) ...[
                const SizedBox(height: 24),
                _buildSectionHeader('LỊCH SỬ (${closedBookings.length})'),
                ...closedBookings.map((b) => _buildBookingCard(context, b, isActive: false)),
              ],
            ],
          );
        },
      ),
    );
  }

  Widget _buildSectionHeader(String title) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
      child: Text(
        title,
        style: const TextStyle(
          fontSize: 14,
          fontWeight: FontWeight.bold,
          color: Colors.grey,
        ),
      ),
    );
  }

  Widget _buildBookingCard(BuildContext context, BookingItem booking, {required bool isActive}) {
    // Xác định trạng thái hiển thị
    String statusText = booking.trangThaiDat;
    Color statusColor = primaryDark;
    Color statusBg = primaryDark.withOpacity(0.1);

    if (isActive) {
       if (statusText.contains('Đã xác nhận')) {
         statusColor = primaryGreen;
         statusBg = primaryGreen.withOpacity(0.1);
       } else {
         statusColor = Colors.orange;
         statusBg = Colors.orange.withOpacity(0.1);
       }
    } else {
       statusColor = Colors.grey;
       statusBg = Colors.grey.withOpacity(0.1);
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: InkWell(
        onTap: () async {
          await Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => BookingDetailPage(bookingId: booking.maDatTour)),
          );
          refreshData();
        },
        borderRadius: BorderRadius.circular(12),
        child: Container(
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            boxShadow: [
              BoxShadow(color: Colors.black.withOpacity(0.04), blurRadius: 8, offset: const Offset(0, 2))
            ],
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Ảnh Thumb (Trái)
              ClipRRect(
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(12),
                  bottomLeft: Radius.circular(12),
                ),
                child: ImageHelper.imageFromData(
                  booking.hinhAnh,
                  width: 100,
                  height: 120, // Chiều cao cố định cho card đẹp hơn
                  fit: BoxFit.cover,
                ),
              ),
              
              // Nội dung (Phải)
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      // Badge trạng thái
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                        decoration: BoxDecoration(
                          color: statusBg,
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Text(
                          statusText.toUpperCase(),
                          style: TextStyle(fontSize: 10, fontWeight: FontWeight.bold, color: statusColor),
                        ),
                      ),
                      const SizedBox(height: 6),
                      
                      // Tên Tour
                      Text(
                        booking.tieuDe,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                          color: darkTextColor,
                          height: 1.2,
                        ),
                      ),
                      
                      const SizedBox(height: 8),
                      
                      // Thông tin phụ
                      Row(
                        children: [
                          Icon(Icons.calendar_today, size: 12, color: Colors.grey[600]),
                          const SizedBox(width: 4),
                          Text(
                            booking.ngayDat,
                            style: TextStyle(fontSize: 12, color: Colors.grey[600]),
                          ),
                          const Spacer(),
                          Text(
                            currencyFormat.format(booking.tongTien),
                            style: const TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.bold,
                              color: primaryGreen,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}