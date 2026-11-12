# KẾ HOẠCH TRIỂN KHAI – HỆ THỐNG QUẢN LÝ VÀ BÁN SÁCH TRỰC TUYẾN

> Tài liệu phân tích giai đoạn 1 (chưa viết code), dựa trên *Tài liệu thống nhất yêu cầu* và *Báo cáo C#.docx* hiện tại.
> Ngày lập: 11/11/2026 – Nhóm 13.

---

## A. TÓM TẮT YÊU CẦU CHÍNH THỨC

### A.1. Yêu cầu BẮT BUỘC (vi phạm = bị giới hạn điểm)

| # | Yêu cầu | Hậu quả nếu thiếu |
|---|---|---|
| B1 | Có ứng dụng **Web ASP.NET Core MVC** (.NET 8) cho khách hàng | Tối đa 6,0 |
| B2 | Có ứng dụng **Desktop Windows Forms** (.NET 8) cho quản trị | Tối đa 6,0 |
| B3 | **Một** database **SQL Server** dùng chung cho cả hai | Tối đa 7,0 / 6,5 |
| B4 | Sản phẩm **chạy và demo được** | Tối đa 5,0 |
| B5 | Chức năng chính **xử lý thật** (không chỉ giao diện, không dữ liệu giả) | Tối đa 4,0 |
| B6 | Nộp mã nguồn | Tối đa 4,0 |
| B7 | Kiến trúc phân lớp: Domain / Data / Business / Web / Admin / Tests | Mất điểm tiêu chí 5 |
| B8 | EF Core, DI, nghiệp vụ không viết trong Controller/Form | Mất điểm tiêu chí 5 |
| B9 | CSDL có PK, FK, đạt 3NF | Mất điểm tiêu chí 2 |
| B10 | Báo cáo đủ 17 mục, có hình chụp chạy thực tế | Mất điểm tiêu chí 7 |

### A.2. Yêu cầu KHUYẾN KHÍCH

- Web API (chỉ làm nếu thật sự cần – **đề xuất: KHÔNG làm**, vì Web và Desktop đã dùng chung tầng Business, không cần API).
- xUnit cho các nghiệp vụ quan trọng (**đề xuất: CÓ làm** – rẻ, ăn điểm tiêu chí 5 và 15. Kiểm thử trong báo cáo).
- Các bảng "nếu cần": UserRoles, BookImages, CustomerAddresses, Warehouses, StockTransactions.
- Chức năng mở rộng: giỏ hàng lưu DB, đánh giá, khuyến mãi, mã giảm giá.

### A.3. Quy trình bắt buộc phải demo được (Phần 7)

Admin thêm sách trên Desktop → sách xuất hiện trên Web → khách đặt mua → đơn hiện trên Desktop → admin xác nhận → trạng thái cập nhật trên Web.
**Đây là kịch bản demo số 1, phải chạy trơn tru 100%.**

---

## B. KIẾN TRÚC HỆ THỐNG ĐỀ XUẤT

### B.1. Cấu trúc Solution

```
BookStore.sln
├── src/
│   ├── BookStore.Domain      (class library, net8.0)        – Entity, Enum, hằng số nghiệp vụ
│   ├── BookStore.Data        (class library, net8.0)        – BookStoreDbContext, Configurations, Migrations, Seed
│   ├── BookStore.Business    (class library, net8.0)        – Services, DTO, Validation, Result<T>, PasswordHasher
│   ├── BookStore.Web         (ASP.NET Core MVC, net8.0)     – Controllers, Views, ViewModels, Cookie Auth
│   └── BookStore.Admin       (WinForms, net8.0-windows)     – Forms, UserControls, DI Host
└── tests/
    └── BookStore.Tests       (xUnit, net8.0)                – test Service với SQLite in-memory / SQL Server test DB
```

Quan hệ tham chiếu (một chiều, không vòng):

```
Web ──┐
      ├──> Business ──> Data ──> Domain
Admin ┘                  │
Tests ───────────────────┘
```

