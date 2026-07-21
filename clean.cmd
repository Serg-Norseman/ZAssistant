@echo off

del .\bin\* /s /q
for /d %%p in (.\bin\*) do rd "%%p" /s /q

del .\pub\* /s /q
for /d %%p in (.\pub\*) do rd "%%p" /s /q

for /f "tokens=*" %%G in ('dir /b /ad /s .vs') do rmdir /s /q "%%G"
rmdir .\.vs

rem <<< Main assemblies cleaning >>>

rmdir .\ZLMKit\bin /s /q
rmdir .\ZLMKit\obj /s /q

rmdir .\ZLMKit.Tests\bin /s /q
rmdir .\ZLMKit.Tests\obj /s /q

rmdir .\ZAssistant\bin /s /q
rmdir .\ZAssistant\obj /s /q

rmdir .\ZLMTools\bin /s /q
rmdir .\ZLMTools\obj /s /q
