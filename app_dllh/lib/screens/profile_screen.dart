// import 'package:flutter/material.dart';
// import 'package:fridge_chef_app/screens/add_recipe_screen.dart';
// import 'package:fridge_chef_app/screens/recipe_detail_screen.dart';
// import '/models/recipe_model.dart';
// import '/providers/user_data_provider.dart';
// import 'package:provider/provider.dart';
// import 'package:cached_network_image/cached_network_image.dart';

// class ProfileScreenWrapper extends StatelessWidget {
//   const ProfileScreenWrapper({super.key});

//   @override
//   Widget build(BuildContext context) {
//     return Consumer<UserDataProvider>(
//       builder: (context, userDataProvider, child) {
//         if (userDataProvider.userProfile == null &&
//             !userDataProvider.isLoading) {
//           Future.microtask(() => userDataProvider.loadAllUserData());
//         }
//         return const ProfileScreen();
//       },
//     );
//   }
// }

// class ProfileScreen extends StatelessWidget {
//   const ProfileScreen({super.key});

//   @override
//   Widget build(BuildContext context) {
//     final provider = context.watch<UserDataProvider>();

//     return Scaffold(
//       backgroundColor: Colors.grey[100],
//       floatingActionButton: FloatingActionButton.extended(
//         onPressed: () {
//           Navigator.of(
//             context,
//           ).push(MaterialPageRoute(builder: (_) => const AddRecipeScreen()));
//         },
//         label: const Text('Tạo công thức'),
//         icon: const Icon(Icons.add),
//         backgroundColor: Colors.green,
//         foregroundColor: Colors.white,
//       ),
//       body:
//           provider.isLoading && provider.userProfile == null
//               ? const Center(
//                 child: CircularProgressIndicator(color: Colors.purple),
//               )
//               : RefreshIndicator(
//                 onRefresh:
//                     () => context.read<UserDataProvider>().loadAllUserData(),
//                 child: ListView(
//                   padding: const EdgeInsets.fromLTRB(16, 24, 16, 80),
//                   children: [
//                     _buildUserInfo(context, provider),
//                     const SizedBox(height: 24),
//                     _buildStatsSection(provider),
//                     const SizedBox(height: 24),
//                     _buildSectionCard(
//                       title: 'Công thức của tôi',
//                       icon: Icons.auto_stories,
//                       iconColor: Colors.blue,
//                       child: _buildRecipeHorizontalList(provider.myRecipes),
//                       onTap:
//                           provider.myRecipes.isEmpty
//                               ? null
//                               : () => _showRecipeListDialog(
//                                 context,
//                                 'Công thức của tôi',
//                                 provider.myRecipes,
//                               ),
//                     ),
//                     const SizedBox(height: 16),
//                     _buildSectionCard(
//                       title: 'Món ăn đã xem gần đây',
//                       icon: Icons.history,
//                       iconColor: Colors.orange,
//                       child: _buildRecipeHorizontalList(provider.viewedHistory),
//                       onTap:
//                           provider.viewedHistory.isEmpty
//                               ? null
//                               : () => _showRecipeListDialog(
//                                 context,
//                                 'Món ăn đã xem gần đây',
//                                 provider.viewedHistory,
//                               ),
//                     ),
//                     const SizedBox(height: 16),
//                     _buildSectionCard(
//                       title: 'Món ăn yêu thích',
//                       icon: Icons.favorite,
//                       iconColor: Colors.pink,
//                       child: _buildRecipeHorizontalList(
//                         provider.favoriteRecipes,
//                       ),
//                       onTap:
//                           provider.favoriteRecipes.isEmpty
//                               ? null
//                               : () => _showRecipeListDialog(
//                                 context,
//                                 'Món ăn yêu thích',
//                                 provider.favoriteRecipes,
//                               ),
//                     ),
//                     const SizedBox(height: 16),
//                     _buildSectionCard(
//                       title: 'Lịch sử nấu ăn',
//                       icon: Icons.soup_kitchen_outlined,
//                       iconColor: Colors.green,
//                       child:
//                           provider.cookedHistory.isEmpty
//                               ? _buildEmptyState(
//                                 'Bạn chưa nấu món nào',
//                                 Icons.menu_book,
//                               )
//                               : _buildRecipeHorizontalList(
//                                 provider.cookedHistory,
//                               ),
//                       onTap:
//                           provider.cookedHistory.isEmpty
//                               ? null
//                               : () => _showRecipeListDialog(
//                                 context,
//                                 'Lịch sử nấu ăn',
//                                 provider.cookedHistory,
//                               ),
//                     ),
//                   ],
//                 ),
//               ),
//     );
//   }

