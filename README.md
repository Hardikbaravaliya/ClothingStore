# ClothingStore

Kids clothing (Girls Wear & Boys Wear) mate **multi-tenant SaaS** platform.

- **Manager** – ASP.NET Core MVC (Admin)
- **Catalog** – ASP.NET Core MVC (Customer website, data Api thi)
- **Api** – ASP.NET Core Web API (customer JWT)
- **Contracts** – Api na request/response models (Api, Catalog, future mobile app)
- **.NET 9**, **SQL Server**, **EF Core 9**, **Bootstrap 5**

Puro plan: [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md)

## Local setup

**Jaruri:** .NET 9 SDK (`global.json` thi pin), SQL Server LocalDB (Visual Studio sathe aave chhe).

```bash
dotnet tool restore
dotnet build
```

### Ek var: Api no JWT signing key (git ma nathi)
```powershell
$b = New-Object byte[] 64; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($b)) --project src/ClothingStore.Api
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

Local folders (repo ni bahar): `D:\ClothingStore\uploads` (product images), `D:\ClothingStore\keys`
(Data Protection keys – Manager e encrypt karela Razorpay secrets Api vanchi shake e mate).

### Demo login (fakt local)
Seed config mujab, password `Seed:DemoAdminPassword`:
- **Super Admin (Manager):** Store code khali, `Seed:SuperAdmin` nu email/password
- **Store admin (Manager):** Store code `shop1` (ke `shop2`), email `admin@shop1.local`
- **Customer (Catalog):** `customer@shop1.local` (email pehla thi verified)

### Emails (local)
`Email:Provider = Log`: verification / reset password ni email **Api na console log** ma aave chhe (link sathe).
smtp4dev vaparvu hoy to Api config ma `"Email": { "Provider": "Smtp", "Host": "localhost", "Port": 25 }`.

### Online payment (Razorpay test mode)
Manager → **Settings** ma Razorpay **test** keys (`rzp_test_…`) nakho. Key na hoy to fakt Cash on Delivery dekhay.
Webhook (backup) mate public URL joie (Dev Tunnels / ngrok): `https://<tunnel>/api/webhooks/razorpay/<store code>`.

### Navi migration
```bash
dotnet ef migrations add <Name> -p src/ClothingStore.Infrastructure -s src/ClothingStore.Manager -o Data/Migrations
```

### Tests
Integration tests LocalDB par navo temporary database banave ane pachhi delete kare.
```bash
dotnet test
```

### Secrets
JWT key, Razorpay/Shiprocket keys jeva secrets git ma commit na karva: `dotnet user-secrets` / environment variables.
Store na Razorpay keys Manager Settings thi DB ma **encrypted** save thay chhe.
