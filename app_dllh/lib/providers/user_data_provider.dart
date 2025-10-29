// import 'dart:io';
// import 'dart:convert';
// import 'package:flutter/material.dart';
// import 'package:http/http.dart' as http;
// import 'package:fridge_chef_app/main.dart';
// import 'package:fridge_chef_app/models/category_model.dart';
// import 'package:fridge_chef_app/models/ingredient_model.dart';
// import 'package:fridge_chef_app/models/recipe_model.dart';
// import 'package:fridge_chef_app/models/user_profile_model.dart';
// import 'package:fridge_chef_app/screens/login_screen.dart';
// import 'package:image_picker/image_picker.dart';
// import 'package:supabase_flutter/supabase_flutter.dart';

// class UserDataProvider extends ChangeNotifier {
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

//   int _viewedCount = 0;
//   int get viewedCount => _viewedCount;
//   int _cookedCount = 0;
//   int get cookedCount => _cookedCount;
//   int _favoriteCount = 0;
//   int get favoriteCount => _favoriteCount;

//   List<Recipe> _myRecipes = [];
//   List<Recipe> get myRecipes => _myRecipes;
//   List<Recipe> _viewedHistory = [];
//   List<Recipe> get viewedHistory => _viewedHistory;
//   List<Recipe> _favoriteRecipes = [];
//   List<Recipe> get favoriteRecipes => _favoriteRecipes;
//   List<Recipe> _cookedHistory = [];
//   List<Recipe> get cookedHistory => _cookedHistory;
//   Set<int> _favoriteRecipeIds = {};
//   Set<int> get favoriteRecipeIds => _favoriteRecipeIds;
//   List<Category> _categories = [];
//   List<Category> get categories => _categories;

//   List<Ingredient> _dbIngredients = [];
//   List<String> _apiIngredientNames = [];

//   Future<void> loadAllUserData() async {
//     if (!_isLoading) {
//       _isLoading = true;
//       notifyListeners();
//     }
//     try {
//       final userId = supabase.auth.currentUser?.id;
//       if (userId == null) throw Exception("User is not logged in");

//       await Future.wait([
//         _fetchAllDbIngredients(),
//         _fetchAllApiIngredients(),
//         _fetchMainUserData(userId),
//       ]);
//     } catch (e) {
//       print('Error loading all user data: $e');
//       throw e;
//     } finally {
//       _isLoading = false;
//       notifyListeners();
//     }
//   }

//   Future<void> _fetchMainUserData(String userId) async {
//     final responses = await Future.wait([
//       supabase.from('profiles').select().eq('id', userId).single(),
//       supabase
//           .from('view_history')
//           .select('recipes(*)')
//           .eq('user_id', userId)
//           .order('last_viewed_at', ascending: false)
//           .limit(5),
//       supabase
//           .from('user_favorites')
//           .select('recipes(*)')
//           .eq('user_id', userId)
//           .order('created_at', ascending: false)
//           .limit(5),
//       supabase
//           .from('cooking_history')
//           .select('recipes(*)')
//           .eq('user_id', userId)
//           .order('cooked_at', ascending: false)
//           .limit(5),
//       supabase.from('cooking_history').select('id').eq('user_id', userId),
//       supabase.from('user_favorites').select('recipe_id').eq('user_id', userId),
//       supabase.from('view_history').select('recipe_id').eq('user_id', userId),
//       supabase
//           .from('recipes')
//           .select()
//           .eq('user_id', userId)
//           .order('created_at', ascending: false),
//       supabase.from('category').select().order('name', ascending: true),
//     ]);

//     _userProfile = UserProfile.fromJson(responses[0] as Map<String, dynamic>);
//     final viewedData = responses[1] as List;
//     _viewedHistory =
//         viewedData
//             .where((item) => item['recipes'] != null)
//             .map((item) => Recipe.fromJson(item['recipes']))
//             .toList();
//     final favoriteData = responses[2] as List;
//     _favoriteRecipes =
//         favoriteData
//             .where((item) => item['recipes'] != null)
//             .map((item) => Recipe.fromJson(item['recipes']))
//             .toList();
//     final cookedData = responses[3] as List;
//     _cookedHistory =
//         cookedData
//             .where((item) => item['recipes'] != null)
//             .map((item) => Recipe.fromJson(item['recipes']))
//             .toList();
//     _cookedCount = (responses[4] as List).length;
//     final favoriteResponse = responses[5] as List;
//     _favoriteCount = favoriteResponse.length;
//     _favoriteRecipeIds =
//         favoriteResponse.map<int>((item) => item['recipe_id'] as int).toSet();
//     _viewedCount = (responses[6] as List).length;
//     final myRecipesData = responses[7] as List;
//     _myRecipes = myRecipesData.map((item) => Recipe.fromJson(item)).toList();
//     final categoriesData = responses[8] as List;
//     _categories =
//         categoriesData.map((item) => Category.fromSupabaseJson(item)).toList();
//   }

