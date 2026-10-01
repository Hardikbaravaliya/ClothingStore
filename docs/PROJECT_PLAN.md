# ClothingStore – Project Plan (Girls Wear & Boys Wear)

> Aa file project no master plan che. Koi pan decision ke change aave, to e **Changelog** ma ane
> te section ma update karvo.

**Last updated:** 2026-09-30

---

## 1. Project overview

Kids clothing (Girls Wear ane Boys Wear) mate **multi-tenant SaaS platform**. Ek j system par ghana
shop owners (tenants) potano alag store chalavi shake. Darek tenant no data **TenantId** thi alag rahe.

- **Manager (Admin) side:** Product/image/details add karvi, supplier pase thi maal ni entry (purchase),
  stock, orders, shipping ane reports.
- **Catalog (Customer) side:** Product jovi, cart, purchase ane order tracking.
- Order padta stock aapoaap minus thay.
- Reports: Orders, sales, profit, stock ane supplier-wise purchase.

---

## 2. Final decisions

| Topic | Decision |
|---|---|
| Platform type | **Multi-tenant SaaS**: Shared Database, darek table ma `TenantId` – juo Section 4 |
| Framework | **.NET 9 / ASP.NET Core** |
| Database | **SQL Server** + Entity Framework Core 9 |
| Manager (Admin) | ASP.NET Core **MVC** – alag project |
| Catalog (Customer website) | ASP.NET Core **MVC** – alag project, data **API call** thi le che |
| API | ASP.NET Core **Web API** – Catalog mate. Future ma mobile app pan aa j API vaparse. |
| Mobile app | Haal nathi banavvani. Pachhi jarur pade to API par banavsu. |
| Payment | **Razorpay** (UPI, Card, NetBanking, Wallet) + COD – juo Section 10 |
| Shipping | Shiprocket (aggregator) – AWB, label, tracking webhook |
| Image storage | `IImageStorage` interface. **Haal: Local folder** (development/testing). **Live: Azure Blob Storage** (container `product-images`). Config thi switch. |
| Development | **Badhu local machine par**. Testing puru thay pachhi j hosting ane plans kharidva. Juo Section 13. |
| Customer login | **Email + Password** (ASP.NET Core Identity, email verification, forgot password) |
| UI / CSS | **Bootstrap 5** (Manager ane Catalog banne ma) |
| Notification | Email (SendGrid/SMTP) + SMS/WhatsApp (MSG91) – *final nakki karvanu baki* |
| Reports export | ClosedXML (Excel), QuestPDF (PDF), Chart.js (charts) |

---

## 3. Architecture

```
                    ┌──────────────────────────┐
                    │      SQL Server DB       │
                    └────────────▲─────────────┘
                                 │ EF Core
                    ┌────────────┴─────────────┐
                    │  ClothingStore.Services  │  ← badho business logic
                    │  (+ Infrastructure/Core) │
                    └───▲──────────────────▲───┘
                        │                  │
          ┌─────────────┴───┐        ┌─────┴──────────────┐
          │ ClothingStore.  │        │ ClothingStore.Api  │  ← Web API (JWT)
          │ Manager (MVC)   │        │ + Shiprocket       │
          │ admin.domain    │        │   webhook          │
          └─────────────────┘        └─────▲──────────────┘
                                           │ HttpClient (API call)
                                     ┌─────┴──────────────┐
                                     │ ClothingStore.     │
                                     │ Catalog (MVC)      │
                                     │ www.domain         │
                                     └────────────────────┘
                                     (Future: Mobile App → same API)
```

**Rules:**
- Business logic fakt **Services** ma. Controller ma logic nahi.
- **Manager** Services ne direct call kare.
- **Catalog** DB ne direct nahi adke. Badhu **Api** thi j kare.
- Stock minus, profit calculation, order status: badhu ek j jagya e (Services).
- Darek request ma **current tenant** resolve thay. Koi pan query bija tenant no data na lavi shake.

---

## 4. Multi-tenancy (SaaS)

**Approach:** **Shared Database, Shared Schema.** Ek j SQL Server database, ane darek business table ma
`TenantId` column hoy.

