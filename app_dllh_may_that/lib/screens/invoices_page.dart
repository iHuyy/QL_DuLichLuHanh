import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/models/invoice_item.dart';
import 'invoice_detail_page.dart';
import 'package:intl/intl.dart';

// --- BỘ MÀU WEB STYLE ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);
const Color darkTextColor = Color(0xFF2C3E50);

class InvoicesPage extends StatefulWidget {
  final String userID;
  const InvoicesPage({Key? key, required this.userID}) : super(key: key);

  @override
  InvoicesPageState createState() => InvoicesPageState();
}

class InvoicesPageState extends State<InvoicesPage> {
  final AuthService _auth = AuthService();
  late Future<List<InvoiceItem>> _invoicesFuture;
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

  @override
  void initState() {
    super.initState();
    _invoicesFuture = _fetchInvoices();
  }

  void refreshData() {
    if (mounted) {
      setState(() {
        _invoicesFuture = _fetchInvoices();
      });
    }
  }

  Future<List<InvoiceItem>> _fetchInvoices() async {
    try {
      final list = await _auth.getInvoices();
      return list.map((m) => InvoiceItem.fromJson(m)).toList();
    } catch (e) {
      throw Exception('Không thể tải hóa đơn: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        title: const Text(
          'DANH SÁCH HÓA ĐƠN',
          style: TextStyle(
            color: primaryDark,
            fontSize: 16,
            fontWeight: FontWeight.w800,
            letterSpacing: 1,
          ),
        ),
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        iconTheme: const IconThemeData(color: primaryDark),
      ),
      body: FutureBuilder<List<InvoiceItem>>(
        future: _invoicesFuture,
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
                  )
                ],
              ),
            );
          }
          
          final invoices = snapshot.data ?? [];
          if (invoices.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.receipt_long, size: 80, color: Colors.grey[300]),
                  const SizedBox(height: 16),
                  Text(
                    'Chưa có hóa đơn nào',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.grey[600]),
                  ),
                ],
              ),
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: invoices.length,
            separatorBuilder: (ctx, i) => const SizedBox(height: 12),
            itemBuilder: (context, index) {
              final inv = invoices[index];
              final statusLower = inv.status.toLowerCase();

              // --- LOGIC KIỂM TRA MỚI ---
              // 1. Kiểm tra hủy trước
              final isCancelled = statusLower.contains('hủy') || statusLower.contains('cancel');
              
              // 2. Kiểm tra thanh toán (chỉ đúng nếu không hủy VÀ có từ khóa thanh toán/paid)
              final isPaid = !isCancelled && (statusLower.contains('thanh toán') || statusLower.contains('paid'));

              // 3. Xác định màu và text
              Color statusColor;
              String statusText;
              IconData statusIcon;

              if (isCancelled) {
                statusColor = Colors.red;
                statusText = 'ĐÃ HỦY';
                statusIcon = Icons.cancel_outlined;
              } else if (isPaid) {
                statusColor = primaryGreen;
                statusText = 'ĐÃ THANH TOÁN';
                statusIcon = Icons.check_circle_outline;
              } else {
                statusColor = Colors.orange;
                statusText = 'CHƯA THANH TOÁN';
                statusIcon = Icons.receipt_outlined;
              }

              return InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: () async {
                  await Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => InvoiceDetailPage(invoiceId: inv.id)),
                  );
                  refreshData();
                },
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: Colors.grey.shade200),
                    boxShadow: [
                      BoxShadow(color: Colors.black.withOpacity(0.03), blurRadius: 8, offset: const Offset(0, 2))
                    ],
                  ),
                  child: Row(
                    children: [
                      // Icon Box
                      Container(
                        width: 48,
                        height: 48,
                        decoration: BoxDecoration(
                          color: statusColor.withOpacity(0.1),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Icon(
                          statusIcon,
                          color: statusColor,
                          size: 24,
                        ),
                      ),
                      const SizedBox(width: 16),
                      
                      // Info
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Hóa đơn #${inv.id}',
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                fontSize: 15,
                                color: darkTextColor,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              inv.date,
                              style: TextStyle(fontSize: 12, color: Colors.grey[500]),
                            ),
                          ],
                        ),
                      ),
                      
                      // Status & Amount
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(
                            currencyFormat.format(inv.amount),
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                              color: primaryDark,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                            decoration: BoxDecoration(
                              color: statusColor,
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Text(
                              statusText,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 9,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}