Option Explicit
' Dernek Takip — gizli başlat (CMD görev çubuğunda görünmez).
Dim sh, fso, kok, exe, url, kod
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
kok = fso.GetParentFolderName(WScript.ScriptFullName)
If Right(kok, 1) <> "\" Then kok = kok & "\"
exe = kok & "bin\Debug\net10.0\DernekTakip.exe"
If Not fso.FileExists(exe) Then exe = kok & "bin\Release\net10.0\DernekTakip.exe"
url = "http://127.0.0.1:5130/"

On Error Resume Next
sh.Run "cmd /c taskkill /IM DernekTakip.exe /F >nul 2>&1", 0, True
WScript.Sleep 400
On Error GoTo 0

kod = sh.Run("cmd /c dotnet build """ & kok & "DernekTakip.csproj"" --nologo -v q", 0, True)
If kod <> 0 Or Not fso.FileExists(exe) Then
  MsgBox "Derleme başarısız.", vbCritical, "Dernek Takip"
  WScript.Quit 1
End If

sh.Environment("Process")("ASPNETCORE_ENVIRONMENT") = "Development"
sh.Environment("Process")("ASPNETCORE_URLS") = "http://127.0.0.1:5130"
sh.Environment("Process")("ASPNETCORE_CONTENTROOT") = Left(kok, Len(kok) - 1)
sh.CurrentDirectory = Left(kok, Len(kok) - 1)
' 0 = gizli pencere (WinExe)
sh.Run """" & exe & """", 0, False
WScript.Sleep 1500
sh.Run url, 1, False
