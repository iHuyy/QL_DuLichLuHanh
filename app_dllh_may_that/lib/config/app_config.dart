/// Cấu hình ứng dụng
/// Thay đổi serverIp để kết nối tới server khác
class AppConfig {
  // Thay đổi địa chỉ IP này theo máy chủ của bạn
  // Ví dụ: "192.168.1.10" hoặc "192.168.100.5"
  
  static const String serverPort = "80";
  
  static const String baseUrl = "http://192.168.1.14/KLTN";

  static const String dotnetBaseUrl = "http://192.168.1.14:5127";
  
  // Thời gian timeout cho request (giây)
  static const int requestTimeoutSeconds = 12;
  
  // Thời gian check session (giây)
  static const int sessionCheckIntervalSeconds = 15;
  
  // Thời gian throttle cho interactive checks (giây)
  static const int interactiveCheckThrottleSeconds = 5;
}
