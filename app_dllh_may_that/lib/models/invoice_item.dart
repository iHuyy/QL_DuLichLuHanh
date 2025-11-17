class InvoiceItem {
  final int id;
  final double amount;
  final String status;
  final String date;

  InvoiceItem({required this.id, required this.amount, required this.status, required this.date});

  factory InvoiceItem.fromJson(Map<String, dynamic> m) {
    return InvoiceItem(
      id: (m['MAHOADON'] ?? m['MAHOADON'] ?? 0) is int ? (m['MAHOADON'] ?? 0) : int.tryParse('${m['MAHOADON'] ?? 0}') ?? 0,
      amount: double.tryParse('${m['SOTIEN'] ?? m['SOTIEN'] ?? 0}') ?? 0.0,
      status: '${m['TRANGTHAI'] ?? m['TRANGTHAI'] ?? 'Pending'}',
      date: '${m['NGAYLAP'] ?? m['NGAYLAP'] ?? ''}',
    );
  }
}