import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
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
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

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
    // Xử lý trường hợp server trả về HTML lỗi kèm JSON
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

  // --- CÁC HÀM HELPER AN TOÀN (FIX LỖI STRING/INT) ---

  // Lấy chuỗi an toàn (tránh null)
  String _getString(Map<String, dynamic> m, List<String> keys) {
    for (var key in keys) {
      if (m[key] != null && m[key].toString().isNotEmpty) return m[key].toString();
    }
    return '';
  }

  // Lấy số thực an toàn (tự động parse từ String)
  double _getDouble(Map<String, dynamic> m, List<String> keys) {
    String val = _getString(m, keys);
    // Xóa ký tự không phải số nếu cần (ví dụ 1,000.00 -> 1000.00)
    val = val.replaceAll(RegExp(r'[^0-9\.-]'), '');
    return double.tryParse(val) ?? 0.0;
  }

  // Lấy số nguyên an toàn
  int _getInt(Map<String, dynamic> m, List<String> keys) {
    String val = _getString(m, keys);
    // Lấy phần nguyên nếu là số thực (ví dụ 10.0 -> 10)
    if (val.contains('.')) val = val.split('.')[0];
    return int.tryParse(val) ?? 0;
  }

  // Xử lý chuỗi ảnh Base64
  String _processImage(String raw) {
    if (raw.isEmpty) return '';
    if (!raw.startsWith('http') && !raw.startsWith('data:')) {
       // Nếu thiếu header, tự động thêm vào
       return 'data:image/jpeg;base64,$raw';
    }
    return raw;
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
          
          // --- TRÍCH XUẤT DỮ LIỆU AN TOÀN ---
          final title = _getString(m, ['TIEUDE', 'TieuDe', 'tieuDe']);
          
          String tourImage = _getString(m, ['HINHANH', 'hinhAnh', 'HinhAnh', 'image', 'DULIEUANH', 'ANHTHUMB', 'AnhThumb']);
          tourImage = _processImage(tourImage);

          final bookingId = _getString(m, ['MADATTOUR', 'MaDatTour', 'maDatTour']);
          final invoiceId = _getInt(m, ['MAHOADON', 'MaHoaDon', 'maHoaDon']); // Lấy về dạng int an toàn
          
          final bookingDate = _getString(m, ['NGAYDAT', 'NgayDat', 'ngayDat']);
          final tourDate = _getString(m, ['THOIGIAN', 'ThoiGian', 'thoiGian']);
          final startPlace = _getString(m, ['NOIKHOIHANH', 'NoiKhoiHanh', 'noiKhoiHanh']);
          final destination = _getString(m, ['NOIDEN', 'NoiDen', 'noiDen']);
          
          final adults = _getInt(m, ['SONGUOILON', 'SoNguoiLon']);
          final children = _getInt(m, ['SOTREEM', 'SoTreEm']);
          final totalRaw = _getDouble(m, ['TONGTIEN', 'TongTien']);
          
          final description = _getString(m, ['MOTA', 'MoTa']);
          final specialRequest = _getString(m, ['YEUCAUDACBIET', 'YeuCauDacBiet']);
          final status = _getString(m, ['TRANGTHAIDAT', 'TrangThaiDat']);
          final paymentStatus = _getString(m, ['TRANGTHAITHANHTOAN', 'TrangThaiThanhToan']);

          return Column(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      // 1. Hình ảnh & Tiêu đề
                      Container(
                        color: Colors.white,
                        padding: const EdgeInsets.only(bottom: 20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (tourImage.isNotEmpty) 
                              ImageHelper.imageFromData(
                                tourImage, 
                                width: double.infinity, 
                                height: 220, 
                                fit: BoxFit.cover
                              )
                            else
                              Container(
                                height: 180,
                                width: double.infinity,
                                color: Colors.blue.shade50,
                                child: const Icon(Icons.image_not_supported, size: 60, color: Colors.blue),
                              ),
                            
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

                      // 2. Thông tin Tour
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

                      // 3. Chi tiết Booking
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

                      // 4. Thông tin thêm
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
              
              // 5. BOTTOM BAR (Nút chuyển sang Hóa Đơn)
              // Chỉ hiện khi có mã hóa đơn hợp lệ (> 0)
              if (invoiceId > 0) 
                Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 10, offset: const Offset(0, -5))],
                  ),
                  child: SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: ElevatedButton.icon(
                      onPressed: () {
                        // Chuyển sang màn hình chi tiết hóa đơn
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (context) => InvoiceDetailPage(invoiceId: invoiceId)
                          ),
                        );
                      },
                      icon: const Icon(Icons.receipt_long_rounded),
                      label: const Text('Xem Hóa Đơn', 
                          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
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
          );
        },
      ),
    );
  }

  // --- CÁC WIDGET CON (Giữ nguyên style đẹp) ---

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