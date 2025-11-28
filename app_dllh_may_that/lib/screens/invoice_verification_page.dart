import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:app_dllh/services/api_client.dart';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:app_dllh/config/app_config.dart';
import 'package:shared_preferences/shared_preferences.dart';

// --- BỘ MÀU ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color scaffoldBg = Color(0xFFF8F9FA);

class InvoiceVerificationPage extends StatefulWidget {
  const InvoiceVerificationPage({Key? key}) : super(key: key);

  @override
  _InvoiceVerificationPageState createState() =>
      _InvoiceVerificationPageState();
}

class _InvoiceVerificationPageState extends State<InvoiceVerificationPage> {
  final ApiClient _api = ApiClient();

  String? _fileName;
  String? _pickedFilePath; // Biến quan trọng: Lưu đường dẫn file thực tế
  int? _invoiceId;
  bool _isLoading = false;
  Map<String, dynamic>? _verificationResult;

  // 1. Hàm chọn file (Chỉ chạy khi bấm vào vùng chọn file)
  Future<void> _pickFile() async {
    try {
      FilePickerResult? result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf'],
      );

      if (result != null) {
        final file = result.files.single;
        setState(() {
          _fileName = file.name;
          _pickedFilePath = file.path; // Lưu đường dẫn để lát nữa upload
          _verificationResult = null;  // Reset kết quả cũ
          _invoiceId = _extractIdFromFilename(file.name);
        });
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Lỗi chọn file: $e'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }

  int? _extractIdFromFilename(String filename) {
    final RegExp regex = RegExp(r'(\d+)');
    final match = regex.firstMatch(filename);
    if (match != null) {
      return int.tryParse(match.group(0)!);
    }
    return null;
  }

  // 2. Hàm Verify (Upload file sang C#)
  // SỬA LỖI: Không gọi pickFiles ở đây nữa
  Future<void> _verify() async {
    if (_pickedFilePath == null) {
       ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng chọn file PDF trước.')),
      );
      return;
    }

    setState(() => _isLoading = true);

    try {
      // URL API C#: http://IP:5127/api/hoadon/verify
      var uri = Uri.parse('${AppConfig.dotnetBaseUrl}/api/hoadon/verify');
      
      var request = http.MultipartRequest('POST', uri);
      
      // Thêm Token JWT (nếu có)
      final prefs = await SharedPreferences.getInstance();
      final token = prefs.getString('jwt_token');
      if (token != null) {
        request.headers['Authorization'] = 'Bearer $token';
      }

      // Đính kèm file từ đường dẫn đã chọn
      request.files.add(await http.MultipartFile.fromPath(
        'invoiceFile', // Tên tham số khớp với API C#
        _pickedFilePath!,
      ));

      // Gửi request
      final streamedResponse = await request.send();
      final response = await http.Response.fromStream(streamedResponse);

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        setState(() {
          _verificationResult = decoded;
        });
      } else {
        setState(() {
          _verificationResult = {
            'success': false,
            'message': 'Lỗi server (${response.statusCode}): ${response.body}'
          };
        });
      }

    } catch (e) {
      setState(() {
        _verificationResult = {'success': false, 'message': 'Lỗi kết nối: $e'};
      });
    } finally {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      appBar: AppBar(
        title: const Text(
          'XÁC THỰC HÓA ĐƠN',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
        ),
        backgroundColor: Colors.white,
        foregroundColor: primaryDark,
        elevation: 0,
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Hướng dẫn
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.blue.shade50,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: Colors.blue.shade100),
              ),
              child: const Row(
                children: [
                  Icon(Icons.info_outline, color: Colors.blue),
                  SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'Chọn file PDF hóa đơn để hệ thống kiểm tra chữ ký số và nội dung.',
                      style: TextStyle(color: Colors.black87, fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Khu vực chọn file
            InkWell(
              onTap: _pickFile,
              borderRadius: BorderRadius.circular(12),
              child: Container(
                width: double.infinity,
                height: 150,
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: Colors.grey.shade300,
                    style: BorderStyle.solid,
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withOpacity(0.03),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(
                      _fileName != null
                          ? Icons.description
                          : Icons.cloud_upload_outlined,
                      size: 40,
                      color: _fileName != null ? primaryGreen : Colors.grey,
                    ),
                    const SizedBox(height: 12),
                    Text(
                      _fileName ?? 'Nhấn để chọn file PDF',
                      style: TextStyle(
                        color: _fileName != null ? primaryDark : Colors.grey,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    if (_invoiceId != null)
                      Text(
                        '(Mã hóa đơn: $_invoiceId)',
                        style: const TextStyle(color: Colors.green, fontSize: 12),
                      ),
                  ],
                ),
              ),
            ),

            const SizedBox(height: 24),

            // Nút xác thực
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                // Chỉ enable nút khi đã chọn file (_pickedFilePath != null)
                onPressed: (_pickedFilePath != null && !_isLoading) ? _verify : null,
                style: ElevatedButton.styleFrom(
                  backgroundColor: primaryGreen,
                  disabledBackgroundColor: Colors.grey.shade300,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(8),
                  ),
                ),
                child: _isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          color: Colors.white,
                          strokeWidth: 2,
                        ),
                      )
                    : const Text(
                        'KIỂM TRA NGAY',
                        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                      ),
              ),
            ),

            const SizedBox(height: 30),

            // Kết quả
            if (_verificationResult != null) _buildResultCard(),
          ],
        ),
      ),
    );
  }

  Widget _buildResultCard() {
    bool isSuccess = _verificationResult!['success'] == true;
    bool isValid = _verificationResult!['isValid'] == true;
    String message = _verificationResult!['message'] ?? '';
    
    // Lấy data an toàn
    Map<String, dynamic>? data;
    if (_verificationResult!['data'] is Map<String, dynamic>) {
        data = _verificationResult!['data'];
    }

    Color cardColor;
    IconData icon;

    if (!isSuccess) {
      cardColor = Colors.red.shade50;
      icon = Icons.error_outline;
    } else if (isValid) {
      cardColor = Colors.green.shade50;
      icon = Icons.check_circle;
    } else {
      cardColor = Colors.orange.shade50;
      icon = Icons.warning_amber_rounded;
    }

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: cardColor,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: isValid && isSuccess
              ? Colors.green.shade200
              : (isSuccess ? Colors.orange.shade200 : Colors.red.shade200),
        ),
      ),
      child: Column(
        children: [
          Icon(
            icon,
            size: 50,
            color: isValid && isSuccess
                ? Colors.green
                : (isSuccess ? Colors.orange : Colors.red),
          ),
          const SizedBox(height: 12),
          Text(
            isValid && isSuccess ? 'HỢP LỆ' : (isSuccess ? 'CẢNH BÁO' : 'LỖI'),
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: isValid && isSuccess
                  ? Colors.green.shade800
                  : (isSuccess ? Colors.orange.shade800 : Colors.red.shade800),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 14),
          ),

          if (data != null) ...[
            const Divider(height: 24),
            _buildInfoRow('Mã hóa đơn:', '#${data['maHoaDon']}'),
            _buildInfoRow('Ngày xuất:', '${data['ngayXuat']}'),
            _buildInfoRow('Trạng thái:', '${data['trangThai']}'),
          ],
        ],
      ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.grey)),
          Text(value, style: const TextStyle(fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}