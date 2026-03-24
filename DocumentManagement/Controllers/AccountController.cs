using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DocumentManagement.Models;
using System.Data.SqlClient;
using System.Configuration;

namespace DocumentManagement.Controllers
{
    public class AccountController : Controller
    {
        string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        public ActionResult Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT * FROM Users WHERE Username = @Username AND Password = @Password AND IsBlocked = 0";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Username", model.Username);
                    cmd.Parameters.AddWithValue("@Password", model.Password);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        int userID = (int)reader["ID"];
                        bool isAdmin = (bool)reader["IsAdmin"];

                        System.Diagnostics.Debug.WriteLine("=== ĐĂNG NHẬP ===");
                        System.Diagnostics.Debug.WriteLine("UserID đăng nhập: " + userID);
                        System.Diagnostics.Debug.WriteLine("Username: " + reader["Username"]);
                        System.Diagnostics.Debug.WriteLine("IsAdmin: " + isAdmin);

                        // Lưu session
                        Session["UserID"] = userID;
                        Session["Username"] = reader["Username"];
                        Session["IsAdmin"] = isAdmin;

                        // Nếu là admin, chuyển đến Dashboard
                        if (isAdmin)
                        {
                            return RedirectToAction("Dashboard", "Admin");
                        }

                        // Nếu là user thường, chuyển đến My Drive
                        return RedirectToAction("Index", "Drive");
                    }
                    else
                    {
                        ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản đã bị khóa!";
                    }
                }
            }
            return View(model);
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        // GET: Thông tin tài khoản
        public ActionResult Profile()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userID = (int)Session["UserID"];
            User user = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Users WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", userID);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    user = new User
                    {
                        ID = (int)reader["ID"],
                        Username = reader["Username"].ToString(),
                        FullName = reader["FullName"]?.ToString(),
                        Email = reader["Email"]?.ToString(),
                        IsBlocked = (bool)reader["IsBlocked"],
                        IsAdmin = (bool)reader["IsAdmin"],
                        CreatedAt = (DateTime)reader["CreatedAt"],
                        TotalStorage = (int)reader["TotalStorage"],
                        UsedStorage = (int)reader["UsedStorage"]
                    };
                }
            }

            return View(user);
        }

        // POST: Cập nhật thông tin
        [HttpPost]
        public ActionResult UpdateProfile(string fullName, string email)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "UPDATE Users SET FullName = @FullName, Email = @Email WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@ID", userID);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return Json(new { success = true, message = "Cập nhật thành công" });
        }

        // POST: Đổi mật khẩu
        [HttpPost]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" });
            }

            int userID = (int)Session["UserID"];

            // Kiểm tra mật khẩu hiện tại
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT Password FROM Users WHERE ID = @ID";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ID", userID);
                conn.Open();
                string dbPassword = cmd.ExecuteScalar()?.ToString();

                if (dbPassword != currentPassword)
                {
                    return Json(new { success = false, message = "Mật khẩu hiện tại không đúng" });
                }

                if (newPassword != confirmPassword)
                {
                    return Json(new { success = false, message = "Mật khẩu mới không khớp" });
                }

                if (newPassword.Length < 3)
                {
                    return Json(new { success = false, message = "Mật khẩu phải có ít nhất 3 ký tự" });
                }

                // Cập nhật mật khẩu
                query = "UPDATE Users SET Password = @Password WHERE ID = @ID";
                cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Password", newPassword);
                cmd.Parameters.AddWithValue("@ID", userID);
                cmd.ExecuteNonQuery();
            }

            return Json(new { success = true, message = "Đổi mật khẩu thành công" });
        }

        // GET: Đăng ký
        public ActionResult Register()
        {
            return View();
        }

        // POST: Đăng ký
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Kiểm tra username đã tồn tại
                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
                    SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@Username", model.Username);
                    conn.Open();
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count > 0)
                    {
                        ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại");
                        return View(model);
                    }

                    // Thêm user mới (mặc định IsAdmin = 0, IsBlocked = 0)
                    string query = @"INSERT INTO Users (Username, Password, FullName, Email, IsAdmin, IsBlocked, CreatedAt, TotalStorage, UsedStorage)
                             VALUES (@Username, @Password, @FullName, @Email, 0, 0, GETDATE(), 15360, 0)";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Username", model.Username);
                    cmd.Parameters.AddWithValue("@Password", model.Password);
                    cmd.Parameters.AddWithValue("@FullName", string.IsNullOrEmpty(model.FullName) ? (object)DBNull.Value : model.FullName);
                    cmd.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(model.Email) ? (object)DBNull.Value : model.Email);

                    cmd.ExecuteNonQuery();

                    ViewBag.Success = "Đăng ký thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
            }
            return View(model);
        }

        // GET: Quên mật khẩu
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Quên mật khẩu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Users WHERE Username = @Username AND Email = @Email";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Username", model.Username);
                    cmd.Parameters.AddWithValue("@Email", model.Email);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();

                    if (count > 0)
                    {
                        // Tạo mật khẩu mới ngẫu nhiên
                        string newPassword = GenerateRandomPassword();

                        string updateQuery = "UPDATE Users SET Password = @Password WHERE Username = @Username";
                        SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                        updateCmd.Parameters.AddWithValue("@Password", newPassword);
                        updateCmd.Parameters.AddWithValue("@Username", model.Username);
                        updateCmd.ExecuteNonQuery();

                        ViewBag.Success = $"Mật khẩu mới của bạn là: {newPassword}. Vui lòng đăng nhập và đổi mật khẩu.";
                    }
                    else
                    {
                        ViewBag.Error = "Tên đăng nhập hoặc email không chính xác";
                    }
                }
            }
            return View(model);
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}