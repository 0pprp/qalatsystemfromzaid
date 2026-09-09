package com.qalaat.sales_employee_application

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.database.sqlite.SQLiteDatabase
import android.os.Build

/**
 * Restarts foreground tracking after reboot only when an active shift is persisted locally
 * and the cutoff has not passed.
 */
class TrackingBootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent?) {
        val action = intent?.action ?: return
        if (action != Intent.ACTION_BOOT_COMPLETED &&
            action != Intent.ACTION_MY_PACKAGE_REPLACED &&
            action != Intent.ACTION_LOCKED_BOOT_COMPLETED
        ) {
            return
        }
        LocationForegroundService.log("BOOT_OR_REPLACE action=$action")
        val dbFile = context.getDatabasePath(LocationForegroundService.DB_NAME)
        if (!dbFile.exists()) return

        var shiftId = 0
        var cutoff = 0L
        var active = false
        try {
            val db = SQLiteDatabase.openDatabase(dbFile.path, null, SQLiteDatabase.OPEN_READONLY)
            db.rawQuery(
                "SELECT key, value FROM tracking_meta WHERE key IN ('active_shift_id','cutoff_at_utc_ms','shift_active')",
                null,
            ).use { cursor ->
                while (cursor.moveToNext()) {
                    when (cursor.getString(0)) {
                        "active_shift_id" -> shiftId = cursor.getString(1)?.toIntOrNull() ?: 0
                        "cutoff_at_utc_ms" -> cutoff = cursor.getString(1)?.toLongOrNull() ?: 0L
                        "shift_active" -> active = cursor.getString(1) == "1"
                    }
                }
            }
            db.close()
        } catch (e: Exception) {
            LocationForegroundService.log("BOOT_SKIP reason=meta-read error=${e.message}")
            return
        }

        val now = System.currentTimeMillis()
        if (!active || shiftId <= 0 || cutoff <= 0L || now >= cutoff) {
            LocationForegroundService.log(
                "BOOT_SKIP reason=no-active-shift shiftId=$shiftId active=$active cutoff=$cutoff",
            )
            return
        }

        val start = Intent(context, LocationForegroundService::class.java)
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(start)
            } else {
                context.startService(start)
            }
            LocationForegroundService.log("BOOT_RESTART_TRACKING shiftId=$shiftId")
        } catch (e: Exception) {
            LocationForegroundService.log("UPLOAD_FAILED status=boot-start error=${e.message}")
        }
    }
}
