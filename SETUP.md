# Hướng dẫn Setup Hệ thống SafeRide

Tài liệu này hướng dẫn chi tiết cách thiết lập môi trường phát triển cho hệ thống SafeRide, bao gồm cả Backend (.NET 8) và Frontend (Flutter), cũng như cách chạy các bài kiểm thử (test).

## 1. Yêu cầu hệ thống (Prerequisites)

Để chạy dự án, máy tính của bạn cần cài đặt:
- **.NET 8 SDK**
- **Flutter SDK** (phiên bản ổn định mới nhất)
- **SQL Server** (hoặc LocalDB)
- **Redis Server** (có thể dùng Docker để chạy: `docker run -p 6379:6379 -d redis`)
- **Git**
- IDE khuyến nghị: Visual Studio / Rider (cho BE), VS Code / Android Studio (cho FE).

---

## 2. Thiết lập Backend (.NET 8)

Backend của SafeRide nằm trong thư mục `src/SRD_Master`. Nó được xây dựng dựa trên Clean Architecture.

### 2.1 Cài đặt dependencies
Mở terminal, di chuyển vào thư mục gốc của BE và khôi phục các packages:
```bash
cd src/SRD_Master
dotnet build SafeRide.slnx
```

### 2.2 Cấu hình Database & Secrets
Dự án yêu cầu các cấu hình nhạy cảm (Connection Strings, JWT Secret, Redis, Cloudinary, SMS, Map Keys, v.v.). **Không hard-code** vào mã nguồn. 
Sử dụng `user-secrets` (trong development) hoặc cấu hình qua biến môi trường.

Ví dụ thiết lập chuỗi kết nối DB và Redis bằng `user-secrets` cho dự án API:
```bash
cd src/SRD_Master/SafeRide.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=SafeRideDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"
```

### 2.3 Chạy Entity Framework Core Migrations
Đảm bảo SQL Server đang chạy. Tiến hành update database:
```bash
# Di chuyển vào thư mục API
cd src/SRD_Master/SafeRide.API

# Update database (Dự án migration thường nằm ở Infrastructure)
dotnet ef database update --project ../SafeRide.Infrastructure --startup-project .
```
*(Chi tiết các lệnh EF Core khác có thể xem thêm trong file `SafeRide_EFCore_Migration_Workflow.md`)*

### 2.4 Chạy Backend
Chạy project API:
```bash
cd src/SRD_Master/SafeRide.API

# Chạy mặc định
dotnet run

# Chạy với profile cụ thể (VD: cấu hình profile tên "https" trong launchSettings.json)
dotnet run --launch-profile "https"

# Chạy với môi trường cụ thể (VD: Development, Staging, Production)
dotnet run --environment "Development"
```
Mặc định API sẽ lắng nghe ở cổng được cấu hình trong `launchSettings.json`. Truy cập `https://localhost:<port>/swagger` (nếu có) để xem tài liệu API.

### 2.5 Chạy Test (Unit & Integration)
Hệ thống sử dụng các bộ test đã được tích hợp sẵn. Để chạy test:
```bash
cd src/SRD_Master

# Chạy toàn bộ Unit Tests
dotnet test SafeRide.UnitTests/SafeRide.UnitTests.csproj

# Chạy toàn bộ Integration Tests
dotnet test SafeRide.IntegrationTests/SafeRide.IntegrationTests.csproj

# Chạy test với môi trường cụ thể (thiết lập qua biến môi trường trước khi chạy)
# Windows (PowerShell):
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet test SafeRide.IntegrationTests/SafeRide.IntegrationTests.csproj

# Chạy một test class hoặc test method cụ thể (sử dụng --filter)
# Ví dụ: chỉ chạy tất cả test trong class BookingServiceTests
dotnet test SafeRide.UnitTests/SafeRide.UnitTests.csproj --filter "FullyQualifiedName~BookingServiceTests"

# Ví dụ: chỉ chạy hàm test CreateBooking_ShouldReturnSuccess
dotnet test SafeRide.UnitTests/SafeRide.UnitTests.csproj --filter "FullyQualifiedName~CreateBooking_ShouldReturnSuccess"
```

---

## 3. Thiết lập Frontend (Flutter)

Mobile app được phát triển bằng Flutter và nằm trong thư mục `src/safe_ride_flutter`. Hệ thống sử dụng Provider, GetIt, Dio.

### 3.1 Cài đặt dependencies
Mở terminal, di chuyển vào thư mục FE:
```bash
cd src/safe_ride_flutter
flutter pub get
```

### 3.2 Cấu hình Môi trường
Các thông tin cấu hình nhạy cảm hoặc thay đổi theo môi trường (như API endpoint, Google Maps/VietMap keys) thường được lưu ở file cục bộ, ví dụ `env/*.local.json` (nhớ bỏ qua các file này trong `.gitignore`).
Hãy đảm bảo bạn thiết lập API URL trỏ về Backend đang chạy (lưu ý dùng `10.0.2.2` thay cho `localhost` nếu chạy Android Emulator).

