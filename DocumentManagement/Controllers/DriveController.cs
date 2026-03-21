using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DocumentManagement.Models;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;

namespace DocumentManagement.Controllers
{
    public class DriveController : Controller
    {
        string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        // GET: Drive
        public ActionResult Index(int? folderId, string filter = "All", string sort = "name", string order = "asc")
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];
            var model = new DriveViewModel
            {
                Folders = GetFolders(userID, folderId),
                Files = GetFiles(userID, folderId, filter, sort, order),
                CurrentFolderID = folderId ?? 0,
                CurrentFolderName = GetFolderName(folderId),
                Breadcrumb = GetBreadcrumb(folderId)
            };

            return View(model);
        }

        // POST: Upload file
        [HttpPost]
        public ActionResult Upload(HttpPostedFileBase[] files, int? folderId)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];

            System.Diagnostics.Debug.WriteLine("=== UPLOAD ===");
            System.Diagnostics.Debug.WriteLine("UserID trong session: " + userID);
            System.Diagnostics.Debug.WriteLine("UserID: " + userID);
            System.Diagnostics.Debug.WriteLine("Số file: " + (files != null ? files.Length.ToString() : "0"));



            if (files != null && files.Length > 0)
            {
                // Tạo thư mục cho user nếu chưa có
                string userFolder = @"D:\DocumentManagement\Source\DocumentManagement\DocumentManagement\Storage\User_" + userID;
                System.Diagnostics.Debug.WriteLine("ĐƯỜNG DẪN THẬT: " + userFolder);
                System.Diagnostics.Debug.WriteLine("THƯ MỤC CÓ TỒN TẠI: " + Directory.Exists(userFolder));
                System.Diagnostics.Debug.WriteLine("Đường dẫn: " + userFolder);

                if (!Directory.Exists(userFolder))
                {
                    Directory.CreateDirectory(userFolder);
                }

                foreach (var file in files)
                {
                    if (file != null && file.ContentLength > 0)
                    {

                        System.Diagnostics.Debug.WriteLine("Đang xử lý file: " + file.FileName);

                        string fileName = Path.GetFileName(file.FileName);
                        string filePath = Path.Combine(userFolder, fileName);

                        // Xử lý trùng tên
                        int count = 1;
                        string fileNameOnly = Path.GetFileNameWithoutExtension(fileName);
                        string extension = Path.GetExtension(fileName);
                        while (System.IO.File.Exists(filePath))
                        {
                            fileName = $"{fileNameOnly} ({count}){extension}";
                            filePath = Path.Combine(userFolder, fileName);
                            count++;
                        }

                        // Lưu file
                        file.SaveAs(filePath);

                        // Lưu vào database
                        SaveFileToDatabase(userID, fileName, filePath, file.ContentLength, folderId);
                    }
                }
            }

            return RedirectToAction("Index", new { folderId = folderId });
        }

        private void SaveFileToDatabase(int userID, string fileName, string filePath, int fileSize, int? folderId)
        {
            string fileType = Path.GetExtension(fileName).ToLower().Replace(".", "");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"INSERT INTO Files (FileName, FilePath, FileSize, FileType, UserID, FolderID, UploadedAt, IsDeleted, IsStarred)
                        VALUES (@FileName, @FilePath, @FileSize, @FileType, @UserID, @FolderID, GETDATE(), 0, 0)";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FileName", fileName);
                cmd.Parameters.AddWithValue("@FilePath", filePath);
                cmd.Parameters.AddWithValue("@FileSize", fileSize);
                cmd.Parameters.AddWithValue("@FileType", fileType);
                cmd.Parameters.AddWithValue("@UserID", userID);

                // CHỈ 1 DÒNG NÀY - ĐÃ XÓA DÒNG CŨ
                if (folderId == null || folderId == 0)
                {
                    cmd.Parameters.AddWithValue("@FolderID", DBNull.Value);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@FolderID", folderId);
                }

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private List<FolderModel> GetFolders(int userID, int? parentFolderId)
        {
            var folders = new List<FolderModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT * FROM Folders 
                                WHERE UserID = @UserID 
                                AND IsDeleted = 0 
                                AND ParentFolderID " + (parentFolderId == null ? "IS NULL" : "= @ParentFolderID");

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserID", userID);
                if (parentFolderId != null)
                {
                    cmd.Parameters.AddWithValue("@ParentFolderID", parentFolderId);
                }

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    folders.Add(new FolderModel
                    {
                        ID = (int)reader["ID"],
                        FolderName = reader["FolderName"].ToString(),
                        UserID = (int)reader["UserID"],
                        ParentFolderID = reader["ParentFolderID"] == DBNull.Value ? null : (int?)reader["ParentFolderID"],
                        CreatedAt = (DateTime)reader["CreatedAt"],
                        IsDeleted = (bool)reader["IsDeleted"],
                        FolderColor = reader["FolderColor"].ToString()
                    });
                }
            }

            return folders;
        }

        private List<FileModel> GetFiles(int userID, int? folderId, string filter = "", string sort = "name", string order = "asc")
        {
            var files = new List<FileModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "";
                string whereClause = "";

                // Xây dựng WHERE clause
                if (folderId == null || folderId == 0)
                {
                    whereClause = "UserID = @UserID AND IsDeleted = 0 AND FolderID IS NULL";
                }
                else
                {
                    whereClause = "UserID = @UserID AND IsDeleted = 0 AND FolderID = @FolderID";
                }

                // Thêm filter (chỉ thêm khi filter KHÔNG phải "All" và KHÔNG rỗng)
                if (!string.IsNullOrEmpty(filter) && filter != "All")
                {
                    whereClause += " AND FileType = @Filter";
                }

                query = "SELECT * FROM Files WHERE " + whereClause;

                // Thêm ORDER BY
                switch (sort)
                {
                    case "name":
                        query += " ORDER BY FileName " + (order == "asc" ? "ASC" : "DESC");
                        break;
                    case "size":
                        query += " ORDER BY FileSize " + (order == "asc" ? "ASC" : "DESC");
                        break;
                    case "date":
                        query += " ORDER BY UploadedAt " + (order == "asc" ? "ASC" : "DESC");
                        break;
                    default:
                        query += " ORDER BY UploadedAt DESC";
                        break;
                }

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserID", userID);

                if (folderId != null && folderId != 0)
                {
                    cmd.Parameters.AddWithValue("@FolderID", folderId);
                }

                // Chỉ thêm filter khi không phải "All"
                if (!string.IsNullOrEmpty(filter) && filter != "All")
                {
                    cmd.Parameters.AddWithValue("@Filter", filter);
                }

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    files.Add(new FileModel
                    {
                        ID = (int)reader["ID"],
                        FileName = reader["FileName"].ToString(),
                        FilePath = reader["FilePath"].ToString(),
                        FileSize = (int)reader["FileSize"],
                        FileType = reader["FileType"].ToString(),
                        UserID = (int)reader["UserID"],
                        FolderID = reader["FolderID"] == DBNull.Value ? null : (int?)reader["FolderID"],
                        UploadedAt = (DateTime)reader["UploadedAt"],
                        IsDeleted = (bool)reader["IsDeleted"],
                        DeletedAt = reader["DeletedAt"] == DBNull.Value ? null : (DateTime?)reader["DeletedAt"],
                        IsStarred = (bool)reader["IsStarred"]
                    });
                }
            }

            return files;
        }

        private string GetFolderName(int? folderId)
        {
            if (folderId == null) return "My Drive";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT FolderName FROM Folders WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", folderId);

                conn.Open();
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? "My Drive";
            }
        }

        private List<FolderModel> GetBreadcrumb(int? folderId)
        {
            var breadcrumb = new List<FolderModel>();

            if (folderId == null) return breadcrumb;

            int? currentId = folderId;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                while (currentId != null)
                {
                    string query = "SELECT * FROM Folders WHERE ID = @ID";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@ID", currentId);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        var folder = new FolderModel
                        {
                            ID = (int)reader["ID"],
                            FolderName = reader["FolderName"].ToString(),
                            UserID = (int)reader["UserID"],
                            ParentFolderID = reader["ParentFolderID"] == DBNull.Value ? null : (int?)reader["ParentFolderID"],
                            FolderColor = reader["FolderColor"].ToString()
                        };
                        breadcrumb.Insert(0, folder);
                        currentId = folder.ParentFolderID;
                    }
                    else
                    {
                        currentId = null;
                    }

                    conn.Close();
                }
            }

            return breadcrumb;
        }

        // GET: Download file
        public ActionResult Download(int id)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Files WHERE ID = @ID AND UserID = @UserID AND IsDeleted = 0";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    string filePath = reader["FilePath"].ToString();
                    string fileName = reader["FileName"].ToString();

                    // Ghi log download (sẽ làm sau)

                    // Trả file về cho client
                    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                    return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
                }
            }

            return HttpNotFound();
        }

        // POST: Delete file (xóa mềm)
        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Files SET IsDeleted = 1, DeletedAt = GETDATE() WHERE ID = @ID AND UserID = @UserID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    return Json(new { success = true, message = "Đã xóa file" });
                }
            }

            return Json(new { success = false, message = "Không tìm thấy file" });
        }

        // POST: Create Folder
        [HttpPost]
        public ActionResult CreateFolder(string folderName, int? folderId)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            if (string.IsNullOrEmpty(folderName))
            {
                return Json(new { success = false, message = "Tên folder không được để trống" });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"INSERT INTO Folders (FolderName, UserID, ParentFolderID, CreatedAt, IsDeleted, FolderColor)
                        VALUES (@FolderName, @UserID, @ParentFolderID, GETDATE(), 0, '#808080')";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FolderName", folderName);
                cmd.Parameters.AddWithValue("@UserID", userID);

                // SỬA CHỖ NÀY: Nếu folderId = 0 hoặc null thì truyền DBNull
                if (folderId == null || folderId == 0)
                {
                    cmd.Parameters.AddWithValue("@ParentFolderID", DBNull.Value);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@ParentFolderID", folderId);
                }

                conn.Open();
                cmd.ExecuteNonQuery();

                return Json(new { success = true, message = "Đã tạo folder" });
            }
        }

        // GET: Trash
        public ActionResult Trash()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];

            var files = new List<FileModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Files WHERE UserID = @UserID AND IsDeleted = 1 ORDER BY DeletedAt DESC";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserID", userID);

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
                        DeletedAt = reader["DeletedAt"] == DBNull.Value ? null : (DateTime?)reader["DeletedAt"]
                    });
                }
            }

            return View(files);
        }

        [HttpPost]
        public ActionResult Restore(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Files SET IsDeleted = 0, DeletedAt = NULL WHERE ID = @ID AND UserID = @UserID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    return Json(new { success = true });
                }
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public ActionResult DeleteForever(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Lấy đường dẫn file trước khi xóa
                string query = "SELECT FilePath FROM Files WHERE ID = @ID AND UserID = @UserID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                string filePath = cmd.ExecuteScalar()?.ToString();

                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Xóa khỏi database
                query = "DELETE FROM Files WHERE ID = @ID AND UserID = @UserID";
                cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);
                cmd.ExecuteNonQuery();
            }

            return Json(new { success = true });
        }

        // POST: Toggle Star (đánh dấu sao/bỏ sao)
        [HttpPost]
        public ActionResult ToggleStar(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Files SET IsStarred = ~IsStarred WHERE ID = @ID AND UserID = @UserID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    // Lấy trạng thái mới
                    query = "SELECT IsStarred FROM Files WHERE ID = @ID";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@ID", id);
                    bool newStatus = (bool)cmd.ExecuteScalar();

                    return Json(new { success = true, isStarred = newStatus });
                }
            }

            return Json(new { success = false });
        }

        // GET: Starred (hiển thị file đã đánh dấu sao)
        public ActionResult Starred()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];
            var files = new List<FileModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Files WHERE UserID = @UserID AND IsDeleted = 0 AND IsStarred = 1 ORDER BY UploadedAt DESC";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserID", userID);

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
                        IsStarred = (bool)reader["IsStarred"]
                    });
                }
            }

            return View(files);
        }

        // GET: Search
        public ActionResult Search(string keyword)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];
            var files = new List<FileModel>();

            if (!string.IsNullOrEmpty(keyword))
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT * FROM Files 
                            WHERE UserID = @UserID 
                            AND IsDeleted = 0 
                            AND FileName LIKE @Keyword 
                            ORDER BY UploadedAt DESC";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@UserID", userID);
                    cmd.Parameters.AddWithValue("@Keyword", "%" + keyword + "%");

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
                            IsStarred = (bool)reader["IsStarred"]
                        });
                    }
                }
            }

            ViewBag.Keyword = keyword;
            return View(files);
        }

        // POST: Tạo link chia sẻ
        [HttpPost]
        public ActionResult CreateShareLink(int fileId, string password, DateTime? expiryDate)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string checkQuery = "SELECT COUNT(*) FROM Files WHERE ID = @FileID AND UserID = @UserID AND IsDeleted = 0";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@FileID", fileId);
                checkCmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                int count = (int)checkCmd.ExecuteScalar();

                if (count == 0)
                {
                    return Json(new { success = false, message = "File không tồn tại" });
                }

                // Tạo token ngẫu nhiên
                string token = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);

                // THÊM DEBUG
                System.Diagnostics.Debug.WriteLine("Tạo token: " + token);
                System.Diagnostics.Debug.WriteLine("FileID: " + fileId);
                System.Diagnostics.Debug.WriteLine("Password: " + (string.IsNullOrEmpty(password) ? "null" : password));
                System.Diagnostics.Debug.WriteLine("ExpiryDate: " + (expiryDate.HasValue ? expiryDate.Value.ToString() : "null"));

                string query = @"INSERT INTO SharedLinks (FileID, Token, Password, ExpiryDate, CreatedAt, DownloadCount)
                        VALUES (@FileID, @Token, @Password, @ExpiryDate, GETDATE(), 0)";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FileID", fileId);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.Parameters.AddWithValue("@Password", string.IsNullOrEmpty(password) ? (object)DBNull.Value : password);
                cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate.HasValue ? (object)expiryDate.Value : DBNull.Value);

                int rows = cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Số dòng insert: " + rows);

                string shareUrl = Request.Url.Scheme + "://" + Request.Url.Authority + "/Drive/Shared?token=" + token;

                return Json(new { success = true, url = shareUrl, token = token });
            }
        }

        // GET: Xem file chia sẻ
        public ActionResult Shared(string token)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT sl.*, f.FileName, f.FilePath, f.FileSize 
                        FROM SharedLinks sl
                        JOIN Files f ON sl.FileID = f.ID
                        WHERE sl.Token = @Token 
                        AND (sl.ExpiryDate IS NULL OR sl.ExpiryDate > GETDATE())";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var share = new SharedLink
                    {
                        ID = (int)reader["ID"],
                        FileID = (int)reader["FileID"],
                        Token = reader["Token"].ToString(),
                        Password = reader["Password"] == DBNull.Value ? null : reader["Password"].ToString(),
                        ExpiryDate = reader["ExpiryDate"] == DBNull.Value ? null : (DateTime?)reader["ExpiryDate"],
                        FileName = reader["FileName"].ToString(),
                        FilePath = reader["FilePath"].ToString(),
                        FileSize = (int)reader["FileSize"]
                    };

                    // Nếu có password, hiện form nhập
                    if (!string.IsNullOrEmpty(share.Password))
                    {
                        ViewBag.Token = token;
                        return View("SharedPassword");
                    }

                    return View("SharedView", share);
                }
            }

            return Content("Link chia sẻ không hợp lệ hoặc đã hết hạn");
        }

        // POST: Xác thực password
        [HttpPost]
        public ActionResult VerifyPassword(string token, string password)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT sl.*, f.FileName, f.FilePath, f.FileSize 
                        FROM SharedLinks sl
                        JOIN Files f ON sl.FileID = f.ID
                        WHERE sl.Token = @Token AND sl.Password = @Password";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.Parameters.AddWithValue("@Password", password);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var share = new SharedLink
                    {
                        ID = (int)reader["ID"],
                        FileID = (int)reader["FileID"],
                        FileName = reader["FileName"].ToString(),
                        FilePath = reader["FilePath"].ToString(),
                        FileSize = (int)reader["FileSize"]
                    };

                    ViewBag.Token = token;
                    ViewBag.Password = password; // THÊM DÒNG NÀY ĐỂ TRUYỀN MẬT KHẨU
                    return View("SharedView", share);
                }
            }

            ViewBag.Error = "Mật khẩu không đúng";
            ViewBag.Token = token;
            return View("SharedPassword");
        }

        // GET: Download file từ link chia sẻ
        [HttpPost] // THÊM [HttpPost]
        public ActionResult DownloadShared(string token, string password)
        {
            System.Diagnostics.Debug.WriteLine("DownloadShared - Token: " + token + ", Password: " + password);

            if (string.IsNullOrEmpty(token))
            {
                return Content("Link không hợp lệ - Token rỗng");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT sl.*, f.FilePath, f.FileName 
                        FROM SharedLinks sl
                        JOIN Files f ON sl.FileID = f.ID
                        WHERE sl.Token = @Token 
                        AND (sl.ExpiryDate IS NULL OR sl.ExpiryDate > GETDATE())";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                string filePath = "";
                string fileName = "";
                string dbPassword = "";

                if (reader.Read())
                {
                    filePath = reader["FilePath"].ToString();
                    fileName = reader["FileName"].ToString();
                    dbPassword = reader["Password"] == DBNull.Value ? null : reader["Password"].ToString();
                }

                reader.Close();

                if (string.IsNullOrEmpty(filePath))
                {
                    return Content("Link không hợp lệ hoặc đã hết hạn");
                }

                // Kiểm tra password
                if (!string.IsNullOrEmpty(dbPassword) && password != dbPassword)
                {
                    return Content("Mật khẩu không đúng");
                }

                // Tăng lượt download
                string updateQuery = "UPDATE SharedLinks SET DownloadCount = DownloadCount + 1 WHERE Token = @Token";
                SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                updateCmd.Parameters.AddWithValue("@Token", token);
                updateCmd.ExecuteNonQuery();

                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
            }
        }

        [HttpPost]
        public ActionResult ChangeFolderColor(int folderId, string color)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Folders SET FolderColor = @Color WHERE ID = @FolderID AND UserID = @UserID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Color", color);
                cmd.Parameters.AddWithValue("@FolderID", folderId);
                cmd.Parameters.AddWithValue("@UserID", userID);

                conn.Open();
                int rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                {
                    return Json(new { success = true });
                }
            }

            return Json(new { success = false });
        }

    }
}