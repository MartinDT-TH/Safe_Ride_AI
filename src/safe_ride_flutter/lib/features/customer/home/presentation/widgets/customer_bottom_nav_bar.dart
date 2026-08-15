import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import 'package:provider/provider.dart';
import '../../../../shared/chat/presentation/providers/chat_unread_provider.dart';

class CustomerBottomNavBar extends StatelessWidget {
  final int currentIndex;
  final Function(int) onTap;

  CustomerBottomNavBar({
    super.key,
    required this.currentIndex,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final hasUnreadChat = context.watch<ChatUnreadProvider>().totalUnread > 0;
    return SafeArea(
      top: false,
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border(
            top: BorderSide(color: Colors.grey.withOpacity(0.1), width: 1),
          ),
        ),
        child: BottomNavigationBar(
          currentIndex: currentIndex,
          type: BottomNavigationBarType.fixed,
          selectedItemColor: Color(0xFF006B70),
          unselectedItemColor: Colors.grey,
          showUnselectedLabels: true,
          selectedLabelStyle: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 12,
          ),
          unselectedLabelStyle: TextStyle(fontSize: 12),
          onTap: onTap,
          items: [
            _buildNavItem(
              Icons.home_outlined,
              Icons.home_filled,
              context.l10n.home,
              0,
            ),
            _buildNavItem(
              Icons.history,
              Icons.history,
              context.l10n.activity,
              1,
              showBadge: hasUnreadChat,
            ),
            _buildNavItem(
              Icons.person_outline_rounded,
              Icons.person_rounded,
              context.l10n.account,
              2,
            ),
          ],
        ),
      ),
    );
  }

  BottomNavigationBarItem _buildNavItem(
    IconData icon,
    IconData activeIcon,
    String label,
    int index, {
    bool showBadge = false,
  }) {
    bool isSelected = currentIndex == index;
    return BottomNavigationBarItem(
      icon: Container(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
        decoration: BoxDecoration(
          color: isSelected ? Color(0xFF006B70) : Colors.transparent,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Icon(
              isSelected ? activeIcon : icon,
              color: isSelected ? Colors.white : Colors.grey,
            ),
            if (showBadge)
              Positioned(
                top: -4,
                right: -5,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: Color(0xFFE11D48),
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 1),
                  ),
                ),
              ),
          ],
        ),
      ),
      label: label,
    );
  }
}
