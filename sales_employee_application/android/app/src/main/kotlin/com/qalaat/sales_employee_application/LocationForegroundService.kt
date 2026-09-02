package com.qalaat.sales_employee_application

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.location.Location
import android.os.Build
import android.os.IBinder
import android.os.Looper
import androidx.core.app.NotificationCompat
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import android.database.sqlite.SQLiteDatabase
import android.os.Handler
import com.google.android.gms.location.LocationAvailability
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

class LocationForegroundService : Service() {
    private val client by lazy { LocationServices.getFusedLocationProviderClient(this) }
    private val handler = Handler(Looper.getMainLooper())
    private var db: SQLiteDatabase? = null
    private var shiftId: Int = 0
    private var cutoffAtUtcMs: Long = 0
    private var intervalMs: Long = 12_000
    private var stationaryIntervalMs: Long = 60_000
    private var minDistance: Float = 20f
    private var lastLocation: Location? = null
    private var lastMoveAt: Long = 0
    private var lastGpsAvailable: Boolean? = null

    private val cutoffStop = Runnable { stopCollecting() }

    private val callback = object : LocationCallback() {
        override fun onLocationResult(result: LocationResult) {
            val loc = result.lastLocation ?: return
            if (System.currentTimeMillis() >= cutoffAtUtcMs) {
                stopCollecting()
                return
            }
            val prev = lastLocation
            val moved = prev == null || loc.distanceTo(prev) >= minDistance
            val now = System.currentTimeMillis()
            if (!moved && now - lastMoveAt < stationaryIntervalMs) {
                return
            }
            if (moved) {
                lastMoveAt = now
            }
            lastLocation = loc
            insertPoint(loc)
        }

        override fun onLocationAvailability(availability: LocationAvailability) {
            val available = availability.isLocationAvailable
            if (lastGpsAvailable == available) return
            lastGpsAvailable = available
            insertEvent(if (available) "GPS_ENABLED" else "GPS_DISABLED")
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            handler.removeCallbacks(cutoffStop)
            stopUpdates()
            running = false
            stopSelf()
            return START_NOT_STICKY
        }
        openDb()
        if (intent != null && intent.hasExtra(EXTRA_SHIFT_ID)) {
            shiftId = intent.getIntExtra(EXTRA_SHIFT_ID, 0)
            cutoffAtUtcMs = intent.getLongExtra(EXTRA_CUTOFF, 0L)
            intervalMs = intent.getLongExtra(EXTRA_INTERVAL, 12_000)
            stationaryIntervalMs = intent.getLongExtra(EXTRA_STATIONARY, 60_000)
            minDistance = intent.getFloatExtra(EXTRA_DISTANCE, 20f)
        } else {
            shiftId = readMetaInt("active_shift_id")
            cutoffAtUtcMs = readMetaLong("cutoff_at_utc_ms")
            intervalMs = readMetaLong("interval_ms").takeIf { it > 0 } ?: 12_000
            stationaryIntervalMs = readMetaLong("stationary_ms").takeIf { it > 0 } ?: 60_000
            minDistance = readMetaFloat("min_distance").takeIf { it > 0f } ?: 20f
        }
        persistSession()
        startForegroundNotification()
        if (shiftId <= 0 || System.currentTimeMillis() >= cutoffAtUtcMs) {
            stopCollecting()
            return START_NOT_STICKY
        }
        running = true
        startUpdates()
        handler.removeCallbacks(cutoffStop)
        val delay = cutoffAtUtcMs - System.currentTimeMillis()
        if (delay > 0) {
            handler.postDelayed(cutoffStop, delay)
        }
        return START_REDELIVER_INTENT
    }

    override fun onDestroy() {
        handler.removeCallbacks(cutoffStop)
        stopUpdates()
        db?.close()
        running = false
        super.onDestroy()
    }

    private fun stopCollecting() {
        running = false
        handler.removeCallbacks(cutoffStop)
        stopUpdates()
        stopSelf()
    }

