using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web.Mvc;
using DocumentManagement.Models;

namespace DocumentManagement.Controllers
{
    public class AdminController : Controller
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        // Kiểm tra admin
        private bool IsAdmin()
        {
            if (Session["UserID"] != null && Session["IsAdmin"] != null)
            {
                return (bool)Session["IsAdmin"];
            }
            return false;
        }

        // GET: Admin/Dashboard
        // GET: Admin/Dashboard
        public ActionResult Dashboard()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new AdminDashboardViewModel
            {
                TopUsers = new List<TopUserModel>(),
                TopFiles = new List<TopFileModel>(),
                RecentFiles = new List<FileModel>(),
                FileTypeStats = new Dictionary<string, int>(),
                UploadStats = new Dictionary<string, int>()
            };

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 1. Tổng số user
                string query = "SELECT COUNT(*) FROM Users WHERE IsAdmin = 0";
                SqlCommand cmd = new SqlCommand(query, conn);
                model.TotalUsers = Convert.ToInt32(cmd.ExecuteScalar());

                // 2. Tổng số file (chưa xóa)
                query = "SELECT COUNT(*) FROM Files WHERE IsDeleted = 0";
                cmd = new SqlCommand(query, conn);
                model.TotalFiles = Convert.ToInt32(cmd.ExecuteScalar());

                // 3. Tổng dung lượng - SỬA LỖI Ở ĐÂY
                query = "SELECT ISNULL(SUM(FileSize), 0) FROM Files WHERE IsDeleted = 0";
                cmd = new SqlCommand(query, conn);
                var result = cmd.ExecuteScalar();
                model.TotalStorage = Convert.ToInt64(result);  // Dùng Convert thay vì cast trực tiếp

                // 4. Tổng số lượt chia sẻ
                query = "SELECT COUNT(*) FROM SharedLinks";
                cmd = new SqlCommand(query, conn);
                model.TotalShares = Convert.ToInt32(cmd.ExecuteScalar());

                // 5. Số file trong thùng rác
                query = "SELECT COUNT(*) FROM Files WHERE IsDeleted = 1";
                cmd = new SqlCommand(query, conn);
                model.TrashFiles = Convert.ToInt32(cmd.ExecuteScalar());

                // 6. Top 5 user upload nhiều nhất
                query = @"
            SELECT TOP 5 
                u.ID as UserID, 
                u.Username, 
                COUNT(f.ID) as FileCount, 
                ISNULL(SUM(f.FileSize), 0) as TotalSize
            FROM Users u
            LEFT JOIN Files f ON u.ID = f.UserID AND f.IsDeleted = 0
            WHERE u.IsAdmin = 0
            GROUP BY u.ID, u.Username
            ORDER BY FileCount DESC, TotalSize DESC";

                cmd = new SqlCommand(query, conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    model.TopUsers.Add(new TopUserModel
                    {
                        UserID = Convert.ToInt32(reader["UserID"]),
                        Username = reader["Username"].ToString(),
                        FileCount = Convert.ToInt32(reader["FileCount"]),
                        TotalSize = Convert.ToInt64(reader["TotalSize"])
                    });
                }
                reader.Close();

                // 7. Top 5 file được download nhiều nhất
                query = @"
            SELECT TOP 5 
                f.ID as FileID, 
                f.FileName, 
                ISNULL(sl.DownloadCount, 0) as DownloadCount,
                u.Username as UploaderName
            FROM Files f
            JOIN Users u ON f.UserID = u.ID
            LEFT JOIN SharedLinks sl ON f.ID = sl.FileID
            WHERE f.IsDeleted = 0
            ORDER BY DownloadCount DESC";

