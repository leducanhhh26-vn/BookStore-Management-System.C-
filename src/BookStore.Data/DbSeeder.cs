using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Data;

/// <summary>
/// Nạp dữ liệu mẫu ban đầu (chỉ chạy khi bảng Users còn trống).
/// Tồn kho được tính đúng theo nghiệp vụ: nhập hàng → bán → hoàn kho, và mọi biến động
/// đều có dòng StockTransactions tương ứng, để số liệu trên Dashboard/Thống kê nhất quán.
///
/// Tài khoản mẫu:
///   admin      / Admin@123     (Quản trị viên – Desktop)
///   nhanvien   / Nhanvien@123  (Nhân viên – Desktop)
///   khachhang1 / Khach@123     (Khách hàng – Web)
/// </summary>
public static class DbSeeder
{
    private record BookSeed(string Title, string[] Authors, string Category, string Publisher,
        int Year, int Pages, decimal Price, string Cover, string Description);

    public static async Task SeedAsync(BookStoreDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var rnd = new Random(2026);
        var now = DateTime.Now;

        // ---------------- Tài khoản ----------------
        var admin = NewUser("admin", "Admin@123", "Lê Đức Anh", "admin@bookstore.vn", "0901000001", UserRole.Admin);
        var staff1 = NewUser("nhanvien", "Nhanvien@123", "Nguyễn Vĩnh Thịnh", "thinh.nv@bookstore.vn", "0901000002", UserRole.Staff);
        var staff2 = NewUser("nhanvien2", "Nhanvien@123", "Tạ Văn Đức Khanh", "khanh.tvd@bookstore.vn", "0901000003", UserRole.Staff);
        var customers = new List<User>
        {
            NewUser("khachhang1", "Khach@123", "Trần Minh Khoa", "khoa.tran@gmail.com", "0912345678", UserRole.Customer),
            NewUser("khachhang2", "Khach@123", "Phạm Thu Hà", "ha.pham@gmail.com", "0987654321", UserRole.Customer),
            NewUser("khachhang3", "Khach@123", "Võ Quốc Bảo", "bao.vo@gmail.com", "0934567890", UserRole.Customer),
            NewUser("khachhang4", "Khach@123", "Đặng Ngọc Lan", "lan.dang@gmail.com", "0978123456", UserRole.Customer),
        };
        var addressSeeds = new[]
        {
            ("273 An Dương Vương", "Phường 3", "Quận 5", "TP. Hồ Chí Minh"),
            ("12 Nguyễn Trãi", "Phường Bến Thành", "Quận 1", "TP. Hồ Chí Minh"),
            ("45 Lê Lợi", "Phường Thạch Thang", "Quận Hải Châu", "Đà Nẵng"),
            ("88 Cầu Giấy", "Phường Quan Hoa", "Quận Cầu Giấy", "Hà Nội"),
        };
        for (int i = 0; i < customers.Count; i++)
        {
            var a = addressSeeds[i];
            customers[i].Addresses.Add(new CustomerAddress
            {
                ReceiverName = customers[i].FullName, Phone = customers[i].Phone,
                AddressLine = a.Item1, Ward = a.Item2, District = a.Item3, Province = a.Item4, IsDefault = true
            });
            customers[i].CreatedAt = now.AddDays(-60 + i * 5);
        }
        db.Users.AddRange(admin, staff1, staff2);
        db.Users.AddRange(customers);

        // ---------------- Danh mục, NXB ----------------
        var categories = new[]
        {
            new Category { Name = "Văn học", Description = "Tiểu thuyết, truyện ngắn, tác phẩm kinh điển trong và ngoài nước" },
            new Category { Name = "Kinh tế", Description = "Quản trị, đầu tư, khởi nghiệp, tài chính cá nhân" },
            new Category { Name = "Kỹ năng sống", Description = "Phát triển bản thân, tư duy, thói quen" },
            new Category { Name = "Công nghệ thông tin", Description = "Lập trình, kỹ thuật phần mềm, khoa học máy tính" },
            new Category { Name = "Ngoại ngữ", Description = "Sách học tiếng Anh, tiếng Nhật" },
            new Category { Name = "Thiếu nhi", Description = "Truyện tranh, truyện thiếu nhi" },
        }.ToDictionary(c => c.Name);
        db.Categories.AddRange(categories.Values);

        var publishers = new[]
        {
            new Publisher { Name = "NXB Trẻ", Address = "161B Lý Chính Thắng, Quận 3, TP.HCM", Phone = "02839316289" },
            new Publisher { Name = "NXB Kim Đồng", Address = "55 Quang Trung, Hai Bà Trưng, Hà Nội", Phone = "02439434730" },
            new Publisher { Name = "NXB Tổng hợp TP.HCM", Address = "62 Nguyễn Thị Minh Khai, Quận 1, TP.HCM", Phone = "02838225340" },
            new Publisher { Name = "NXB Lao Động", Address = "175 Giảng Võ, Đống Đa, Hà Nội", Phone = "02438515380" },
            new Publisher { Name = "NXB Thế Giới", Address = "46 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", Phone = "02438253841" },
            new Publisher { Name = "NXB Hội Nhà Văn", Address = "65 Nguyễn Du, Hai Bà Trưng, Hà Nội", Phone = "02438222135" },
            new Publisher { Name = "NXB ĐHQG Hà Nội", Address = "16 Hàng Chuối, Hai Bà Trưng, Hà Nội", Phone = "02439714896" },
        }.ToDictionary(p => p.Name);
        db.Publishers.AddRange(publishers.Values);

        // ---------------- Sách ----------------
        var seeds = new List<BookSeed>
        {
            new("Mắt Biếc", new[] { "Nguyễn Nhật Ánh" }, "Văn học", "NXB Trẻ", 2019, 300, 110000, "Bìa mềm",
                "Câu chuyện tình đơn phương của Ngạn dành cho Hà Lan – cô bạn có đôi mắt biếc – từ thuở ấu thơ ở làng Đo Đo đến khi trưởng thành nơi thành phố."),
            new("Tôi Thấy Hoa Vàng Trên Cỏ Xanh", new[] { "Nguyễn Nhật Ánh" }, "Văn học", "NXB Trẻ", 2018, 378, 125000, "Bìa mềm",
                "Những trang viết trong trẻo về tuổi thơ nghèo khó nhưng đầy tình thương của hai anh em Thiều và Tường ở một làng quê miền Trung."),
            new("Cho Tôi Xin Một Vé Đi Tuổi Thơ", new[] { "Nguyễn Nhật Ánh" }, "Văn học", "NXB Trẻ", 2018, 216, 80000, "Bìa mềm",
                "Một chuyến tàu đưa người đọc trở về thế giới tuổi thơ với những trò nghịch ngợm và suy nghĩ ngây ngô mà sâu sắc."),
            new("Nhà Giả Kim", new[] { "Paulo Coelho" }, "Văn học", "NXB Hội Nhà Văn", 2020, 228, 79000, "Bìa mềm",
                "Hành trình của chàng chăn cừu Santiago đi tìm kho báu và khám phá ý nghĩa của việc theo đuổi ước mơ."),
            new("Rừng Na Uy", new[] { "Haruki Murakami" }, "Văn học", "NXB Hội Nhà Văn", 2021, 556, 155000, "Bìa cứng",
                "Tiểu thuyết về tuổi trẻ, tình yêu và mất mát ở Tokyo cuối thập niên 1960."),
            new("Số Đỏ", new[] { "Vũ Trọng Phụng" }, "Văn học", "NXB Hội Nhà Văn", 2020, 260, 72000, "Bìa mềm",
                "Tiểu thuyết trào phúng kinh điển về xã hội thành thị Việt Nam thời Pháp thuộc qua nhân vật Xuân Tóc Đỏ."),
            new("Cha Giàu Cha Nghèo", new[] { "Robert T. Kiyosaki" }, "Kinh tế", "NXB Trẻ", 2022, 344, 115000, "Bìa mềm",
                "Những bài học về tư duy tài chính, tài sản và tiêu sản qua câu chuyện về hai người cha."),
            new("Nhà Đầu Tư Thông Minh", new[] { "Benjamin Graham" }, "Kinh tế", "NXB Lao Động", 2020, 664, 239000, "Bìa cứng",
                "Cuốn sách nền tảng của trường phái đầu tư giá trị."),
            new("Từ Tốt Đến Vĩ Đại", new[] { "Jim Collins" }, "Kinh tế", "NXB Trẻ", 2019, 452, 165000, "Bìa mềm",
                "Nghiên cứu về những yếu tố giúp một công ty tốt trở thành công ty vĩ đại."),
            new("Khởi Nghiệp Tinh Gọn", new[] { "Eric Ries" }, "Kinh tế", "NXB Lao Động", 2021, 376, 139000, "Bìa mềm",
                "Phương pháp xây dựng sản phẩm và doanh nghiệp bằng vòng lặp Xây dựng – Đo lường – Học hỏi."),
            new("Chiến Lược Đại Dương Xanh", new[] { "W. Chan Kim", "Renée Mauborgne" }, "Kinh tế", "NXB Lao Động", 2020, 412, 179000, "Bìa mềm",
                "Cách tạo ra khoảng thị trường mới thay vì cạnh tranh trong đại dương đỏ."),
            new("Đắc Nhân Tâm", new[] { "Dale Carnegie" }, "Kỹ năng sống", "NXB Tổng hợp TP.HCM", 2021, 320, 86000, "Bìa mềm",
                "Nghệ thuật giao tiếp, thu phục lòng người và tạo ảnh hưởng tích cực."),
            new("Thói Quen Nguyên Tử", new[] { "James Clear" }, "Kỹ năng sống", "NXB Thế Giới", 2022, 380, 189000, "Bìa mềm",
                "Những thay đổi nhỏ tạo nên kết quả lớn: phương pháp xây dựng thói quen tốt và loại bỏ thói quen xấu."),
            new("Tuổi Trẻ Đáng Giá Bao Nhiêu", new[] { "Rosie Nguyễn" }, "Kỹ năng sống", "NXB Hội Nhà Văn", 2020, 292, 90000, "Bìa mềm",
                "Những chia sẻ về học tập, đi và trải nghiệm dành cho người trẻ."),
            new("Sức Mạnh Của Thói Quen", new[] { "Charles Duhigg" }, "Kỹ năng sống", "NXB Lao Động", 2021, 408, 149000, "Bìa mềm",
                "Giải thích cơ chế hình thành thói quen ở cá nhân, tổ chức và xã hội."),
            new("Tư Duy Nhanh Và Chậm", new[] { "Daniel Kahneman" }, "Kỹ năng sống", "NXB Thế Giới", 2021, 612, 249000, "Bìa cứng",
                "Hai hệ thống tư duy chi phối cách con người ra quyết định."),
            new("Clean Code – Mã Sạch", new[] { "Robert C. Martin" }, "Công nghệ thông tin", "NXB Thế Giới", 2022, 464, 299000, "Bìa mềm",
                "Nguyên tắc viết mã dễ đọc, dễ bảo trì dành cho lập trình viên chuyên nghiệp."),
            new("Design Patterns", new[] { "Erich Gamma", "Richard Helm", "Ralph Johnson", "John Vlissides" }, "Công nghệ thông tin", "NXB Thế Giới", 2021, 395, 320000, "Bìa cứng",
                "23 mẫu thiết kế hướng đối tượng kinh điển."),
            new("The Pragmatic Programmer", new[] { "Andrew Hunt", "David Thomas" }, "Công nghệ thông tin", "NXB Thế Giới", 2023, 352, 280000, "Bìa mềm",
                "Những lời khuyên thực tiễn để trở thành lập trình viên giỏi."),
            new("Refactoring – Cải Tiến Mã Nguồn", new[] { "Martin Fowler" }, "Công nghệ thông tin", "NXB Thế Giới", 2022, 448, 310000, "Bìa mềm",
                "Kỹ thuật cải tiến cấu trúc mã nguồn mà không thay đổi hành vi."),
            new("Cấu Trúc Dữ Liệu Và Giải Thuật", new[] { "Đỗ Xuân Lôi" }, "Công nghệ thông tin", "NXB ĐHQG Hà Nội", 2019, 400, 95000, "Bìa mềm",
                "Giáo trình nền tảng về cấu trúc dữ liệu và thuật toán."),
            new("English Grammar In Use", new[] { "Raymond Murphy" }, "Ngoại ngữ", "NXB Tổng hợp TP.HCM", 2020, 380, 168000, "Bìa mềm",
                "Sách ngữ pháp tiếng Anh tự học trình độ trung cấp."),
            new("Minna No Nihongo Sơ Cấp 1", new[] { "3A Corporation" }, "Ngoại ngữ", "NXB Trẻ", 2021, 300, 98000, "Bìa mềm",
                "Giáo trình tiếng Nhật sơ cấp phổ biến nhất."),
            new("Destination B1", new[] { "Malcolm Mann", "Steve Taylore-Knowles" }, "Ngoại ngữ", "NXB Tổng hợp TP.HCM", 2020, 256, 145000, "Bìa mềm",
                "Ngữ pháp và từ vựng tiếng Anh trình độ B1."),
            new("Dế Mèn Phiêu Lưu Ký", new[] { "Tô Hoài" }, "Thiếu nhi", "NXB Kim Đồng", 2021, 144, 45000, "Bìa mềm",
                "Cuộc phiêu lưu của chú Dế Mèn – tác phẩm thiếu nhi kinh điển của văn học Việt Nam."),
            new("Hoàng Tử Bé", new[] { "Antoine de Saint-Exupéry" }, "Thiếu nhi", "NXB Hội Nhà Văn", 2019, 112, 65000, "Bìa mềm",
                "Câu chuyện triết lý nhẹ nhàng về tình bạn, tình yêu và cách nhìn thế giới của trẻ thơ."),
            new("Doraemon Tập 1", new[] { "Fujiko F. Fujio" }, "Thiếu nhi", "NXB Kim Đồng", 2022, 192, 25000, "Bìa mềm",
                "Chú mèo máy đến từ thế kỷ 22 cùng những bảo bối thần kỳ."),
            new("Kính Vạn Hoa Tập 1", new[] { "Nguyễn Nhật Ánh" }, "Thiếu nhi", "NXB Kim Đồng", 2020, 230, 60000, "Bìa mềm",
                "Những câu chuyện học trò của nhóm bạn Quý ròm, Tiểu Long và nhỏ Hạnh."),
            new("Harry Potter Và Hòn Đá Phù Thủy", new[] { "J.K. Rowling" }, "Thiếu nhi", "NXB Trẻ", 2022, 366, 145000, "Bìa mềm",
                "Tập đầu tiên trong loạt truyện về cậu bé phù thủy Harry Potter."),
            new("Hai Số Phận", new[] { "Jeffrey Archer" }, "Văn học", "NXB Hội Nhà Văn", 2019, 774, 175000, "Bìa cứng",
                "Cuộc đời song song của hai người đàn ông sinh cùng ngày nhưng ở hai thế giới khác biệt."),
        };

        var authors = seeds.SelectMany(s => s.Authors).Distinct()
            .Select(n => new Author { Name = n }).ToDictionary(a => a.Name);
        db.Authors.AddRange(authors.Values);

        var books = new List<Book>();
        for (int i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            var book = new Book
            {
                Title = s.Title,
                Isbn = MakeIsbn(i + 1),
                Category = categories[s.Category],
                Publisher = publishers[s.Publisher],
                PublishYear = s.Year,
                Pages = s.Pages,
                Price = s.Price,
                CoverType = s.Cover,
                Description = s.Description,
                LowStockThreshold = 10,
                IsFeatured = i % 4 == 0,
                CreatedAt = now.AddDays(-50 + i),
            };
            foreach (var a in s.Authors) book.BookAuthors.Add(new BookAuthor { Author = authors[a] });
            books.Add(book);
        }
        db.Books.AddRange(books);

        // ---------------- Nhà cung cấp, mã giảm giá ----------------
        var suppliers = new[]
        {
            new Supplier { Name = "Công ty Phát hành sách Fahasa", Phone = "02838225798", Email = "kinhdoanh@fahasa.com", Address = "60-62 Lê Lợi, Quận 1, TP.HCM" },
            new Supplier { Name = "Công ty Văn hóa Phương Nam", Phone = "02838663447", Email = "info@phuongnam.vn", Address = "940 Ba Tháng Hai, Quận 11, TP.HCM" },
            new Supplier { Name = "Nhà sách Kim Đồng – Chi nhánh phía Nam", Phone = "02839303832", Email = "cnkimdong@nxbkimdong.com.vn", Address = "268 Nguyễn Đình Chiểu, Quận 3, TP.HCM" },
        };
        db.Suppliers.AddRange(suppliers);

        db.Coupons.AddRange(
            new Coupon { Code = "GIAM10", DiscountType = DiscountType.Percent, Value = 10, MaxDiscount = 50000, MinOrder = 150000,
                StartAt = now.AddMonths(-1), EndAt = now.AddMonths(6), UsageLimit = 100 },
            new Coupon { Code = "SACH30K", DiscountType = DiscountType.Amount, Value = 30000, MinOrder = 200000,
                StartAt = now.AddMonths(-1), EndAt = now.AddMonths(6), UsageLimit = 50 },
            new Coupon { Code = "HETHAN", DiscountType = DiscountType.Percent, Value = 20, MinOrder = 0,
                StartAt = now.AddMonths(-3), EndAt = now.AddMonths(-2), UsageLimit = 10 });

        await db.SaveChangesAsync();

        // ---------------- Nhập hàng đầu kỳ (45 ngày trước) ----------------
        var stock = books.ToDictionary(b => b.Id, _ => 0);
        var lowStockBooks = new HashSet<int> { books[5].Id, books[20].Id, books[23].Id };
        var receipts = new List<ImportReceipt>();
        for (int r = 0; r < 2; r++)
        {
            var receipt = new ImportReceipt
            {
                Supplier = suppliers[r], StaffId = staff1.Id,
                ImportDate = now.Date.AddDays(-45 + r).AddHours(9),
                Note = r == 0 ? "Nhập hàng đầu kỳ – đợt 1" : "Nhập hàng đầu kỳ – đợt 2"
            };
            foreach (var b in books.Where((_, idx) => idx % 2 == r))
            {
                int qty = lowStockBooks.Contains(b.Id) ? 12 : rnd.Next(6, 16) * 10;
                receipt.Details.Add(new ImportReceiptDetail
                {
                    BookId = b.Id, Quantity = qty, UnitCost = Math.Round(b.Price * 0.6m / 1000) * 1000
                });
            }
            receipts.Add(receipt);
        }
        db.ImportReceipts.AddRange(receipts);
        await db.SaveChangesAsync();

        var transactions = new List<StockTransaction>();
        foreach (var receipt in receipts)
            foreach (var d in receipt.Details)
            {
                stock[d.BookId] += d.Quantity;
                transactions.Add(new StockTransaction
                {
                    BookId = d.BookId, StaffId = receipt.StaffId, Type = StockTransactionType.Import,
                    QuantityChange = d.Quantity, StockAfter = stock[d.BookId], RefId = receipt.Id,
                    Reason = $"Phiếu nhập #{receipt.Id}", CreatedAt = receipt.ImportDate
                });
            }

        // ---------------- Đơn hàng mẫu trong 30 ngày qua ----------------
        var orders = new List<Order>();
        for (int i = 0; i < 36; i++)
        {
            var customer = customers[rnd.Next(customers.Count)];
            var address = customer.Addresses.First();
            int daysAgo = 30 - (int)(i * 30.0 / 36);
            var orderDate = now.Date.AddDays(-daysAgo).AddHours(rnd.Next(8, 22)).AddMinutes(rnd.Next(60));
            if (orderDate > now) orderDate = now.AddMinutes(-rnd.Next(30, 300));

            var status = daysAgo switch
            {
                > 4 => rnd.Next(10) == 0 ? OrderStatus.Cancelled : OrderStatus.Completed,
                > 2 => rnd.Next(2) == 0 ? OrderStatus.Shipping : OrderStatus.Completed,
                > 1 => OrderStatus.Confirmed,
                _ => OrderStatus.Pending
            };

            var order = new Order
            {
                CustomerId = customer.Id, OrderDate = orderDate,
                ReceiverName = address.ReceiverName, ReceiverPhone = address.Phone, ShippingAddress = address.FullAddress,
                PaymentMethod = rnd.Next(3) == 0 ? PaymentMethod.BankTransfer : PaymentMethod.COD,
                Status = status, ShippingFee = 25000,
                HandledById = status == OrderStatus.Pending ? null : (rnd.Next(2) == 0 ? staff1.Id : staff2.Id)
            };

            var picked = books.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4));
            foreach (var b in picked)
            {
                int qty = rnd.Next(1, 4);
                if (stock[b.Id] - qty < 2) continue;
                stock[b.Id] -= qty;
                order.Details.Add(new OrderDetail { BookId = b.Id, Quantity = qty, UnitPrice = b.Price });
            }
            if (order.Details.Count == 0) continue;

