@echo off

del .\bin\* /s /q
for /d %%p in (.\bin\*) do rd "%%p" /s /q

for /f "tokens=*" %%G in ('dir /b /ad /s .vs') do rmdir /s /q "%%G"
rmdir .\.vs

rem <<< Main assemblies cleaning >>>

rmdir .\BSLib.LMKit\bin /s /q
rmdir .\BSLib.LMKit\obj /s /q

rmdir .\ZAssistant\bin /s /q
rmdir .\ZAssistant\obj /s /q