### Tenant kevi rite olkhase (Tenant resolution)
| App | Kevi rite |
|---|---|
| Catalog (customer website) | **Subdomain** (`shopname.domain.com`) athva **custom domain** → Api ne header `X-Tenant` mokle |
| Api | `X-Tenant` header / Host → Tenants table mathi TenantId. Customer JWT ma pan `tenant_id` claim. |
| Manager (admin) | Login user no `TenantId` claim (staff user ek j tenant no hoy) |
| Super Admin | Platform owner. Badha tenants manage kare (tenant filter bypass). |

### Implementation
- `ITenantProvider` (scoped) → current `TenantId` aape. Middleware request ni sharuaat ma set kare.
- `ITenantEntity` interface → `int TenantId { get; set; }`
- **EF Core Global Query Filter**: darek `ITenantEntity` par `e => e.TenantId == currentTenantId`.
  Etle developer bhuli jay to pan bija tenant no data na aave.
- **SaveChanges override**: navi entity par `TenantId` aapoaap set thay. Bija tenant nu `TenantId` hoy to exception.
- **Unique indexes** hamesha `TenantId` sathe: `(TenantId, SKU)`, `(TenantId, OrderNo)`, `(TenantId, Slug)`,
  `(TenantId, Email)`.
- **Indexes**: moti tables par `TenantId` pehla column tarike (e.g. `(TenantId, CreatedAt)`).
- Super Admin mate j `IgnoreQueryFilters()`, bije kyay nahi.

### Tenant-wise settings
- **Azure Blob**: path `product-images/{TenantId}/{ProductId}/{file}`
- **Razorpay**: darek tenant ni potani KeyId/KeySecret/WebhookSecret (encrypted, `TenantSettings` ma).
  Webhook URL ma tenant: `/api/webhooks/razorpay/{tenantKey}`
- **Shiprocket**: darek tenant ni potani API credentials ane pickup address (`TenantSettings` ma).
  Webhook URL: `/api/webhooks/shiprocket/{tenantKey}`
- Store name, logo, theme color, GST no, contact → `TenantSettings`
- **TimeZone ane Currency** → `TenantSettings` (niche detail)

### Tenant TimeZone ane Currency
Darek tenant potano **TimeZone** ane **Currency** set kare. Customer ane manager ne badhu e mujab dekhay.

| Setting | Example | Kaam |
|---|---|---|
| `TimeZoneId` | `Asia/Kolkata` (IANA) | Order date, tracking time, reports: badhu aa timezone ma batavvu |
| `CurrencyCode` | `INR` (ISO 4217) | Price, cart, order total, invoice, reports |
| `CurrencySymbol` | `₹` | Display mate |
| `CultureName` | `en-IN` | Number/date format (₹1,23,456.00, dd-MM-yyyy) |

**Rules:**
- **DB ma date hamesha UTC** (`DateTime` UTC / `DateTimeOffset`). Display vakhte tenant na TimeZone ma convert karvi.
  `TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId)` (.NET 9 ma IANA id Windows/Linux banne par chale).
- **Reports ni date range** (aaj, aa mahino) tenant na TimeZone mujab ganvi. Pachhi UTC ma convert kari query karvi.
- Amount `decimal(18,2)` ma save. **Orders, Payments ane Purchases ma `CurrencyCode` snapshot** save karvo,
  jethi tenant pachhi currency badle to jena order par asar na thay.
- Currency formatting: `amount.ToString("C", new CultureInfo(tenant.CultureName))`, athva common `IMoneyFormatter` service.
- Helper services: `ITenantClock` (tenant local "now" ane convert) ane `IMoneyFormatter`. Views ma direct `DateTime.Now` **nahi** vaparvo.
- Api response ma date UTC (ISO 8601) mokalvi. Sathe tenant settings endpoint `GET /api/store/settings`
  (name, logo, currency, timezone, culture). Catalog e mujab format kare.
- **Razorpay** ma order ni currency tenant ni `CurrencyCode` hase. INR sivay ni currency mate Razorpay account ma
  international payments enable hova joye.
- Currency badlavani **price convert nathi karti**. Manager e prices pote update karva padse (Manager UI ma warning batavvi).

### Platform tables (TenantId filter *nathi*)
| Table | Kaam |
|---|---|
| Tenants | Id, Name, Subdomain/Slug, CustomDomain, Status (Active/Suspended/Trial), PlanId, CreatedAt |
| TenantSettings | TenantId, logo, theme, GST, **TimeZoneId, CurrencyCode, CurrencySymbol, CultureName**, Razorpay keys, Shiprocket keys (encrypted) |
| SubscriptionPlans | Plan name, price, limits (max products, max staff users, max orders/month) |
| TenantSubscriptions | TenantId, PlanId, StartDate, EndDate, Status, payment ref |

