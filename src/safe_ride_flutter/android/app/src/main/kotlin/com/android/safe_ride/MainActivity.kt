package com.android.safe_ride

import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.View
import android.view.WindowInsets
import android.view.WindowInsetsController
import io.flutter.embedding.android.FlutterActivity

class MainActivity : FlutterActivity() {
    private val systemUiHandler = Handler(Looper.getMainLooper())
    private val hideNavigationRunnable = object : Runnable {
        override fun run() {
            hideNavigationBar()
            systemUiHandler.postDelayed(this, NAVIGATION_HIDE_DELAY_MS)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.decorView.setOnSystemUiVisibilityChangeListener { visibility ->
            val navigationBarIsVisible =
                visibility and View.SYSTEM_UI_FLAG_HIDE_NAVIGATION == 0
            if (navigationBarIsVisible) {
                scheduleNavigationBarHide()
            }
        }
    }

    override fun onPostResume() {
        super.onPostResume()
        hideNavigationBar()
        scheduleNavigationBarHide()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            scheduleNavigationBarHide()
        }
    }

    private fun scheduleNavigationBarHide() {
        systemUiHandler.removeCallbacks(hideNavigationRunnable)
        systemUiHandler.postDelayed(hideNavigationRunnable, NAVIGATION_HIDE_DELAY_MS)
    }

    private fun hideNavigationBar() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            window.insetsController?.apply {
                hide(WindowInsets.Type.navigationBars())
                systemBarsBehavior =
                    WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            }
            return
        }

        @Suppress("DEPRECATION")
        window.decorView.systemUiVisibility =
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY or
                View.SYSTEM_UI_FLAG_HIDE_NAVIGATION or
                View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION or
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
    }

    override fun onDestroy() {
        systemUiHandler.removeCallbacks(hideNavigationRunnable)
        window.decorView.setOnSystemUiVisibilityChangeListener(null)
        super.onDestroy()
    }

    private companion object {
        const val NAVIGATION_HIDE_DELAY_MS = 2_000L
    }
}
