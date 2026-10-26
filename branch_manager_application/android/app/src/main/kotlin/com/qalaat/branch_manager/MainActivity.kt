package com.qalaat.branch_manager

import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.android.FlutterActivityLaunchConfigs
import io.flutter.embedding.android.RenderMode
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File

class MainActivity : FlutterActivity() {
    private var pendingApkPath: String? = null

    override fun getRenderMode(): RenderMode = RenderMode.texture

    override fun getBackgroundMode(): FlutterActivityLaunchConfigs.BackgroundMode =
        FlutterActivityLaunchConfigs.BackgroundMode.opaque

    override fun onResume() {
        super.onResume()
        val path = pendingApkPath ?: return
        if (canRequestInstall()) {
            pendingApkPath = null
            try {
                installApkFile(path, openSettingsIfNeeded = false)
            } catch (_: Exception) {
            }
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "branch_manager/apk_install")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "canInstall" -> result.success(canRequestInstall())
                    "openInstallSettings" -> {
                        val path = call.argument<String>("path")
                        if (!path.isNullOrBlank()) pendingApkPath = path
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
            startActivity(
                Intent(
                    Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                    Uri.parse("package:$packageName"),
                ),
            )
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
            if (openSettingsIfNeeded) openUnknownSourcesSettings()
            return mapOf(
                "ok" to false,
                "code" to "need_permission",
                "message" to "فعّل السماح بتثبيت التطبيقات من مصدر تطبيق إدارة الفرع ثم ارجع للتطبيق",
            )
        }

        val uri = FileProvider.getUriForFile(
            this,
            "$packageName.fileprovider",
            file,
        )
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
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
}
