// File: lib/screens/tour_scanner_page.dart

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart'
    as ms; // Dùng alias ms cho giống file kia
import 'package:image_picker/image_picker.dart'; // Thêm thư viện chọn ảnh
import 'package:app_dllh/services/api_client.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/screens/tour_detail_page.dart';
import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

const Color primaryBlue = Color(0xFF007AFF);

class TourScannerPage extends StatefulWidget {
  const TourScannerPage({super.key});

  @override
  State<TourScannerPage> createState() => _TourScannerPageState();
}

class _TourScannerPageState extends State<TourScannerPage> {
  late ms.MobileScannerController cameraController;
  final ImagePicker _picker = ImagePicker();
  final ApiClient _apiClient = ApiClient();

  bool _isProcessing = false;
  bool _isPickingImage = false;

  @override
  void initState() {
    super.initState();
    // Khởi tạo controller giống trang QR Login
    cameraController = ms.MobileScannerController();
  }

  @override
  void dispose() {
    cameraController.dispose();
    super.dispose();
  }

  // Hàm trích xuất Tour ID
  String? _extractTourId(String rawValue) {
    final uriRegex = RegExp(r'TourDetail\/(\d+)');
    final match = uriRegex.firstMatch(rawValue);
    if (match != null) {
      return match.group(1);
    }
    if (int.tryParse(rawValue) != null) {
      return rawValue;
    }
    return null;
  }

  // Xử lý khi phát hiện mã (từ Camera hoặc Ảnh)
  // Xử lý khi phát hiện mã (từ Camera hoặc Ảnh)
  void _processTourCode(String rawValue) async {
    if (_isProcessing) return;

    final String? tourId = _extractTourId(rawValue);
    if (tourId == null) {
      if (!mounted) return;

      setState(() {
        _isProcessing = true;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Mã QR không đúng định dạng Tour: "$rawValue"'),
          backgroundColor: Colors.orange,
          duration: const Duration(seconds: 2),
        ),
      );

      Future.delayed(const Duration(seconds: 2), () {
        if (mounted) {
          setState(() {
            _isProcessing = false;
          });
        }
      });
      return;
    }

    setState(() {
      _isProcessing = true;
    });

    await cameraController.stop();

    if (!mounted) return;
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => const Center(child: CircularProgressIndicator()),
    );

    try {
      final response = await _apiClient.getJson('get_tour.php?id=$tourId');

      // Đóng loading
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }

      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body['success'] == true && body['data'] != null) {
          final tour = Tour.fromJson(body['data']);
          final prefs = await SharedPreferences.getInstance();
          final userID = prefs.getString('user_id') ?? '';

          if (!mounted) return;
          
          Navigator.pushReplacement(
            context,
            MaterialPageRoute(
              builder: (context) => TourDetailPage(tour: tour, userID: userID),
            ),
          );
          return;
        }
      }

      // Trường hợp API trả về 200 nhưng success = false (VD: Tour không tồn tại)
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Không tìm thấy thông tin tour trên hệ thống.'),
          backgroundColor: Colors.red,
        ),
      );
      _restartCamera();
    } catch (e) {
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Lỗi kết nối: $e'), backgroundColor: Colors.red),
      );
      _restartCamera();
    }
  }

  void _restartCamera() async {
    setState(() {
      _isProcessing = false;
      _isPickingImage = false;
    });
    if (mounted) {
      await cameraController.start();
    }
  }

  // Sự kiện quét từ Camera
  void _onDetect(ms.BarcodeCapture capture) {
    if (_isProcessing || _isPickingImage) return;
    final List<ms.Barcode> barcodes = capture.barcodes;
    if (barcodes.isNotEmpty && barcodes.first.rawValue != null) {
      _processTourCode(barcodes.first.rawValue!);
    }
  }

  // Sự kiện quét từ Thư viện ảnh
  void _scanImageFromGallery() async {
    if (_isPickingImage || _isProcessing) return;

    setState(() {
      _isPickingImage = true;
    });

    try {
      final XFile? image = await _picker.pickImage(source: ImageSource.gallery);
      if (image != null) {
        // MobileScanner 5.x dùng analyzeImage trả về bool nếu tìm thấy barcode
        // Nó sẽ trigger callback _onDetect nếu tìm thấy
        final bool barcodeFound = await cameraController.analyzeImage(
          image.path,
        );

        if (!barcodeFound) {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Không tìm thấy mã QR trong ảnh.'),
              backgroundColor: Colors.orange,
            ),
          );
          setState(() {
            _isPickingImage = false;
          });
        }
      } else {
        setState(() {
          _isPickingImage = false;
        });
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Lỗi ảnh: $e'), backgroundColor: Colors.red),
      );
      setState(() {
        _isPickingImage = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    // Kích thước khung quét (70% chiều rộng màn hình)
    final double scanAreaSize = MediaQuery.of(context).size.width * 0.7;

    return Scaffold(
      body: Stack(
        children: [
          // 1. Camera View
          ms.MobileScanner(controller: cameraController, onDetect: _onDetect),

          // 2. Lớp phủ mờ có lỗ khoét ở giữa (ColorFiltered)
          ColorFiltered(
            colorFilter: ColorFilter.mode(
              Colors.black.withOpacity(0.5),
              BlendMode.srcOut,
            ),
            child: Stack(
              children: [
                Container(
                  decoration: const BoxDecoration(color: Colors.transparent),
                ),
                Align(
                  alignment: Alignment.center,
                  child: Container(
                    width: scanAreaSize,
                    height: scanAreaSize,
                    decoration: BoxDecoration(
                      color: Colors.black,
                      borderRadius: BorderRadius.circular(20),
                    ),
                  ),
                ),
              ],
            ),
          ),

          // 3. Viền trắng bao quanh lỗ khoét
          Align(
            alignment: Alignment.center,
            child: Container(
              width: scanAreaSize,
              height: scanAreaSize,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                  color: Colors.white.withOpacity(0.7),
                  width: 2,
                ),
              ),
            ),
          ),

          // 4. Nút Đóng (Góc trái trên)
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

          // 5. Nút Flash (Góc phải trên)
          Align(
            alignment: Alignment.topRight,
            child: Padding(
              padding: const EdgeInsets.only(top: 50.0, right: 15.0),
              child: IconButton(
                icon: ValueListenableBuilder(
                  valueListenable: cameraController.torchState,
                  builder: (context, state, child) {
                    final color = state == ms.TorchState.on
                        ? primaryBlue
                        : Colors.white;
                    return Icon(Icons.flash_on, color: color, size: 30);
                  },
                ),
                onPressed: () => cameraController.toggleTorch(),
              ),
            ),
          ),

          // 6. Tiêu đề hướng dẫn
          Align(
            alignment: Alignment.topCenter,
            child: Padding(
              padding: const EdgeInsets.only(top: 100.0),
              child: Text(
                'Quét mã QR Tour',
                style: TextStyle(
                  color: Colors.white.withOpacity(0.9),
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),

          // 7. Nút Chọn ảnh từ thư viện
          Align(
            alignment: Alignment.bottomCenter,
            child: Padding(
              padding: const EdgeInsets.only(bottom: 40, left: 20, right: 20),
              child: ElevatedButton.icon(
                onPressed: (_isProcessing || _isPickingImage)
                    ? null
                    : _scanImageFromGallery,
                icon: const Icon(Icons.image, color: Colors.white),
                label: const Text(
                  'Quét QR từ Thư viện Ảnh',
                  style: TextStyle(fontSize: 18, color: Colors.white),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: primaryBlue,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 15,
                  ),
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