### Tenant onboarding
1. Shop owner signup → Tenant bane (Trial) → Admin user bane
2. Store setup: name, logo, subdomain, **timezone, currency**, Razorpay ane Shiprocket keys
3. Categories/products add → Catalog live

### Rules
- Darek business table ma `TenantId` **NOT NULL** + FK → Tenants.
- Cache keys ma pan TenantId: `tenant:{id}:categories`
- Reports ane exports hamesha current tenant na j.
- Logs ma TenantId lakhvo (debugging mate).
- Test: Tenant A ni login thi Tenant B no data na dekhavo joye (integration test).

---

## 5. Solution structure

```
ClothingStore.sln
│
├─ src/
│  ├─ ClothingStore.Core/               (class library)
│  │   ├─ Entities/                     Product, ProductVariant, Order, Purchase ...
│  │   ├─ Enums/                        OrderStatus, PaymentStatus, StockTxnType ...
│  │   ├─ Interfaces/                   IRepository, IStockService, IShippingProvider ...
│  │   ├─ Common/                       BaseEntity, TenantEntity (ITenantEntity), Result, PagedList
│   └─ Tenancy/                      ITenantProvider, Tenant, TenantSettings, SubscriptionPlan
│  │
│  ├─ ClothingStore.Infrastructure/     (class library)
│  │   ├─ Data/                         AppDbContext (Global tenant query filter), Configurations, Migrations, Seed
│   ├─ Tenancy/                      TenantProvider, TenantResolutionMiddleware
│  │   ├─ Repositories/
│  │   ├─ Identity/                     ApplicationUser, Roles
│  │   ├─ Storage/                      Azure Blob image upload service (Azure.Storage.Blobs)
│  │   ├─ Payments/                     Razorpay
│  │   ├─ Shipping/                     Shiprocket client
│  │   └─ Notifications/                Email / SMS
│  │
│  ├─ ClothingStore.Services/           (class library)
│  │   ├─ DTOs/
│  │   ├─ Products/                     ProductService, CategoryService
│  │   ├─ Purchases/                    SupplierService, PurchaseService
│  │   ├─ Inventory/                    StockService
│  │   ├─ Orders/                       CartService, OrderService
│  │   ├─ Shipping/                     ShipmentService
│  │   └─ Reports/                      ReportService
│  │
│  ├─ ClothingStore.Api/                (ASP.NET Core Web API)
│  │   ├─ Controllers/                  Catalog, Cart, Orders, Account, Tracking, Webhooks
│  │   └─ Auth/                         JWT
│  │
│  ├─ ClothingStore.Manager/            (ASP.NET Core MVC – Admin)
│  │   ├─ Controllers/  Views/  wwwroot/
│  │   └─ Areas/ (optional)
│  │
│  └─ ClothingStore.Catalog/            (ASP.NET Core MVC – Customer website)
│      ├─ Controllers/  Views/  wwwroot/
│      └─ ApiClients/                   Typed HttpClient (ProductApiClient, OrderApiClient ...)
│
└─ tests/
   └─ ClothingStore.Services.Tests/
```

---

## 6. Modules

### 6.0 Super Admin (Platform owner)
- [ ] Tenants list, create, suspend/activate
- [ ] Subscription plans ane tenant subscriptions
- [ ] Platform-level reports (total tenants, active stores, revenue)

### 6.1 Manager (Admin – darek tenant nu)
- [ ] Login ane roles (SuperAdmin, TenantAdmin, Manager)
- [ ] Store settings: name, logo, GST, **TimeZone, Currency (code, symbol, culture)**, Razorpay keys, Shiprocket keys
- [x] Category / Sub-category (Girls Wear, Boys Wear → Frock, T-Shirt, Jeans ...)
- [x] Product: name, description, brand, fabric, age group, MRP, selling price, badhi images
- [x] Variants: Size + Color + SKU + stock
- [x] Supplier management (name, contact, GST, address)
- [x] Purchase entry: supplier, date, items, qty, cost price → stock aapoaap plus
- [x] Stock view, low-stock alert, stock ledger
- [ ] Orders: confirm, pack, ship, deliver, cancel, return
- [ ] Shiprocket: shipment create, AWB, label print
- [ ] Reports dashboard
- [ ] Coupons (optional)

