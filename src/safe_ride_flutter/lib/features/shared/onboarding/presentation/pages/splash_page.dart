import 'package:flutter/material.dart';
import 'dart:async';
import 'dart:math' as math;
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../auth/presentation/pages/login_page.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/profile/presentation/pages/edit_profile_page.dart';
import '../../presentation/pages/role_selection_page.dart';

class SplashPage extends StatefulWidget {
  const SplashPage({super.key});

  @override
  State<SplashPage> createState() => _SplashPageState();
}

class _SplashPageState extends State<SplashPage> with TickerProviderStateMixin {
  late AnimationController _moveController;
  late AnimationController _wheelController;
  late AnimationController _vibrateController;
  
  late Animation<double> _moveAnimation;
  late Animation<double> _opacityAnimation;
  late Animation<double> _textOpacityAnimation;

  @override
  void initState() {
    super.initState();
    
    _moveController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 3500),
    );

    _wheelController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 400),
    )..repeat();

    _vibrateController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 100),
    )..repeat(reverse: true);

    _moveAnimation = Tween<double>(begin: -400.0, end: 0.0).animate(
      CurvedAnimation(
        parent: _moveController,
        curve: const Interval(0.0, 0.8, curve: Curves.easeOutQuart),
      ),
    );

    _opacityAnimation = Tween<double>(begin: 0.0, end: 1.0).animate(
      CurvedAnimation(
        parent: _moveController,
        curve: const Interval(0.0, 0.2, curve: Curves.easeIn),
      ),
    );

    _textOpacityAnimation = Tween<double>(begin: 0.0, end: 1.0).animate(
      CurvedAnimation(
        parent: _moveController,
        curve: const Interval(0.7, 1.0, curve: Curves.easeIn),
      ),
    );

    _moveController.forward();

    Timer(const Duration(seconds: 4), () {
      if (mounted) {
        _navigateToNext(context);
      }
    });
  }

  void _navigateToNext(BuildContext context) {
    final auth = context.read<AuthProvider>();

    Widget destination;
    if (auth.token != null && auth.token!.isNotEmpty) {
      if (auth.isProfileComplete) {
        destination = const CustomerHomePage();
      } else if (auth.nextStep == AuthNextStep.completeProfile) {
        destination = EditProfilePage(
          requiredCompletion: true,
          phoneNumber: auth.phoneNumber,
        );
      } else if (auth.nextStep == AuthNextStep.selectRole) {
        destination = const RoleSelectionPage();
      } else {
        destination = const CustomerHomePage();
      }
    } else {
      destination = const LoginPage();
    }

    Navigator.of(context).pushReplacement(
      PageRouteBuilder(
        pageBuilder: (context, animation, secondaryAnimation) => destination,
        transitionsBuilder: (context, animation, secondaryAnimation, child) {
          return FadeTransition(opacity: animation, child: child);
        },
        transitionDuration: const Duration(milliseconds: 800),
      ),
    );
  }

  @override
  void dispose() {
    _moveController.dispose();
    _wheelController.dispose();
    _vibrateController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: Container(
        width: double.infinity,
        height: double.infinity,
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              AppColors.white,
              AppColors.primary.withOpacity(0.08),
            ],
          ),
        ),
        child: Stack(
          alignment: Alignment.center,
          children: [
            Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                // Car Container
                AnimatedBuilder(
                  animation: Listenable.merge([_moveController, _vibrateController]),
                  builder: (context, child) {
                    return Transform.translate(
                      offset: Offset(
                        _moveAnimation.value, 
                        _vibrateController.value * 2.0
                      ),
                      child: Opacity(
                        opacity: _opacityAnimation.value,
                        child: child,
                      ),
                    );
                  },
                  child: SizedBox(
                    width: 250,
                    height: 120,
                    child: Stack(
                      alignment: Alignment.bottomCenter,
                      children: [
                        // Ground Shadow
                        Positioned(
                          bottom: 5,
                          child: Container(
                            width: 180,
                            height: 10,
                            decoration: BoxDecoration(
                              boxShadow: [
                                BoxShadow(
                                  color: Colors.black.withOpacity(0.15),
                                  blurRadius: 15,
                                  spreadRadius: 2,
                                ),
                              ],
                            ),
                          ),
                        ),
                        // Car Body
                        CustomPaint(
                          size: const Size(250, 120),
                          painter: SportCarPainter(primaryColor: AppColors.primary),
                        ),
                        // Rear Wheel
                        Positioned(
                          bottom: 5,
                          left: 45,
                          child: _SportWheel(controller: _wheelController),
                        ),
                        // Front Wheel
                        Positioned(
                          bottom: 5,
                          right: 48,
                          child: _SportWheel(controller: _wheelController),
                        ),
                      ],
                    ),
                  ),
                ),
                
                const SizedBox(height: 50),
                
                FadeTransition(
                  opacity: _textOpacityAnimation,
                  child: Column(
                    children: [
                      Text(
                        AppStrings.appName.toUpperCase(),
                        style: const TextStyle(
                          fontSize: 42,
                          fontWeight: FontWeight.w900,
                          color: AppColors.primary,
                          letterSpacing: 14,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        AuthStrings.slogan,
                        style: TextStyle(
                          fontSize: 16,
                          color: AppColors.textSecondary.withOpacity(0.6),
                          letterSpacing: 2,
                          fontWeight: FontWeight.w300,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class SportCarPainter extends CustomPainter {
  final Color primaryColor;

  SportCarPainter({required this.primaryColor});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = primaryColor
      ..style = PaintingStyle.fill;

    // 1. Car Body - Lowered Profile like the GIF
    final bodyPath = Path();
    bodyPath.moveTo(20, 95); // Rear bottom
    bodyPath.lineTo(210, 95); // Front bottom
    bodyPath.quadraticBezierTo(235, 95, 235, 75); // Nose curve
    bodyPath.lineTo(235, 65);
    bodyPath.lineTo(170, 55); // Hood
    bodyPath.lineTo(130, 25); // Windshield
    bodyPath.quadraticBezierTo(100, 18, 60, 25); // Roof
    bodyPath.lineTo(35, 50); // Rear window
    bodyPath.lineTo(10, 55); // Trunk
    bodyPath.lineTo(10, 80); // Rear bumper
    bodyPath.quadraticBezierTo(10, 95, 20, 95);
    canvas.drawPath(bodyPath, paint);

    // 2. Windows - Dark Tinted
    final windowPaint = Paint()..color = const Color(0xFF1A1A1A).withOpacity(0.85);
    final windowPath = Path();
    windowPath.moveTo(65, 30);
    windowPath.lineTo(125, 30);
    windowPath.lineTo(160, 55);
    windowPath.lineTo(65, 55);
    windowPath.close();
    canvas.drawPath(windowPath, windowPaint);
    
    // Window reflection/shine
    final shinePaint = Paint()..color = Colors.white.withOpacity(0.1);
    canvas.drawRect(Rect.fromLTRB(80, 35, 90, 50), shinePaint);

    // 3. Details
    // Side line/Accent
    final accentPaint = Paint()
      ..color = Colors.black.withOpacity(0.2)
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;
    canvas.drawLine(const Offset(40, 75), const Offset(180, 75), accentPaint);

    // Headlight (Yellow-White)
    final lightPaint = Paint()..color = const Color(0xFFFFFFD0);
    canvas.drawRRect(
      RRect.fromLTRBR(220, 68, 232, 75, const Radius.circular(2)),
      lightPaint,
    );

    // Taillight (Red)
    canvas.drawRRect(
      RRect.fromLTRBR(10, 68, 18, 75, const Radius.circular(2)),
      Paint()..color = Colors.redAccent,
    );
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _SportWheel extends StatelessWidget {
  final AnimationController controller;

  const _SportWheel({required this.controller});

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (context, child) {
        return Transform.rotate(
          angle: controller.value * 2 * math.pi,
          child: child,
        );
      },
      child: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: const Color(0xFF111111),
          shape: BoxShape.circle,
          border: Border.all(color: const Color(0xFF222222), width: 4),
        ),
        child: Stack(
          alignment: Alignment.center,
          children: [
            // Hub/Rim
            Container(
              width: 26,
              height: 26,
              decoration: BoxDecoration(
                color: Colors.grey[800],
                shape: BoxShape.circle,
                gradient: LinearGradient(
                  colors: [Colors.grey[700]!, Colors.grey[900]!],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
              ),
            ),
            // Multi-spoke design from GIF
            for (int i = 0; i < 6; i++)
              Transform.rotate(
                angle: (i * 2 * math.pi) / 6,
                child: Container(
                  width: 3,
                  height: 22,
                  color: Colors.white10,
                ),
              ),
            // Center nut
            Container(
              width: 6,
              height: 6,
              decoration: const BoxDecoration(
                color: Colors.black,
                shape: BoxShape.circle,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
