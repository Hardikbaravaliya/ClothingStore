# ClothingStore

Kids clothing (Girls Wear & Boys Wear) mate **multi-tenant SaaS** platform.

- **Manager** – ASP.NET Core MVC (Admin)
- **Catalog** – ASP.NET Core MVC (Customer website, data Api thi)
- **Api** – ASP.NET Core Web API (JWT)
- **.NET 9**, **SQL Server**, **EF Core 9**, **Bootstrap 5**

Puro plan: [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md)

## Local setup

**Jaruri:** .NET 9 SDK (`global.json` thi pin), SQL Server LocalDB (Visual Studio sathe aave chhe).

```bash
dotnet tool restore
dotnet build
```

Database alag thi banavvo nathi padto: **Manager** Development ma start thay tyare migrations apply kare
ane demo data seed kare (`Seed` section, `src/ClothingStore.Manager/appsettings.Development.json`).

| App | URL |
|---|---|
| Manager | https://localhost:5002 |
| Api (Swagger) | https://localhost:5003/swagger |
| Catalog – shop1 | http://shop1.localhost:5101 |
| Catalog – shop2 | http://shop2.localhost:5101 |

Pehla Manager, pachhi Api, pachhi Catalog chalavo (Visual Studio ma "Multiple startup projects" pan chale).

### Demo login (fakt local)
Seed config mujab:
- **Super Admin:** Store code khali, `Seed:SuperAdmin` nu email/password
- **Store admin:** Store code `shop1` (ke `shop2`), email `admin@shop1.local`, password `Seed:DemoAdminPassword`

### Navi migration
```bash
dotnet ef migrations add <Name> -p src/ClothingStore.Infrastructure -s src/ClothingStore.Manager -o Data/Migrations
```

### Tests
Tenant isolation integration tests LocalDB par navo temporary database banave ane pachhi delete kare.
```bash
dotnet test
```

### Secrets
Razorpay/Shiprocket keys jeva secrets `dotnet user-secrets` thi set karva, git ma commit na karva.