### 6.2 Catalog (Customer website)
- [x] Home page, category listing, filters (size, color, price), search
- [ ] Product detail (images, size chart, variant select)
- [x] Customer register/login: Email + Password, email verification, forgot/reset password
- [x] Cart ane checkout (address)
- [x] Payment: Razorpay / COD
- [x] My Orders ane tracking timeline
- [ ] Return / Exchange request
- [x] SEO-friendly URLs ane meta tags

### 6.3 API
- [x] JWT auth (customer)
- [x] Catalog endpoints (categories, products, product detail)
- [x] Cart, Checkout, Orders endpoints
- [x] Razorpay: create order, payment verify, webhook endpoints
- [ ] Tracking endpoint
- [ ] Shiprocket webhook endpoint
- [x] Swagger (dev ma)

---

## 7. Database tables

> **Badha business tables ma `TenantId` (int, NOT NULL, FK → Tenants)** che. Platform tables
> (Tenants, SubscriptionPlans vagere) mate juo Section 4.

| Table | Kaam |
|---|---|
| AspNetUsers / Roles (Identity) | Staff ane customer login + `TenantId` (SuperAdmin mate null). Email unique per tenant. |
| Categories | Girls/Boys, sub-category (ParentId) |
| Products | Main product |
| ProductImages | Photos (Url, SortOrder, IsPrimary) |
| ProductVariants | Size, Color, SKU, SellingPrice, **StockQty**, **AvgCostPrice**, RowVersion |
| Suppliers | Supplier details |
| Purchases | Supplier, date, invoice no, total |
| PurchaseItems | Variant, qty, cost price |
| StockTransactions | Darek IN/OUT/Return/Adjust nu record |
| Customers / Addresses | Customer ane addresses |
| Carts / CartItems | Cart |
| Orders | Order no, customer, status, totals, shipping, discount |
| OrderItems | Variant, qty, selling price, **cost price (snapshot)** |
| Payments | Razorpay id, status, method |
| Shipments | Courier, AWB, status |
| ShipmentTrackingLogs | Tracking na badha updates |
| Returns | Return/Exchange requests |
| Coupons | (optional) |

---

## 8. Important business rules

### Stock
- **Purchase save** → `StockQty += qty`, StockTransaction "IN", ane AvgCostPrice update.
- **Order confirm / payment success** → `StockQty -= qty`, StockTransaction "OUT".
- **Cancel / Return** → `StockQty += qty`, StockTransaction "RETURN".
- Stock update hamesha **DB transaction** ma karvo. Concurrency mate **RowVersion** vaparvo.
  Stock kyarey minus ma na javo joye.

### Cost & Profit
- Cost method: **Weighted Average Cost**.
  `NewAvg = ((OldQty × OldAvg) + (NewQty × NewCost)) / (OldQty + NewQty)`
- Order vakhte OrderItem ma CostPrice save karvo (snapshot).
- Profit = Σ (SellingPrice − CostPrice) × Qty − Discount − Shipping cost

### Order status flow
```
Pending → Confirmed → Packed → Shipped → OutForDelivery → Delivered
              └→ Cancelled                    └→ ReturnRequested → Returned
```

---

## 9. Reports
- [ ] Aaj / week / month na orders ane sales
- [ ] Profit report (date range)
- [ ] Product-wise ane category-wise sales, best sellers
- [ ] Supplier-wise purchase report
- [ ] Stock report, low stock, dead stock
- [ ] Order status report
- [ ] COD vs Online report
- [ ] Excel / PDF export

---

## 10. Payment (Razorpay)

**Decision:** Online payment mate **Razorpay** vaparvanu che. Sathe **COD** option pan rahese.

**Payment methods:** UPI, Debit/Credit Card, Net Banking, Wallets (Razorpay Checkout thi) + COD

**Package:** `Razorpay` NuGet (.NET SDK). Integration `ClothingStore.Infrastructure/Payments/` ma.

**Settings (appsettings / secrets – git ma commit na karva):**
```json
"Razorpay": {
  "KeyId": "rzp_test_xxxxx",
  "KeySecret": "xxxxx",
  "WebhookSecret": "xxxxx"
}
```

### Online payment flow
1. Customer Catalog ma checkout kare → Api `POST /api/orders` → Order **Pending** status ma bane
   (haju stock minus nahi).
