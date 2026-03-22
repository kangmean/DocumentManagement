using System;
using System.Collections.Generic;

namespace DocumentManagement.Models
{
    public class AdminDashboardViewModel
    {
        // Thống kê tổng quan
        public int TotalUsers { get; set; }
        public int TotalFiles { get; set; }
        public long TotalStorage { get; set; }
        public int TotalShares { get; set; }
        public int TrashFiles { get; set; }

        // Top 5 user upload nhiều nhất
        public List<TopUserModel> TopUsers { get; set; }

        // Top 5 file được download nhiều nhất (từ SharedLinks)
        public List<TopFileModel> TopFiles { get; set; }

        // Recent files (10 file mới nhất)
        public List<FileModel> RecentFiles { get; set; }

        // Thống kê theo loại file
        public Dictionary<string, int> FileTypeStats { get; set; }

        // Thống kê upload theo ngày (7 ngày gần nhất)
        public Dictionary<string, int> UploadStats { get; set; }
    }

    public class TopUserModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public string FormattedSize => FormatSize(TotalSize);

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024) + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)) + " MB";
            return (bytes / (1024 * 1024 * 1024)) + " GB";
        }
    }

    public class TopFileModel
    {
        public int FileID { get; set; }
        public string FileName { get; set; }
        public int DownloadCount { get; set; }
        public string UploaderName { get; set; }
    }
}