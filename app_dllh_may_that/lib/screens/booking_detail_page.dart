import 'package:flutter/material.dart';
// import 'package:http/http.dart' as http; // XÓA DÒNG NÀY
import 'package:app_dllh/services/api_client.dart'; // THÊM DÒNG NÀY
import 'dart:convert';
import 'package:app_dllh/config/app_config.dart';

const Color darkTextColor = Color(0xFF1E1E1E);

class BookingDetailPage extends StatefulWidget {
  final int bookingId;
  const BookingDetailPage({Key? key, required this.bookingId}) : super(key: key);

  @override
  _BookingDetailPageState createState() => _BookingDetailPageState();
}

class _BookingDetailPageState extends State<BookingDetailPage> {
  late Future<Map<String, dynamic>> _detailFuture;
  final ApiClient _apiClient = ApiClient(); // THÊM DÒNG NÀY

  @override
  void initState() {
    super.initState();
    _detailFuture = _fetchDetail();
  }

  Future<Map<String, dynamic>> _fetchDetail() async {
    // SỬA LỖI: Chỉ truyền endpoint
    final endpoint = 'get_booking_detail.php?madattour=${widget.bookingId}';
    
    // SỬA LỖI: Dùng _apiClient.getJson
    final resp = await _apiClient.getJson(endpoint).timeout(const Duration(seconds: 10));

    if (resp.statusCode != 200) throw Exception('HTTP ${resp.statusCode}');
    final body = resp.body.trim();
    if (body.startsWith('<')) {
      // try to extract JSON
      final idx = body.indexOf('{');
      if (idx >= 0) {
        final Map<String, dynamic> decodedMap = jsonDecode(body.substring(idx));
        if (decodedMap['success'] == true) return decodedMap['booking'] as Map<String, dynamic>;
        throw Exception(decodedMap['message'] ?? 'Invalid response');
      }
      throw Exception('Invalid server response');
    }
    final decoded = jsonDecode(body) as Map<String, dynamic>;
    if (decoded['success'] != true) throw Exception(decoded['message'] ?? 'Failed to load booking');
    return decoded['booking'] as Map<String, dynamic>;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Booking Detail'), backgroundColor: Colors.white, foregroundColor: darkTextColor, elevation: 0),
      body: FutureBuilder<Map<String, dynamic>>(
        future: _detailFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
          if (snapshot.hasError) return Center(child: Text('Error: ${snapshot.error}'));
          final m = snapshot.data!;

          return SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(m['TIEUDE'] ?? m['TieuDe'] ?? 'Unknown Tour', style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                Text('Booking ID: ${m['MADATTOUR'] ?? m['MaDatTour'] ?? widget.bookingId}'),
                const SizedBox(height: 8),
                Text('Tour date: ${m['THOIGIAN'] ?? m['THOIGIAN'] ?? ''}'),
                const SizedBox(height: 8),
                Text('Booked on: ${m['NGAYDAT'] ?? m['NGAYDAT'] ?? ''}'),
                const SizedBox(height: 12),
                Row(children: [Text('Adults: ${m['SONGUOILON'] ?? m['SONGUOILON'] ?? ''}'), const SizedBox(width: 12), Text('Children: ${m['SOTREEM'] ?? m['SOTREEM'] ?? ''}')]),
                const SizedBox(height: 12),
                Text('Total: ${m['TONGTIEN'] ?? m['TONGTIEN'] ?? ''}', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.green)),
                const SizedBox(height: 12),
                const Divider(),
                const SizedBox(height: 8),
                Text('Start: ${m['NOIKHOIHANH'] ?? m['NOIKHOIHANH'] ?? ''}'),
                const SizedBox(height: 4),
                Text('Destination: ${m['NOIDEN'] ?? m['NOIDEN'] ?? ''}'),
                const SizedBox(height: 12),
                Text('Tour description', style: const TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                Text(m['MOTA'] ?? m['MoTa'] ?? ''),
                const SizedBox(height: 16),
                Text('Special requests', style: const TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                Text(m['YEUCAUDACBIET'] ?? m['YeuCauDacBiet'] ?? ''),
              ],
            ),
          );
        },
      ),
    );
  }
}