2. Api Razorpay ma **Order create** kare (amount paisa ma, `receipt` = aapdo OrderNo) →
   `razorpay_order_id` Payments table ma save.
3. Catalog page par **Razorpay Checkout (JS)** khule → customer payment kare.
4. Success par Razorpay aa 3 values aape: `razorpay_payment_id`, `razorpay_order_id`, `razorpay_signature`
   → Catalog e Api `POST /api/payments/verify` ne mokle.
5. Api **signature verify** kare (HMAC SHA256, KeySecret thi):
   `HMAC(order_id + "|" + payment_id) == signature`
6. Verify thay to → Payment **Paid**, Order **Confirmed**, **stock minus** (ek j DB transaction ma).
7. Fail / cancel thay to → Payment **Failed**, Order Pending/Cancelled, stock ma koi change nahi.

### Webhook (backup)
- Endpoint: `POST /api/webhooks/razorpay` (header `X-Razorpay-Signature` WebhookSecret thi verify karvo)
- Events: `payment.captured`, `payment.failed`, `refund.processed`
- Customer browser band kari de to pan payment status webhook thi update thai jay.
- **Idempotent** rakhvu: same payment be var aave to stock be var minus na thavo joye.

### COD
- Order sidho **Confirmed** thay, stock minus, Payment status **Pending (COD)**.
- Delivery pachhi Payment **Paid** mark thay (Shiprocket COD remittance / manager manual update).

### Refund
- Cancel / Return approve thay tyare Manager mathi **Razorpay Refund API** call.
- Refund status Payments table ma update, stock pachho plus (Return case ma).

### Payments table fields
`Id, OrderId, Method (Online/COD), RazorpayOrderId, RazorpayPaymentId, RazorpaySignature,
Amount, Status (Pending/Paid/Failed/Refunded), RefundId, RefundAmount, CreatedAt, UpdatedAt`

### Checklist
- [ ] Razorpay account ane KYC
- [ ] Test keys thi development, live keys fakt production ma
- [x] Create order + Checkout + Verify signature
- [x] Webhook endpoint ane signature verify
- [ ] Refund (Manager)
- [ ] Payment report (Online vs COD, Failed payments)

---

## 11. Shipping & tracking (Shiprocket)
1. Admin "Ship" kare → Shiprocket API → AWB number ane label.
2. Shiprocket **webhook** → `Api/Webhooks/Shiprocket` → ShipmentTrackingLogs ma save → Order status update.
3. Customer Catalog ma "My Orders" ma timeline jove.
4. Status badlay tyare Email/SMS jay.

> Volume vadhe (roj 50–100+ orders) tyare direct Delhivery contract no vichar karvo.

---

## 12. Deployment
- Manager → `admin.<domain>` (badha tenants mate ek j; login thi tenant nakki)
- Catalog → `{tenant}.<domain>` (wildcard subdomain + wildcard SSL) / tenant no custom domain
- Api → `api.<domain>`
- Hosting: *nakki karvanu baki*
- Images: Azure Blob Storage (Storage account + container `product-images`, public read ke SAS URL)
- Secrets (connection string, API keys) → appsettings.Production / environment variables / user-secrets
  (git ma commit na karva)

---

## 13. Local development setup (haal)

**Decision:** Atyare badhu **local** par banavvu ane test karvu. Hosting, Azure ane paid plans test puru thay pachhi j levaa.
Local setup no kharcho **₹0** che.

| Vastu | Local ma shu vaparvu | Live ma |
|---|---|---|
| IDE | Visual Studio 2022 Community / VS Code | – |
| Database | SQL Server Developer / Express (ke LocalDB) | Azure SQL / VPS SQL Server |
| Images | `LocalFileImageStorage`: Api/Manager ni bahar ek **common folder** (e.g. `D:\ClothingStore\uploads`), static files thi serve | `AzureBlobImageStorage` |
| Payment | **Razorpay Test Mode** (test keys, test card/UPI, paisa nathi kapata) | Razorpay Live keys |
| Shipping | `FakeShippingProvider` (dummy AWB ane status) | Shiprocket |
| Email | **smtp4dev / Papercut** (local fake SMTP, mail browser ma dekhay) | Brevo / SendGrid |
| Tenant subdomain | `shop1.localhost:5001`, `shop2.localhost:5001` (browser ma sidhu chale) | `shop1.domain.com` |
| Webhooks test | **Visual Studio Dev Tunnels** / ngrok (free) | Real public URL |