### 3.3 Chạy Ứng dụng
Kết nối thiết bị di động qua cáp hoặc mở máy ảo (Android Emulator / iOS Simulator), sau đó chạy lệnh:
```bash
cd src/safe_ride_flutter

# Chạy ứng dụng đọc cấu hình từ file env (được dùng phổ biến trong dự án này)
flutter run --dart-define-from-file=env/api_keys.local.json

# Nếu muốn chạy với chế độ release để test hiệu năng:
flutter run --release --dart-define-from-file=env/api_keys.local.json
```

### 3.4 Kiểm tra code và Chạy Test
Để đảm bảo code Flutter tuân thủ quy chuẩn và vượt qua các bài kiểm tra logic:
```bash
cd src/safe_ride_flutter

# Chạy công cụ phân tích tĩnh (static analysis)
flutter analyze

# Chạy toàn bộ Unit / Widget tests
flutter test

# Chạy một file test cụ thể
flutter test test/features/booking/booking_service_test.dart

# Chạy một test cụ thể bên trong file (dựa theo tên miêu tả của test case)
flutter test --plain-name "returns null if API fails" test/features/booking/booking_service_test.dart
```

---

## 4. Một số lưu ý quan trọng khác
- **Realtime / SignalR:** Đảm bảo Backend đang chạy ổn định để kết nối SignalR của Frontend hoạt động chính xác. Hub mapping được cấu hình ở tầng API.
- **Background Jobs (Hangfire):** Các jobs định kỳ được đăng ký ở `API` thông qua `Infrastructure`. Đảm bảo Redis và SQL Server hoạt động để Hangfire có thể điều phối jobs. Redis cũng được dùng cho GEO mapping để tìm kiếm tài xế.
- **Kiểm tra trước khi commit:** Luôn thực thi lệnh build và chạy bộ test (cả BE và FE) tương ứng với các phần code bạn vừa thay đổi trước khi commit.

---

## 5. Tổng quan cấu trúc dự án (Project Structure)

Để Dev mới dễ dàng nắm bắt, dự án được chia làm 2 phần chính:

- `src/SRD_Master/`: Chứa mã nguồn Backend (.NET 8).
  - `SafeRide.Domain`: Chứa các Entities, Enums cốt lõi (không phụ thuộc vào framework ngoài).
  - `SafeRide.Application`: Chứa Interfaces, DTOs, MediatR Handlers, và Business Logic.
  - `SafeRide.Infrastructure`: Chứa cấu hình EF Core, Identity, Redis, Hangfire, và kết nối external APIs (Cloudinary, SMS).
  - `SafeRide.Realtime`: Chứa cài đặt SignalR Hub và Notifications.
  - `SafeRide.API`: Composition root, chứa Controllers, Middleware, Swagger.
- `src/safe_ride_flutter/`: Chứa mã nguồn Mobile App (Flutter). Được tổ chức theo feature-based, sử dụng Provider/GetIt cho State Management/DI.

---

## 6. Dữ liệu mẫu & Khôi phục DB (Database Seeding)

Trong thư mục gốc của dự án có file `db13.sql`. Nếu bạn cần khởi tạo database với dữ liệu mẫu có sẵn để test nhanh mà không muốn chạy từ đầu:
1. Mở SQL Server Management Studio (SSMS) hoặc Azure Data Studio.
2. Tạo một database rỗng tên `SafeRideDb` (hoặc tên tương ứng trong Connection String).
3. Mở file `db13.sql` và thực thi (Execute) script này trên database vừa tạo.

---

## 7. Khắc phục sự cố thường gặp (Troubleshooting)

- **Không kết nối được API từ Android Emulator:** 
  Mặc định, Android Emulator không hiểu `localhost` là máy tính chủ. Bạn **phải** cấu hình URL API thành `http://10.0.2.2:<port>` hoặc `https://10.0.2.2:<port>`. 
  (Nếu dùng iOS Simulator, bạn vẫn dùng `localhost` hoặc `127.0.0.1` bình thường).
- **Lỗi chứng chỉ SSL (HTTPS) ở Local:** 
  Nếu .NET API chạy HTTPS và Mobile App bị lỗi SSL khi gọi API, bạn có thể phải cài chứng chỉ `dotnet dev-certs https --trust` ở máy chủ, hoặc tạm thời bypass SSL check ở môi trường dev trên Flutter (chỉ dùng cho môi trường Dev).
- **Không tìm thấy Driver qua tính năng Map/Matching:** 
  Đảm bảo Redis đang chạy bình thường, vì hệ thống lưu trữ vị trí tọa độ của Driver (GEO) trên Redis. Nếu Redis chết, tính năng này sẽ không hoạt động.
