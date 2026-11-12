# BookStore – Hệ thống quản lý và bán sách trực tuyến

Đồ án môn Lập trình C# – Nhóm 13 – Khoa CNTT, Trường Đại học Sài Gòn.

| Thành phần | Công nghệ |
|---|---|
| Web khách hàng | ASP.NET Core MVC (.NET 8), Bootstrap 5 |
| Desktop quản trị | Windows Forms (.NET 8) |
| Cơ sở dữ liệu | Microsoft SQL Server (một database dùng chung) |
| Truy cập dữ liệu | Entity Framework Core 8 (Code-First + Migrations) |
| Kiểm thử | xUnit (30 test, SQLite in-memory) |

## 1. Cấu trúc solution

```
BookStore.sln
├── src/
│   ├── BookStore.Domain     Entity, Enum, quy tắc trạng thái đơn hàng
│   ├── BookStore.Data       DbContext, cấu hình bảng (PK/FK/UNIQUE/CHECK), Migrations, dữ liệu mẫu
│   ├── BookStore.Business   Service nghiệp vụ, DTO, kiểm tra dữ liệu, phân quyền
│   ├── BookStore.Web        Controller, View, đăng nhập Cookie – dành cho khách hàng
│   └── BookStore.Admin      Windows Forms – dành cho Quản trị viên / Nhân viên
├── tests/BookStore.Tests    Unit test các nghiệp vụ quan trọng
├── database/                Script SQL tạo CSDL (xuất từ Migrations)
└── docs/                    Kế hoạch triển khai, phân tích yêu cầu
```

Tham chiếu một chiều: `Web, Admin → Business → Data → Domain`.
Web và Admin đều gọi `services.AddBookStore(connectionString)` → **cùng tầng nghiệp vụ, cùng một database**.

## 2. Chạy trên Windows

### Yêu cầu
- **Visual Studio 2022** (bản 17.8 trở lên) với 2 workload:
  - *ASP.NET and web development*
  - *.NET desktop development*
- **SQL Server**: LocalDB được cài kèm Visual Studio là đủ. Có thể dùng SQL Server Express/Developer (xem mục 3).

### Các bước
1. Mở `BookStore.sln` bằng Visual Studio.
2. Chuột phải Solution → **Configure Startup Projects…** → chọn **Multiple startup projects** → đặt `BookStore.Web` và `BookStore.Admin` là **Start**.
3. Nhấn **F5**.
   - Lần chạy đầu, ứng dụng tự tạo database `BookStoreDb` (Migrations) và nạp dữ liệu mẫu (30 đầu sách, 7 tài khoản, ~35 đơn hàng trong 30 ngày qua).
   - Mở app nào trước cũng được, cả hai đều tự khởi tạo database nếu chưa có.

Hoặc chạy bằng dòng lệnh (mở 2 cửa sổ terminal tại thư mục gốc):
```bash
dotnet run --project src/BookStore.Web
dotnet run --project src/BookStore.Admin
```

### Tài khoản mẫu

| Ứng dụng | Tên đăng nhập | Mật khẩu | Vai trò |
|---|---|---|---|
| Desktop | `admin` | `Admin@123` | Quản trị viên (toàn quyền) |
| Desktop | `nhanvien` | `Nhanvien@123` | Nhân viên |
| Web | `khachhang1` … `khachhang4` | `Khach@123` | Khách hàng |

Mã giảm giá mẫu: `GIAM10` (giảm 10%, tối đa 50.000đ, đơn từ 150.000đ), `SACH30K` (giảm 30.000đ, đơn từ 200.000đ), `HETHAN` (đã hết hạn, để demo lỗi).

## 3. Đổi SQL Server

Sửa chuỗi kết nối `ConnectionStrings:BookStore` ở **cả hai** file (phải giống nhau):
- `src/BookStore.Web/appsettings.json`
- `src/BookStore.Admin/appsettings.json`

