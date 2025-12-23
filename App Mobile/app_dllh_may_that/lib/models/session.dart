class Session {
  final String sessionId;
  final String deviceType;
  final String deviceInfo;
  final String loginTime;

  Session({
    required this.sessionId,
    required this.deviceType,
    required this.deviceInfo,
    required this.loginTime,
  });

  factory Session.fromJson(Map<String, dynamic> json) {
    return Session(
      sessionId: json['SESSION_ID'],
      deviceType: json['DEVICE_TYPE'],
      deviceInfo: json['DEVICE_INFO'],
      loginTime: json['LOGIN_TIME'],
    );
  }
}
