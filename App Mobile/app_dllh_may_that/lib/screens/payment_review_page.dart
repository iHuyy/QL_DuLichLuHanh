import 'package:flutter/material.dart';
import 'package:app_dllh/models/booking_request.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/services/booking_service.dart';
import 'package:intl/intl.dart';
import 'booking_detail_page.dart';

const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color lightGreyBackground = Color(0xFFF8F9FA);

class PaymentReviewPage extends StatefulWidget {
  final BookingRequest bookingRequest;
  final Tour tour;
  final double totalAmount;

  const PaymentReviewPage({
    Key? key,
    required this.bookingRequest,
    required this.tour,
    required this.totalAmount,
  }) : super(key: key);

  @override
  _PaymentReviewPageState createState() => _PaymentReviewPageState();
}

class _PaymentReviewPageState extends State<PaymentReviewPage> {
  final BookingService _bookingService = BookingService();
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');
  bool _isLoading = false;
  String _selectedMethod = 'vnpay'; // Mặc định

  Future<void> _processPayment() async {
    setState(() => _isLoading = true);
    
    // Gọi API create_booking.php (PHP đã cập nhật trạng thái "Đã thanh toán")
    final result = await _bookingService.createBooking(widget.bookingRequest);
    
    setState(() => _isLoading = false);

    if (result['success'] == true) {
      // Chuyển hướng đến trang chi tiết booking
      // Xóa hết các màn hình trước đó để tránh back lại trang thanh toán
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (context) => BookingDetailPage(
            bookingId: int.parse(result['bookingId'].toString()),
          ),
        ),
        (route) => route.isFirst,
      );
      
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Thanh toán và đặt tour thành công!'), backgroundColor: primaryGreen),
      );
    } else {
      _showErrorDialog(result['message'] ?? 'Thanh toán thất bại');
    }
  }

  void _showErrorDialog(String message) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text("Lỗi", style: TextStyle(color: Colors.red)),
        content: Text(message),
        actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text("Đóng"))],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: lightGreyBackground,
      appBar: AppBar(
        title: const Text('THANH TOÁN', style: TextStyle(color: primaryDark, fontWeight: FontWeight.bold)),
        backgroundColor: Colors.white,
        elevation: 0,
        iconTheme: const IconThemeData(color: primaryDark),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // 1. Thông tin Tour
            _buildSectionHeader('THÔNG TIN TOUR'),
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(widget.tour.tieuDe, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                  const Divider(height: 24),
                  _buildInfoRow('Ngày đi:', widget.tour.thoiGian?.toString() ?? 'N/A'),
                  _buildInfoRow('Khách hàng:', widget.bookingRequest.hoTen),
                  _buildInfoRow('Số khách:', '${widget.bookingRequest.soNguoiLon} Lớn, ${widget.bookingRequest.soTreEm} Nhỏ'),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // 2. Phương thức thanh toán
            _buildSectionHeader('PHƯƠNG THỨC THANH TOÁN'),
            Container(
              decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
              child: Column(
                children: [
                  _buildPaymentOption('cash', 'Tiền mặt', Icons.money, Colors.blue),
                  const Divider(height: 1),
                  _buildPaymentOption('momo', 'Ví MoMo', Icons.account_balance_wallet, Colors.pink),
                  const Divider(height: 1),
                  _buildPaymentOption('transfer', 'Chuyển khoản ngân hàng', Icons.account_balance, Colors.orange),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // 3. Tổng tiền
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: primaryGreen),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('TỔNG THANH TOÁN', style: TextStyle(fontWeight: FontWeight.bold)),
                  Text(
                    currencyFormat.format(widget.totalAmount),
                    style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: primaryGreen),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 30),

            // 4. Nút Thanh toán
            SizedBox(
              width: double.infinity,
              height: 54,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _processPayment,
                style: ElevatedButton.styleFrom(
                  backgroundColor: primaryGreen,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                ),
                child: _isLoading 
                  ? const CircularProgressIndicator(color: Colors.white) 
                  : const Text('THANH TOÁN NGAY', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSectionHeader(String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10, left: 4),
      child: Text(title, style: const TextStyle(color: Colors.grey, fontWeight: FontWeight.bold)),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(color: Colors.grey[600])),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }

  Widget _buildPaymentOption(String value, String title, IconData icon, Color iconColor) {
    return RadioListTile<String>(
      value: value,
      groupValue: _selectedMethod,
      onChanged: (val) => setState(() => _selectedMethod = val!),
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w500)),
      secondary: Icon(icon, color: iconColor),
      activeColor: primaryGreen,
    );
  }
}