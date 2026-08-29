import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import 'package:provider/provider.dart';
import '../../../../../core/widgets/app_dialog.dart';
import '../../../../../core/widgets/motorbike_feature_notice.dart';
import '../../data/models/vehicle_model.dart';
import '../providers/vehicle_provider.dart';
import '../widgets/vehicle_card.dart';
import '../widgets/vehicle_form_sheet.dart';
import 'vehicle_insurance_page.dart';

class MyVehiclesPage extends StatefulWidget {
  const MyVehiclesPage({super.key});

  @override
  State<MyVehiclesPage> createState() => _MyVehiclesPageState();
}

class _MyVehiclesPageState extends State<MyVehiclesPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<VehicleProvider>().loadVehicles();
    });
  }

  void _onAddVehicle() {
    final provider = context.read<VehicleProvider>();
    VehicleFormSheet.show(
      context,
      onSave: (newVehicle) async {
        final saved = await provider.saveVehicle(newVehicle);
        if (!mounted) return false;
        if (!saved) _showError(provider);
        return saved;
      },
    );
  }

  void _onEditVehicle(VehicleModel vehicle) {
    final provider = context.read<VehicleProvider>();
    VehicleFormSheet.show(
      context,
      vehicle: vehicle,
      onSave: (updatedVehicle) async {
        final saved = await provider.saveVehicle(updatedVehicle);
        if (!mounted) return false;
        if (!saved) _showError(provider);
        return saved;
      },
    );
  }

  Future<void> _openInsurance(VehicleModel vehicle) {
    if (vehicle.type == VehicleType.motorbike) {
      return MotorbikeFeatureNotice.show(context);
    }
    return Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider.value(
          value: context.read<VehicleProvider>(),
          child: VehicleInsurancePage(vehicle: vehicle),
        ),
      ),
    );
  }

  void _handleDelete(VehicleModel vehicle) {
    AppDialog.show(
      context: context,
      icon: Icons.delete_forever_rounded,
      title: context.l10n.deleteVehicleQuestion,
      description: context.l10n.deleteVehicleDescription(vehicle.name),
      confirmText: context.l10n.deleteNow,
      cancelText: context.l10n.dismiss,
      onConfirm: () async {
        Navigator.pop(context);
        final provider = context.read<VehicleProvider>();
        final deleted = await provider.deleteVehicle(vehicle.id);
        if (!mounted) return;
        if (!deleted) {
          _showError(provider);
        }
      },
    );
  }

  void _showError(VehicleProvider provider) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(provider.errorMessage ?? context.l10n.requestFailed),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    const tealColor = Color(0xFF006B70);

    return Scaffold(
      backgroundColor: Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        leading: IconButton(
          icon: Icon(Icons.arrow_back, color: tealColor),
          onPressed: () => Navigator.pop(context),
        ),
        title: Text(
          context.l10n.myVehicles,
          style: TextStyle(
            color: tealColor,
            fontWeight: FontWeight.bold,
            fontSize: 20,
          ),
        ),
        centerTitle: false,
      ),
      body: Consumer<VehicleProvider>(
        builder: (context, provider, child) {
          return Stack(
            children: [
              Column(
                children: [
                  Expanded(child: _buildContent(provider)),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 0, 24, 32),
                    child: SizedBox(
                      width: double.infinity,
                      height: 56,
                      child: ElevatedButton.icon(
                        onPressed: provider.isLoading || provider.isMutating
                            ? null
                            : _onAddVehicle,
                        icon: Icon(Icons.add, size: 22),
                        label: Text(
                          context.l10n.addVehicle,
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: tealColor,
                          foregroundColor: Colors.white,
                          elevation: 0,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(16),
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              if (provider.isMutating)
                const Positioned.fill(
                  child: ColoredBox(
                    color: Color(0x22000000),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  Widget _buildContent(VehicleProvider provider) {
    if (provider.isLoading) {
      return Center(child: CircularProgressIndicator());
    }

    if (provider.errorMessage != null && provider.vehicles.isEmpty) {
      return RefreshIndicator(
        onRefresh: provider.loadVehicles,
        color: Color(0xFF006B70),
        child: LayoutBuilder(
          builder: (context, constraints) {
            return SingleChildScrollView(
              physics: AlwaysScrollableScrollPhysics(),
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24.0),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          Icons.cloud_off_rounded,
                          size: 80,
                          color: Colors.grey,
                        ),
                        SizedBox(height: 16),
                        Text(
                          context.l10n.serverConnectionErrorTitle,
                          style: TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF1A1A1A),
                          ),
                        ),
                        SizedBox(height: 8),
                        Text(
                          provider.errorMessage!,
                          textAlign: TextAlign.center,
                          style: TextStyle(fontSize: 15, color: Colors.black54),
                        ),
                        SizedBox(height: 32),
                        ElevatedButton.icon(
                          onPressed: provider.loadVehicles,
                          icon: Icon(Icons.refresh_rounded),
                          label: Text(
                            context.l10n.retry,
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: Color(0xFF006B70),
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(
                              horizontal: 32,
                              vertical: 14,
                            ),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(16),
                            ),
                            elevation: 0,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: provider.loadVehicles,
      child: ListView(
        physics: AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            context.l10n.vehicleManagementDescription,
            style: TextStyle(
              fontSize: 14,
              color: Color(0xFF6B7280),
              height: 1.5,
            ),
          ),
          SizedBox(height: 24),
          if (provider.vehicles.isEmpty)
            Padding(
              padding: EdgeInsets.symmetric(vertical: 64),
              child: Column(
                children: [
                  Icon(
                    Icons.directions_car_outlined,
                    size: 56,
                    color: Color(0xFF9CA3AF),
                  ),
                  SizedBox(height: 16),
                  Text(
                    context.l10n.noVehicles,
                    style: TextStyle(color: Color(0xFF6B7280), fontSize: 15),
                  ),
                ],
              ),
            ),
          ...provider.vehicles.map(
            (vehicle) => VehicleCard(
              key: ValueKey(vehicle.id),
              vehicle: vehicle,
              onEdit: () => _onEditVehicle(vehicle),
              onDelete: () => _handleDelete(vehicle),
              onInsurance: () => _openInsurance(vehicle),
            ),
          ),
        ],
      ),
    );
  }
}
