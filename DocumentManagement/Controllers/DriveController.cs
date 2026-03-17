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
        public ActionResult Index(int? folderId)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];
            var model = new DriveViewModel
            {
                Folders = GetFolders(userID, folderId),
                Files = GetFiles(userID, folderId),
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

        private List<FileModel> GetFiles(int userID, int? folderId)
        {
            var files = new List<FileModel>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "";

                if (folderId == null || folderId == 0)
                {
                    // Trường hợp ở root: lấy file có FolderID IS NULL
                    query = @"SELECT * FROM Files 
                      WHERE UserID = @UserID 
                      AND IsDeleted = 0 
                      AND FolderID IS NULL";
                }
                else
                {
                    // Trường hợp ở trong folder: lấy file có FolderID = @FolderID
                    query = @"SELECT * FROM Files 
                      WHERE UserID = @UserID 
                      AND IsDeleted = 0 
                      AND FolderID = @FolderID";
                }

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserID", userID);

                if (folderId != null && folderId != 0)
                {
                    cmd.Parameters.AddWithValue("@FolderID", folderId);
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
    }
}