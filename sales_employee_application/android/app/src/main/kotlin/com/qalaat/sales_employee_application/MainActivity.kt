package com.qalaat.sales_employee_application

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Handler
import android.os.Looper
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
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
                        intent.putExtra(LocationForegroundService.EXTRA_STARTED_AT, (call.argument<Number>("startedAtUtcMs") ?: 0).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_INTERVAL, (call.argument<Number>("intervalMs") ?: 30_000).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_OFFICIAL_INTERVAL, (call.argument<Number>("officialIntervalMs") ?: 600_000).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_DISTANCE, (call.argument<Number>("minDistance") ?: 0).toFloat())
                        intent.putExtra(LocationForegroundService.EXTRA_STATIONARY, (call.argument<Number>("stationaryIntervalMs") ?: 45000).toLong())
                        intent.putExtra(LocationForegroundService.EXTRA_API_BASE, call.argument<String>("apiBase") ?: "")
                        intent.putExtra(LocationForegroundService.EXTRA_TOKEN, call.argument<String>("token") ?: "")
                        try {
                            LocationForegroundService.log("SERVICE_STARTED invoke startForegroundService")
                            android.util.Log.d("SHIFT START", "startForegroundService called")
                            startForegroundService(intent)
                            android.util.Log.d("SHIFT START", "startForegroundService returned")
                            result.success(true)
                            android.util.Log.d("SHIFT START", "startForegroundService returned")
                            result.success(true)
                        } catch (e: Exception) {
                            android.util.Log.e("SHIFT START", "startForegroundService ${e.javaClass.simpleName}: ${e.message}", e)
                            result.error("FOREGROUND_START", e.message, e.javaClass.simpleName)
                        }
                    }
                    "stop" -> {
                        val intent = Intent(this, LocationForegroundService::class.java)
                        intent.action = LocationForegroundService.ACTION_STOP
                        startService(intent)
                        result.success(true)
                    }
                    "flushAndStop" -> {
                        val intent = Intent(this, LocationForegroundService::class.java)
                        intent.action = LocationForegroundService.ACTION_FLUSH_STOP
                        startService(intent)
                        result.success(true)
                    }
                    "isRunning" -> result.success(LocationForegroundService.running)
                    "currentFix" -> currentFix(result)
                    "dial" -> dial(call.argument<String>("number"), result)
                    else -> result.notImplemented()
                }
            }
    }

    private fun currentFix(result: MethodChannel.Result) {
        val fine = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
        val coarse = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION)
        if (fine != PackageManager.PERMISSION_GRANTED && coarse != PackageManager.PERMISSION_GRANTED) {
            result.error("PERMISSION", "location permission denied", null)
            return
        }
        val client = LocationServices.getFusedLocationProviderClient(this)
        val cts = CancellationTokenSource()
        Handler(Looper.getMainLooper()).postDelayed({ cts.cancel() }, 15_000)
        client.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, cts.token)
            .addOnSuccessListener { loc ->
                if (loc != null) {
                    result.success(
                        hashMapOf(
                            "latitude" to loc.latitude,
                            "longitude" to loc.longitude,
                            "accuracy" to loc.accuracy.toDouble(),
                        ),
                    )
                    return@addOnSuccessListener
                }
                client.lastLocation
                    .addOnSuccessListener { last ->
                        if (last == null) {
                            result.error("GPS", "no location", null)
                        } else {
                            result.success(
                                hashMapOf(
                                    "latitude" to last.latitude,
                                    "longitude" to last.longitude,
                                    "accuracy" to last.accuracy.toDouble(),
                                ),
                            )
                        }
                    }
                    .addOnFailureListener { e -> result.error("GPS", e.message, null) }
            }
            .addOnFailureListener { e -> result.error("GPS", e.message, null) }
    }

    private fun dial(number: String?, result: MethodChannel.Result) {
        if (number.isNullOrBlank()) {
            result.error("PHONE", "missing number", null)
            return
        }
        try {
            startActivity(Intent(Intent.ACTION_DIAL, Uri.parse("tel:$number")))
            result.success(true)
        } catch (e: Exception) {
            result.error("PHONE", e.message, null)
        }
    }
}