//   Future<void> _fetchAllDbIngredients() async {
//     try {
//       final response = await supabase.from('ingredients').select();
//       _dbIngredients =
//           response.map((item) => Ingredient.fromJson(item)).toList();
//       _dbIngredients.sort(
//         (a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()),
//       );
//     } catch (e) {
//       print('Error fetching ALL DB ingredients: $e');
//     }
//   }

//   Future<void> _fetchAllApiIngredients() async {
//     final uri = Uri.parse(
//       'https://www.themealdb.com/api/json/v1/1/list.php?i=list',
//     );
//     try {
//       final response = await http.get(uri).timeout(const Duration(seconds: 15));
//       if (response.statusCode == 200) {
//         final data = jsonDecode(response.body);
//         final List<dynamic> meals = data['meals'] ?? [];
//         _apiIngredientNames =
//             meals
//                 .map<String>((json) => json['strIngredient'] as String)
//                 .toList();
//         _apiIngredientNames.sort(
//           (a, b) => a.toLowerCase().compareTo(b.toLowerCase()),
//         );
//       }
//     } catch (e) {
//       print('Error fetching all API ingredients list: $e');
//     }
//   }

//   List<Ingredient> getFilteredSuggestions(String query) {
//     if (query.trim().isEmpty) return [];
//     final lowerCaseQuery = query.toLowerCase();
//     final List<Ingredient> suggestions = [];
//     final Set<String> addedNames = {};

//     final dbMatches = _dbIngredients.where(
//       (ing) => ing.name.toLowerCase().contains(lowerCaseQuery),
//     );
//     for (final ing in dbMatches) {
//       if (addedNames.add(ing.name.toLowerCase())) {
//         suggestions.add(ing);
//       }
//     }

//     final apiMatches = _apiIngredientNames.where(
//       (name) => name.toLowerCase().contains(lowerCaseQuery),
//     );
//     for (final name in apiMatches) {
//       if (addedNames.add(name.toLowerCase())) {
//         suggestions.add(Ingredient(id: 0, name: name));
//       }
//     }
//     return suggestions.take(8).toList();
//   }

//   Future<void> createRecipe({
//     required String name,
//     required String instructions,
//     required String youtubeUrl,
//     required List<String> ingredientNames,
//     required XFile imageFile,
//     required String categoryId,
//   }) async {
//     final userId = supabase.auth.currentUser?.id;
//     if (userId == null) throw Exception('User not logged in');

//     try {
//       final imageExtension = imageFile.path.split('.').last.toLowerCase();
//       final imagePath =
//           'public/$userId/${DateTime.now().millisecondsSinceEpoch}.$imageExtension';
//       await supabase.storage
//           .from('recipe-images')
//           .upload(
//             imagePath,
//             File(imageFile.path),
//             fileOptions: const FileOptions(cacheControl: '3600', upsert: false),
//           );
//       final imageUrl = supabase.storage
//           .from('recipe-images')
//           .getPublicUrl(imagePath);

//       final newRecipeData =
//           await supabase
//               .from('recipes')
//               .insert({
//                 'name': name,
//                 'instructions': instructions,
//                 'image_url': imageUrl,
//                 'user_id': userId,
//                 'youtube_url': youtubeUrl.isNotEmpty ? youtubeUrl : null,
//               })
//               .select('id')
//               .single();

//       final newRecipeId = newRecipeData['id'] as int;

//       await supabase.from('recipe_category').insert({
//         'recipe_id': newRecipeId,
//         'category_id': int.parse(categoryId),
//       });

//       if (ingredientNames.isNotEmpty) {
//         final List<Map<String, dynamic>> recipeIngredientsToInsert = [];
//         for (final ingredientName in ingredientNames) {
//           if (ingredientName.trim().isNotEmpty) {
//             final ingredientData =
//                 await supabase
//                     .from('ingredients')
//                     .upsert({'name': ingredientName.trim()}, onConflict: 'name')
//                     .select('id')
//                     .single();
//             final ingredientId = ingredientData['id'];
//             recipeIngredientsToInsert.add({
//               'recipe_id': newRecipeId,
//               'ingredient_id': ingredientId,
//             });
//           }
//         }
//         if (recipeIngredientsToInsert.isNotEmpty) {
//           await supabase
//               .from('recipe_ingredients')
//               .insert(recipeIngredientsToInsert);
//         }
//       }

