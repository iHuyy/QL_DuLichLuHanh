# 🌍 Tour & Travel App (DLLH)

Ứng dụng di động Flutter cho nền tảng đặt tour du lịch - Dù Lịch Du Lịch Hành

## 📋 Mục Lục

- [Tính Năng](#-tính-năng)
- [Chuẩn Bị](#-chuẩn-bị)
- [Chạy Trên Máy Thật](#-chạy-trên-máy-thật)
- [Cấu Trúc Project](#-cấu-trúc-project)
- [Tài Liệu Liên Quan](#-tài-liệu-liên-quan)

---

## ✨ Tính Năng

- ✅ **Đăng nhập/Đăng xuất** - Session management
- ✅ **Quét mã QR** - Đăng nhập bằng QR code
- ✅ **Xem tour du lịch** - Danh sách, tìm kiếm, lọc
- ✅ **Chi tiết tour** - Thông tin chi tiết, hình ảnh, bình luận
- ✅ **Đặt tour** - Booking management
- ✅ **Hóa đơn** - Xem hóa đơn đã đặt
- ✅ **Quản lý hồ sơ** - Cập nhật thông tin cá nhân
- ✅ **Đăng xuất từ xa** - Remote logout

---

## 🚀 Chạy Trên Máy Thật

### **⚡ Quick Start (30 giây)**

```powershell
# 1. Tìm IP máy chủ
ipconfig

# 2. Cập nhật IP trong: lib/config/app_config.dart
static const String serverIp = "192.168.1.14"; # ← Thay IP của bạn

# 3. Chạy app
cd "G:\Study\KLTN\AppQLDVDLLH\app_dllh_may_that"
flutter pub get
flutter run
```

### **📖 Hướng Dẫn Chi Tiết**

Xem: [`SETUP_REAL_DEVICE.md`](./SETUP_REAL_DEVICE.md)

### **✅ Checklist**

Xem: [`CHECKLIST.md`](./CHECKLIST.md)

### **🔧 Sửa Lỗi Network**

Xem: [`NETWORK_TROUBLESHOOTING.md`](./NETWORK_TROUBLESHOOTING.md)

---

## 📁 Cấu Trúc Project

```
lib/
├── main.dart                 # Entry point, AuthWrapper
├── config/
│   └── app_config.dart       # ⚙️ CẬP NHẬT IP TẠI ĐÂY
├── screens/                  # UI Pages
│   ├── login_page.dart
│   ├── register_page.dart
│   ├── home_page.dart
│   ├── tour_detail_page.dart
│   ├── booking_page.dart
│   ├── profile_page.dart
│   └── invoices_page.dart
├── services/                 # Business Logic
│   ├── api_client.dart       # HTTP Client
│   ├── auth_service.dart     # Authentication
│   ├── booking_service.dart  # Booking
│   └── navigation_service.dart # Navigation
├── models/                   # Data Models
│   ├── tour.dart
│   ├── booking_request.dart
│   └── session.dart
└── providers/                # State Management
    ├── auth_provider.dart
    ├── profile_provider.dart
    └── user_data_provider.dart

android/                       # Android config
├── app/src/main/
│   └── AndroidManifest.xml   # ✅ Quyền đã cập nhật
└── app/build.gradle.kts

ios/                          # iOS config
└── Runner.xcworkspace
```

---

## 🔧 Cấu Hình

### **IP Server**
```dart
// File: lib/config/app_config.dart
class AppConfig {
  static const String serverIp = "192.168.1.14"; // ← Thay đổi ở đây
  static const int requestTimeoutSeconds = 12;
}
```

### **Quyền Android**
```xml
<!-- android/app/src/main/AndroidManifest.xml -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.CAMERA" />
```

---

## 📦 Dependencies

- **flutter** - UI framework
- **http** - HTTP client
- **shared_preferences** - Local storage
- **provider** - State management
- **mobile_scanner** - QR code scanner
- **image_picker** - Image selection
- **device_info_plus** - Device information

---

## 🛠️ Công Cụ Yêu Cầu

- **Flutter SDK** ≥ 3.8.1
- **Dart SDK** ≥ 3.8.1
- **Android SDK** (for Android) hoặc **Xcode** (for iOS)
- **VS Code** hoặc **Android Studio**

---

## 📝 Lệnh Hữu Ích

```powershell
# Cài dependencies
flutter pub get

# Phân tích code
flutter analyze

# Chạy app (chọn device)
flutter run

# Build APK (Android)
flutter build apk --release

# Xóa cache
flutter clean

# Hot reload (thay đổi code nhanh)
# Nhấn R trong terminal

# Hot restart (khởi động lại app)
# Nhấn Shift+R trong terminal
```

---

## 🐛 Sửa Lỗi Nhanh

| Vấn Đề | Giải Pháp |
|--------|----------|
| "Unable to connect to server" | Kiểm tra IP trong `AppConfig` |
| "No devices found" | Bật USB Debugging hoặc `adb devices` |
| "Timeout" | Tăng `requestTimeoutSeconds` |
| "Permission denied" | Cấp quyền trong phone Settings |

---

## 📱 Hỗ Trợ Thiết Bị

| Nền Tảng | Phiên Bản | Kết Nối |
|---------|----------|--------|
| Android | 5.0+ | USB hoặc WiFi |
| iOS | 11+ | USB (Mac) |

---

## 📚 Tài Liệu Liên Quan

| File | Mô Tả |
|------|-------|
| `QUICK_START.md` | Quick reference card (1 trang) |
| `SETUP_REAL_DEVICE.md` | Hướng dẫn chi tiết chạy trên device |
| `CHECKLIST.md` | Danh sách kiểm tra từng bước |
| `NETWORK_TROUBLESHOOTING.md` | Sửa lỗi network & diagnosis |
| `CHANGES_SUMMARY.md` | Tóm tắt các thay đổi code |

---

## 🎓 Quy Trình Phát Triển

```
1. Sửa code
   ↓
2. Hot reload (R)
   ↓
3. Test trên device
   ↓
4. Commit changes
   ↓
5. Build release (khi cần)
```

---

## 🌐 Server Requirements

- **Apache/Nginx** chạy trên máy tính
- **PHP** ≥ 7.4
- **MySQL/Oracle** database
- **CORS** hỗ trợ (nếu cần)
- Server address: `http://<IP>/KLTN`

---

## 📞 Cần Giúp?

1. Đọc `QUICK_START.md` (1 trang)
2. Kiểm tra `CHECKLIST.md` (danh sách)
3. Xem `NETWORK_TROUBLESHOOTING.md` (sửa lỗi)
4. Chạy `flutter run -v` (xem logs chi tiết)

---

## 📄 License

Private project for KLTN (Khóa Luận Tốt Nghiệp)

---

**Last Updated:** Nov 15, 2025  
**Status:** Ready for Device Testing  
**Next:** Test on real Android/iOS device