                cmd = new SqlCommand(query, conn);
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    model.TopFiles.Add(new TopFileModel
                    {
                        FileID = Convert.ToInt32(reader["FileID"]),
                        FileName = reader["FileName"].ToString(),
                        DownloadCount = Convert.ToInt32(reader["DownloadCount"]),
                        UploaderName = reader["UploaderName"].ToString()
                    });
                }
                reader.Close();

                // 8. Recent files (10 file mới nhất)
                query = @"
            SELECT TOP 10 
                f.*, 
                u.Username 
            FROM Files f
            JOIN Users u ON f.UserID = u.ID
            WHERE f.IsDeleted = 0
            ORDER BY f.UploadedAt DESC";

                cmd = new SqlCommand(query, conn);
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    model.RecentFiles.Add(new FileModel
                    {
                        ID = Convert.ToInt32(reader["ID"]),
                        FileName = reader["FileName"].ToString(),
                        FileSize = Convert.ToInt32(reader["FileSize"]),
                        FileType = reader["FileType"].ToString(),
                        UploadedAt = Convert.ToDateTime(reader["UploadedAt"]),
                        UserID = Convert.ToInt32(reader["UserID"])
                    });
                }
                reader.Close();

                // 9. Thống kê theo loại file
                query = @"
            SELECT 
                FileType, 
                COUNT(*) as Count 
            FROM Files 
            WHERE IsDeleted = 0
            GROUP BY FileType
            ORDER BY Count DESC";

                cmd = new SqlCommand(query, conn);
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string fileType = reader["FileType"].ToString();
                    if (string.IsNullOrEmpty(fileType)) fileType = "unknown";
                    model.FileTypeStats[fileType] = Convert.ToInt32(reader["Count"]);
                }
                reader.Close();

                // 10. Thống kê upload 7 ngày gần nhất
                for (int i = 6; i >= 0; i--)
                {
                    string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                    query = "SELECT COUNT(*) FROM Files WHERE IsDeleted = 0 AND CAST(UploadedAt AS DATE) = @Date";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Date", date);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    model.UploadStats.Add(date, count);
                }
            }

            return View(model);
        }

        // GET: Admin/Users (quản lý user)
        public ActionResult Users()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT u.*, 
                        ISNULL(COUNT(f.ID), 0) as FileCount,
                        ISNULL(SUM(f.FileSize), 0) as TotalUsed
                    FROM Users u
                    LEFT JOIN Files f ON u.ID = f.UserID AND f.IsDeleted = 0
                    WHERE u.IsAdmin = 0
                    GROUP BY u.ID, u.Username, u.Password, u.FullName, u.Email, 
                             u.IsBlocked, u.IsAdmin, u.CreatedAt, u.TotalStorage, u.UsedStorage
                    ORDER BY u.CreatedAt DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    users.Add(new User
                    {
                        ID = (int)reader["ID"],
                        Username = reader["Username"].ToString(),
                        FullName = reader["FullName"]?.ToString(),
                        Email = reader["Email"]?.ToString(),
                        IsBlocked = (bool)reader["IsBlocked"],
                        CreatedAt = (DateTime)reader["CreatedAt"],
                        TotalStorage = (int)reader["TotalStorage"],
                        UsedStorage = (int)reader["TotalUsed"]
                    });
                }
            }

            return View(users);
        }

        // POST: Admin/ToggleBlock
        [HttpPost]
        public ActionResult ToggleBlock(int userId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Không có quyền" });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Users SET IsBlocked = ~IsBlocked WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", userId);

                conn.Open();
                cmd.ExecuteNonQuery();

                // Lấy trạng thái mới
                query = "SELECT IsBlocked FROM Users WHERE ID = @ID";
                cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", userId);
                bool newStatus = (bool)cmd.ExecuteScalar();

                return Json(new { success = true, isBlocked = newStatus });
            }
        }

        // GET: Admin/Files (xem tất cả file hệ thống)
        public ActionResult Files(string search = "")
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            var files = new List<FileModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT f.*, u.Username 
                    FROM Files f
                    JOIN Users u ON f.UserID = u.ID
                    WHERE (f.FileName LIKE @Search OR u.Username LIKE @Search)
                    ORDER BY f.UploadedAt DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Search", "%" + search + "%");

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    files.Add(new FileModel
                    {
                        ID = (int)reader["ID"],
                        FileName = reader["FileName"].ToString(),
                        FileSize = (int)reader["FileSize"],
                        FileType = reader["FileType"].ToString(),
                        UploadedAt = (DateTime)reader["UploadedAt"],
                        UserID = (int)reader["UserID"]
                    });
                }
            }

            ViewBag.Search = search;
            return View(files);
        }

        // POST: Admin/DeleteFile (xóa file bất kỳ)
        [HttpPost]
        public ActionResult DeleteFile(int fileId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Không có quyền" });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Lấy đường dẫn file
                string query = "SELECT FilePath FROM Files WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", fileId);

                conn.Open();
                string filePath = cmd.ExecuteScalar()?.ToString();

                // Xóa file vật lý
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Xóa khỏi database
                query = "DELETE FROM Files WHERE ID = @ID";
                cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", fileId);
                cmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
        }

        // GET: Admin/Logs
        public ActionResult Logs()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            var logs = new List<ActivityLog>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT TOP 200 * FROM ActivityLog 
            ORDER BY Time DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    logs.Add(new ActivityLog
                    {
                        ID = (int)reader["ID"],
                        UserID = (int)reader["UserID"],
                        UserName = reader["UserName"].ToString(),
                        Action = reader["Action"].ToString(),
                        FileName = reader["FileName"].ToString(),
                        FileSize = (int)reader["FileSize"],
                        Time = (DateTime)reader["Time"],
                        IPAddress = reader["IPAddress"].ToString()
                    });
                }
            }

            return View(logs);
        }

    }
}