### B.2. Quyết định kỹ thuật (chọn cái đơn giản, giải thích được)

| Vấn đề | Lựa chọn | Lý do |
|---|---|---|
| Truy cập dữ liệu | EF Core 8 + SQL Server, **không** thêm Repository chung chung | DbContext đã là Unit of Work + Repository; thêm lớp bọc chỉ làm dài code, khó giải thích |
| Xác thực Web | **Cookie Authentication tự viết** + BCrypt.Net-Next | ASP.NET Identity sinh ~7 bảng AspNet* riêng, khó dùng chung với Desktop |
| Xác thực Desktop | `AuthService.Login()` dùng chung, lưu `CurrentUser` trong một `SessionContext` singleton | Cùng một bảng Users, cùng hàm băm mật khẩu |
| Phân quyền | Cột `Role` (Admin / Staff / Customer) trong `Users` | Mỗi người 1 vai trò → không cần bảng UserRoles (tránh bảng thừa) |
| DI cho WinForms | `Host.CreateApplicationBuilder()` trong `Program.cs`, mở Form qua `IServiceProvider`; mỗi Form tạo 1 `IServiceScope` | DbContext có vòng đời ngắn, tránh dữ liệu cũ |
| Ảnh bìa sách | Lưu trong DB, bảng `BookImages` (varbinary) – Web trả ảnh qua `/images/book/{id}` | Desktop và Web có thể chạy trên **máy khác nhau** mà vẫn thấy ảnh. Không phụ thuộc thư mục chung |
| Trừ tồn kho | Trừ **ngay khi đặt hàng**, trong transaction, bằng câu lệnh có điều kiện `UPDATE Books SET Stock = Stock - @q WHERE Id = @id AND Stock >= @q` (EF Core `ExecuteUpdate`) | Chống bán vượt tồn khi 2 khách đặt cùng lúc – dễ giải thích khi vấn đáp |
| Đơn mới hiện trên Desktop | `System.Windows.Forms.Timer` làm mới danh sách đơn mỗi 15 giây + badge "đơn chờ xử lý" | Đơn giản, không cần SignalR. Dữ liệu vốn đã chung DB |
| Xung đột cập nhật | `RowVersion` trên `Books` và `Orders` | Hai nhân viên sửa cùng lúc → báo lỗi thay vì ghi đè |
| Kết quả nghiệp vụ | `Result` / `Result<T>` (Success, Error message) trả từ Service | Controller và Form chỉ hiển thị, không chứa logic |
| Cấu hình | `appsettings.json` ở cả Web và Admin, cùng 1 ConnectionString | Bằng chứng "dùng chung DB" |
| Migrations | Chỉ đặt ở `BookStore.Data`, chạy tự động khi khởi động Web (`Database.Migrate()`) + seed dữ liệu mẫu thật | Máy chấm chỉ cần chạy là có DB |

---

## C. DANH SÁCH CHỨC NĂNG WEB (khách hàng)

Mức độ: **[BB]** bắt buộc theo đặc tả – **[MR]** mở rộng / sáng tạo.

