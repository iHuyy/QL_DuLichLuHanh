import 'dart:convert';

class Tour {
  final String? maTour;
  final String tieuDe;
  final String? imageData; // base64 or URL
  final String? imageMime; // optional mime type for BLOB
  final String? moTa;
  final String? noiKhoiHanh;
  final String? noiDen;
  final String? thanhPho;
  final String? thoiGian;
  final String? giaNguoiLon;
  final String? giaTreEm;
  final String? soLuong;
  final String? soChoConLai; // Số chỗ còn lại
  final String? chiNhanh;

  Tour({
    this.maTour,
    required this.tieuDe,
    this.imageData,
    this.imageMime,
    this.moTa,
    this.noiKhoiHanh,
    this.noiDen,
    this.thanhPho,
    this.thoiGian,
    this.giaNguoiLon,
    this.giaTreEm,
    this.soLuong,
    this.soChoConLai,
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
    String? rawImage;
    for (final key in ['QR', 'QR_CODE', 'HINHANH', 'ANHTOUR', 'DULIEUANH', 'DU_LIEU_ANH']) {
      final v = _get(json, key);
      if (v != null && _toStr(v).trim().isNotEmpty) {
        rawImage = _toStr(v).trim();
        break;
      }
    }

    String? rawMime;
    for (final key in ['LOAIANH', 'LOAI_ANH', 'MIME', 'CONTENT_TYPE']) {
      final v = _get(json, key);
      if (v != null && _toStr(v).trim().isNotEmpty) {
        rawMime = _toStr(v).trim();
        break;
      }
    }

    String? normalizedImage;
    if (rawImage != null && rawImage.isNotEmpty) {
      final s = rawImage;
      if (s.startsWith('data:')) {
        normalizedImage = s;
      } else {
        try {
          final cleaned = s.replaceAll(RegExp(r"\s+"), '');
          base64Decode(cleaned);
          final mime = rawMime ?? 'image/jpeg';
          normalizedImage = 'data:$mime;base64,$cleaned';
        } catch (e) {
          normalizedImage = s;
        }
      }
    }

    return Tour(
      maTour: _toStr(_get(json, 'MATOUR')).isEmpty ? null : _toStr(_get(json, 'MATOUR')),
      tieuDe: _toStr(_get(json, 'TIEUDE')).isEmpty ? 'No title' : _toStr(_get(json, 'TIEUDE')),
      imageData: normalizedImage,
      imageMime: rawMime,
      moTa: _toStr(_get(json, 'MOTA')).isEmpty ? null : _toStr(_get(json, 'MOTA')),
      noiKhoiHanh: _toStr(_get(json, 'NOIKHOIHANH')).isEmpty ? null : _toStr(_get(json, 'NOIKHOIHANH')),
      noiDen: _toStr(_get(json, 'NOIDEN')).isEmpty ? null : _toStr(_get(json, 'NOIDEN')),
      thanhPho: _toStr(_get(json, 'THANHPHO')).isEmpty ? null : _toStr(_get(json, 'THANHPHO')),
      thoiGian: _toStr(_get(json, 'THOIGIAN')).isEmpty ? null : _toStr(_get(json, 'THOIGIAN')),
      giaNguoiLon: _toStr(_get(json, 'GIANGUOILON')).isEmpty ? null : _toStr(_get(json, 'GIANGUOILON')),
      giaTreEm: _toStr(_get(json, 'GIATREEM')).isEmpty ? null : _toStr(_get(json, 'GIATREEM')),
      soLuong: _toStr(_get(json, 'SOLUONG')).isEmpty ? null : _toStr(_get(json, 'SOLUONG')),
      soChoConLai: _toStr(_get(json, 'SOCHOCONLAI')).isEmpty ? null : _toStr(_get(json, 'SOCHOCONLAI')),
      chiNhanh: (() {
        final rawId = _get(json, 'MACHINHANH') ?? _get(json, 'MaChiNhanh') ?? _get(json, 'MACHINHANH');
        if (rawId != null && _toStr(rawId).isNotEmpty) return _toStr(rawId);
        final rawName = _get(json, 'CHINHANH') ?? _get(json, 'ChiNhanh') ?? _get(json, 'TENCHINHANH');
        return _toStr(rawName).isEmpty ? null : _toStr(rawName);
      })(),
    );
  }
}