import 'package:flutter/material.dart';
// import 'package:http/http.dart' as http; // XÓA DÒNG NÀY
import 'package:app_dllh/services/api_client.dart'; // THÊM DÒNG NÀY
import 'dart:convert';
import 'booking_detail_page.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'package:app_dllh/models/booking_item.dart';
import 'package:app_dllh/config/app_config.dart';

const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

class MyBookingPage extends StatefulWidget {
  final String userID;

  const MyBookingPage({
    Key? key,
    required this.userID,
  }) : super(key: key);

  @override
  MyBookingPageState createState() => MyBookingPageState();
}

class MyBookingPageState extends State<MyBookingPage> {
  late Future<List<BookingItem>> _bookingsFuture;
  final ApiClient _apiClient = ApiClient(); // THÊM DÒNG NÀY

  @override
  void initState() {
    super.initState();
    _bookingsFuture = _fetchUserBookings();
  }
  
  // Thêm phương thức công khai để refresh data
  void refreshData() {
    if (mounted) {
      setState(() {
        _bookingsFuture = _fetchUserBookings();
      });
    }
  }

  Future<List<BookingItem>> _fetchUserBookings() async {
    try {
      // SỬA LỖI: Chỉ truyền endpoint, không truyền full URL
      final endpoint = 'get_user_bookings.php?makhachhang=${widget.userID}';
      print('Fetching bookings from: ${AppConfig.baseUrl}/$endpoint');
      
      // SỬA LỖI: Dùng _apiClient.getJson thay vì http.get
      final response = await _apiClient.getJson(endpoint);

      if (response.statusCode != 200) {
        throw Exception('HTTP ${response.statusCode}: ${response.reasonPhrase}');
      }

      final body = response.body.trim();
      print('Response: $body');

      if (body.isEmpty) {
        throw Exception('Empty response from server');
      }

      if (body.startsWith('<')) {
        throw Exception('Server returned HTML instead of JSON');
      }

      final decoded = json.decode(body);

      // (SỬA LỖI: PHP trả về {success: true, bookings: [...]})
      if (decoded['success'] != true) {
        throw Exception(decoded['error'] ?? 'Unknown error');
      }

      final bookingsList = decoded['bookings'] as List;
      return bookingsList.map<BookingItem>((e) => BookingItem.fromJson(e)).toList();
    } catch (e) {
      print('Error fetching bookings: $e');
      throw Exception('Failed to load bookings: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios, color: darkTextColor),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          'My Booking',
          style: TextStyle(
            color: darkTextColor,
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
        ),
        centerTitle: true,
      ),
      body: FutureBuilder<List<BookingItem>>(
        future: _bookingsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline, size: 64, color: Colors.red),
                    const SizedBox(height: 16),
                    const Text(
                      'Error loading bookings',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${snapshot.error}',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.grey),
                    ),
                    const SizedBox(height: 20),
                    ElevatedButton(
                      onPressed: () {
                        setState(() {
                          _bookingsFuture = _fetchUserBookings();
                        });
                      },
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            );
          }

          final bookings = snapshot.data ?? [];

          if (bookings.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.shopping_bag_outlined, size: 64, color: Colors.grey),
                  const SizedBox(height: 16),
                  const Text(
                    'No bookings yet',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Start booking tours to see them here',
                    style: TextStyle(color: Colors.grey),
                  ),
                ],
              ),
            );
          }

          // Group bookings by status
          // Nếu không có statusThaiDat, đặt mặc định là "Pending"
          final onGoingBookings = bookings.where((b) {
            final status = b.trangThaiDat.toLowerCase();
            return status.contains('confirm') || status.isEmpty;
          }).toList();
          final closedBookings = bookings.where((b) => b.trangThaiDat.toLowerCase().contains('closed')).toList();

          return ListView(
            children: [
              // On Going Section
              if (onGoingBookings.isNotEmpty) ...[
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  child: Text(
                    'On Going',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: darkTextColor,
                    ),
                  ),
                ),
                ...onGoingBookings.map((booking) => _buildBookingCard(context, booking, 'On Going')),
              ],

              // Closed Section
              if (closedBookings.isNotEmpty) ...[
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  child: Text(
                    'Closed',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: darkTextColor,
                    ),
                  ),
                ),
                ...closedBookings.map((booking) => _buildBookingCard(context, booking, 'Closed')),
              ],

              const SizedBox(height: 20),
            ],
          );
        },
      ),
    );
  }

  Widget _buildBookingCard(BuildContext context, BookingItem booking, String status) {
    final displayStatus = booking.trangThaiDat.isEmpty ? 'On Going' : status;
    
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async { // Bắt kết quả từ chi tiết booking
          await Navigator.of(context).push(MaterialPageRoute(builder: (_) => BookingDetailPage(bookingId: booking.maDatTour)));
          // Refresh list sau khi quay lại (nếu có thay đổi trạng thái thanh toán)
          refreshData();
        },
        child: Card(
          elevation: 1,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Column(
              children: [
                // Image section
                Stack(
                  children: [
                    booking.hinhAnh.isNotEmpty
                          ? ImageHelper.imageFromData(
                              booking.hinhAnh,
                              width: double.infinity,
                              height: 140,
                              fit: BoxFit.cover,
                            )
                        : Container(
                            width: double.infinity,
                            height: 140,
                            color: lightGreyBackground,
                            child: const Icon(Icons.tour, size: 50, color: Colors.grey),
                          ),
                    // Status badge - top right
                    Positioned(
                      top: 8,
                      right: 8,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                        decoration: BoxDecoration(
                          color: displayStatus == 'On Going' ? Colors.blue : Colors.green,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          displayStatus,
                          style: const TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                // Content section
                Padding(
                  padding: const EdgeInsets.all(12.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      // Tour title
                      Text(
                        booking.tieuDe,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.bold,
                          color: darkTextColor,
                        ),
                      ),
                      const SizedBox(height: 6),
                      // Duration (placeholder since DB doesn't have it)
                      const Text(
                        '2 Days 3 Night',
                        style: TextStyle(fontSize: 12, color: Colors.grey),
                      ),
                      const SizedBox(height: 8),
                      // Date and Details row
                      Row(
                        children: [
                          const Icon(Icons.calendar_today, size: 12, color: Colors.grey),
                          const SizedBox(width: 4),
                          Text(
                            booking.ngayDat,
                            style: const TextStyle(fontSize: 11, color: Colors.grey),
                          ),
                          const SizedBox(width: 16),
                          Text(
                            '${booking.soNguoiLon} Adults • ${booking.soTreEm} Children',
                            style: const TextStyle(fontSize: 11, color: Colors.grey),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      // Price row
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              const Icon(Icons.info_outline, size: 14, color: primaryBlue),
                              const SizedBox(width: 4),
                              const Text(
                                'Details',
                                style: TextStyle(
                                  fontSize: 11,
                                  color: primaryBlue,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                              const Icon(Icons.chevron_right, size: 16, color: primaryBlue),
                            ],
                          ),
                          Text(
                            '\$${booking.tongTien.toStringAsFixed(0)}',
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                              color: primaryBlue,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}