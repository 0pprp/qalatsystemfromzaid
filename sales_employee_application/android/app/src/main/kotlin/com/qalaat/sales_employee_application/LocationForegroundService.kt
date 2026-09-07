package com.qalaat.sales_employee_application

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.ContentValues
import android.content.Intent
import android.content.pm.ServiceInfo
import android.database.sqlite.SQLiteDatabase
import android.location.Location
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.os.PowerManager
import android.util.Log
import androidx.core.app.NotificationCompat
import com.google.android.gms.location.LocationAvailability
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.atomic.AtomicBoolean

class LocationForegroundService : Service() {
    private val client by lazy { LocationServices.getFusedLocationProviderClient(this) }
    private val handler = Handler(Looper.getMainLooper())
    private val flushing = AtomicBoolean(false)
    private val liveUploading = AtomicBoolean(false)
    private var lastLiveUploadMs: Long = 0
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
    private var wakeLock: PowerManager.WakeLock? = null

    private val cutoffStop = Runnable { stopCollecting(flush = true) }
    private val officialTick = object : Runnable {
        override fun run() {
            persistDueOfficialPoints()
            requestFreshFix()
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
                stopCollecting(flush = true)
                return
            }
            val loc = result.lastLocation ?: return
            log(
                "LOCATION_RECEIVED lat=${loc.latitude} lng=${loc.longitude} accuracy=${
                    if (loc.hasAccuracy()) loc.accuracy else -1
                }",
            )
            if (!isUsable(loc)) return
            lastLocation = loc
            persistLastFix(loc)
            maybeUploadLive(loc)
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
        try {
            startForegroundNotification()
        } catch (e: Exception) {
            log("DATABASE_ERROR startForeground ${e.javaClass.simpleName}: ${e.message}")
            stopSelf()
            return START_NOT_STICKY
        }
        val dbOk = try {
            openDb()
        } catch (e: Exception) {
            log("DATABASE_ERROR openDb ${e.javaClass.simpleName}: ${e.message}")
            false
        }
        if (!dbOk) {
            stopForegroundCompat()
            stopSelf()
            return START_NOT_STICKY
        }
        try {
            if (intent?.action == ACTION_STOP || intent?.action == ACTION_FLUSH_STOP) {
                running = false
                handler.removeCallbacks(cutoffStop)
                handler.removeCallbacks(officialTick)
                handler.removeCallbacks(syncFlush)
                    persistDueOfficialPoints()
                Thread {
                    uploadLiveLocation(lastLocation)
                    flushPendingToServer()
                    handler.post {
                        stopUpdates()
                        stopForegroundCompat()
                        stopSelf()
                        log("SHIFT_ENDED")
                        log("SERVICE_STOPPED")
                    }
                }.start()
                return START_NOT_STICKY
            }
            loadSession(intent)
            if (cutoffAtUtcMs <= 0L) {
                cutoffAtUtcMs = defaultCutoffUtcMs()
            }
            lastOfficialAtMs = readMetaLong(officialMetaKey())
            restoreLastFix()
            persistSession()
            if (shiftId <= 0 || System.currentTimeMillis() >= cutoffAtUtcMs) {
                log("SERVICE_STOPPED reason=no-active-shift shiftId=$shiftId")
                stopCollecting(flush = false)
                return START_NOT_STICKY
            }
            running = true
            log("SERVICE_STARTED shiftId=$shiftId officialIntervalMs=$officialIntervalMs")
            log("SHIFT_STARTED shiftId=$shiftId startedAtUtcMs=$shiftStartedAtUtcMs cutoffAtUtcMs=$cutoffAtUtcMs")
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
        } catch (e: Exception) {
            log("DATABASE_ERROR onStartCommand ${e.javaClass.simpleName}: ${e.message}")
            running = false
            handler.removeCallbacks(cutoffStop)
            handler.removeCallbacks(officialTick)
            handler.removeCallbacks(syncFlush)
            stopUpdates()
            stopForegroundCompat()
            stopSelf()
            return START_NOT_STICKY
        }
    }

    override fun onTaskRemoved(rootIntent: Intent?) {
        log("TASK_REMOVED keep running")
    }

    override fun onDestroy() {
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(officialTick)
        handler.removeCallbacks(syncFlush)
        stopUpdates()
        db?.close()
        db = null
        running = false
        releaseWakeLock()
        log("SERVICE_STOPPED")
        super.onDestroy()
    }

    private fun stopCollecting(flush: Boolean) {
        running = false
        handler.removeCallbacks(cutoffStop)
        handler.removeCallbacks(officialTick)
        handler.removeCallbacks(syncFlush)
        if (flush) {
            persistDueOfficialPoints()
            Thread {
                uploadLiveLocation(lastLocation)
                flushPendingToServer()
                handler.post {
                    stopUpdates()
                    stopForegroundCompat()
                    stopSelf()
                    log("SHIFT_ENDED")
                    log("SERVICE_STOPPED")
                }
            }.start()
            return
        }
        stopUpdates()
        stopForegroundCompat()
        stopSelf()
        log("SERVICE_STOPPED")
    }

    private fun stopForegroundCompat() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            stopForeground(STOP_FOREGROUND_REMOVE)
        } else {
            @Suppress("DEPRECATION")
            stopForeground(true)
        }
    }

    private fun startForegroundNotification() {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            manager.createNotificationChannel(
                NotificationChannel(CHANNEL_ID, "الدوام", NotificationManager.IMPORTANCE_LOW),
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
            .setMinUpdateIntervalMillis(10_000)
            .setMinUpdateDistanceMeters(0f)
            .setWaitForAccurateLocation(false)
            .build()
        try {
            client.requestLocationUpdates(request, callback, Looper.getMainLooper())
        } catch (e: SecurityException) {
            log("LIVE_UPLOAD_FAILED status=permission error=${e.message}")
            stopSelf()
        }
    }

    private fun seedLastLocation() {
        requestFreshFix()
        try {
            client.lastLocation.addOnSuccessListener { loc ->
                if (!running) return@addOnSuccessListener
                if (loc != null && isUsable(loc)) {
                    lastLocation = loc
                    persistLastFix(loc)
                    maybeUploadLive(loc)
                    persistDueOfficialPoints()
                }
            }
        } catch (_: SecurityException) {
        }
    }

    private fun requestFreshFix() {
        try {
            client.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, com.google.android.gms.tasks.CancellationTokenSource().token)
                .addOnSuccessListener { loc ->
                    if (!running) return@addOnSuccessListener
                    if (loc != null && isUsable(loc)) {
                        log(
                            "LOCATION_RECEIVED lat=${loc.latitude} lng=${loc.longitude} accuracy=${
                                if (loc.hasAccuracy()) loc.accuracy else -1
                            }",
                        )
                        lastLocation = loc
                        persistLastFix(loc)
                        maybeUploadLive(loc)
                        persistDueOfficialPoints()
                    }
                }
        } catch (_: Exception) {
        }
    }

    private fun stopUpdates() {
        try {
            client.removeLocationUpdates(callback)
        } catch (_: Exception) {
        }
    }

    private fun openDb(): Boolean {
        if (db?.isOpen == true) return true
        return try {
            val file = getDatabasePath(DB_NAME)
            file.parentFile?.mkdirs()
            db = SQLiteDatabase.openOrCreateDatabase(file, null)
            db?.enableWriteAheadLogging()
            execPragma("PRAGMA journal_mode=WAL")
            execPragma("PRAGMA busy_timeout=5000")
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
            )""",
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
            )""",
            )
            true
        } catch (e: Exception) {
            log("DATABASE_ERROR openDb ${e.javaClass.simpleName}: ${e.message}")
            try {
                db?.close()
            } catch (_: Exception) {
            }
            db = null
            false
        }
    }

    private fun execPragma(sql: String) {
        db?.rawQuery(sql, null)?.close()
    }

    private fun loadSession(intent: Intent?) {
        if (intent != null && intent.hasExtra(EXTRA_SHIFT_ID)) {
            shiftId = intent.getIntExtra(EXTRA_SHIFT_ID, 0)
            cutoffAtUtcMs = intent.getLongExtra(EXTRA_CUTOFF, 0L)
            shiftStartedAtUtcMs = intent.getLongExtra(EXTRA_STARTED_AT, 0L)
            intervalMs = intent.getLongExtra(EXTRA_INTERVAL, 30_000).coerceAtLeast(5_000)
            officialIntervalMs = intent.getLongExtra(EXTRA_OFFICIAL_INTERVAL, 600_000).coerceAtLeast(5_000)
            apiBase = normalizeApiBase(intent.getStringExtra(EXTRA_API_BASE) ?: "")
            token = intent.getStringExtra(EXTRA_TOKEN) ?: ""
        } else {
            shiftId = readMetaInt("active_shift_id")
            cutoffAtUtcMs = readMetaLong("cutoff_at_utc_ms")
            shiftStartedAtUtcMs = readMetaLong("shift_started_at_utc_ms")
            intervalMs = readMetaLong("interval_ms").takeIf { it > 0 } ?: 30_000
            officialIntervalMs = readMetaLong("official_interval_ms").takeIf { it > 0 } ?: 600_000
            apiBase = normalizeApiBase(readMeta("api_base") ?: "")
            token = readMeta("api_token") ?: ""
        }
    }

    private fun persistDueOfficialPoints() {
        if (shiftId <= 0) return
        val loc = lastLocation ?: return
        try {
        val now = System.currentTimeMillis()
        if (now >= cutoffAtUtcMs) {
            if (running) stopCollecting(flush = true)
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
            if (insertOfficialPoint(loc, cursor)) {
                inserted = true
            }
            latest = cursor
            cursor += officialIntervalMs
        }
        if (latest > lastOfficialAtMs) {
            lastOfficialAtMs = latest
            writeMeta(officialMetaKey(), latest.toString())
        } else if (inserted) {
            lastOfficialAtMs = latest
            writeMeta(officialMetaKey(), latest.toString())
        }
        if (inserted) {
            Thread { flushPendingToServer() }.start()
        }
        } catch (e: Exception) {
            log("DATABASE_ERROR persistDue ${e.javaClass.simpleName}: ${e.message}")
        }
    }

    private fun scheduleNextOfficialTick() {
        handler.removeCallbacks(officialTick)
        if (!running) return
        val now = System.currentTimeMillis()
        val last = if (lastOfficialAtMs > 0L) floorSlotUtcMs(lastOfficialAtMs) else floorSlotUtcMs(now)
        val next = last + officialIntervalMs
        val delay = (next - now).coerceIn(1_000L, officialIntervalMs.coerceAtLeast(5_000L))
        log("NEXT_CAPTURE delayMs=$delay nextAtMs=$next intervalMs=$officialIntervalMs")
        handler.postDelayed(officialTick, delay)
    }

    private fun floorSlotUtcMs(epochMs: Long): Long {
        if (officialIntervalMs != 600_000L) {
            return (epochMs / officialIntervalMs) * officialIntervalMs
        }
        val cal = java.util.Calendar.getInstance(TimeZone.getTimeZone("Asia/Baghdad"))
        cal.timeInMillis = epochMs
        val minute = cal.get(java.util.Calendar.MINUTE)
        cal.set(java.util.Calendar.MINUTE, (minute / 10) * 10)
        cal.set(java.util.Calendar.SECOND, 0)
        cal.set(java.util.Calendar.MILLISECOND, 0)
        return cal.timeInMillis
    }

    private fun slotSequence(slotUtcMs: Long): Long = slotUtcMs / officialIntervalMs

    private fun insertOfficialPoint(loc: Location, slotUtcMs: Long): Boolean {
        val seq = slotSequence(slotUtcMs)
        val captured = utcIso(slotUtcMs)
        val actual = utcIso(loc.time.takeIf { it > 0 } ?: System.currentTimeMillis())
        val values = ContentValues()
        values.put("shift_id", shiftId)
        values.put("latitude", loc.latitude)
        values.put("longitude", loc.longitude)
        if (loc.hasAccuracy()) values.put("accuracy", loc.accuracy.toDouble())
        if (loc.hasSpeed()) values.put("speed", loc.speed.toDouble())
        if (loc.hasBearing()) values.put("heading", loc.bearing.toDouble())
        values.put("captured_at_utc", captured)
        values.put("device_sequence", seq)
        values.put("sync_status", "Pending")
        values.put("retry_count", 0)
        val id = db?.insertWithOnConflict(
            "local_location_points",
            null,
            values,
            SQLiteDatabase.CONFLICT_IGNORE,
        ) ?: -1L
        writeMeta("actual_captured_${shiftId}_$seq", actual)
        if (id > 0L) {
            log("ROUTE_POINT_SAVED localId=$id seq=$seq captured=$captured lat=${loc.latitude} lng=${loc.longitude}")
            return true
        }
        return false
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
        writeMeta("shift_active", if (shiftId > 0) "1" else "0")
    }

    private fun persistLastFix(loc: Location) {
        writeMeta("last_lat", loc.latitude.toString())
        writeMeta("last_lng", loc.longitude.toString())
        if (loc.hasAccuracy()) writeMeta("last_acc", loc.accuracy.toString())
        writeMeta("last_fix_ms", (loc.time.takeIf { it > 0 } ?: System.currentTimeMillis()).toString())
    }

    private fun maybeUploadLive(loc: Location) {
        if (!running || shiftId <= 0) return
        val now = System.currentTimeMillis()
        if (now - lastLiveUploadMs < 10_000L) return
        lastLiveUploadMs = now
        Thread { uploadLiveLocation(loc) }.start()
    }

    private fun uploadLiveLocation(loc: Location?) {
        val point = loc ?: lastLocation ?: return
        if (!liveUploading.compareAndSet(false, true)) return
        try {
            val base = normalizeApiBase(if (apiBase.isNotEmpty()) apiBase else readMeta("api_base") ?: "")
            val auth = (if (token.isNotEmpty()) token else readMeta("api_token") ?: "").trim()
            val sid = if (shiftId > 0) shiftId else readMetaInt("active_shift_id")
            if (base.isEmpty() || auth.isEmpty() || sid <= 0) {
                log("LIVE_UPLOAD_FAILED status=missing-config")
                return
            }
            val body = JSONObject()
            body.put("shiftId", sid)
            body.put("latitude", point.latitude)
            body.put("longitude", point.longitude)
            if (point.hasAccuracy()) body.put("accuracy", point.accuracy.toDouble())
            if (point.hasSpeed()) body.put("speed", point.speed.toDouble())
            if (point.hasBearing()) body.put("heading", point.bearing.toDouble())
            body.put("capturedAtUtc", utcIso(point.time.takeIf { it > 0 } ?: System.currentTimeMillis()))
            val url = java.net.URL(base.trimEnd('/') + "/sales/location/live")
            log("LIVE_UPLOAD_START url=$url lat=${point.latitude} lng=${point.longitude}")
            val conn = url.openConnection() as java.net.HttpURLConnection
            try {
                conn.requestMethod = "POST"
                conn.connectTimeout = 10000
                conn.readTimeout = 15000
                conn.useCaches = false
                conn.setRequestProperty("Content-Type", "application/json")
                conn.setRequestProperty("Accept", "application/json")
                conn.setRequestProperty("Authorization", "Bearer $auth")
                conn.doOutput = true
                val payload = body.toString().toByteArray(Charsets.UTF_8)
                conn.setFixedLengthStreamingMode(payload.size)
                conn.outputStream.use { it.write(payload) }
                val code = conn.responseCode
                val text = try {
                    val stream = if (code in 200..299) conn.inputStream else conn.errorStream
                    stream?.bufferedReader()?.readText() ?: ""
                } catch (_: Exception) {
                    ""
                }
                if (code in 200..299) {
                    log("LIVE_UPLOAD_SUCCESS status=$code")
                } else {
                    log("LIVE_UPLOAD_FAILED status=$code error=${text.take(240)}")
                }
            } finally {
                conn.disconnect()
            }
        } catch (e: Exception) {
            log("LIVE_UPLOAD_FAILED status=exception error=${e.javaClass.simpleName}:${e.message}")
        } finally {
            liveUploading.set(false)
        }
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
            arrayOf<Any?>(shiftId, type, utcIso(System.currentTimeMillis())),
        )
    }

    private fun officialMetaKey() = "last_official_ms_$shiftId"

    private fun utcIso(epochMs: Long): String {
        val fmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US)
        fmt.timeZone = TimeZone.getTimeZone("UTC")
        return fmt.format(Date(epochMs))
    }

    private fun writeMeta(key: String, value: String) {
        try {
            db?.execSQL("INSERT OR REPLACE INTO tracking_meta(key, value) VALUES (?, ?)", arrayOf(key, value))
        } catch (e: Exception) {
            log("DATABASE_ERROR writeMeta key=$key ${e.javaClass.simpleName}: ${e.message}")
        }
    }

    private fun readMetaInt(key: String) = readMeta(key)?.toIntOrNull() ?: 0

    private fun readMetaLong(key: String) = readMeta(key)?.toLongOrNull() ?: 0L

    private fun readMeta(key: String): String? {
        return try {
            val cursor = db?.rawQuery("SELECT value FROM tracking_meta WHERE key = ?", arrayOf(key))
            val value = if (cursor != null && cursor.moveToFirst()) cursor.getString(0) else null
            cursor?.close()
            value
        } catch (e: Exception) {
            log("DATABASE_ERROR readMeta key=$key ${e.javaClass.simpleName}: ${e.message}")
            null
        }
    }

    private fun flushPendingToServer() {
        if (!flushing.compareAndSet(false, true)) return
        acquireWakeLock()
        try {
            val base = normalizeApiBase(if (apiBase.isNotEmpty()) apiBase else readMeta("api_base") ?: "")
            val auth = (if (token.isNotEmpty()) token else readMeta("api_token") ?: "").trim()
            val sid = if (shiftId > 0) shiftId else readMetaInt("active_shift_id")
            if (base.isEmpty() || auth.isEmpty() || sid <= 0 || db == null) {
                log("ROUTE_UPLOAD_FAILED status=missing-config baseEmpty=${base.isEmpty()} tokenEmpty=${auth.isEmpty()} shiftId=$sid")
                return
            }
            val cursor = db?.rawQuery(
                "SELECT id, latitude, longitude, accuracy, speed, heading, captured_at_utc, device_sequence FROM local_location_points WHERE shift_id = ? AND sync_status IN ('Pending','Failed','Syncing') ORDER BY device_sequence ASC LIMIT 150",
                arrayOf(sid.toString()),
            ) ?: return
            val points = JSONArray()
            val seqs = mutableListOf<Long>()
            while (cursor.moveToNext()) {
                val seq = cursor.getLong(7)
                seqs.add(seq)
                val obj = JSONObject()
                obj.put("latitude", cursor.getDouble(1))
                obj.put("longitude", cursor.getDouble(2))
                if (!cursor.isNull(3)) obj.put("accuracy", cursor.getDouble(3))
                if (!cursor.isNull(4)) obj.put("speed", cursor.getDouble(4))
                if (!cursor.isNull(5)) obj.put("heading", cursor.getDouble(5))
                obj.put("capturedAtUtc", cursor.getString(6))
                obj.put("deviceSequence", seq)
                obj.put("isOfficial", true)
                points.put(obj)
            }
            cursor.close()
            if (points.length() == 0) return
            val body = JSONObject()
            body.put("shiftId", sid)
            body.put("points", points)
            val url = java.net.URL(base.trimEnd('/') + "/sales/location/batch")
            log("ROUTE_UPLOAD_START count=${points.length()} url=$url")
            val conn = url.openConnection() as java.net.HttpURLConnection
            try {
                conn.requestMethod = "POST"
                conn.connectTimeout = 15000
                conn.readTimeout = 20000
                conn.useCaches = false
                conn.instanceFollowRedirects = true
                conn.setRequestProperty("Content-Type", "application/json")
                conn.setRequestProperty("Accept", "application/json")
                conn.setRequestProperty("Authorization", "Bearer $auth")
                conn.doOutput = true
                val payload = body.toString().toByteArray(Charsets.UTF_8)
                conn.setFixedLengthStreamingMode(payload.size)
                conn.outputStream.use { it.write(payload) }
                val code = conn.responseCode
                val text = try {
                    val stream = if (code in 200..299) conn.inputStream else conn.errorStream
                    stream?.bufferedReader()?.readText() ?: ""
                } catch (_: Exception) {
                    ""
                }
                if (code in 200..299) {
                    val parsed = parseBatchResult(text)
                    val keepPending = parsed != null && parsed.accepted == 0 && parsed.duplicates == 0 && parsed.rejected > 0
                    if (keepPending) {
                        markStatus(sid, seqs, "Failed")
                        log("ROUTE_UPLOAD_FAILED status=$code error=all-rejected rejected=${parsed.rejected}")
                    } else {
                        markStatus(sid, seqs, "Synced")
                        log("ROUTE_UPLOAD_SUCCESS accepted=${parsed?.accepted ?: "?"} duplicates=${parsed?.duplicates ?: "?"} rejected=${parsed?.rejected ?: "?"}")
                    }
                } else {
                    markStatus(sid, seqs, "Failed", incrementRetry = true)
                    log("ROUTE_UPLOAD_FAILED status=$code error=${text.take(240)}")
                }
            } finally {
                conn.disconnect()
            }
        } catch (e: Exception) {
            log("ROUTE_UPLOAD_FAILED status=exception error=${e.javaClass.simpleName}:${e.message}")
        } finally {
            flushing.set(false)
            releaseWakeLock()
        }
    }

    private fun markStatus(shiftId: Int, seqs: List<Long>, status: String, incrementRetry: Boolean = false) {
        if (seqs.isEmpty()) return
        val placeholders = seqs.joinToString(",") { "?" }
        val sql = if (incrementRetry) {
            "UPDATE local_location_points SET sync_status = ?, retry_count = retry_count + 1 WHERE shift_id = ? AND device_sequence IN ($placeholders)"
        } else {
            "UPDATE local_location_points SET sync_status = ? WHERE shift_id = ? AND device_sequence IN ($placeholders)"
        }
        db?.execSQL(
            sql,
            buildList<Any?> {
                add(status)
                add(shiftId)
                seqs.forEach { add(it) }
            }.toTypedArray(),
        )
    }

    private data class BatchResult(val accepted: Int, val duplicates: Int, val rejected: Int)

    private fun parseBatchResult(text: String): BatchResult? {
        if (text.isBlank()) return null
        return try {
            val obj = JSONObject(text)
            BatchResult(
                accepted = obj.optInt("accepted", obj.optInt("Accepted")),
                duplicates = obj.optInt("duplicates", obj.optInt("Duplicates")),
                rejected = obj.optInt("rejected", obj.optInt("Rejected")),
            )
        } catch (_: Exception) {
            null
        }
    }

    private fun acquireWakeLock() {
        if (wakeLock?.isHeld == true) return
        val pm = getSystemService(POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "QalaaTracking:flush").also {
            it.setReferenceCounted(false)
            it.acquire(30_000)
        }
    }

    private fun releaseWakeLock() {
        try {
            if (wakeLock?.isHeld == true) wakeLock?.release()
        } catch (_: Exception) {
        }
    }

    companion object {
        const val TAG = "QalaaTracking"
        const val DB_NAME = "sales_tracking.db"
        const val ACTION_STOP = "com.qalaat.sales_employee_application.STOP_TRACKING"
        const val ACTION_FLUSH_STOP = "com.qalaat.sales_employee_application.FLUSH_STOP_TRACKING"
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

        fun log(message: String) {
            Log.i(TAG, message)
        }

        fun normalizeApiBase(raw: String): String {
            var value = raw.trim().replace('\\', '/')
            if (value.isEmpty()) return value
            while (value.contains("/api/api")) {
                value = value.replace("/api/api", "/api")
            }
            if (!value.endsWith("/")) value = "$value/"
            if (value.endsWith(":8080/")) value = "${value}api/"
            return value
        }
    }
}
