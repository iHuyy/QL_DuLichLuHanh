import 'package:app_dllh/screens/payment_review_page.dart';
import 'package:flutter/material.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/models/booking_request.dart';
import 'package:app_dllh/services/booking_service.dart';
import 'package:app_dllh/services/auth_service.dart'; // [MỚI] Import AuthService
import 'package:intl/intl.dart';

// --- BỘ MÀU ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color darkTextColor = Color(0xFF2C3E50);
const Color lightGreyBackground = Color(0xFFF8F9FA);

class BookingPage extends StatefulWidget {
  final Tour tour;
  final String userID;

  const BookingPage({Key? key, required this.tour, required this.userID})
    : super(key: key);

  @override
  _BookingPageState createState() => _BookingPageState();
}

class _BookingPageState extends State<BookingPage> {
  final BookingService _bookingService = BookingService();
  final AuthService _authService = AuthService(); // [MỚI] Khởi tạo AuthService
  final _formKey = GlobalKey<FormState>();
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

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

    // [MỚI] Tự động tải thông tin người dùng để điền vào form
    _autoFillUserInfo();
  }

  // [MỚI] Hàm lấy thông tin và điền form
  Future<void> _autoFillUserInfo() async {
    try {
      // Gọi API lấy thông tin user (get_user.php)
      final result = await _authService.getUser(widget.userID);

      if (result['success'] == true && result['data'] != null) {
        final data = result['data'];
        if (mounted) {
          setState(() {
            // Chỉ điền nếu ô đang trống (tránh ghi đè nếu user đã nhập nhanh)
            if (_hoTenController.text.isEmpty) {
              _hoTenController.text = data['fullName'] ?? '';
            }
            if (_emailController.text.isEmpty) {
              _emailController.text = data['email'] ?? '';
            }
            if (_soDienThoaiController.text.isEmpty) {
              _soDienThoaiController.text = data['phone'] ?? '';
            }
            // Nếu muốn điền địa chỉ vào ghi chú (tùy chọn)
            // if (_ghiChuController.text.isEmpty && data['address'] != null) {
            //   _ghiChuController.text = "Địa chỉ: ${data['address']}";
            // }
          });
        }
      }
    } catch (e) {
      print("Lỗi tự động điền thông tin: $e");
    }
  }

  @override
  void dispose() {
    _hoTenController.dispose();
    _soDienThoaiController.dispose();
    _emailController.dispose();
    _ghiChuController.dispose();
    super.dispose();
  }

  // --- HELPER: Lấy số chỗ còn lại ---
  int _getAvailableSlots() {
    if (widget.tour.soChoConLai != null) {
      return int.tryParse(widget.tour.soChoConLai.toString()) ?? 0;
    }
    return int.tryParse(widget.tour.soLuong.toString()) ?? 0;
  }

  double _parsePrice(dynamic price) {
    if (price == null) return 0.0;
    if (price is num) return price.toDouble();
    try {
      String clean = price.toString().replaceAll(RegExp(r'[^0-9\.-]'), '');
      return double.parse(clean);
    } catch (_) {
      return 0.0;
    }
  }

  double _calculateTotal() {
    double giaNguoiLon = _parsePrice(widget.tour.giaNguoiLon);
    double giaTreEm = _parsePrice(widget.tour.giaTreEm);
    return (giaNguoiLon * _soNguoiLon) + (giaTreEm * _soTreEm);
  }

  Future<void> _submitBooking() async {
    if (!_formKey.currentState!.validate()) return;

    // Validate số chỗ
    int totalGuests = _soNguoiLon + _soTreEm;
    int available = _getAvailableSlots();
    if (totalGuests > available) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Xin lỗi, chỉ còn $available chỗ trống!'),
          backgroundColor: Colors.red,
        ),
      );
      return;
    }

    // Tạo object request
    final bookingRequest = BookingRequest(
      maTour: widget.tour.maTour,
      maKhachHang: widget.userID,
      soNguoiLon: _soNguoiLon,
      soTreEm: _soTreEm,
      hoTen: _hoTenController.text.trim(),
      soDienThoai: _soDienThoaiController.text.trim(),
      email: _emailController.text.trim(),
      ghiChu: _ghiChuController.text.trim(),
    );

    // Tính tổng tiền để hiển thị bên trang thanh toán
    double totalAmount = _calculateTotal();

    // Chuyển sang trang Thanh toán
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => PaymentReviewPage(
          bookingRequest: bookingRequest,
          tour: widget.tour,
          totalAmount: totalAmount,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final total = _calculateTotal();
    final availableSlots = _getAvailableSlots();
    final bool isSoldOut = availableSlots <= 0;

    return Scaffold(
      backgroundColor: lightGreyBackground,
      appBar: AppBar(
        title: const Text(
          'ĐẶT CHỖ',
          style: TextStyle(color: primaryDark, fontWeight: FontWeight.w800),
        ),
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        iconTheme: const IconThemeData(color: primaryDark),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // 1. Tóm tắt Tour (Card nổi)
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.blueGrey.shade100),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      widget.tour.tieuDe,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: darkTextColor,
                      ),
                    ),
                    const Divider(height: 24),
                    _buildInfoRow(
                      Icons.calendar_today,
                      'Ngày khởi hành',
                      widget.tour.thoiGian?.toString() ?? 'N/A',
                    ),
                    const SizedBox(height: 8),
                    _buildInfoRow(
                      Icons.place_outlined,
                      'Điểm đến',
                      widget.tour.noiDen ?? 'N/A',
                    ),
                    const SizedBox(height: 8),
                    // HIỂN THỊ SỐ CHỖ CÒN LẠI
                    Row(
                      children: [
                        Icon(
                          Icons.event_seat,
                          size: 18,
                          color: isSoldOut ? Colors.red : primaryGreen,
                        ),
                        const SizedBox(width: 12),
                        Text(
                          'Tình trạng: ',
                          style: TextStyle(
                            color: Colors.grey[600],
                            fontSize: 14,
                          ),
                        ),
                        Text(
                          isSoldOut
                              ? 'ĐÃ HẾT CHỖ'
                              : 'Còn nhận $availableSlots khách',
                          style: TextStyle(
                            fontWeight: FontWeight.bold,
                            color: isSoldOut ? Colors.red : primaryGreen,
                            fontSize: 14,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              // 2. Số lượng hành khách
              if (!isSoldOut) ...[
                _buildSectionTitle('SỐ LƯỢNG KHÁCH'),
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    children: [
                      _buildCounterRow(
                        'Người lớn',
                        _soNguoiLon,
                        (val) => setState(() => _soNguoiLon = val),
                        min: 1,
                        max: availableSlots - _soTreEm, // Giới hạn tổng
                      ),
                      const Divider(height: 24),
                      _buildCounterRow(
                        'Trẻ em',
                        _soTreEm,
                        (val) => setState(() => _soTreEm = val),
                        min: 0,
                        max: availableSlots - _soNguoiLon, // Giới hạn tổng
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
              ],

              // 3. Thông tin liên hệ
              _buildSectionTitle('THÔNG TIN LIÊN HỆ'),
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Column(
                  children: [
                    _buildTextField(
                      'Họ và tên *',
                      _hoTenController,
                      Icons.person_outline,
                      required: true,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      'Số điện thoại *',
                      _soDienThoaiController,
                      Icons.phone_outlined,
                      required: true,
                      isPhone: true,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      'Email *',
                      _emailController,
                      Icons.email_outlined,
                      required: true,
                      isEmail: true,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      'Ghi chú thêm',
                      _ghiChuController,
                      Icons.note_alt_outlined,
                      maxLines: 3,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              // 4. Tổng tiền
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: primaryDark.withOpacity(0.05),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: primaryDark.withOpacity(0.1)),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'TỔNG CỘNG',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                        color: darkTextColor,
                      ),
                    ),
                    Text(
                      currencyFormat.format(total),
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: primaryGreen,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              // 5. Nút xác nhận
              SizedBox(
                width: double.infinity,
                height: 54,
                child: ElevatedButton(
                  // Disable nút nếu hết chỗ hoặc đang loading
                  onPressed: (isSoldOut || _isLoading) ? null : _submitBooking,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryGreen,
                    disabledBackgroundColor: Colors.grey,
                    foregroundColor: Colors.white,
                    elevation: 4,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  child: _isLoading
                      ? const SizedBox(
                          width: 24,
                          height: 24,
                          child: CircularProgressIndicator(
                            color: Colors.white,
                            strokeWidth: 2,
                          ),
                        )
                      : Text(
                          isSoldOut ? 'HẾT CHỖ' : 'XÁC NHẬN ĐẶT VÉ',
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 1,
                          ),
                        ),
                ),
              ),
              const SizedBox(height: 40),
            ],
          ),
        ),
      ),
    );
  }

  // --- WIDGETS CON ---

  Widget _buildSectionTitle(String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12, left: 4),
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

  Widget _buildInfoRow(IconData icon, String label, String value) {
    return Row(
      children: [
        Icon(icon, size: 18, color: primaryGreen),
        const SizedBox(width: 12),
        Text(
          '$label: ',
          style: TextStyle(color: Colors.grey[600], fontSize: 14),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
          ),
        ),
      ],
    );
  }

  // Widget Counter có giới hạn Max
  Widget _buildCounterRow(
    String label,
    int value,
    Function(int) onChanged, {
    int min = 0,
    required int max,
  }) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w500,
            color: darkTextColor,
          ),
        ),
        Container(
          decoration: BoxDecoration(
            border: Border.all(color: Colors.grey.shade300),
            borderRadius: BorderRadius.circular(8),
          ),
          child: Row(
            children: [
              IconButton(
                icon: const Icon(Icons.remove, size: 18),
                color: value > min ? primaryDark : Colors.grey,
                onPressed: value > min ? () => onChanged(value - 1) : null,
              ),
              Container(
                width: 40,
                alignment: Alignment.center,
                child: Text(
                  '$value',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              IconButton(
                icon: const Icon(Icons.add, size: 18),
                color: value < max
                    ? primaryGreen
                    : Colors.grey, // Disable nếu đạt max
                onPressed: value < max ? () => onChanged(value + 1) : null,
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildTextField(
    String label,
    TextEditingController controller,
    IconData icon, {
    bool required = false,
    bool isEmail = false,
    bool isPhone = false,
    int maxLines = 1,
  }) {
    return TextFormField(
      controller: controller,
      maxLines: maxLines,
      keyboardType: isPhone
          ? TextInputType.phone
          : (isEmail ? TextInputType.emailAddress : TextInputType.text),
      validator: (val) {
        if (required && (val == null || val.isEmpty))
          return 'Vui lòng nhập thông tin này';
        if (isEmail && val != null && val.isNotEmpty && !val.contains('@'))
          return 'Email không hợp lệ';
        return null;
      },
      decoration: InputDecoration(
        labelText: label,
        prefixIcon: Icon(icon, color: Colors.grey),
        filled: true,
        fillColor: lightGreyBackground,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: BorderSide.none,
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: const BorderSide(color: primaryGreen),
        ),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 14,
        ),
      ),
    );
  }
}
