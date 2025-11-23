import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'package:intl/intl.dart';
import 'package:printing/printing.dart';
import 'package:pdf/pdf.dart';

// --- BỘ MÀU ĐỒNG BỘ VỚI WEB ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);
const Color darkTextColor = Color(0xFF2C3E50);

class InvoiceDetailPage extends StatefulWidget {
  final int invoiceId;
  const InvoiceDetailPage({Key? key, required this.invoiceId})
    : super(key: key);

  @override
  _InvoiceDetailPageState createState() => _InvoiceDetailPageState();
}

class _InvoiceDetailPageState extends State<InvoiceDetailPage> {
  final ApiClient _api = ApiClient();
  Map<String, dynamic>? _invoice;
  bool _loading = true;
  bool _paying = false;
  bool _exporting = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final resp = await _api.getJson(
        'get_invoice_detail.php?mahoadon=${widget.invoiceId}',
      );
      final body = resp.body;

      // Xử lý trường hợp server trả về HTML lỗi
      if (body.trim().startsWith('<')) {
        _showError('Lỗi server (HTML response)');
        return;
      }

      final decoded = jsonDecode(body);
      if (decoded['success'] == true && decoded['invoice'] != null) {
        setState(() {
          _invoice = Map<String, dynamic>.from(decoded['invoice']);
        });
      } else {
        _showError(decoded['message'] ?? 'Không tìm thấy hóa đơn');
      }
    } catch (e) {
      _showError('Lỗi kết nối: $e');
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _pay() async {
    if (_invoice == null) return;
    setState(() => _paying = true);

    try {
      // 1. Lấy ID hóa đơn
      final invoiceId = _invoice!['MAHOADON'] ?? widget.invoiceId;

      // 2. Gọi PHP
      final resp = await _api.postJson(
        'pay_invoice.php',
        body: {'maHoaDon': invoiceId},
      );

      // 3. Kiểm tra nội dung phản hồi trước khi decode
      final body = resp.body.trim();
      print(">>> Pay response: $body"); // Log ra để xem lỗi là gì

      if (resp.statusCode == 200) {
        // Nếu server trả về HTML (lỗi), báo lỗi rõ ràng
        if (body.startsWith('<') || !body.startsWith('{')) {
          _showError('Lỗi máy chủ: Phản hồi không đúng định dạng JSON.');
          return;
        }

        try {
          final decoded = jsonDecode(body);
          if (decoded['success'] == true) {
            _showSuccess(decoded['message'] ?? 'Thanh toán thành công!');

            // Cập nhật UI ngay lập tức
            setState(() {
              _invoice!['TRANGTHAI'] = 'Đã thanh toán';
            });

            // Tải lại dữ liệu & Xuất PDF
            await _load();
            await _exportInvoicePdf();
          } else {
            _showError(decoded['message'] ?? 'Thanh toán thất bại');
          }
        } catch (e) {
          _showError('Lỗi phân tích dữ liệu: $e');
        }
      } else {
        _showError('Lỗi server: ${resp.statusCode}');
      }
    } catch (e) {
      _showError('Lỗi kết nối: $e');
    } finally {
      setState(() => _paying = false);
    }
  }

  Future<void> _exportInvoicePdf() async {
    if (_invoice == null) return;
    setState(() => _exporting = true);

    try {
      final bookingId = _invoice!['MADATTOUR'];
      final response = await _api.getJson('api/hoadon/html/$bookingId');

      if (response.statusCode == 200) {
        await Printing.layoutPdf(
          onLayout: (PdfPageFormat format) async =>
              await Printing.convertHtml(format: format, html: response.body),
          name: 'HoaDon_${widget.invoiceId}.pdf',
        );
      } else {
        _showError('Không tải được file PDF');
      }
    } catch (e) {
      _showError('Lỗi xuất PDF: $e');
    } finally {
      setState(() => _exporting = false);
    }
  }

  void _showError(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.red),
    );
  }

  void _showSuccess(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: primaryGreen),
    );
  }

  @override
  Widget build(BuildContext context) {
    final status = _invoice?['TRANGTHAI'] ?? '';
    final isPaid =
        status.toString().toLowerCase().contains('đã') ||
        status.toString().toLowerCase() == 'paid';

    final currencyFormatter = NumberFormat.currency(
      locale: 'vi_VN',
      symbol: '₫',
    );

    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        title: const Text(
          'CHI TIẾT HÓA ĐƠN',
          style: TextStyle(
            color: primaryDark,
            fontWeight: FontWeight.bold,
            fontSize: 16,
          ),
        ),
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        iconTheme: const IconThemeData(color: primaryDark),
        actions: [
          if (_invoice != null && isPaid)
            IconButton(
              icon: _exporting
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.print_outlined),
              onPressed: _exporting ? null : _exportInvoicePdf,
              tooltip: 'In hóa đơn',
            ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: primaryGreen))
          : _invoice == null
          ? _buildNotFound()
          : SingleChildScrollView(
              padding: const EdgeInsets.all(20.0),
              child: Column(
                children: [
                  _buildInfoCard(isPaid, currencyFormatter),
                  const SizedBox(height: 24),
                  _buildActionButtons(isPaid),
                ],
              ),
            ),
    );
  }

  Widget _buildNotFound() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: const [
          Icon(Icons.receipt_long_outlined, size: 60, color: Colors.grey),
          SizedBox(height: 16),
          Text('Không tìm thấy hóa đơn', style: TextStyle(color: Colors.grey)),
        ],
      ),
    );
  }

  Widget _buildInfoCard(bool isPaid, NumberFormat currencyFormatter) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(8), // Bo góc nhẹ giống web
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.05),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Card
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: primaryDark.withOpacity(0.03),
              borderRadius: const BorderRadius.vertical(
                top: Radius.circular(8),
              ),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'HÓA ĐƠN',
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.grey[600],
                        letterSpacing: 1,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '#${_invoice!['MAHOADON'] ?? widget.invoiceId}',
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                        color: primaryDark,
                      ),
                    ),
                  ],
                ),
                // Badge Trạng thái
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 6,
                  ),
                  decoration: BoxDecoration(
                    color: isPaid ? primaryGreen : Colors.orange,
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Text(
                    isPaid ? 'ĐÃ THANH TOÁN' : 'CHƯA THANH TOÁN',
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 11,
                      letterSpacing: 0.5,
                    ),
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1),

          // Body Card
          Padding(
            padding: const EdgeInsets.all(20.0),
            child: Column(
              children: [
                // Tổng tiền nổi bật
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text(
                      'Tổng thanh toán',
                      style: TextStyle(color: Colors.grey),
                    ),
                    const Spacer(),
                    Text(
                      currencyFormatter.format(
                        double.tryParse(_invoice!['SOTIEN'].toString()) ?? 0,
                      ),
                      style: const TextStyle(
                        fontSize: 24,
                        fontWeight: FontWeight.bold,
                        color: primaryGreen, // Tiền màu xanh lá
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),

                // Chi tiết
                _buildDetailRow(
                  Icons.calendar_today,
                  'Ngày tạo',
                  _invoice!['NGAYXUAT'] ?? '---',
                ),
                const SizedBox(height: 16),
                _buildDetailRow(
                  Icons.confirmation_number_outlined,
                  'Mã đặt tour',
                  '#${_invoice!['MADATTOUR'] ?? '---'}',
                ),
                const SizedBox(height: 16),

                // Chữ ký số
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color:
                        (_invoice!['CHUKYSO'] != null &&
                            _invoice!['CHUKYSO'].toString().isNotEmpty)
                        ? Colors.green.withOpacity(0.05)
                        : Colors.grey.withOpacity(0.05),
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(
                      color:
                          (_invoice!['CHUKYSO'] != null &&
                              _invoice!['CHUKYSO'].toString().isNotEmpty)
                          ? Colors.green.withOpacity(0.2)
                          : Colors.grey.withOpacity(0.2),
                    ),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.verified_user_outlined,
                        size: 20,
                        color:
                            (_invoice!['CHUKYSO'] != null &&
                                _invoice!['CHUKYSO'].toString().isNotEmpty)
                            ? Colors.green
                            : Colors.grey,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          (_invoice!['CHUKYSO'] != null &&
                                  _invoice!['CHUKYSO'].toString().isNotEmpty)
                              ? 'Hóa đơn đã được ký số bảo mật.'
                              : 'Hóa đơn chưa có chữ ký số.',
                          style: TextStyle(
                            fontSize: 13,
                            color: Colors.grey[700],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDetailRow(IconData icon, String label, String value) {
    return Row(
      children: [
        Container(
          padding: const EdgeInsets.all(8),
          decoration: BoxDecoration(
            color: Colors.grey[100],
            shape: BoxShape.circle,
          ),
          child: Icon(icon, size: 18, color: primaryDark),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(fontSize: 12, color: Colors.grey[600]),
              ),
              const SizedBox(height: 2),
              Text(
                value,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w500,
                  color: darkTextColor,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildActionButtons(bool isPaid) {
    if (isPaid) {
      return SizedBox(
        width: double.infinity,
        height: 50,
        child: ElevatedButton.icon(
          onPressed: _exporting ? null : _exportInvoicePdf,
          icon: _exporting
              ? const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(
                    color: Colors.white,
                    strokeWidth: 2,
                  ),
                )
              : const Icon(Icons.download_rounded),
          label: Text(_exporting ? 'Đang tải...' : 'TẢI HÓA ĐƠN PDF'),
          style: ElevatedButton.styleFrom(
            backgroundColor: primaryDark, // Nút tải màu Xanh đen
            foregroundColor: Colors.white,
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(4),
            ),
          ),
        ),
      );
    } else {
      return Column(
        children: [
          SizedBox(
            width: double.infinity,
            height: 54,
            child: ElevatedButton(
              onPressed: _paying ? null : _pay,
              style: ElevatedButton.styleFrom(
                backgroundColor: primaryGreen, // Nút thanh toán màu Xanh lá
                foregroundColor: Colors.white,
                elevation: 4,
                shadowColor: primaryGreen.withOpacity(0.4),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
              child: _paying
                  ? const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            color: Colors.white,
                            strokeWidth: 2,
                          ),
                        ),
                        SizedBox(width: 12),
                        Text(
                          'ĐANG XỬ LÝ...',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    )
                  : const Text(
                      'THANH TOÁN NGAY',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 1,
                      ),
                    ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Nhấn thanh toán để xác nhận và nhận hóa đơn điện tử.',
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.grey[500], fontSize: 13),
          ),
        ],
      );
    }
  }
}
