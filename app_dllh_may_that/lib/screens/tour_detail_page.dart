import 'package:flutter/material.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/screens/booking_page.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'package:app_dllh/services/api_client.dart';
import 'dart:convert';
import 'package:intl/intl.dart';

// --- BỘ MÀU WEB STYLE ---
const Color primaryGreen = Color(0xFF86B817);
const Color primaryDark = Color(0xFF13357B);
const Color darkTextColor = Color(0xFF2C3E50);
const Color lightGreyBackground = Color(0xFFF8F9FA);

class TourDetailPage extends StatefulWidget {
  final Tour tour;
  final String userID;

  const TourDetailPage({Key? key, required this.tour, required this.userID}) : super(key: key);

  @override
  State<TourDetailPage> createState() => _TourDetailPageState();
}

class _TourDetailPageState extends State<TourDetailPage> {
  final ApiClient _apiClient = ApiClient();
  late Future<Tour> _tourFuture;

  @override
  void initState() {
    super.initState();
    _tourFuture = _fetchTourDetail();
  }

  Future<Tour> _fetchTourDetail() async {
    try {
      final response = await _apiClient.getJson('get_tour.php?id=${widget.tour.maTour}');
      if (response.statusCode == 200) {
        final body = jsonDecode(response.body);
        if (body['success'] == true && body['data'] != null) {
          return Tour.fromJson(body['data']);
        }
      }
    } catch (e) {
      print("Lỗi làm mới tour: $e");
    }
    return widget.tour;
  }