//   Widget _buildUserInfo(BuildContext context, UserDataProvider provider) {
//     return Row(
//       children: [
//         CircleAvatar(
//           radius: 40,
//           backgroundColor: Colors.purple.shade100,
//           backgroundImage:
//               (provider.userProfile?.avatarUrl != null &&
//                       provider.userProfile!.avatarUrl!.isNotEmpty)
//                   ? CachedNetworkImageProvider(provider.userProfile!.avatarUrl!)
//                   : null,
//           child:
//               (provider.userProfile?.avatarUrl == null ||
//                       provider.userProfile!.avatarUrl!.isEmpty)
//                   ? Icon(Icons.person, size: 50, color: Colors.purple.shade300)
//                   : null,
//         ),
//         const SizedBox(width: 16),
//         Expanded(
//           child: Column(
//             crossAxisAlignment: CrossAxisAlignment.start,
//             children: [
//               Text(
//                 provider.userProfile?.username ?? 'Người dùng',
//                 style: const TextStyle(
//                   fontSize: 22,
//                   fontWeight: FontWeight.bold,
//                 ),
//               ),
//               const Text(
//                 'Thành viên mới',
//                 style: TextStyle(fontSize: 16, color: Colors.grey),
//               ),
//             ],
//           ),
//         ),
//         IconButton(
//           icon: const Icon(Icons.logout, color: Colors.grey),
//           tooltip: 'Đăng xuất',
//           onPressed: () => _showSignOutDialog(context),
//         ),
//       ],
//     );
//   }

//   void _showSignOutDialog(BuildContext context) {
//     showDialog(
//       context: context,
//       builder: (BuildContext dialogContext) {
//         return AlertDialog(
//           title: const Text('Xác nhận Đăng xuất'),
//           content: const Text('Bạn có chắc chắn muốn đăng xuất?'),
//           actions: <Widget>[
//             TextButton(
//               child: const Text('Hủy'),
//               onPressed: () => Navigator.of(dialogContext).pop(),
//             ),
//             TextButton(
//               child: const Text('Đăng xuất'),
//               onPressed: () {
//                 Navigator.of(dialogContext).pop();
//                 context.read<UserDataProvider>().signOut(context);
//               },
//             ),
//           ],
//         );
//       },
//     );
//   }

//   // *** WIDGET ĐÃ ĐƯỢC CẬP NHẬT ***
//   Widget _buildStatsSection(UserDataProvider provider) {
//     return Card(
//       elevation: 2,
//       shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
//       child: Padding(
//         padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 8),
//         child: Row(
//           mainAxisAlignment: MainAxisAlignment.spaceBetween,
//           children: [
//             _buildStatItem(provider.myRecipes.length.toString(), 'Đã tạo'),
//             _buildStatItem(provider.viewedCount.toString(), 'Đã xem'),
//             _buildStatItem(provider.cookedCount.toString(), 'Đã nấu'),
//             _buildStatItem(provider.favoriteCount.toString(), 'Yêu thích'),
//           ],
//         ),
//       ),
//     );
//   }

//   Widget _buildStatItem(String value, String label) {
//     return Column(
//       children: [
//         Text(
//           value,
//           style: const TextStyle(
//             fontSize: 24,
//             fontWeight: FontWeight.bold,
//             color: Colors.purple,
//           ),
//         ),
//         const SizedBox(height: 4),
//         Text(label, style: const TextStyle(fontSize: 14, color: Colors.grey)),
//       ],
//     );
//   }

//   Widget _buildSectionCard({
//     required String title,
//     required IconData icon,
//     required Color iconColor,
//     required Widget child,
//     VoidCallback? onTap,
//   }) {
//     return Card(
//       elevation: 2,
//       shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
//       color: Colors.white,
//       clipBehavior: Clip.antiAlias,
//       child: InkWell(
//         onTap: onTap,
//         child: Padding(
//           padding: const EdgeInsets.all(16.0),
//           child: Column(
//             crossAxisAlignment: CrossAxisAlignment.start,
//             children: [
//               Row(
//                 children: [
//                   Icon(icon, color: iconColor),
//                   const SizedBox(width: 8),
//                   Text(
//                     title,
//                     style: TextStyle(
//                       fontSize: 18,
//                       fontWeight: FontWeight.bold,
//                       color: iconColor,
//                     ),
//                   ),
//                   const Spacer(),
//                   if (onTap != null)
//                     const Icon(
//                       Icons.arrow_forward_ios,
//                       size: 16,
//                       color: Colors.grey,
//                     ),
//                 ],
//               ),
//               const SizedBox(height: 12),
//               child,
//             ],
//           ),
//         ),
//       ),
//     );
//   }

