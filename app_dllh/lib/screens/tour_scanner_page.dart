// File: tour_scanner_page.dart 🧭

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart'; // Import thư viện quét
// Giả sử: import 'package:app_dllh/api/tour_service.dart';

const Color primaryBlue = Color(0xFF007AFF);

class TourScannerPage extends StatefulWidget {
  const TourScannerPage({super.key});

  @override
  State<TourScannerPage> createState() => _TourScannerPageState();
}

class _TourScannerPageState extends State<TourScannerPage> {
  MobileScannerController cameraController = MobileScannerController();
  bool _isProcessing = false;

  void _onDetect(BarcodeCapture capture) async {
    if (_isProcessing) return; // Tránh xử lý nhiều lần
    
    final List<Barcode> barcodes = capture.barcodes;
    if (barcodes.isEmpty) return;
    
    final String? tourId = barcodes.first.rawValue;
    if (tourId == null) return;
    
    setState(() {
      _isProcessing = true;
    });

    // 1. Dừng camera để không quét tiếp
    cameraController.stop(); 

    // 2. Xử lý Logic Quét Tour
    try {
        // ⚠️ TODO: Thực hiện cuộc gọi API để tìm kiếm và hiển thị thông tin tour
        // final Map<String, dynamic> tourInfo = await TourService().fetchTourDetails(tourId);
        
        // Mô phỏng xử lý (thay thế bằng API thật)
        await Future.delayed(const Duration(seconds: 1)); 

        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Đã tìm thấy Tour ID: $tourId. Đang chuyển hướng...'),
                backgroundColor: Colors.green,
            ),
        );

        // 3. Điều hướng đến trang chi tiết Tour (giả lập trả kết quả)
        Navigator.pop(context, {'tour_id': tourId, 'status': 'success'});

    } catch (e) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Lỗi quét hoặc tìm kiếm Tour: $e'),
                backgroundColor: Colors.red,
            ),
        );
         // Khởi động lại camera nếu không thành công
        cameraController.start(); 
    } finally {
        setState(() {
            _isProcessing = false;
        });
    }
  }

  @override
  void dispose() {
    cameraController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text(
          'Quét Mã QR Tour',
          style: TextStyle(color: Colors.white),
        ),
        backgroundColor: primaryBlue,
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      body: Stack(
        children: [
          // Widget quét camera
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
          const Align(
            alignment: Alignment.topCenter,
            child: Padding(
              padding: EdgeInsets.only(top: 50),
              child: Text(
                'Hướng camera vào mã QR của Tour',
                style: TextStyle(
                  color: Colors.white,
                  backgroundColor: Colors.black54,
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
          if (_isProcessing)
             const Center(
                child: CircularProgressIndicator(color: primaryBlue),
            ),
        ],
      ),
    );
  }
}