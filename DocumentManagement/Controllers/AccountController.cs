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
    }
}