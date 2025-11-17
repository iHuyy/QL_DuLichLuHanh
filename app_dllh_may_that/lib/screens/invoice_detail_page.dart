import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';

class InvoiceDetailPage extends StatefulWidget {
  final int invoiceId;
  const InvoiceDetailPage({Key? key, required this.invoiceId}) : super(key: key);

  @override
  _InvoiceDetailPageState createState() => _InvoiceDetailPageState();
}

class _InvoiceDetailPageState extends State<InvoiceDetailPage> {
  final ApiClient _api = ApiClient();
  Map<String, dynamic>? _invoice;
  bool _loading = true;
  bool _paying = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Reload invoice detail every time page is displayed
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; });
    try {
      final resp = await _api.getJson('get_invoice_detail.php?mahoadon=${widget.invoiceId}');
      final body = resp.body;
      final decoded = jsonDecode(body);
      if (decoded['success'] == true && decoded['invoice'] != null) {
        setState(() { _invoice = Map<String,dynamic>.from(decoded['invoice']); });
      } else {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(decoded['message'] ?? 'Invoice not found')));
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to load invoice: $e')));
    } finally {
      setState(() { _loading = false; });
    }
  }

  Future<void> _pay() async {
    if (_invoice == null) return;
    setState(() { _paying = true; });
    try {
      final resp = await _api.postJson('pay_invoice.php', body: {'maHoaDon': widget.invoiceId});
      final decoded = jsonDecode(resp.body);
      if (decoded['success'] == true) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Payment successful')));
        await _load();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(decoded['message'] ?? 'Payment failed')));
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Payment error: $e')));
    } finally {
      setState(() { _paying = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Invoice #${widget.invoiceId}'), backgroundColor: Colors.white, foregroundColor: Colors.black),
      body: _loading
        ? const Center(child: CircularProgressIndicator())
        : _invoice == null
          ? const Center(child: Text('Invoice not available'))
          : Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Invoice ID: ${_invoice!['MAHOADON'] ?? widget.invoiceId}', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
                  const SizedBox(height: 12),
                  Text('Amount: \$${_invoice!['SOTIEN'] ?? ''}', style: const TextStyle(fontSize: 16)),
                  const SizedBox(height: 8),
                  Text('Status: ${_invoice!['TRANGTHAI'] ?? ''}', style: const TextStyle(fontSize: 14)),
                  const SizedBox(height: 8),
                  Text('Date: ${_invoice!['NGAYXUAT'] ?? ''}', style: const TextStyle(fontSize: 14)),
                  const SizedBox(height: 12),
                  Text('Related booking: ${_invoice!['MADATTOUR'] ?? ''}'),
                  const SizedBox(height: 12),
                  Expanded(child: SingleChildScrollView(child: Text('Signature present: ${_invoice!['CHUKYSO'] != null && (_invoice!['CHUKYSO'] as String).isNotEmpty}'))),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton(
                      onPressed: _paying ? null : _pay,
                      child: _paying ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) : const Text('Pay'),
                    ),
                  ),
                ],
              ),
            ),
    );
  }
}
