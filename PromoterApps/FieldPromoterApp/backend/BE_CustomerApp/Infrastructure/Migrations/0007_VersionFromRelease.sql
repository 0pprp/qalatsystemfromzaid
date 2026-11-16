-- App versions are no longer dashboard settings: the published APK's release.json decides the (mandatory) version.
DELETE FROM agents.AppSettings WHERE [Key] IN ('MinSupportedVersion', 'LatestVersion', 'ApkDownloadUrl');
