import 'package:flutter/material.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/models/booking_request.dart';
import 'package:app_dllh/services/booking_service.dart';

const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

class BookingPage extends StatefulWidget {
  final Tour tour;
  final String userID;

  const BookingPage({Key? key, required this.tour, required this.userID}) : super(key: key);

  @override
  _BookingPageState createState() => _BookingPageState();
}

class _BookingPageState extends State<BookingPage> {
  final BookingService _bookingService = BookingService();
  final _formKey = GlobalKey<FormState>();

  late TextEditingController _hoTenController;
  late TextEditingController _soDienThoaiController;
  late TextEditingController _emailController;
  late TextEditingController _ghiChuController;

  int _soNguoiLon = 1;
  int _soTreEm = 0;
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _hoTenController = TextEditingController();
    _soDienThoaiController = TextEditingController();
    _emailController = TextEditingController();
    _ghiChuController = TextEditingController();
  }

  @override
  void dispose() {
    _hoTenController.dispose();
    _soDienThoaiController.dispose();
    _emailController.dispose();
    _ghiChuController.dispose();
    super.dispose();
  }

  // Tính tổng tiền dựa trên số lượng người
  double _calculateTotal() {
    double giaNguoiLon = double.tryParse(widget.tour.giaNguoiLon?.toString() ?? '0') ?? 0;
    double giaTreEm = double.tryParse(widget.tour.giaTreEm?.toString() ?? '0') ?? 0;
    return (giaNguoiLon * _soNguoiLon) + (giaTreEm * _soTreEm);
  }

  Future<void> _submitBooking() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isLoading = true);

    try {
      final booking = BookingRequest(
        maTour: widget.tour.maTour,
        maKhachHang: widget.userID,
        soNguoiLon: _soNguoiLon,
        soTreEm: _soTreEm,
        hoTen: _hoTenController.text.trim(),
        soDienThoai: _soDienThoaiController.text.trim(),
        email: _emailController.text.trim(),
        ghiChu: _ghiChuController.text.trim(),
      );

      final result = await _bookingService.createBooking(booking);

      setState(() => _isLoading = false);

      if (result['success'] == true) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(result['message'] ?? 'Booking successful!')),
        );
        // Điều hướng tới trang xác nhận hoặc quay lại
        Navigator.of(context).pop({'success': true, 'bookingId': result['bookingId']});
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(result['message'] ?? 'Booking failed'), backgroundColor: Colors.red),
        );
      }
    } catch (e) {
      setState(() => _isLoading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
  final total = _calculateTotal();
  final finalTotal = total; // No service charge

    return Scaffold(
      appBar: AppBar(
        title: const Text('Booking', style: TextStyle(color: darkTextColor)),
        backgroundColor: Colors.white,
        elevation: 1,
        iconTheme: const IconThemeData(color: primaryBlue),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Booking Details Section
              const Text(
                'Booking Details',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: darkTextColor),
              ),
              const SizedBox(height: 16),
              _buildDetailTile(Icons.card_giftcard, 'Package', widget.tour.tieuDe),
              _buildDetailTile(Icons.schedule, 'Duration', widget.tour.thoiGian ?? 'N/A'),
              _buildDetailTile(Icons.location_on, 'Destination', widget.tour.noiDen ?? 'N/A'),
              const SizedBox(height: 16),

              // Passenger Count Section
              const Text(
                'Passengers',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: darkTextColor),
              ),
              const SizedBox(height: 12),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Adults', style: TextStyle(fontSize: 14)),
                  Row(
                    children: [
                      IconButton(
                        icon: const Icon(Icons.remove_circle, color: primaryBlue),
                        onPressed: _soNguoiLon > 1 ? () => setState(() => _soNguoiLon--) : null,
                      ),
                      Text('$_soNguoiLon', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                      IconButton(
                        icon: const Icon(Icons.add_circle, color: primaryBlue),
                        onPressed: () => setState(() => _soNguoiLon++),
                      ),
                    ],
                  ),
                ],
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Children', style: TextStyle(fontSize: 14)),
                  Row(
                    children: [
                      IconButton(
                        icon: const Icon(Icons.remove_circle, color: primaryBlue),
                        onPressed: _soTreEm > 0 ? () => setState(() => _soTreEm--) : null,
                      ),
                      Text('$_soTreEm', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                      IconButton(
                        icon: const Icon(Icons.add_circle, color: primaryBlue),
                        onPressed: () => setState(() => _soTreEm++),
                      ),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: 20),

              // Contact Information Section
              const Text(
                'Contact Information',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: darkTextColor),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _hoTenController,
                decoration: InputDecoration(
                  labelText: 'Full Name',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                ),
                validator: (val) => val?.isEmpty ?? true ? 'Please enter name' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _soDienThoaiController,
                decoration: InputDecoration(
                  labelText: 'Phone Number',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                ),
                validator: (val) => val?.isEmpty ?? true ? 'Please enter phone' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _emailController,
                decoration: InputDecoration(
                  labelText: 'Email',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                ),
                validator: (val) {
                  if (val?.isEmpty ?? true) return 'Please enter email';
                  if (!val!.contains('@')) return 'Invalid email';
                  return null;
                },
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _ghiChuController,
                decoration: InputDecoration(
                  labelText: 'Special Requests (Optional)',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                ),
                maxLines: 3,
              ),
              const SizedBox(height: 24),

              // Payment Summary Section
              const Text(
                'Payment Summary',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: darkTextColor),
              ),
              const SizedBox(height: 12),
              _buildPaymentRow('${widget.tour.tieuDe}', '\$${total.toStringAsFixed(2)}'),
              const Divider(thickness: 1.5, height: 20),
              _buildPaymentRow('Total', '\$${finalTotal.toStringAsFixed(2)}', isBold: true),
              const SizedBox(height: 24),

              // Submit Button
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _submitBooking,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryBlue,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  child: _isLoading
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                      : const Text('Proceed to Checkout', style: TextStyle(fontSize: 16, color: Colors.white, fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildDetailTile(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: Row(
        children: [
          Icon(icon, color: primaryBlue, size: 20),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(fontSize: 12, color: Colors.black54)),
              Text(value, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: darkTextColor)),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildPaymentRow(String label, String amount, {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: TextStyle(
              fontSize: isBold ? 16 : 14,
              fontWeight: isBold ? FontWeight.bold : FontWeight.normal,
              color: darkTextColor,
            ),
          ),
          Text(
            amount,
            style: TextStyle(
              fontSize: isBold ? 16 : 14,
              fontWeight: isBold ? FontWeight.bold : FontWeight.normal,
              color: isBold ? primaryBlue : darkTextColor,
            ),
          ),
        ],
      ),
    );
  }
}