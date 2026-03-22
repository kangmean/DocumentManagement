using System;
using System.Data.SqlClient;
using System.Configuration;
using System.Web;

namespace DocumentManagement.Models
{
    public static class LogHelper
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public static void Log(int userId, string userName, string action, string fileName, int fileSize, string ipAddress)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO ActivityLog (UserID, UserName, Action, FileName, FileSize, Time, IPAddress)
                                    VALUES (@UserID, @UserName, @Action, @FileName, @FileSize, GETDATE(), @IPAddress)";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    cmd.Parameters.AddWithValue("@Action", action);
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.Parameters.AddWithValue("@FileSize", fileSize);
                    cmd.Parameters.AddWithValue("@IPAddress", ipAddress);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Log error: " + ex.Message);
            }
        }

        public static string GetClientIP()
        {
            string ip = HttpContext.Current.Request.UserHostAddress;

            if (HttpContext.Current.Request.Headers["X-Forwarded-For"] != null)
            {
                ip = HttpContext.Current.Request.Headers["X-Forwarded-For"];
            }

            return ip ?? "Unknown";
        }
    }
}