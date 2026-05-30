import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/home_provider.dart';

import '../widgets/quick_action_item.dart';
import '../widgets/recent_trip_card.dart';
import '../widgets/promo_banner.dart';

class CustomerHomePage extends StatefulWidget {
  const CustomerHomePage({super.key});

  @override
  State<CustomerHomePage> createState() =>
      _CustomerHomePageState();
}

class _CustomerHomePageState
    extends State<CustomerHomePage> {

  @override
  void initState() {
    super.initState();

    Future.microtask(() {
      context
          .read<HomeProvider>()
          .loadHomeData();
    });
  }

  @override
  Widget build(BuildContext context) {

    return Scaffold(

      appBar: AppBar(
        title: const Text('SafeRide'),
      ),

      body: Consumer<HomeProvider>(

        builder: (_, provider, __) {

          if (provider.isLoading) {

            return const Center(
              child:
              CircularProgressIndicator(),
            );
          }

          return SingleChildScrollView(

            padding: const EdgeInsets.all(20),

            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment.start,

              children: [

                Text(
                  'Chào ${provider.userName}',
                  style: const TextStyle(
                    fontSize: 28,
                    fontWeight:
                    FontWeight.bold,
                  ),
                ),

                const SizedBox(height: 20),

                const PromoBanner(
                  title:
                  'Giảm 20% cho chuyến đi tối',
                  code: 'SAFERIDE20',
                ),

                const SizedBox(height: 30),

                Row(
                  mainAxisAlignment:
                  MainAxisAlignment
                      .spaceBetween,

                  children: [

                    QuickActionItem(
                      icon: Icons.history,
                      title: 'Lịch sử',

                      backgroundColor:
                      Colors.grey.shade200,

                      iconColor:
                      Colors.black,

                      onTap: () {},
                    ),

                    QuickActionItem(
                      icon:
                      Icons.local_taxi,

                      title: 'Đặt xe',

                      backgroundColor:
                      Colors.green.shade100,

                      iconColor:
                      Colors.green,

                      onTap: () {},
                    ),

                    QuickActionItem(
                      icon:
                      Icons.local_offer,

                      title:
                      'Khuyến mãi',

                      backgroundColor:
                      Colors.orange.shade100,

                      iconColor:
                      Colors.orange,

                      onTap: () {},
                    ),

                    QuickActionItem(
                      icon: Icons.sos,

                      title: 'SOS',

                      backgroundColor:
                      Colors.red.shade100,

                      iconColor:
                      Colors.red,

                      onTap: () {},
                    ),
                  ],
                ),

                const SizedBox(height: 30),

                const Text(
                  'Chuyến đi gần đây',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight:
                    FontWeight.bold,
                  ),
                ),

                const SizedBox(height: 15),

                ...provider.recentTrips.map(
                      (trip) {

                    return Padding(
                      padding:
                      const EdgeInsets.only(
                        bottom: 12,
                      ),

                      child: RecentTripCard(
                        pickup:
                        trip.pickup,

                        destination:
                        trip.destination,

                        time:
                        trip.time,

                        onRebook: () {},
                      ),
                    );
                  },
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}