    private fun startForegroundNotification() {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            manager.createNotificationChannel(
                NotificationChannel(CHANNEL_ID, "الدوام", NotificationManager.IMPORTANCE_LOW)
            )
        }
        val notification: Notification = NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("قلعة الضمان")
            .setContentText("الدوام فعال")
            .setSmallIcon(android.R.drawable.ic_menu_mylocation)
            .setOngoing(true)
            .setSilent(true)
            .build()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION)
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun startUpdates() {
        val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, intervalMs)
            .setMinUpdateIntervalMillis(intervalMs)
            .setMinUpdateDistanceMeters(minDistance)
            .build()
        try {
            client.requestLocationUpdates(request, callback, Looper.getMainLooper())
        } catch (_: SecurityException) {
            stopSelf()
        }
    }

    private fun stopUpdates() {
        try {
            client.removeLocationUpdates(callback)
        } catch (_: Exception) {
        }
    }

    private fun openDb() {
        db = openOrCreateDatabase("sales_tracking.db", MODE_PRIVATE, null)
        db?.execSQL(
            """CREATE TABLE IF NOT EXISTS local_location_points (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                shift_id INTEGER NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                accuracy REAL,
                speed REAL,
                heading REAL,
                captured_at_utc TEXT NOT NULL,
                device_sequence INTEGER NOT NULL,
                sync_status TEXT NOT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0,
                UNIQUE(shift_id, device_sequence)
            )"""
        )
        db?.execSQL("CREATE TABLE IF NOT EXISTS tracking_meta (key TEXT PRIMARY KEY, value TEXT)")
        db?.execSQL(
            """CREATE TABLE IF NOT EXISTS local_tracking_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                shift_id INTEGER,
                event_type TEXT NOT NULL,
                occurred_at_utc TEXT NOT NULL,
                sync_status TEXT NOT NULL
            )"""
        )
    }

    private fun nextSequence(): Long {
        val key = "seq_$shiftId"
        val cursor = db?.rawQuery("SELECT value FROM tracking_meta WHERE key = ?", arrayOf(key))
        var current = 0L
        if (cursor != null && cursor.moveToFirst()) {
            current = cursor.getString(0)?.toLongOrNull() ?: 0L
        }
        cursor?.close()
        val next = current + 1
        db?.execSQL(
            "INSERT OR REPLACE INTO tracking_meta(key, value) VALUES (?, ?)",
            arrayOf(key, next.toString())
        )
        return next
    }

    private fun insertPoint(loc: Location) {
        val seq = nextSequence()
        val fmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
        fmt.timeZone = TimeZone.getTimeZone("UTC")
        val captured = fmt.format(Date(loc.time.takeIf { it > 0 } ?: System.currentTimeMillis()))
        db?.execSQL(
            """INSERT OR IGNORE INTO local_location_points
                (shift_id, latitude, longitude, accuracy, speed, heading, captured_at_utc, device_sequence, sync_status, retry_count)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Pending', 0)""",
            arrayOf<Any?>(
                shiftId,
                loc.latitude,
                loc.longitude,
                loc.accuracy,
                if (loc.hasSpeed()) loc.speed else null,
                if (loc.hasBearing()) loc.bearing else null,
                captured,
                seq
            )
        )
    }

    private fun persistSession() {
        writeMeta("active_shift_id", shiftId.toString())
        writeMeta("cutoff_at_utc_ms", cutoffAtUtcMs.toString())
        writeMeta("interval_ms", intervalMs.toString())
        writeMeta("stationary_ms", stationaryIntervalMs.toString())
        writeMeta("min_distance", minDistance.toString())
    }

    private fun insertEvent(type: String) {
        val fmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
        fmt.timeZone = TimeZone.getTimeZone("UTC")
        db?.execSQL(
            "INSERT INTO local_tracking_events(shift_id, event_type, occurred_at_utc, sync_status) VALUES (?, ?, ?, 'Pending')",
            arrayOf(shiftId, type, fmt.format(Date()))
        )
    }

    private fun writeMeta(key: String, value: String) {
        db?.execSQL("INSERT OR REPLACE INTO tracking_meta(key, value) VALUES (?, ?)", arrayOf(key, value))
    }

    private fun readMetaInt(key: String) = readMeta(key)?.toIntOrNull() ?: 0

    private fun readMetaLong(key: String) = readMeta(key)?.toLongOrNull() ?: 0L

    private fun readMetaFloat(key: String) = readMeta(key)?.toFloatOrNull() ?: 0f

    private fun readMeta(key: String): String? {
        val cursor = db?.rawQuery("SELECT value FROM tracking_meta WHERE key = ?", arrayOf(key))
        val value = if (cursor != null && cursor.moveToFirst()) cursor.getString(0) else null
        cursor?.close()
        return value
    }

    companion object {
        const val ACTION_STOP = "com.qalaat.sales_employee_application.STOP_TRACKING"
        const val EXTRA_SHIFT_ID = "shiftId"
        const val EXTRA_CUTOFF = "cutoffAtUtcMs"
        const val EXTRA_INTERVAL = "intervalMs"
        const val EXTRA_STATIONARY = "stationaryIntervalMs"
        const val EXTRA_DISTANCE = "minDistance"
        const val CHANNEL_ID = "sales_shift"
        const val NOTIFICATION_ID = 4101
        @Volatile var running: Boolean = false
    }
}