  @override
  Widget build(BuildContext context) {
    final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

    return Scaffold(
      backgroundColor: lightGreyBackground,
      body: FutureBuilder<Tour>(
        future: _tourFuture,
        initialData: widget.tour,
        builder: (context, snapshot) {
          final tour = snapshot.data!;
          
          double price = 0;
          try {
            String cleanPrice = tour.giaNguoiLon?.toString().replaceAll(RegExp(r'[^0-9]'), '') ?? '0';
            price = double.parse(cleanPrice);
          } catch (_) {}

          return Stack(
            children: [
              // 1. ẢNH HEADER (Cố định)
              Positioned(
                top: 0, left: 0, right: 0, height: 300,
                child: ImageHelper.imageFromData(
                  tour.imageData,
                  mime: tour.imageMime,
                  width: double.infinity, height: 300, fit: BoxFit.cover,
                ),
              ),
              
              // 2. NÚT BACK & REFRESH
              Positioned(
                top: 40, left: 20, right: 20,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    _buildGlassButton(
                      icon: Icons.arrow_back_ios_new,
                      onTap: () => Navigator.pop(context),
                    ),
                    _buildGlassButton(
                      icon: Icons.refresh,
                      onTap: () {
                        setState(() { _tourFuture = _fetchTourDetail(); });
                      },
                    ),
                  ],
                ),
              ),

              // 3. NỘI DUNG CHÍNH (Cuộn được)
              Positioned.fill(
                top: 260,
                child: Container(
                  decoration: const BoxDecoration(
                    color: lightGreyBackground,
                    borderRadius: BorderRadius.vertical(top: Radius.circular(30)),
                  ),
                  // Padding bottom = 0 vì nút đặt vé đã ở bottomNavigationBar
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(24, 30, 24, 24), 
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          tour.tieuDe,
                          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: primaryDark, height: 1.3),
                        ),
                        const SizedBox(height: 8),
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Icon(Icons.location_on, color: primaryGreen, size: 18),
                            const SizedBox(width: 4),
                            Expanded(
                              child: Text(
                                '${tour.noiDen ?? 'N/A'}${tour.thanhPho != null ? ', ${tour.thanhPho}' : ''}',
                                style: TextStyle(fontSize: 15, color: Colors.grey[600], fontWeight: FontWeight.w500),
                                maxLines: 2, overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ],
                        ),
                        
                        const SizedBox(height: 24),

                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(child: _buildInfoBox(Icons.calendar_month, 'Ngày đi', tour.thoiGian?.toString() ?? 'N/A')),
                            const SizedBox(width: 12),
                            Expanded(child: _buildInfoBox(Icons.place_outlined, 'Khởi hành', tour.noiKhoiHanh ?? 'N/A')),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(child: _buildInfoBox(
                              Icons.groups_outlined, 
                              'Còn trống', 
                              '${tour.soChoConLai ?? tour.soLuong ?? 0} chỗ'
                            )),
                            const SizedBox(width: 12),
                            Expanded(child: _buildInfoBox(Icons.storefront, 'Chi nhánh', tour.chiNhanh ?? 'Trụ sở chính')),
                          ],
                        ),

                        const SizedBox(height: 30),

                        const Text("GIỚI THIỆU", style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: primaryDark)),
                        const SizedBox(height: 10),
                        Text(
                          tour.moTa ?? 'Chưa có mô tả chi tiết.',
                          style: TextStyle(fontSize: 15, color: Colors.grey[700], height: 1.6),
                          textAlign: TextAlign.justify,
                        ),

                        const SizedBox(height: 30),

                        Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(color: Colors.grey.shade200),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text("BẢNG GIÁ", style: TextStyle(fontWeight: FontWeight.bold, color: primaryDark)),
                              const Divider(height: 20),
                              _buildPriceRow('Người lớn', tour.giaNguoiLon),
                              const SizedBox(height: 8),
                              _buildPriceRow('Trẻ em', tour.giaTreEm),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ],
          );
        },
      ),

      // 4. THANH ĐẶT VÉ (Sử dụng bottomNavigationBar để không che nội dung)
      bottomNavigationBar: FutureBuilder<Tour>(
        future: _tourFuture,
        initialData: widget.tour,
        builder: (context, snapshot) {
           final tour = snapshot.data!;
           double price = 0;
           try {
              String cleanPrice = tour.giaNguoiLon?.toString().replaceAll(RegExp(r'[^0-9]'), '') ?? '0';
              price = double.parse(cleanPrice);
           } catch (_) {}

           return Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: Colors.white,
              boxShadow: [
                BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 10, offset: const Offset(0, -5))
              ],
            ),
            child: SafeArea(
              child: Row(
                children: [
                  Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text("Giá từ:", style: TextStyle(fontSize: 12, color: Colors.grey)),
                      Text(
                        price > 0 ? currencyFormat.format(price) : 'Liên hệ',
                        style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: primaryGreen),
                      ),
                    ],
                  ),
                  const Spacer(),
                  ElevatedButton(
                    onPressed: () async {
                      final result = await Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) => BookingPage(tour: tour, userID: widget.userID),
                        ),
                      );
                      if (result != null && result is Map && result['success'] == true) {
                        setState(() { _tourFuture = _fetchTourDetail(); }); // Reload lại số chỗ
                      }
                    },
                    style: ElevatedButton.styleFrom(
                      backgroundColor: primaryDark,
                      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      elevation: 4,
                    ),
                    child: const Text('ĐẶT NGAY', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white)),
                  ),
                ],
              ),
            ),
          );
        }
      ),
    );
  }

  Widget _buildGlassButton({required IconData icon, required VoidCallback onTap}) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: Colors.white.withOpacity(0.9),
          shape: BoxShape.circle,
          boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.1), blurRadius: 8)],
        ),
        child: Icon(icon, color: primaryDark, size: 20),
      ),
    );
  }

  Widget _buildInfoBox(IconData icon, String label, String value) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade200),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(color: primaryGreen.withOpacity(0.1), shape: BoxShape.circle),
            child: Icon(icon, size: 18, color: primaryGreen),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: const TextStyle(fontSize: 11, color: Colors.grey)),
                const SizedBox(height: 2),
                Text(
                  value, 
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: primaryDark),
                  softWrap: true,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPriceRow(String label, String? price) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(color: Colors.grey)),
        Text(
          price ?? 'Liên hệ',
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: darkTextColor),
        ),
      ],
    );
  }
}