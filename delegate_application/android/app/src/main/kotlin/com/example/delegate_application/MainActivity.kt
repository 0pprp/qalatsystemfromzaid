package com.example.delegate_application

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.Intent
import android.net.Uri
import androidx.core.content.FileProvider
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream

/**
 * Opens THIS customer's WhatsApp chat with the PDF attached.
 *
 * WhatsApp honors the jid extra only when a chat with that number already
 * exists; otherwise it shows the contacts picker. Dart opens the chat by link
 * first for new numbers, then calls sharePdfToWhatsApp.
 *
 * Never uses Intent.createChooser. Never hardcodes a phone number.
 */
class MainActivity : FlutterActivity() {
    private val channelName = "delegate_application/whatsapp_share"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, channelName)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "sharePdfToWhatsApp" -> {
                        val phone = call.argument<String>("phone") ?: ""
                        val filePath = call.argument<String>("filePath") ?: ""
                        val text = call.argument<String>("text")
                        try {
                            val mode = sharePdfIntoCustomerChat(phone, filePath, text)
                            result.success(mode)
                        } catch (e: ActivityNotFoundException) {
                            result.error("NO_WHATSAPP", "WhatsApp not installed", null)
                        } catch (e: Exception) {
                            result.error("SHARE_FAILED", e.message, null)
                        }
                    }
                    "openWhatsAppChat" -> {
                        val phone = call.argument<String>("phone") ?: ""
                        val text = call.argument<String>("text")
                        try {
                            openCustomerChat(phone, text)
                            result.success(true)
                        } catch (e: ActivityNotFoundException) {
                            result.error("NO_WHATSAPP", "WhatsApp not installed", null)
                        } catch (e: Exception) {
                            result.error("OPEN_FAILED", e.message, null)
                        }
                    }
                    else -> result.notImplemented()
                }
            }
    }

    /**
     * @return "pdf" when chat opened with attachment, "chat" when only deep-link opened.
     */
    private fun sharePdfIntoCustomerChat(
        phoneRaw: String,
        filePath: String,
        text: String?,
    ): String {
        // Fresh normalize every call — never reuse a previous customer's number.
        val phone = normalizePhone(phoneRaw)
        if (phone.isEmpty()) throw IllegalArgumentException("Empty phone")

        val src = File(filePath)
        if (!src.exists() || src.length() == 0L) {
            throw IllegalArgumentException("File not found: $filePath")
        }

        // Folder per phone keeps each customer's copy separate; name shown in WhatsApp.
        val dir = File(externalCacheDir ?: cacheDir, "wa_$phone").apply { mkdirs() }
        val shareFile = File(dir, "كشف حساب.pdf")
        FileInputStream(src).use { input ->
            FileOutputStream(shareFile).use { output -> input.copyTo(output) }
        }

        val uri = FileProvider.getUriForFile(
            this,
            "${applicationContext.packageName}.fileprovider",
            shareFile,
        )

        val packages = listOf("com.whatsapp", "com.whatsapp.w4b")
        // jid is built from THIS call's phone only.
        val jids = listOf("$phone@s.whatsapp.net", "$phone@c.us")

        for (pkg in packages) {
            if (!isPackageInstalled(pkg)) continue
            for (jid in jids) {
                val intent = Intent(Intent.ACTION_SEND).apply {
                    type = "application/pdf"
                    putExtra(Intent.EXTRA_STREAM, uri)
                    putExtra("jid", jid)
                    setPackage(pkg)
                    clipData = ClipData.newUri(contentResolver, "كشف حساب.pdf", uri)
                    // NEW_TASK without CLEAR_TOP — CLEAR_TOP reopened the previous customer's chat.
                    addFlags(
                        Intent.FLAG_GRANT_READ_URI_PERMISSION or
                            Intent.FLAG_ACTIVITY_NEW_TASK,
                    )
                }
                try {
                    grantUriPermission(
                        pkg,
                        uri,
                        Intent.FLAG_GRANT_READ_URI_PERMISSION,
                    )
                    if (intent.resolveActivity(packageManager) == null) continue
                    startActivity(intent)
                    return "pdf"
                } catch (_: Exception) {
                    // try next combination
                }
            }
        }

        // Same link path, dynamic phone: wa.me/{phone} / whatsapp://send?phone={phone}
        openCustomerChat(phone, text)
        return "chat"
    }

    private fun openCustomerChat(phoneRaw: String, text: String?) {
        val phone = normalizePhone(phoneRaw)
        if (phone.isEmpty()) throw IllegalArgumentException("Empty phone")

        val encoded = if (!text.isNullOrBlank()) Uri.encode(text) else null
        // Template is fixed; only {phone} changes per customer.
        val candidates = if (encoded != null) {
            listOf(
                Uri.parse("whatsapp://send?phone=$phone&text=$encoded"),
                Uri.parse("https://api.whatsapp.com/send?phone=$phone&text=$encoded"),
                Uri.parse("https://wa.me/$phone?text=$encoded"),
            )
        } else {
            listOf(
                Uri.parse("whatsapp://send?phone=$phone"),
                Uri.parse("https://api.whatsapp.com/send?phone=$phone"),
                Uri.parse("https://wa.me/$phone"),
            )
        }

        var last: Exception? = null
        for (uri in candidates) {
            for (pkg in listOf("com.whatsapp", "com.whatsapp.w4b")) {
                if (!isPackageInstalled(pkg)) continue
                try {
                    val intent = Intent(Intent.ACTION_VIEW, uri).apply {
                        setPackage(pkg)
                        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                    }
                    if (intent.resolveActivity(packageManager) != null) {
                        startActivity(intent)
                        return
                    }
                } catch (e: Exception) {
                    last = e
                }
            }
            try {
                startActivity(
                    Intent(Intent.ACTION_VIEW, uri).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
                )
                return
            } catch (e: Exception) {
                last = e
            }
        }
        throw (last ?: ActivityNotFoundException())
    }

    private fun isPackageInstalled(pkg: String): Boolean {
        return try {
            packageManager.getPackageInfo(pkg, 0)
            true
        } catch (_: Exception) {
            false
        }
    }

    private fun normalizePhone(phoneRaw: String): String {
        val digits = phoneRaw.filter { it.isDigit() }
        return when {
            digits.startsWith("964") -> digits
            digits.startsWith("0") && digits.length >= 10 -> "964${digits.substring(1)}"
            digits.length == 10 && digits.startsWith("7") -> "964$digits"
            digits.length == 9 && digits.startsWith("7") -> "964$digits"
            else -> digits
        }
    }
}
