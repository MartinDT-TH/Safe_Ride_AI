# SafeRide EF Core Migration Workflow

Tài liệu này chỉ hướng dẫn **chạy lệnh EF Core migration** cho backend SafeRide.

---

## 1. Chạy lệnh ở đường dẫn nào?

Mở terminal tại **thư mục root của solution**, tức là thư mục chứa file `.sln` và chứa các project:

```text
SRD_Master/
├── SafeRide.API/
├── SafeRide.Infrastructure/
├── SafeRide.Application/
├── SafeRide.Domain/
└── SafeRide.sln
```

Ví dụ trên máy của bạn có thể là:

```powershell
cd D:\Projects\Safe_Ride_AI\src\SRD_Master
```

Tất cả lệnh bên dưới nên chạy tại thư mục này.

---

## 2. Migration sẽ nằm ở đâu?

Migration sẽ nằm trong project:

```text
SafeRide.Infrastructure
```

Và thư mục migration là:

```text
SafeRide.Infrastructure/Persistence/Migrations
```

Vì vậy khi chạy lệnh EF, cần thêm option:

```bash
--project SafeRide.Infrastructure
--startup-project SafeRide.API
```

Ý nghĩa:

| Option | Ý nghĩa |
|---|---|
| `--project SafeRide.Infrastructure` | Nơi chứa `ApplicationDbContext` và nơi lưu migration |
| `--startup-project SafeRide.API` | Project chạy appsettings, DI, Program.cs để EF tạo DbContext |
| `--context ApplicationDbContext` | DbContext cần dùng để tạo migration/update database |

---

## 3. Tạo migration mới

Cú pháp:

```bash
dotnet ef migrations add <MigrationName> --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output-dir Persistence/Migrations
```

Ví dụ:

```bash
dotnet ef migrations add InitialCreate --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output-dir Persistence/Migrations
```

Sau khi chạy xong, file migration sẽ được tạo tại:

```text
SafeRide.Infrastructure/Persistence/Migrations
```

---

## 4. Update database

Sau khi tạo migration, chạy:

```bash
dotnet ef database update --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Lệnh này sẽ apply migration vào SQL Server theo connection string trong:

```text
SafeRide.API/appsettings.json
```

---

## 5. Xem danh sách migration

```bash
dotnet ef migrations list --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Dùng lệnh này để kiểm tra migration nào đã tồn tại.

---

## 6. Xóa migration cuối cùng khi chưa update database

Nếu vừa tạo migration sai và **chưa chạy database update**, dùng:

```bash
dotnet ef migrations remove --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Lệnh này chỉ xóa migration cuối cùng trong code.

---

## 7. Rollback database về migration trước đó

Nếu đã update database rồi và muốn quay về migration trước, dùng:

```bash
dotnet ef database update <PreviousMigrationName> --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Ví dụ:

```bash
dotnet ef database update InitialCreate --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Sau khi rollback database xong, nếu muốn xóa migration cuối trong code:

```bash
dotnet ef migrations remove --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

---

## 8. Drop toàn bộ database

Cẩn thận: lệnh này xóa database theo connection string hiện tại.

```bash
dotnet ef database drop --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Nếu muốn tự động xác nhận, thêm `--force`:

```bash
dotnet ef database drop --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --force
```

Sau khi drop database, tạo lại bằng:

```bash
dotnet ef database update --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

---

## 9. Tạo script SQL từ migration

Tạo script SQL từ đầu tới migration mới nhất:

```bash
dotnet ef migrations script --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output migration-script.sql
```

Tạo script idempotent, chạy được an toàn hơn trên database đã có migration history:

```bash
dotnet ef migrations script --idempotent --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output migration-script-idempotent.sql
```

---

## 10. Workflow khuyến nghị khi sửa database

### Trường hợp sửa entity hoặc DbContext

```bash
dotnet ef migrations add YourMigrationName --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output-dir Persistence/Migrations
```

```bash
dotnet ef database update --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

### Trường hợp migration sai nhưng chưa update database

```bash
dotnet ef migrations remove --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

### Trường hợp migration sai và đã update database

Bước 1: rollback database về migration trước:

```bash
dotnet ef database update <PreviousMigrationName> --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Bước 2: xóa migration cuối:

```bash
dotnet ef migrations remove --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

---

## 11. Thêm index raw sau cùng

Nếu index không cấu hình trong `ApplicationDbContext`, tạo migration riêng:

```bash
dotnet ef migrations add AddRawIndexes --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output-dir Persistence/Migrations
```

Sau đó mở file migration trong:

```text
SafeRide.Infrastructure/Persistence/Migrations
```

Thêm SQL raw cho index:

Vào db V9 -> tab Index -> tự thêm script này vào app SQL Server


## 12. Lệnh hay dùng nhất

Tạo migration vào đúng thư mục:

```bash
dotnet ef migrations add MigrationName --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --output-dir Persistence/Migrations
```

Update database:

```bash
dotnet ef database update --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Xóa migration cuối nếu chưa update database:

```bash
dotnet ef migrations remove --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Rollback database:

```bash
dotnet ef database update PreviousMigrationName --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API
```

Drop database:

```bash
dotnet ef database drop --context ApplicationDbContext --project SafeRide.Infrastructure --startup-project SafeRide.API --force
```
