

call "cmd /c start .\SharedServer\bin\Debug\SharedServer.exe servidor1"
call "cmd /c start .\SharedServer\bin\Debug\SharedServer.exe servidor2"
call "cmd /c start .\SharedServer\bin\Debug\SharedServer.exe servidor3"

call "cmd /c start .\Client\bin\Debug\Client.exe servidor1"
call "cmd /c start .\Client\bin\Debug\Client.exe servidor2"
call "cmd /c start .\Client\bin\Debug\Client.exe servidor3"

echo start
pause