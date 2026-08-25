package com.android.safe_ride

import io.flutter.app.FlutterApplication

/**
 * Custom Application class.
 *
 * Registered in AndroidManifest.xml to run before any Activity.
 * Provides a hook for early SDK initialization and for future
 * app-level configuration that must run before Flutter bootstraps.
 *
 * Note: "Too many Flogger logs before configuration" warnings are
 * emitted by Google Play Services internals (ProxyAndroidLoggerBackend)
 * and resolve themselves once Play Services finishes initializing.
 * They are harmless and cannot be suppressed from application code
 * because the Flogger backend is an internal Play Services component.
 */
class SafeRideApplication : FlutterApplication() {
    override fun onCreate() {
        super.onCreate()
    }
}
