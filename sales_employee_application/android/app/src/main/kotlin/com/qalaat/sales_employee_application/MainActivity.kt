package com.qalaat.sales_employee_application

import android.content.Intent
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "saleshaider/location")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "start" -> {
                        val intent = Intent(this, LocationForegroundService::class.java)
                        intent.putExtra(LocationForegroundService.EXTRA_SHIFT_ID, call.argument<Int>("shiftId") ?: 0)
                        intent.putExtra(LocationForegroundService.EXTRA_CUTOFF, (call.argument<Number>("cutoffAtUtcMs") ?: 0).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_INTERVAL, (call.argument<Number>("intervalMs") ?: 12000).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_DISTANCE, (call.argument<Number>("minDistance") ?: 20).toFloat())
                        intent.putExtra(LocationForegroundService.EXTRA_STATIONARY, (call.argument<Number>("stationaryIntervalMs") ?: 60000).toLong())
                        startForegroundService(intent)
                        result.success(true)
                    }
                    "stop" -> {
                        val intent = Intent(this, LocationForegroundService::class.java)
                        intent.action = LocationForegroundService.ACTION_STOP
                        startService(intent)
                        result.success(true)
                    }
                    "isRunning" -> result.success(LocationForegroundService.running)
                    else -> result.notImplemented()
                }
            }
    }
}
