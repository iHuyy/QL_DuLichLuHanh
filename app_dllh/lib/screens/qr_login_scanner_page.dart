// File: qr_login_scanner_page.dart (CHỈ DÙNG mobile_scanner & image_picker)

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart'; 
import 'package:image_picker/image_picker.dart'; // 🔑 Cần thiết để chọn ảnh
// Bạn có thể cần import 'dart:io'; nếu muốn làm việc với File, nhưng image.path là đủ.

const Color primaryBlue = Color(0xFF007AFF);

class QRLoginScannerPage extends StatefulWidget { 
  const QRLoginScannerPage({super.key});

  @override
  State<QRLoginScannerPage> createState() => _QRLoginScannerPageState(); 
}

class _QRLoginScannerPageState extends State<QRLoginScannerPage> { 
  MobileScannerController cameraController = MobileScannerController();
  final ImagePicker _picker = ImagePicker(); 

  // =========================================================
  // LOGIC QUÉT TRỰC TIẾP
  // =========================================================
  void _onDetect(BarcodeCapture capture) {
    final List<Barcode> barcodes = capture.barcodes;
    
    if (barcodes.isNotEmpty) {
      final String? rawValue = barcodes.first.rawValue;
      if (rawValue != null) {
        cameraController.stop(); 
        Navigator.pop(context, rawValue);
      }
    }
  }

  // =========================================================
  // 🔑 LOGIC ĐỌC QR TỪ THƯ VIỆN ẢNH (Dùng mobile_scanner.analyzeImage)
  // =========================================================
  Future<void> _scanImageFromGallery() async {
  try {
    cameraController.stop(); 
    final XFile? image = await _picker.pickImage(source: ImageSource.gallery);

    if (image == null) {
      cameraController.start();
      return;
    }
    
    // 🔑 DÒNG SỬA LỖI: Ép kiểu kết quả trả về thành Barcode? một cách an toàn
    final Barcode? result = await cameraController.analyzeImage(image.path) as Barcode?;

    // Kiểm tra an toàn Barcode? và thuộc tính rawValue
    if (result != null && result.rawValue != null) {
      final String qrData = result.rawValue!;
      
      if (mounted) {
          Navigator.pop(context, qrData);
      }

    } else {
      // Bao gồm trường hợp kết quả là null hoặc không phải Barcode
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không tìm thấy mã QR hợp lệ trong hình ảnh. Vui lòng thử lại.'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
    
    cameraController.start();

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
          
          // 2. Nút Đọc QR từ Thư viện Ảnh (Vị trí ở dưới)
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