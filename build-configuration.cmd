@echo off

%FAKE% %NYX% "target=clean" -st
%FAKE% %NYX% "target=RestoreNugetPackages" -st
%FAKE% %NYX% "target=RestoreBowerPackages" -st

IF NOT [%1]==[] (set RELEASE_NUGETKEY="%1")
IF NOT [%2]==[] (set RELEASE_TARGETSOURCE="%2")

SET RELEASE_NOTES=RELEASE_NOTES.md
SET SUMMARY="Elders.Cronus.Transport.RabbitMQ"
SET DESCRIPTION="Elders.Cronus.Transport.RabbitMQ"

%FAKE% %NYX% appName=Elders.Cronus.Transport.RabbitMQ appReleaseNotes=%RELEASE_NOTES% appSummary=%SUMMARY% appDescription=%DESCRIPTION% nugetkey=%RELEASE_NUGETKEY% nugetPackageName=Cronus.Transport.RabbitMQ
IF errorlevel 1 (echo Faild with exit code %errorlevel% & exit /b %errorlevel%)