//       await loadAllUserData();
//     } catch (e) {
//       print("Error in createRecipe (Dart version): $e");
//       rethrow;
//     }
//   }

//   Future<int> _ensureRecipeExistsInDb(Recipe recipe) async {
//     try {
//       final existing =
//           await supabase
//               .from('recipes')
//               .select('id')
//               .eq('external_id', recipe.id.toString())
//               .maybeSingle();
//       if (existing != null) {
//         return existing['id'];
//       } else {
//         final newRecipeData =
//             await supabase
//                 .from('recipes')
//                 .insert({
//                   'name': recipe.name,
//                   'image_url': recipe.imageUrl,
//                   'instructions': recipe.instructions,
//                   'youtube_url': recipe.youtubeUrl,
//                   'external_id': recipe.id.toString(),
//                   'user_id': null,
//                 })
//                 .select('id')
//                 .single();
//         return newRecipeData['id'];
//       }
//     } catch (e) {
//       print("Error in _ensureRecipeExistsInDb: $e");
//       throw Exception("Could not ensure recipe exists in DB");
//     }
//   }

//   Future<void> toggleFavorite(Recipe recipe) async {
//     final userId = supabase.auth.currentUser?.id;
//     if (userId == null) return;
//     try {
//       final internalRecipeId = await _ensureRecipeExistsInDb(recipe);
//       final currentlyFavorite = isFavorite(internalRecipeId);

//       if (currentlyFavorite) {
//         _favoriteRecipeIds.remove(internalRecipeId);
//         _favoriteRecipes.removeWhere((r) => r.id == internalRecipeId);
//         _favoriteCount--;
//       } else {
//         _favoriteRecipeIds.add(internalRecipeId);
//         _favoriteRecipes.insert(0, recipe);
//         _favoriteCount++;
//       }
//       notifyListeners();

//       if (currentlyFavorite) {
//         await supabase.from('user_favorites').delete().match({
//           'user_id': userId,
//           'recipe_id': internalRecipeId,
//         });
//       } else {
//         await supabase.from('user_favorites').insert({
//           'user_id': userId,
//           'recipe_id': internalRecipeId,
//         });
//       }
//     } catch (e) {
//       print('Error toggling favorite on server: $e');
//       await loadAllUserData();
//     }
//   }

//   Future<void> addViewToHistory(Recipe recipe) async {
//     final userId = supabase.auth.currentUser?.id;
//     if (userId == null) return;
//     try {
//       final internalRecipeId = await _ensureRecipeExistsInDb(recipe);
//       final now = DateTime.now().toIso8601String();
//       await supabase.from('view_history').upsert({
//         'user_id': userId,
//         'recipe_id': internalRecipeId,
//         'last_viewed_at': now,
//       }, onConflict: 'user_id, recipe_id');
//       await loadAllUserData();
//     } catch (e) {
//       print('Error adding view to history: $e');
//     }
//   }

//   Future<void> addCookingToHistory(Recipe recipe) async {
//     final userId = supabase.auth.currentUser?.id;
//     if (userId == null) return;
//     try {
//       final internalRecipeId = await _ensureRecipeExistsInDb(recipe);
//       await supabase.from('cooking_history').insert({
//         'user_id': userId,
//         'recipe_id': internalRecipeId,
//         'cooked_at': DateTime.now().toIso8601String(),
//       });
//       await loadAllUserData();
//     } catch (e) {
//       print('Error adding cooking to history: $e');
//     }
//   }

//   bool isFavorite(int recipeId) {
//     return _favoriteRecipeIds.contains(recipeId);
//   }

//   Future<void> signOut(BuildContext context) async {
//     await supabase.auth.signOut();
//     _userProfile = null;
//     _favoriteRecipeIds.clear();
//     _favoriteRecipes.clear();
//     _viewedHistory.clear();
//     _cookedHistory.clear();
//     _myRecipes.clear();
//     _viewedCount = 0;
//     _cookedCount = 0;
//     _favoriteCount = 0;
//     _categories.clear();
//     _dbIngredients.clear();
//     _apiIngredientNames.clear();
//     Navigator.of(context, rootNavigator: true).pushAndRemoveUntil(
//       MaterialPageRoute(builder: (_) => const LoginScreen()),
//       (route) => false,
//     );
//   }
// }
