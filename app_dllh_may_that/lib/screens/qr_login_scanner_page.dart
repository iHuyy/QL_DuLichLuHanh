import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart' as ms; 
import 'package:image_picker/image_picker.dart';
// *** BẮT ĐẦU SỬA LỖI: Imports ***
// import 'package:app_dllh/services/api_client.dart'; // Không cần nữa
import 'package:app_dllh/services/auth_service.dart'; // Import AuthService
// *** KẾT THÚC SỬA LỖI ***
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';
import 'dart:io';
import 'package:device_info_plus/device_info_plus.dart';

const Color primaryBlue = Color(0xFF007AFF);

class QRLoginScannerPage extends StatefulWidget { 
  const QRLoginScannerPage({super.key});

  @override
  State<QRLoginScannerPage> createState() => _QRLoginScannerPageState(); 
}

class _QRLoginScannerPageState extends State<QRLoginScannerPage> { 
  late ms.MobileScannerController cameraController;
  final ImagePicker _picker = ImagePicker(); 

  // *** BẮT ĐẦU SỬA LỖI: Khởi tạo AuthService ***
  final AuthService _authService = AuthService(); // Sử dụng AuthService
  // final ApiClient _apiClient = ApiClient(); // Xóa dòng này
  // *** KẾT THÚC SỬA LỖI ***
  
  bool _isPickingImage = false;
  bool _isProcessing = false; // Flag khóa để chống vòng lặp

  @override
  void initState() {
    super.initState();
    cameraController = ms.MobileScannerController();
  }
  
  // *** BẮT ĐẦU SỬA LỖI: Hàm xử lý logic ***
  // Hàm này được gọi bởi _onDetect và _scanImageFromGallery
  void _processQrCode(String qrToken) async {
    // 1. Khóa lại ngay lập tức
    if (_isProcessing) return;
    setState(() {
      _isProcessing = true;
    });

    // Dừng camera để tránh quét lại
    await cameraController.stop();

    // Hiển thị loading (an toàn hơn)
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (context) => const Center(child: CircularProgressIndicator()),
    );

    try {
      bool success = await _authService.approveQrLogin(qrToken);

      Navigator.of(context).pop();

      if (success) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Phê duyệt đăng nhập Web thành công!'),
            backgroundColor: Colors.green,
          ),
        );
        Navigator.of(context).pop();
      } else {
        throw Exception('Mã QR không hợp lệ hoặc đã hết hạn');
      }

    } catch (e) {
      if (Navigator.of(context).canPop()) {
         Navigator.of(context).pop(); // Tắt loading (nếu vẫn mở)
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Lỗi: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
      // 5. Mở khóa và khởi động lại camera để thử lại
      setState(() {
        _isProcessing = false;
      });
      if (mounted) {
         await cameraController.start();
      }
    }
  }
  // *** KẾT THÚC SỬA LỖI ***


  void _onDetect(ms.BarcodeCapture capture) async {
    // *** BẮT ĐẦU SỬA LỖI: Thêm khóa ***
    if (_isProcessing) return; 
    // *** KẾT THÚC SỬA LỖI ***

    final List<ms.Barcode> barcodes = capture.barcodes;

    if (barcodes.isNotEmpty && barcodes.first.rawValue != null) {
      String qrToken = barcodes.first.rawValue!;
      // Gọi hàm xử lý logic
      _processQrCode(qrToken);
    }
  }
  
  void _scanImageFromGallery() async {
    if (_isPickingImage || _isProcessing) return;

    setState(() {
      _isPickingImage = true;
      // _isProcessing sẽ được đặt trong _processQrCode
    });

    try {
      final XFile? image = await _picker.pickImage(source: ImageSource.gallery);
      if (image != null) {
        // Sửa lỗi: analyzeImage trả về bool, và sẽ kích hoạt _onDetect
        final bool barcodeFound = await cameraController.analyzeImage(image.path);
        
        if (!barcodeFound) {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Không tìm thấy mã QR trong ảnh.'),
              backgroundColor: Colors.orange,
            ),
          );
           // Mở khóa nếu không tìm thấy
           setState(() {
             _isPickingImage = false;
           });
        }
        // Nếu thành công (true), _onDetect sẽ được gọi và _processQrCode sẽ xử lý
      } else {
         setState(() {
           _isPickingImage = false;
         });
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Lỗi khi quét ảnh: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
      setState(() {
        _isPickingImage = false;
      });
    }
    // Không cần đặt lại _isProcessing ở đây vì _processQrCode sẽ xử lý
  }


  @override
  void dispose() {
    cameraController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(
        children: [
          // 1. Camera View
          ms.MobileScanner(
            controller: cameraController,
            onDetect: _onDetect,
          ),
          
          // Lớp phủ mờ với một lỗ ở giữa
          ColorFiltered(
            colorFilter: ColorFilter.mode(
              Colors.black.withOpacity(0.5),
              BlendMode.srcOut,
            ),
            child: Stack(
              children: [
                Container(
                  decoration: const BoxDecoration(
                    color: Colors.transparent,
                  ),
                ),
                Align(
                  alignment: Alignment.center,
                  child: Container(
                    width: MediaQuery.of(context).size.width * 0.7,
                    height: MediaQuery.of(context).size.width * 0.7,
                    decoration: BoxDecoration(
                      color: Colors.black,
                      borderRadius: BorderRadius.circular(20),
                    ),
                  ),
                ),
              ],
            ),
          ),
          
          // Khung viền
          Align(
            alignment: Alignment.center,
            child: Container(
              width: MediaQuery.of(context).size.width * 0.7,
              height: MediaQuery.of(context).size.width * 0.7,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                  color: Colors.white.withOpacity(0.7),
                  width: 2,
                ),
              ),
            ),
          ),
          
          // Nút Đóng
          Align(
            alignment: Alignment.topLeft,
            child: Padding(
              padding: const EdgeInsets.only(top: 50.0, left: 15.0),
              child: IconButton(
                icon: const Icon(Icons.close, color: Colors.white, size: 30),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ),
          ),

          // Nút Flash
          Align(
            alignment: Alignment.topRight,
            child: Padding(
              padding: const EdgeInsets.only(top: 50.0, right: 15.0),
              child: IconButton(
                icon: ValueListenableBuilder(
                  valueListenable: cameraController.torchState,
                  builder: (context, state, child) {
                    final color = state == ms.TorchState.on ? primaryBlue : Colors.white;
                    return Icon(Icons.flash_on, color: color, size: 30);
                  },
                ),
                onPressed: () => cameraController.toggleTorch(),
              ),
            ),
          ),
          
          // Hướng dẫn
          Align(
            alignment: Alignment.topCenter,
            child: Padding(
              padding: const EdgeInsets.only(top: 100.0),
              child: Text(
                'Quét mã QR trên Web',
                style: TextStyle(
                  color: Colors.white.withOpacity(0.9),
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
                onPressed: _isProcessing ? null : _scanImageFromGallery, // Khóa nút khi đang xử lý
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

          // (Hiển thị loading đã được chuyển vào hàm _processQrCode)
        ],
      ),
    );
  }
}