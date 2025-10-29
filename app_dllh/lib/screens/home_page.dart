// File: HomePage.dart
import 'package:flutter/material.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'login_page.dart'; 
import 'tour_scanner_page.dart'; // Import màn hình QR Scanner Tour
import 'qr_login_scanner_page.dart'; // 🔑 Import màn hình QR Scanner Đăng nhập Web

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
  bool _loggingOut = false; // trạng thái khi đang logout
  
  // State quản lý tab đang được chọn trong Bottom Navigation Bar
  int _selectedIndex = 0; 
  
  // Dữ liệu mẫu (mock data) cho giao diện
  final List<Map<String, dynamic>> _exclusivePackages = [
    {
      'title': 'Golden Temple Tour',
      'subtitle': 'Khám phá Ấn Độ',
      'price': '450\$',
      'image': 'https://placehold.co/150x180/007AFF/ffffff?text=Golden+Temple',
      'rating': 4.5
    },
    {
      'title': 'Machu Picchu Trek',
      'subtitle': 'Phiêu lưu ở Peru',
      'price': '899\$',
      'image': 'https://placehold.co/150x180/FF6347/ffffff?text=Machu+Picchu',
      'rating': 4.8
    },
    {
      'title': 'Great Wall Hike',
      'subtitle': 'Chinh phục Trung Quốc',
      'price': '350\$',
      'image': 'https://placehold.co/150x180/3CB371/ffffff?text=Great+Wall',
      'rating': 4.2
    },
  ];

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
      // Xử lý chuyển tab thông thường (Home, Favorite, Inbox, Setting)
      setState(() {
        _selectedIndex = index;
      });
    }
  }


  // =========================================================
  // WIDGETS CỦA GIAO DIỆN HOME_SCREEN
  // =========================================================

  Widget _buildHeader(BuildContext context) {
    // Lấy tên hiển thị: ưu tiên 'fullname' nếu có, không thì dùng userID
    final displayedName = widget.userData?['fullname']?.toString().isNotEmpty == true
        ? widget.userData!['fullname'].toString()
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
  Widget _buildPackageList(BuildContext context) {
    return Container(
      height: 200, // Chiều cao cố định cho ListView ngang
      padding: const EdgeInsets.only(left: 24.0),
      child: ListView.builder(
        scrollDirection: Axis.horizontal,
        itemCount: _exclusivePackages.length,
        itemBuilder: (context, index) {
          final item = _exclusivePackages[index];
          return Container(
            width: 150,
            margin: const EdgeInsets.only(right: 16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(16),
              boxShadow: [
                BoxShadow(
                  color: Colors.grey.withOpacity(0.1),
                  spreadRadius: 1,
                  blurRadius: 5,
                  offset: const Offset(0, 3),
                ),
              ],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Hình ảnh giả lập
                ClipRRect(
                  borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                  child: Image.network(
                    item['image'] as String,
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
                        item['title'] as String,
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: darkTextColor),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        item['subtitle'] as String,
                        style: const TextStyle(fontSize: 12, color: Colors.black54),
                      ),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          const Icon(Icons.star, color: Colors.amber, size: 14),
                          const SizedBox(width: 4),
                          Text(
                            item['rating'].toString(),
                            style: const TextStyle(fontSize: 12, color: Colors.black87),
                          ),
                          const Spacer(),
                          Text(
                            item['price'] as String,
                            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: primaryBlue),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  // Danh mục khám phá (Explore Categories)
  Widget _buildExploreCategories() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24.0),
      child: GridView.builder(
        shrinkWrap: true, // Quan trọng: GridView trong SingleChildScrollView
        physics: const NeverScrollableScrollPhysics(),
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 4, // 4 cột
          crossAxisSpacing: 16,
          mainAxisSpacing: 16,
          childAspectRatio: 0.8, // Tỉ lệ chiều rộng/chiều cao
        ),
        itemCount: _exploreCategories.length,
        itemBuilder: (context, index) {
          final item = _exploreCategories[index];
          return Column(
            children: [
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: lightGreyBackground,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(item['icon'] as IconData, color: primaryBlue, size: 30),
              ),
              const SizedBox(height: 4),
              Text(
                item['title'] as String,
                style: const TextStyle(fontSize: 14, color: darkTextColor),
                textAlign: TextAlign.center,
              ),
            ],
          );
        },
      ),
    );
  }

  // Tiêu đề Recommended Packages
  Widget _buildRecommendedTabs() {
    return const Padding(
      padding: EdgeInsets.symmetric(horizontal: 24.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            'Trending Now',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: primaryBlue),
          ),
          Text(
            'New Deals',
            style: TextStyle(fontSize: 16, color: Colors.black54),
          ),
          Text(
            'Luxury',
            style: TextStyle(fontSize: 16, color: Colors.black54),
          ),
        ],
      ),
    );
  }

  // Danh sách Recommended Packages (dạng List dọc)
  Widget _buildRecommendedPackages(BuildContext context) {
    // Dùng lại dữ liệu Exclusive Packages làm Recommended
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
      child: Column(
        children: _exclusivePackages.map((item) => Padding(
          padding: const EdgeInsets.only(bottom: 16),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Hình ảnh giả lập
              ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Image.network(
                  item['image'] as String,
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
                      item['title'] as String,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                        color: darkTextColor,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      item['subtitle'] as String,
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
                        Text(item['rating'].toString()),
                        const Spacer(),
                        Text(
                          item['price'] as String,
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: primaryBlue),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
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
          icon: Icon(Icons.settings),
          label: 'Setting',
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      // SafeArea đảm bảo nội dung không bị che bởi thanh trạng thái
      body: SafeArea(
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // 1. Header Section (Bao gồm thông tin user, nút QR Web và nút logout)
              _buildHeader(context),
              
              const SizedBox(height: 24),

              // 2. Exclusive Package Section
              _buildSectionTitle('Exclusive Package'),
              _buildCategoryChips(),
              _buildPackageList(context),

              const SizedBox(height: 32),

              // 3. Explore Category Section
              _buildSectionTitle('Explore Category'),
              _buildExploreCategories(),

              const SizedBox(height: 32),

              // 4. Recommended Package Section
              _buildSectionTitle('Recommended Package'),
              _buildRecommendedTabs(),
              _buildRecommendedPackages(context),

              const SizedBox(height: 40),
            ],
          ),
        ),
      ),
      // 5. Bottom Navigation Bar
      bottomNavigationBar: _buildBottomNavigationBar(),
    );
  }
}