### Code rule: provider pattern
Darek external service nu **interface** banavvu, ane `appsettings.json` thi implementation pasand karvi.
Live jati vakhte **fakt config badlay, code nahi.**

```json
"Storage":  { "Provider": "Local", "LocalPath": "D:\\ClothingStore\\uploads", "BaseUrl": "https://localhost:5003/uploads" },
"Shipping": { "Provider": "Fake" },
"Email":    { "Provider": "Smtp", "Host": "localhost", "Port": 25 },
"Razorpay": { "Mode": "Test" }
```

| Interface | Local implementation | Live implementation |
|---|---|---|
| `IImageStorage` | LocalFileImageStorage | AzureBlobImageStorage |
| `IShippingProvider` | FakeShippingProvider | ShiprocketProvider |
| `IEmailSender` | SmtpEmailSender (smtp4dev) | Brevo/SendGrid |
| `IPaymentGateway` | Razorpay (test keys) | Razorpay (live keys) |

- DB ma image no **relative path** save karvo (e.g. `{TenantId}/{ProductId}/abc.jpg`), full URL nahi.
  URL `BaseUrl + path` thi bane. Etle pachhi Azure Blob par gaya pachhi DB badalvo nahi pade.
- Migrate vakhte local folder na files Azure Blob ma same path par upload kari devaa (AzCopy).

### Live jata pehla checklist
- [ ] Badha modules local par test
- [ ] Domain kharidvu
- [ ] Hosting nakki ane setup
- [ ] Azure Storage account + container, images migrate
- [ ] Razorpay KYC + Live keys
- [ ] Shiprocket account + KYC
- [ ] Email provider
- [ ] Config ma providers switch (Local → Azure, Fake → Shiprocket)

---

## 14. Phases

| Phase | Kaam | Status |
|---|---|---|
| 1 | Solution setup, Core, Infrastructure, **Multi-tenancy (Tenants, TenantId, query filter, middleware)**, DB tables, Identity, Migrations | ✅ Done |
| 2 | Manager: Category, Product, Variants, Images, Supplier, Purchase, Stock | ✅ Done |
| 3 | Api + Catalog: Product list/detail, filter, cart, checkout, payment, order | ✅ Done |
| 4 | Manager: Order management + Shiprocket; Catalog: tracking page | ⏳ Pending |
| 5 | Reports dashboard + Excel/PDF export | ⏳ Pending |
| 6 | Returns, Coupons, GST invoice, (future) Mobile app | ⏳ Pending |

---

## 15. Pending decisions
- [x] Image storage → **Local folder (haal)**, live par **Azure Blob Storage**
- [ ] SMS/WhatsApp provider
- [ ] Hosting provider
- [x] Customer login → **Email + Password**
- [x] UI theme / CSS framework → **Bootstrap 5**

---

## 16. Changelog

> Navo change aave to **upar** navi line add karo: `YYYY-MM-DD – shu badlayu`

- **2026-10-01** – **Phase 3 puro.** Api + Catalog: categories, product list/filters/search, product detail, customer register/login
  (email verification, forgot/reset password), cart (guest + login merge), checkout (address), COD + Razorpay, My orders + timeline.
  Manager ma **Store settings** (contact, timezone/currency, Razorpay keys). Nakki karela nirnayo:
  - Navo project **ClothingStore.Contracts**: Api na request/response models; Catalog EF/Services ne reference nathi kartu.
  - Customer login **JWT (7 divas)**, Catalog ni encrypted login cookie ma rakhay. Refresh token haju nathi.
  - Login pehla **email verify farajiyat**. Email/reset links hamesha store na potana domain par (`Tenancy:CatalogBaseUrlTemplate`).
  - Razorpay keys DB ma **Data Protection thi encrypted**; key ring `D:\ClothingStore\keys` Manager ane Api share kare.
  - Online order **Pending** rahe, stock payment verify thay pachhi j ghate; cart pan tyare j khali thay. Payment confirm idempotent
    (verify + webhook banne aave to pan ek j var). Payment pachhi stock na hoy to order Cancelled + "refund pending" (refund Phase 4).
  - Delivery charge, tax ane coupon haal **0** (prices GST sathe). Shipping rules Phase 4 ma.
  - Customer ne stock "max 10" sudhi j dekhay; sacho stock khanagi.
  - Login/register/password endpoints par rate limit (10/min/IP).

