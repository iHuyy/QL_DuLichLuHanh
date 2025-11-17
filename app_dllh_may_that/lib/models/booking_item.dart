class BookingItem {
  final int maDatTour;
  final int maTour;
  final String ngayDat;
  final int soNguoiLon;
  final int soTreEm;
  final double tongTien;
  final String trangThaiDat;
  final String trangThaiThanhToan;
  final String yeuCauDacBiet;
  final String tieuDe;
  final String moTa;
  final String noiKhoiHanh;
  final String noiDen;
  final String thanhPho;
  final String thoiGian;
  final double giaNguoiLon;
  final double giaTreEm;
  final String hinhAnh;
  final int? maHoaDon;
  final double hoaDonSoTien;
  final String hoaDonTrangThai;

  BookingItem({
    required this.maDatTour,
    required this.maTour,
    required this.ngayDat,
    required this.soNguoiLon,
    required this.soTreEm,
    required this.tongTien,
    required this.trangThaiDat,
    required this.trangThaiThanhToan,
    required this.yeuCauDacBiet,
    required this.tieuDe,
    required this.moTa,
    required this.noiKhoiHanh,
    required this.noiDen,
    required this.thanhPho,
    required this.thoiGian,
    required this.giaNguoiLon,
    required this.giaTreEm,
    required this.hinhAnh,
    this.maHoaDon,
    required this.hoaDonSoTien,
    required this.hoaDonTrangThai,
  });

  factory BookingItem.fromJson(Map<String, dynamic> json) {
    // Determine image value: support various server fields and BLOB/base64
    String imageValue = '';
    // Helper to check keys case-insensitively
    String? _get(Map<String, dynamic> m, List<String> variants) {
      for (var k in variants) {
        if (m.containsKey(k) &&
            m[k] != null &&
            m[k].toString().trim().isNotEmpty)
          return m[k].toString();
        final up = k.toUpperCase();
        if (m.containsKey(up) &&
            m[up] != null &&
            m[up].toString().trim().isNotEmpty)
          return m[up].toString();
      }
      return null;
    }

    // Common fields that might contain base64 data or a URL/path
    final rawBase64 = _get(json, [
      'DULIEUANH',
      'dulieuAnh',
      'DL_ANH',
      'ANH_DATA',
    ]);
    final rawPath = _get(json, [
      'hinhAnh',
      'HinhAnh',
      'DuongDanAnh',
      'DUONGDANANH',
      'ANHTOUR',
    ]);
    final rawMime = _get(json, ['LOAIANH', 'loaiAnh', 'MIME']);

    if (rawBase64 != null) {
      // clean base64 (remove whitespace/newlines)
      final cleaned = rawBase64.replaceAll(RegExp(r'\s+'), '');
      final mime = rawMime ?? 'image/jpeg';
      if (cleaned.startsWith('data:')) {
        imageValue = cleaned;
      } else {
        imageValue = 'data:$mime;base64,$cleaned';
      }
    } else if (rawPath != null) {
      imageValue = rawPath;
    } else {
      imageValue = json['hinhAnh'] ?? json['HinhAnh'] ?? '';
    }

    return BookingItem(
      maDatTour: json['maDatTour'] ?? 0,
      maTour: json['maTour'] ?? 0,
      ngayDat: json['ngayDat'] ?? '',
      soNguoiLon: json['soNguoiLon'] ?? 0,
      soTreEm: json['soTreEm'] ?? 0,
      tongTien: (json['tongTien'] ?? 0).toDouble(),
      trangThaiDat: json['trangThaiDat'] ?? '',
      trangThaiThanhToan: json['trangThaiThanhToan'] ?? '',
      yeuCauDacBiet: json['yeuCauDacBiet'] ?? '',
      tieuDe: json['tieuDe'] ?? '',
      moTa: json['moTa'] ?? '',
      noiKhoiHanh: json['noiKhoiHanh'] ?? '',
      noiDen: json['noiDen'] ?? '',
      thanhPho: json['thanhPho'] ?? '',
      thoiGian: json['thoiGian'] ?? '',
      giaNguoiLon: (json['giaNguoiLon'] ?? 0).toDouble(),
      giaTreEm: (json['giaTreEm'] ?? 0).toDouble(),
      hinhAnh: imageValue,
      maHoaDon: json['maHoaDon'],
      hoaDonSoTien: (json['hoaDonSoTien'] ?? 0).toDouble(),
      hoaDonTrangThai: json['hoaDonTrangThai'] ?? '',
    );
  }
}
