import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'package:app_dllh/services/booking_service.dart'; // Import Service hủy
import 'dart:convert';
import 'package:intl/intl.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'invoice_detail_page.dart';

// --- BỘ MÀU ĐỒNG BỘ ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color darkTextColor = Color(0xFF2C3E50);
const Color lightGreyBackground = Color(0xFFF8F9FA);

class BookingDetailPage extends StatefulWidget {
  final int bookingId;
  const BookingDetailPage({Key? key, required this.bookingId}) : super(key: key);

  @override
  _BookingDetailPageState createState() => _BookingDetailPageState();
}

class _BookingDetailPageState extends State<BookingDetailPage> {
  late Future<Map<String, dynamic>> _detailFuture;
  final ApiClient _apiClient = ApiClient();
  final BookingService _bookingService = BookingService(); // Khởi tạo Service
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');
  
  // Biến trạng thái loading khi hủy
  bool _isCanceling = false;

  @override
  void initState() {
    super.initState();
    _detailFuture = _fetchDetail();
  }

  Future<Map<String, dynamic>> _fetchDetail() async {
    final endpoint = 'get_booking_detail.php?madattour=${widget.bookingId}';
    final resp = await _apiClient.getJson(endpoint).timeout(const Duration(seconds: 10));

    if (resp.statusCode != 200) throw Exception('HTTP ${resp.statusCode}');
    
    final body = resp.body.trim();
    if (body.startsWith('<')) {
      final idx = body.indexOf('{');
      if (idx >= 0) {
        final jsonPart = body.substring(idx);
        try {
          final Map<String, dynamic> decodedMap = jsonDecode(jsonPart);
          if (decodedMap['success'] == true) return decodedMap['booking'];
        } catch (_) {}
      }
      throw Exception('Lỗi máy chủ (HTML Response)');
    }

    final decoded = jsonDecode(body);
    if (decoded['success'] != true) throw Exception(decoded['message'] ?? 'Lỗi tải dữ liệu');
    
    return decoded['booking'];
  }

  // --- CÁC HÀM HELPER ---
  String _getString(Map<String, dynamic> m, List<String> keys) {
    for (var key in keys) {
      if (m[key] != null && m[key].toString().isNotEmpty) return m[key].toString();
    }
    return '';
  }

  double _getDouble(Map<String, dynamic> m, List<String> keys) {
    String val = _getString(m, keys);
    val = val.replaceAll(RegExp(r'[^0-9\.-]'), '');
    return double.tryParse(val) ?? 0.0;
  }

  int _getInt(Map<String, dynamic> m, List<String> keys) {
    String val = _getString(m, keys);
    if (val.contains('.')) val = val.split('.')[0];
    return int.tryParse(val) ?? 0;
  }

  String _processImage(String raw) {
    if (raw.isEmpty) return '';
    if (!raw.startsWith('http') && !raw.startsWith('data:')) {
       return 'data:image/jpeg;base64,$raw';
    }
    return raw;
  }