| Mã | Chức năng | Mức | Ghi chú triển khai |
|---|---|---|---|
| W1 | Trang chủ: banner, sách nổi bật, bán chạy, sách mới, menu danh mục | BB | "Bán chạy" = tính từ OrderDetails của đơn Completed. "Nổi bật" = cờ `IsFeatured` |
| W2 | Danh sách sách theo danh mục, phân trang | BB | 12 sách/trang |
| W3 | Tìm kiếm theo tên, tác giả, danh mục; lọc theo giá, NXB; sắp xếp | BB | 1 form lọc dùng chung cho W2 & W3 |
| W4 | Chi tiết sách: ảnh, giá, tác giả, NXB, mô tả, tồn kho, đánh giá | BB | Hết hàng → ẩn nút "Thêm vào giỏ" |
| W5 | Đăng ký / Đăng nhập / Đăng xuất | BB | Mật khẩu BCrypt, kiểm tra trùng Email/SĐT |
| W6 | Hồ sơ cá nhân + đổi mật khẩu | BB | |
| W7 | Sổ địa chỉ nhận hàng (thêm/sửa/xóa, đặt mặc định) | BB | Bảng CustomerAddresses |
| W8 | Giỏ hàng: thêm, xóa, cập nhật số lượng, tổng tiền, kiểm tra tồn | BB | Lưu DB theo User. Khách chưa đăng nhập dùng Session rồi gộp khi đăng nhập |
| W9 | Đặt hàng: chọn địa chỉ, phương thức thanh toán (COD / Chuyển khoản), tính tổng, tạo đơn | BB | Transaction + trừ tồn có điều kiện. Kiểm tra lại giá & tồn phía server |
| W10 | Lịch sử đơn hàng + chi tiết đơn + trạng thái (timeline) | BB | Thấy ngay khi Desktop cập nhật |
| W11 | Khách tự hủy đơn khi còn "Chờ xác nhận" | MR | Hoàn tồn kho trong transaction |
| W12 | Áp mã giảm giá khi thanh toán | MR | |
| W13 | Đánh giá sách (1–5 sao) – **chỉ khi đã mua và đơn hoàn tất** | MR | Hợp lý nghiệp vụ, ăn điểm sáng tạo |
| W14 | Giao diện responsive, thông báo toast thành công/lỗi, trạng thái loading | BB | Bootstrap 5 |

## D. DANH SÁCH CHỨC NĂNG DESKTOP (quản trị)

| Mã | Chức năng | Quyền | Mức |
|---|---|---|---|
| D1 | Đăng nhập, chặn tài khoản bị khóa, chặn tài khoản Customer | Admin, Staff | BB |
| D2 | Dashboard: tổng sách, đơn, khách, doanh thu tháng, đơn chờ xử lý, sách sắp hết, biểu đồ doanh thu 7/30 ngày | Admin, Staff | BB |
| D3 | Quản lý sách: CRUD, tìm kiếm, lọc, chọn ảnh bìa, nhiều tác giả, ẩn/hiện | Admin, Staff | BB |
| D4 | Quản lý danh mục: CRUD, tìm kiếm, không xóa khi đang có sách (chỉ ẩn) | Admin, Staff | BB |
| D5 | Quản lý tác giả: CRUD, tìm kiếm | Admin, Staff | BB |
| D6 | Quản lý NXB: CRUD, tìm kiếm | Admin, Staff | BB |
| D7 | Quản lý khách hàng: danh sách, tìm kiếm, xem chi tiết + lịch sử mua, khóa/mở khóa | Admin, Staff | BB |
| D8 | Quản lý đơn hàng: danh sách lọc theo trạng thái, chi tiết, xác nhận, chuyển trạng thái, hủy (nhập lý do, hoàn tồn), tự làm mới | Admin, Staff | BB |
| D9 | Quản lý kho: xem tồn, **phiếu nhập** (NCC, nhiều dòng sách), **điều chỉnh kho** có lý do, lịch sử nhập/xuất, cảnh báo sắp hết | Admin, Staff | BB |
| D10 | Quản lý nhà cung cấp (CRUD đơn giản) | Admin, Staff | BB (phục vụ D9) |
| D11 | Quản lý tài khoản nội bộ: tạo, sửa, khóa/mở, đặt lại mật khẩu, phân quyền Admin/Staff; không tự khóa/hạ quyền chính mình | **Admin** | BB |
| D12 | Thống kê: doanh thu theo ngày/tháng, số đơn theo trạng thái, top sách bán chạy, tồn kho theo danh mục | Admin, Staff | BB |
| D13 | Quản lý mã giảm giá | Admin | MR |
| D14 | Xuất báo cáo ra Excel (ClosedXML) | Admin, Staff | MR |
| D15 | Ẩn menu theo quyền (Staff không thấy D11, D13) + kiểm tra quyền lại ở Service | — | BB |

---

## E. THIẾT KẾ CƠ SỞ DỮ LIỆU DỰ KIẾN

