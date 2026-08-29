import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class WelcomePage extends StatefulWidget {
  const WelcomePage({super.key});
  @override
  State<WelcomePage> createState() => _WelcomePage();
}

class _WelcomePage extends State<WelcomePage> {
  @override
  void initState() {
    super.initState();
    checkData();
  }

  Future<void> checkData() async {
    await Future.delayed(const Duration(seconds: 2));
    if (!mounted) return;
    SharedPreferences prefs = await SharedPreferences.getInstance();
    String delegateIdString = prefs.getString('DelegateID') ?? '0';
    try {
      double delegateId = double.parse(delegateIdString);
      if (!mounted) return;
      if (delegateId > 0) {
        Navigator.pushReplacementNamed(context, '/HomePage');
      } else {
        Navigator.pushReplacementNamed(context, '/Login');
      }
    } catch (e) {
      if (!mounted) return;
      Navigator.pushReplacementNamed(context, '/Login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Theme.of(context).scaffoldBackgroundColor,
      body: Stack(
        children: [
          Positioned(
              top: -60,
              right: -60,
              child: Container(
                width: 150,
                height: 150,
                decoration: BoxDecoration(
                    color: AppTheme.primaryColor.withValues(alpha: 0.1),
                    shape: BoxShape.circle),
              )),
          Positioned(
              bottom: -80,
              left: -80,
              child: Container(
                width: 200,
                height: 200,
                decoration: BoxDecoration(
                    color: AppTheme.secondaryColor.withValues(alpha: 0.1),
                    shape: BoxShape.circle),
              )),
          Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Hero(
                  tag: 'logo',
                  child: Image.asset('assets/icons/logoMain.png', width: 180),
                ),
                const SizedBox(height: 16),
                const Text(
                  'تطبيق المتابع',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    color: AppTheme.primaryColor,
                  ),
                ),
                const SizedBox(height: 30),
                const CircularProgressIndicator(color: AppTheme.primaryColor),
                const SizedBox(height: 20),
                const Text("جاري التحميل...",
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
              ],
            ),
          ),
        ],
      ),
    );
  }
}
