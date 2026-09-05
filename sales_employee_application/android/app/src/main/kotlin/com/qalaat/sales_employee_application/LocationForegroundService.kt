package com.qalaat.sales_employee_application

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.database.sqlite.SQLiteDatabase
import android.location.Location
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import androidx.core.app.NotificationCompat
import com.google.android.gms.location.LocationAvailability
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
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
    private var shiftStartedAtUtcMs: Long = 0
    private var intervalMs: Long = 30_000
    private var officialIntervalMs: Long = 600_000
    private var apiBase: String = ""
    private var token: String = ""
    private var lastLocation: Location? = null
    private var lastOfficialAtMs: Long = 0
    private var lastGpsAvailable: Boolean? = null

    private val cutoffStop = Runnable { stopCollecting() }
    private val officialTick = object : Runnable {
        override fun run() {
            persistDueOfficialPoints()
            if (running) {
                scheduleNextOfficialTick()
            }
        }
    }
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
            if (System.currentTimeMillis() >= cutoffAtUtcMs) {
                stopCollecting()
                return
            }
            val loc = result.lastLocation ?: return
            if (!isUsable(loc)) return
            lastLocation = loc
            persistLastFix(loc)
            persistDueOfficialPoints()
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
            running = false
            handler.removeCallbacks(cutoffStop)
            handler.removeCallbacks(officialTick)
            handler.removeCallbacks(syncFlush)
            stopUpdates()
            flushPendingToServer()
            stopSelf()
            return START_NOT_STICKY
        }
        openDb()
        if (intent != null && intent.hasExtra(EXTRA_SHIFT_ID)) {
            shiftId = intent.getIntExtra(EXTRA_SHIFT_ID, 0)
            cutoffAtUtcMs = intent.getLongExtra(EXTRA_CUTOFF, 0L)
            shiftStartedAtUtcMs = intent.getLongExtra(EXTRA_STARTED_AT, 0L)
            intervalMs = intent.getLongExtra(EXTRA_INTERVAL, 30_000).coerceAtLeast(5_000)
            officialIntervalMs = intent.getLongExtra(EXTRA_OFFICIAL_INTERVAL, 600_000).coerceAtLeast(60_000)
            apiBase = intent.getStringExtra(EXTRA_API_BASE) ?: ""
            token = intent.getStringExtra(EXTRA_TOKEN) ?: ""
        } else {
            shiftId = readMetaInt("active_shift_id")
            cutoffAtUtcMs = readMetaLong("cutoff_at_utc_ms")
            shiftStartedAtUtcMs = readMetaLong("shift_started_at_utc_ms")
            intervalMs = readMetaLong("interval_ms").takeIf { it > 0 } ?: 30_000
            officialIntervalMs = readMetaLong("official_interval_ms").takeIf { it > 0 } ?: 600_000
            apiBase = readMeta("api_base") ?: ""
            token = readMeta("api_token") ?: ""
        }
        if (cutoffAtUtcMs <= 0L) {
            cutoffAtUtcMs = defaultCutoffUtcMs()
        }
        lastOfficialAtMs = readMetaLong(officialMetaKey())
        restoreLastFix()
        persistSession()
        startForegroundNotification()
        if (shiftId <= 0 || System.currentTimeMillis() >= cutoffAtUtcMs) {
            stopCollecting()
            return START_NOT_STICKY
        }
        running = true
        startUpdates()
        seedLastLocation()
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(officialTick)
        handler.removeCallbacks(syncFlush)
        persistDueOfficialPoints()
        handler.post(syncFlush)
        scheduleNextOfficialTick()
        val delay = cutoffAtUtcMs - System.currentTimeMillis()
        if (delay > 0) {
            handler.postDelayed(cutoffStop, delay)
        }
        return START_REDELIVER_INTENT
    }

    override fun onDestroy() {
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(officialTick)
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
        handler.removeCallbacks(officialTick)
        handler.removeCallbacks(syncFlush)
        flushPendingToServer()
        stopUpdates()
        stopSelf()
    }

    private fun startForegroundNotification() {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            manager.createNotificationChannel(
                NotificationChannel(CHANNEL_ID, "Ø§Ù„Ø¯ÙˆØ§Ù…", NotificationManager.IMPORTANCE_LOW)
            )
        }
        val notification: Notification = NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Ù‚Ù„Ø¹Ø© Ø§Ù„Ø¶Ù…Ø§Ù†")
            .setContentText("Ø§Ù„Ø¯ÙˆØ§Ù… ÙØ¹Ø§Ù„")
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
            .setMinUpdateIntervalMillis(15_000)
            .setMinUpdateDistanceMeters(0f)
            .setWaitForAccurateLocation(false)
            .build()
        try {
            client.requestLocationUpdates(request, callback, Looper.getMainLooper())
        } catch (_: SecurityException) {
            stopSelf()
        }
    }

    private fun seedLastLocation() {
        try {
            client.lastLocation.addOnSuccessListener { loc ->
                if (!running) return@addOnSuccessListener
                if (loc != null && isUsable(loc)) {
                    lastLocation = loc
                    persistLastFix(loc)
                    persistDueOfficialPoints()
                }
            }
        } catch (_: SecurityException) {
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
        db?.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS ux_local_points_slot ON local_location_points(shift_id, captured_at_utc)")
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

    private fun persistDueOfficialPoints() {
        if (!running || shiftId <= 0) return
        val loc = lastLocation ?: return
        val now = System.currentTimeMillis()
        if (now >= cutoffAtUtcMs) {
            stopCollecting()
            return
        }
        val start = if (shiftStartedAtUtcMs > 0L) shiftStartedAtUtcMs else now
        val firstSlot = floorSlotUtcMs(start)
        var last = lastOfficialAtMs
        if (last > 0L) {
            last = floorSlotUtcMs(last)
        }
        var cursor = if (last > 0L) last + officialIntervalMs else firstSlot
        if (cursor < firstSlot) {
            cursor = firstSlot
        }
        var latest = last
        var inserted = false
        while (cursor <= now && cursor < cutoffAtUtcMs) {
            insertOfficialPoint(loc, cursor)
            latest = cursor
            inserted = true
            cursor += officialIntervalMs
        }
        if (inserted) {
            lastOfficialAtMs = latest
            writeMeta(officialMetaKey(), latest.toString())
        }
    }

    private fun scheduleNextOfficialTick() {
        handler.removeCallbacks(officialTick)
        if (!running) return
        val now = System.currentTimeMillis()
        val last = if (lastOfficialAtMs > 0L) floorSlotUtcMs(lastOfficialAtMs) else floorSlotUtcMs(now)
        val next = last + officialIntervalMs
        val delay = (next - now).coerceIn(1_000L, officialIntervalMs.coerceAtLeast(60_000L))
        handler.postDelayed(officialTick, delay)
    }

    private fun floorSlotUtcMs(epochMs: Long): Long {
        val cal = java.util.Calendar.getInstance(TimeZone.getTimeZone("Asia/Baghdad"))
        cal.timeInMillis = epochMs
        val minute = cal.get(java.util.Calendar.MINUTE)
        cal.set(java.util.Calendar.MINUTE, (minute / 10) * 10)
        cal.set(java.util.Calendar.SECOND, 0)
        cal.set(java.util.Calendar.MILLISECOND, 0)
        return cal.timeInMillis
    }

    private fun slotSequence(slotUtcMs: Long): Long = slotUtcMs / officialIntervalMs

    private fun insertOfficialPoint(loc: Location, slotUtcMs: Long) {
        val seq = slotSequence(slotUtcMs)
        val captured = utcIso(slotUtcMs)
        val actual = utcIso(loc.time.takeIf { it > 0 } ?: System.currentTimeMillis())
        db?.execSQL(
            """INSERT OR IGNORE INTO local_location_points
                (shift_id, latitude, longitude, accuracy, speed, heading, captured_at_utc, device_sequence, sync_status, retry_count)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Pending', 0)""",
            arrayOf<Any?>(
                shiftId,
                loc.latitude,
                loc.longitude,
                if (loc.hasAccuracy()) loc.accuracy else null,
                if (loc.hasSpeed()) loc.speed else null,
                if (loc.hasBearing()) loc.bearing else null,
                captured,
                seq
            )
        )
        writeMeta("actual_captured_${shiftId}_$seq", actual)
    }

    private fun isUsable(loc: Location): Boolean {
        if (loc.latitude < -90 || loc.latitude > 90 || loc.longitude < -180 || loc.longitude > 180) {
            return false
        }
        if (kotlin.math.abs(loc.latitude) < 0.000001 && kotlin.math.abs(loc.longitude) < 0.000001) {
            return false
        }
        if (loc.hasAccuracy() && loc.accuracy > 5000f) {
            return false
        }
        return true
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
        if (shiftStartedAtUtcMs > 0L) writeMeta("shift_started_at_utc_ms", shiftStartedAtUtcMs.toString())
        writeMeta("interval_ms", intervalMs.toString())
        writeMeta("official_interval_ms", officialIntervalMs.toString())
        if (apiBase.isNotEmpty()) writeMeta("api_base", apiBase)
        if (token.isNotEmpty()) writeMeta("api_token", token)
    }

    private fun persistLastFix(loc: Location) {
        writeMeta("last_lat", loc.latitude.toString())
        writeMeta("last_lng", loc.longitude.toString())
        if (loc.hasAccuracy()) writeMeta("last_acc", loc.accuracy.toString())
        writeMeta("last_fix_ms", (loc.time.takeIf { it > 0 } ?: System.currentTimeMillis()).toString())
    }

    private fun restoreLastFix() {
        val lat = readMeta("last_lat")?.toDoubleOrNull() ?: return
        val lng = readMeta("last_lng")?.toDoubleOrNull() ?: return
        val loc = Location("restored")
        loc.latitude = lat
        loc.longitude = lng
        readMeta("last_acc")?.toFloatOrNull()?.let { loc.accuracy = it }
        loc.time = readMetaLong("last_fix_ms").takeIf { it > 0 } ?: System.currentTimeMillis()
        if (isUsable(loc)) {
            lastLocation = loc
        }
    }

    private fun insertEvent(type: String) {
        db?.execSQL(
            "INSERT INTO local_tracking_events(shift_id, event_type, occurred_at_utc, sync_status) VALUES (?, ?, ?, 'Pending')",
            arrayOf<Any?>(shiftId, type, utcIso(System.currentTimeMillis()))
        )
    }

    private fun officialMetaKey() = "last_official_ms_$shiftId"

    private fun utcIso(epochMs: Long): String {
        val fmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US)
        fmt.timeZone = TimeZone.getTimeZone("UTC")
        return fmt.format(Date(epochMs))
    }

    private fun writeMeta(key: String, value: String) {
        db?.execSQL("INSERT OR REPLACE INTO tracking_meta(key, value) VALUES (?, ?)", arrayOf(key, value))
    }

    private fun readMetaInt(key: String) = readMeta(key)?.toIntOrNull() ?: 0

    private fun readMetaLong(key: String) = readMeta(key)?.toLongOrNull() ?: 0L

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
                db?.execSQL(
                    "UPDATE local_location_points SET sync_status = 'Synced' WHERE shift_id = ? AND device_sequence IN ($placeholders)",
                    buildList<Any?> {
                        add(sid)
                        seqs.forEach { add(it) }
                    }.toTypedArray()
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
        const val EXTRA_OFFICIAL_INTERVAL = "officialIntervalMs"
        const val EXTRA_STATIONARY = "stationaryIntervalMs"
        const val EXTRA_DISTANCE = "minDistance"
        const val EXTRA_API_BASE = "apiBase"
        const val EXTRA_TOKEN = "token"
        const val EXTRA_STARTED_AT = "startedAtUtcMs"
        const val CHANNEL_ID = "sales_shift"
        const val NOTIFICATION_ID = 4101
        @Volatile var running: Boolean = false
    }
}


