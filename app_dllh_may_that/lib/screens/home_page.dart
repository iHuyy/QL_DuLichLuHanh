// File: HomePage.dart
import 'dart:convert';
import 'package:flutter/material.dart';
// import 'package:http/http.dart' as http; // XÓA DÒNG NÀY
import 'package:app_dllh/services/api_client.dart'; // THÊM DÒNG NÀY
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/models/tour.dart'; // <-- sử dụng model mới
import 'tour_detail_page.dart'; // <-- thêm import để điều hướng sang trang chi tiết
import 'profile_page.dart';
import 'my_booking_page.dart';
import 'invoices_page.dart';
import 'login_page.dart';
import 'tour_scanner_page.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'qr_login_scanner_page.dart';
import 'package:app_dllh/config/app_config.dart';

// Màu xanh chính (Primary Blue) và Màu đen đậm (Dark Black)
const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);
const Color lightGreyBackground = Color(0xFFF2F2F7);

// Định nghĩa GlobalKey cho State của MyBookingPage và InvoicesPage
// Cần khai báo này ở đây để sử dụng trong _HomePageState
final GlobalKey<MyBookingPageState> _myBookingKey = GlobalKey<MyBookingPageState>();
final GlobalKey<InvoicesPageState> _invoicesKey = GlobalKey<InvoicesPageState>();

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
  final ApiClient _apiClient = ApiClient(); // THÊM DÒNG NÀY

  bool _loggingOut = false;
  int _selectedIndex = 0;
  String _selectedBranch = '';
  int? _selectedBranchId;
  String _searchQuery = '';

  late Future<List<Tour>> _toursFuture;
  late Future<List<Map<String, dynamic>>> _branchesFuture;

  @override
  void initState() {
    super.initState();
    _toursFuture = _fetchTours();
    _branchesFuture = _fetchBranches();
  }
  
  // Đã xóa didChangeDependencies

  Future<List<Tour>> _fetchTours() async {
    // SỬA LỖI: Dùng _apiClient.getJson thay vì http.get
    final response = await _apiClient.getJson('get_tours.php');

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

  Future<List<Map<String, dynamic>>> _fetchBranches() async {
    final uri = '${AppConfig.baseUrl}/get_branches.php'; // (Chỉ để log)
    print('Fetching branches from: $uri');
    try {
      // SỬA LỖI: Dùng _apiClient.getJson thay vì http.get
      final response = await _apiClient.getJson('get_branches.php');

      if (response.statusCode != 200) {
        print('Branches API error: ${response.statusCode}');
        return [];
      }
      final body = response.body.trim();
      print('Branches response: $body');
      
      if (body.isEmpty) return [];
      if (body.startsWith('<')) {
        print('Server returned HTML instead of JSON');
        return [];
      }

      final decoded = json.decode(body);
      if (decoded is List) {
        print('Decoded branches: $decoded');
        return List<Map<String, dynamic>>.from(decoded);
      } else {
        print('Invalid branches structure');
        return [];
      }
    } catch (e) {
      print('Failed to fetch branches: $e');
      return [];
    }
  }

  // =========================================================
  // LOGIC XỬ LÝ (Giữ nguyên)
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
      // ⚠️ Logic Refresh khi chuyển tab
      if (index == 1) { // Favorite/My Booking Tab
        _myBookingKey.currentState?.refreshData();
      } else if (index == 3) { // Inbox/Invoices Tab
        _invoicesKey.currentState?.refreshData();
      }

      // Xử lý chuyển tab thông thường (Home, Favorite, Inbox, Profile)
      setState(() {
        _selectedIndex = index;
      });
    }
  }


  // =========================================================
  // WIDGETS CỦA GIAO DIỆN HOME_SCREEN (Giữ nguyên)
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

  // Search bar widget
  Widget _buildSearchBar() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
      child: TextField(
        onChanged: (value) {
          setState(() {
            _searchQuery = value.toLowerCase();
          });
        },
        decoration: InputDecoration(
          hintText: 'Search tours by name or destination...',
          prefixIcon: const Icon(Icons.search, color: Colors.grey),
          suffixIcon: _searchQuery.isNotEmpty
              ? IconButton(
                  icon: const Icon(Icons.clear, color: Colors.grey),
                  onPressed: () {
                    setState(() {
                      _searchQuery = '';
                    });
                  },
                )
              : null,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Colors.grey, width: 1),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Colors.grey, width: 1),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: primaryBlue, width: 2),
          ),
          contentPadding: const EdgeInsets.symmetric(vertical: 12, horizontal: 16),
        ),
      ),
    );
  }

  // Chip phân loại theo chi nhánh
  Widget _buildBranchChips(List<Map<String, dynamic>> branches) {
    // Helper function to safely get branch name
    String getBranchName(Map<String, dynamic> branch) {
      // Try different key variations
      for (var key in branch.keys) {
        if (key.toString().toUpperCase().contains('TENCHINHАNH') || 
            key.toString().toUpperCase().contains('TENCHINHANH')) {
          return branch[key]?.toString() ?? 'Unknown';
        }
      }
      // Fallback to common names
      return branch['TenChiNhanh'] ?? branch['tenChiNhanh'] ?? 'Unknown';
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24.0),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            Padding(
              padding: const EdgeInsets.only(right: 8.0),
              child: FilterChip(
                label: const Text('All', style: TextStyle(fontWeight: FontWeight.bold)),
                selected: _selectedBranch.isEmpty && _selectedBranchId == null,
                backgroundColor: lightGreyBackground,
                selectedColor: primaryBlue,
                labelStyle: TextStyle(
                  color: _selectedBranch.isEmpty ? Colors.white : darkTextColor,
                  fontWeight: FontWeight.bold,
                ),
                onSelected: (selected) {
                  setState(() {
                    _selectedBranch = '';
                    _selectedBranchId = null;
                  });
                },
              ),
            ),
            ...branches.map((branch) {
              final branchName = getBranchName(branch);
              // try to read MaChiNhanh as int if present
              int? branchId;
              try {
                final rawId = branch['MaChiNhanh'] ?? branch['MACHINHANH'] ?? branch['MaChiNhanh'];
                if (rawId != null) branchId = int.tryParse(rawId.toString());
              } catch (_) {}

              final isSelected = (_selectedBranchId != null && branchId == _selectedBranchId) ||
                  (_selectedBranchId == null && _selectedBranch == branchName);

              return Padding(
                padding: const EdgeInsets.only(right: 8.0),
                child: FilterChip(
                  label: Text(
                    branchName,
                    style: TextStyle(
                      color: isSelected ? Colors.white : darkTextColor,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  selected: isSelected,
                  backgroundColor: lightGreyBackground,
                  selectedColor: primaryBlue,
                  onSelected: (selected) {
                    setState(() {
                      if (selected) {
                        _selectedBranch = branchName;
                        _selectedBranchId = branchId;
                      } else {
                        _selectedBranch = '';
                        _selectedBranchId = null;
                      }
                    });
                  },
                ),
              );
            }).toList(),
          ],
        ),
      ),
    );
  }

  // Display all tours as vertical list
  Widget _buildTourList(BuildContext context, List<Tour> tours) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
      child: Column(
        children: tours.map((tour) => Padding(
          padding: const EdgeInsets.only(bottom: 16),
          child: GestureDetector(
            onTap: () async { // Thêm async để chờ kết quả từ TourDetailPage
              final shouldRefresh = await Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => TourDetailPage(tour: tour, userID: widget.userID)),
              );

              // Nếu TourDetailPage trả về true (sau khi BookingPage pop thành công)
              if (shouldRefresh == true) {
                // Refresh My Booking Page
                _myBookingKey.currentState?.refreshData();
                // Chuyển sang tab My Booking
                setState(() {
                  _selectedIndex = 1; 
                });
              }
            },
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: ImageHelper.imageFromData(
                    tour.imageData ?? '',
                    mime: tour.imageMime,
                    width: 90,
                    height: 90,
                    fit: BoxFit.cover,
                    borderRadius: BorderRadius.circular(12),
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
                          const Text('4.2'),
                          const Spacer(),
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
      builder: (context, toursSnapshot) {
        if (toursSnapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }
        if (toursSnapshot.hasError) {
          return Center(child: Padding(
            padding: const EdgeInsets.all(16.0),
            child: Text('Error loading tours: ${toursSnapshot.error}', style: const TextStyle(color: Colors.red)),
          ));
        }
        final allTours = toursSnapshot.data ?? <Tour>[];
        // Debug: list tour branch values
        try {
          final branchesInTours = allTours.map((t) => t.chiNhanh ?? '').toList();
          print('Tours loaded - chiNhanh values: $branchesInTours');
        } catch (_) {}
        
        return FutureBuilder<List<Map<String, dynamic>>>(
          future: _branchesFuture,
          builder: (context, branchesSnapshot) {
            List<Map<String, dynamic>> branches = [];
            if (branchesSnapshot.connectionState == ConnectionState.done && branchesSnapshot.hasData) {
              branches = branchesSnapshot.data ?? [];
            }
            
            // Filter tours by selected branch AND search query
            final filteredTours = allTours.where((tour) {
              // Apply branch filter
              if (_selectedBranchId != null || _selectedBranch.isNotEmpty) {
                final tourBranchRaw = tour.chiNhanh ?? '';
                final tourBranchId = int.tryParse(tourBranchRaw);
                if (_selectedBranchId != null && tourBranchId != null) {
                  if (tourBranchId != _selectedBranchId) return false;
                } else if (_selectedBranch.isNotEmpty) {
                  final tourBranchName = tourBranchRaw.toString().trim().toLowerCase();
                  final selectedName = _selectedBranch.toString().trim().toLowerCase();
                  if (tourBranchName != selectedName) return false;
                }
              }
              // Apply search filter (search in title, description, destination)
              if (_searchQuery.isNotEmpty) {
                final title = tour.tieuDe.toLowerCase();
                final desc = (tour.moTa ?? '').toLowerCase();
                final dest = (tour.noiDen ?? '').toLowerCase();
                return title.contains(_searchQuery) || desc.contains(_searchQuery) || dest.contains(_searchQuery);
              }
              return true;
            }).toList();

            return SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _buildHeader(context),
                  const SizedBox(height: 16),
                  _buildSearchBar(),
                  const SizedBox(height: 16),
                  _buildSectionTitle('Lọc Theo Chi Nhánh'),
                  _buildBranchChips(branches),
                  const SizedBox(height: 24),
                  _buildSectionTitle('All Tours'),
                  if (filteredTours.isEmpty)
                    Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Center(
                        child: Text(
                          'No tours found',
                          style: TextStyle(fontSize: 16, color: Colors.grey[600]),
                        ),
                      ),
                    )
                  else
                    _buildTourList(context, filteredTours),
                  const SizedBox(height: 40),
                ],
              ),
            );
          },
        );
      },
    );
  }

  // Placeholder cho tab Favorite (My Booking Page)
  Widget _buildFavoriteTab() {
    // Truyền key vào MyBookingPage
    return MyBookingPage(key: _myBookingKey, userID: widget.userID);
  }

  // Placeholder cho tab Inbox (Invoices Page)
  Widget _buildInboxTab() {
    // Truyền key vào InvoicesPage
    return InvoicesPage(key: _invoicesKey, userID: widget.userID);
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