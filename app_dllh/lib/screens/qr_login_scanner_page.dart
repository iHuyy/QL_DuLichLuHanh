import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart'; 
import 'package:image_picker/image_picker.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';

const Color primaryBlue = Color(0xFF007AFF);

class QRLoginScannerPage extends StatefulWidget { 
  const QRLoginScannerPage({super.key});

  @override
  State<QRLoginScannerPage> createState() => _QRLoginScannerPageState(); 
}

class _QRLoginScannerPageState extends State<QRLoginScannerPage> { 
  MobileScannerController cameraController = MobileScannerController();
  final ImagePicker _picker = ImagePicker(); 

  final String apiUrl = "http://localhost:5127/QrLogin/AuthenticateQrSession";

  final int currentUserId = 123; // ⚠️ TODO: Lấy từ session hoặc SharedPrefs thực tế

  void _onDetect(BarcodeCapture capture) async {
    final List<Barcode> barcodes = capture.barcodes;
    
    if (barcodes.isNotEmpty) {
      final String? sessionKey = barcodes.first.rawValue;
      if (sessionKey != null) {
        cameraController.stop(); 
        await _authenticateWithServer(sessionKey);
      }
    }
  }

  Future<void> _scanImageFromGallery() async {
    try {
      cameraController.stop(); 
      final XFile? image = await _picker.pickImage(source: ImageSource.gallery);

      if (image == null) {
        cameraController.start();
        return;
      }

      final Barcode? result =
          await cameraController.analyzeImage(image.path) as Barcode?;

      if (result != null && result.rawValue != null) {
        final String sessionKey = result.rawValue!;
        await _authenticateWithServer(sessionKey);
      } else {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Không tìm thấy mã QR hợp lệ trong hình ảnh.'),
              backgroundColor: Colors.red,
            ),
          );
        }
        cameraController.start();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Lỗi quét ảnh: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
      cameraController.start();
    }
  }

  // =========================================================
  // 🔐 Gửi yêu cầu xác thực QR tới Web API
  // =========================================================
  Future<void> _authenticateWithServer(String sessionKey) async {
    try {
      final response = await http.post(
        Uri.parse(apiUrl),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'sessionKey': sessionKey,
          'userId': currentUserId,
        }),
      );

      if (response.statusCode == 200) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('✅ Xác thực thành công! Bạn có thể quay lại trình duyệt.'),
              backgroundColor: Colors.green,
            ),
          );
          Navigator.pop(context, sessionKey);
        }
      } else if (response.statusCode == 404) {
        _showError('Mã QR không hợp lệ hoặc đã hết hạn.');
      } else {
        _showError('Lỗi server: ${response.statusCode}');
      }
    } catch (e) {
      _showError('Không thể kết nối máy chủ: $e');
    } finally {
      cameraController.start();
    }
  }

  void _showError(String message) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message), backgroundColor: Colors.red),
      );
    }
  }

  @override
  void dispose() {
    cameraController.dispose();
    super.dispose();
  }

  // =========================================================
  // WIDGETS GIAO DIỆN
  // =========================================================
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text(
          'Quét Mã QR Đăng nhập',
          style: TextStyle(color: Colors.white),
        ),
        backgroundColor: primaryBlue,
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      body: Stack(
        children: [
          // 1. Mobile Scanner (Camera)
          MobileScanner(
            controller: cameraController,
            onDetect: _onDetect,
            overlay: Padding(
              padding: const EdgeInsets.all(40.0),
              child: Container(
                decoration: BoxDecoration(
                  border: Border.all(
                    color: primaryBlue,
                    width: 4,
                  ),
                  borderRadius: BorderRadius.circular(12),
                ),
              ),
            ),
          ),
          
          // Thông báo hướng dẫn
          const Align(
            alignment: Alignment.topCenter,
            child: Padding(
              padding: EdgeInsets.only(top: 50),
              child: Text(
                'Đặt mã QR trong khung để đăng nhập',
                style: TextStyle(
                  color: Colors.white,
                  backgroundColor: Colors.black54,
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
          
          // 2. Nút Đọc QR từ Thư viện Ảnh
          Align(
            alignment: Alignment.bottomCenter,
            child: Padding(
              padding: const EdgeInsets.only(bottom: 40, left: 20, right: 20),
              child: ElevatedButton.icon(
                onPressed: _scanImageFromGallery,
                icon: const Icon(Icons.image, color: Colors.white),
                label: const Text(
                  'Quét QR từ Thư viện Ảnh',
                  style: TextStyle(fontSize: 18, color: Colors.white),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: primaryBlue,
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 15),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
