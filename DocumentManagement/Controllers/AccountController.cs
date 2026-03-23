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
                        // THÊM DÒNG DEBUG NÀY
                        int userID = (int)reader["ID"];
                        System.Diagnostics.Debug.WriteLine("=== ĐĂNG NHẬP ===");
                        System.Diagnostics.Debug.WriteLine("UserID đăng nhập: " + userID);
                        System.Diagnostics.Debug.WriteLine("Username: " + reader["Username"]);

                        // Lưu session
                        Session["UserID"] = userID;
                        Session["Username"] = reader["Username"];
                        Session["IsAdmin"] = reader["IsAdmin"];

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
    }
}