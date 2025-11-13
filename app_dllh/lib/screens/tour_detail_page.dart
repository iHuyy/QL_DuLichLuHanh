import 'package:flutter/material.dart';
import 'package:app_dllh/models/tour.dart';
import 'package:app_dllh/screens/booking_page.dart';

const Color primaryBlue = Color(0xFF007AFF);
const Color darkTextColor = Color(0xFF1E1E1E);

class TourDetailPage extends StatelessWidget {
  final Tour tour;
  final String userID;

  const TourDetailPage({Key? key, required this.tour, required this.userID}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(tour.tieuDe, style: const TextStyle(color: darkTextColor)),
        backgroundColor: Colors.white,
        elevation: 1,
        iconTheme: const IconThemeData(color: primaryBlue),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Hình ảnh (placeholder)
            Container(
              height: 250,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(12),
                image: const DecorationImage(
                  image: NetworkImage('https://placehold.co/600x400/007AFF/ffffff?text=Tour+Image'),
                  fit: BoxFit.cover,
                ),
              ),
            ),
            const SizedBox(height: 24),
            Text(
              tour.tieuDe,
              style: const TextStyle(
                fontSize: 26,
                fontWeight: FontWeight.bold,
                color: darkTextColor,
              ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(Icons.location_on, color: primaryBlue, size: 16),
                const SizedBox(width: 4),
                Text(
                  '${tour.noiDen ?? 'N/A'}${tour.thanhPho != null ? ', ${tour.thanhPho}' : ''}',
                  style: const TextStyle(fontSize: 16, color: Colors.black54),
                ),
              ],
            ),
            const SizedBox(height: 16),
            const Divider(),
            const SizedBox(height: 16),
            _buildDetailRow(Icons.description, 'Description', tour.moTa ?? 'No description available.'),
            _buildDetailRow(Icons.place, 'Departure', tour.noiKhoiHanh ?? 'N/A'),
            _buildDetailRow(Icons.calendar_today, 'Date', tour.thoiGian?.toString() ?? 'N/A'),
            _buildDetailRow(Icons.people, 'Adult Price', tour.giaNguoiLon ?? 'N/A'),
            _buildDetailRow(Icons.child_care, 'Child Price', tour.giaTreEm ?? 'N/A'),
            _buildDetailRow(Icons.inventory, 'Available Slots', tour.soLuong ?? 'N/A'),
            _buildDetailRow(Icons.business, 'Branch', tour.chiNhanh ?? 'N/A'),
            const SizedBox(height: 24),
            Center(
              child: ElevatedButton(
                onPressed: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (context) => BookingPage(tour: tour, userID: userID),
                    ),
                  );
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: primaryBlue,
                  padding: const EdgeInsets.symmetric(horizontal: 50, vertical: 15),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                child: const Text(
                  'Book Now',
                  style: TextStyle(fontSize: 18, color: Colors.white),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDetailRow(IconData icon, String title, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: primaryBlue, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: darkTextColor)),
                const SizedBox(height: 4),
                Text(value, style: const TextStyle(fontSize: 16, color: Colors.black87)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
