class BookingRequest {
  final String? maTour;
  final String? maKhachHang;
  final int soNguoiLon;
  final int soTreEm;
  final String hoTen;
  final String soDienThoai;
  final String email;
  final String? ghiChu;

  BookingRequest({
    this.maTour,
    this.maKhachHang,
    required this.soNguoiLon,
    required this.soTreEm,
    required this.hoTen,
    required this.soDienThoai,
    required this.email,
    this.ghiChu,
  });

  Map<String, dynamic> toJson() => {
    'maTour': maTour,
    'maKhachHang': maKhachHang,
    'soNguoiLon': soNguoiLon,
    'soTreEm': soTreEm,
    'hoTen': hoTen,
    'soDienThoai': soDienThoai,
    'email': email,
    'ghiChu': ghiChu,
  };
}