# Overview
This is a simple project to demonstrate the use of Managed Identity and AISearch.

# Instructions

Deploy infra using the following commands:
```bash
azd auth login
azd up
```

Copy sample.env to .env.
Fill in the following
- COSMOSDB_ENDPOINT=
- COSMOSDB_DBNAME=

Run the project using the following command(s):

```
dotnet run --project ./Project.csproj
```