using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DocumentManagement.Models
{
    public class User
    {
        public int ID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalStorage { get; set; }
        public int UsedStorage { get; set; }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class FileModel
    {
        public int ID { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int FileSize { get; set; }
        public string FileType { get; set; }
        public int UserID { get; set; }
        public int? FolderID { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsStarred { get; set; }

        // Hiển thị
        public string FormattedSize
        {
            get
            {
                if (FileSize < 1024) return FileSize + " B";
                if (FileSize < 1024 * 1024) return (FileSize / 1024) + " KB";
                if (FileSize < 1024 * 1024 * 1024) return (FileSize / (1024 * 1024)) + " MB";
                return (FileSize / (1024 * 1024 * 1024)) + " GB";
            }
        }
    }

    public class FolderModel
    {
        public int ID { get; set; }
        public string FolderName { get; set; }
        public int UserID { get; set; }
        public int? ParentFolderID { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string FolderColor { get; set; }
    }

    public class DriveViewModel
    {
        public List<FolderModel> Folders { get; set; }
        public List<FileModel> Files { get; set; }
        public int CurrentFolderID { get; set; }
        public string CurrentFolderName { get; set; }
        public List<FolderModel> Breadcrumb { get; set; }
    }

    public class UploadViewModel
    {
        public HttpPostedFileBase[] Files { get; set; }
        public int? FolderID { get; set; }
    }

}