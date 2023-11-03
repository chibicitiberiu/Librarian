rem Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef

mkdir DB\Migrations
dotnet ef migrations add %1 --context PostgresDatabaseContext --output-dir DB\Migrations

rem TODO: add for other DB types