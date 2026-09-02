import 'dart:io';

import 'package:sales_employee_application/services/geo_fix.dart';

Future<GeoFix?> readLocation() async {
  if (!Platform.isWindows) return null;
  try {
    final result = await Process.run(
      'powershell',
      [
        '-NoProfile',
        '-Command',
        r'''
Add-Type -AssemblyName System.Device
$w = New-Object System.Device.Location.GeoCoordinateWatcher
$w.Start()
$n = 0
while ($w.Status -ne 'Ready' -and $n -lt 15) { Start-Sleep -Milliseconds 200; $n++ }
$c = $w.Position.Location
$w.Dispose()
if ($c.IsUnknown) { '' } else { '{0}|{1}|{2}' -f $c.Latitude, $c.Longitude, $c.HorizontalAccuracy }
''',
      ],
    );
    final text = (result.stdout ?? '').toString().trim();
    if (text.isEmpty) return null;
    final parts = text.split('|');
    if (parts.length < 2) return null;
    final lat = double.tryParse(parts[0]);
    final lng = double.tryParse(parts[1]);
    if (lat == null || lng == null) return null;
    return GeoFix(lat, lng, parts.length > 2 ? double.tryParse(parts[2]) : null);
  } catch (_) {
    return null;
  }
}