- **2026-10-01** – **Phase 2 puro.** Manager ma Categories (2 level), Products, Variants, Images, Suppliers, Purchases ane Stock (list, low-stock, ledger, adjust).
  Nakki karela nirnayo:
  - **Variants ek sathe:** sizes ane colors comma thi lakho, darek Size × Color no variant bane. SKU aapoaap (`{ProductId}-{Size}-{Color}`), pachhi edit thai shake.
  - **Stock fakt Purchase ke Stock Adjust thi j badlay** (variant edit ma stock field nathi). Darek badlav no ledger row.
  - `StockTransactions.Quantity` have **signed** chhe (+ aave, − jay), jethi Adjust ma direction dekhay.
  - **Saved purchase edit/delete nathi thati.** Bhul hoy to Stock page par adjust. (Purchase return pachhi jarur pade to umersu.)
  - Purchase save ek DB transaction ma; RowVersion conflict par 3 var retry.
  - Images: JPG/PNG/WEBP fakt (file na andar na bytes thi check), 5 MB, product dith 10. Path `{TenantId}/{ProductId}/{guid}.ext`.
  - Product/variant/supplier no history (stock, purchase, order) hoy to delete nahi; **Inactive** karvo.
  - `ITenantClock` ane `IMoneyFormatter` umerya (Section 4). Store pages fakt TenantAdmin/Manager mate; Super Admin ne nathi dekhata.

- **2026-10-01** – Badha tables na **Id ane FK `Guid` mathi `int` (identity)** karya, Identity users/roles pan `int`.
  `InitialCreate` migration fari banavi (haju koi live data nathi). Public URL ma order mate `OrderNo` vaparvo, Id nahi.

- **2026-10-01** – **Phase 1 puro.** Solution (6 projects + tests), badha tables, `InitialCreate` migration, Identity,
  tenant query filter ane SaveChanges rules, Api `X-Tenant`/subdomain resolution, Manager login.
  Nakki karela nirnayo:
  - **Manager login ma "Store code"** field: staff email fakt tenant ma unique chhe, etle login vakhte store jaanvo pade. Khali = Super Admin.
  - Catalog Api ne `X-Tenant` ma potano **host** mokle (`shop1.localhost`); Api j host → tenant nakki kare (subdomain ke custom domain).
  - Haal **Services direct `AppDbContext`** vapare chhe (EF Core pote repository/unit of work chhe). Generic `IRepository` nathi banavyu.
  - Business tables na FK default **Restrict**; fakt child rows (OrderItems, PurchaseItems, ProductImages, ProductVariants, CartItems, Addresses, TrackingLogs) par Cascade.
  - Enums DB ma **string** tarike save; badha `DateTime` UTC (read vakhte `Kind=Utc`).
  - Local ports: Catalog `5101` (http, subdomain mate), Manager `5002`, Api `5003`. `global.json` thi .NET 9 SDK pin (VS 2022 mate).

- **2026-10-01** – Haal badhu **local** par: images local folder ma, Razorpay test mode, Fake shipping, smtp4dev. Provider pattern thi live par fakt config switch. Section 13 (Local development setup) add karyo. Plans testing pachhi kharidva.
- **2026-09-30** – Tenant settings ma **TimeZone ane Currency** add karya. DB ma UTC date, display tenant timezone ma, Orders/Payments/Purchases ma CurrencyCode snapshot.
- **2026-09-30** – Project **multi-tenant SaaS** banyo. Darek table ma `TenantId`. Section 4 (Multi-tenancy) add karyo: tenant resolution, global query filter, tenant-wise Razorpay/Shiprocket/Blob, subscription plans, Super Admin.
- **2026-09-30** – Image storage **Azure Blob**, customer login **Email + Password**, UI **Bootstrap 5** final. SMS/WhatsApp ane Hosting haju pending.
- **2026-09-30** – Payment mate **Razorpay** final. Section 10 ma Razorpay flow, webhook, COD ane refund add karya.
- **2026-09-30** – Initial plan. .NET 9, SQL Server, Manager (MVC) ane Catalog (MVC) alag projects.
  Catalog data Api thi le. Mobile app haal nahi, future ma same Api par.
