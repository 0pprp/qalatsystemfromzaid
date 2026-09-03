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
    private var intervalMs: Long = 3_000
    private var stationaryIntervalMs: Long = 45_000
    private var minDistance: Float = 5f
    private var apiBase: String = ""
    private var token: String = ""
    private var lastLocation: Location? = null
    private var lastMoveAt: Long = 0
    private var lastGpsAvailable: Boolean? = null

    private val cutoffStop = Runnable { stopCollecting() }
    private val syncFlush = object : Runnable {
        override fun run() {
            Thread { flushPendingToServer() }.start()
            if (running) {
                handler.postDelayed(this, 20_000)
            }
        }
    }

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
            handler.removeCallbacks(syncFlush)
            stopUpdates()
            running = false
            stopSelf()
            return START_NOT_STICKY
        }
        openDb()
        if (intent != null && intent.hasExtra(EXTRA_SHIFT_ID)) {
            shiftId = intent.getIntExtra(EXTRA_SHIFT_ID, 0)
            cutoffAtUtcMs = intent.getLongExtra(EXTRA_CUTOFF, 0L)
            intervalMs = intent.getLongExtra(EXTRA_INTERVAL, 3_000)
            stationaryIntervalMs = intent.getLongExtra(EXTRA_STATIONARY, 45_000)
            minDistance = intent.getFloatExtra(EXTRA_DISTANCE, 5f)
            apiBase = intent.getStringExtra(EXTRA_API_BASE) ?: ""
            token = intent.getStringExtra(EXTRA_TOKEN) ?: ""
        } else {
            shiftId = readMetaInt("active_shift_id")
            cutoffAtUtcMs = readMetaLong("cutoff_at_utc_ms")
            intervalMs = readMetaLong("interval_ms").takeIf { it > 0 } ?: 3_000
            stationaryIntervalMs = readMetaLong("stationary_ms").takeIf { it > 0 } ?: 45_000
            minDistance = readMetaFloat("min_distance").takeIf { it > 0f } ?: 5f
            apiBase = readMeta("api_base") ?: ""
            token = readMeta("api_token") ?: ""
        }
        if (cutoffAtUtcMs <= 0L) {
            cutoffAtUtcMs = defaultCutoffUtcMs()
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
        handler.removeCallbacks(syncFlush)
        handler.post(syncFlush)
        val delay = cutoffAtUtcMs - System.currentTimeMillis()
        if (delay > 0) {
            handler.postDelayed(cutoffStop, delay)
        }
        return START_REDELIVER_INTENT
    }

    override fun onDestroy() {
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(syncFlush)
        flushPendingToServer()
        stopUpdates()
        db?.close()
        running = false
        super.onDestroy()
    }

    private fun stopCollecting() {
        running = false
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(syncFlush)
        flushPendingToServer()
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
            .setMinUpdateIntervalMillis(2_000)
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

    private fun defaultCutoffUtcMs(): Long {
        val cal = java.util.Calendar.getInstance(TimeZone.getTimeZone("Asia/Baghdad"))
        if (cal.get(java.util.Calendar.HOUR_OF_DAY) >= 3) {
            cal.add(java.util.Calendar.DAY_OF_MONTH, 1)
        }
        cal.set(java.util.Calendar.HOUR_OF_DAY, 3)
        cal.set(java.util.Calendar.MINUTE, 0)
        cal.set(java.util.Calendar.SECOND, 0)
        cal.set(java.util.Calendar.MILLISECOND, 0)
        return cal.timeInMillis
    }

    private fun persistSession() {
        writeMeta("active_shift_id", shiftId.toString())
        writeMeta("cutoff_at_utc_ms", cutoffAtUtcMs.toString())
        writeMeta("interval_ms", intervalMs.toString())
        writeMeta("stationary_ms", stationaryIntervalMs.toString())
        writeMeta("min_distance", minDistance.toString())
        if (apiBase.isNotEmpty()) writeMeta("api_base", apiBase)
        if (token.isNotEmpty()) writeMeta("api_token", token)
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

    private fun flushPendingToServer() {
        val base = (if (apiBase.isNotEmpty()) apiBase else readMeta("api_base") ?: "").trim()
        val auth = (if (token.isNotEmpty()) token else readMeta("api_token") ?: "").trim()
        val sid = if (shiftId > 0) shiftId else readMetaInt("active_shift_id")
        if (base.isEmpty() || auth.isEmpty() || sid <= 0 || db == null) return
        val cursor = db?.rawQuery(
            "SELECT latitude, longitude, accuracy, speed, heading, captured_at_utc, device_sequence FROM local_location_points WHERE shift_id = ? AND sync_status IN ('Pending','Failed') ORDER BY device_sequence ASC LIMIT 150",
            arrayOf(sid.toString())
        ) ?: return
        val points = org.json.JSONArray()
        val seqs = mutableListOf<Long>()
        while (cursor.moveToNext()) {
            val seq = cursor.getLong(6)
            seqs.add(seq)
            val obj = org.json.JSONObject()
            obj.put("latitude", cursor.getDouble(0))
            obj.put("longitude", cursor.getDouble(1))
            if (!cursor.isNull(2)) obj.put("accuracy", cursor.getDouble(2))
            if (!cursor.isNull(3)) obj.put("speed", cursor.getDouble(3))
            if (!cursor.isNull(4)) obj.put("heading", cursor.getDouble(4))
            obj.put("capturedAtUtc", cursor.getString(5))
            obj.put("deviceSequence", seq)
            points.put(obj)
        }
        cursor.close()
        if (points.length() == 0) return
        val body = org.json.JSONObject()
        body.put("shiftId", sid)
        body.put("points", points)
        try {
            val url = java.net.URL(base.trimEnd('/') + "/sales/location/batch")
            val conn = url.openConnection() as java.net.HttpURLConnection
            conn.requestMethod = "POST"
            conn.connectTimeout = 15000
            conn.readTimeout = 20000
            conn.setRequestProperty("Content-Type", "application/json")
            conn.setRequestProperty("Accept", "application/json")
            conn.setRequestProperty("Authorization", "Bearer $auth")
            conn.doOutput = true
            conn.outputStream.use { it.write(body.toString().toByteArray(Charsets.UTF_8)) }
            val code = conn.responseCode
            conn.disconnect()
            if (code in 200..299) {
                val placeholders = seqs.joinToString(",") { "?" }
                val args = mutableListOf<Any?>()
                args.add(sid)
                args.addAll(seqs)

                db?.execSQL(
                    "UPDATE local_location_points SET sync_status = 'Synced' WHERE shift_id = ? AND device_sequence IN ($placeholders)",
                    args.toTypedArray()
                )
            }
        } catch (_: Exception) {
        }
    }

    companion object {
        const val ACTION_STOP = "com.qalaat.sales_employee_application.STOP_TRACKING"
        const val EXTRA_SHIFT_ID = "shiftId"
        const val EXTRA_CUTOFF = "cutoffAtUtcMs"
        const val EXTRA_INTERVAL = "intervalMs"
        const val EXTRA_STATIONARY = "stationaryIntervalMs"
        const val EXTRA_DISTANCE = "minDistance"
        const val EXTRA_API_BASE = "apiBase"
        const val EXTRA_TOKEN = "token"
        const val CHANNEL_ID = "sales_shift"
        const val NOTIFICATION_ID = 4101
        @Volatile var running: Boolean = false
    }
}