//   Widget _buildEmptyState(String message, IconData icon) {
//     return SizedBox(
//       height: 120,
//       child: Center(
//         child: Column(
//           mainAxisAlignment: MainAxisAlignment.center,
//           children: [
//             Icon(icon, size: 40, color: Colors.grey.shade400),
//             const SizedBox(height: 8),
//             Text(message, style: const TextStyle(color: Colors.grey)),
//           ],
//         ),
//       ),
//     );
//   }

//   Widget _buildRecipeHorizontalList(List<Recipe> recipes) {
//     if (recipes.isEmpty) {
//       return _buildEmptyState('Chưa có món ăn nào', Icons.no_food);
//     }
//     return SizedBox(
//       height: 150,
//       child: ListView.builder(
//         scrollDirection: Axis.horizontal,
//         itemCount: recipes.length,
//         itemBuilder: (context, index) {
//           final recipe = recipes[index];
//           return GestureDetector(
//             onTap: () {
//               Navigator.of(context).push(
//                 MaterialPageRoute(
//                   builder:
//                       (_) => RecipeDetailScreenWrapper(recipeId: recipe.id),
//                 ),
//               );
//             },
//             child: Container(
//               width: 120,
//               margin: const EdgeInsets.only(right: 12),
//               child: Column(
//                 crossAxisAlignment: CrossAxisAlignment.start,
//                 children: [
//                   Expanded(
//                     child: ClipRRect(
//                       borderRadius: BorderRadius.circular(12),
//                       child: CachedNetworkImage(
//                         imageUrl: recipe.imageUrl ?? '',
//                         fit: BoxFit.cover,
//                         width: double.infinity,
//                         errorWidget:
//                             (c, u, e) => Container(color: Colors.grey[200]),
//                       ),
//                     ),
//                   ),
//                   const SizedBox(height: 8),
//                   Text(
//                     recipe.name,
//                     maxLines: 2,
//                     overflow: TextOverflow.ellipsis,
//                     style: const TextStyle(fontWeight: FontWeight.w600),
//                   ),
//                 ],
//               ),
//             ),
//           );
//         },
//       ),
//     );
//   }

//   void _showRecipeListDialog(
//     BuildContext context,
//     String title,
//     List<Recipe> recipes,
//   ) {
//     showDialog(
//       context: context,
//       builder: (context) {
//         return Dialog(
//           insetPadding: const EdgeInsets.all(16),
//           shape: RoundedRectangleBorder(
//             borderRadius: BorderRadius.circular(24),
//           ),
//           child: Container(
//             constraints: const BoxConstraints(maxHeight: 500),
//             padding: const EdgeInsets.all(16),
//             child: Column(
//               mainAxisSize: MainAxisSize.min,
//               children: [
//                 Text(
//                   title,
//                   style: const TextStyle(
//                     fontSize: 20,
//                     fontWeight: FontWeight.bold,
//                   ),
//                 ),
//                 const SizedBox(height: 16),
//                 Expanded(
//                   child: ListView.builder(
//                     itemCount: recipes.length,
//                     itemBuilder: (context, index) {
//                       final recipe = recipes[index];
//                       return ListTile(
//                         leading: ClipRRect(
//                           borderRadius: BorderRadius.circular(8),
//                           child: CachedNetworkImage(
//                             imageUrl: recipe.imageUrl ?? '',
//                             width: 50,
//                             height: 50,
//                             fit: BoxFit.cover,
//                             errorWidget:
//                                 (c, u, e) => Container(color: Colors.grey[200]),
//                           ),
//                         ),
//                         title: Text(
//                           recipe.name,
//                           maxLines: 2,
//                           overflow: TextOverflow.ellipsis,
//                         ),
//                         onTap: () {
//                           Navigator.of(context).pop();
//                           Navigator.of(context).push(
//                             MaterialPageRoute(
//                               builder:
//                                   (_) => RecipeDetailScreenWrapper(
//                                     recipeId: recipe.id,
//                                   ),
//                             ),
//                           );
//                         },
//                       );
//                     },
//                   ),
//                 ),
//               ],
//             ),
//           ),
//         );
//       },
//     );
//   }
// }