Giữ phần lớn thiết kế 19 bảng trong báo cáo (đã rất tốt). Sửa các điểm chưa khớp đặc tả, chuyển tên sang tiếng Anh cho thống nhất với code C#/EF (báo cáo ghi kèm tên tiếng Việt).

### E.1. Danh sách bảng (18 bảng)

| Nhóm | Bảng | Khóa / ghi chú chính |
|---|---|---|
| Tài khoản | **Users** | Id PK; Username UQ; Email UQ; Phone UQ; PasswordHash; FullName; Role (Admin/Staff/Customer); IsActive; CreatedAt |
| | **CustomerAddresses** | Id PK; UserId FK; ReceiverName; Phone; AddressLine; Ward; District; Province; IsDefault |
| Sách | **Categories** | Id PK; Name UQ; Description; IsHidden |
| | **Authors** | Id PK; Name; Bio |
| | **Publishers** | Id PK; Name UQ; Address; Phone |
| | **Books** | Id PK; CategoryId FK; PublisherId FK; Isbn UQ; Title; PublishYear; Pages; CoverType; Description; Price; Stock; LowStockThreshold; IsFeatured; IsHidden; CreatedAt; RowVersion |
| | **BookAuthors** | (BookId, AuthorId) PK ghép, FK |
| | **BookImages** | BookId PK+FK (1–1); Data varbinary(max); ContentType |
| Kho | **Suppliers** | Id PK; Name; Phone; Email; Address |
| | **ImportReceipts** | Id PK; SupplierId FK; StaffId FK→Users; ImportDate; Note |
| | **ImportReceiptDetails** | (ReceiptId, BookId) PK; Quantity > 0; UnitCost ≥ 0 |
| | **StockTransactions** | Id PK; BookId FK; StaffId FK NULL; Type (Import/Sale/Cancel/Adjust); QuantityChange; StockAfter; RefId; Reason; CreatedAt – *sổ nhật ký nhập/xuất kho* |
| Giỏ hàng | **CartItems** | (UserId, BookId) PK; Quantity; AddedAt – *bỏ bảng Carts vì mỗi user 1 giỏ → bảng Carts dư thừa* |
| Đơn hàng | **Coupons** | Id PK; Code UQ; DiscountType; Value; MaxDiscount NULL; MinOrder; StartAt; EndAt; UsageLimit; UsedCount; IsActive |
| | **Orders** | Id PK; CustomerId FK; HandledById FK NULL; CouponId FK NULL; OrderDate; CompletedAt NULL; ReceiverName; ReceiverPhone; ShippingAddress; PaymentMethod (COD/BankTransfer); Status; Subtotal; DiscountAmount; ShippingFee; CancelReason NULL; RowVersion |
| | **OrderDetails** | (OrderId, BookId) PK; Quantity; UnitPrice |
| | **Payments** | Id PK; OrderId FK; Amount; Status (Pending/Paid/Failed); PaidAt NULL; ReferenceCode NULL |
| Mở rộng | **Reviews** | (UserId, BookId) PK; Rating 1–5; Comment; CreatedAt |

**Ghi chú 3NF (đưa vào báo cáo mục 11):**
- `OrderDetails.UnitPrice` và `Orders.ShippingAddress` **không phải dư thừa**: đây là giá trị *chốt tại thời điểm đặt*. Sau này đổi giá sách hoặc đổi địa chỉ thì lịch sử đơn vẫn đúng.
- `Orders.Subtotal` là giá trị chốt (được tính một lần trong transaction), không cập nhật lại → chấp nhận được, giải thích là *snapshot*.
- `Books.Stock` là giá trị dẫn xuất từ StockTransactions. Giữ lại để truy vấn nhanh, và **mọi thay đổi Stock đều đi kèm 1 dòng StockTransactions trong cùng transaction** → giải thích rõ là phi chuẩn hóa có kiểm soát.
- Bỏ bảng `PaymentMethods` của bản cũ, thay bằng enum (chỉ 2 giá trị cố định). Bỏ `Carts`. Gộp `DieuChinhKho` vào `StockTransactions` → **18 bảng**.

