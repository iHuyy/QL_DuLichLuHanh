import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:app_dllh/services/api_client.dart';
import 'package:app_dllh/services/auth_service.dart';
import 'package:app_dllh/models/tour.dart';
import 'tour_detail_page.dart';
import 'profile_page.dart';
import 'my_booking_page.dart';
import 'invoices_page.dart';
import 'login_page.dart';
import 'tour_scanner_page.dart';
import 'package:app_dllh/utils/image_helper.dart';
import 'qr_login_scanner_page.dart';
import 'package:app_dllh/config/app_config.dart';
import 'package:intl/intl.dart';

// --- BỘ MÀU (GIỮ TÔNG MÀU WEB) ---
const Color primaryGreen = Color(0xFF86B817); // Xanh lá mạ
const Color primaryDark = Color(0xFF13357B); // Xanh đen đậm
const Color scaffoldBg = Color(0xFFF8F9FA); // Nền xám trắng sáng sủa
const Color cardColor = Colors.white;

final GlobalKey<MyBookingPageState> _myBookingKey =
    GlobalKey<MyBookingPageState>();
final GlobalKey<InvoicesPageState> _invoicesKey =
    GlobalKey<InvoicesPageState>();

class HomePage extends StatefulWidget {
  final String userID;
  final String role;
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
  final ApiClient _apiClient = ApiClient();
  final currencyFormat = NumberFormat.currency(locale: 'vi_VN', symbol: '₫');

  bool _loggingOut = false;
  int _selectedIndex = 0;
  String _selectedBranch = '';
  int? _selectedBranchId;
  String _searchQuery = '';
  String _userFullName = '';

  late Future<List<Tour>> _toursFuture;
  late Future<List<Map<String, dynamic>>> _branchesFuture;

  @override
  void initState() {
    super.initState();
    _toursFuture = _fetchTours();
    _branchesFuture = _fetchBranches();
    _fetchUserProfile();
  }

  Future<void> _fetchUserProfile() async {
    try {
      if (widget.userData != null && widget.userData!['fullname'] != null) {
        setState(() {
          _userFullName = widget.userData!['fullname'];
        });
        return;
      }
      final response = await _apiClient.getJson('get_user.php');
      if (response.statusCode == 200) {
        final body = json.decode(response.body);
        if (body['success'] == true && body['data'] != null) {
          setState(() {
            _userFullName = body['data']['fullName'] ?? '';
          });
        }
      }
    } catch (e) {
      print(e);
    }
  }

