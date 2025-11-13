// File: HomePage.dart
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/models/tour.dart'; // <-- sử dụng model mới
import 'tour_detail_page.dart'; // <-- thêm import để điều hướng sang trang chi tiết
import 'profile_page.dart';
import 'login_page.dart';
import 'tour_scanner_page.dart';
import 'qr_login_scanner_page.dart';

// Màu xanh chính (Primary Blue) và Màu đen đậm (Dark Black)
const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

class HomePage extends StatefulWidget {
  final String userID;
  final String role; 
  // Dữ liệu người dùng tạm thời. Trong ứng dụng thực tế, nên dùng Model User
  final Map<String, dynamic>? userData; 

  const HomePage({
    Key? key, 
    required this.userID, 
    this.role = 'DEFAULT',
    this.userData,
  }) : super(key: key);

  @override
  _HomePageState createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  final AuthService _authService = AuthService();
  bool _loggingOut = false;
  int _selectedIndex = 0;

  late Future<List<Tour>> _toursFuture;

  @override
  void initState() {
    super.initState();
    _toursFuture = _fetchTours();
  }

  Future<List<Tour>> _fetchTours() async {
    final uri = Uri.parse('http://10.0.2.2/KLTN/get_tours.php');
    final response = await http.get(uri);

    // Nếu server trả HTML warning/error, báo rõ để debug
    if (response.statusCode != 200) {
      throw Exception('HTTP ${response.statusCode}: ${response.reasonPhrase}');
    }
    final body = response.body.trim();
    if (body.startsWith('<')) {
      // server trả HTML (warning/notice) trước JSON
      throw Exception('Server returned HTML instead of JSON: ${body.substring(0, body.length.clamp(0, 200))}');
    }

    try {
      final decoded = json.decode(body);
      if (decoded is List) {
        return decoded.map<Tour>((e) {
          if (e is Map<String, dynamic>) return Tour.fromJson(e);
          return Tour.fromJson(Map<String, dynamic>.from(e));
        }).toList();
      } else {
        throw Exception('Invalid JSON structure for tours');
      }
    } catch (e) {
      throw Exception('Failed to parse tours JSON: $e\nBody: ${body.length > 500 ? body.substring(0,500) : body}');
    }
  }

  final List<Map<String, dynamic>> _exploreCategories = [
    {'icon': Icons.airplane_ticket, 'title': 'Flights'},
    {'icon': Icons.hotel, 'title': 'Hotels'},
    {'icon': Icons.train, 'title': 'Trains'},
    {'icon': Icons.directions_bus, 'title': 'Buses'},
    {'icon': Icons.attractions, 'title': 'Attractions'},
    {'icon': Icons.more_horiz, 'title': 'More'},
  ];


  // =========================================================
  // LOGIC XỬ LÝ
  // =========================================================