### E.2. Trạng thái đơn hàng (thống nhất cho Web, Desktop, báo cáo)

```
Pending (Chờ xác nhận) ──> Confirmed (Đã xác nhận) ──> Shipping (Đang giao) ──> Completed (Hoàn tất)
     │                          │
     └──────────> Cancelled (Đã hủy) <──────┘
```
- Tồn kho trừ khi **đặt hàng** (Pending). Hủy → hoàn tồn + ghi StockTransactions (Type = Cancel).
- Khách tự hủy được khi Pending. Nhân viên hủy được khi Pending/Confirmed.
- Completed → ghi CompletedAt. Đơn COD → Payment chuyển Paid.
- Doanh thu chỉ tính đơn Completed theo CompletedAt.

---

## F. DANH SÁCH CÔNG VIỆC THEO THỨ TỰ TRIỂN KHAI

Nguyên tắc: **làm xong luồng Phần 7 (end-to-end) càng sớm càng tốt**, sau đó mới làm rộng ra.

| Giai đoạn | Công việc | Kết quả nghiệm thu |
|---|---|---|
| **0. Môi trường** (½ ngày) | Chốt máy chạy Windows + SQL Server (xem H1); tạo Git repo riêng cho đồ án; tạo Solution 6 project | `dotnet build` thành công |
| **1. Nền tảng** (1–2 ngày) | Domain entities, DbContext + Fluent configuration, Migration đầu tiên, Seed (5 danh mục, 30+ sách thật có ảnh, 2 admin/staff, 3 khách) | DB tạo được bằng 1 lệnh |
| **2. Business lõi** (2 ngày) | AuthService, BookService, CategoryService, CartService, **OrderService (đặt hàng + chuyển trạng thái + hủy)**, InventoryService | Unit test cho đặt hàng, trừ tồn, hủy hoàn tồn đều xanh |
| **3. Luồng end-to-end tối thiểu** (2 ngày) | Desktop: Login, CRUD Sách, Danh sách + xác nhận đơn. Web: Trang chủ, chi tiết, đăng ký/đăng nhập, giỏ, đặt hàng, lịch sử đơn | **Chạy được kịch bản Phần 7** ✅ (mốc an toàn ≈ 7đ) |
| **4. Hoàn thiện Desktop** (3 ngày) | Dashboard, Danh mục, Tác giả, NXB, NCC, Khách hàng, Kho (phiếu nhập, điều chỉnh, lịch sử, cảnh báo), Tài khoản & phân quyền, Thống kê | Đủ D1–D12, D15 |
| **5. Hoàn thiện Web** (3 ngày) | Tìm kiếm/lọc/phân trang, hồ sơ, sổ địa chỉ, theo dõi đơn timeline, responsive, thông báo | Đủ W1–W10, W14 |
| **6. Mở rộng** (2 ngày) | Mã giảm giá (W12, D13), Đánh giá (W13), Khách tự hủy (W11), Xuất Excel (D14) | Có ít nhất 3 chức năng sáng tạo chạy thật |
| **7. Kiểm thử** (1 ngày) | Bảng test case thủ công (~40 ca), unit test Service, test đặt hàng đồng thời | Bảng kết quả kiểm thử cho báo cáo |
| **8. Báo cáo** (2–3 ngày) | Sửa nội dung mâu thuẫn, bổ sung 7 mục còn thiếu, chụp màn hình thực tế | Đủ 17 mục |
| **9. Tập dượt** (1 ngày) | Kịch bản demo 10 phút, phân công vấn đáp theo module, cài đặt trên máy demo từ đầu | Demo không lỗi 3 lần liên tiếp |

---

## G. ĐỐI CHIẾU YÊU CẦU VỚI RUBRIC 10 ĐIỂM

