# Azure Function with SQL Database

## Preparation

After provision _SQL Database_, do following:

- Update firewalls in SQL Server settings
    - Add client IP address to access from you local PC
    - Allow Azure services and resources to access this server

- Add `managed identity` to __Active Directory Admin__ in SQL Server Settings

Create a table and add some data. See [sql.txt](sql.txt) for sample sql query.