  // --- LOGIC HỦY TOUR ---
  void _confirmCancel() {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Xác nhận hủy', style: TextStyle(color: Colors.red)),
        content: const Text('Bạn có chắc chắn muốn hủy đơn đặt tour này không?\nHành động này không thể hoàn tác.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Đóng', style: TextStyle(color: Colors.grey)),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(ctx); // Đóng dialog
              _handleCancel();    // Thực hiện hủy
            },
            style: ElevatedButton.styleFrom(backgroundColor: Colors.red),
            child: const Text('Hủy Tour', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  Future<void> _handleCancel() async {
    setState(() => _isCanceling = true);
    
    // Gọi API hủy (cần đảm bảo cancel_booking.php đã có trên server)
    final result = await _bookingService.cancelBooking(widget.bookingId, "Khách hàng hủy qua App");
    
    setState(() => _isCanceling = false);

    if (result['success'] == true) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã hủy đặt tour thành công'), backgroundColor: Colors.green),
      );
      // Tải lại trang để cập nhật trạng thái
      setState(() {
        _detailFuture = _fetchDetail();
      });
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(result['message'] ?? 'Lỗi khi hủy'), backgroundColor: Colors.red),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: lightGreyBackground,
      appBar: AppBar(
        title: const Text('CHI TIẾT ĐẶT TOUR', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, letterSpacing: 1)),
        backgroundColor: Colors.white,
        foregroundColor: primaryDark,
        elevation: 0,
        centerTitle: true,
      ),
      body: FutureBuilder<Map<String, dynamic>>(
        future: _detailFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator(color: primaryGreen));
          }
          if (snapshot.hasError) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.error_outline, size: 50, color: Colors.red),
                  const SizedBox(height: 16),
                  Text('Lỗi: ${snapshot.error}', textAlign: TextAlign.center),
                ],
              ),
            );
          }
          
          final m = snapshot.data!;
          
          final title = _getString(m, ['TIEUDE', 'TieuDe', 'tieuDe']);
          String tourImage = _getString(m, ['HINHANH', 'hinhAnh', 'DULIEUANH', 'ANHTHUMB']);
          tourImage = _processImage(tourImage);

          final bookingId = _getString(m, ['MADATTOUR', 'MaDatTour']);
          final invoiceId = _getInt(m, ['MAHOADON', 'MaHoaDon']);
          
          final bookingDate = _getString(m, ['NGAYDAT', 'NgayDat']);
          final tourDate = _getString(m, ['THOIGIAN', 'ThoiGian']);
          final startPlace = _getString(m, ['NOIKHOIHANH', 'NoiKhoiHanh']);
          final destination = _getString(m, ['NOIDEN', 'NoiDen']);
          
          final adults = _getInt(m, ['SONGUOILON', 'SoNguoiLon']);
          final children = _getInt(m, ['SOTREEM', 'SoTreEm']);
          final totalRaw = _getDouble(m, ['TONGTIEN', 'TongTien']);
          
          final description = _getString(m, ['MOTA', 'MoTa']);
          final specialRequest = _getString(m, ['YEUCAUDACBIET', 'YeuCauDacBiet']);
          
          final status = _getString(m, ['TRANGTHAIDAT', 'TrangThaiDat']);
          final paymentStatus = _getString(m, ['TRANGTHAITHANHTOAN', 'TrangThaiThanhToan']);

          // Logic kiểm tra xem có được phép hủy hay không
          // Cho phép hủy nếu trạng thái KHÔNG chứa "hủy", "hoàn thành", "kết thúc"
          final bool canCancel = !status.toLowerCase().contains('hủy') && 
                                 !status.toLowerCase().contains('hoàn thành') &&
                                 !status.toLowerCase().contains('kết thúc');

          return Column(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        color: Colors.white,
                        padding: const EdgeInsets.only(bottom: 20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (tourImage.isNotEmpty) 
                              ImageHelper.imageFromData(tourImage, width: double.infinity, height: 220, fit: BoxFit.cover)
                            else
                              Container(height: 180, width: double.infinity, color: Colors.blue.shade50, child: const Icon(Icons.image_not_supported, size: 60, color: Colors.blue)),
                            
                            Padding(
                              padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                              child: Text(
                                title.isNotEmpty ? title : 'Tên Tour',
                                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: darkTextColor),
                              ),
                            ),
                            
                            Padding(
                              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                              child: Row(
                                children: [
                                  _buildBadge(status, false),
                                  const SizedBox(width: 10),
                                  _buildBadge(paymentStatus, true),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: 12),

                      _buildCard(
                        title: 'THÔNG TIN HÀNH TRÌNH',
                        icon: Icons.map_outlined,
                        children: [
                          _buildRow(Icons.calendar_month, 'Khởi hành', tourDate),
                          const Divider(height: 24),
                          _buildRow(Icons.location_on_outlined, 'Nơi đi', startPlace),
                          const SizedBox(height: 12),
                          _buildRow(Icons.flag_outlined, 'Nơi đến', destination),
                        ],
                      ),

                      const SizedBox(height: 12),

                      _buildCard(
                        title: 'CHI TIẾT ĐẶT CHỖ #$bookingId',
                        icon: Icons.confirmation_number_outlined,
                        children: [
                          _buildDetailRow('Ngày đặt', bookingDate),
                          _buildDetailRow('Người lớn', '$adults khách'),
                          _buildDetailRow('Trẻ em', '$children khách'),
                          const Divider(height: 24),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              const Text('TỔNG CỘNG', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.grey)),
                              Text(
                                currencyFormat.format(totalRaw),
                                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 20, color: primaryGreen),
                              ),
                            ],
                          ),
                        ],
                      ),

                      const SizedBox(height: 12),

                      if (description.isNotEmpty || specialRequest.isNotEmpty)
                        _buildCard(
                          title: 'THÔNG TIN THÊM',
                          icon: Icons.info_outline,
                          children: [
                            if (description.isNotEmpty) ...[
                              const Text('Mô tả:', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
                              const SizedBox(height: 4),
                              Text(description, style: TextStyle(color: Colors.grey[700], height: 1.4)),
                              const SizedBox(height: 16),
                            ],
                            if (specialRequest.isNotEmpty)
                              Container(
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: Colors.orange.shade50,
                                  borderRadius: BorderRadius.circular(8),
                                  border: Border.all(color: Colors.orange.shade100),
                                ),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Icon(Icons.note_alt, size: 20, color: Colors.orange),
                                    const SizedBox(width: 10),
                                    Expanded(child: Text(specialRequest, style: TextStyle(color: Colors.orange.shade900))),
                                  ],
                                ),
                              ),
                          ],
                        ),
                      
                      const SizedBox(height: 40),
                    ],
                  ),
                ),
              ),
              
              // --- THANH BOTTOM BAR: Nút Hủy & Xem Hóa Đơn ---
              if (invoiceId > 0 || canCancel) 
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 10, offset: const Offset(0, -5))],
                  ),
                  child: Row(
                    children: [
                      // 1. Nút Hủy Đặt Tour (Màu đỏ nhạt)
                      if (canCancel) 
                        Expanded(
                          child: SizedBox(
                            height: 50,
                            child: ElevatedButton.icon(
                              onPressed: _isCanceling ? null : _confirmCancel,
                              icon: _isCanceling 
                                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.red, strokeWidth: 2))
                                  : const Icon(Icons.cancel_outlined, color: Colors.red),
                              label: Text(
                                _isCanceling ? 'Đang hủy...' : 'Hủy Đặt Tour',
                                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Colors.red),
                              ),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Colors.red.shade50,
                                elevation: 0,
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(8),
                                  side: BorderSide(color: Colors.red.shade200)
                                ),
                              ),
                            ),
                          ),
                        ),
                      
                      // Khoảng cách nếu có cả 2 nút
                      if (canCancel && invoiceId > 0) 
                        const SizedBox(width: 12),

                      // 2. Nút Xem Hóa Đơn (Màu xanh đậm)
                      if (invoiceId > 0)
                        Expanded(
                          child: SizedBox(
                            height: 50,
                            child: ElevatedButton.icon(
                              onPressed: () {
                                Navigator.push(
                                  context,
                                  MaterialPageRoute(
                                    builder: (context) => InvoiceDetailPage(invoiceId: invoiceId)
                                  ),
                                );
                              },
                              icon: const Icon(Icons.receipt_long_rounded),
                              label: const Text('Xem Hóa Đơn', 
                                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: primaryDark, 
                                foregroundColor: Colors.white,
                                elevation: 0,
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  Widget _buildCard({required String title, required IconData icon, required List<Widget> children}) {
    return Container(
      color: Colors.white,
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 20, color: primaryGreen),
              const SizedBox(width: 10),
              Text(title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: primaryDark)),
            ],
          ),
          const SizedBox(height: 20),
          ...children,
        ],
      ),
    );
  }

  Widget _buildBadge(String text, bool isPayment) {
    if (text.isEmpty) return const SizedBox();
    Color color = isPayment 
        ? (text.toLowerCase().contains('đã') ? primaryGreen : Colors.orange) 
        : (text.toLowerCase().contains('hủy') ? Colors.red : Colors.blue);
    
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: color.withOpacity(0.1), borderRadius: BorderRadius.circular(4)),
      child: Text(text, style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12)),
    );
  }

  Widget _buildRow(IconData icon, String label, String value) {
    return Row(
      children: [
        Icon(icon, size: 18, color: Colors.grey[400]),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(fontSize: 12, color: Colors.grey)),
              Text(value.isNotEmpty ? value : '---', style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildDetailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.grey)),
          Text(value.isNotEmpty ? value : '0', style: const TextStyle(fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }
}