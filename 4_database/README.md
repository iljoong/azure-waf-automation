# Azure Function with SQL Database

## DB Preparation

After provision _SQL Database_, do following:

- Update firewalls in SQL Server settings
    - Add client IP address to access from you local PC
    - Allow Azure services and resources to access this server

- To access SQL database from Azure Function using AAD Token authentication, you need to add your `managed identity` which assigned to Azure Function in __SQL Server Settings/Active Directory Admin_.

> `managed identity` is added __Active Directory Admin__ in SQL Server Settings by Terraform.

Create a table for BlockIPs

```
CREATE TABLE WAFBLOCKIP (
    IP_ADDR NVARCHAR(15) NOT NULL,
    TTL INT NOT NULL,
    CONSTRAINT PK_WAFBLOCKIP PRIMARY KEY(IP_ADDR)
)
```

## Sample SQL

- Add BlockIP

```
IF EXISTS(select IP_ADDR from WAFBLOCKIP where IP_ADDR = '{blockIP}')
BEGIN
    update WAFBLOCKIP SET TTL = 15 where IP_ADDR = '{blockIP}'
END
ELSE
BEGIN
    insert into WAFBLOCKIP (IP_ADDR, TTL) values('{blockIP}', 15)
END
```

- Manage TTL

Update TTL, get list of block IP with TTL <=> 0 and remove block IP with TTL = -5 (think of as retry of removing IPs)

```
UPDATE WAFBLOCKIP SET TTL = TTL - 1

select IP_ADDR from WAFBLOCKIP where TTL <= 0

delete from WAFBLOCKIP where TTL <=-5
```