| # | Tiêu chí (điểm) | Phải có để đạt tối đa | Hiện trạng | Việc cần làm |
|---|---|---|---|---|
| 1 | Phân tích & thiết kế (1,0) | Use case, đặc tả, class diagram, sequence **khớp với code** | Đã có nhiều, còn mâu thuẫn (tồn kho, trạng thái đơn, trường form) | Sửa theo mục E.2; thêm UC: quản lý tác giả/NXB/NCC, địa chỉ, lịch sử đơn của khách, đánh giá, mã giảm giá, dashboard |
| 2 | Cơ sở dữ liệu (1,0) | PK/FK, ERD, **phân tích 3NF**, chạy thật trên SQL Server | Thiết kế tốt, thiếu phần chứng minh 3NF | Cập nhật theo E.1; viết mục 3NF; chụp Database Diagram từ SSMS |
| 3 | Desktop (1,5) | D1–D12, D15 chạy thật, CRUD thống nhất, validate, xác nhận xóa, xử lý lỗi | Chưa có code | Giai đoạn 3–4 |
| 4 | Web (1,5) | W1–W10, W14 chạy thật, responsive | Chưa có code | Giai đoạn 3, 5 |
| 5 | C# & kiến trúc (1,0) | 6 project phân lớp, DI, Service, không logic trong Form/Controller, có test | — | Giai đoạn 0–2, 7 |
| 6 | Tích hợp (1,0) | Kịch bản Phần 7 chạy trơn tru, dữ liệu đồng bộ, không bán vượt tồn | — | Giai đoạn 3 – ưu tiên số 1 |
| 7 | Báo cáo (1,0) | Đủ 17 mục, hình thật, nhất quán | Có ~6/17 mục | Giai đoạn 8 (xem bảng dưới) |
| 8 | Demo & vấn đáp (1,0) | Mỗi thành viên giải thích được phần mình | — | Giai đoạn 9; code đơn giản, có comment chỗ khó |
| 9 | Sáng tạo (1,0) | Chức năng mở rộng **có giá trị thật** | — | W11–W13, D13–D14, cảnh báo tồn, dashboard biểu đồ, chống bán vượt tồn đồng thời |

**Đối chiếu 17 mục báo cáo:**

| Mục | Có chưa | Mục | Có chưa |
|---|---|---|---|
| 1 Trang bìa | ✅ | 10 ERD | ✅ (cần cập nhật) |
| 2 Mục lục | ⚠️ sai số trang | 11 Chuẩn hóa 3NF | ❌ |
| 3 Giới thiệu, mục tiêu | ✅ | 12 Kiến trúc hệ thống | ❌ |
| 4 Khảo sát | ✅ | 13 Kết quả Web | ❌ (chụp sau khi code) |
| 5 Phân tích hệ thống | ✅ | 14 Kết quả Desktop | ❌ (chụp sau khi code) |
| 6 Phân tích chức năng | ✅ (cần sửa) | 15 Kiểm thử | ❌ |
| 7 Thiết kế giao diện Web | ❌ | 16 Kết luận, hướng phát triển | ❌ |
| 8 Thiết kế giao diện Desktop | ❌ | 17 Tài liệu tham khảo | ❌ |
| 9 Thiết kế CSDL | ✅ (cần cập nhật) | | |

---

## H. RỦI RO VÀ GIẢI PHÁP