  Future<List<Tour>> _fetchTours() async {
    final response = await _apiClient.getJson('get_tours.php');
    if (response.statusCode != 200)
      throw Exception('HTTP ${response.statusCode}');
    final body = response.body.trim();
    if (body.startsWith('<')) throw Exception('Server Error');
    try {
      final decoded = json.decode(body);
      if (decoded is List) {
        return decoded
            .map<Tour>(
              (e) => e is Map<String, dynamic>
                  ? Tour.fromJson(e)
                  : Tour.fromJson(Map<String, dynamic>.from(e)),
            )
            .toList();
      }
      return [];
    } catch (e) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> _fetchBranches() async {
    try {
      final response = await _apiClient.getJson('get_branches.php');
      if (response.statusCode != 200) return [];
      final body = response.body.trim();
      if (body.isEmpty || body.startsWith('<')) return [];
      final decoded = json.decode(body);
      return decoded is List ? List<Map<String, dynamic>>.from(decoded) : [];
    } catch (e) {
      return [];
    }
  }

  Future<void> _logout() async {
    setState(() => _loggingOut = true);
    await _authService.logout();
    if (mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (context) => const LoginPage()),
        (Route<dynamic> route) => false,
      );
    }
  }

  Future<void> _navigateToWebLoginQR() async {
    final token = await Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (context) => const QRLoginScannerPage()));
    if (token != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Đang xử lý đăng nhập Web...'),
          backgroundColor: primaryGreen,
        ),
      );
    }
  }

  void _onItemTapped(int index) {
    if (index == 2) {
      // Logic quét QR (giữ nguyên)
      Navigator.of(
        context,
      ).push(MaterialPageRoute(builder: (context) => const TourScannerPage()));
    } else {
      // --- BỔ SUNG LOGIC RELOAD CHO HOME ---
      if (index == 0) {
        setState(() {
          // Gán lại Future để kích hoạt FutureBuilder chạy lại API
          _toursFuture = _fetchTours();
          // Nếu muốn cập nhật cả danh sách chi nhánh thì bỏ comment dòng dưới
          // _branchesFuture = _fetchBranches();
        });
      }
      // -------------------------------------

      if (index == 1) _myBookingKey.currentState?.refreshData();
      if (index == 3) _invoicesKey.currentState?.refreshData();

      setState(() => _selectedIndex = index);
    }
  }

  // --- WIDGETS GIAO DIỆN ---

  // Header đơn giản, không còn Banner
  Widget _buildHeader(BuildContext context) {
    final displayName = _userFullName.isNotEmpty
        ? _userFullName
        : widget.userID;
    final avatarChar = displayName.isNotEmpty
        ? displayName[0].toUpperCase()
        : 'U';

    return Container(
      color: Colors.white,
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
      child: Row(
        children: [
          // Avatar
          CircleAvatar(
            radius: 26,
            backgroundColor: primaryGreen.withOpacity(0.1),
            child: Text(
              avatarChar,
              style: const TextStyle(
                color: primaryGreen,
                fontWeight: FontWeight.bold,
                fontSize: 22,
              ),
            ),
          ),
          const SizedBox(width: 14),

          // Greeting
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Xin chào,',
                  style: TextStyle(color: Colors.grey, fontSize: 13),
                ),
                Text(
                  displayName,
                  style: const TextStyle(
                    color: primaryDark,
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),

          // Buttons (QR & Logout) - Màu tối trên nền trắng
          Row(
            children: [
              _buildCircleBtn(Icons.qr_code_scanner, _navigateToWebLoginQR),
              const SizedBox(width: 10),
              _buildCircleBtn(Icons.logout, _logout, isLoading: _loggingOut),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildCircleBtn(
    IconData icon,
    VoidCallback onTap, {
    bool isLoading = false,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(50),
      child: Container(
        width: 42,
        height: 42,
        decoration: BoxDecoration(
          color: Colors.white,
          shape: BoxShape.circle,
          border: Border.all(color: Colors.grey.shade200),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.05),
              blurRadius: 5,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: isLoading
            ? const Padding(
                padding: EdgeInsets.all(12),
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: primaryDark,
                ),
              )
            : Icon(icon, color: primaryDark, size: 20),
      ),
    );
  }

  Widget _buildSearchBar() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.grey.shade200),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.03),
              blurRadius: 10,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: TextField(
          onChanged: (val) => setState(() => _searchQuery = val.toLowerCase()),
          decoration: InputDecoration(
            hintText: 'Bạn muốn đi đâu?',
            hintStyle: TextStyle(color: Colors.grey.shade400, fontSize: 14),
            prefixIcon: const Icon(Icons.search, color: primaryGreen),
            suffixIcon: _searchQuery.isNotEmpty
                ? IconButton(
                    icon: const Icon(Icons.close, color: Colors.grey, size: 18),
                    onPressed: () => setState(() => _searchQuery = ''),
                  )
                : null,
            border: InputBorder.none,
            contentPadding: const EdgeInsets.symmetric(vertical: 14),
          ),
        ),
      ),
    );
  }

  Widget _buildSectionTitle(String title) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
      child: Text(
        title.toUpperCase(),
        style: const TextStyle(
          fontSize: 15,
          fontWeight: FontWeight.w800,
          color: primaryDark,
          letterSpacing: 0.5,
        ),
      ),
    );
  }

  Widget _buildBranchChips(List<Map<String, dynamic>> branches) {
    return SizedBox(
      height: 36,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 20),
        children: [
          _buildSquareChip('TẤT CẢ', _selectedBranch.isEmpty, () {
            setState(() {
              _selectedBranch = '';
              _selectedBranchId = null;
            });
          }),
          ...branches.map((branch) {
            final name = branch['TenChiNhanh'] ?? branch['tenChiNhanh'] ?? 'CN';
            int? id;
            try {
              id = int.parse(branch['MaChiNhanh'].toString());
            } catch (_) {}
            final isSelected =
                (_selectedBranchId != null && id == _selectedBranchId) ||
                (_selectedBranchId == null && _selectedBranch == name);
            return Padding(
              padding: const EdgeInsets.only(left: 10),
              child: _buildSquareChip(
                name.toString().toUpperCase(),
                isSelected,
                () {
                  setState(() {
                    if (isSelected) {
                      _selectedBranch = '';
                      _selectedBranchId = null;
                    } else {
                      _selectedBranch = name;
                      _selectedBranchId = id;
                    }
                  });
                },
              ),
            );
          }),
        ],
      ),
    );
  }

  Widget _buildSquareChip(String label, bool isSelected, VoidCallback onTap) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: isSelected ? primaryGreen : Colors.white,
          borderRadius: BorderRadius.circular(4),
          border: Border.all(
            color: isSelected ? primaryGreen : Colors.grey.shade300,
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: isSelected ? Colors.white : primaryDark,
            fontWeight: FontWeight.w700,
            fontSize: 12,
          ),
        ),
      ),
    );
  }

  Widget _buildTourList(BuildContext context, List<Tour> tours) {
    if (tours.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(40),
          child: Column(
            children: [
              Icon(Icons.travel_explore_outlined, size: 50, color: Colors.grey),
              SizedBox(height: 10),
              Text(
                'Không tìm thấy tour phù hợp',
                style: TextStyle(color: Colors.grey),
              ),
            ],
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
      physics: const NeverScrollableScrollPhysics(),
      shrinkWrap: true,
      itemCount: tours.length,
      separatorBuilder: (ctx, index) => const SizedBox(height: 20),
      itemBuilder: (context, index) {
        final tour = tours[index];
        double price = 0;
        try {
          String cleanPrice =
              tour.giaNguoiLon?.toString().replaceAll(RegExp(r'[^0-9]'), '') ??
              '0';
          price = double.parse(cleanPrice);
        } catch (_) {}

        return GestureDetector(
          onTap: () async {
            final shouldRefresh = await Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) =>
                    TourDetailPage(tour: tour, userID: widget.userID),
              ),
            );
            if (shouldRefresh == true) {
              _myBookingKey.currentState?.refreshData();
              setState(() => _selectedIndex = 1);
            }
          },
          child: Container(
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(4),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withOpacity(0.05),
                  blurRadius: 10,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Ảnh & Tag giá
                Stack(
                  children: [
                    ImageHelper.imageFromData(
                      tour.imageData,
                      mime: tour.imageMime,
                      width: double.infinity,
                      height: 180,
                      fit: BoxFit.cover,
                    ),
                    Positioned(
                      bottom: 0,
                      left: 0,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 6,
                        ),
                        color: primaryGreen,
                        child: Text(
                          price > 0 ? currencyFormat.format(price) : 'Liên hệ',
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.bold,
                            fontSize: 15,
                          ),
                        ),
                      ),
                    ),
                    Positioned(
                      top: 10,
                      right: 10,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 8,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: primaryDark.withOpacity(0.9),
                          borderRadius: BorderRadius.circular(2),
                        ),
                        child: Row(
                          children: [
                            const Icon(
                              Icons.location_on,
                              color: primaryGreen,
                              size: 12,
                            ),
                            const SizedBox(width: 4),
                            Text(
                              tour.noiDen ?? 'Vietnam',
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),

                // Nội dung text
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        tour.tieuDe,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w800,
                          color: primaryDark,
                          height: 1.3,
                        ),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: 10),
                      Row(
                        children: [
                          const Icon(
                            Icons.calendar_month_outlined,
                            size: 16,
                            color: primaryGreen,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            '${tour.thoiGian ?? "3"} ngày',
                            style: TextStyle(
                              fontSize: 13,
                              color: Colors.grey.shade600,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildHomeTab(BuildContext context) {
    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildHeader(context),
          const SizedBox(height: 16),
          _buildSearchBar(),
          const SizedBox(height: 24),

          // CHỈ GỌI 1 LẦN FutureBuilder CHO CHIP
          FutureBuilder<List<Map<String, dynamic>>>(
            future: _branchesFuture,
            builder: (context, snapshot) {
              // Nếu đang load hoặc lỗi, vẫn hiện chip "Tất cả" mặc định
              var branches = snapshot.hasData
                  ? snapshot.data!
                  : <Map<String, dynamic>>[];

              if (branches.isNotEmpty) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildSectionTitle('Điểm đến phổ biến'),
                    _buildBranchChips(branches),
                    const SizedBox(height: 24),
                  ],
                );
              }
              return const SizedBox.shrink();
            },
          ),

          _buildSectionTitle('Tour Nổi Bật'),

          FutureBuilder<List<Tour>>(
            future: _toursFuture,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Padding(
                  padding: EdgeInsets.all(32),
                  child: Center(
                    child: CircularProgressIndicator(color: primaryGreen),
                  ),
                );
              }
              if (snapshot.hasError) {
                return Center(
                  child: Text(
                    'Lỗi kết nối',
                    style: const TextStyle(color: Colors.red),
                  ),
                );
              }

              var tours = snapshot.data ?? [];

              // Filter
              if (_selectedBranchId != null || _selectedBranch.isNotEmpty) {
                tours = tours.where((t) {
                  final branchRaw = t.chiNhanh?.toString() ?? '';
                  if (_selectedBranchId != null)
                    return branchRaw == _selectedBranchId.toString();
                  return branchRaw.toLowerCase().contains(
                    _selectedBranch.toLowerCase(),
                  );
                }).toList();
              }

              if (_searchQuery.isNotEmpty) {
                tours = tours
                    .where(
                      (t) =>
                          t.tieuDe.toLowerCase().contains(_searchQuery) ||
                          (t.noiDen ?? '').toLowerCase().contains(_searchQuery),
                    )
                    .toList();
              }

              return _buildTourList(context, tours);
            },
          ),
          const SizedBox(height: 40),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: scaffoldBg,
      body: SafeArea(
        child: IndexedStack(
          index: _selectedIndex,
          children: [
            _buildHomeTab(context),
            MyBookingPage(key: _myBookingKey, userID: widget.userID),
            Container(),
            InvoicesPage(key: _invoicesKey, userID: widget.userID),
            ProfileScreen(
              userID: widget.userID,
              userName: _userFullName.isNotEmpty
                  ? _userFullName
                  : widget.userID,
            ),
          ],
        ),
      ),
      bottomNavigationBar: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.05),
              blurRadius: 10,
              offset: const Offset(0, -5),
            ),
          ],
        ),
        child: BottomNavigationBar(
          currentIndex: _selectedIndex,
          onTap: _onItemTapped,
          backgroundColor: Colors.white,
          selectedItemColor: primaryGreen,
          unselectedItemColor: Colors.grey.shade400,
          type: BottomNavigationBarType.fixed,
          selectedLabelStyle: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 11,
          ),
          unselectedLabelStyle: const TextStyle(fontSize: 11),
          elevation: 0,
          items: const [
            BottomNavigationBarItem(
              icon: Icon(Icons.home_filled),
              label: 'TRANG CHỦ',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.confirmation_number),
              label: 'ĐẶT VÉ',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.qr_code_2, size: 32),
              label: 'QUÉT',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.receipt),
              label: 'HÓA ĐƠN',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.account_circle),
              label: 'CÁ NHÂN',
            ),
          ],
        ),
      ),
    );
  }
}
