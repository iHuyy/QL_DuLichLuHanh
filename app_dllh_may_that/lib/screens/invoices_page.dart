import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/models/invoice_item.dart';
import 'invoice_detail_page.dart';

const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

class InvoicesPage extends StatefulWidget {
  final String userID;
  const InvoicesPage({Key? key, required this.userID}) : super(key: key);

  @override
  InvoicesPageState createState() => InvoicesPageState();
}

class InvoicesPageState extends State<InvoicesPage> {
  final AuthService _auth = AuthService();
  late Future<List<InvoiceItem>> _invoicesFuture;

  @override
  void initState() {
    super.initState();
    _invoicesFuture = _fetchInvoices();
  }

  // KHÔNG DÙNG didChangeDependencies để reload ngẫu nhiên, thay bằng refreshData
  // @override
  // void didChangeDependencies() {
  //   super.didChangeDependencies();
  //   // Reload invoices every time page is displayed
  //   setState(() {
  //     _invoicesFuture = _fetchInvoices();
  //   });
  // }
  
  // Thêm phương thức công khai để refresh data
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
      throw Exception('Failed to load invoices: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        title: const Text('Invoices'),
        backgroundColor: Colors.white,
        elevation: 0,
        foregroundColor: darkTextColor,
      ),
      body: FutureBuilder<List<InvoiceItem>>(
        future: _invoicesFuture,
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
                      'Error loading invoices',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${snapshot.error}',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.grey),
                    ),
                  ],
                ),
              ),
            );
          }
          
          final invoices = snapshot.data ?? [];
          if (invoices.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.receipt_long_outlined, size: 64, color: Colors.grey),
                  const SizedBox(height: 16),
                  const Text(
                    'No invoices yet',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Your invoices will appear here',
                    style: TextStyle(color: Colors.grey),
                  ),
                ],
              ),
            );
          }

          return ListView.builder(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            itemCount: invoices.length,
            itemBuilder: (context, index) {
              final inv = invoices[index];
              return Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: InkWell(
                  borderRadius: BorderRadius.circular(12),
                  onTap: () async { // Bắt kết quả từ chi tiết hóa đơn
                    await Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => InvoiceDetailPage(invoiceId: inv.id)),
                    );
                    // Force a refresh of the list every time the detail page is closed (for payment status)
                    refreshData(); 
                  },
                  child: Card(
                    elevation: 1,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    child: Padding(
                      padding: const EdgeInsets.all(14),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        children: [
                          // Icon
                          Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                              color: primaryBlue.withOpacity(0.1),
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: const Icon(
                              Icons.receipt_long,
                              color: primaryBlue,
                              size: 24,
                            ),
                          ),
                          const SizedBox(width: 12),
                          // Content
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Invoice #${inv.id}',
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.bold,
                                    color: darkTextColor,
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  inv.date,
                                  style: const TextStyle(fontSize: 11, color: Colors.grey),
                                ),
                                const SizedBox(height: 4),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: inv.status.toLowerCase().contains('paid') 
                                        ? Colors.green.withOpacity(0.1) 
                                        : Colors.orange.withOpacity(0.1),
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: Text(
                                    inv.status,
                                    style: TextStyle(
                                      fontSize: 10,
                                      fontWeight: FontWeight.w600,
                                      color: inv.status.toLowerCase().contains('paid') 
                                          ? Colors.green.shade700 
                                          : Colors.orange.shade700,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          // Amount
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: [
                              Text(
                                '\$${inv.amount.toStringAsFixed(2)}',
                                style: const TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.bold,
                                  color: primaryBlue,
                                ),
                              ),
                              const SizedBox(height: 4),
                              const Icon(Icons.chevron_right, color: Colors.grey, size: 20),
                            ],
                          ),
                        ],
                      ),
                    ),
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
