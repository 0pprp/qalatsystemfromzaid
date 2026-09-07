package com.qalaat.sales_employee_application

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build

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
        val start = Intent(context, LocationForegroundService::class.java)
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(start)
            } else {
                context.startService(start)
            }
        } catch (e: Exception) {
            LocationForegroundService.log("UPLOAD_FAILED status=boot-start error=${e.message}")
        }
    }
}