  Future<void> _logout() async {
    setState(() {
      _loggingOut = true;
    });
    
    // Gọi API đăng xuất
    final result = await _authService.logout();
    
    setState(() {
      _loggingOut = false;
    });

    if (result['success'] == true) {
      // Đăng xuất thành công, chuyển về màn hình đăng nhập
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (context) => const LoginPage()), 
        (Route<dynamic> route) => false,
      );
    } else {
      // Hiển thị lỗi nếu có
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result['message'] ?? 'Đăng xuất thất bại.'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }
  
  // 🔑 Logic xử lý Quét QR để Đăng nhập Web (MỚI)
  Future<void> _navigateToWebLoginQR() async {
    final webLoginToken = await Navigator.of(context).push(
      MaterialPageRoute(
        // Sử dụng màn hình quét cho chức năng Đăng nhập Web
        builder: (context) => const QRLoginScannerPage(), 
      ),
    );

    // Nếu có token (mã QR) được trả về từ máy quét
    if (webLoginToken != null && webLoginToken is String) {
      // ⚠️ TODO: GỌI API để xác thực phiên đăng nhập web
      
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Đã quét mã QR Đăng nhập Web: $webLoginToken. Đang xác thực...'),
              backgroundColor: primaryBlue,
              duration: const Duration(seconds: 4),
          ),
      );
      
      // Sau khi gọi API thành công, bạn có thể thực hiện các hành động tiếp theo
    }
  }
  
  // Xử lý khi nhấn vào Bottom Navigation Bar
  void _onItemTapped(int index) {
    if (index == 2) { // Vị trí thứ 3 là QR Code (index 2)
      // Điều hướng đến màn hình quét QR TOUR
      Navigator.of(context).push(
        MaterialPageRoute(builder: (context) => const TourScannerPage()),
      );
      // Giữ cho Home (index 0) vẫn sáng trên thanh navigation sau khi quay lại
      // Không cần gọi setState nếu không muốn thay đổi trạng thái index của thanh nav
    } else {
      // Xử lý chuyển tab thông thường (Home, Favorite, Inbox, Profile)
      setState(() {
        _selectedIndex = index;
      });
    }
  }


  // =========================================================
  // WIDGETS CỦA GIAO DIỆN HOME_SCREEN
  // =========================================================

  Widget _buildHeader(BuildContext context) {
    final fullnameFromData = widget.userData != null
        ? (widget.userData!['fullname'] ?? widget.userData!['username'] ?? '')
        : '';
    final displayedName = (fullnameFromData != null && fullnameFromData.toString().trim().isNotEmpty)
        ? fullnameFromData.toString()
        : widget.userID;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Row chứa thông tin người dùng và các nút hành động
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Welcome back,',
                    style: TextStyle(
                      fontSize: 16,
                      color: Colors.black54,
                    ),
                  ),
                  Text(
                    // Hiển thị tên người dùng và Role
                    '$displayedName (${widget.role})',
                    style: const TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.bold,
                      color: darkTextColor,
                    ),
                  ),
                ],
              ),
              
              // 🔑 Row chứa Nút QR Đăng nhập Web và Nút Đăng xuất (ĐÃ SỬA)
              Row( 
                mainAxisSize: MainAxisSize.min,
                children: [
                  // 1. Nút Quét QR Đăng nhập Web (MỚI)
                  Container(
                    margin: const EdgeInsets.only(right: 8), // Khoảng cách với nút Đăng xuất
                    decoration: BoxDecoration(
                      color: lightGreyBackground,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: IconButton(
                      icon: const Icon(Icons.qr_code_scanner_outlined, color: primaryBlue), // Icon QR Đăng nhập
                      onPressed: _navigateToWebLoginQR, // Gọi hàm xử lý quét QR Web
                    ),
                  ),

                  // 2. Nút Đăng xuất (Đã có)
                  Container(
                    decoration: BoxDecoration(
                      color: lightGreyBackground,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: IconButton(
                      icon: _loggingOut
                          ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: primaryBlue))
                          : const Icon(Icons.logout, color: primaryBlue),
                      onPressed: _loggingOut ? null : _logout,
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 16),
          // Thanh tìm kiếm (chức năng mock)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            decoration: BoxDecoration(
              color: lightGreyBackground,
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Row(
              children: [
                Icon(Icons.search, color: Colors.grey),
                SizedBox(width: 8),
                Expanded(
                  child: TextField(
                    decoration: InputDecoration(
                      hintText: 'Search your destination...',
                      border: InputBorder.none,
                      isDense: true,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Tiêu đề phần
  Widget _buildSectionTitle(String title) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 8),
      child: Text(
        title,
        style: const TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.bold,
          color: darkTextColor,
        ),
      ),
    );
  }

  // Chip phân loại
  Widget _buildCategoryChips() {
    return const Padding(
      padding: EdgeInsets.symmetric(horizontal: 24.0),
      child: Row(
        children: [
          Chip(
            label: Text('Popular', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
            backgroundColor: primaryBlue,
          ),
          SizedBox(width: 8),
          Chip(
            label: Text('Europe'),
            backgroundColor: lightGreyBackground,
          ),
          SizedBox(width: 8),
          Chip(
            label: Text('Asia'),
            backgroundColor: lightGreyBackground,
          ),
        ],
      ),
    );
  }

  // Danh sách các gói độc quyền
  Widget _buildPackageList(BuildContext context, List<Tour> tours) {
    return Container(
      height: 200, // Chiều cao cố định cho ListView ngang
      padding: const EdgeInsets.only(left: 24.0),
      child: ListView.builder(
        scrollDirection: Axis.horizontal,
        itemCount: tours.length,
        itemBuilder: (context, index) {
          final tour = tours[index];
          return Padding(
            padding: const EdgeInsets.only(right: 16.0),
            child: GestureDetector(
              onTap: () {
                Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => TourDetailPage(tour: tour, userID: widget.userID)),
                );
              },
              child: Container(
                width: 150,
                margin: const EdgeInsets.only(top: 8, bottom: 8),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [BoxShadow(color: Colors.grey.withOpacity(0.08), blurRadius: 6)],
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    ClipRRect(
                      borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
                      child: Image.network(
                        'https://placehold.co/150x100/007AFF/ffffff?text=Tour',
                        height: 100,
                        width: 150,
                        fit: BoxFit.cover,
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(8.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            tour.tieuDe,
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: darkTextColor),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            tour.noiDen ?? 'N/A',
                            style: const TextStyle(fontSize: 12, color: Colors.black54),
                          ),
                          const SizedBox(height: 6),
                          Row(
                            children: [
                              const Icon(Icons.star, color: Colors.amber, size: 14),
                              const SizedBox(width: 6),
                              const Text('4.5', style: TextStyle(fontSize: 12)),
                              const Spacer(),
                              Text(
                                tour.giaNguoiLon ?? 'N/A',
                                style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: primaryBlue),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  // Danh sách Recommended Packages (dạng List dọc)
  Widget _buildRecommendedPackages(BuildContext context, List<Tour> tours) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
      child: Column(
        children: tours.map((tour) => Padding(
          padding: const EdgeInsets.only(bottom: 16),
          child: GestureDetector(
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => TourDetailPage(tour: tour,userID: widget.userID,)),
              );
            },
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Hình ảnh giả lập
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: Image.network(
                    'https://placehold.co/150x180/3CB371/ffffff?text=Tour',
                    height: 90,
                    width: 90,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        tour.tieuDe,
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                          color: darkTextColor,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        tour.noiDen ?? 'N/A',
                        style: const TextStyle(
                          fontSize: 14,
                          color: Colors.black54,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          const Icon(Icons.star, color: Colors.amber, size: 14),
                          const SizedBox(width: 4),
                          const Text('4.2'), // Placeholder rating
                          const Spacer(),
                          // Fix: hiển thị giá an toàn
                          Text(
                            tour.giaNguoiLon ?? 'N/A',
                            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: primaryBlue),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        )).toList(),
      ),
    );
  }
  
  // Thanh điều hướng dưới cùng (Bottom Navigation Bar)
  Widget _buildBottomNavigationBar() {
    return BottomNavigationBar(
      currentIndex: _selectedIndex, // Sử dụng state index
      onTap: _onItemTapped, // Gọi hàm xử lý khi nhấn
      backgroundColor: Colors.white,
      selectedItemColor: primaryBlue,
      unselectedItemColor: Colors.grey,
      type: BottomNavigationBarType.fixed, // Đảm bảo các item không bị dịch chuyển
      showUnselectedLabels: true,
      items: const [
        BottomNavigationBarItem(
          icon: Icon(Icons.home),
          label: 'Home',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.favorite_border),
          label: 'Favorite',
        ),
        // Thêm mục QR Code vào vị trí trung tâm
        BottomNavigationBarItem(
          icon: Icon(Icons.qr_code_scanner),
          label: 'QR Code',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.inbox),
          label: 'Inbox',
        ),
        BottomNavigationBarItem(
          icon: Icon(Icons.person_outline),
          label: 'Profile',
        ),
      ],
    );
  }

  // Xây dựng tab nội dung Home
  Widget _buildHomeTab(BuildContext context) {
    return FutureBuilder<List<Tour>>(
      future: _toursFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
        if (snapshot.hasError) {
          // Hiển thị lỗi rõ ràng trên giao diện để bạn debug nhanh
          return Center(child: Padding(
            padding: const EdgeInsets.all(16.0),
            child: Text('Error loading tours: ${snapshot.error}', style: const TextStyle(color: Colors.red)),
          ));
        }
        final tours = snapshot.data ?? <Tour>[];
        return SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _buildHeader(context),
              const SizedBox(height: 24),
              _buildSectionTitle('Exclusive Package'),
              _buildCategoryChips(),
              _buildPackageList(context, tours),
              const SizedBox(height: 32),
              _buildSectionTitle('Explore Category'),
              const SizedBox(height: 32),
              _buildSectionTitle('Recommended Package'),
              _buildRecommendedPackages(context, tours),
              const SizedBox(height: 40),
            ],
          ),
        );
      },
    );
  }

  // Placeholder cho tab Favorite
  Widget _buildFavoriteTab() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.favorite_border, size: 64, color: Colors.grey),
          const SizedBox(height: 16),
          const Text('Favorite Tours', style: TextStyle(fontSize: 18)),
          const SizedBox(height: 8),
          const Text('No favorites yet', style: TextStyle(color: Colors.grey)),
        ],
      ),
    );
  }

  // Placeholder cho tab Inbox
  Widget _buildInboxTab() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.inbox, size: 64, color: Colors.grey),
          const SizedBox(height: 16),
          const Text('Messages', style: TextStyle(fontSize: 18)),
          const SizedBox(height: 8),
          const Text('No messages yet', style: TextStyle(color: Colors.grey)),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: IndexedStack(
          index: _selectedIndex,
          children: [
            // Tab 0: Home
            _buildHomeTab(context),
            // Tab 1: Favorite
            _buildFavoriteTab(),
            // Tab 2: QR Code (không hiển thị tại đây vì nó push Navigator)
            Container(),
            // Tab 3: Inbox
            _buildInboxTab(),
            // Tab 4: Profile
            ProfileScreen(
              userID: widget.userID,
              userName: widget.userData?['username'] ?? widget.userID,
            ),
          ],
        ),
      ),
      bottomNavigationBar: _buildBottomNavigationBar(),
    );
  }
}