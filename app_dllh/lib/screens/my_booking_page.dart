import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';

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
  _MyBookingPageState createState() => _MyBookingPageState();
}

class _MyBookingPageState extends State<MyBookingPage> {
  late Future<List<BookingItem>> _bookingsFuture;

  @override
  void initState() {
    super.initState();
    _bookingsFuture = _fetchUserBookings();
  }

  Future<List<BookingItem>> _fetchUserBookings() async {
    try {
      final uri = Uri.parse('http://10.0.2.2/KLTN/get_user_bookings.php?makhachhang=${widget.userID}');
      print('Fetching bookings from: $uri');
      
      final response = await http.get(uri);

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
                    Text(
                      'Error loading bookings',
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
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
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
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
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
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
    // Xác định status dựa trên trangThaiDat, nếu trống thì hiển thị "On Going"
    final displayStatus = booking.trangThaiDat.isEmpty ? 'On Going' : status;
    final isOnGoing = displayStatus == 'On Going';
    
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Card(
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        child: Padding(
          padding: const EdgeInsets.all(12.0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Tour image
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: booking.hinhAnh.isNotEmpty
                    ? Image.network(
                        booking.hinhAnh,
                        width: 80,
                        height: 80,
                        fit: BoxFit.cover,
                        errorBuilder: (context, error, stackTrace) {
                          return Container(
                            width: 80,
                            height: 80,
                            color: lightGreyBackground,
                            child: const Icon(Icons.image_not_supported, color: Colors.grey),
                          );
                        },
                      )
                    : Container(
                        width: 80,
                        height: 80,
                        color: lightGreyBackground,
                        child: const Icon(Icons.tour, size: 40, color: Colors.grey),
                      ),
              ),
              const SizedBox(width: 12),

              // Booking details
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
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
                    const SizedBox(height: 4),
                    Text(
                      '${booking.soNguoiLon} Adults • ${booking.soTreEm} Children',
                      style: const TextStyle(fontSize: 12, color: Colors.grey),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                          decoration: BoxDecoration(
                            color: isOnGoing
                                ? Colors.blue.withOpacity(0.2)
                                : Colors.green.withOpacity(0.2),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(
                            displayStatus,
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.bold,
                              color: isOnGoing
                                  ? Colors.blue.shade700
                                  : Colors.green.shade700,
                            ),
                          ),
                        ),
                        Text(
                          '\$${booking.tongTien.toStringAsFixed(2)}',
                          style: const TextStyle(
                            fontSize: 14,
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
    );
  }
}

class BookingItem {
  final int maDatTour;
  final int maTour;
  final String ngayDat;
  final int soNguoiLon;
  final int soTreEm;
  final double tongTien;
  final String trangThaiDat;
  final String trangThaiThanhToan;
  final String yeuCauDacBiet;
  final String tieuDe;
  final String moTa;
  final String noiKhoiHanh;
  final String noiDen;
  final String thanhPho;
  final String thoiGian;
  final double giaNguoiLon;
  final double giaTreEm;
  final String hinhAnh;
  final int? maHoaDon;
  final double hoaDonSoTien;
  final String hoaDonTrangThai;

  BookingItem({
    required this.maDatTour,
    required this.maTour,
    required this.ngayDat,
    required this.soNguoiLon,
    required this.soTreEm,
    required this.tongTien,
    required this.trangThaiDat,
    required this.trangThaiThanhToan,
    required this.yeuCauDacBiet,
    required this.tieuDe,
    required this.moTa,
    required this.noiKhoiHanh,
    required this.noiDen,
    required this.thanhPho,
    required this.thoiGian,
    required this.giaNguoiLon,
    required this.giaTreEm,
    required this.hinhAnh,
    this.maHoaDon,
    required this.hoaDonSoTien,
    required this.hoaDonTrangThai,
  });

  factory BookingItem.fromJson(Map<String, dynamic> json) {
    return BookingItem(
      maDatTour: json['maDatTour'] ?? 0,
      maTour: json['maTour'] ?? 0,
      ngayDat: json['ngayDat'] ?? '',
      soNguoiLon: json['soNguoiLon'] ?? 0,
      soTreEm: json['soTreEm'] ?? 0,
      tongTien: (json['tongTien'] ?? 0).toDouble(),
      trangThaiDat: json['trangThaiDat'] ?? '',
      trangThaiThanhToan: json['trangThaiThanhToan'] ?? '',
      yeuCauDacBiet: json['yeuCauDacBiet'] ?? '',
      tieuDe: json['tieuDe'] ?? '',
      moTa: json['moTa'] ?? '',
      noiKhoiHanh: json['noiKhoiHanh'] ?? '',
      noiDen: json['noiDen'] ?? '',
      thanhPho: json['thanhPho'] ?? '',
      thoiGian: json['thoiGian'] ?? '',
      giaNguoiLon: (json['giaNguoiLon'] ?? 0).toDouble(),
      giaTreEm: (json['giaTreEm'] ?? 0).toDouble(),
      hinhAnh: json['hinhAnh'] ?? '',
      maHoaDon: json['maHoaDon'],
      hoaDonSoTien: (json['hoaDonSoTien'] ?? 0).toDouble(),
      hoaDonTrangThai: json['hoaDonTrangThai'] ?? '',
    );
  }
}
