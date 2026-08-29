@echo off
echo Publishing Delegate Web Application to Desktop...
cd /d "D:\Project\SalesHaider\BE_DelegateWebApplication\BE_DelegateWebApplication"
dotnet publish -c Release -o "%USERPROFILE%\Desktop\DelegateApi"
echo Publish complete! You can find the files in a folder named DelegateApi on your Desktop.
pause
