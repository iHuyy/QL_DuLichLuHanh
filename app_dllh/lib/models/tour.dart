class Tour {
  final String? maTour;
  final String tieuDe;
  final String? moTa;
  final String? noiKhoiHanh;
  final String? noiDen;
  final String? thanhPho;
  final String? thoiGian;
  final String? giaNguoiLon;
  final String? giaTreEm;
  final String? soLuong;
  final String? chiNhanh;

  Tour({
    this.maTour,
    required this.tieuDe,
    this.moTa,
    this.noiKhoiHanh,
    this.noiDen,
    this.thanhPho,
    this.thoiGian,
    this.giaNguoiLon,
    this.giaTreEm,
    this.soLuong,
    this.chiNhanh,
  });

  // Helper: tìm giá trị bất kể key viết hoa/thuong
  static dynamic _get(Map<String, dynamic> json, String key) {
    if (json.containsKey(key)) return json[key];
    final upper = key.toUpperCase();
    if (json.containsKey(upper)) return json[upper];
    final lower = key.toLowerCase();
    if (json.containsKey(lower)) return json[lower];
    // tìm key bất kể hoa/thường
    for (final k in json.keys) {
      if (k.toString().toLowerCase() == key.toLowerCase()) return json[k];
    }
    return null;
  }

  static String _toStr(dynamic v) => v == null ? '' : v.toString();

  factory Tour.fromJson(Map<String, dynamic> json) {
    return Tour(
      maTour: _toStr(_get(json, 'MATOUR')).isEmpty ? null : _toStr(_get(json, 'MATOUR')),
      tieuDe: _toStr(_get(json, 'TIEUDE')).isEmpty ? 'No title' : _toStr(_get(json, 'TIEUDE')),
      moTa: _toStr(_get(json, 'MOTA')).isEmpty ? null : _toStr(_get(json, 'MOTA')),
      noiKhoiHanh: _toStr(_get(json, 'NOIKHOIHANH')).isEmpty ? null : _toStr(_get(json, 'NOIKHOIHANH')),
      noiDen: _toStr(_get(json, 'NOIDEN')).isEmpty ? null : _toStr(_get(json, 'NOIDEN')),
      thanhPho: _toStr(_get(json, 'THANHPHO')).isEmpty ? null : _toStr(_get(json, 'THANHPHO')),
      thoiGian: _toStr(_get(json, 'THOIGIAN')).isEmpty ? null : _toStr(_get(json, 'THOIGIAN')),
      giaNguoiLon: _toStr(_get(json, 'GIANGUOILON')).isEmpty ? null : _toStr(_get(json, 'GIANGUOILON')),
      giaTreEm: _toStr(_get(json, 'GIATREEM')).isEmpty ? null : _toStr(_get(json, 'GIATREEM')),
      soLuong: _toStr(_get(json, 'SOLUONG')).isEmpty ? null : _toStr(_get(json, 'SOLUONG')),
      chiNhanh: _toStr(_get(json, 'CHINHANH')).isEmpty ? null : _toStr(_get(json, 'CHINHANH')),
    );
  }
}