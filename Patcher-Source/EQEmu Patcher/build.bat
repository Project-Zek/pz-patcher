echo building....
set VERSION=1.0.0.0
set BUILD_CONFIGURATION=Release
set SERVER_NAME=Project-Zek
set FILE_NAME=Project-Zek
set FILELIST_URL=https://raw.githubusercontent.com/Project-Zek/eqemupatcher/refs/heads/master/takp/filelist_takp.yml
set PATCHER_URL=https://github.com/Project-Zek/eqemupatcher/releases/latest/download
set KNOWN_HASHES=
set KNOWN_HASH_VERSION=Rain_Of_Fear_2
"C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\msbuild.exe" /m /p:Configuration=%BUILD_CONFIGURATION% /p:VERSION=%VERSION% /p:SERVER_NAME="%SERVER_NAME%" /p:FILELIST_URL="%FILELIST_URL%" /p:PATCHER_URL="%PATCHER_URL%" /p:FILE_NAME="%FILE_NAME%" /p:KNOWN_HASHES="%KNOWN_HASHES%" /p:KNOWN_HASH_VERSION="%KNOWN_HASH_VERSION%"