            order.Subtotal = order.Details.Sum(d => d.UnitPrice * d.Quantity);
            if (order.Subtotal >= 300000) order.ShippingFee = 0; // miễn phí vận chuyển đơn từ 300.000đ
            if (status == OrderStatus.Completed) order.CompletedAt = orderDate.AddDays(rnd.Next(2, 4));
            if (status == OrderStatus.Cancelled)
            {
                order.CancelReason = "Khách hàng yêu cầu hủy";
                foreach (var d in order.Details) stock[d.BookId] += d.Quantity; // hoàn kho
            }

            bool paid = status == OrderStatus.Completed || order.PaymentMethod == PaymentMethod.BankTransfer && status != OrderStatus.Cancelled;
            order.Payments.Add(new Payment
            {
                Amount = order.Total,
                Status = paid ? PaymentStatus.Paid : (status == OrderStatus.Cancelled ? PaymentStatus.Failed : PaymentStatus.Pending),
                PaidAt = paid ? (order.PaymentMethod == PaymentMethod.BankTransfer ? orderDate.AddMinutes(10) : order.CompletedAt) : null,
                ReferenceCode = paid && order.PaymentMethod == PaymentMethod.BankTransfer ? $"FT{orderDate:yyMMddHHmm}{i:00}" : null
            });
            orders.Add(order);
        }
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();

        // Ghi sổ kho cho các đơn mẫu theo đúng thứ tự thời gian
        var running = books.ToDictionary(b => b.Id, b => transactions.Where(t => t.BookId == b.Id).Sum(t => t.QuantityChange));
        foreach (var order in orders.OrderBy(o => o.OrderDate))
        {
            foreach (var d in order.Details)
            {
                running[d.BookId] -= d.Quantity;
                transactions.Add(new StockTransaction
                {
                    BookId = d.BookId, Type = StockTransactionType.Sale, QuantityChange = -d.Quantity,
                    StockAfter = running[d.BookId], RefId = order.Id, Reason = $"Đơn hàng #{order.Id}", CreatedAt = order.OrderDate
                });
            }
            if (order.Status == OrderStatus.Cancelled)
                foreach (var d in order.Details)
                {
                    running[d.BookId] += d.Quantity;
                    transactions.Add(new StockTransaction
                    {
                        BookId = d.BookId, StaffId = order.HandledById, Type = StockTransactionType.Cancel, QuantityChange = d.Quantity,
                        StockAfter = running[d.BookId], RefId = order.Id, Reason = $"Hủy đơn #{order.Id}", CreatedAt = order.OrderDate.AddHours(2)
                    });
                }
        }
        db.StockTransactions.AddRange(transactions);

        foreach (var b in books) b.Stock = stock[b.Id];

        // ---------------- Đánh giá từ các đơn đã hoàn tất ----------------
        var comments = new[]
        {
            (5, "Sách rất hay, giao hàng nhanh, đóng gói cẩn thận."),
            (4, "Nội dung tốt, giấy in đẹp."),
            (5, "Đáng đọc, mình sẽ giới thiệu cho bạn bè."),
            (3, "Nội dung ổn nhưng bìa hơi bị cong khi nhận."),
            (4, "Giá hợp lý, sẽ ủng hộ shop tiếp."),
        };
        var reviewed = new HashSet<(int, int)>();
        foreach (var order in orders.Where(o => o.Status == OrderStatus.Completed))
            foreach (var d in order.Details)
            {
                if (rnd.Next(3) == 0 || !reviewed.Add((order.CustomerId, d.BookId))) continue;
                var c = comments[rnd.Next(comments.Length)];
                db.Reviews.Add(new Review
                {
                    UserId = order.CustomerId, BookId = d.BookId, Rating = c.Item1, Comment = c.Item2,
                    CreatedAt = order.CompletedAt!.Value.AddDays(1)
                });
            }

        await db.SaveChangesAsync();
    }

    private static User NewUser(string username, string password, string fullName, string email, string phone, UserRole role) => new()
    {
        Username = username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        FullName = fullName,
        Email = email,
        Phone = phone,
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.Now.AddDays(-90)
    };

    /// <summary>Sinh mã ISBN-13 hợp lệ (đúng chữ số kiểm tra) cho dữ liệu mẫu.</summary>
    private static string MakeIsbn(int n)
    {
        var digits = $"978604{n:000000}";
        int sum = 0;
        for (int i = 0; i < 12; i++) sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        int check = (10 - sum % 10) % 10;
        return digits + check;
    }
}
