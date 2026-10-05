package com.qalaat.follower

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File

class MainActivity : FlutterActivity() {
    /** APK path waiting for Install unknown apps permission grant. */
    private var pendingApkPath: String? = null

    override fun onResume() {
        super.onResume()
        // After user returns from unknown-sources settings, finish install automatically.
        val path = pendingApkPath ?: return
        if (canRequestInstall()) {
            pendingApkPath = null
            try {
                installApkFile(path, openSettingsIfNeeded = false)
            } catch (_: Exception) {
                // Flutter dialog can still offer retry if needed.
            }
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "follower/apk_install")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "canInstall" -> {
                        result.success(canRequestInstall())
                    }
                    "openInstallSettings" -> {
                        val path = call.argument<String>("path")
                        if (!path.isNullOrBlank()) {
                            pendingApkPath = path
                        }
                        openUnknownSourcesSettings()
                        result.success(true)
                    }
                    "installApk" -> {
                        val path = call.argument<String>("path")
                        if (path.isNullOrBlank()) {
                            result.error("PATH", "missing apk path", null)
                            return@setMethodCallHandler
                        }
                        try {
                            result.success(installApkFile(path, openSettingsIfNeeded = true))
                        } catch (e: Exception) {
                            result.error("INSTALL", e.message, e.javaClass.simpleName)
                        }
                    }
                    else -> result.notImplemented()
                }
            }

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "follower/location")
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
                            LocationForegroundService.log("FOLLOWER_SERVICE_STARTED invoke startForegroundService")
                            startForegroundService(intent)
                            result.success(true)
                        } catch (e: Exception) {
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
                    else -> result.notImplemented()
                }
            }
    }

    private fun canRequestInstall(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            packageManager.canRequestPackageInstalls()
        } else {
            true
        }
    }

    private fun openUnknownSourcesSettings() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            // Package-scoped page — makes this app appear under Install unknown apps.
            val intent = Intent(
                Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:$packageName"),
            )
            startActivity(intent)
        } else {
            @Suppress("DEPRECATION")
            startActivity(Intent(Settings.ACTION_SECURITY_SETTINGS))
        }
    }

    private fun installApkFile(
        path: String,
        openSettingsIfNeeded: Boolean,
    ): Map<String, Any?> {
        val file = File(path)
        if (!file.exists() || file.length() <= 0L) {
            return mapOf(
                "ok" to false,
                "code" to "missing_file",
                "message" to "ملف التحديث غير موجود",
            )
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && !canRequestInstall()) {
            pendingApkPath = path
            if (openSettingsIfNeeded) {
                openUnknownSourcesSettings()
            }
            return mapOf(
                "ok" to false,
                "code" to "need_permission",
                "message" to "فعّل السماح بتثبيت التطبيقات من مصدر تطبيق المتابع ثم ارجع للتطبيق",
            )
        }

        // App-owned FileProvider (content://) — never file://
        val uri = FileProvider.getUriForFile(
            this,
            "$packageName.fileprovider",
            file,
        )
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            // Started from Activity — avoid NEW_TASK/CLEAR_TOP so Package Installer
            // stays on top and shows the native Update / Cancel screen.
        }

        val resInfo = packageManager.queryIntentActivities(intent, PackageManager.MATCH_DEFAULT_ONLY)
        for (resolve in resInfo) {
            grantUriPermission(
                resolve.activityInfo.packageName,
                uri,
                Intent.FLAG_GRANT_READ_URI_PERMISSION,
            )
        }

        return try {
            startActivity(intent)
            pendingApkPath = null
            mapOf(
                "ok" to true,
                "code" to "launched",
                "message" to "opened",
                "resolvers" to resInfo.size,
            )
        } catch (e: Exception) {
            mapOf(
                "ok" to false,
                "code" to "launch_failed",
                "message" to (e.message ?: "تعذر فتح المثبت"),
            )
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
}
