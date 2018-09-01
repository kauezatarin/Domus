using System;

namespace DomusSharedClasses
{
    [Serializable]
    public class User
    {
        public User(string username, string email, string name, string lastName, bool isAdmin, bool isActive, string createdAt, string lastLogin, string password = null, int userId = 0)
        {
            this.Username = username;
            this.Email = email;
            this.Name = name;
            this.LastName = lastName;
            this.IsAdmin = isAdmin;
            this.IsActive = isActive;
            this.CreatedAt = createdAt;
            this.LastLogin = lastLogin;
            this.Password = password;
            this.UserId = userId;
        }

        public int UserId { get; private set; }

        public string Username { get; set; }

        public string Email { get; set; }

        public string Name { get; set; }

        public string LastName { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsActive { get; set; }

        public string CreatedAt { get; set; }

        public string LastLogin { get; set; }

        public string Password { get; set; }

    }
}