| # | Rủi ro | Mức | Giải pháp |
|---|---|---|---|
| **H1** | **Máy đang dùng là macOS (Apple Silicon): Windows Forms KHÔNG chạy được trên Mac** → nguy cơ không demo được Desktop (giới hạn 6,0 / 5,0) | **Rất cao** | Code và build được trên Mac (`EnableWindowsTargeting`), nhưng **chạy/demo phải trên Windows**: máy thành viên khác, hoặc Windows 11 ARM qua Parallels/UTM. Nên chốt **1 laptop Windows chạy cả Web + Desktop + SQL Server** khi bảo vệ |
| H2 | SQL Server không có bản native cho Mac | Cao | Trên Mac: Docker image `mcr.microsoft.com/mssql/server:2022-latest` (bật Rosetta trong Docker Desktop). Khi demo: SQL Server Express/Developer trên Windows |
| H3 | Ảnh bìa thêm từ Desktop không hiện trên Web | Cao | Lưu ảnh trong DB (BookImages), không dùng đường dẫn file |
| H4 | Bán vượt tồn kho khi đặt đồng thời | Trung bình | UPDATE có điều kiện + transaction + unit test |
| H5 | Desktop không thấy đơn mới nếu không bấm tải lại | Trung bình | Timer tự làm mới 15s + nút Làm mới + badge số đơn chờ |
| H6 | Báo cáo không khớp sản phẩm (giảng viên rất hay hỏi) | Cao | Cập nhật báo cáo **sau** khi code xong; dùng đúng tên bảng, trạng thái, màn hình thật |
| H7 | Thành viên không giải thích được code | Cao | Không dùng thư viện/mẫu khó (CQRS, MediatR, AutoMapper, Repository generic). Chia module theo người, mỗi người tự chạy và giải thích |
| H8 | Máy chấm/máy demo thiếu DB, lỗi connection string | Trung bình | Tự migrate + seed khi chạy; README hướng dẫn cài 5 bước; thử cài lại từ đầu trên máy sạch |
| H9 | Repo Git hiện tại là toàn bộ thư mục `Documents` | Trung bình | Tạo repo riêng cho đồ án, đẩy lên GitHub, mỗi thành viên commit phần mình (minh chứng làm nhóm) |
| H10 | Dữ liệu mẫu nghèo nàn làm giao diện xấu | Thấp | Seed ≥ 30 sách thật, có ảnh bìa, ≥ 20 đơn rải nhiều ngày để biểu đồ có số liệu |

---

## I. CHECKLIST NGHIỆM THU TRƯỚC KHI NỘP

### Điều kiện chặn điểm (phải ✅ hết)
- [ ] Web ASP.NET Core MVC chạy được
- [ ] Desktop Windows Forms chạy được trên máy demo
- [ ] Cả hai trỏ tới **cùng một** database SQL Server (cùng connection string)
- [ ] Kịch bản Phần 7 chạy trọn vẹn trước mặt người chấm
- [ ] Không còn màn hình "chỉ có giao diện" hoặc dữ liệu cứng
- [ ] Mã nguồn đã đẩy lên GitHub và nén nộp kèm

### Kỹ thuật
- [ ] Solution đủ 6 project, tham chiếu một chiều
- [ ] Không có truy vấn DbContext trong Controller/Form
- [ ] DI dùng ở cả Web và Desktop
- [ ] Mật khẩu băm BCrypt, không lưu mật khẩu gốc
- [ ] Staff không vào được chức năng của Admin (kiểm tra cả ở Service)
- [ ] Khách không đăng nhập được Desktop; Admin/Staff không mua hàng trên Web
- [ ] Đặt hàng trong transaction, không bán vượt tồn (có test)
- [ ] Hủy đơn hoàn tồn đúng 1 lần
- [ ] Xóa có hộp thoại xác nhận; dữ liệu đã có giao dịch thì chỉ ẩn
- [ ] Tất cả ô nhập có validate + thông báo lỗi rõ ràng
- [ ] Không có crash khi mất kết nối DB (bắt ngoại lệ, báo lỗi thân thiện)
- [ ] Web responsive trên điện thoại (kiểm tra bằng DevTools)
- [ ] Unit test chạy xanh

### Báo cáo
- [ ] Đủ 17 mục theo Phần 10
- [ ] Mục lục, số bảng, số hình tự động và đúng
- [ ] Use case, ERD, tên bảng, trạng thái đơn **khớp với code**
- [ ] Có ảnh chụp mọi màn hình chính của Web và Desktop
- [ ] Có bảng kiểm thử với kết quả thực tế
- [ ] Có kết luận, hướng phát triển, tài liệu tham khảo

### Demo
- [ ] Kịch bản demo 10 phút đã tập ≥ 3 lần
- [ ] Mỗi thành viên giải thích được: kiến trúc, 1 luồng nghiệp vụ, cách Web–Desktop dùng chung DB
- [ ] Đã cài lại từ đầu trên máy demo theo README và chạy được
