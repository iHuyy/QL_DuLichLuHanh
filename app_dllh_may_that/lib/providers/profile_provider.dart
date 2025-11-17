// import 'package:flutter/material.dart';
// import '/models/recipe_model.dart';
// import '/models/user_profile_model.dart';
// import 'package:fridge_chef_app/main.dart';
// import 'package:supabase_flutter/supabase_flutter.dart';
// import '/screens/login_screen.dart';

// class ProfileProvider extends ChangeNotifier {
//   bool _disposed = false;
//   @override
//   void dispose() {
//     _disposed = true;
//     super.dispose();
//   }

//   @override
//   void notifyListeners() {
//     if (!_disposed) super.notifyListeners();
//   }

//   bool _isLoading = true;
//   bool get isLoading => _isLoading;

//   UserProfile? _userProfile;
//   UserProfile? get userProfile => _userProfile;

//   int _cookedCount = 0;
//   int get cookedCount => _cookedCount;

//   int _collectionCount = 0;
//   int get collectionCount => _collectionCount;

//   int _favoriteCount = 0;
//   int get favoriteCount => _favoriteCount;

//   List<Recipe> _viewedHistory = [];
//   List<Recipe> get viewedHistory => _viewedHistory;

//   List<Recipe> _favoriteRecipes = [];
//   List<Recipe> get favoriteRecipes => _favoriteRecipes;

//   List<Recipe> _cookedHistory = [];
//   List<Recipe> get cookedHistory => _cookedHistory;

//   Future<void> fetchProfileData() async {
//     if (!_isLoading) {
//       _isLoading = true;
//       notifyListeners();
//     }

//     try {
//       final userId = supabase.auth.currentUser?.id;
//       if (userId == null) throw Exception("User is not logged in");

//       final futures = await Future.wait([
//         supabase.from('profiles').select().eq('id', userId).single(),
//         supabase
//             .from('view_history')
//             .select('recipes(*)')
//             .eq('user_id', userId)
//             .order('last_viewed_at', ascending: false)
//             .limit(5),
//         supabase
//             .from('user_favorites')
//             .select('recipes(*)')
//             .eq('user_id', userId)
//             .order('created_at', ascending: false)
//             .limit(5),
//         supabase
//             .from('cooking_history')
//             .select('recipes(*)')
//             .eq('user_id', userId)
//             .order('cooked_at', ascending: false)
//             .limit(5),
//       ]);

//       _userProfile = UserProfile.fromJson(futures[0] as Map<String, dynamic>);

//       final viewedHistoryData = futures[1] as List;
//       _viewedHistory =
//           viewedHistoryData
//               .map((item) => Recipe.fromJson(item['recipes']))
//               .whereType<Recipe>()
//               .toList();

//       final favoriteData = futures[2] as List;
//       _favoriteRecipes =
//           favoriteData
//               .map((item) => Recipe.fromJson(item['recipes']))
//               .whereType<Recipe>()
//               .toList();
//       _favoriteCount = _favoriteRecipes.length;

//       final cookedHistoryData = futures[3] as List;
//       _cookedHistory =
//           cookedHistoryData
//               .map((item) => Recipe.fromJson(item['recipes']))
//               .whereType<Recipe>()
//               .toList();

//       final cookedResponse = await supabase
//           .from('cooking_history')
//           .select('id')
//           .eq('user_id', userId);
//       _cookedCount = cookedResponse.length;

//       final collectionResponse = await supabase
//           .from('collections')
//           .select('id')
//           .eq('user_id', userId);
//       _collectionCount = collectionResponse.length;

//       final favoriteCountResponse = await supabase
//           .from('user_favorites')
//           .select('recipe_id')
//           .eq('user_id', userId);
//       _favoriteCount = favoriteCountResponse.length;
//     } catch (e) {
//       print('Error fetching profile data: $e');
//     } finally {
//       _isLoading = false;
//       notifyListeners();
//     }
//   }

//   Future<void> signOut(BuildContext context) async {
//     await supabase.auth.signOut();
//     Navigator.of(context).pushAndRemoveUntil(
//       MaterialPageRoute(builder: (_) => const LoginScreen()),
//       (route) => false,
//     );
//   }
// }