```jsonc
// LocalDB (mặc định)
"Server=(localdb)\\MSSQLLocalDB;Database=BookStoreDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
// SQL Server Express
"Server=.\\SQLEXPRESS;Database=BookStoreDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
// SQL Server trên máy khác (Web và Desktop chạy 2 máy, chung 1 DB)
"Server=192.168.1.10,1433;Database=BookStoreDb;User Id=sa;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Xem CSDL: mở **SQL Server Management Studio**, kết nối `(localdb)\MSSQLLocalDB`, mở database `BookStoreDb`. Có thể tạo **Database Diagram** để chụp hình cho báo cáo.

Muốn làm lại dữ liệu mẫu từ đầu: xóa database `BookStoreDb` trong SSMS rồi chạy lại ứng dụng.

## 4. Kịch bản demo liên kết Web – Desktop – SQL Server

1. **Desktop** đăng nhập `admin` → **Sách** → **+ Thêm sách**: nhập tên, ISBN hợp lệ (vd `9786041234567`), chọn thể loại, NXB, tác giả, giá, *tồn kho ban đầu = 5*, chọn ảnh bìa → Lưu.
2. **Web** (khách chưa đăng nhập) → tìm tên sách vừa thêm (tìm không dấu cũng được) → sách xuất hiện kèm ảnh bìa.
3. Thêm vào giỏ → **Tiến hành đặt hàng** → đăng nhập `khachhang1` (giỏ hàng được giữ lại) → nhập mã `GIAM10` → **Xác nhận đặt hàng**.
4. **Desktop**: trong vòng 15 giây xuất hiện thông báo *"Có đơn hàng mới #…"*, menu Đơn hàng hiện số đơn chờ. Vào **Kho** thấy tồn kho đã giảm, **Nhật ký nhập/xuất** có dòng *Xuất bán*.
5. **Desktop**: mở đơn → **Xác nhận đơn** → **Bắt đầu giao hàng** → **Giao thành công**.
6. **Web**: trang chi tiết đơn tự làm mới, thanh tiến trình chuyển trạng thái. Khi đơn hoàn tất, khách viết được đánh giá cho sách.
7. **Desktop** → **Dashboard / Thống kê**: doanh thu tăng → **Xuất Excel**.

Demo thêm:
- Khóa khách hàng trên Desktop → khách bị đăng xuất ở lần tải trang kế tiếp.
- Đặt quá số lượng tồn → bị chặn.
- Nhân viên (`nhanvien`) không thấy menu *Tài khoản & phân quyền* và *Mã giảm giá*.

## 5. Kiểm thử

```bash
dotnet test
```
30 test gồm: đặt hàng trừ kho và ghi sổ kho, **không bán vượt tồn khi hai khách đặt cùng lúc**, rollback khi một dòng hết hàng, mã giảm giá (trần giảm, hết lượt, hết hạn), luồng trạng thái đơn, hủy đơn hoàn kho đúng một lần, quyền khách/nhân viên/admin, đăng nhập (sai mật khẩu, bị khóa, sai kênh), đăng ký trùng email/SĐT, phiếu nhập, điều chỉnh kho, ISBN, và kiểm tra dữ liệu mẫu có tồn kho khớp với sổ kho.

## 6. Quy tắc nghiệp vụ chính

- **Trừ kho khi đặt hàng**, trong một transaction, bằng câu lệnh `UPDATE Books SET Stock = Stock - @q WHERE Id = @id AND Stock >= @q`. Nếu không đủ hàng thì rollback toàn bộ đơn.
- **Trạng thái đơn**: Chờ xác nhận → Đã xác nhận → Đang giao → Hoàn tất. Được hủy khi đơn ở *Chờ xác nhận* hoặc *Đã xác nhận*, hủy thì hoàn kho. Khách chỉ tự hủy được khi đơn còn *Chờ xác nhận*.
- **Chuyển khoản**: nhân viên phải xác nhận đã nhận tiền thì mới chuyển sang giao hàng. **COD**: tự ghi nhận đã thanh toán khi giao thành công.
- **Doanh thu**: chỉ tính đơn *Hoàn tất*, theo ngày hoàn tất. Doanh thu sách = tiền sách − giảm giá, phí vận chuyển tách riêng.
- **Bảo toàn lịch sử**: sách, thể loại, nhà cung cấp, mã giảm giá đã có giao dịch thì chỉ được ẩn hoặc ngừng, không xóa cứng. Giá bán và địa chỉ được chốt vào đơn tại thời điểm đặt.
- **Mật khẩu** băm bằng BCrypt. **Phân quyền** được kiểm tra ở tầng Service, không chỉ ẩn nút trên giao diện.
- Miễn phí vận chuyển cho đơn từ 300.000đ (sau giảm giá), dưới mức này phí 25.000đ.
