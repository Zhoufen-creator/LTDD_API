# Parental Control - ASP.NET Core Web API Backend

Hệ thống Backend Web API phục vụ ứng dụng giám sát và bảo vệ thiết bị trẻ em (Parental Control), xây dựng trên nền tảng **ASP.NET Core .NET 8**, cơ sở dữ liệu **Microsoft SQL Server 2022**, bảo mật xác thực **JWT (JSON Web Token)**, **Google OAuth 2.0**, và hỗ trợ tunnel ra Internet qua **Conveyor** / triển khai qua **Docker Compose & Caddy**.

---

## Mục lục

1. [Giới thiệu tổng quan](#1-giới-thiệu-tổng-quan)
2. [Yêu cầu hệ thống](#2-yêu-cầu-hệ-thống)
3. [Cấu trúc thư mục](#3-cấu-trúc-thư-mục)
4. [Clone repository](#4-clone-repository)
5. [Cấu hình biến môi trường & Bảo mật](#5-cấu-hình-biến-môi-trường--bảo-mật)
6. [Khởi chạy Cơ sở dữ liệu SQL Server (Docker)](#6-khởi-chạy-cơ-sở-dữ-liệu-sql-server-docker)
7. [Khởi tạo & Áp dụng Migrations (EF Core)](#7-khởi-tạo--áp-dụng-migrations-ef-core)
8. [Khởi chạy API & Kiểm tra hoạt động](#8-khởi-chạy-api--kiểm-tra-hoạt-động)
9. [Cấu hình SMTP & Xác thực OTP](#9-cấu-hình-smtp--xác-thực-otp)
10. [Conveyor Tunnel & Truy cập từ Internet](#10-conveyor-tunnel--truy-cập-từ-internet)
11. [Chạy Unit & Integration Tests](#11-chạy-unit--integration-tests)
12. [Xử lý sự cố thường gặp (Troubleshooting)](#12-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. Giới thiệu tổng quan

Backend cung cấp toàn bộ RESTful API và nghiệp vụ trung tâm cho hệ thống Parental Control:

- **Xác thực & Phân quyền**: Đăng nhập bằng Email/Password, đăng ký xác thực bằng OTP email, đăng nhập nhanh qua Google Sign-In (xác thực Google ID Token phía server), mã hóa PBKDF2 và cấp phát JWT Bearer Token.
- **Ghép đôi thiết bị (Device Pairing)**: Sinh mã vé QR (`PairingTicket`) dùng một lần có hạn 5 phút; kích hoạt và ghi danh thiết bị trẻ em (`DeviceEnrollment`).
- **Phê duyệt đổi chế độ (Device Mode Requests)**: Luồng kiểm soát thiết bị trẻ em yêu cầu đổi chế độ, mở khóa tạm thời hoặc hủy liên kết; phụ huynh phê duyệt/từ chối từ xa hoặc mở khóa trực tiếp (`Direct Unlock`).
- **Định vị & Yêu cầu làm mới vị trí**: Ghi nhận tọa độ GPS thời gian thực từ thiết bị trẻ em; phụ huynh có thể gửi yêu cầu cưỡng chế làm mới vị trí tức thời (`LocationRefreshRequest`).
- **Giám sát pin & tình trạng thiết bị**: Đồng bộ phần trăm pin, trạng thái sạc và mốc thời gian cập nhật gần nhất.
- **Giám sát hoạt động & Thực thi quy tắc (Rule Engine)**: Ghi nhận log mở/đóng ứng dụng theo lô (`Batch Activity Logging`), kiểm tra và thực thi các quy tắc giới hạn thời gian sử dụng, giờ đi ngủ và lịch trình tuần.
- **Cố vấn an toàn (Safety Advisor)**: Phân tích thói quen sử dụng, cảnh báo ứng dụng dùng quá mức và gợi ý biện pháp bảo vệ phù hợp cho phụ huynh.

### Công nghệ sử dụng
- **Ngôn ngữ & Framework**: C# 12, .NET 8.0 SDK (ASP.NET Core Web API)
- **Cơ sở dữ liệu & ORM**: Microsoft SQL Server 2022, Entity Framework Core 8.0 (Code-First Migrations)
- **Bảo mật**: PBKDF2 Password Hashing, JWT HMAC-SHA256, Google ID Token Validation
- **Phụ trợ**: Docker & Docker Compose, Swagger/OpenAPI v1, MemoryCache, HealthChecks
- **Tunnel & Proxy**: Conveyor CLI (kiểm thử Internet), Caddy v2 (reverse proxy production)

---

## 2. Yêu cầu hệ thống

Trước khi bắt đầu, đảm bảo máy tính của bạn đã cài đặt:

- **Git**: Phiên bản 2.30 trở lên
- **.NET SDK**: Phiên bản **8.0** (khuyến nghị SDK `8.0.421` hoặc mới hơn)  
  *Kiểm tra:* `dotnet --version`
- **Entity Framework Core CLI**:  
  *Cài đặt nếu chưa có:* `dotnet tool install --global dotnet-ef`  
  *Kiểm tra:* `dotnet ef --version`
- **Docker Desktop**: Chạy chế độ Linux containers với WSL2 (để chạy SQL Server 2022)  
  *Kiểm tra:* `docker --version` và `docker compose version`

---

## 3. Cấu trúc thư mục

```text
tran_nguyen_vi_khang_api/
├── Controllers/                  # API Controllers (Auth, Children, Devices, Locations, Activities, Rules)
├── Data/                         # EF Core AppDbContext & cấu hình Entity Fluent API
├── DTOs/                         # Data Transfer Objects cho Request và Response
├── Migrations/                   # 6 file EF Core Migrations và snapshot mô hình dữ liệu
├── Models/                       # Các thực thể dữ liệu (User, Child, Device, Activity, Rule, Location,...)
├── Properties/                   # launchSettings.json (chạy port 5153 mặc định)
├── Services/                     # Các lớp xử lý nghiệp vụ, JWT, Google Auth, Email, Rules
├── tests/                        # 28 bài Unit & Integration tests xUnit (.NET 8)
│   ├── ActivityAndRuleEngineTests.cs
│   ├── ModeRequestAndLocationTests.cs
│   ├── SafetyAdvisorServiceTests.cs
│   └── tran_nguyen_vi_khang_api.Tests.csproj
├── appsettings.json              # Cấu hình cơ sở (ConnectionStrings, JWT Issuer,...)
├── appsettings.Development.json  # Cấu hình môi trường Development
├── docker-compose.yml            # Docker Compose khởi chạy SQL Server, API và Caddy
├── Caddyfile                     # Cấu hình Reverse Proxy & HTTPS tự động
├── Dockerfile                    # Multi-stage Dockerfile build Release API trên Linux container
├── tran_nguyen_vi_khang_api.sln  # Solution .NET liên kết Web API và Tests
├── tran_nguyen_vi_khang_api.csproj # File dự án .NET 8 Web API
├── tran_nguyen_vi_khang_api.http # File HTTP requests mẫu dùng để test nhanh qua VS Code/Rider
├── .env.example                  # Mẫu khai báo biến môi trường (KHÔNG chứa secret thật)
├── .gitignore                    # Cấu hình loại trừ file nhạy cảm và thư mục build
└── Program.cs                    # Điểm khởi chạy ứng dụng, DI container, Middleware pipeline
```

---

## 4. Clone repository

```powershell
# Clone mã nguồn backend về máy tính
git clone <URL_BACKEND_REPOSITORY> tran_nguyen_vi_khang_api
cd tran_nguyen_vi_khang_api
```

---

## 5. Cấu hình biến môi trường & Bảo mật

Ứng dụng đọc cấu hình kết hợp từ `appsettings.json`, file `.env`, và Environment Variables của hệ điều hành.

### Bước 5.1: Tạo file `.env` từ `.env.example`

Trong thư mục `tran_nguyen_vi_khang_api`, sao chép file mẫu:

```powershell
Copy-Item .env.example .env
```

### Bước 5.2: Khai báo các biến quan trọng trong `.env`

Mở file `.env` và thiết lập các giá trị:

```ini
# Mật khẩu quản trị viên SQL Server 'sa'
MSSQL_SA_PASSWORD=YourStrongPassword_2026!

# Khóa bí mật ký JWT Token (tối thiểu 32 ký tự)
Jwt__Issuer=ParentalControlAPI
Jwt__Audience=ParentalControlApp
Jwt__Secret=YourSecureRandomSecretKeyMustBeAtLeast32BytesLongForHmacSha256!

# Google Client ID (khớp với ứng dụng Flutter)
Authentication__Google__ClientId=818917238880-dtsl08ml2ekg9fa4gi7pavq8afc4tjvq.apps.googleusercontent.com

# Cờ tự động chạy migrations khi API khởi động
APPLY_MIGRATIONS_ON_STARTUP=true

# Bật Swagger UI
ENABLE_SWAGGER=true
```

> [!CAUTION]
> **Tuyệt đối không commit file `.env` lên GitHub.** File này đã được đưa vào `.gitignore`. Mọi mật khẩu, token, và secret chỉ được lưu trữ cục bộ.

---

## 6. Khởi chạy Cơ sở dữ liệu SQL Server (Docker)

Hệ thống backend sử dụng Microsoft SQL Server 2022 chạy trong Docker container. Để tránh xung đột với các phiên bản SQL Server mặc định trên máy host (thường chiếm port 1433), cấu hình Docker Compose ánh xạ:
- **Cổng trên máy host**: `1434`
- **Cổng bên trong container**: `1433`

### Khởi động container

Từ thư mục `tran_nguyen_vi_khang_api`:

```powershell
docker compose up -d sqldev
```

### Kiểm tra trạng thái container

```powershell
docker ps
```

Kết quả hiển thị container `tran_nguyen_vi_khang_sqlserver` ở trạng thái `Up`, cổng `0.0.0.0:1434->1433/tcp`.

> [!NOTE]
> Dữ liệu được lưu trữ bền vững trong Docker volume `tran_nguyen_vi_khang_sqlserver`. Dữ liệu sẽ không bị mất khi restart container hoặc khởi động lại máy tính.

---

## 7. Khởi tạo & Áp dụng Migrations (EF Core)

Sau khi SQL Server đã hoạt động, bạn cần tạo schema database và các bảng.

Chuỗi kết nối thực tế trên máy host là:
```text
Server=localhost,1434;Database=TranNguyenViKhangDB;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=True
```

### Cách 1: Áp dụng qua lệnh EF Core CLI (Khuyến nghị khi cài đặt mới)

```powershell
# Chạy migration để áp dụng toàn bộ schema lên database
dotnet ef database update
```

Nếu thành công, EF Core sẽ tự động áp dụng 6 migration:
1. `20261029034917_InitialCreate`: Bảng Users, Children, Devices, PairingTickets
2. `20261029043553_AddGoogleAuthToUser`: Bổ sung trường GoogleId và hỗ trợ Google Sign-In
3. `20261029082226_AddDeviceLocation`: Bảng DeviceLocations ghi nhận tọa độ GPS
4. `20261029090256_AddLocationRefreshAndBattery`: Bảng LocationRefreshRequests và cột BatteryLevel
5. `20261029131337_AddModeRequestsAndLocationOptimization`: Bảng DeviceModeRequests
6. `20261029134149_AddDeviceActivityLogAndRuleEngine`: Bảng DeviceActivityLogs và MonitoringRules

### Cách 2: Tự động chạy khi khởi động API
Nếu cờ `APPLY_MIGRATIONS_ON_STARTUP=true` trong `.env`, API khi chạy sẽ tự động kiểm tra và thực thi các migrations còn thiếu với cơ chế retry tối đa 8 lần.

---

## 8. Khởi chạy API & Kiểm tra hoạt động

### Bước 8.1: Restore dependencies & Build

```powershell
dotnet restore
dotnet build
```

### Bước 8.2: Khởi chạy API

```powershell
dotnet run --no-launch-profile --urls "http://localhost:5153"
```

Console hiển thị:
```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5153
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Bước 8.3: Kiểm tra API

Mở trình duyệt hoặc dùng lệnh PowerShell:

- **Kiểm tra Health Endpoint**:
  ```powershell
  Invoke-RestMethod http://localhost:5153/health
  ```
  *Kết quả:* `Healthy` (HTTP 200)

- **Xem tài liệu Swagger UI**:
  Truy cập: [http://localhost:5153/swagger](http://localhost:5153/swagger)

---

## 9. Cấu hình SMTP & Xác thực OTP

Hệ thống hỗ trợ gửi mã OTP 6 chữ số qua email khi đăng ký tài khoản.

### Chế độ Development (Mặc định)
- Khi chạy ở môi trường Development và chưa cấu hình SMTP, `EmailSenderService` sẽ **không báo lỗi** mà ghi trực tiếp mã OTP vào console log:
  ```text
  warn: [DEV ONLY] OTP for khang@example.com (not sent via email): 839201
  ```
- Đồng thời, backend chấp nhận mã fallback chuẩn **`123456`** cho mục đích kiểm thử và demo nhanh mà không cần tạo App Password.

### Cấu hình SMTP thực tế (Nếu muốn gửi email thật)
Nếu muốn gửi email xác thực thật qua Gmail:
1. Tạo Google App Password (16 ký tự) từ tài khoản Google đã bật 2-Step Verification.
2. Cập nhật vào `.env`:
   ```ini
   Smtp__Host=smtp.gmail.com
   Smtp__Port=587
   Smtp__Username=your-email@gmail.com
   Smtp__Password=xxxx xxxx xxxx xxxx
   Smtp__From=your-email@gmail.com
   ```

---

## 10. Conveyor Tunnel & Truy cập từ Internet

Để cho phép ứng dụng Flutter chạy trên điện thoại thật ở bất kỳ mạng Wi-Fi/4G nào kết nối về backend trên máy tính cá nhân, hệ thống sử dụng **Conveyor Tunnel**.

### URL Tunnel hiện tại
```text
https://differentaquahouse65.conveyor.cloud
```

### Nguyên lý hoạt động
1. Máy tính cá nhân chạy backend tại `http://localhost:5153`.
2. Conveyor tạo tunnel an toàn đưa cổng này ra Internet với URL HTTPS công khai.
3. Ứng dụng di động Flutter gửi request tới URL Conveyor.

### Kiểm tra từ Internet
```powershell
Invoke-RestMethod https://differentaquahouse65.conveyor.cloud/health
```
Nếu trả về `Healthy`, kết nối từ Internet đã thông suốt.

> [!WARNING]
> Conveyor không phải VPS độc lập. Khi tắt máy tính cá nhân hoặc đóng terminal chạy Conveyor/API, URL công khai sẽ không thể truy cập. Nếu tạo tunnel mới và được cấp URL khác, bạn phải build lại ứng dụng Flutter với URL mới đó.

---

## 11. Chạy Unit & Integration Tests

Dự án đi kèm bộ test toàn diện 28 tests trong thư mục `tests/`:

```powershell
# Cách 1: Chạy test qua Solution (Khuyến nghị)
dotnet test tran_nguyen_vi_khang_api.sln

# Cách 2: Chạy trực tiếp qua thư mục tests
dotnet test tests
```

Bộ test bao gồm:
- `ActivityAndRuleEngineTests.cs`: Kiểm thử logic ghi nhận log hoạt động và thực thi quy tắc giới hạn
- `ModeRequestAndLocationTests.cs`: Kiểm thử yêu cầu đổi chế độ, duyệt phê duyệt và cập nhật vị trí
- `SafetyAdvisorServiceTests.cs`: Kiểm thử thuật toán phân tích an toàn cho phụ huynh

---

## 12. Xử lý sự cố thường gặp (Troubleshooting)

| Hiện tượng | Nguyên nhân | Cách khắc phục |
|---|---|---|
| Lỗi: `Cannot connect to SQL Server` | Container chưa chạy hoặc sai port | Kiểm tra `docker ps`. Đảm bảo container đang chạy và port host là `1434`. Kiểm tra lại `MSSQL_SA_PASSWORD` trong `.env`. |
| Lỗi: `Port 5153 is already in use` | Process backend trước đó chưa tắt | Chạy `Get-Process -Name "tran_nguyen_vi_khang_api" \| Stop-Process` trên PowerShell rồi chạy lại. |
| Swagger hiển thị 404 | Cờ `ENABLE_SWAGGER` đang là `false` | Đổi `ENABLE_SWAGGER=true` trong `.env` hoặc đảm bảo `ASPNETCORE_ENVIRONMENT=Development`. |
| Conveyor trả về 502 Bad Gateway | Backend chưa chạy trên cổng 5153 | Đảm bảo backend đã khởi động tại `http://localhost:5153` trước khi kiểm tra tunnel. |
| Không nhận được email OTP | Chưa cấu hình SMTP | Sử dụng mã OTP in trên console log của backend hoặc nhập mã fallback **`123456`**. |

