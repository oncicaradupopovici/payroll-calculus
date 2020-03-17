param(
  [Parameter(Mandatory=$true)][String]$DbName,
  [Parameter(Mandatory=$true)][String]$DbScript
)

$detach_db_sql = @"
IF (SELECT COUNT(*) FROM sys.databases WHERE name = '$DbName') > 0
  EXEC sp_detach_db @dbname = N'$DbName'
"@

$detach_db_sql | Out-File "detachdb.sql"
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -i "detachdb.sql"
Remove-Item "detachdb.sql"

if (Test-Path "$PSScriptRoot\$DbName.mdf") { Remove-Item "$PSScriptRoot\$DbName.mdf" }
if (Test-Path "$PSScriptRoot\$DbName.ldf") { Remove-Item "$PSScriptRoot\$DbName.ldf" }

$create_db_sql = @"
CREATE DATABASE $DbName
ON (
  NAME = ${DbName}_dat,
  FILENAME = '$PSScriptRoot\$DbName.mdf'
)
LOG ON (
  NAME = ${DbName}_log,
  FILENAME = '$PSScriptRoot\$DbName.ldf'
)
"@

$create_db_sql | Out-File "createdb.sql"
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -i "createdb.sql"
Remove-Item "createdb.sql"

sqlcmd -S "(LocalDB)\MSSQLLocalDB" -i "$DbScript"

$detach_db_sql | Out-File "detachdb.sql"
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -i "detachdb.sql"
Remove-Item "detachdb.sql"