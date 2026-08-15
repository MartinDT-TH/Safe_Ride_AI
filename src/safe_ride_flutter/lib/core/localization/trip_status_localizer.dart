import '../../l10n/generated/app_localizations.dart';

abstract final class TripStatusLocalizer {
  static String translate(AppLocalizations l10n, String status) {
    switch (status.toLowerCase()) {
      case 'pending':
        return l10n.statusPending;
      case 'pendingschedule':
      case 'pending_schedule':
        return l10n.awaitingConfirmation;
      case 'searching':
        return l10n.searchingDriver;
      case 'driverassigned':
      case 'driver_assigned':
        return l10n.driverConfirmed;
      case 'driverarriving':
      case 'driver_arriving':
        return l10n.statusDriverArriving;
      case 'inprogress':
      case 'in_progress':
        return l10n.statusInProgress;
      case 'accepted':
      case 'driveraccepted':
        return l10n.statusAccepted;
      case 'arrived':
        return l10n.statusArrived;
      case 'waiting_return_confirm':
        return l10n.waitingReturnConfirmation;
      case 'return_confirmed':
        return l10n.returnConfirmedStatus;
      case 'waiting_payment':
        return l10n.waitForPayment;
      case 'completed':
        return l10n.statusCompleted;
      case 'cancelled':
      case 'canceled':
        return l10n.statusCancelled;
      case 'expired':
        return l10n.expired;
      case 'sent':
        return l10n.offerSent;
      case 'customerconfirmed':
        return l10n.offerCustomerConfirmed;
      case 'rejected':
        return l10n.offerRejected;
      case 'online':
        return l10n.statusOnline;
      case 'offline':
        return l10n.statusOffline;
      case 'busy':
        return l10n.statusBusy;
      default:
        return status;
    }
  }
}
